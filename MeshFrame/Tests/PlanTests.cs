// Test thuần chạy được trên Linux (mcs + mono), không cần Tekla. Chuẩn 05.TC.01 REV04 (V-25x2, PL-25x1.2, @ tối đa 1000)
// + đa giác N điểm (lồi hoặc lõm, không tự cắt).
using System;
using System.Linq;
using MeshFrame.Core;

static class PlanTests
{
    static int fail = 0, pass = 0;
    static void Check(string name, bool ok) { if (ok) pass++; else { fail++; Console.WriteLine("FAIL: " + name); } }
    static bool Near(double a, double b, double tol) { return Math.Abs(a - b) <= tol; }
    static V2[] Rect(double L, double H, double skew) { return new[] { new V2(0, 0), new V2(L, 0), new V2(L + skew, H), new V2(skew, H) }; }
    static V2[] Pts(params double[] xy) { var r = new V2[xy.Length / 2]; for (int i = 0; i < r.Length; i++) r[i] = new V2(xy[2 * i], xy[2 * i + 1]); return r; }
    static MeshFramePlan Plan(MeshFrameSettings s, double L, double H, double skew) { return MeshFramePlanner.Plan(s, Rect(L, H, skew)); }
    static double Area(V2[] q) { return Math.Abs(Geo.Area(q)); }
    static bool Inside(V2[] poly, V2 p)
    {
        bool c = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) c = !c;
        return c;
    }

    static int Main()
    {
        // --- giá trị mặc định theo chuẩn
        var d = new MeshFrameSettings();
        Check("mac dinh theo chuan: V25x25x2, PL25x1.2, @1000",
              Near(d.VLegA, 25, 0) && Near(d.VLegB, 25, 0) && Near(d.VThk, 2, 0) && Near(d.StripWidth, 25, 0) && Near(d.StripThk, 1.2, 0) && Near(d.StiffMax, 1000, 0));
        Check("mac dinh luoi RebarMesh, canh goc tu chon", d.MeshKind == MeshKind.RebarMesh && d.StiffBaseEdge == 0);

        // --- khung chữ nhật 4000 x 1200
        var s = new MeshFrameSettings();
        var p = Plan(s, 4000, 1200, 0);
        Check("hop le", p.Errors.Count == 0);
        Check("4 o", p.Cells.Count == 4 && p.Cells.All(c => Near(c, 1000, 1e-6)));
        Check("bars = 4 khung + 3 gia cuong", p.Bars.Count == 7 && p.Bars.Count(b => b.Role == BarRole.Stiffener) == 3);
        Check("nep = 4 + 3", p.Strips.Count == 7);
        Check("canh goc tu chon = canh dai nhat (canh 1)", p.BaseEdge == 0);
        Check("luoi phu den mep ngoai (gap 0): dien tich = 4000*1200", Near(Area(p.Mesh.Polygon), 4000.0 * 1200.0, 0.01));
        Check("luoi hinh chu nhat nhan dien", p.Mesh.IsRectangle);
        Check("do day luoi = 2*0.37", Near(p.Mesh.Thickness, 0.74, 1e-9));
        Check("mac dinh mac/co soi rebar = MeshGrade/MeshWireDia", p.Mesh.RebarGrade == "SUS304" && p.Mesh.RebarSize == "0.37");
        p = Plan(new MeshFrameSettings { RebarGrade = "B500B", RebarSize = "6" }, 4000, 1200, 0);
        Check("mac/co soi rebar ghi de", p.Mesh.RebarGrade == "B500B" && p.Mesh.RebarSize == "6");

        // --- thứ tự lớp theo chuẩn
        p = Plan(s, 4000, 1200, 0);
        Check("luoi duoi canh nam V (phia ngoai)", Near(p.Mesh.ZBottom, -0.74, 1e-9) && p.Mesh.ZBottom < 0);
        Check("nep ngoai luoi: ZRef = -meshT", p.Strips.All(x => Near(x.ZRef, -0.74, 1e-9)));
        Check("V khung tham chieu o goc ngoai z=0", p.Bars.All(b => Near(b.ZRef, 0, 1e-12)));

        // --- thanh gia cường: cùng tiết diện, vuông góc, cánh nằm về +x
        var st = p.Bars.First(b => b.Role == BarRole.Stiffener);
        Check("gia cuong thang dung tai x=1000", Near(st.Start.X, 1000, 1e-6) && Near(st.End.X, 1000, 1e-6));
        Check("gia cuong dung o mat trong canh dung V khung (y 2..1198)", Near(Math.Min(st.Start.Y, st.End.Y), 2, 1e-6) && Near(Math.Max(st.Start.Y, st.End.Y), 1198, 1e-6));
        Check("huong Start->End sao cho canh nam (ben trai) ve +x", Near((st.End - st.Start).Unit().Left().X, 1, 1e-9));
        var fr = p.Bars.First(b => b.Role == BarRole.Frame);
        Check("V gia cuong CUNG TIET DIEN V khung", st.Profile == fr.Profile && Near(st.LegA, fr.LegA, 0) && Near(st.LegB, fr.LegB, 0) && Near(st.Thk, fr.Thk, 0));
        Check("moi gia cuong co 2 khuyet; khung khong khuyet", p.Bars.Where(b => b.Role == BarRole.Stiffener).All(b => b.Notches.Count == 2) && p.Bars.Where(b => b.Role == BarRole.Frame).All(b => b.Notches.Count == 0));
        var n0 = st.Notches[0]; var n1 = st.Notches[1];
        Check("khuyet: x 999..1025, dien tich 26*24 moi cai",
              n0.All(v => v.X >= 999 - 1e-6 && v.X <= 1025 + 1e-6) && n1.All(v => v.X >= 999 - 1e-6 && v.X <= 1025 + 1e-6) && Near(Area(n0), 26.0 * 24.0, 1e-6) && Near(Area(n1), 26.0 * 24.0, 1e-6));
        Check("hai khuyet: mot o day (y 1..25), mot o tren (y 1175..1199)",
              (Near(n0.Min(v => v.Y), 1, 1e-6) && Near(n0.Max(v => v.Y), 25, 1e-6) && Near(n1.Min(v => v.Y), 1175, 1e-6) && Near(n1.Max(v => v.Y), 1199, 1e-6))
              || (Near(n1.Min(v => v.Y), 1, 1e-6) && Near(n1.Max(v => v.Y), 25, 1e-6) && Near(n0.Min(v => v.Y), 1175, 1e-6) && Near(n0.Max(v => v.Y), 1199, 1e-6)));

        // --- nẹp
        Check("nep chu vi: tim cach mep ngoai w/2 = 12.5", Near(p.Strips[0].Start.Y, 12.5, 1e-6) && Near(p.Strips[0].End.Y, 12.5, 1e-6));
        var ns = p.Strips.First(x => x.OnStiffener);
        Check("nep gia cuong: tim x = 1000 + 12.5", Near(ns.Start.X, 1012.5, 1e-6) && Near(ns.End.X, 1012.5, 1e-6));
        Check("nep gia cuong dung o mep trong nep chu vi (y 25..1175)", Near(Math.Min(ns.Start.Y, ns.End.Y), 25, 1e-6) && Near(Math.Max(ns.Start.Y, ns.End.Y), 1175, 1e-6));

        // --- danh sách ô tùy ý
        p = Plan(new MeshFrameSettings { StiffMode = StiffMode.Custom, StiffCells = "1000 2*1500" }, 4000, 1200, 0);
        Check("custom 3 o, 2 thanh x=1000,2500", p.Errors.Count == 0 && p.Cells.Count == 3 && Near(p.Bars.Where(b => b.Role == BarRole.Stiffener).Select(b => b.Start.X).Sum(), 3500, 1e-6));
        p = Plan(new MeshFrameSettings { StiffMode = StiffMode.Custom, StiffCells = "1000 500" }, 4000, 1200, 0);
        Check("con du them o cuoi + canh bao", p.Cells.Count == 3 && Near(p.Cells[2], 2500, 1e-6) && p.Warnings.Any(w => w.Contains("thêm ô cuối")));
        {
            // Tổng ô vượt khung: KHÔNG khóa, chỉ cảnh báo; cắt ở mép khung (3000 + 1000 = 4000, ô 2000 thành 1000)
            var pc = Plan(new MeshFrameSettings { StiffMode = StiffMode.Custom, StiffCells = "3000 2000" }, 4000, 1200, 0);
            Check("tong o > khung: khong loi, co canh bao", pc.Errors.Count == 0 && pc.Warnings.Any(w => w.Contains("cắt ở mép khung")));
            Check("tong o > khung: cat o cuoi o mep khung", pc.Cells.Count == 2 && Near(pc.Cells[0], 3000, 1e-6) && Near(pc.Cells[1], 1000, 1e-6) && pc.Bars.Count(b => b.Role == BarRole.Stiffener) == 1);
            pc = Plan(new MeshFrameSettings { StiffMode = StiffMode.Custom, StiffCells = "4000" }, 628.3, 400, 0);
            Check("o 4000 tren khung 628.3: khong loi, khong gia cuong", pc.Errors.Count == 0 && pc.Cells.Count == 1);
            pc = Plan(new MeshFrameSettings { StiffMode = StiffMode.Custom, StiffCells = "4000 1000" }, 628.3, 400, 0);
            Check("o dau vuot khung: chi 1 o", pc.Errors.Count == 0 && pc.Cells.Count == 1 && Near(pc.Cells[0], 628.3, 1e-6));
        }

        // --- chọn cạnh gốc
        p = Plan(new MeshFrameSettings { StiffMax = 600, StiffBaseEdge = 2 }, 4000, 1200, 0);     // cạnh 2 = đứng bên phải
        var hs = p.Bars.Last();
        Check("canh goc 2: 2 o, 1 thanh ngang y=600", p.BaseEdge == 1 && p.Cells.Count == 2 && p.Bars.Count(b => b.Role == BarRole.Stiffener) == 1 && Near(hs.Start.Y, 600, 1e-6) && Near(hs.End.Y, 600, 1e-6));
        Check("thanh ngang: canh nam (ben trai) ve +y, 2 khuyet", Near((hs.End - hs.Start).Unit().Left().Y, 1, 1e-9) && hs.Notches.Count == 2);
        Check("canh goc khong ton tai bao loi", Plan(new MeshFrameSettings { StiffBaseEdge = 9 }, 4000, 1200, 0).Errors.Count == 1);

        // --- khung bình hành: kích thước đo theo hình chiếu dọc cạnh gốc
        p = Plan(new MeshFrameSettings(), 4000, 1200, 300);
        Check("binh hanh hop le: 5 o (4300/1000), gia cuong dung", p.Errors.Count == 0 && p.Cells.Count == 5 && p.Bars.Where(b => b.Role == BarRole.Stiffener).All(b => Near(b.Start.X, b.End.X, 1e-6)));
        Check("dien tich luoi binh hanh = khung", Near(Area(p.Mesh.Polygon), 4000.0 * 1200.0, 0.01) && !p.Mesh.IsRectangle);
        p = Plan(new MeshFrameSettings(), 4000, 1200, 2000);
        Check("lech lon: van tao, gia cuong ngan hon o phan lech", p.Errors.Count == 0 && p.Bars.Where(b => b.Role == BarRole.Stiffener).Any(b => (b.End - b.Start).Length < 1000));

        // --- đa giác bất kỳ
        p = MeshFramePlanner.Plan(new MeshFrameSettings(), Pts(0, 0, 3000, 0, 1500, 1000));
        Check("tam giac: hop le, 3 V + 2 gia cuong, 3 + 2 nep", p.Errors.Count == 0 && p.Bars.Count == 5 && p.Strips.Count == 5 && p.Cells.Count == 3);
        Check("tam giac: moi gia cuong nam trong khung", p.Bars.Where(b => b.Role == BarRole.Stiffener).All(b => Inside(p.Polygon, b.Start) && Inside(p.Polygon, b.End)));
        p = MeshFramePlanner.Plan(new MeshFrameSettings(), Pts(0, 0, 2500, 0, 3000, 800, 1500, 1300, -200, 700));
        Check("ngu giac: hop le, 5 V khung moi canh co cat vat", p.Errors.Count == 0 && p.Bars.Count(b => b.Role == BarRole.Frame) == 5 && p.Bars.Where(b => b.Role == BarRole.Frame).All(b => b.CutStart.HasValue && b.CutEnd.HasValue));
        var Lshape = Pts(0, 0, 3000, 0, 3000, 1000, 1000, 1000, 1000, 2000, 0, 2000);
        p = MeshFramePlanner.Plan(new MeshFrameSettings(), Lshape);
        Check("hinh L (lom): hop le, 6 V khung", p.Errors.Count == 0 && p.Bars.Count(b => b.Role == BarRole.Frame) == 6 && p.Strips.Count(x => !x.OnStiffener) == 6);
        Check("hinh L: dien tich luoi = 4.000.000 (3000*1000 + 1000*1000)", Near(Area(p.Mesh.Polygon), 4000000.0, 0.01));
        Check("hinh L: gia cuong nam trong khung", p.Bars.Where(b => b.Role == BarRole.Stiffener).All(b => Inside(p.Polygon, (b.Start + b.End) * 0.5)));
        var Ushape = Pts(0, 0, 3000, 0, 3000, 1000, 2000, 1000, 2000, 300, 1000, 300, 1000, 1000, 0, 1000);
        p = MeshFramePlanner.Plan(new MeshFrameSettings { StiffMax = 500, StiffBaseEdge = 2 }, Ushape);
        var split = p.Bars.Where(b => b.Role == BarRole.Stiffener && Near(b.Start.Y, 500, 1e-6)).ToList();
        Check("hinh U (lom): duong chia y=500 cat 2 canh -> 2 thanh gia cuong rieng", p.Errors.Count == 0 && split.Count == 2 && split.All(b => b.Notches.Count == 2));
        Check("hinh U: hai thanh nam hai canh khac nhau", split.Count == 2 && Math.Abs(split[0].Start.X - split[1].Start.X) > 1500);
        Check("diem lom co huong cat vat", p.Bars.Where(b => b.Role == BarRole.Frame).All(b => b.CutStart.HasValue));
        // thứ tự CW được chấp nhận và chuẩn hóa
        var cw = Rect(4000, 1200, 0).Reverse().ToArray();
        p = MeshFramePlanner.Plan(new MeshFrameSettings(), cw);
        Check("nhap nguoc chieu kim dong ho van chap nhan (chuan hoa)", p.Errors.Count == 0 && Geo.Area(p.Polygon) > 0 && p.Bars.Count == 7);
        // lỗi đầu vào
        Check("2 diem bao loi", MeshFramePlanner.Plan(new MeshFrameSettings(), Pts(0, 0, 100, 0)).Errors.Count > 0);
        Check("hinh no bo (tu cat) bao loi", MeshFramePlanner.Plan(new MeshFrameSettings(), Pts(0, 0, 100, 100, 100, 0, 0, 100)).Errors.Count > 0);
        Check("trung diem bao loi", MeshFramePlanner.Plan(new MeshFrameSettings(), Pts(0, 0, 100, 0, 100, 0, 100, 100)).Errors.Count > 0);
        Check("5 diem tu cat bao loi", MeshFramePlanner.Plan(new MeshFrameSettings(), Pts(0, 0, 3000, 0, 3000, 1000, 0, -500, 1500, 500)).Errors.Count > 0);
        Check("khung qua nho", Plan(new MeshFrameSettings(), 30, 30, 0).Errors.Count > 0);

        // --- trạng thái gia cường (hiện trong app)
        p = Plan(new MeshFrameSettings(), 800, 600, 0);
        Check("khung nho: khong gia cuong + ghi chu ro ly do", p.Cells.Count == 1 && p.Bars.Count == 4 && p.Notes.Any(x => x.Contains("không cần") && x.Contains("800")));
        Check("khong dung gia cuong: ghi chu", Plan(new MeshFrameSettings { StiffMode = StiffMode.None }, 4000, 1200, 0).Notes.Any(x => x.Contains("không dùng")));
        Check("co gia cuong: ghi chu so thanh", Plan(new MeshFrameSettings(), 4000, 1200, 0).Notes.Any(x => x.Contains("3 thanh") && x.Contains("4 ô")));
        // dải hẹp: các đường chia đều ngắn hơn chỗ cắt khuyết -> cảnh báo trong app
        p = MeshFramePlanner.Plan(new MeshFrameSettings { StiffMax = 500, UseStrip = false }, Rect(3000, 40, 0));
        Check("dai hep: canh bao khong tao duoc gia cuong", p.Warnings.Any(w => w.Contains("Không tạo được gia cường")) && p.Bars.Count(b => b.Role == BarRole.Stiffener) == 0);

        // --- không nẹp, cắt vát vs chạy suốt
        p = Plan(new MeshFrameSettings { UseStrip = false }, 4000, 1200, 0);
        Check("khong nep => khong nep, gia cuong van co", p.Strips.Count == 0 && p.Bars.Count == 7);
        p = Plan(new MeshFrameSettings { StiffMode = StiffMode.None }, 4000, 1200, 0);
        Check("miter co huong cat", p.Bars.All(b => b.CutStart.HasValue && b.CutEnd.HasValue));
        p = Plan(new MeshFrameSettings { StiffMode = StiffMode.None, Corner = CornerKind.OneThrough }, 4000, 1200, 0);
        Check("OneThrough 4 diem: canh 0 chay suot, canh 1 ngan lai (y 2..1198)",
              Near(p.Bars[0].Start.X, 0, 1e-6) && Near(p.Bars[0].End.X, 4000, 1e-6) && Near(p.Bars[1].Start.Y, 2, 1e-6) && Near(p.Bars[1].End.Y, 1198, 1e-6));
        Check("OneThrough: canh ngan co cat khuyet canh nam o ca 2 dau, canh dai khong", p.Bars[0].Notches.Count == 0 && p.Bars[2].Notches.Count == 0 && p.Bars[1].Notches.Count == 2 && p.Bars[3].Notches.Count == 2);
        Check("OneThrough: khong cat vat", p.Bars.All(b => !b.CutStart.HasValue && !b.CutEnd.HasValue));
        {
            // tam giac: khong co canh nao song song canh goc ngoai chinh no -> moi goc van cat vat
            p = MeshFramePlanner.Plan(new MeshFrameSettings { StiffMode = StiffMode.None, Corner = CornerKind.OneThrough }, Pts(0, 0, 3000, 0, 1500, 1000));
            Check("OneThrough tam giac: chi canh goc chay dai, hai canh con lai dung sat", p.Bars.Count == 3 && !p.Bars[0].CutStart.HasValue && p.Bars[0].Notches.Count == 0 && p.Bars[1].Notches.Count >= 1);
            // chu U: cac canh ngang chay dai, canh dung dung sat
            p = MeshFramePlanner.Plan(new MeshFrameSettings { StiffMode = StiffMode.None, Corner = CornerKind.OneThrough }, Pts(0, 0, 3000, 0, 3000, 1000, 2000, 1000, 2000, 300, 1000, 300, 1000, 1000, 0, 1000));
            Check("OneThrough chu U hop le", p.Errors.Count == 0 && p.Bars.Count == 8);
            // canh ngang 0 (day) chay dai: x 0..3000
            Check("OneThrough chu U: day chay dai 0..3000", Near(p.Bars[0].Start.X, 0, 1e-6) && Near(p.Bars[0].End.X, 3000, 1e-6));
        }
        {
            // nep: vi tri so voi canh nam V (V 25, nep 20): Phai = sat canh dung (0..20), Trai = sat dau canh nam (5..25), Giua (2.5..22.5)
            Func<StripPosKind, double> mid = k =>
            {
                var pl = Plan(new MeshFrameSettings { StiffMode = StiffMode.None, StripWidth = 20, StripPos = k }, 4000, 1200, 0);
                // canh 0 chay theo +X, phia trong = +Y: tim nep = y
                return (pl.Strips[0].Start.Y + pl.Strips[0].End.Y) / 2;
            };
            Check("nep Phai: tim cach mep ngoai 10", Near(mid(StripPosKind.Right), 10, 1e-6));
            Check("nep Giua: tim cach mep ngoai 12.5", Near(mid(StripPosKind.Middle), 12.5, 1e-6));
            Check("nep Trai: tim cach mep ngoai 15", Near(mid(StripPosKind.Left), 15, 1e-6));
            // gia cuong: nep tren thanh gia cuong theo cung quy tac (huong +u)
            var ps = Plan(new MeshFrameSettings { StripWidth = 20, StripPos = StripPosKind.Left }, 4000, 1200, 0);
            var sb = ps.Bars.First(b => b.Role == BarRole.Stiffener);
            var ss = ps.Strips.First(t => t.OnStiffener);
            Check("nep gia cuong Trai: tim = duong chia + 15 theo +X", Near((ss.Start.X + ss.End.X) / 2, sb.Start.X + 15, 1e-6));
            Check("nep rong bang canh V: Giua/Trai/Phai trung nhau", Near(Plan(new MeshFrameSettings { StripPos = StripPosKind.Left }, 4000, 1200, 0).Strips[0].Start.Y, Plan(new MeshFrameSettings { StripPos = StripPosKind.Right }, 4000, 1200, 0).Strips[0].Start.Y, 1e-6));
        }

        // --- mô hình 3D thật (tiết diện L, cắt vát, khuyết)
        {
            // 1 thanh thẳng 1000, V 25x25x2: thể tích = (25*2 + (25-2)*2) * 1000 = 96000
            var bar = new PlanBar { Role = BarRole.Frame, Start = new V2(0, 0), End = new V2(1000, 0), LegA = 25, LegB = 25, Thk = 2, ZRef = 0 };
            var pl = new MeshFramePlan(); pl.Bars.Add(bar);
            var m3 = Model3D.Build(pl);
            double vol = m3.Prisms.Sum(q => q.Volume);
            Check("3D: thep goc L thang: 2 lang tru, the tich dung tiet dien chu L", m3.Prisms.Count == 2 && Near(vol, (25.0 * 2 + 23.0 * 2) * 1000, 1e-6));
            // cánh nằm ở phía trái (y > 0), cánh đứng ở mép ngoài y 0..2
            var hz = m3.Prisms.First(q => Near(q.Z0, 0, 1e-9) && Near(q.Z1, 2, 1e-9));
            var vt = m3.Prisms.First(q => Near(q.Z0, 2, 1e-9) && Near(q.Z1, 25, 1e-9));
            Check("3D: canh nam y 0..25, canh dung y 0..2", hz.Poly.Max(v => v.Y) > 24.99 && hz.Poly.Min(v => v.Y) > -1e-9 && vt.Poly.Max(v => v.Y) < 2.01);
            // cắt vát 45 độ ở đầu Start: cạnh dài ngoài 1000, cánh nằm mất tam giác 25x25/2
            bar.CutStart = new V2(1, 1).Unit(); pl = new MeshFramePlan(); pl.Bars.Add(bar);
            m3 = Model3D.Build(pl);
            double hv = m3.Prisms.First(q => Near(q.Z1, 2, 1e-9)).Volume;
            Check("3D: cat vat 45 do bo tam giac 25x25/2 cua canh nam", Near(hv, (25.0 * 1000 - 25.0 * 25 / 2) * 2, 1e-6));
            // khuyết: cánh nằm mất vùng [0, 25] dọc thanh ở đầu Start (vùng khuyết trải hết bề rộng)
            bar.CutStart = null; bar.Notches.Add(new[] { new V2(0, -1), new V2(25, -1), new V2(25, 25), new V2(0, 25) });
            pl = new MeshFramePlan(); pl.Bars.Add(bar);
            hv = Model3D.Build(pl).Prisms.First(q => Near(q.Z1, 2, 1e-9)).Volume;
            Check("3D: khuyet cat canh nam doan dau 25 mm", Near(hv, 25.0 * (1000 - 25) * 2, 1e-6));
            // đủ khung: mọi lăng trụ hợp lệ, cắt tai đa giác lõm
            var full = Plan(new MeshFrameSettings(), 4000, 1200, 0);
            var mf = Model3D.Build(full);
            Check("3D: khung day du co V, nep, luoi", mf.Prisms.Any(q => q.Kind == PartKind.FrameBar) && mf.Prisms.Any(q => q.Kind == PartKind.Stiffener) && mf.Prisms.Any(q => q.Kind == PartKind.Strip) && mf.Prisms.Any(q => q.Kind == PartKind.Mesh));
            var concave = Pts(0, 0, 3000, 0, 3000, 1000, 2000, 1000, 2000, 300, 1000, 300, 1000, 1000, 0, 1000);
            double ta = 0; foreach (int[] t in Model3D.Triangulate(concave)) ta += Math.Abs(Geo.Area(new[] { concave[t[0]], concave[t[1]], concave[t[2]] }));
            Check("3D: cat tai chu U giu nguyen dien tich", Near(ta, Math.Abs(Geo.Area(concave)), 1e-6));
            var pu = MeshFramePlanner.Plan(new MeshFrameSettings { StiffMode = StiffMode.None, Corner = CornerKind.OneThrough }, concave);
            var mu = Model3D.Build(pu);
            Check("3D: chu U kieu chay dai + dung: dung hinh, khong lang tru nao rong", pu.Errors.Count == 0 && mu.Prisms.Count > 16 && mu.Prisms.All(q => q.Volume > 0));
        }

        // --- giới hạn của chuẩn
        Check("4000x1200 khong canh bao gioi han", !Plan(new MeshFrameSettings(), 4000, 1200, 0).Warnings.Any(w => w.Contains("tối đa")));
        Check("L > 4000 canh bao", Plan(new MeshFrameSettings(), 4500, 1200, 0).Warnings.Any(w => w.Contains("4000")));
        Check("H > 1200 canh bao", Plan(new MeshFrameSettings(), 4000, 1300, 0).Warnings.Any(w => w.Contains("1200")));
        Check("o > 1000 canh bao", Plan(new MeshFrameSettings { StiffMode = StiffMode.Custom, StiffCells = "1500 1500" }, 4000, 1200, 0).Warnings.Any(w => w.Contains("1000")));
        Check("nep rong hon canh V canh bao", Plan(new MeshFrameSettings { StripWidth = 30 }, 4000, 1200, 0).Warnings.Any(w => w.Contains("cánh nằm V")));

        // --- tiết diện nhập tùy ý
        p = Plan(new MeshFrameSettings { VLegA = 30, VLegB = 30, VThk = 2.5, StripWidth = 30, StripThk = 2 }, 4000, 1200, 0);
        Check("V 30x30x2.5: khung + gia cuong cung tiet dien", p.Bars.All(b => b.Profile == "L30*30*2.5"));
        Check("nep tuy y 30x2", p.Strips.All(x => Near(x.Width, 30, 0) && Near(x.Thk, 2, 0) && Near(x.ZRef, -0.74, 1e-9)));
        Check("khuyet theo canh 30: x toi da 1030", Near(p.Bars.First(b => b.Role == BarRole.Stiffener).Notches[0].Max(v => v.X), 1030, 1e-6));

        // --- profile ghi đè, hướng đặt, kiểu lưới, cảnh báo chữ có dấu
        p = Plan(new MeshFrameSettings { VProfile = "L50x50x3", StripProfile = "PL3*30" }, 4000, 1200, 0);
        Check("profile ghi de: V khung + gia cuong + nep", p.Bars.All(b => b.Profile == "L50x50x3") && p.Strips.All(x => x.Profile == "PL3*30"));
        p = Plan(new MeshFrameSettings(), 4000, 1200, 0);
        Check("huong dat mac dinh dang ten enum Tekla", p.Placement.VPlane == "MIDDLE" && p.Placement.VRotation == "FRONT" && p.Placement.VDepth == "MIDDLE" && p.Placement.StripPlane == "MIDDLE" && p.Placement.StripDepth == "MIDDLE");
        p = Plan(new MeshFrameSettings { VPlane = PlaneKind.Right, VRotation = RotationKind.Below, VDepth = DepthKind.Middle, FlipMiterCut = true, VLateral = 1.5, StripLateral = -2 }, 4000, 1200, 0);
        Check("huong dat + lech ngang doi theo thong so", p.Placement.VPlane == "RIGHT" && p.Placement.VRotation == "BELOW" && p.Placement.FlipMiterCut && Near(p.Placement.VLateral, 1.5, 0) && Near(p.Placement.StripLateral, -2, 0));
        Check("RebarMesh duoc chon", Plan(new MeshFrameSettings { MeshKind = MeshKind.RebarMesh }, 4000, 1200, 0).Mesh.Kind == MeshKind.RebarMesh);
        p = Plan(new MeshFrameSettings { MeshKind = MeshKind.None }, 4000, 1200, 0);
        Check("khong tao luoi: Mesh = null, nep van co", p.Mesh == null && p.Strips.Count == 7 && Near(p.Strips[0].ZRef, -0.74, 1e-9));
        p = Plan(new MeshFrameSettings { VName = "KHUNG LƯỚI", MeshName = "Lưới" }, 4000, 1200, 0);
        Check("chu co dau -> canh bao", p.Warnings.Any(w => w.Contains("VName") && w.Contains("MeshName")));
        Check("mac dinh khong canh bao ASCII", !Plan(new MeshFrameSettings(), 4000, 1200, 0).Warnings.Any(w => w.Contains("ASCII")));

        // --- Frame 3D tổng quát
        var corners = new[] { new V3(0, 0, 5000), new V3(4000, 0, 5000), new V3(4000, 1200, 5000), new V3(0, 1200, 5000) };
        var r = MeshFrameBuilder.Build(new MeshFrameSettings(), corners, null);
        Check("builder 3D 4 diem", r.Success && Near(r.Frame.N.Z, 1, 1e-9));
        var penta = new[] { new V3(0, 0, 0), new V3(2500, 0, 0), new V3(3000, 800, 0), new V3(1500, 1300, 0), new V3(-200, 700, 0) };
        r = MeshFrameBuilder.Build(new MeshFrameSettings(), penta, null);
        Check("builder 3D 5 diem", r.Success && r.Plan.Polygon.Length == 5);
        var rev = penta.Reverse().ToArray();
        r = MeshFrameBuilder.Build(new MeshFrameSettings(), rev, null);
        Check("pick nguoc chieu: mat phang lat (N huong xuong) nhung van hop le", r.Success && r.Frame.N.Z < -0.99);
        var slope = new[] { new V3(0, 0, 0), new V3(4000, 0, 0), new V3(4000, 1200, 120), new V3(0, 1200, 120) };
        Check("mai doc hop le", MeshFrameBuilder.Build(new MeshFrameSettings(), slope, null).Success);
        var bad = new[] { new V3(0, 0, 0), new V3(4000, 0, 0), new V3(4000, 1200, 500), new V3(0, 1200, 0) };
        Check("khong dong phang bao loi", !MeshFrameBuilder.Build(new MeshFrameSettings(), bad, null).Success);
        Check("duoi 3 diem bao loi", !MeshFrameBuilder.Build(new MeshFrameSettings(), new[] { new V3(0, 0, 0), new V3(1, 0, 0) }, null).Success);

        // --- lưu / nạp thông số
        var cfg = new MeshFrameSettings { VLegA = 40.5, StiffMode = StiffMode.Custom, StiffCells = "800 2*1500", UseStrip = false, MeshName = "TEST", StripThk = 1.5, StiffBaseEdge = 3, RebarGrade = "B500B", RebarSize = "6" };
        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "meshframe_test_" + Guid.NewGuid().ToString("N") + ".ini");
        SettingsStore.Save(cfg, tmp);
        var back = SettingsStore.Load(tmp);
        System.IO.File.Delete(tmp);
        Check("luu/nap thong so", Near(back.VLegA, 40.5, 1e-12) && back.StiffMode == StiffMode.Custom && back.StiffCells == "800 2*1500"
              && !back.UseStrip && back.MeshName == "TEST" && Near(back.StripThk, 1.5, 1e-12) && back.StiffBaseEdge == 3 && back.RebarGrade == "B500B" && back.RebarSize == "6");
        var cfg2 = new MeshFrameSettings { VPlane = PlaneKind.Right, StripDepth = DepthKind.Behind, FlipMiterCut = true, VProfile = "L50x50x3", MeshKind = MeshKind.Plate };
        SettingsStore.Save(cfg2, tmp); var back2 = SettingsStore.Load(tmp); System.IO.File.Delete(tmp);
        Check("luu/nap huong dat + profile + kieu luoi", back2.VPlane == PlaneKind.Right && back2.StripDepth == DepthKind.Behind && back2.FlipMiterCut && back2.VProfile == "L50x50x3" && back2.MeshKind == MeshKind.Plate);
        Check("nap file khong ton tai = mac dinh", Near(SettingsStore.Load("/khong/co/file.ini").VLegA, 25, 1e-12));
        // thiết lập lưu từ bản cũ (không có @version) bị bỏ -> mặc định mới có hiệu lực (lỗi thật: On plane = Left lưu từ bản cũ)
        System.IO.File.WriteAllLines(tmp, new[] { "VPlane=Left", "VDepth=Front", "VLegA=50", "StripDepth=Behind" });
        var old = SettingsStore.Load(tmp);
        Check("thiet lap cu (khong @version) bi bo: mac dinh moi (Middle, 25) co hieu luc", old.VPlane == PlaneKind.Middle && old.VDepth == DepthKind.Middle && Near(old.VLegA, 25, 0) && old.StripDepth == DepthKind.Middle);
        Check("thiet lap cu duoc sao luu .bak", System.IO.File.Exists(tmp + ".v0.bak") && !System.IO.File.Exists(tmp));
        System.IO.File.Delete(tmp + ".v0.bak");
        SettingsStore.Save(new MeshFrameSettings { VLegA = 31 }, tmp);
        Check("thiet lap moi (co @version) nap binh thuong", Near(SettingsStore.Load(tmp).VLegA, 31, 0));
        System.IO.File.Delete(tmp);
        Check("mac dinh tu canh bat", new MeshFrameSettings().AutoPlacement);

        // --- hình học cơ bản
        Check("IsSimple: hinh chu nhat", Geo.IsSimple(Rect(10, 10, 0)));
        Check("ClipLine hinh U: 2 doan", Geo.ClipLine(Ushape, new V2(0, 500), new V2(1, 0)).Count == 2);

        Console.WriteLine("pass " + pass + " fail " + fail);
        return fail == 0 ? 0 : 1;
    }
}
