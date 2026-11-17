// MeshFrame.exe - ứng dụng chạy riêng, kết nối vào Tekla đang mở. MỘT bản exe cho 21.1 / 2020 / 2025:
// API Tekla được nạp lúc chạy theo --tekla-version (cùng cách Cover Tool), không tham chiếu cứng DLL Tekla.
// Luồng: hộp thoại thông số -> pick 4 điểm trong Tekla -> tạo khung + gia cường + nẹp + lưới + vít -> lặp lại.
using System;
using System.IO;
using System.Windows.Forms;

namespace MeshFrame.App
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Mọi lỗi khởi động/chạy đều hiện hộp thoại và ghi log -> không bao giờ "mở không lên mà không rõ vì sao".
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e) { Fail(e.ExceptionObject as Exception); };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e) { Fail(e.Exception); };
            try { return Start(args); }
            catch (Exception ex) { Fail(ex); return 1; }
        }

        private static void Fail(Exception ex)
        {
            string text = ex == null ? "Lỗi không rõ." : ex.ToString();
            string log = null;
            foreach (string dir in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeshFrame"), AppDomain.CurrentDomain.BaseDirectory, Path.GetTempPath() })
            {
                try { Directory.CreateDirectory(dir); log = Path.Combine(dir, "MeshFrame-error.log"); File.AppendAllText(log, DateTime.Now.ToString("s") + "\r\n" + text + "\r\n\r\n"); break; }
                catch (Exception) { log = null; }
            }
            try { MessageBox.Show("MeshFrame gặp lỗi:\n\n" + (ex == null ? text : ex.Message) + (log == null ? "" : "\n\nChi tiết: " + log), "MeshFrame", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch (Exception) { }
        }

        private static int Start(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string release, bin;
            if (!TeklaLocator.Resolve(args, out release, out bin))
            {
                MessageBox.Show("Không tìm thấy Tekla.Structures.dll + Tekla.Structures.Model.dll.\n\n" +
                                "• chạy với  --tekla-version=21.1 | 2020 | 2025\n" +
                                "• hoặc  --tekla-bin=\"C:\\TeklaStructures\\2025.0\\bin\"\n" +
                                "• hoặc ghi  TeklaBin=...  vào MeshFrame.ini cạnh file exe.", "MeshFrame");
                return 2;
            }
            return Runner.Run(release, bin);
        }
    }
}
