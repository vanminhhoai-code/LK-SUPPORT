// Test bộ ghi / picker / dịch vụ khung trên SDK GIẢ + ghi lại mọi thành viên Tekla đã dùng (cho ContractCheck đối chiếu DLL thật).
// Dùng: WriterTests.exe <thư mục chứa Tekla.Structures*.dll giả> <file ghi thành viên>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeshFrame.Core;
using MeshFrame.Tekla;
using Tekla.Structures.Catalogs;
using Tekla.Structures.Model;

static class WriterTests
{
    static int fail, pass;
    static void Check(string n, bool ok) { if (ok) pass++; else { fail++; Console.WriteLine("FAIL: " + n); } }
    // Chế độ "legacy": SDK giả không có PickPoint(chuỗi, điểm) - lỗi thật đã gặp trên Tekla 21.1.
    static int Legacy(TeklaApi api)
    {
        FakeLog.Reset(); FakeCatalog.Reset(); FakeLog.Picked = 0;
        foreach (var p in new[] { new[] { 0.0, 0, 0 }, new[] { 3000.0, 0, 0 }, new[] { 3000.0, 1000, 0 }, new[] { 1500.0, 1600, 0 }, new[] { 0.0, 1000, 0 } }) FakeLog.PickScript.Add(p);
        PickResult pr = TeklaPicker.PickPolygon(api, new Tekla.Structures.Model.Model());
        Check("[legacy] pick 5 diem van chay dung PickPoint(chuoi) khi thieu PickPoint(chuoi, diem)", pr.Complete && pr.Points.Length == 5 && pr.Picked == 5);
        Check("[legacy] van ve du: 14 net + 9 nhan, du cac diem", Count("DrawLine") == 14 && CountStart("DrawText") == 9 && pr.Points[3].Y == 1600);
        Check("[legacy] loi nhac van co so do doan truoc + chu vi", FakeLog.Prompts.Count == 6 && FakeLog.Prompts[3].Contains("doan truoc 1000.0 mm") && FakeLog.Prompts[3].Contains("chu vi 4000.0 mm"));
        Check("[legacy] ChUA dung o diem 1 (loi cu: coi thieu ham la ket thuc)", pr.Picked > 1 && pr.StopReason != null && pr.StopReason.Contains("interrupted"));
        var rep = TeklaContract.VerifyLoaded(api);
        Check("[legacy] thieu PickPoint(chuoi, diem) la TUY CHON: khong co ham bat buoc nao thieu -> khong hien canh bao luc khoi dong", rep.Required.Count == 0 && rep.Optional.Count == 1 && rep.Optional[0].Contains("PickPoint"));
        Console.WriteLine("legacy pass " + pass + " fail " + fail + "  (thieu bat buoc " + rep.Required.Count + ", tuy chon " + rep.Optional.Count + ")");
        return fail == 0 ? 0 : 1;
    }

    static double Spread(System.Collections.ArrayList pts)
    {
        if (pts.Count < 2) return 0;
        double lo = double.MaxValue, hi = double.MinValue;
        foreach (Tekla.Structures.Geometry3d.Point p in pts) { lo = Math.Min(lo, p.Y); hi = Math.Max(hi, p.Y); }
        return hi - lo;
    }
    static double SpreadZ(System.Collections.ArrayList pts)
    {
        if (pts.Count < 2) return 0;
        double lo = double.MaxValue, hi = double.MinValue;
        foreach (Tekla.Structures.Geometry3d.Point p in pts) { lo = Math.Min(lo, p.Z); hi = Math.Max(hi, p.Z); }
        return hi - lo;
    }

    static int Count(string e) { return FakeLog.Entries.Count(x => x == e); }
    static int CountStart(string prefix) { return FakeLog.Entries.Count(x => x.StartsWith(prefix)); }
    static void Reset() { FakeLog.Reset(); FakeCatalog.Reset(); }

    static int Main(string[] a)
    {
        TeklaApi api = TeklaApi.Load(a[0]);
        var used = new HashSet<string>();
        api.Trace = delegate (string s) { used.Add(s); };
        if (a.Length > 2 && a[2] == "legacy") return Legacy(api);          // bản Tekla KHÔNG có PickPoint(chuỗi, điểm), như 21.1 thật

        var corners = new[] { new V3(0, 0, 5000), new V3(4000, 0, 5000), new V3(4000, 1200, 5120), new V3(0, 1200, 5120) };
        var penta = new[] { new V3(0, 0, 0), new V3(2500, 0, 0), new V3(3000, 800, 0), new V3(1500, 1300, 0), new V3(-200, 700, 0) };

        // ---------------------------------------------------------------- mặc định: RebarMesh đủ thuộc tính
        Reset();
        var w = new TeklaModelWriter(api, null) { FrameId = "abc123" };
        BuildResult r = MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false }, corners, w);
        Check("build ok", r.Success && w.Messages.Count == 0);
        Check("14 beam (7 V + 7 nep)", Count("Insert Beam") == 14);
        Check("16 fitting (4 V + 4 nep, 2 dau)", Count("Insert Fitting") == 16);
        Check("khuyet: 6 tam cat + 6 BooleanPart + xoa 6 tam cat", Count("Insert ContourPlate") == 6 && Count("Insert BooleanPart") == 6 && Count("Delete ContourPlate") == 6);
        Check("tam cat dung class BlOpCl cua Tekla", FakeLog.Entries.Count(e => e == "Cut BlOpCl by plate") == 6);
        Check("RebarMesh: 1 lan chen", Count("Insert RebarMesh") == 1 && FakeLog.Meshes.Count == 1);
        var m = FakeLog.Meshes[0];
        Check("RebarMesh: MeshType POLYGON + spacing SAME_DISTANCE + cross bar ABOVE (khong UNDEFINED)",
              m.MeshType == RebarMesh.RebarMeshTypeEnum.POLYGON_MESH && m.LongitudinalSpacingMethod == RebarMesh.RebarMeshSpacingMethodEnum.SPACING_TYPE_SAME_DISTANCE
              && m.CrossBarLocation == RebarMesh.RebarMeshCrossBarLocationEnum.LOCATION_TYPE_ABOVE);
        Check("RebarMesh: buoc soi 2.37 hai huong, OnPlaneOffsets 1 phan tu, offset 0, CutByFatherPartCuts false",
              m.LongitudinalDistances.Count == 1 && (double)m.LongitudinalDistances[0] == 2.37 && m.CrossDistances.Count == 1 && m.OnPlaneOffsets.Count == 1 && m.FromPlaneOffset == 0 && !m.CutByFatherPartCuts);
        Check("RebarMesh: mac SUS304, co soi 0.37, CatalogName, 4 diem da giac", m.Grade == "SUS304" && m.LongitudinalSize == "0.37" && m.CrossSize == "0.37" && !string.IsNullOrEmpty(m.CatalogName) && m.Polygon.Points.Count == 4);
        Check("RebarMesh: Father la V khung dau tien", m.Father is Beam && FakeLog.Uda.ContainsKey(m.Father));
        Check("RebarMesh: ap dung overhang 0 + offset type COVER", m.LeftOverhangCross == 0 && m.RightOverhangLongitudinal == 0 && m.StartPointOffsetType == Reinforcement.RebarOffsetTypeEnum.OFFSET_TYPE_COVER_THICKNESS);
        Check("assembly: 13 beam, khong tam luoi, Modify 1", Count("Add Beam") == 13 && Count("Add ContourPlate") == 0 && Count("Modify Assembly") == 1);
        Check("gan ma khung vao part chinh", Count("UDA MESHFRAME_ID=abc123") == 1 && w.MainPart != null && FakeLog.Uda.ContainsKey((ModelObject)w.MainPart));
        Check("mat phang doi roi tra lai + commit", Count("SetPlane") == 2 && Count("Commit") == 1);
        Check("mat phang dat truoc khi chen", FakeLog.Entries.IndexOf("SetPlane") < FakeLog.Entries.IndexOf("Insert Beam"));
        Check("khong con bolt nao", !FakeLog.Entries.Any(e => e.Contains("Bolt")));

        // ---------------------------------------------------------------- kiểm tra catalog rebar -> lưới tấm dự phòng
        Reset();
        w = new TeklaModelWriter(api, null);
        r = MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false, RebarGrade = "XYZ" }, corners, w);
        Check("mac khong co trong catalog: canh bao nua cac mac co, khong thu RebarMesh", w.Messages.Count == 1 && w.Messages[0].Contains("'XYZ'") && w.Messages[0].Contains("SUS304, B500B") && Count("Insert RebarMesh") == 0);
        Check("mac sai: thay bang luoi tam (7 ContourPlate, vao assembly)", Count("Insert ContourPlate") == 7 && Count("Add ContourPlate") == 1 && Count("Modify Assembly") == 1 && r.Success);
        Reset();
        w = new TeklaModelWriter(api, null);
        MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false, RebarGrade = "B500B", RebarSize = "5" }, corners, w);
        Check("co soi khong co cho mac: bao cac co co (6, 8)", w.Messages.Count == 1 && w.Messages[0].Contains("'5'") && w.Messages[0].Contains("6, 8") && Count("Insert ContourPlate") == 7);
        Reset();
        w = new TeklaModelWriter(api, null);
        MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false, RebarGrade = "B500B", RebarSize = "6" }, corners, w);
        Check("mac + co soi hop le trong catalog: tao RebarMesh", w.Messages.Count == 0 && Count("Insert RebarMesh") == 1 && FakeLog.Meshes[0].LongitudinalSize == "6" && FakeLog.Meshes[0].Grade == "B500B");
        Reset(); FakeCatalog.Connected = false;
        w = new TeklaModelWriter(api, null);
        MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false }, corners, w);
        Check("khong doc duoc catalog: bo qua kiem tra, van thu RebarMesh", w.Messages.Count == 0 && Count("Insert RebarMesh") == 1);

        // ---------------------------------------------------------------- Tekla từ chối RebarMesh
        Reset(); FakeLog.RejectRebarMesh = true;
        w = new TeklaModelWriter(api, null) { FrameId = "x1" };
        r = MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false }, corners, w);
        Check("RebarMesh bi tu choi: thu da giac roi chu nhat (2 lan), thay bang tam", Count("Insert RebarMesh") == 2 && Count("Insert ContourPlate") == 7 && r.Success);
        Check("canh bao co ly do cua Tekla + noi ro da thay bang tam", w.Messages.Count == 1 && w.Messages[0].Contains("required info missing") && w.Messages[0].Contains("lưới dạng tấm"));
        Check("van gop assembly day du + gan ma", Count("Add Beam") == 13 && Count("Add ContourPlate") == 1 && Count("Modify Assembly") == 1 && CountStart("UDA MESHFRAME_ID=") == 1 && CountStart("UDA MESHFRAME_KEY=") == 15);
        Check("van tra mat phang + commit", Count("SetPlane") == 2 && Count("Commit") == 1);
        Reset(); FakeLog.RejectPolygonMesh = true;
        w = new TeklaModelWriter(api, null);
        MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false }, corners, w);
        Check("da giac bi tu choi nhung chu nhat duoc: RECTANGULAR_MESH, khong can tam", Count("Insert RebarMesh") == 2 && FakeLog.Meshes.Count == 1 && FakeLog.Meshes[0].MeshType == RebarMesh.RebarMeshTypeEnum.RECTANGULAR_MESH
              && Count("Insert ContourPlate") == 6 && w.Messages.Count == 1 && w.Messages[0].Contains("chữ nhật"));
        Check("RECTANGULAR_MESH khong dat Polygon", FakeLog.Meshes[0].Polygon == null);
        Reset(); FakeLog.RejectRebarMesh = true;
        w = new TeklaModelWriter(api, null);
        MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false }, penta, w);
        Check("ngu giac bi tu choi: chi thu 1 lan (khong phai chu nhat), thay bang tam", Count("Insert RebarMesh") == 1 && Count("Insert ContourPlate") >= 1 && Count("Add ContourPlate") == 1 && w.Messages.Count == 1);

        // ---------------------------------------------------------------- kiểu lưới khác
        Reset();
        w = new TeklaModelWriter(api, null);
        MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false, MeshKind = MeshKind.Plate }, corners, w);
        Check("luoi tam theo y/c: 7 ContourPlate, khong RebarMesh, khong canh bao", Count("Insert RebarMesh") == 0 && Count("Insert ContourPlate") == 7 && Count("Add ContourPlate") == 1 && w.Messages.Count == 0);
        Reset();
        w = new TeklaModelWriter(api, null);
        MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false, MeshKind = MeshKind.None }, corners, w);
        Check("khong tao luoi", Count("Insert RebarMesh") == 0 && Count("Insert ContourPlate") == 6 && Count("Add ContourPlate") == 0);

        // ---------------------------------------------------------------- đa giác 5 điểm
        Reset();
        w = new TeklaModelWriter(api, null);
        r = MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false, StiffMode = StiffMode.None }, penta, w);
        Check("ngu giac: 5 V + 5 nep, 20 fitting (cat vat moi dau), RebarMesh 5 diem", r.Success && Count("Insert Beam") == 10 && Count("Insert Fitting") == 20 && FakeLog.Meshes.Count == 1 && FakeLog.Meshes[0].Polygon.Points.Count == 5);

        // ---------------------------------------------------------------- lỗi nặng -> xóa sạch, không gộp
        Reset(); FakeLog.FailBeamAt = 3;
        bool rolled = false;
        try { MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false }, corners, new TeklaModelWriter(api, null)); }
        catch (InvalidOperationException) { rolled = true; }
        Check("beam thu 3 loi -> nem loi; rollback xoa 2 beam, khong gop, van tra mat phang", rolled && Count("Delete Beam") == 2 && Count("Modify Assembly") == 0 && Count("SetPlane") == 2 && Count("Commit") == 1);
        Reset(); FakeLog.RejectInsert = true;
        bool threw = false;
        try { MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false }, corners, new TeklaModelWriter(api, null)); }
        catch (InvalidOperationException e) { threw = e.Message.Contains("profile"); }
        Check("Tekla tu choi Insert -> loi nhac kiem tra profile", threw);

        // ---------------------------------------------------------------- đường tham chiếu (tim)
        Reset();
        MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false, StiffMode = StiffMode.None }, corners, new TeklaModelWriter(api, null));
        double[] v0 = FakeLog.BeamStarts[0], s0 = FakeLog.BeamStarts[4];
        Check("V khung: tham chieu o tim canh nam (y=12.5) va tim chieu cao (z=12.5)", Math.Abs(v0[0]) < 1e-6 && Math.Abs(v0[1] - 12.5) < 1e-6 && Math.Abs(v0[2] - 12.5) < 1e-6);
        Check("nep: tim o y=12.5, z = -0.74 - 0.6", Math.Abs(s0[0] - 12.5) < 1e-6 && Math.Abs(s0[1] - 12.5) < 1e-6 && Math.Abs(s0[2] + 1.34) < 1e-6);
        Reset();
        MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false, VLateral = 3, StripLateral = -2, StiffMode = StiffMode.None }, corners, new TeklaModelWriter(api, null));
        Check("lech ngang nguoi dung cong vao", Math.Abs(FakeLog.BeamStarts[0][1] - 15.5) < 1e-6 && Math.Abs(FakeLog.BeamStarts[4][1] - 10.5) < 1e-6);
        Reset();
        MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false, VPlane = PlaneKind.Right, VRotation = RotationKind.Below, VDepth = DepthKind.Middle, StiffMode = StiffMode.None, UseStrip = false }, corners, new TeklaModelWriter(api, null));
        Check("huong dat tu thong so ghi vao beam", FakeLog.BeamPositions.Count == 4 && FakeLog.BeamPositions.All(x => x == "RIGHT/BELOW/MIDDLE"));
        foreach (PlaneKind k in Enum.GetValues(typeof(PlaneKind))) api.Enum("Position+PlaneEnum", PlanPlacement.Name(k));
        foreach (RotationKind k in Enum.GetValues(typeof(RotationKind))) api.Enum("Position+RotationEnum", PlanPlacement.Name(k));
        foreach (DepthKind k in Enum.GetValues(typeof(DepthKind))) api.Enum("Position+DepthEnum", PlanPlacement.Name(k));

        // ---------------------------------------------------------------- TỰ CANH theo hình khối thật (SDK giả có quy ước đặt KHÁC giả định của app)
        Reset();
        w = new TeklaModelWriter(api, null) { FrameId = "auto1" };
        r = MeshFrameBuilder.Build(new MeshFrameSettings { StiffMode = StiffMode.None }, corners, w);
        Check("tu canh: build ok, khong canh bao", r.Success && w.Messages.Count == 0);
        Check("tu canh: V xoay BELOW (thu FRONT, TOP, BACK roi BELOW), nep xoay TOP; ghi chu ro", w.Notes.Count == 2 && w.Notes[0].Contains("V xoay BELOW") && w.Notes[0].Contains("nẹp xoay TOP"));
        Check("do lai tren thanh that: do du 8 thanh, khong can doi, con 0", w.Notes[1].Contains("Đo lại trên hình khối thật: 8/8") && w.Notes[1].Contains("dời 0 thanh") && w.Notes[1].Contains("còn 0 mm"));
        Check("tu canh: thanh mau bi xoa het (4 V + 2 nep mau), thanh that giu nguyen", Count("Insert Beam") == 8 + 6 && Count("Delete Beam") == 6 && FakeLog.Beams.Count == 8);
        Solid sv = FakeLog.Beams[0].GetSolid();                          // V khung dau tien, doc theo +X
        Check("tu canh: V that chiem y 0..25 (tu mep ngoai vao trong) va z 0..25", Math.Abs(sv.MinimumPoint.Y) < 1e-6 && Math.Abs(sv.MaximumPoint.Y - 25) < 1e-6 && Math.Abs(sv.MinimumPoint.Z) < 1e-6 && Math.Abs(sv.MaximumPoint.Z - 25) < 1e-6);
        double xm = (sv.MinimumPoint.X + sv.MaximumPoint.X) / 2;
        double spanBottom = Spread(sv.Intersect(new Tekla.Structures.Geometry3d.Point(xm, -2, 1), new Tekla.Structures.Geometry3d.Point(xm, 30, 1)));
        double spanTop = Spread(sv.Intersect(new Tekla.Structures.Geometry3d.Point(xm, -2, 24), new Tekla.Structures.Geometry3d.Point(xm, 30, 24)));
        Check("tu canh: V that co CANH NAM O DUOI (rong 25 tai z=1) va CANH DUNG O TREN chi day 2 (tai z=24)", Math.Abs(spanBottom - 25) < 1e-6 && Math.Abs(spanTop - 2) < 1e-6);
        double spanOuter = SpreadZ(sv.Intersect(new Tekla.Structures.Geometry3d.Point(xm, 1, -2), new Tekla.Structures.Geometry3d.Point(xm, 1, 30)));
        double spanInner = SpreadZ(sv.Intersect(new Tekla.Structures.Geometry3d.Point(xm, 24, -2), new Tekla.Structures.Geometry3d.Point(xm, 24, 30)));
        Check("tu canh: V that co CANH DUNG O MEP NGOAI (cao 25 tai y=1) va chi co canh nam o phia trong (day 2 tai y=24)", Math.Abs(spanOuter - 25) < 1e-6 && Math.Abs(spanInner - 2) < 1e-6);
        Solid ss = FakeLog.Beams[4].GetSolid();                          // nep dau tien
        Check("tu canh: NEP that y 0..25 (tim nep trung tim canh nam V, rong 25 = canh V) va z -1.94..-0.74 (ngoai luoi, ep vao canh V)",
              Math.Abs(ss.MinimumPoint.Y) < 1e-6 && Math.Abs(ss.MaximumPoint.Y - 25) < 1e-6 && Math.Abs(ss.MinimumPoint.Z + 1.94) < 1e-6 && Math.Abs(ss.MaximumPoint.Z + 0.74) < 1e-6);
        Check("tu canh: V va nep cung khoang ngang (y) -> nep nam giua canh nam cua V", Math.Abs((sv.MinimumPoint.Y + sv.MaximumPoint.Y) / 2 - (ss.MinimumPoint.Y + ss.MaximumPoint.Y) / 2) < 1e-6);
        Check("tu canh: V dung Position MIDDLE/MIDDLE", FakeLog.BeamPositions.Take(4).All(x => x == "MIDDLE/BELOW/MIDDLE") && FakeLog.BeamPositions.Skip(4).All(x => x == "MIDDLE/TOP/MIDDLE"));
        // cung ket qua khi quy uoc dat thay doi? -> chay lai voi V xoay mac dinh = BELOW van ra dung
        Reset();
        w = new TeklaModelWriter(api, null);
        MeshFrameBuilder.Build(new MeshFrameSettings { StiffMode = StiffMode.None, VRotation = RotationKind.Below, StripRotation = RotationKind.Top }, corners, w);
        Check("tu canh: neu xoay mac dinh da dung thi chi do 1 lan moi loai (2 thanh mau)", Count("Delete Beam") == 2);
        // quy uoc dat KHAC (tiet dien giả xoay them 90 va 270 do): van ra dung hinh dang, chi doi huong xoay duoc chon
        foreach (int turn in new[] { 90, 270 })
        {
            Reset(); FakeLog.SectionTurn = turn;
            w = new TeklaModelWriter(api, null);
            MeshFrameBuilder.Build(new MeshFrameSettings { StiffMode = StiffMode.None }, corners, w);
            Solid tv = FakeLog.Beams[0].GetSolid(), ts = FakeLog.Beams[4].GetSolid();
            double tx = (tv.MinimumPoint.X + tv.MaximumPoint.X) / 2;
            bool okV = Math.Abs(tv.MinimumPoint.Y) < 1e-6 && Math.Abs(tv.MaximumPoint.Y - 25) < 1e-6 && Math.Abs(tv.MinimumPoint.Z) < 1e-6 && Math.Abs(tv.MaximumPoint.Z - 25) < 1e-6
                       && Math.Abs(Spread(tv.Intersect(new Tekla.Structures.Geometry3d.Point(tx, -2, 1), new Tekla.Structures.Geometry3d.Point(tx, 30, 1))) - 25) < 1e-6
                       && Math.Abs(SpreadZ(tv.Intersect(new Tekla.Structures.Geometry3d.Point(tx, 1, -2), new Tekla.Structures.Geometry3d.Point(tx, 1, 30))) - 25) < 1e-6;
            bool okS = Math.Abs(ts.MinimumPoint.Y) < 1e-6 && Math.Abs(ts.MaximumPoint.Y - 25) < 1e-6 && Math.Abs(ts.MinimumPoint.Z + 1.94) < 1e-6 && Math.Abs(ts.MaximumPoint.Z + 0.74) < 1e-6;
            Check("tu canh voi tiet dien xoay them " + turn + " do: V va nep van dung vi tri/chieu", okV && okS && !w.Notes[0].Contains("V xoay BELOW"));
        }
        // tu canh khong do duoc -> dung dat theo thong so + canh bao
        Reset(); FakeLog.NoSolid = true;
        w = new TeklaModelWriter(api, null);
        r = MeshFrameBuilder.Build(new MeshFrameSettings { StiffMode = StiffMode.None }, corners, w);
        Check("khong do duoc hinh khoi: van tao bang thong so + canh bao ro", r.Success && w.Messages.Count == 2 && w.Messages[0].Contains("Không đo được hình khối") && w.Messages[0].Contains("có thể lệch") && w.Messages[1].Contains("chưa được kiểm chứng") && w.Notes.Count == 0);


        Check("khong do duoc: thanh mau van bi xoa, thanh that van du", Count("Insert Beam") == 8 + 1 && Count("Delete Beam") == 1 && FakeLog.Beams.Count == 8);

        // VÒNG KÍN: thanh mẫu không đại diện (mọi thanh thật bị lệch 1.5 mm) -> đo trên thanh thật, dời, đo lại; mọi hướng thanh
        Reset(); FakeLog.Bias = 1.5;
        w = new TeklaModelWriter(api, null);
        r = MeshFrameBuilder.Build(new MeshFrameSettings { StiffMode = StiffMode.None }, corners, w);
        Check("vong kin: phat hien lech 1.5 mm tren 8 thanh, doi, con 0", r.Success && w.Messages.Count == 0 && w.Notes.Any(x => x.Contains("Đo lại trên hình khối thật: 8/8") && x.Contains("dời 8 thanh") && x.Contains("lệch lớn nhất 1.5 mm") && x.Contains("còn 0 mm")));
        {
            bool all = true; int idx = 0;
            foreach (var bm in FakeLog.Beams)
            {
                bool isV = idx < r.Plan.Bars.Count;
                V2 ps = isV ? r.Plan.Bars[idx].Start : r.Plan.Strips[idx - r.Plan.Bars.Count].Start, pe = isV ? r.Plan.Bars[idx].End : r.Plan.Strips[idx - r.Plan.Bars.Count].End;
                V2 dd = (pe - ps).Unit(), nn = dd.Left(), md = (ps + pe) * 0.5;
                Solid sd = bm.GetSolid();
                double zz = isV ? 1.0 : -1.34;
                var hit = sd.Intersect(new Tekla.Structures.Geometry3d.Point(md.X - nn.X * 40, md.Y - nn.Y * 40, zz), new Tekla.Structures.Geometry3d.Point(md.X + nn.X * 40, md.Y + nn.Y * 40, zz));
                double lo = double.MaxValue, hi = double.MinValue;
                foreach (Tekla.Structures.Geometry3d.Point q in hit) { double l = (q.X - md.X) * nn.X + (q.Y - md.Y) * nn.Y; lo = Math.Min(lo, l); hi = Math.Max(hi, l); }
                // V: cánh nằm [0, 25] so với đường tham chiếu của kế hoạch; nẹp: [-12.5, 12.5] quanh đường tim
                all &= hit.Count >= 2 && Math.Abs(lo - (isV ? 0 : -12.5)) < 1e-6 && Math.Abs(hi - (isV ? 25 : 12.5)) < 1e-6;
                idx++;
            }
            Check("vong kin: sau khi doi moi V (0..25) va nep (-12.5..12.5) dung vi tri ke hoach", all);
        }
        FakeLog.Bias = 0;
        // tu canh tat -> khong co thanh mau
        Reset();
        MeshFrameBuilder.Build(new MeshFrameSettings { StiffMode = StiffMode.None, AutoPlacement = false }, corners, new TeklaModelWriter(api, null));
        Check("tat tu canh: khong co thanh mau, dung dung o On plane/Rotation/At depth", Count("Insert Beam") == 8 && Count("Delete Beam") == 0 && FakeLog.BeamPositions[0] == "MIDDLE/FRONT/MIDDLE");

        // ---------------------------------------------------------------- pick đa giác: vẽ điểm thật + số đo
        Reset(); FakeLog.Picked = 0; FakeLog.PickFailAt = -1;
        FakeLog.PickScript.Clear();
        foreach (var p in new[] { new[] { 0.0, 0, 0 }, new[] { 3000.0, 0, 0 }, new[] { 3000.0, 1000, 0 }, new[] { 1500.0, 1600, 0 }, new[] { 0.0, 1000, 0 } }) FakeLog.PickScript.Add(p);
        PickResult pr = TeklaPicker.PickPolygon(api, new Tekla.Structures.Model.Model());
        Check("pick 5 diem roi chuot giua: du diem, ghi ly do dung", pr.Complete && pr.Points.Length == 5 && pr.Picked == 5 && pr.StopReason != null && pr.StopReason.Contains("interrupted"));
        Check("toa do GLOBAL dung", pr.Points[1].X == 3000 && pr.Points[3].Y == 1600);
        Check("ve that: moi diem 2 net thap + 1 nhan; tu diem 2 them doan noi + so do", Count("DrawLine") == 5 * 2 + 4 && CountStart("DrawText") == 5 + 4);
        Check("nhan diem P1..P5 va so do doan (3000.0 mm, 1000.0 mm)", Count("DrawText P1") == 1 && Count("DrawText P5") == 1 && Count("DrawText 3000.0 mm") == 1 && FakeLog.Entries.Contains("DrawText 1000.0 mm"));
        Check("loi nhac diem 1, 2: chua co doan truoc", !FakeLog.Prompts[0].Contains("doan truoc") && !FakeLog.Prompts[1].Contains("doan truoc"));
        Check("loi nhac diem 3: doan truoc 3000.0 mm, chu vi 3000.0 mm", FakeLog.Prompts[2].Contains("doan truoc 3000.0 mm") && FakeLog.Prompts[2].Contains("chu vi 3000.0 mm"));
        Check("loi nhac diem 4: doan truoc 1000.0 mm, chu vi cong don 4000.0 mm", FakeLog.Prompts[3].Contains("doan truoc 1000.0 mm") && FakeLog.Prompts[3].Contains("chu vi 4000.0 mm"));
        Check("loi nhac chi ASCII + huong dan chuot giua", !FakeLog.NonAsciiPrompt && FakeLog.Prompts.All(x => x.Contains("chuot giua")));
        Check("do hoa duoc giu cho den khi Cleanup", CountStart("RemoveGraphics") == 0);
        pr.Cleanup();
        Check("Cleanup xoa dung 23 do hoa tam", Count("RemoveGraphics 23") == 1);
        Reset(); FakeLog.Picked = 0; FakeLog.PickScript.Clear();
        FakeLog.PickScript.Add(new[] { 0.0, 0, 0 }); FakeLog.PickScript.Add(new[] { 100.0, 0, 0 });
        pr = TeklaPicker.PickPolygon(api, new Tekla.Structures.Model.Model());
        Check("chi 2 diem roi ngat: chua du, bao ly do", !pr.Complete && pr.Picked == 2 && pr.StopReason != null);

        // ---------------------------------------------------------------- dịch vụ khung: tạo -> sửa (thay) -> giữ khung cũ khi lỗi
        Reset();
        string folder = Path.Combine(Path.GetTempPath(), "mf_test_" + Guid.NewGuid().ToString("N"));
        FakeLog.ModelPath = folder;
        var svc = new TeklaFrameService(api, new Tekla.Structures.Model.Model());
        var s1 = new MeshFrameSettings { AutoPlacement = false, VLegA = 25 };
        ServiceResult sr = svc.Create(s1, corners);
        Check("dich vu: tao khung moi OK, co ma, thong bao so luong", sr.Success && sr.FrameId != null && sr.Message.Contains("4 V khung") && sr.Message.Contains("3 gia cường") && sr.Message.Contains("7 nẹp"));
        string recPath = TeklaFrameStore.RecordPath(folder, sr.FrameId);
        FrameRecord rec = FrameRecord.Load(recPath);
        Check("ban ghi khung luu canh model: ma + thong so + 4 diem", rec != null && rec.Id == sr.FrameId && rec.Corners.Length == 4 && Math.Abs(rec.Settings.VLegA - 25) < 1e-9 && rec.Corners[2].Z == 5120);
        ModelObject frameMain = FakeLog.Uda.Keys.First();
        ModelObject secondary = (ModelObject)((Part)frameMain).GetAssembly().GetSecondaries()[3];
        FakeLog.Selected.Clear(); FakeLog.Selected.Add(secondary);
        string err;
        FrameRecord loaded = svc.LoadSelected(out err);
        if (loaded == null) Console.WriteLine("DEBUG LoadSelected: " + err);
        Check("chon 1 part PHU cua khung -> tim ra khung qua assembly", loaded != null && loaded.Id == sr.FrameId && svc.EditingId == sr.FrameId && err == null);
        int meshBefore = FakeLog.Meshes.Count;
        FakeLog.Entries.Clear();
        var s2 = new MeshFrameSettings { AutoPlacement = false, VLegA = 30, VLegB = 30, StripWidth = 30 };
        int cutsBefore = FakeLog.Booleans.Count;
        ServiceResult sr2 = svc.Create(s2, loaded.Corners);
        Check("cap nhat tai cho: KHONG tao beam moi, KHONG xoa beam, sua 14 part", sr2.Success && sr2.FrameId == sr.FrameId && Count("Insert Beam") == 0 && Count("Delete Beam") == 0 && Count("Modify Beam") == 14);
        Check("cap nhat tai cho: thong bao ro so part sua/them/xoa", sr2.Message.Contains("Đã cập nhật khung") && sr2.Message.Contains("sửa 14 part, thêm 0, xóa 0"));
        Check("cap nhat tai cho: ban ghi duoc ghi de bang thong so moi, het che do sua", FrameRecord.Load(recPath).Settings.VLegA == 30 && svc.EditingId == null);
        Check("cap nhat tai cho: vet cat cu bi xoa va lam lai (khong chong chat, so luong khong doi), luoi cu bi xoa roi tao lai", FakeLog.Booleans.Count == cutsBefore && FakeLog.Meshes.Count == meshBefore);
        Check("cap nhat tai cho: profile cua chinh part cu doi sang L30*30*2", FakeLog.Beams.Where(x => x.Profile.ProfileString.StartsWith("L30")).Count() >= 7);
        // đổi số thanh gia cường (3 -> 1): sửa 10 part, xóa 4 part thừa
        ModelObject main3 = FakeLog.Uda.Keys.First();
        FakeLog.Selected.Clear(); FakeLog.Selected.Add(main3);
        loaded = svc.LoadSelected(out err);
        FakeLog.Entries.Clear();
        var s3 = new MeshFrameSettings { AutoPlacement = false, VLegA = 30, VLegB = 30, StripWidth = 30, StiffMax = 2000 };
        ServiceResult sr2b = svc.Create(s3, loaded.Corners);
        Check("cap nhat tai cho doi so gia cuong 3 -> 1: sua 10, xoa 4 part thua, khong tao moi", sr2b.Success && Count("Insert Beam") == 0 && Count("Delete Beam") == 4 && sr2b.Message.Contains("sửa 10 part, thêm 0, xóa 4"));
        // cập nhật tại chỗ lỗi (Modify lần thứ 3 thất bại) -> dựng lại khung mới + xóa khung cũ, không chồng hai khung
        FakeLog.Selected.Clear(); FakeLog.Selected.Add(main3);
        loaded = svc.LoadSelected(out err);
        FakeLog.Entries.Clear(); FakeLog.ModifyCount = 0; FakeLog.FailModifyAt = 3;
        ServiceResult sr3 = svc.Create(s2, loaded.Corners);
        FakeLog.FailModifyAt = -1;
        Check("cap nhat tai cho loi: dung lai khung moi va xoa het khung cu, noi ro ly do", sr3.Success && sr3.Message.Contains("dựng lại khung mới") && Count("Insert Beam") == 14 && Count("Delete Beam") >= 10 && sr3.Message.Contains("Đã thay khung cũ"));
        ModelObject main2 = FakeLog.Uda.Keys.Last();
        svc.StopEditing();
        // lỗi khi tạo khung MỚI được rollback
        FakeLog.Entries.Clear(); FakeLog.BeamCount = 0; FakeLog.FailBeamAt = 3;
        ServiceResult sr3b = svc.Create(new MeshFrameSettings { AutoPlacement = false }, corners);
        FakeLog.FailBeamAt = -1;
        Check("tao moi bi loi: rollback 2 beam da tao, bao loi", !sr3b.Success && Count("Delete Beam") >= 2);
        // part không thuộc assembly nào (GetAssembly = null): trước đây NullReferenceException
        FakeLog.NullAssembly = true;
        FakeLog.Selected.Clear(); FakeLog.Selected.Add(secondary);
        Check("chon part khong thuoc assembly: bao 'chua chon khung', KHONG loi NullReference", svc.LoadSelected(out err) == null && err.Contains("Chưa chọn khung"));
        FakeLog.NullAssembly = false;
        // không chọn / thiếu bản ghi
        FakeLog.Selected.Clear();
        Check("khong chon gi: bao chua chon khung", svc.LoadSelected(out err) == null && err.Contains("Chưa chọn khung"));
        File.Delete(recPath);
        FakeLog.Selected.Add(main2);
        Check("co ma nhung mat ban ghi: bao ro", svc.LoadSelected(out err) == null && err.Contains("không có bản ghi"));
        FakeLog.ModelPath = "";
        var svc2 = new TeklaFrameService(api, new Tekla.Structures.Model.Model());
        ServiceResult sr4 = svc2.Create(s1, corners);
        Check("model chua luu: van tao, canh bao khong luu duoc ban ghi", sr4.Success && sr4.Message.Contains("Model chưa lưu"));
        try { Directory.Delete(folder, true); } catch (IOException) { }

        // ---------------------------------------------------------------- đọc catalog (cho ô chọn mác/cỡ sợi trong app)
        FakeCatalog.Reset();
        var rebars = TeklaCatalog.ReadRebars(api);
        Check("doc catalog rebar: 3 muc", rebars.Count == 3 && rebars[0].Grade == "SUS304" && rebars[0].Size == "0.37");
        Check("catalog Check: hop le -> null", TeklaCatalog.Check(api, "SUS304", "0.37") == null);

        // ---------------------------------------------------------------- đọc đối tượng đang chọn (lưới mẫu)
        Reset();
        var sample = new RebarMesh { Name = "SAMPLE-MESH", Grade = "SUS304", Class = 6, LongitudinalSize = "0.37" };
        FakeLog.Selected.Add(sample);
        string probe = TeklaProbe.DumpSelected(api);
        string dump = File.ReadAllText(TeklaProbe.OutputPath);
        Check("doc luoi mau: ghi file, co thuoc tinh va gia tri", probe.Contains("selected-object.txt") && dump.Contains("Name = SAMPLE-MESH") && dump.Contains("LongitudinalSize = 0.37") && dump.Contains("MeshType = UNKNOWN_MESH"));
        FakeLog.Selected.Clear();
        Check("doc luoi mau: chua chon -> nhac", TeklaProbe.DumpSelected(api).Contains("Chưa chọn"));

        // ---------------------------------------------------------------- mọi lời gọi SDK đi qua Invoke (SdkThread của Cover Tool)
        int hooked = 0;
        api.Invoke = delegate (Func<object> f) { hooked++; return f(); };
        Reset();
        MeshFrameBuilder.Build(new MeshFrameSettings { AutoPlacement = false, StiffMode = StiffMode.None, UseStrip = false }, corners, new TeklaModelWriter(api, null));
        Check("moi loi goi SDK qua Invoke", hooked > 50);

        // ---------------------------------------------------------------- hợp đồng ngay trong app + phân biệt lỗi API với ngắt pick
        var full = TeklaContract.VerifyLoaded(api);
        Check("kiem tra API trong app: SDK day du -> khong thieu gi", full.Required.Count == 0 && full.Optional.Count == 0);
        Check("ham tuy chon duoc nhan dien, ham bat buoc thi khong", TeklaContract.IsOptional("call Tekla.Structures.Model.UI.Picker::PickPoint(System.String;Tekla.Structures.Geometry3d.Point)") && TeklaContract.IsOptional("call Tekla.Structures.Model.UI.GraphicsDrawer::DrawText(x)") && !TeklaContract.IsOptional("call Tekla.Structures.Model.Beam::Insert()"));
        Check("IsApiError: thieu ham/kieu la loi API, nguoi dung ngat pick thi khong", TeklaPicker.IsApiError(new MissingMethodException("x")) && TeklaPicker.IsApiError(new MissingMemberException("x"))
              && TeklaPicker.IsApiError(new TypeLoadException("x")) && !TeklaPicker.IsApiError(new ApplicationException("User interrupted")));

        File.WriteAllLines(a[1], used.OrderBy(x => x).ToArray());
        Console.WriteLine("writer pass " + pass + " fail " + fail + "  (thanh vien Tekla da dung: " + used.Count + ")");
        return fail == 0 ? 0 : 1;
    }
}
