using System.Drawing;
using System.Windows.Forms;

namespace TeklaQuickAssign;

internal static class UiBackdrop
{
	internal static bool Inside(Control c)
	{
		return false;
	}

	internal static void Paint(Control c, Graphics g, Rectangle area)
	{
		using SolidBrush brush = new SolidBrush(UiTheme.Bg);
		g.FillRectangle(brush, area);
	}
}
