// Phần "chủ" tối thiểu để dùng NGUYÊN VẸN UI/UiTheme.cs (bản sao không sửa của Cover Tool 1.7.128, CoreSource/UiTheme.cs).
// Khi tích hợp vào Cover Tool: xóa file này và UiTheme.cs, dùng trực tiếp lớp UiTheme / UiFonts của Cover Tool.
// Cover Tool có thêm video nền nhà xưởng (UiBackdrop, FactoryWelcome...) - MeshFrame không dùng: các lớp dưới đây chỉ là chỗ trống.
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TeklaQuickAssign
{
    internal static class TeklaConnectionHost
    {
        /// <summary>Có đang chạy trên Windows thật không (bật font Inter nạp riêng, bảng màu tab / combo tự vẽ).</summary>
        internal static bool IsWindows { get { return Path.DirectorySeparatorChar == '\\'; } }
        /// <summary>Đường dẫn file đi kèm exe (font Inter ở thư mục Fonts cạnh MeshFrame.exe).</summary>
        internal static string SourceFile(string name) { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name); }
    }

    /// <summary>Luôn giao diện tối, không video nền.</summary>
    internal static class UiPreferences
    {
        internal static bool Dark { get { return true; } }
        internal static bool Motion { get { return false; } }
    }

    internal static class UiBackdrop
    {
        internal static bool Inside(Control c) { return false; }
        internal static void Paint(Control c, Graphics g, Rectangle area) { using (var b = new SolidBrush(UiTheme.Bg)) g.FillRectangle(b, area); }
    }

    internal sealed class FactoryWelcome : Control { }
    internal sealed class FactoryNavPanel : Control { }
    internal sealed class StatusChip : Control { }
}
