// Test bộ ghi trên SDK GIẢ: kiểm thứ tự/số lượng thao tác và ghi lại mọi thành viên Tekla đã dùng (cho ContractCheck).
// Dùng: WriterTests.exe <thư mục chứa Tekla.Structures*.dll giả> <file ghi thành viên>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeshFrame.Core;
using MeshFrame.Tekla;
using Tekla.Structures.Model;

static class WriterTests
{
    static int fail, pass;
    static void Check(string n, bool ok) { if (ok) pass++; else { fail++; Console.WriteLine("FAIL: " + n); } }
    static int Count(string prefix) { return FakeLog.Entries.Count(e => e == prefix); }

    static int Main(string[] a)
    {
        TeklaApi api = TeklaApi.Load(a[0]);
        var used = new HashSet<string>();
        api.Trace = delegate (string s) { used.Add(s); };

        var corners = new[] { new V3(0, 0, 5000), new V3(4000, 0, 5000), new V3(4000, 1200, 5120), new V3(0, 1200, 5120) };

        // --- đầy đủ: khung + 3 gia cường + nẹp + lưới + vít
        FakeLog.Entries.Clear();
        var w = new TeklaModelWriter(api, null);
        BuildResult r = MeshFrameBuilder.Build(new MeshFrameSettings(), corners, w);
        Check("build ok", r.Success);
        Check("14 beam (7 V + 7 nep)", Count("Insert Beam") == 14);
        Check("16 fitting (4 V + 4 nep, 2 dau)", Count("Insert Fitting") == 16);
        Check("mac dinh: luoi dang tam (khong RebarMesh)", Count("Insert RebarMesh") == 0);
        Check("khuyet: 6 tam cat + 1 tam luoi = 7 ContourPlate, 6 BooleanPart, xoa dung 6 tam cat", Count("Insert ContourPlate") == 7 && Count("Insert BooleanPart") == 6 && Count("Delete ContourPlate") == 6);
        Check("tam cat dung class BlOpCl cua Tekla", FakeLog.Entries.Count(e => e == "Cut BlOpCl by plate") == 6);
        Check("tam cat khong vao assembly, tam luoi vao assembly", Count("Add ContourPlate") == 1);
        Check("7 BoltArray (1/nep)", Count("Insert BoltArray") == 7);
        Check("assembly: 13 beam + Modify", Count("Add Beam") == 13 && Count("Modify Assembly") == 1);
        Check("mat phang doi roi tra lai + commit", Count("SetPlane") == 2 && Count("Commit") == 1);
        Check("khong co canh bao", w.Messages.Count == 0);
        Check("mat phang dat truoc khi chen", FakeLog.Entries.IndexOf("SetPlane") < FakeLog.Entries.IndexOf("Insert Beam"));
        Check("luoi chen sau thanh", FakeLog.Entries.LastIndexOf("Insert ContourPlate") > FakeLog.Entries.LastIndexOf("Insert Beam"));

        // --- RebarMesh chọn tay: chèn được, không vào assembly
        FakeLog.Entries.Clear();
        w = new TeklaModelWriter(api, null);
        MeshFrameBuilder.Build(new MeshFrameSettings { MeshKind = MeshKind.RebarMesh }, corners, w);
        Check("RebarMesh: 1 mesh, khong tam luoi, khong vao assembly", Count("Insert RebarMesh") == 1 && Count("Insert ContourPlate") == 6 && Count("Add ContourPlate") == 0 && w.Messages.Count == 0);

        // --- RebarMesh lỗi (như Tekla 21.1 thật): KHÔNG làm mất vít + assembly, chỉ cảnh báo
        FakeLog.Entries.Clear(); FakeLog.RejectRebarMesh = true;
        w = new TeklaModelWriter(api, null);
        r = MeshFrameBuilder.Build(new MeshFrameSettings { MeshKind = MeshKind.RebarMesh }, corners, w);
        FakeLog.RejectRebarMesh = false;
        Check("RebarMesh loi: van thanh cong", r.Success);
        Check("RebarMesh loi: van tao vit + gop assembly", Count("Insert BoltArray") == 7 && Count("Modify Assembly") == 1 && Count("Add Beam") == 13);
        Check("RebarMesh loi: canh bao ro ly do", w.Messages.Count == 1 && w.Messages[0].Contains("required info missing") && w.Messages[0].Contains("vẫn được tạo"));
        Check("RebarMesh loi: van tra mat phang + commit", Count("SetPlane") == 2 && Count("Commit") == 1);

        // --- không tạo lưới
        FakeLog.Entries.Clear();
        w = new TeklaModelWriter(api, null);
        MeshFrameBuilder.Build(new MeshFrameSettings { MeshKind = MeshKind.None }, corners, w);
        Check("khong luoi: khong tam luoi, khong RebarMesh", Count("Insert RebarMesh") == 0 && Count("Insert ContourPlate") == 6 && Count("Add ContourPlate") == 0 && Count("Insert BoltArray") == 7);

        // --- lỗi nặng giữa chừng: xóa sạch những gì đã tạo, không gộp assembly
        FakeLog.Entries.Clear(); FakeLog.BeamCount = 0; FakeLog.FailBeamAt = 3;
        bool rolled = false;
        try { MeshFrameBuilder.Build(new MeshFrameSettings(), corners, new TeklaModelWriter(api, null)); }
        catch (InvalidOperationException) { rolled = true; }
        FakeLog.FailBeamAt = -1;
        Check("beam thu 3 loi -> nem loi", rolled);
        Check("rollback: xoa 2 beam da tao, khong gop assembly", Count("Delete Beam") == 2 && Count("Modify Assembly") == 0);
        Check("rollback: van tra mat phang + commit", Count("SetPlane") == 2 && Count("Commit") == 1);

        // --- đường tham chiếu: đặt theo tim (Middle) -> V cách mép ngoài legA/2, cao legB/2; nẹp ở tim nẹp, ngoài lưới
        FakeLog.Entries.Clear(); FakeLog.BeamStarts.Clear(); FakeLog.BeamCount = 0;
        MeshFrameBuilder.Build(new MeshFrameSettings(), corners, new TeklaModelWriter(api, null));
        double[] v0 = FakeLog.BeamStarts[0], s0 = FakeLog.BeamStarts[7];
        Check("V khung: tham chieu o tim canh nam (y=12.5) va tim chieu cao (z=12.5)", Math.Abs(v0[0] - 0) < 1e-6 && Math.Abs(v0[1] - 12.5) < 1e-6 && Math.Abs(v0[2] - 12.5) < 1e-6);
        Check("nep: tim o y=12.5 (trung tim canh nam V), z = -0.74 - 0.6", Math.Abs(s0[0] - 12.5) < 1e-6 && Math.Abs(s0[1] - 12.5) < 1e-6 && Math.Abs(s0[2] - (-1.34)) < 1e-6);
        Check("V va nep cung tim ngang (y)", Math.Abs(v0[1] - s0[1]) < 1e-6);
        FakeLog.Entries.Clear(); FakeLog.BeamStarts.Clear(); FakeLog.BeamCount = 0;
        MeshFrameBuilder.Build(new MeshFrameSettings { VLateral = 3, StripLateral = -2, StiffMode = StiffMode.None }, corners, new TeklaModelWriter(api, null));
        Check("lech ngang nguoi dung cong vao", Math.Abs(FakeLog.BeamStarts[0][1] - 15.5) < 1e-6 && Math.Abs(FakeLog.BeamStarts[4][1] - 10.5) < 1e-6);

        // --- đọc đối tượng đang chọn (lưới mẫu)
        FakeLog.Selected.Clear();
        var sample = new RebarMesh { Name = "SAMPLE-MESH", Grade = "SUS304", Class = 6, LongitudinalSize = "0.37" };
        sample.Father = null; FakeLog.Selected.Add(sample);
        string probe = TeklaProbe.DumpSelected(api);
        string dump = File.ReadAllText(TeklaProbe.OutputPath);
        Check("doc luoi mau: ghi file", probe.Contains("selected-object.txt") && dump.Contains("RebarMesh"));
        Check("doc luoi mau: co thuoc tinh va gia tri", dump.Contains("Name = SAMPLE-MESH") && dump.Contains("LongitudinalSize = 0.37") && dump.Contains("Class = 6"));
        FakeLog.Selected.Clear();
        Check("doc luoi mau: chua chon -> nhac", TeklaProbe.DumpSelected(api).Contains("Chưa chọn"));

        // --- không nẹp, không gia cường
        FakeLog.Entries.Clear();
        w = new TeklaModelWriter(api, null);
        r = MeshFrameBuilder.Build(new MeshFrameSettings { UseStrip = false, StiffMode = StiffMode.None }, corners, w);
        Check("khong nep: 4 beam, 4 fitting, 0 bolt", r.Success && Count("Insert Beam") == 4 && Count("Insert Fitting") == 8 && Count("Insert BoltArray") == 0);

        // --- hàn điểm: cảnh báo, không bolt
        FakeLog.Entries.Clear();
        w = new TeklaModelWriter(api, null);
        MeshFrameBuilder.Build(new MeshFrameSettings { Fasten = FastenKind.SpotWeld }, corners, w);
        Check("han diem: canh bao + khong bolt", Count("Insert BoltArray") == 0 && w.Messages.Count == 1);

        // --- Tekla từ chối Insert -> lỗi rõ + vẫn trả mặt phẳng làm việc
        FakeLog.Entries.Clear(); FakeLog.BeamCount = 0; FakeLog.RejectInsert = true;
        bool threw = false;
        try { MeshFrameBuilder.Build(new MeshFrameSettings(), corners, new TeklaModelWriter(api, null)); }
        catch (InvalidOperationException e) { threw = e.Message.Contains("profile"); }
        FakeLog.RejectInsert = false;
        Check("Insert false -> loi nhac kiem tra profile", threw);
        Check("loi van tra mat phang + commit", Count("SetPlane") == 2 && Count("Commit") == 1);

        // --- mọi giá trị enum hướng đặt người dùng chọn được phải là enum Tekla có thật (ghi vào members để đối chiếu DLL thật)
        foreach (PlaneKind k in Enum.GetValues(typeof(PlaneKind))) api.Enum("Position+PlaneEnum", PlanPlacement.Name(k));
        foreach (RotationKind k in Enum.GetValues(typeof(RotationKind))) api.Enum("Position+RotationEnum", PlanPlacement.Name(k));
        foreach (DepthKind k in Enum.GetValues(typeof(DepthKind))) api.Enum("Position+DepthEnum", PlanPlacement.Name(k));

        // --- hướng đặt từ plan thật sự được ghi vào Tekla
        FakeLog.Entries.Clear(); FakeLog.BeamPositions.Clear();
        var set = new MeshFrameSettings { VPlane = PlaneKind.Right, VRotation = RotationKind.Below, VDepth = DepthKind.Middle, StiffMode = StiffMode.None, UseStrip = false };
        MeshFrameBuilder.Build(set, corners, new TeklaModelWriter(api, null));
        Check("huong dat tu thong so duoc ghi vao beam", FakeLog.BeamPositions.Count == 4 && FakeLog.BeamPositions.All(x => x == "RIGHT/BELOW/MIDDLE"));

        // --- pick 4 điểm (hồi quy: Point.X/Y/Z là FIELD, trước đây lỗi sau lần pick đầu)
        FakeLog.Picked = 0; FakeLog.PickFailAt = -1;
        PickResult pr = TeklaPicker.PickCorners(api, new Tekla.Structures.Model.Model());
        Check("pick du 4 diem", pr.Complete && pr.Picked == 4 && pr.Corners[3].X == 400 && pr.Corners[0].Y == 10 && pr.Corners[0].Z == 5);
        FakeLog.Picked = 0; FakeLog.PickFailAt = 1;                    // Esc ở điểm thứ 2
        pr = TeklaPicker.PickCorners(api, new Tekla.Structures.Model.Model());
        Check("Esc o diem 2: dung, noi ro ly do", !pr.Complete && pr.Picked == 1 && pr.StopReason != null && pr.StopReason.Contains("interrupted"));
        FakeLog.PickFailAt = -1;
        Check("loi nhac pick chi ASCII (Tekla loi font neu co dau)", !FakeLog.NonAsciiPrompt);

        // --- SdkThread hook
        int hooked = 0;
        api.Invoke = delegate (Func<object> f) { hooked++; return f(); };
        FakeLog.Entries.Clear();
        MeshFrameBuilder.Build(new MeshFrameSettings { UseStrip = false, StiffMode = StiffMode.None }, corners, new TeklaModelWriter(api, null));
        Check("moi loi goi SDK qua Invoke", hooked > 50);

        File.WriteAllLines(a[1], used.OrderBy(x => x).ToArray());
        Console.WriteLine("writer pass " + pass + " fail " + fail + "  (thanh vien Tekla da dung: " + used.Count + ")");
        return fail == 0 ? 0 : 1;
    }
}
