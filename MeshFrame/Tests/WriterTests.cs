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
        Check("1 RebarMesh", Count("Insert RebarMesh") == 1);
        Check("7 BoltArray (1/nep)", Count("Insert BoltArray") == 7);
        Check("assembly: 13 Add + Modify", Count("Add Beam") == 13 && Count("Modify Assembly") == 1);
        Check("mat phang doi roi tra lai + commit", Count("SetPlane") == 2 && Count("Commit") == 1);
        Check("khong co canh bao", w.Messages.Count == 0);
        Check("mat phang dat truoc khi chen", FakeLog.Entries.IndexOf("SetPlane") < FakeLog.Entries.IndexOf("Insert Beam"));
        Check("lưới chen sau thanh", FakeLog.Entries.IndexOf("Insert RebarMesh") > FakeLog.Entries.LastIndexOf("Insert Beam"));

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
        FakeLog.Entries.Clear(); FakeLog.RejectInsert = true;
        bool threw = false;
        try { MeshFrameBuilder.Build(new MeshFrameSettings(), corners, new TeklaModelWriter(api, null)); }
        catch (InvalidOperationException e) { threw = e.Message.Contains("profile"); }
        FakeLog.RejectInsert = false;
        Check("Insert false -> loi nhac kiem tra profile", threw);
        Check("loi van tra mat phang + commit", Count("SetPlane") == 2 && Count("Commit") == 1);

        // --- pick 4 điểm (hồi quy: Point.X/Y/Z là FIELD, trước đây lỗi sau lần pick đầu)
        FakeLog.Picked = 0; FakeLog.PickFailAt = -1;
        PickResult pr = TeklaPicker.PickCorners(api, new Tekla.Structures.Model.Model());
        Check("pick du 4 diem", pr.Complete && pr.Picked == 4 && pr.Corners[3].X == 400 && pr.Corners[0].Y == 10 && pr.Corners[0].Z == 5);
        FakeLog.Picked = 0; FakeLog.PickFailAt = 1;                    // Esc ở điểm thứ 2
        pr = TeklaPicker.PickCorners(api, new Tekla.Structures.Model.Model());
        Check("Esc o diem 2: dung, noi ro ly do", !pr.Complete && pr.Picked == 1 && pr.StopReason != null && pr.StopReason.Contains("interrupted"));
        FakeLog.PickFailAt = -1;

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
