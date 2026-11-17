// Test thuần chạy được trên Linux (mcs + mono), không cần Tekla.
using System;
using System.Linq;
using MeshFrame.Core;

static class PlanTests
{
    static int fail = 0, pass = 0;
    static void Check(string name, bool ok) { if (ok) pass++; else { fail++; Console.WriteLine("FAIL: " + name); } }
    static bool Near(double a, double b, double tol) { return Math.Abs(a - b) <= tol; }
    static V2[] Rect(double L, double H, double skew) { return new[] { new V2(0, 0), new V2(L, 0), new V2(L + skew, H), new V2(skew, H) }; }

    static int Main()
    {
        // --- khung 4000 x 1200, gia cường Equal max 1000 -> 4 ô, 3 thanh
        var s = new MeshFrameSettings();
        var p = MeshFramePlanner.Plan(s, Rect(4000, 1200, 0));
        Check("hop le", p.Errors.Count == 0);
        Check("4 o", p.Cells.Count == 4 && p.Cells.All(c => Near(c, 1000, 1e-6)));
        Check("bars = 4 khung + 3 gia cuong", p.Bars.Count == 7 && p.Bars.Count(b => b.Role == BarRole.Stiffener) == 3);
        Check("nep = 4 + 3", p.Strips.Count == 7);
        double a = Math.Abs(Geo.Area(p.Mesh.Polygon));
        Check("dien tich luoi = (4000-16)*(1200-16)", Near(a, 3984.0 * 1184.0, 0.01));
        Check("luoi lui: dinh dau", Near(p.Mesh.Polygon[0].X, 8, 1e-6) && Near(p.Mesh.Polygon[0].Y, 8, 1e-6));
        Check("do day luoi = 2*0.65", Near(p.Mesh.Thickness, 1.3, 1e-9));
        var st = p.Bars.First(b => b.Role == BarRole.Stiffener);
        Check("gia cuong thang dung, tim x=1000", Near(st.Start.X, 1000, 1e-6) && Near(st.End.X, 1000, 1e-6));
        Check("gia cuong tu mat trong V (y=3 -> 1197)", Near(st.Start.Y, 3, 1e-6) && Near(st.End.Y, 1197, 1e-6));
        var ns = p.Strips.First(x => x.OnStiffener);
        Check("nep gia cuong tu mep trong nep chu vi (y=33 -> 1167)", Near(ns.Start.Y, 33, 1e-6) && Near(ns.End.Y, 1167, 1e-6));
        Check("nep sat V: tim nep chu vi cach mep 3+15", Near(p.Strips[0].Start.Y, 18, 1e-6) || Near(p.Strips[0].Start.Y, 18, 1e-6));
        Check("vit cach nhau <= 250", MaxGap(p) <= 250.0 + 1e-6);

        // --- danh sach o tuy y
        s = new MeshFrameSettings { StiffMode = StiffMode.Custom, StiffCells = "1000 2*1500" };
        p = MeshFramePlanner.Plan(s, Rect(4000, 1200, 0));
        Check("custom 3 o", p.Errors.Count == 0 && p.Cells.Count == 3 && Near(p.Cells[1], 1500, 1e-9));
        Check("custom 2 thanh x=1000,2500", Near(p.Bars.Where(b => b.Role == BarRole.Stiffener).Select(b => b.Start.X).Sum(), 3500, 1e-6));
        s = new MeshFrameSettings { StiffMode = StiffMode.Custom, StiffCells = "1000 500" };
        p = MeshFramePlanner.Plan(s, Rect(4000, 1200, 0));
        Check("con du them o cuoi + canh bao", p.Cells.Count == 3 && Near(p.Cells[2], 2500, 1e-6) && p.Warnings.Count >= 1);
        s = new MeshFrameSettings { StiffMode = StiffMode.Custom, StiffCells = "3000 2000" };
        p = MeshFramePlanner.Plan(s, Rect(4000, 1200, 0));
        Check("tong o > khung bao loi", p.Errors.Count == 1);

        // --- huong chia theo P1-P4
        s = new MeshFrameSettings { StiffMode = StiffMode.Equal, StiffMax = 600, StiffDir = StiffDir.AcrossP1P4 };
        p = MeshFramePlanner.Plan(s, Rect(4000, 1200, 0));
        Check("chia theo H: 2 o, 1 thanh ngang", p.Cells.Count == 2 && p.Bars.Count(b => b.Role == BarRole.Stiffener) == 1
              && Near(p.Bars.Last().Start.Y, p.Bars.Last().End.Y, 1e-6));

        // --- khung khong vuong (hinh thang / binh hanh)
        s = new MeshFrameSettings();
        p = MeshFramePlanner.Plan(s, Rect(4000, 1200, 300));
        Check("binh hanh hop le", p.Errors.Count == 0 && p.Bars.Count == 7);
        var sb = p.Bars.First(b => b.Role == BarRole.Stiffener);
        Check("gia cuong VUONG GOC day/tren du khung lech", Near(sb.Start.X, sb.End.X, 1e-6));
        Check("gia cuong vuong goc canh goc (tich vo huong = 0)", Math.Abs((sb.End - sb.Start).Unit().Dot(new V2(1, 0))) < 1e-9);
        Check("khung lech qua lon: bao loi", MeshFramePlanner.Plan(new MeshFrameSettings(), Rect(4000, 1200, 2000)).Errors.Count == 1);
        Check("dien tich luoi binh hanh < khung", Math.Abs(Geo.Area(p.Mesh.Polygon)) < 4000.0 * 1200.0);

        // --- khong nep: khong nep, khong vit, gia cuong van co
        s = new MeshFrameSettings { UseStrip = false };
        p = MeshFramePlanner.Plan(s, Rect(4000, 1200, 0));
        Check("khong nep => khong nep & khong vit", p.Strips.Count == 0 && p.Fasteners.Count == 0 && p.Bars.Count == 7);

        // --- khung nho khong can gia cuong
        s = new MeshFrameSettings();
        p = MeshFramePlanner.Plan(s, Rect(800, 600, 0));
        Check("khung <= max: khong gia cuong", p.Cells.Count == 1 && p.Bars.Count == 4);

        // --- cat vat vs chay suot
        s = new MeshFrameSettings { StiffMode = StiffMode.None };
        p = MeshFramePlanner.Plan(s, Rect(4000, 1200, 0));
        Check("miter co huong cat", p.Bars.All(b => b.CutStart.HasValue && b.CutEnd.HasValue));
        s.Corner = CornerKind.OneThrough;
        p = MeshFramePlanner.Plan(s, Rect(4000, 1200, 0));
        Check("OneThrough: canh 0 chay suot 0..4000, canh 1 ngan lai",
              Near(p.Bars[0].Start.X, 0, 1e-6) && Near(p.Bars[0].End.X, 4000, 1e-6) && Near(p.Bars[1].Start.Y, 3, 1e-6) && Near(p.Bars[1].End.Y, 1197, 1e-6));

        // --- loi
        Check("khung qua nho", MeshFramePlanner.Plan(new MeshFrameSettings(), Rect(30, 30, 0)).Errors.Count > 0);
        Check("diem pick sai thu tu", MeshFramePlanner.Plan(new MeshFrameSettings(), new[] { new V2(0, 0), new V2(100, 100), new V2(100, 0), new V2(0, 100) }).Errors.Count > 0);

        // --- Builder 3D + Frame
        var corners = new[] { new V3(0, 0, 5000), new V3(4000, 0, 5000), new V3(4000, 1200, 5000), new V3(0, 1200, 5000) };
        var r = MeshFrameBuilder.Build(new MeshFrameSettings(), corners, null);
        Check("builder 3D", r.Success && Near(r.Frame.N.Z, 1, 1e-9));
        var slope = new[] { new V3(0, 0, 0), new V3(4000, 0, 0), new V3(4000, 1200, 120), new V3(0, 1200, 120) };
        r = MeshFrameBuilder.Build(new MeshFrameSettings(), slope, null);
        Check("mai doc hop le", r.Success);
        var bad = new[] { new V3(0, 0, 0), new V3(4000, 0, 0), new V3(4000, 1200, 500), new V3(0, 1200, 0) };
        Check("khong dong phang bao loi", !MeshFrameBuilder.Build(new MeshFrameSettings(), bad, null).Success);

        Console.WriteLine("pass " + pass + " fail " + fail);
        return fail == 0 ? 0 : 1;
    }

    static double MaxGap(MeshFramePlan p)
    {
        // khoảng cách vít liên tiếp trên từng nẹp
        double mx = 0;
        foreach (var st in p.Strips)
        {
            var pts = p.Fasteners.Where(f => Math.Abs((f.Pos - st.Start).Cross((st.End - st.Start).Unit())) < 1e-6).OrderBy(f => (f.Pos - st.Start).Length).ToList();
            for (int i = 1; i < pts.Count; i++) mx = Math.Max(mx, (pts[i].Pos - pts[i - 1].Pos).Length);
        }
        return mx;
    }
}
