// MeshFrame.exe - ứng dụng chạy riêng, kết nối vào Tekla đang mở. MỘT bản exe cho 21.1 / 2020 / 2025:
// API Tekla được nạp lúc chạy theo --tekla-version (cùng cách Cover Tool), không tham chiếu cứng DLL Tekla.
// Luồng: hộp thoại thông số -> pick 4 điểm trong Tekla -> tạo khung + gia cường + nẹp + lưới + vít -> lặp lại.
using System;
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
