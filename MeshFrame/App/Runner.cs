using System;
using System.IO;
using System.Text;
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

            MeshFrameSettings settings = SettingsStore.Load(SettingsPath);
            while (true)
            {
                using (var dlg = new MeshFrameDialog())
                {
                    dlg.Text = title;
                    dlg.Settings = settings;
                    if (dlg.ShowDialog() != DialogResult.OK) break;
                    settings = dlg.Settings;
                }
                try { SettingsStore.Save(settings, SettingsPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }

                PickResult pick;
                try { pick = TeklaPicker.PickCorners(api, model); }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi đọc điểm pick:\n" + Root(ex).GetType().Name + ": " + Root(ex).Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }
                if (!pick.Complete)                          // Esc / Tekla dừng pick -> nói rõ, rồi quay lại hộp thoại
                {
                    MessageBox.Show("Pick dừng ở " + pick.Picked + "/4 điểm, chưa tạo gì.\n" + pick.StopReason, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    continue;
                }
                V3[] corners = pick.Corners;

                var writer = new TeklaModelWriter(api, model);
                BuildResult r;
                try { r = MeshFrameBuilder.Build(settings, corners, writer); }
                catch (Exception ex)                           // lỗi từ Tekla (profile, catalog, ...)
                {
                    MessageBox.Show("Lỗi khi tạo trong Tekla:\n" + Root(ex).Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }
                Report(r, writer, title);
            }
            return 0;
        }

        private static void Report(BuildResult r, TeklaModelWriter w, string title)
        {
            var sb = new StringBuilder();
            if (r.Success)
            {
                MeshFramePlan p = r.Plan;
                int stiff = 0; foreach (PlanBar b in p.Bars) if (b.Role == BarRole.Stiffener) stiff++;
                sb.AppendLine(string.Format("Đã tạo: V khung 4, gia cường {0}, nẹp {1}, vít {2}.", stiff, p.Strips.Count, p.Fasteners.Count));
                foreach (string s in p.Warnings) sb.AppendLine("⚠ " + s);
                foreach (string s in w.Messages) sb.AppendLine("⚠ " + s);
            }
            else foreach (string e in r.Errors) sb.AppendLine("✖ " + e);
            MessageBox.Show(sb.ToString(), title, MessageBoxButtons.OK, r.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private static Exception Root(Exception e) { while (e.InnerException != null) e = e.InnerException; return e; }
    }
}
