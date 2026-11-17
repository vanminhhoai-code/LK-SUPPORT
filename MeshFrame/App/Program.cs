// MeshFrame.exe - ứng dụng chạy riêng, kết nối vào Tekla đang mở (cùng kiểu Cover Tool: nạp DLL Tekla theo đường dẫn lúc chạy).
// Luồng: hộp thoại thông số -> pick 4 điểm trong Tekla -> tạo khung + gia cường + nẹp + lưới + vít -> lặp lại.
using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace MeshFrame.App
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string bin = TeklaLocator.Find(args);
            if (bin == null)
            {
                MessageBox.Show("Không tìm thấy thư mục Tekla (Tekla.Structures.Model.dll).\n\n" +
                                "Cách chỉ đường dẫn:\n  • chạy với  --tekla-bin=\"C:\\TeklaStructures\\2025.0\\bin\"\n" +
                                "  • hoặc ghi  TeklaBin=...  vào MeshFrame.ini cạnh file exe.", "MeshFrame");
                return 2;
            }

            // Phải gắn bộ nạp DLL TRƯỚC khi chạy bất kỳ hàm nào dùng kiểu Tekla (Runner nằm ở hàm riêng).
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object s, ResolveEventArgs e)
            {
                string p = Path.Combine(bin, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(p) ? Assembly.LoadFrom(p) : null;
            };
            return Runner.Run();
        }
    }

    internal static class TeklaLocator
    {
        private static readonly string[] Candidates =
        {
            @"C:\TeklaStructures\2025.0\bin", @"C:\Program Files\Tekla Structures\2025.0\bin",
            @"C:\TeklaStructures\2020.0\bin", @"C:\Program Files\Tekla Structures\2020.0\bin",
            @"C:\TeklaStructures\21.1\nt\bin\plugins", @"C:\Program Files\Tekla Structures\21.1\nt\bin\plugins"
        };

        public static string Find(string[] args)
        {
            foreach (string a in args)
                if (a.StartsWith("--tekla-bin=", StringComparison.OrdinalIgnoreCase)) { string d = a.Substring(12).Trim('"'); if (Ok(d)) return d; }

            string ini = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MeshFrame.ini");
            if (File.Exists(ini))
                foreach (string line in File.ReadAllLines(ini))
                    if (line.StartsWith("TeklaBin=", StringComparison.OrdinalIgnoreCase)) { string d = line.Substring(9).Trim().Trim('"'); if (Ok(d)) return d; }

            string env = Environment.GetEnvironmentVariable("MESHFRAME_TEKLA_BIN");
            if (!string.IsNullOrEmpty(env) && Ok(env)) return env;

            foreach (string c in Candidates) if (Ok(c)) return c;
            return null;
        }

        private static bool Ok(string dir) { return Directory.Exists(dir) && File.Exists(Path.Combine(dir, "Tekla.Structures.Model.dll")); }
    }
}
