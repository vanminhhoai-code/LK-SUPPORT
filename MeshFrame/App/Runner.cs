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

            var svc = new TeklaFrameService(api, model);
            PickResult lastPick = null;
            Func<V3[]> pick = delegate
            {
                if (lastPick != null && lastPick.Cleanup != null) lastPick.Cleanup();     // xóa đồ họa tạm của lần pick trước
                lastPick = TeklaPicker.PickPolygon(api, model);
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
