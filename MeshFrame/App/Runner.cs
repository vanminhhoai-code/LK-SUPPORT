using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using MeshFrame.Core;
using MeshFrame.Tekla;
using MeshFrame.UI;
using TSG = Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;
using TSMUI = Tekla.Structures.Model.UI;

namespace MeshFrame.App
{
    internal static class Runner
    {
        private static readonly string SettingsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeshFrame", "settings.ini");

        public static int Run()
        {
            var model = new TSM.Model();
            if (!model.GetConnectionStatus())
            {
                MessageBox.Show("Chưa kết nối được Tekla. Mở Tekla và một model trước, rồi chạy lại MeshFrame.", "MeshFrame");
                return 3;
            }

            MeshFrameSettings settings = SettingsStore.Load(SettingsPath);
            while (true)
            {
                using (var dlg = new MeshFrameDialog())
                {
                    dlg.Settings = settings;
                    if (dlg.ShowDialog() != DialogResult.OK) break;
                    settings = dlg.Settings;
                }
                try { SettingsStore.Save(settings, SettingsPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }

                V3[] corners = PickCorners(model);
                if (corners == null) continue;               // hủy pick -> quay lại hộp thoại

                var writer = new TeklaModelWriter();
                BuildResult r;
                try { r = MeshFrameBuilder.Build(settings, corners, writer); }
                catch (Exception ex)                           // lỗi từ Tekla (profile, catalog, ...)
                {
                    MessageBox.Show("Lỗi khi tạo trong Tekla:\n" + ex.Message, "MeshFrame", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }
                Report(r, writer);
            }
            return 0;
        }

        /// <summary>Pick 4 điểm trong Tekla (theo work plane hiện hành) rồi đổi sang toạ độ GLOBAL. Hủy pick -> null.</summary>
        private static V3[] PickCorners(TSM.Model model)
        {
            try
            {
                var picker = new TSMUI.Picker();
                TSG.Matrix toGlobal = model.GetWorkPlaneHandler().GetCurrentTransformationPlane().TransformationMatrixToGlobal;
                var c = new V3[4];
                for (int i = 0; i < 4; i++)
                {
                    TSG.Point p = picker.PickPoint("MeshFrame: pick điểm P" + (i + 1) + " / 4 (đi liên tiếp quanh khung, ngược chiều kim đồng hồ nhìn từ trên)");
                    TSG.Point g = toGlobal.Transform(p);
                    c[i] = new V3(g.X, g.Y, g.Z);
                }
                return c;
            }
            catch (Exception)                                   // Tekla ném lỗi khi người dùng nhấn Esc / hủy pick
            {
                return null;
            }
        }

        private static void Report(BuildResult r, TeklaModelWriter w)
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
            MessageBox.Show(sb.ToString(), "MeshFrame", MessageBoxButtons.OK, r.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
    }
}
