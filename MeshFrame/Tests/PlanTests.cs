// Test thuần chạy được trên Linux (mcs + mono), không cần Tekla. Theo chuẩn 05.TC.01 REV04 (V-25x2, PL-25x1.2, vít SC06@250, @ tối đa 1000).
using System;
using System.Linq;
using MeshFrame.Core;

static class PlanTests
{
    static int fail = 0, pass = 0;
    static void Check(string name, bool ok) { if (ok) pass++; else { fail++; Console.WriteLine("FAIL: " + name); } }
    static bool Near(double a, double b, double tol) { return Math.Abs(a - b) <= tol; }
    static V2[] Rect(double L, double H, double skew) { return new[] { new V2(0, 0), new V2(L, 0), new V2(L + skew, H), new V2(skew, H) }; }
    static MeshFramePlan Plan(MeshFrameSettings s, double L, double H, double skew) { return MeshFramePlanner.Plan(s, Rect(L, H, skew)); }
    static double Area(V2[] q) { return Math.Abs(Geo.Area(q)); }

    static int Main()
    {
        // --- giá trị mặc định theo chuẩn
        var d = new MeshFrameSettings();
        Check("mac dinh theo chuan: V25x25x2, PL25x1.2, vit 6@250, @1000",
              Near(d.VLegA, 25, 0) && Near(d.VLegB, 25, 0) && Near(d.VThk, 2, 0) && Near(d.StripWidth, 25, 0) && Near(d.StripThk, 1.2, 0)
              && Near(d.FastenDia, 6, 0) && Near(d.FastenPitch, 250, 0) && Near(d.StiffMax, 1000, 0));

        // --- khung 4000 x 1200, gia cường chia đều tối đa 1000 -> 4 ô, 3 thanh
        var s = new MeshFrameSettings();
        var p = Plan(s, 4000, 1200, 0);
        Check("hop le", p.Errors.Count == 0);
        Check("4 o", p.Cells.Count == 4 && p.Cells.All(c => Near(c, 1000, 1e-6)));
        Check("bars = 4 khung + 3 gia cuong", p.Bars.Count == 7 && p.Bars.Count(b => b.Role == BarRole.Stiffener) == 3);
        Check("nep = 4 + 3", p.Strips.Count == 7);
        Check("luoi phu den mep ngoai khung (gap 0): dien tich = 4000*1200", Near(Area(p.Mesh.Polygon), 4000.0 * 1200.0, 0.01));
        Check("do day luoi = 2*0.37", Near(p.Mesh.Thickness, 0.74, 1e-9));

        // --- thứ tự lớp theo chuẩn: V (z 0..t) -> lưới (z -0.74..0) -> nẹp (ngoài lưới) -> đầu vít
        Check("luoi nam duoi canh nam V (phia ngoai)", Near(p.Mesh.ZBottom, -0.74, 1e-9) && p.Mesh.ZBottom < 0);
        Check("nep ngoai luoi: ZRef = -meshT", p.Strips.All(x => Near(x.ZRef, -0.74, 1e-9)));
        Check("dau vit o mat ngoai nep: -0.74-1.2", p.Fasteners.All(f => Near(f.ZHead, -1.94, 1e-9)) && p.Fasteners.Count > 0);
        Check("chieu dai vit = V + luoi + nep", p.Fasteners.All(f => Near(f.Length, 2 + 0.74 + 1.2, 1e-9)));
        Check("V khung tham chieu o goc ngoai z=0", p.Bars.All(b => Near(b.ZRef, 0, 1e-12)));

        // --- thanh gia cường: cùng tiết diện, vuông góc, cánh nằm về +x
        var st = p.Bars.First(b => b.Role == BarRole.Stiffener);
        Check("gia cuong thang dung tai x=1000", Near(st.Start.X, 1000, 1e-6) && Near(st.End.X, 1000, 1e-6));
        Check("gia cuong dung o mat trong canh dung V khung (y 2..1198)", Near(Math.Min(st.Start.Y, st.End.Y), 2, 1e-6) && Near(Math.Max(st.Start.Y, st.End.Y), 1198, 1e-6));
        Check("huong Start->End sao cho canh nam (ben trai) ve +x", Near((st.End - st.Start).Unit().Left().X, 1, 1e-9));
        var fr = p.Bars.First(b => b.Role == BarRole.Frame);
        Check("V gia cuong CUNG TIET DIEN V khung", st.Profile == fr.Profile && Near(st.LegA, fr.LegA, 0) && Near(st.LegB, fr.LegB, 0) && Near(st.Thk, fr.Thk, 0));

        // --- cắt khuyết hai đầu gia cường (gia công)
        Check("moi gia cuong co 2 khuyet", p.Bars.Where(b => b.Role == BarRole.Stiffener).All(b => b.Notches.Count == 2));
        Check("khung khong co khuyet", p.Bars.Where(b => b.Role == BarRole.Frame).All(b => b.Notches.Count == 0));
        var n0 = st.Notches[0]; var n1 = st.Notches[1];
        Check("khuyet: x 999..1025 (lech -1 den legA=25), dien tich 26*24 moi cai",
              n0.All(v => v.X >= 999 - 1e-6 && v.X <= 1025 + 1e-6) && n1.All(v => v.X >= 999 - 1e-6 && v.X <= 1025 + 1e-6) && Near(Area(n0), 26.0 * 24.0, 1e-6) && Near(Area(n1), 26.0 * 24.0, 1e-6));
        Check("hai khuyet: mot o day (y 1..25), mot o tren (y 1175..1199)",
              (Near(n0.Min(v => v.Y), 1, 1e-6) && Near(n0.Max(v => v.Y), 25, 1e-6) && Near(n1.Min(v => v.Y), 1175, 1e-6) && Near(n1.Max(v => v.Y), 1199, 1e-6))
              || (Near(n1.Min(v => v.Y), 1, 1e-6) && Near(n1.Max(v => v.Y), 25, 1e-6) && Near(n0.Min(v => v.Y), 1175, 1e-6) && Near(n0.Max(v => v.Y), 1199, 1e-6)));
        Check("khuyet nam trong vung chong V khung (offset tV/2..legA), khong cham mep ngoai",
              p.Bars.Where(b => b.Role == BarRole.Stiffener).All(b => b.Notches.All(q => q.All(v => v.Y >= 1 - 1e-6 && v.Y <= 1199 + 1e-6))));

        // --- nẹp: PL phủ offset [0, w] từ mép ngoài; nẹp gia cường dưới cánh nằm gia cường
        Check("nep chu vi: tim cach mep ngoai w/2 = 12.5", Near(p.Strips[0].Start.Y, 12.5, 1e-6) && Near(p.Strips[0].End.Y, 12.5, 1e-6));
        var ns = p.Strips.First(x => x.OnStiffener);
        Check("nep gia cuong: tim x = 1000 + 12.5 (nam duoi canh nam)", Near(ns.Start.X, 1012.5, 1e-6) && Near(ns.End.X, 1012.5, 1e-6));
        Check("nep gia cuong dung o mep trong nep chu vi (y 25..1175)", Near(Math.Min(ns.Start.Y, ns.End.Y), 25, 1e-6) && Near(Math.Max(ns.Start.Y, ns.End.Y), 1175, 1e-6));
        Check("vit cach nhau <= 250", MaxGap(p) <= 250.0 + 1e-6);

        // --- danh sách ô tùy ý
        s = new MeshFrameSettings { StiffMode = StiffMode.Custom, StiffCells = "1000 2*1500" };
        p = Plan(s, 4000, 1200, 0);
        Check("custom 3 o", p.Errors.Count == 0 && p.Cells.Count == 3 && Near(p.Cells[1], 1500, 1e-9));
        Check("custom 2 thanh x=1000,2500", Near(p.Bars.Where(b => b.Role == BarRole.Stiffener).Select(b => b.Start.X).Sum(), 3500, 1e-6));
        s = new MeshFrameSettings { StiffMode = StiffMode.Custom, StiffCells = "1000 500" };
        p = Plan(s, 4000, 1200, 0);
        Check("con du them o cuoi + canh bao", p.Cells.Count == 3 && Near(p.Cells[2], 2500, 1e-6) && p.Warnings.Count >= 1);
        p = Plan(new MeshFrameSettings { StiffMode = StiffMode.Custom, StiffCells = "3000 2000" }, 4000, 1200, 0);
        Check("tong o > khung bao loi", p.Errors.Count == 1);

        // --- hướng chia theo P1-P4
        s = new MeshFrameSettings { StiffMode = StiffMode.Equal, StiffMax = 600, StiffDir = StiffDir.AcrossP1P4 };
        p = Plan(s, 4000, 1200, 0);
        Check("chia theo H: 2 o, 1 thanh ngang", p.Cells.Count == 2 && p.Bars.Count(b => b.Role == BarRole.Stiffener) == 1
              && Near(p.Bars.Last().Start.Y, p.Bars.Last().End.Y, 1e-6));
        var hs = p.Bars.Last();
        Check("thanh ngang: canh nam (ben trai) ve +y", Near((hs.End - hs.Start).Unit().Left().Y, 1, 1e-9));
        Check("thanh ngang co 2 khuyet", hs.Notches.Count == 2);

        // --- khung không vuông
        p = Plan(new MeshFrameSettings(), 4000, 1200, 300);
        Check("binh hanh hop le", p.Errors.Count == 0 && p.Bars.Count == 7);
        var sb = p.Bars.First(b => b.Role == BarRole.Stiffener);
        Check("gia cuong VUONG GOC day/tren du khung lech", Near(sb.Start.X, sb.End.X, 1e-6));
        Check("khung lech qua lon: bao loi", Plan(new MeshFrameSettings(), 4000, 1200, 2000).Errors.Count == 1);
        Check("dien tich luoi binh hanh = khung", Near(Area(p.Mesh.Polygon), 4000.0 * 1200.0, 0.01));

        // --- không nẹp: không nẹp, không vít, gia cường vẫn có
        p = Plan(new MeshFrameSettings { UseStrip = false }, 4000, 1200, 0);
        Check("khong nep => khong nep & khong vit", p.Strips.Count == 0 && p.Fasteners.Count == 0 && p.Bars.Count == 7);
        p = Plan(new MeshFrameSettings(), 800, 600, 0);
        Check("khung <= max: khong gia cuong", p.Cells.Count == 1 && p.Bars.Count == 4);

        // --- cắt vát vs chạy suốt
        p = Plan(new MeshFrameSettings { StiffMode = StiffMode.None }, 4000, 1200, 0);
        Check("miter co huong cat", p.Bars.All(b => b.CutStart.HasValue && b.CutEnd.HasValue));
        p = Plan(new MeshFrameSettings { StiffMode = StiffMode.None, Corner = CornerKind.OneThrough }, 4000, 1200, 0);
        Check("OneThrough: canh 0 chay suot 0..4000, canh 1 ngan lai (y 2..1198)",
              Near(p.Bars[0].Start.X, 0, 1e-6) && Near(p.Bars[0].End.X, 4000, 1e-6) && Near(p.Bars[1].Start.Y, 2, 1e-6) && Near(p.Bars[1].End.Y, 1198, 1e-6));

        // --- lỗi đầu vào
        Check("khung qua nho", Plan(new MeshFrameSettings(), 30, 30, 0).Errors.Count > 0);
        Check("diem pick sai thu tu", MeshFramePlanner.Plan(new MeshFrameSettings(), new[] { new V2(0, 0), new V2(100, 100), new V2(100, 0), new V2(0, 100) }).Errors.Count > 0);

        // --- giới hạn của chuẩn
        Check("4000x1200 khong canh bao gioi han", !Plan(new MeshFrameSettings(), 4000, 1200, 0).Warnings.Any(w => w.Contains("tối đa")));
        Check("L > 4000 canh bao", Plan(new MeshFrameSettings(), 4500, 1200, 0).Warnings.Any(w => w.Contains("4000")));
        Check("H > 1200 canh bao", Plan(new MeshFrameSettings(), 4000, 1300, 0).Warnings.Any(w => w.Contains("1200")));
        Check("o > 1000 canh bao", Plan(new MeshFrameSettings { StiffMode = StiffMode.Custom, StiffCells = "1500 1500" }, 4000, 1200, 0).Warnings.Any(w => w.Contains("1000")));
        Check("nep rong hon canh V canh bao", Plan(new MeshFrameSettings { StripWidth = 30 }, 4000, 1200, 0).Warnings.Any(w => w.Contains("cánh nằm V")));

        // --- tiết diện nhập tùy ý: gia cường theo V khung; nẹp theo ô nhập
        p = Plan(new MeshFrameSettings { VLegA = 30, VLegB = 30, VThk = 2.5, StripWidth = 30, StripThk = 2 }, 4000, 1200, 0);
        Check("V 30x30x2.5: khung + gia cuong cung tiet dien", p.Bars.All(b => b.Profile == "L30*30*2.5"));
        Check("nep tuy y 30x2", p.Strips.All(x => Near(x.Width, 30, 0) && Near(x.Thk, 2, 0) && Near(x.ZRef, -0.74, 1e-9)) && p.Fasteners.All(f => Near(f.ZHead, -2.74, 1e-9)));
        Check("khuyet theo cánh 30: x tu 999 den 1030", Near(p.Bars.First(b => b.Role == BarRole.Stiffener).Notches[0].Max(v => v.X), 1030, 1e-6));

        // --- profile ghi đè, hướng đặt, cảnh báo chữ có dấu
        s = new MeshFrameSettings { VProfile = "L50x50x3", StripProfile = "PL3*30" };
        p = Plan(s, 4000, 1200, 0);
        Check("profile ghi de: V khung + gia cuong + nep", p.Bars.All(b => b.Profile == "L50x50x3") && p.Strips.All(x => x.Profile == "PL3*30"));
        Check("huong dat mac dinh dang ten enum Tekla", p.Placement.VPlane == "MIDDLE" && p.Placement.VRotation == "FRONT" && p.Placement.VDepth == "MIDDLE" && p.Placement.StripPlane == "MIDDLE" && p.Placement.StripDepth == "MIDDLE" && p.Placement.BoltStandard == "8.8XOX");
        p = Plan(new MeshFrameSettings { VPlane = PlaneKind.Right, VRotation = RotationKind.Below, VDepth = DepthKind.Middle, FlipMiterCut = true, FlipBolt = true }, 4000, 1200, 0);
        Check("huong dat doi theo thong so", p.Placement.VPlane == "RIGHT" && p.Placement.VRotation == "BELOW" && p.Placement.VDepth == "MIDDLE" && p.Placement.FlipMiterCut && p.Placement.FlipBolt);
        p = Plan(new MeshFrameSettings { VName = "KHUNG LƯỚI", MeshName = "Lưới" }, 4000, 1200, 0);
        Check("chu co dau -> canh bao", p.Warnings.Any(w => w.Contains("VName") && w.Contains("MeshName")));
        Check("mac dinh khong canh bao ASCII", !Plan(new MeshFrameSettings(), 4000, 1200, 0).Warnings.Any(w => w.Contains("ASCII")));

        // --- kiểu lưới
        Check("mac dinh luoi dang tam mong", Plan(new MeshFrameSettings(), 4000, 1200, 0).Mesh.Kind == MeshKind.Plate);
        Check("RebarMesh duoc chon", Plan(new MeshFrameSettings { MeshKind = MeshKind.RebarMesh }, 4000, 1200, 0).Mesh.Kind == MeshKind.RebarMesh);
        p = Plan(new MeshFrameSettings { MeshKind = MeshKind.None }, 4000, 1200, 0);
        Check("khong tao luoi: Mesh = null, nep + vit van co", p.Mesh == null && p.Strips.Count == 7 && p.Fasteners.Count > 0 && Near(p.Strips[0].ZRef, -0.74, 1e-9));
        p = Plan(new MeshFrameSettings { VLateral = 1.5, StripLateral = -2 }, 4000, 1200, 0);
        Check("lech ngang chuyen sang placement", Near(p.Placement.VLateral, 1.5, 0) && Near(p.Placement.StripLateral, -2, 0));

        // --- Builder 3D + Frame
        var corners = new[] { new V3(0, 0, 5000), new V3(4000, 0, 5000), new V3(4000, 1200, 5000), new V3(0, 1200, 5000) };
        var r = MeshFrameBuilder.Build(new MeshFrameSettings(), corners, null);
        Check("builder 3D", r.Success && Near(r.Frame.N.Z, 1, 1e-9));
        var slope = new[] { new V3(0, 0, 0), new V3(4000, 0, 0), new V3(4000, 1200, 120), new V3(0, 1200, 120) };
        Check("mai doc hop le", MeshFrameBuilder.Build(new MeshFrameSettings(), slope, null).Success);
        var bad = new[] { new V3(0, 0, 0), new V3(4000, 0, 0), new V3(4000, 1200, 500), new V3(0, 1200, 0) };
        Check("khong dong phang bao loi", !MeshFrameBuilder.Build(new MeshFrameSettings(), bad, null).Success);

        // --- lưu / nạp thông số
        var cfg = new MeshFrameSettings { VLegA = 40.5, StiffMode = StiffMode.Custom, StiffCells = "800 2*1500", UseStrip = false, Fasten = FastenKind.SpotWeld, MeshName = "TEST", StripThk = 1.5 };
        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "meshframe_test_" + Guid.NewGuid().ToString("N") + ".ini");
        SettingsStore.Save(cfg, tmp);
        var back = SettingsStore.Load(tmp);
        System.IO.File.Delete(tmp);
        Check("luu/nap thong so", Near(back.VLegA, 40.5, 1e-12) && back.StiffMode == StiffMode.Custom && back.StiffCells == "800 2*1500"
              && !back.UseStrip && back.Fasten == FastenKind.SpotWeld && back.MeshName == "TEST" && Near(back.StripThk, 1.5, 1e-12));
        var cfg2 = new MeshFrameSettings { VPlane = PlaneKind.Right, StripDepth = DepthKind.Behind, FlipMiterCut = true, FlipBolt = true, BoltStandard = "7990", VProfile = "L50x50x3" };
        SettingsStore.Save(cfg2, tmp); var back2 = SettingsStore.Load(tmp); System.IO.File.Delete(tmp);
        Check("luu/nap huong dat + profile + bolt", back2.VPlane == PlaneKind.Right && back2.StripDepth == DepthKind.Behind && back2.MeshKind == MeshKind.Plate && back2.FlipMiterCut && back2.FlipBolt && back2.BoltStandard == "7990" && back2.VProfile == "L50x50x3");
        Check("nap file khong ton tai = mac dinh", Near(SettingsStore.Load("/khong/co/file.ini").VLegA, 25, 1e-12));

        Console.WriteLine("pass " + pass + " fail " + fail);
        return fail == 0 ? 0 : 1;
    }

    static double MaxGap(MeshFramePlan p)
    {
        double mx = 0;
        foreach (var st in p.Strips)
        {
            var pts = p.Fasteners.Where(f => Math.Abs((f.Pos - st.Start).Cross((st.End - st.Start).Unit())) < 1e-6).OrderBy(f => (f.Pos - st.Start).Length).ToList();
            for (int i = 1; i < pts.Count; i++) mx = Math.Max(mx, (pts[i].Pos - pts[i - 1].Pos).Length);
        }
        return mx;
    }
}
