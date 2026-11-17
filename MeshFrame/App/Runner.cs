using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using MeshFrame.Core;
using MeshFrame.Tekla;
using MeshFrame.UI;

namespace MeshFrame.App
{
    internal static class Runner
    {
        private static readonly string SettingsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeshFrame", "settings.ini");

        public static int Run(string release, string bin)
        {
            string title = "MeshFrame - Tekla " + release;
            TeklaApi api; object model;
            try
            {
                api = TeklaApi.Load(bin);
                model = api.New(api.ModelType("Model"));
                if (!(bool)api.Call(model, "GetConnectionStatus"))
                {
                    MessageBox.Show("Chưa kết nối được Tekla " + release + ". Mở Tekla và một model trước, rồi chạy lại MeshFrame.", title);
                    return 3;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không nạp được API Tekla " + release + " (" + bin + "):\n" + Root(ex).Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 3;
            }

            // Bản Tekla này có đủ hàm API MeshFrame cần không? (21.1 / 2020 có thể thiếu so với 2021 / 2025)
            Func<string> apiCheck = delegate
            {
                List<string> missing = TeklaContract.VerifyLoaded(api);
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeshFrame", "api-missing.txt");
                if (missing.Count == 0) return "Tekla " + release + ": đủ mọi hàm API mà MeshFrame dùng.";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, missing.ToArray());
                var head = new List<string>(); for (int i = 0; i < Math.Min(8, missing.Count); i++) head.Add("• " + missing[i].Split(new[] { "   <--" }, StringSplitOptions.None)[0]);
                return "Tekla " + release + " THIẾU " + missing.Count + " thành viên API mà MeshFrame dùng:\n\n" + string.Join("\n", head.ToArray()) + (missing.Count > 8 ? "\n..." : "")
                       + "\n\nDanh sách đầy đủ: " + path + "\nGửi file này cho người hỗ trợ; chức năng liên quan có thể lỗi.";
            };
            try
            {
                string startup = apiCheck();
                if (startup.Contains("THIẾU")) MessageBox.Show(startup, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception) { }

            var svc = new TeklaFrameService(api, model);
            PickResult lastPick = null;
            Func<V3[]> pick = delegate
            {
                if (lastPick != null && lastPick.Cleanup != null) lastPick.Cleanup();     // xóa đồ họa tạm của lần pick trước
                try { lastPick = TeklaPicker.PickPolygon(api, model); }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi pick (bản Tekla này thiếu hàm API?):\n" + Root(ex).GetType().Name + ": " + Root(ex).Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
                if (!lastPick.Complete)
                {
                    MessageBox.Show("Pick dừng ở " + lastPick.Picked + " điểm, cần ít nhất 3 điểm. Chưa có gì được tạo.\n" + lastPick.StopReason, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return null;
                }
                if (lastPick.DrawNote != null) MessageBox.Show(lastPick.DrawNote, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return lastPick.Points;
            };

            using (var dlg = new MeshFrameDialog())
            {
                dlg.Text = title;
                dlg.Settings = SettingsStore.Load(SettingsPath);
                dlg.PickHandler = pick;
                dlg.SelectedObjectProbe = delegate { return TeklaProbe.DumpSelected(api); };
                dlg.ApiCheckHandler = apiCheck;
                dlg.CreateHandler = delegate (MeshFrameSettings s, V3[] pts)
                {
                    ServiceResult r = svc.Create(s, pts);
                    return new CreateOutcome { Success = r.Success, Message = r.Message };
                };
                dlg.EditHandler = delegate
                {
                    string err;
                    FrameRecord rec = svc.LoadSelected(out err);
                    return rec == null ? new LoadedFrame { Error = err } : new LoadedFrame { Settings = rec.Settings, Points = rec.Corners, Id = rec.Id };
                };
                try
                {
                    if (api.HasCatalogs)
                    {
                        var items = new List<string[]>();
                        foreach (RebarEntry e in TeklaCatalog.ReadRebars(api)) items.Add(new[] { e.Grade, e.Size });
                        dlg.SetRebarCatalog(items);
                    }
                }
                catch (Exception) { }                                                     // không đọc được catalog: ô mác/cỡ sợi để nhập tay

                V3[] first = pick();                                                      // pick ngay khi mở app; hủy thì vào hộp thoại trống
                if (first != null) dlg.Points = first;
                dlg.ShowDialog();
                try { SettingsStore.Save(dlg.Settings, SettingsPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            if (lastPick != null && lastPick.Cleanup != null) lastPick.Cleanup();
            return 0;
        }

        private static Exception Root(Exception e) { while (e.InnerException != null) e = e.InnerException; return e; }
    }
}
