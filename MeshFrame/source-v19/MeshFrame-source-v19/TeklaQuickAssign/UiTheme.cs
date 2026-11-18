using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TeklaQuickAssign;

internal static class UiTheme
{
	internal struct OverlayShape
	{
		internal Rectangle Bounds;

		internal int Radius;

		internal Color Fill;
	}

	private sealed class ButtonState
	{
		internal bool Hover;

		internal bool Down;

		internal Color Original;
	}

	private sealed class ComboSkin : NativeWindow
	{
		private readonly ComboBox box;

		private static readonly ConditionalWeakTable<ComboBox, ComboSkin> all = new ConditionalWeakTable<ComboBox, ComboSkin>();

		private ComboSkin(ComboBox box)
		{
			this.box = box;
		}

		internal static void Attach(ComboBox box)
		{
			if (!all.TryGetValue(box, out var skin))
			{
				skin = new ComboSkin(box);
				all.Add(box, skin);
				if (box.IsHandleCreated)
				{
					skin.AssignHandle(box.Handle);
				}
				box.HandleCreated += delegate
				{
					skin.ReleaseHandle();
					skin.AssignHandle(box.Handle);
				};
				box.HandleDestroyed += delegate
				{
					skin.ReleaseHandle();
				};
			}
		}

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == 312 && Enabled && !box.Enabled && box.DropDownStyle != ComboBoxStyle.DropDownList)
			{
				SetTextColor(m.WParam, ColorTranslator.ToWin32(Muted));
				SetBkColor(m.WParam, ColorTranslator.ToWin32(DisabledField));
				m.Result = DisabledBrush();
				return;
			}
			base.WndProc(ref m);
			if (m.Msg != 15 || !Enabled || box.DropDownStyle != ComboBoxStyle.DropDownList)
			{
				return;
			}
			try
			{
				using Graphics g = Graphics.FromHwnd(box.Handle);
				Draw(g);
			}
			catch (Exception)
			{
			}
		}

		private void Draw(Graphics g)
		{
			Rectangle r = new Rectangle(0, 0, box.Width - 1, box.Height - 1);
			using (SolidBrush brush = new SolidBrush(Under(box) ?? Bg))
			{
				g.FillRectangle(brush, box.ClientRectangle);
			}
			g.SmoothingMode = SmoothingMode.AntiAlias;
			using (GraphicsPath path = Round(r, 6))
			{
				using (SolidBrush brush2 = new SolidBrush(box.Enabled ? Field : Color.FromArgb(38, 40, 44)))
				{
					g.FillPath(brush2, path);
				}
				using Pen pen = new Pen(box.Focused ? Color.FromArgb(47, 95, 149) : Border);
				g.DrawPath(pen, path);
			}
			int num = box.Width - 14;
			int num2 = box.Height / 2 - 1;
			using (Pen pen2 = new Pen(Color.FromArgb(154, 164, 178), 1.6f))
			{
				g.DrawLines(pen2, new Point[3]
				{
					new Point(num - 4, num2 - 2),
					new Point(num, num2 + 2),
					new Point(num + 4, num2 - 2)
				});
			}
			g.SmoothingMode = SmoothingMode.None;
			TextRenderer.DrawText(g, box.Text, box.Font, new Rectangle(8, 0, box.Width - 30, box.Height), box.Enabled ? Text : Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		}
	}

	private sealed class TabSkin : NativeWindow
	{
		private readonly TabControl tabs;

		private static readonly ConditionalWeakTable<TabControl, TabSkin> all = new ConditionalWeakTable<TabControl, TabSkin>();

		private TabSkin(TabControl tabs)
		{
			this.tabs = tabs;
		}

		internal static void Attach(TabControl tabs)
		{
			if (!all.TryGetValue(tabs, out var skin))
			{
				skin = new TabSkin(tabs);
				all.Add(tabs, skin);
				if (tabs.IsHandleCreated)
				{
					skin.AssignHandle(tabs.Handle);
				}
				tabs.HandleCreated += delegate
				{
					skin.ReleaseHandle();
					skin.AssignHandle(tabs.Handle);
				};
				tabs.HandleDestroyed += delegate
				{
					skin.ReleaseHandle();
				};
			}
		}

		protected override void WndProc(ref Message m)
		{
			base.WndProc(ref m);
			if (m.Msg != 15 || !Enabled || tabs.TabCount == 0)
			{
				return;
			}
			try
			{
				using Graphics graphics = Graphics.FromHwnd(tabs.Handle);
				Rectangle displayRectangle = tabs.DisplayRectangle;
				int num = Math.Max(0, displayRectangle.Top - 3);
				using (SolidBrush brush = new SolidBrush(TabStrip))
				{
					graphics.FillRectangle(brush, 0, 0, tabs.Width, num);
				}
				using (SolidBrush brush2 = new SolidBrush(InHost(tabs) ? Key : Bg))
				{
					graphics.FillRectangle(brush2, 0, num, tabs.Width, displayRectangle.Top - num);
					graphics.FillRectangle(brush2, 0, displayRectangle.Top, displayRectangle.Left, tabs.Height - displayRectangle.Top);
					graphics.FillRectangle(brush2, displayRectangle.Right, displayRectangle.Top, tabs.Width - displayRectangle.Right, tabs.Height - displayRectangle.Top);
					graphics.FillRectangle(brush2, 0, displayRectangle.Bottom, tabs.Width, tabs.Height - displayRectangle.Bottom);
				}
				using (Pen pen = new Pen(CardBorder))
				{
					graphics.DrawLine(pen, 0, num - 1, tabs.Width, num - 1);
				}
				for (int i = 0; i < tabs.TabCount; i++)
				{
					Rectangle tabRect = tabs.GetTabRect(i);
					DrawTab(tabs, graphics, i, new Rectangle(tabRect.Left, 0, tabRect.Width, num - 1));
				}
			}
			catch (Exception)
			{
			}
		}
	}

	private sealed class DarkColors : ProfessionalColorTable
	{
		public override Color ToolStripGradientBegin => Surface;

		public override Color ToolStripGradientMiddle => Surface;

		public override Color ToolStripGradientEnd => Surface;

		public override Color ToolStripBorder => CardBorder;

		public override Color ToolStripDropDownBackground => Surface;

		public override Color StatusStripGradientBegin => Toolbar;

		public override Color StatusStripGradientEnd => Toolbar;

		public override Color MenuStripGradientBegin => Surface;

		public override Color MenuStripGradientEnd => Surface;

		public override Color MenuBorder => Border;

		public override Color MenuItemBorder => Accent;

		public override Color MenuItemSelected => Selection;

		public override Color MenuItemSelectedGradientBegin => Selection;

		public override Color MenuItemSelectedGradientEnd => Selection;

		public override Color MenuItemPressedGradientBegin => Header;

		public override Color MenuItemPressedGradientEnd => Header;

		public override Color ImageMarginGradientBegin => Surface;

		public override Color ImageMarginGradientMiddle => Surface;

		public override Color ImageMarginGradientEnd => Surface;

		public override Color SeparatorDark => Border;

		public override Color SeparatorLight => Border;

		public override Color ButtonSelectedHighlight => Selection;

		public override Color ButtonSelectedGradientBegin => Selection;

		public override Color ButtonSelectedGradientEnd => Selection;

		public override Color ButtonSelectedBorder => Accent;

		internal DarkColors()
		{
			UseSystemColors = false;
		}
	}

	internal static readonly Color Bg = Color.FromArgb(27, 28, 32);

	internal static readonly Color Surface = Color.FromArgb(35, 37, 42);

	internal static readonly Color Field = Color.FromArgb(44, 47, 53);

	internal static readonly Color FieldInput = Color.FromArgb(27, 29, 33);

	internal static readonly Color Border = Color.FromArgb(61, 65, 72);

	internal static readonly Color CardBorder = Color.FromArgb(51, 54, 61);

	internal static readonly Color Text = Color.FromArgb(227, 230, 234);

	internal static readonly Color Muted = Color.FromArgb(141, 149, 161);

	internal static readonly Color HeaderText = Color.FromArgb(170, 178, 189);

	internal static readonly Color Accent = Color.FromArgb(59, 142, 234);

	internal static readonly Color AccentHover = Color.FromArgb(78, 157, 242);

	internal static readonly Color AccentBar = Color.FromArgb(74, 163, 255);

	internal static readonly Color Nav = Color.FromArgb(20, 21, 25);

	internal static readonly Color NavDark = Color.FromArgb(38, 42, 49);

	internal static readonly Color NavText = Color.FromArgb(180, 186, 195);

	internal static readonly Color NavMuted = Color.FromArgb(94, 101, 112);

	internal static readonly Color Header = Color.FromArgb(42, 45, 51);

	internal static readonly Color AltRow = Color.FromArgb(38, 40, 45);

	internal static readonly Color RowLine = Color.FromArgb(44, 47, 53);

	internal static readonly Color Selection = Color.FromArgb(30, 58, 92);

	internal static readonly Color SelectionText = Color.FromArgb(230, 241, 255);

	internal static readonly Color Danger = Color.FromArgb(255, 122, 112);

	internal static readonly Color Toolbar = Color.FromArgb(26, 28, 32);

	internal static readonly Color TabStrip = Color.FromArgb(22, 23, 27);

	internal static readonly Color OnFill = Color.FromArgb(32, 54, 83);

	internal static readonly Color OnBorder = Color.FromArgb(47, 95, 149);

	internal static readonly Color OkBack = Color.FromArgb(23, 58, 39);

	internal static readonly Color OkText = Color.FromArgb(111, 220, 151);

	internal static readonly Color InfoBack = Color.FromArgb(30, 58, 92);

	internal static readonly Color InfoText = Color.FromArgb(156, 203, 255);

	internal static readonly Color WarnBack = Color.FromArgb(61, 50, 24);

	internal static readonly Color WarnText = Color.FromArgb(242, 194, 91);

	internal static readonly Color ErrBack = Color.FromArgb(63, 31, 29);

	internal static readonly Color ErrText = Color.FromArgb(255, 138, 128);

	internal static readonly Color Key = Color.FromArgb(11, 12, 14);

	internal static bool KeyMode;

	internal static DateTime ZoomBusyUntil = DateTime.MinValue;

	internal const int BackdropVeil = 60;

	internal static readonly Color CardFill = Color.FromArgb(158, 30, 32, 37);

	internal static readonly Color GlassFill = Color.FromArgb(185, 14, 16, 20);

	internal static readonly Color HitCard = Color.FromArgb(34, 36, 41);

	internal static readonly Color HitGlass = Color.FromArgb(21, 23, 28);

	internal static readonly Color HitGlassAlt = Color.FromArgb(25, 27, 32);

	internal const float PaperDark = 0.1f;

	internal const float PaperLight = 0.92f;

	private static readonly float[,] HueHalfTurn = new float[3, 3]
	{
		{ -0.574f, 1.43f, 0.144f },
		{ 0.426f, 0.43f, 0.144f },
		{ 0.426f, 1.43f, -0.856f }
	};

	private static readonly List<WeakReference> cardOwners = new List<WeakReference>();

	private static readonly ConditionalWeakTable<Control, object> glass = new ConditionalWeakTable<Control, object>();

	private static readonly Color ClassicNavActive = Color.FromArgb(0, 112, 192);

	private static readonly Color ClassicNav = Color.FromArgb(31, 54, 77);

	private static bool? active;

	internal const int NavWidth = 212;

	[ThreadStatic]
	private static bool busy;

	private static readonly ConditionalWeakTable<Control, object> hooked = new ConditionalWeakTable<Control, object>();

	private static readonly ConditionalWeakTable<Control, object> ours = new ConditionalWeakTable<Control, object>();

	private static readonly ConditionalWeakTable<Control, object> zoneSet = new ConditionalWeakTable<Control, object>();

	private static readonly ConditionalWeakTable<Control, object> wholeCards = new ConditionalWeakTable<Control, object>();

	private static readonly ConditionalWeakTable<Control, object> headers = new ConditionalWeakTable<Control, object>();

	private static readonly ConditionalWeakTable<TableLayoutPanel, List<int[]>> bands = new ConditionalWeakTable<TableLayoutPanel, List<int[]>>();

	private static readonly List<WeakReference> zones = new List<WeakReference>();

	private static readonly List<WeakReference> leaves = new List<WeakReference>();

	private static readonly List<WeakReference> grids = new List<WeakReference>();

	private static bool installed;

	private static readonly ConditionalWeakTable<Form, object> forms = new ConditionalWeakTable<Form, object>();

	private static readonly MethodInfo setStyle = typeof(Control).GetMethod("SetStyle", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly string[] primaryTexts = new string[10] { "KẾT NỐI MODEL", "APPLY PROPERTIES", "XUẤT FILE", "CẬP NHẬT KHỔ TỪ SELECTION", "IMPORT TÊN MỚI", "CHẠY", "GHI MÃ VÀO PART", "GHI NAME", "ÁP DỤNG", "LƯU CẤU HÌNH" };

	private static readonly ConditionalWeakTable<ButtonBase, ButtonState> buttonStates = new ConditionalWeakTable<ButtonBase, ButtonState>();

	internal static readonly Color DisabledField = Color.FromArgb(38, 40, 44);

	private static IntPtr disabledBrush;

	internal static bool ZoomBusy => DateTime.UtcNow < ZoomBusyUntil;

	internal static bool Enabled
	{
		get
		{
			if (!active.HasValue)
			{
				active = UiPreferences.Dark;
			}
			return active.Value;
		}
	}

	internal static Color NavActive
	{
		get
		{
			if (!Enabled)
			{
				return ClassicNavActive;
			}
			return NavDark;
		}
	}

	internal static Color NavBack
	{
		get
		{
			if (!Enabled)
			{
				return ClassicNav;
			}
			return Nav;
		}
	}

	internal static ColorMatrix PaperMatrix()
	{
		float num = 0.82f;
		float[][] array = new float[5][];
		for (int i = 0; i < 5; i++)
		{
			array[i] = new float[5];
		}
		for (int j = 0; j < 3; j++)
		{
			for (int k = 0; k < 3; k++)
			{
				array[j][k] = (0f - num) * HueHalfTurn[k, j];
			}
		}
		array[3][3] = 1f;
		array[4][0] = (array[4][1] = (array[4][2] = 0.92f));
		array[4][4] = 1f;
		return new ColorMatrix(array);
	}

	internal static Color PaperColor(Color c)
	{
		float num = 0.82f;
		float[] array = new float[3]
		{
			(float)(int)c.R / 255f,
			(float)(int)c.G / 255f,
			(float)(int)c.B / 255f
		};
		int[] array2 = new int[3];
		for (int i = 0; i < 3; i++)
		{
			float num2 = 0.92f;
			for (int j = 0; j < 3; j++)
			{
				num2 -= num * HueHalfTurn[i, j] * array[j];
			}
			array2[i] = (int)Math.Round(Math.Max(0f, Math.Min(1f, num2)) * 255f);
		}
		return Color.FromArgb(c.A, array2[0], array2[1], array2[2]);
	}

	internal static void PaintPaper(Control control, PaintEventArgs e, Action<PaintEventArgs> body)
	{
		Size clientSize = control.ClientSize;
		if (!Enabled || clientSize.Width <= 0 || clientSize.Height <= 0)
		{
			body(e);
			return;
		}
		using Bitmap image = new Bitmap(clientSize.Width, clientSize.Height);
		using (Graphics graphics = Graphics.FromImage(image))
		{
			graphics.Clear((control.BackColor.A == byte.MaxValue) ? control.BackColor : Color.White);
			body(new PaintEventArgs(graphics, new Rectangle(Point.Empty, clientSize)));
		}
		using ImageAttributes imageAttributes = new ImageAttributes();
		imageAttributes.SetColorMatrix(PaperMatrix());
		e.Graphics.DrawImage(image, new Rectangle(Point.Empty, clientSize), 0, 0, clientSize.Width, clientSize.Height, GraphicsUnit.Pixel, imageAttributes);
	}

	internal static ToolTip Styled(ToolTip tip)
	{
		StyleToolTip(tip);
		return tip;
	}

	internal static void StyleToolTip(ToolTip tip)
	{
		if (!Enabled || tip == null || tip.OwnerDraw)
		{
			return;
		}
		tip.OwnerDraw = true;
		tip.Draw += (object sender, DrawToolTipEventArgs e) =>
		{
			using (SolidBrush brush = new SolidBrush(Surface))
			{
				e.Graphics.FillRectangle(brush, e.Bounds);
			}
			using (Pen pen = new Pen(Border))
			{
				e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1));
			}
			TextRenderer.DrawText(e.Graphics, e.ToolTipText, e.Font, e.Bounds, Text, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.LeftAndRightPadding);
		};
	}

	internal static void Freeze(Control control, Action change)
	{
		bool flag = TeklaConnectionHost.IsWindows && control != null && control.IsHandleCreated;
		if (flag)
		{
			SendMessage(control.Handle, 11, IntPtr.Zero, IntPtr.Zero);
		}
		try
		{
			change();
		}
		finally
		{
			if (flag)
			{
				SendMessage(control.Handle, 11, new IntPtr(1), IntPtr.Zero);
				control.Invalidate(invalidateChildren: true);
				control.Update();
			}
		}
	}

	internal static void Prepare(Form form)
	{
		if (Enabled && form != null && !forms.TryGetValue(form, out var _))
		{
			forms.Add(form, new object());
			MapFonts(form);
			Apply(form);
			form.Deactivate += delegate
			{
				PrepareOpenForms();
			};
		}
	}

	private static void PrepareOpenForms()
	{
		try
		{
			foreach (Form openForm in Application.OpenForms)
			{
				Prepare(openForm);
			}
		}
		catch (Exception)
		{
		}
	}

	internal static Color? HitFill(Control control)
	{
		if (!InHost(control))
		{
			return null;
		}
		return InCard(control) ? HitCard : HitGlass;
	}

	private static void Own(Control c)
	{
		cardOwners.Add(new WeakReference(c));
	}

	private static bool InHost(Control c)
	{
		if (KeyMode)
		{
			return UiBackdrop.Inside(c);
		}
		return false;
	}

	internal static void Glass(Control control)
	{
		if (Enabled && control != null && !glass.TryGetValue(control, out var _))
		{
			glass.Add(control, new object());
			Own(control);
		}
	}

	internal static bool IsGlass(Control control)
	{
		object value;
		if (control != null)
		{
			return glass.TryGetValue(control, out value);
		}
		return false;
	}

	internal static List<OverlayShape> Shapes(Control host)
	{
		List<OverlayShape> list = new List<OverlayShape>();
		if (host == null || !host.IsHandleCreated)
		{
			return list;
		}
		for (int num = cardOwners.Count - 1; num >= 0; num--)
		{
			if (cardOwners[num].Target is Control { IsDisposed: false } control)
			{
				if (!control.IsHandleCreated || !control.Visible || !host.Contains(control))
				{
					continue;
				}
				Point point;
				try
				{
					point = host.PointToClient(control.PointToScreen(Point.Empty));
				}
				catch (Exception)
				{
					continue;
				}
				if (control is TableLayoutPanel tableLayoutPanel && bands.TryGetValue(tableLayoutPanel, out var _))
				{
					foreach (KeyValuePair<Rectangle, int> item in BandRects(tableLayoutPanel))
					{
						Rectangle key = item.Key;
						key.Offset(point);
						list.Add(new OverlayShape
						{
							Bounds = key,
							Radius = 8,
							Fill = CardFill
						});
					}
				}
				else if (control is GroupBox)
				{
					Rectangle bounds = new Rectangle(point.X, point.Y, control.Width - 1, control.Height - 4);
					list.Add(new OverlayShape
					{
						Bounds = bounds,
						Radius = 8,
						Fill = CardFill
					});
				}
				else if (IsWholeCard(control))
				{
					Rectangle bounds2 = Inset(control.ClientRectangle, 4);
					bounds2.Offset(point);
					list.Add(new OverlayShape
					{
						Bounds = bounds2,
						Radius = 8,
						Fill = CardFill
					});
				}
				else if (IsGlass(control))
				{
					list.Add(new OverlayShape
					{
						Bounds = new Rectangle(point, control.ClientSize),
						Radius = 6,
						Fill = GlassFill
					});
				}
			}
			else
			{
				cardOwners.RemoveAt(num);
			}
		}
		list.Reverse();
		return list;
	}

	internal static void ResetForTests()
	{
		active = null;
	}

	private static void Ours(Control c)
	{
		if (!ours.TryGetValue(c, out var _))
		{
			ours.Add(c, new object());
		}
	}

	internal static void Install()
	{
		if (installed || !Enabled)
		{
			return;
		}
		installed = true;
		Application.Idle += delegate
		{
			try
			{
				foreach (Form openForm in Application.OpenForms)
				{
					Prepare(openForm);
				}
			}
			catch (InvalidOperationException)
			{
			}
		};
	}

	internal static void Apply(Control root)
	{
		if (root is Form key && !forms.TryGetValue(key, out var _))
		{
			forms.Add(key, new object());
		}
		if (Enabled && root != null)
		{
			Walk(root);
		}
	}

	internal static void Bands(TableLayoutPanel layout, params int[] spec)
	{
		if (!Enabled || layout == null)
		{
			return;
		}
		List<int[]> list = new List<int[]>();
		for (int i = 0; i + 2 < spec.Length; i += 3)
		{
			list.Add(new int[3]
			{
				spec[i],
				spec[i + 1],
				spec[i + 2]
			});
		}
		bands.Remove(layout);
		bands.Add(layout, list);
		Own(layout);
		foreach (Control control in layout.Controls)
		{
			if (BandOf(layout, control) != null)
			{
				layout.GetPositionFromControl(control);
				Padding margin = control.Margin;
				control.Margin = new Padding(Math.Max(margin.Left, 14), margin.Top, Math.Max(margin.Right, 14), margin.Bottom);
			}
		}
	}

	internal static void WholeCard(Control control)
	{
		if (Enabled && control != null && !wholeCards.TryGetValue(control, out var _))
		{
			wholeCards.Add(control, new object());
			Own(control);
			Padding padding = control.Padding;
			control.Padding = new Padding(padding.Left + 14, padding.Top + 12, padding.Right + 14, padding.Bottom + 12);
		}
	}

	internal static void MarkHeader(Control label)
	{
		if (Enabled && label != null)
		{
			if (!headers.TryGetValue(label, out var _))
			{
				headers.Add(label, new object());
			}
			if (label is Label)
			{
				label.Padding = new Padding(label.Padding.Left, Math.Max(label.Padding.Top, 6), label.Padding.Right, label.Padding.Bottom);
			}
			label.Font = (Font)UiFonts.Semi(10f).Clone();
		}
	}

	private static int[] BandOf(TableLayoutPanel layout, Control child)
	{
		if (!bands.TryGetValue(layout, out var value))
		{
			return null;
		}
		TableLayoutPanelCellPosition positionFromControl = layout.GetPositionFromControl(child);
		if (positionFromControl.Row < 0)
		{
			return null;
		}
		foreach (int[] item in value)
		{
			if (positionFromControl.Row >= item[0] && positionFromControl.Row <= item[1])
			{
				return item;
			}
		}
		return null;
	}

	private static bool IsWholeCard(Control c)
	{
		object value;
		if (c != null)
		{
			return wholeCards.TryGetValue(c, out value);
		}
		return false;
	}

	internal static bool IsZone(Control c)
	{
		object value;
		if (c != null)
		{
			return zoneSet.TryGetValue(c, out value);
		}
		return false;
	}

	internal static bool InCard(Control control)
	{
		Control control2 = control;
		while (control2 != null && control2.Parent != null)
		{
			Control parent = control2.Parent;
			if (IsWholeCard(parent) || parent is GroupBox || parent is DataGridView)
			{
				return true;
			}
			if (parent is TableLayoutPanel layout && BandOf(layout, control2) != null)
			{
				return true;
			}
			control2 = control2.Parent;
		}
		return false;
	}

	internal static Color? Under(Control control)
	{
		if (InHost(control))
		{
			return Key;
		}
		Control parent = control.Parent;
		if (parent == null)
		{
			return Bg;
		}
		if (parent is TableLayoutPanel layout && BandOf(layout, control) != null)
		{
			return Surface;
		}
		if (IsWholeCard(parent))
		{
			return Surface;
		}
		if (IsZone(parent))
		{
			return null;
		}
		if (parent.BackColor.A < byte.MaxValue)
		{
			return Under(parent);
		}
		return parent.BackColor;
	}

	internal static void PaintUnder(Control control, Graphics g, Rectangle area)
	{
		Color? color = Under(control);
		if (color.HasValue)
		{
			using (SolidBrush brush = new SolidBrush(color.Value))
			{
				g.FillRectangle(brush, area);
				return;
			}
		}
		UiBackdrop.Paint(control, g, area);
	}

	private static void Walk(Control control)
	{
		if (!Painted(control))
		{
			Style(control);
		}
		else
		{
			DoubleBuffer(control);
		}
		foreach (Control control2 in control.Controls)
		{
			Walk(control2);
		}
		if (control.ContextMenuStrip != null)
		{
			StyleStrip(control.ContextMenuStrip);
		}
		if (hooked.TryGetValue(control, out var _))
		{
			return;
		}
		hooked.Add(control, new object());
		control.ControlAdded += (object sender, ControlEventArgs e) =>
		{
			if (Enabled)
			{
				MapFonts(e.Control);
				Walk(e.Control);
			}
		};
	}

	internal static Color MapBack(Color color, Color fallback)
	{
		if (color.A < byte.MaxValue)
		{
			return color;
		}
		if (color.GetBrightness() <= 0.6f)
		{
			return color;
		}
		if (Chroma(color) < 0.08f)
		{
			return fallback;
		}
		return FromHsl(color.GetHue(), Math.Min(color.GetSaturation(), 0.35f), 0.18f);
	}

	internal static Color MapFore(Color color)
	{
		if (color.A < byte.MaxValue)
		{
			return color;
		}
		float brightness = color.GetBrightness();
		if (brightness >= 0.55f)
		{
			return color;
		}
		if (color.GetSaturation() < 0.2f)
		{
			if (!(brightness < 0.3f))
			{
				return Muted;
			}
			return Text;
		}
		return FromHsl(color.GetHue(), Math.Min(color.GetSaturation(), 0.85f), 0.72f);
	}

	internal static float Chroma(Color color)
	{
		return (float)(Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B))) / 255f;
	}

	internal static bool IsLight(Color color)
	{
		if (color.A == byte.MaxValue)
		{
			return color.GetBrightness() > 0.6f;
		}
		return false;
	}

	internal static Color FromHsl(float hue, float saturation, float lightness)
	{
		float num = (1f - Math.Abs(2f * lightness - 1f)) * saturation;
		float num2 = hue / 60f;
		float num3 = num * (1f - Math.Abs(num2 % 2f - 1f));
		float m = lightness - num / 2f;
		float arg = 0f;
		float arg2 = 0f;
		float arg3 = 0f;
		if (num2 < 1f)
		{
			arg = num;
			arg2 = num3;
		}
		else if (num2 < 2f)
		{
			arg = num3;
			arg2 = num;
		}
		else if (num2 < 3f)
		{
			arg2 = num;
			arg3 = num3;
		}
		else if (num2 < 4f)
		{
			arg2 = num3;
			arg3 = num;
		}
		else if (num2 < 5f)
		{
			arg = num3;
			arg3 = num;
		}
		else
		{
			arg = num;
			arg3 = num3;
		}
		Func<float, int> func = (float v) => Math.Max(0, Math.Min(255, (int)Math.Round((v + m) * 255f)));
		return Color.FromArgb(func(arg), func(arg2), func(arg3));
	}

	internal static bool Painted(Control control)
	{
		if (control is PictureBox || control.BackgroundImage != null)
		{
			return true;
		}
		if (control is FactoryWelcome || control is FactoryNavPanel || control is StatusChip)
		{
			return true;
		}
		Type type = control.GetType();
		Assembly assembly = typeof(Control).Assembly;
		if (type.Assembly != assembly)
		{
			string[] array = new string[2] { "OnPaint", "OnPaintBackground" };
			foreach (string name in array)
			{
				MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(PaintEventArgs) }, null);
				if (method != null && method.DeclaringType != null && method.DeclaringType.Assembly != assembly)
				{
					return true;
				}
			}
		}
		if (ours.TryGetValue(control, out var _))
		{
			return false;
		}
		return HasPaintHandler(control);
	}

	private static bool HasPaintHandler(Control control)
	{
		try
		{
			object obj = null;
			string[] array = new string[2] { "EventPaint", "PaintEvent" };
			foreach (string name in array)
			{
				FieldInfo field = typeof(Control).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
				if (field != null)
				{
					obj = field.GetValue(null);
					break;
				}
			}
			if (obj == null)
			{
				return false;
			}
			PropertyInfo property = typeof(Component).GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic);
			EventHandlerList eventHandlerList = ((property == null) ? null : (property.GetValue(control, null) as EventHandlerList));
			return eventHandlerList != null && (object)eventHandlerList[obj] != null;
		}
		catch (Exception)
		{
			return false;
		}
	}

	internal static void MapFonts(Control control)
	{
		if (!Enabled || control == null || control is FactoryWelcome)
		{
			return;
		}
		try
		{
			Font font = control.Font;
			bool flag = control.Parent != null && object.Equals(font, control.Parent.Font);
			if (!headers.TryGetValue(control, out var value))
			{
				if (control is Form)
				{
					control.Font = (Font)UiFonts.Body(9f).Clone();
				}
				else if ((control is Label || control is ButtonBase) && !control.AutoSize && (!(control is Button) || !headers.TryGetValue(control, out value)))
				{
					control.Font = new Font((font.Name == "Inter" || font.Name == "Inter SemiBold") ? "Segoe UI" : font.Name, font.Size, font.Style);
				}
				else if (control is ButtonBase || control is TabControl)
				{
					control.Font = (Font)UiFonts.Semi(Math.Max(8.25f, Math.Min(font.Size, 9.5f) * 0.97f)).Clone();
				}
				else if (!flag && UiFonts.Replaceable(font))
				{
					Control control2 = control;
					Font font2;
					if (font.Size >= 13f && font.Bold)
					{
						font2 = UiFonts.Semi(10.5f);
					}
					else
					{
						font2 = (font.Bold ? UiFonts.Semi(font.Size * 0.97f) : UiFonts.Body(font.Size * 0.97f, font.Style));
					}
					control2.Font = (Font)font2.Clone();
				}
			}
		}
		catch (Exception)
		{
		}
		if (control is DataGridView)
		{
			return;
		}
		foreach (Control control3 in control.Controls)
		{
			MapFonts(control3);
		}
	}

	private static void Style(Control control)
	{
		busy = true;
		try
		{
			StyleCore(control);
		}
		catch (Exception)
		{
		}
		finally
		{
			busy = false;
		}
		Watch(control);
	}

	private static bool IsContainer(Control c)
	{
		if (!(c is Panel) && !(c is UserControl) && !(c is TableLayoutPanel) && !(c is FlowLayoutPanel) && !(c is SplitContainer) && !(c is SplitterPanel) && !(c is TabPage))
		{
			return c is ContainerControl;
		}
		return true;
	}

	private static void StyleCore(Control control)
	{
		if (control is Form)
		{
			control.BackColor = Bg;
			control.ForeColor = Text;
			DarkTitle((Form)control);
		}
		else if (control is DataGridView)
		{
			StyleGrid((DataGridView)control);
		}
		else if (control is Button)
		{
			StyleButton((Button)control);
		}
		else if (control is CheckBox || control is RadioButton)
		{
			StyleCheck((ButtonBase)control);
		}
		else if (control is ComboBox)
		{
			StyleCombo((ComboBox)control);
		}
		else if (control is TextBoxBase)
		{
			control.BackColor = MapBack(control.BackColor, FieldInput);
			control.ForeColor = MapFore(control.ForeColor);
			if (control is TextBox { BorderStyle: BorderStyle.Fixed3D } textBox)
			{
				textBox.BorderStyle = BorderStyle.FixedSingle;
			}
			if (control is RichTextBox richTextBox)
			{
				richTextBox.BorderStyle = BorderStyle.FixedSingle;
			}
			DarkScroll(control);
		}
		else if (control is UpDownBase)
		{
			control.BackColor = MapBack(control.BackColor, FieldInput);
			control.ForeColor = MapFore(control.ForeColor);
			((UpDownBase)control).BorderStyle = BorderStyle.FixedSingle;
		}
		else if (control is ListBox || control is ListView || control is TreeView)
		{
			control.BackColor = MapBack(control.BackColor, FieldInput);
			control.ForeColor = MapFore(control.ForeColor);
			if (control is ListBox { BorderStyle: BorderStyle.Fixed3D } listBox)
			{
				listBox.BorderStyle = BorderStyle.FixedSingle;
			}
			DarkScroll(control);
		}
		else if (control is TabControl)
		{
			StyleTabs((TabControl)control);
		}
		else if (control is ToolStrip)
		{
			StyleStrip((ToolStrip)control);
		}
		else if (control is GroupBox)
		{
			StyleGroup((GroupBox)control);
		}
		else if (control is LinkLabel)
		{
			LinkLabel linkLabel = (LinkLabel)control;
			linkLabel.LinkColor = AccentBar;
			linkLabel.ActiveLinkColor = Text;
			linkLabel.VisitedLinkColor = AccentBar;
			linkLabel.BackColor = MapBack(linkLabel.BackColor, Color.Transparent);
			linkLabel.ForeColor = MapFore(linkLabel.ForeColor);
			Leaf(linkLabel);
		}
		else if (control is Label)
		{
			Color? color = Under(control);
			if (InHost(control) && !IsLight(control.BackColor))
			{
				control.BackColor = Key;
			}
			else
			{
				control.BackColor = MapBack(control.BackColor, (color.HasValue && !IsZone(control.Parent)) ? color.Value : Color.Transparent);
			}
			control.ForeColor = (IsLight(control.BackColor) ? Color.FromArgb(20, 20, 20) : MapFore(control.ForeColor));
			if (headers.TryGetValue(control, out var _))
			{
				control.ForeColor = Text;
			}
			Leaf(control);
		}
		else if (control is ProgressBar)
		{
			StyleProgress((ProgressBar)control);
		}
		else if (IsContainer(control))
		{
			if (control is TabPage)
			{
				((TabPage)control).UseVisualStyleBackColor = false;
			}
			if (InHost(control))
			{
				control.BackColor = Key;
				if (!InCard(control))
				{
					Zone(control);
				}
			}
			else if (InCard(control))
			{
				control.BackColor = Surface;
			}
			else
			{
				control.BackColor = Bg;
				Zone(control);
			}
			control.ForeColor = Text;
			DoubleBuffer(control);
			if (control is ScrollableControl { AutoScroll: not false })
			{
				DarkScroll(control);
			}
		}
		else
		{
			control.BackColor = MapBack(control.BackColor, Bg);
			control.ForeColor = (IsLight(control.BackColor) ? Color.FromArgb(20, 20, 20) : MapFore(control.ForeColor));
		}
	}

	private static void Leaf(Control control)
	{
		if (control.BackColor.A != byte.MaxValue && !Under(control).HasValue)
		{
			leaves.Add(new WeakReference(control));
			DoubleBuffer(control);
		}
	}

	private static void Zone(Control control)
	{
		if (zoneSet.TryGetValue(control, out var _))
		{
			return;
		}
		zoneSet.Add(control, new object());
		zones.Add(new WeakReference(control));
		Ours(control);
		control.Paint += (object sender, PaintEventArgs e) =>
		{
			if (Enabled)
			{
				bool flag = InHost(control);
				if (!flag)
				{
					UiBackdrop.Paint(control, e.Graphics, e.ClipRectangle);
				}
				if (control is TableLayoutPanel layout)
				{
					foreach (KeyValuePair<Rectangle, int> item in BandRects(layout))
					{
						DrawCard(e.Graphics, item.Key, null, item.Value, flag);
					}
				}
				if (IsWholeCard(control))
				{
					DrawCard(e.Graphics, Inset(control.ClientRectangle, 4), null, 0, flag);
				}
			}
		};
	}

	internal static void BackdropSurface(Control control)
	{
		if (Enabled && !zoneSet.TryGetValue(control, out var _))
		{
			zoneSet.Add(control, new object());
			zones.Add(new WeakReference(control));
		}
	}

	private static Rectangle Inset(Rectangle r, int d)
	{
		r.Inflate(-d, -d);
		return r;
	}

	internal static List<KeyValuePair<Rectangle, int>> BandRects(TableLayoutPanel layout)
	{
		List<KeyValuePair<Rectangle, int>> list = new List<KeyValuePair<Rectangle, int>>();
		if (!bands.TryGetValue(layout, out var value))
		{
			return list;
		}
		int[] rowHeights = layout.GetRowHeights();
		int num = layout.Padding.Top;
		int[] array = new int[rowHeights.Length + 1];
		for (int i = 0; i < rowHeights.Length; i++)
		{
			array[i] = num;
			num += rowHeights[i];
		}
		array[rowHeights.Length] = num;
		foreach (int[] item in value)
		{
			if (item[0] < rowHeights.Length)
			{
				int num2 = array[item[0]];
				int num3 = array[Math.Min(item[1] + 1, rowHeights.Length)];
				if (num3 - num2 >= 8)
				{
					int num4 = ((item[0] == 0) ? (num2 - Math.Min(8, layout.Padding.Top)) : (num2 + 3));
					int num5 = ((item[1] >= rowHeights.Length - 1) ? (num3 + Math.Min(8, layout.Padding.Bottom)) : (num3 - 3));
					Rectangle key = new Rectangle(layout.Padding.Left - Math.Min(6, layout.Padding.Left), num4, layout.ClientSize.Width - layout.Padding.Horizontal + 2 * Math.Min(6, layout.Padding.Left), num5 - num4);
					list.Add(new KeyValuePair<Rectangle, int>(key, (item[2] != 0) ? array[item[0] + 1] : 0));
				}
			}
		}
		return list;
	}

	internal static void DrawCard(Graphics g, Rectangle r, string title, int headerLine, bool outline = false)
	{
		if (r.Width <= 4 || r.Height <= 4)
		{
			return;
		}
		SmoothingMode smoothingMode = g.SmoothingMode;
		g.SmoothingMode = (outline ? SmoothingMode.None : SmoothingMode.AntiAlias);
		if (outline)
		{
			using GraphicsPath path = Round(r, 8);
			using Pen pen = new Pen(CardBorder);
			g.DrawPath(pen, path);
		}
		else
		{
			using GraphicsPath path2 = Round(r, 8);
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
			{
				g.TranslateTransform(0f, 3f);
				g.FillPath(brush, path2);
				g.TranslateTransform(0f, -3f);
			}
			using (SolidBrush brush2 = new SolidBrush(Surface))
			{
				g.FillPath(brush2, path2);
			}
			using Pen pen2 = new Pen(CardBorder);
			g.DrawPath(pen2, path2);
		}
		g.SmoothingMode = smoothingMode;
		if (headerLine > r.Top && headerLine < r.Bottom)
		{
			using Pen pen3 = new Pen(CardBorder);
			g.DrawLine(pen3, r.Left + 1, headerLine, r.Right - 1, headerLine);
		}
		if (title != null)
		{
			TextRenderer.DrawText(g, title, UiFonts.Semi(9.5f), new Rectangle(r.Left + 14, r.Top + 1, r.Width - 28, 34), Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		}
	}

	internal static GraphicsPath Round(Rectangle r, int radius)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		int num = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
		if (num <= 1)
		{
			graphicsPath.AddRectangle(r);
			return graphicsPath;
		}
		graphicsPath.AddArc(r.Left, r.Top, num, num, 180f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Top, num, num, 270f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
		graphicsPath.AddArc(r.Left, r.Bottom - num, num, num, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}

	internal static void DoubleBuffer(Control control)
	{
		try
		{
			if (setStyle != null)
			{
				setStyle.Invoke(control, new object[2]
				{
					ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,
					true
				});
			}
		}
		catch (Exception)
		{
		}
	}

	private static void Watch(Control control)
	{
		if (hooked.TryGetValue(control, out var _))
		{
			return;
		}
		hooked.Add(control, new object());
		control.ControlAdded += (object sender, ControlEventArgs e) =>
		{
			if (Enabled)
			{
				MapFonts(e.Control);
				Walk(e.Control);
			}
		};
		EventHandler value2 = delegate
		{
			if (!busy && Enabled)
			{
				Style(control);
			}
		};
		control.BackColorChanged += value2;
		control.ForeColorChanged += value2;
		if (control is ButtonBase)
		{
			control.EnabledChanged += delegate
			{
				control.Invalidate();
			};
		}
	}

	private static void StyleGroup(GroupBox box)
	{
		box.BackColor = (InHost(box) ? Key : Surface);
		box.ForeColor = Text;
		if (hooked.TryGetValue(box, out var _))
		{
			return;
		}
		Padding padding = box.Padding;
		box.Padding = new Padding(Math.Max(padding.Left, 8), Math.Max(padding.Top, 22), Math.Max(padding.Right, 8), Math.Max(padding.Bottom, 8));
		Ours(box);
		Own(box);
		box.Paint += (object sender, PaintEventArgs e) =>
		{
			if (Enabled)
			{
				PaintUnder(box, e.Graphics, box.ClientRectangle);
				DrawCard(e.Graphics, new Rectangle(0, 0, box.Width - 1, box.Height - 4), (box.Text ?? "").Replace("&&", "&").ToUpperInvariant(), 36, InHost(box));
			}
		};
	}

	internal static bool IsPrimary(Button button)
	{
		string text = (button.Text ?? "").Trim().ToUpperInvariant();
		string[] array = primaryTexts;
		foreach (string text2 in array)
		{
			if (text == text2 || text.StartsWith(text2 + " ") || text.StartsWith(text2 + "("))
			{
				return true;
			}
		}
		return text.StartsWith("GHI ");
	}

	private static void StyleButton(Button button)
	{
		if (!buttonStates.TryGetValue(button, out var state))
		{
			state = new ButtonState
			{
				Original = button.BackColor
			};
			buttonStates.Add(button, state);
			button.MouseEnter += delegate
			{
				state.Hover = true;
				button.Invalidate();
			};
			button.MouseLeave += delegate
			{
				state.Hover = false;
				state.Down = false;
				button.Invalidate();
			};
			button.MouseDown += delegate
			{
				state.Down = true;
				button.Invalidate();
			};
			button.MouseUp += delegate
			{
				state.Down = false;
				button.Invalidate();
			};
			button.TextChanged += delegate
			{
				button.Invalidate();
			};
			Ours(button);
			button.Paint += (object sender, PaintEventArgs e) =>
			{
				if (Enabled)
				{
					PaintButton(button, state, e.Graphics);
				}
			};
		}
		else if (!busy)
		{
			state.Original = button.BackColor;
		}
		button.UseVisualStyleBackColor = false;
		button.FlatStyle = FlatStyle.Flat;
		button.FlatAppearance.BorderSize = 0;
		bool flag = button.Parent != null && state.Original.ToArgb() == button.Parent.BackColor.ToArgb();
		button.BackColor = ((flag || IsLight(state.Original) || state.Original.A < byte.MaxValue) ? Field : MapBack(state.Original, Field));
		if (Chroma(state.Original) > 0.25f && state.Original.GetBrightness() < 0.6f)
		{
			button.BackColor = Field;
		}
		button.ForeColor = Text;
	}

	private static void PaintButton(Button b, ButtonState s, Graphics g)
	{
		Rectangle r = new Rectangle(0, 0, b.Width - 1, b.Height - 1);
		PaintUnder(b, g, b.ClientRectangle);
		string text = (b.Text ?? "").Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&");
		string text2 = text.Trim().ToUpperInvariant();
		bool flag = IsPrimary(b);
		bool flag2 = !flag && Chroma(s.Original) > 0.25f && s.Original.GetBrightness() < 0.6f;
		bool flag3 = text2.StartsWith("HỦY") || text2.StartsWith("XÓA") || text2.StartsWith("DELETE") || text2.StartsWith("DỪNG");
		Color color;
		if (flag)
		{
			if (s.Down)
			{
				color = Color.FromArgb(48, 124, 210);
			}
			else
			{
				color = (s.Hover ? AccentHover : Accent);
			}
		}
		else if (flag2)
		{
			color = OnFill;
		}
		else if (s.Down)
		{
			color = Color.FromArgb(61, 66, 75);
		}
		else
		{
			color = (s.Hover ? Color.FromArgb(53, 57, 65) : Field);
		}
		Color color2;
		if (flag)
		{
			color2 = color;
		}
		else
		{
			color2 = (flag2 ? OnBorder : Border);
		}
		Color foreColor;
		if (flag)
		{
			foreColor = Color.White;
		}
		else if (flag2)
		{
			foreColor = Color.FromArgb(207, 230, 255);
		}
		else
		{
			foreColor = (flag3 ? Danger : Text);
		}
		if (!b.Enabled)
		{
			color = Color.FromArgb(38, 40, 44);
			color2 = Color.FromArgb(51, 54, 61);
			foreColor = Color.FromArgb(98, 104, 114);
		}
		g.SmoothingMode = SmoothingMode.AntiAlias;
		using (GraphicsPath path = Round(r, 6))
		{
			using (SolidBrush brush = new SolidBrush(color))
			{
				g.FillPath(brush, path);
			}
			using Pen pen = new Pen(color2);
			g.DrawPath(pen, path);
		}
		g.SmoothingMode = SmoothingMode.None;
		Rectangle bounds = new Rectangle(4, 0, b.Width - 8, b.Height);
		if (b.Image != null)
		{
			int y = (b.Height - b.Image.Height) / 2;
			g.DrawImage(b.Image, 8, y, b.Image.Width, b.Image.Height);
			bounds = new Rectangle(12 + b.Image.Width, 0, b.Width - 16 - b.Image.Width, b.Height);
		}
		TextRenderer.DrawText(g, text, b.Font, bounds, foreColor, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
	}

	private static void StyleCheck(ButtonBase box)
	{
		box.BackColor = MapBack(box.BackColor, Color.Transparent);
		box.ForeColor = Text;
		if (buttonStates.TryGetValue(box, out var value))
		{
			return;
		}
		value = new ButtonState();
		buttonStates.Add(box, value);
		CheckBox check = box as CheckBox;
		RadioButton radio = box as RadioButton;
		if ((check != null && check.Appearance == Appearance.Button) || (radio != null && radio.Appearance == Appearance.Button))
		{
			return;
		}
		Ours(box);
		box.Paint += (object sender, PaintEventArgs e) =>
		{
			if (Enabled)
			{
				Graphics graphics = e.Graphics;
				Color? color = HitFill(box);
				if (color.HasValue)
				{
					using SolidBrush brush = new SolidBrush(color.Value);
					graphics.FillRectangle(brush, box.ClientRectangle);
				}
				else
				{
					PaintUnder(box, graphics, box.ClientRectangle);
				}
				bool flag = ((check != null) ? (check.CheckState != CheckState.Unchecked) : radio.Checked);
				bool flag2 = check != null && check.CheckState == CheckState.Indeterminate;
				int num = 13;
				int y = Math.Max(0, (box.Height - num) / 2);
				if (box.TextAlign == ContentAlignment.TopLeft || box.Height > 30)
				{
					y = Math.Max(0, (box.Font.Height - num) / 2 + 1);
				}
				Rectangle rectangle = new Rectangle(1, y, num, num);
				graphics.SmoothingMode = SmoothingMode.AntiAlias;
				if (radio != null)
				{
					using (SolidBrush brush2 = new SolidBrush(FieldInput))
					{
						graphics.FillEllipse(brush2, rectangle);
					}
					using (Pen pen = new Pen(flag ? Accent : Color.FromArgb(88, 96, 112), 1.5f))
					{
						graphics.DrawEllipse(pen, rectangle);
					}
					if (flag)
					{
						using SolidBrush brush3 = new SolidBrush(Accent);
						graphics.FillEllipse(brush3, (float)rectangle.X + 3.5f, (float)rectangle.Y + 3.5f, num - 7, num - 7);
					}
				}
				else
				{
					using (GraphicsPath path = Round(rectangle, 3))
					{
						using (SolidBrush brush4 = new SolidBrush(flag ? Accent : FieldInput))
						{
							graphics.FillPath(brush4, path);
						}
						using Pen pen2 = new Pen(flag ? Accent : Color.FromArgb(88, 96, 112), 1.5f);
						graphics.DrawPath(pen2, path);
					}
					if (flag2)
					{
						using SolidBrush brush5 = new SolidBrush(Color.White);
						graphics.FillRectangle(brush5, rectangle.X + 3, rectangle.Y + 6, num - 6, 2);
					}
					else if (flag)
					{
						using Pen pen3 = new Pen(Color.White, 2f);
						graphics.DrawLines(pen3, new PointF[3]
						{
							new PointF((float)rectangle.X + 3.2f, (float)rectangle.Y + 7.2f),
							new PointF((float)rectangle.X + 6f, (float)rectangle.Y + 10f),
							new PointF((float)rectangle.X + 10.8f, (float)rectangle.Y + 4.2f)
						});
					}
				}
				graphics.SmoothingMode = SmoothingMode.None;
				Color foreColor = ((!box.Enabled) ? Color.FromArgb(98, 104, 114) : ((box.ForeColor.GetBrightness() < 0.5f) ? Text : box.ForeColor));
				TextRenderer.DrawText(graphics, box.Text, box.Font, new Rectangle(16, 0, Math.Max(1, box.Width - 16), box.Height), foreColor, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
		};
		box.EnabledChanged += delegate
		{
			box.Invalidate();
		};
		if (box.AutoSize)
		{
			Padding padding = box.Padding;
			box.Padding = new Padding(padding.Left, padding.Top, padding.Right + 4, padding.Bottom);
		}
		Leaf(box);
	}

	private static void StyleCombo(ComboBox box)
	{
		box.FlatStyle = FlatStyle.Flat;
		box.BackColor = ((box.DropDownStyle == ComboBoxStyle.DropDownList) ? Field : FieldInput);
		box.ForeColor = Text;
		if (TeklaConnectionHost.IsWindows)
		{
			ComboSkin.Attach(box);
		}
	}

	private static void StyleProgress(ProgressBar bar)
	{
		Action dark = () =>
		{
			try
			{
				if (TeklaConnectionHost.IsWindows && bar.IsHandleCreated)
				{
					SetWindowTheme(bar.Handle, "", "");
				}
				bar.BackColor = Field;
				bar.ForeColor = Accent;
			}
			catch (Exception)
			{
			}
		};
		dark();
		bar.HandleCreated += delegate
		{
			dark();
		};
	}

	private static void StyleGrid(DataGridView grid)
	{
		grid.EnableHeadersVisualStyles = false;
		bool flag = InHost(grid);
		grid.BackgroundColor = (flag ? Key : Surface);
		grid.GridColor = RowLine;
		grid.BorderStyle = BorderStyle.None;
		if (grid.CellBorderStyle == DataGridViewCellBorderStyle.Single || grid.CellBorderStyle == DataGridViewCellBorderStyle.Raised || grid.CellBorderStyle == DataGridViewCellBorderStyle.Sunken)
		{
			grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
		}
		grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
		grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
		grid.DefaultCellStyle.BackColor = MapBack(grid.DefaultCellStyle.BackColor.IsEmpty ? Color.White : grid.DefaultCellStyle.BackColor, Surface);
		grid.DefaultCellStyle.ForeColor = MapFore(grid.DefaultCellStyle.ForeColor.IsEmpty ? Color.Black : grid.DefaultCellStyle.ForeColor);
		grid.DefaultCellStyle.SelectionBackColor = Selection;
		grid.DefaultCellStyle.SelectionForeColor = SelectionText;
		grid.DefaultCellStyle.Padding = new Padding(6, 0, 4, 0);
		grid.AlternatingRowsDefaultCellStyle.BackColor = (flag ? HitGlassAlt : AltRow);
		if (flag)
		{
			grid.DefaultCellStyle.BackColor = HitGlass;
		}
		DataGridViewCellStyle[] array = new DataGridViewCellStyle[2] { grid.ColumnHeadersDefaultCellStyle, grid.RowHeadersDefaultCellStyle };
		foreach (DataGridViewCellStyle obj in array)
		{
			obj.BackColor = Header;
			obj.ForeColor = HeaderText;
			obj.SelectionBackColor = Header;
			obj.SelectionForeColor = HeaderText;
			obj.Padding = new Padding(6, 0, 4, 0);
			obj.Font = UiFonts.Semi(8.25f);
		}
		if (grid.ColumnHeadersHeightSizeMode != DataGridViewColumnHeadersHeightSizeMode.AutoSize && grid.ColumnHeadersHeight < 30)
		{
			grid.ColumnHeadersHeight = 30;
		}
		if (grid.RowTemplate.Height < 28)
		{
			grid.RowTemplate.Height = 28;
		}
		grid.ForeColor = Text;
		grid.BackColor = (flag ? Key : Surface);
		if (hooked.TryGetValue(grid, out var _))
		{
			return;
		}
		grids.Add(new WeakReference(grid));
		Ours(grid);
		grid.CellFormatting += (object sender, DataGridViewCellFormattingEventArgs e) =>
		{
			if (Enabled && e.CellStyle != null)
			{
				if (IsLight(e.CellStyle.BackColor))
				{
					e.CellStyle.BackColor = MapBack(e.CellStyle.BackColor, Surface);
				}
				e.CellStyle.ForeColor = MapFore(e.CellStyle.ForeColor);
				if (IsLight(e.CellStyle.SelectionBackColor))
				{
					e.CellStyle.SelectionBackColor = Selection;
				}
				if (e.CellStyle.SelectionForeColor.GetBrightness() < 0.55f)
				{
					e.CellStyle.SelectionForeColor = SelectionText;
				}
				if (e.Value is int || e.Value is long || e.Value is double || e.Value is decimal || e.Value is float)
				{
					e.CellStyle.Font = UiFonts.Mono(8.75f);
				}
			}
		};
		grid.CellPainting += (object sender, DataGridViewCellPaintingEventArgs e) =>
		{
			if (Enabled)
			{
				PaintCell(grid, e);
			}
		};
		grid.CellMouseUp += (object sender, DataGridViewCellMouseEventArgs e) =>
		{
			if (Enabled && e.Button == MouseButtons.Left)
			{
				ToggleCheckCell(grid, e.ColumnIndex, e.RowIndex, e.Location);
			}
		};
		grid.EditingControlShowing += (object sender, DataGridViewEditingControlShowingEventArgs e) =>
		{
			if (Enabled && e.Control != null)
			{
				e.Control.BackColor = FieldInput;
				e.Control.ForeColor = Text;
			}
		};
		grid.Paint += (object sender, PaintEventArgs e) =>
		{
			if (Enabled)
			{
				PaintGridEmpty(grid, e.Graphics);
			}
		};
		grid.ControlAdded += (object sender, ControlEventArgs e) =>
		{
			if (e.Control is ScrollBar)
			{
				DarkScroll(e.Control);
			}
		};
		foreach (Control control in grid.Controls)
		{
			if (control is ScrollBar)
			{
				DarkScroll(control);
			}
		}
	}

	internal static Rectangle GridEmpty(DataGridView grid)
	{
		int num = (grid.ColumnHeadersVisible ? grid.ColumnHeadersHeight : 0);
		int lastRow = grid.Rows.GetLastRow(DataGridViewElementStates.Displayed);
		if (lastRow >= 0)
		{
			num = Math.Max(num, grid.GetRowDisplayRectangle(lastRow, cutOverflow: true).Bottom);
		}
		int num2 = grid.ClientSize.Width;
		int num3 = grid.ClientSize.Height;
		foreach (Control control in grid.Controls)
		{
			if (control.Visible && control is VScrollBar)
			{
				num2 -= control.Width;
			}
			else if (control.Visible && control is HScrollBar)
			{
				num3 -= control.Height;
			}
		}
		return new Rectangle(0, num + 1, Math.Max(0, num2), Math.Max(0, num3 - num - 1));
	}

	private static void PaintGridEmpty(DataGridView grid, Graphics g)
	{
		if (InHost(grid))
		{
			return;
		}
		Rectangle rectangle = GridEmpty(grid);
		if (rectangle.Height < 12 || rectangle.Width < 12)
		{
			return;
		}
		UiBackdrop.Paint(grid, g, rectangle);
		using SolidBrush brush = new SolidBrush(Color.FromArgb(185, Surface));
		g.FillRectangle(brush, rectangle);
	}

	private static bool IsStatusColumn(DataGridViewColumn column)
	{
		if (column == null)
		{
			return false;
		}
		string text = (column.HeaderText ?? "").Trim().ToUpperInvariant();
		string text2 = (column.Name ?? "").ToUpperInvariant();
		switch (text)
		{
		default:
			return text2 == "STATUS";
		case "TRẠNG THÁI":
		case "KẾT QUẢ":
		case "STATUS":
			return true;
		}
	}

	internal static int Tone(string text)
	{
		string text2 = (text ?? "").ToLowerInvariant();
		if (text2.Length == 0 || text2 == "—" || text2 == "-")
		{
			return 0;
		}
		string[] array = new string[9] { "lỗi", "trùng", "từ chối", "sai", "chặn", "fail", "không đọc", "không được", "error" };
		foreach (string value in array)
		{
			if (text2.Contains(value))
			{
				return 4;
			}
		}
		array = new string[7] { "thiếu", "cảnh báo", "issue", "cần ", "lệch", "bỏ qua", "warning" };
		foreach (string value2 in array)
		{
			if (text2.Contains(value2))
			{
				return 3;
			}
		}
		array = new string[9] { "đã đúng", "khớp", "đã gán", "đã ghi", "xong", " ok", "ok", "giữ nguyên", "không đổi" };
		foreach (string value3 in array)
		{
			if (text2.Contains(value3))
			{
				return 1;
			}
		}
		return 2;
	}

	private static void PaintCell(DataGridView grid, DataGridViewCellPaintingEventArgs e)
	{
		if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Handled)
		{
			return;
		}
		DataGridViewColumn dataGridViewColumn = grid.Columns[e.ColumnIndex];
		if (e.CellStyle == null)
		{
			return;
		}
		if (dataGridViewColumn is DataGridViewCheckBoxColumn)
		{
			e.PaintBackground(e.CellBounds, cellsPaintSelectionBackground: true);
			object formattedValue = e.FormattedValue;
			bool flag = ((formattedValue is bool) ? ((bool)formattedValue) : (formattedValue is CheckState && (CheckState)formattedValue != CheckState.Unchecked));
			Rectangle r = CheckGlyph(grid, e.ColumnIndex, e.RowIndex, e.CellBounds);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (GraphicsPath path = Round(r, 3))
			{
				using (SolidBrush brush = new SolidBrush(flag ? Accent : FieldInput))
				{
					e.Graphics.FillPath(brush, path);
				}
				using Pen pen = new Pen(flag ? Accent : Color.FromArgb(88, 96, 112), 1.5f);
				e.Graphics.DrawPath(pen, path);
			}
			if (flag)
			{
				using Pen pen2 = new Pen(Color.White, 2f);
				e.Graphics.DrawLines(pen2, new PointF[3]
				{
					new PointF((float)r.X + 3.2f, (float)r.Y + 7.2f),
					new PointF((float)r.X + 6f, (float)r.Y + 10f),
					new PointF((float)r.X + 10.8f, (float)r.Y + 4.2f)
				});
			}
			e.Graphics.SmoothingMode = SmoothingMode.None;
			e.Handled = true;
		}
		else
		{
			if (!IsStatusColumn(dataGridViewColumn))
			{
				return;
			}
			string text = Convert.ToString(e.FormattedValue);
			int num = Tone(text);
			if (num == 0)
			{
				return;
			}
			e.PaintBackground(e.CellBounds, cellsPaintSelectionBackground: true);
			Color color = num switch
			{
				4 => ErrBack, 
				3 => WarnBack, 
				1 => OkBack, 
				_ => InfoBack, 
			};
			Color foreColor = num switch
			{
				4 => ErrText, 
				3 => WarnText, 
				1 => OkText, 
				_ => InfoText, 
			};
			Font font = UiFonts.Semi(8f);
			int val = Math.Min(TextRenderer.MeasureText(e.Graphics, text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 16, e.CellBounds.Width - 10);
			int num2 = Math.Min(20, e.CellBounds.Height - 6);
			Rectangle rectangle = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + (e.CellBounds.Height - num2) / 2, Math.Max(10, val), num2);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (GraphicsPath path2 = Round(rectangle, num2 / 2))
			{
				using SolidBrush brush2 = new SolidBrush(color);
				e.Graphics.FillPath(brush2, path2);
			}
			e.Graphics.SmoothingMode = SmoothingMode.None;
			TextRenderer.DrawText(e.Graphics, text, font, rectangle, foreColor, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			e.Handled = true;
		}
	}

	internal static Rectangle CheckGlyph(DataGridView grid, int column, int row, Rectangle cell)
	{
		Rectangle rectangle = Rectangle.Empty;
		try
		{
			rectangle = grid.Rows[row].Cells[column].GetContentBounds(row);
		}
		catch (Exception)
		{
		}
		int num = ((rectangle.Width > 0) ? (cell.X + rectangle.X + rectangle.Width / 2) : (cell.X + cell.Width / 2));
		int num2 = ((rectangle.Height > 0) ? (cell.Y + rectangle.Y + rectangle.Height / 2) : (cell.Y + cell.Height / 2));
		return new Rectangle(num - 7, num2 - 7, 14, 14);
	}

	internal static bool ToggleCheckCell(DataGridView grid, int column, int row, Point inCell)
	{
		if (row < 0 || column < 0 || row >= grid.Rows.Count || column >= grid.Columns.Count)
		{
			return false;
		}
		if (!(grid.Columns[column] is DataGridViewCheckBoxColumn) || grid.ReadOnly)
		{
			return false;
		}
		DataGridViewCell dataGridViewCell = grid.Rows[row].Cells[column];
		if (dataGridViewCell.ReadOnly || dataGridViewCell.GetContentBounds(row).Contains(inCell))
		{
			return false;
		}
		object value = dataGridViewCell.Value;
		bool flag = ((value is bool) ? ((bool)value) : (value is CheckState && (CheckState)value != CheckState.Unchecked));
		if (grid.IsCurrentCellInEditMode)
		{
			grid.EndEdit();
		}
		dataGridViewCell.Value = ((value is CheckState) ? ((object)((!flag) ? CheckState.Checked : CheckState.Unchecked)) : ((object)(!flag)));
		return true;
	}

	private static void StyleTabs(TabControl tabs)
	{
		if (tabs.DrawMode != TabDrawMode.Normal || hooked.TryGetValue(tabs, out var _))
		{
			return;
		}
		tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
		bool frozen = false;
		tabs.Selecting += delegate
		{
			if (TeklaConnectionHost.IsWindows && tabs.IsHandleCreated && !frozen)
			{
				frozen = true;
				SendMessage(tabs.Handle, 11, IntPtr.Zero, IntPtr.Zero);
			}
		};
		tabs.Selected += delegate
		{
			if (frozen)
			{
				frozen = false;
				SendMessage(tabs.Handle, 11, new IntPtr(1), IntPtr.Zero);
				tabs.Invalidate(invalidateChildren: true);
			}
		};
		tabs.Padding = new Point(16, 8);
		tabs.DrawItem += (object sender, DrawItemEventArgs e) =>
		{
			if (e.Index >= 0 && e.Index < tabs.TabPages.Count)
			{
				DrawTab(tabs, e.Graphics, e.Index, e.Bounds);
			}
		};
		if (TeklaConnectionHost.IsWindows)
		{
			TabSkin.Attach(tabs);
		}
	}

	internal static void DrawTab(TabControl tabs, Graphics g, int index, Rectangle area)
	{
		bool flag = index == tabs.SelectedIndex;
		using (SolidBrush brush = new SolidBrush(flag ? Surface : TabStrip))
		{
			g.FillRectangle(brush, area);
		}
		if (flag)
		{
			using SolidBrush brush2 = new SolidBrush(AccentBar);
			g.FillRectangle(brush2, area.Left, area.Bottom - 2, area.Width, 2);
		}
		TextRenderer.DrawText(g, tabs.TabPages[index].Text, tabs.Font, area, flag ? Text : Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
	}

	internal static void StyleStrip(ToolStrip strip)
	{
		if (!Enabled || strip == null)
		{
			return;
		}
		strip.Renderer = new ToolStripProfessionalRenderer(new DarkColors())
		{
			RoundedEdges = false
		};
		strip.BackColor = ((strip is StatusStrip) ? Toolbar : Surface);
		strip.ForeColor = ((strip is StatusStrip) ? Muted : Text);
		foreach (ToolStripItem item in strip.Items)
		{
			StyleItem(item, strip is StatusStrip);
		}
		if (hooked.TryGetValue(strip, out var _))
		{
			return;
		}
		hooked.Add(strip, new object());
		strip.ItemAdded += (object sender, ToolStripItemEventArgs e) =>
		{
			if (Enabled)
			{
				StyleItem(e.Item, strip is StatusStrip);
			}
		};
	}

	private static void StyleItem(ToolStripItem item, bool status)
	{
		item.ForeColor = (status ? Muted : MapFore(item.ForeColor));
		if (item is ToolStripDropDownItem { DropDown: not null } toolStripDropDownItem)
		{
			StyleStrip(toolStripDropDownItem.DropDown);
		}
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

	[DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
	private static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

	[DllImport("gdi32.dll")]
	private static extern int SetTextColor(IntPtr hdc, int color);

	[DllImport("gdi32.dll")]
	private static extern int SetBkColor(IntPtr hdc, int color);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateSolidBrush(int color);

	private static IntPtr DisabledBrush()
	{
		if (disabledBrush == IntPtr.Zero)
		{
			disabledBrush = CreateSolidBrush(ColorTranslator.ToWin32(DisabledField));
		}
		return disabledBrush;
	}

	private static void DarkTitle(Form form)
	{
		if (!TeklaConnectionHost.IsWindows)
		{
			return;
		}
		EventHandler eventHandler = delegate
		{
			try
			{
				int value = 1;
				if (DwmSetWindowAttribute(form.Handle, 20, ref value, 4) != 0)
				{
					DwmSetWindowAttribute(form.Handle, 19, ref value, 4);
				}
			}
			catch (Exception)
			{
			}
		};
		if (form.IsHandleCreated)
		{
			eventHandler(form, EventArgs.Empty);
		}
		else
		{
			form.HandleCreated += eventHandler;
		}
	}

	internal static void DarkScroll(Control control)
	{
		if (!TeklaConnectionHost.IsWindows)
		{
			return;
		}
		EventHandler eventHandler = delegate
		{
			try
			{
				SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
			}
			catch (Exception)
			{
			}
		};
		if (control.IsHandleCreated)
		{
			eventHandler(control, EventArgs.Empty);
		}
		control.HandleCreated += eventHandler;
	}

	internal static void MarkNav(Label item)
	{
		if (!hooked.TryGetValue(item, out var _))
		{
			hooked.Add(item, new object());
		}
		Ours(item);
		item.Paint += (object sender, PaintEventArgs e) =>
		{
			bool flag = item.BackColor.ToArgb() == NavActive.ToArgb();
			if (Enabled)
			{
				using (SolidBrush brush = new SolidBrush(flag ? NavDark : Nav))
				{
					e.Graphics.FillRectangle(brush, item.ClientRectangle);
				}
				if (flag)
				{
					using SolidBrush brush2 = new SolidBrush(AccentBar);
					e.Graphics.FillRectangle(brush2, 0, 0, 3, item.Height);
				}
				TextRenderer.DrawText(e.Graphics, item.Text, UiFonts.Semi(9f), new Rectangle(18, 0, item.Width - 20, item.Height), flag ? Color.White : NavText, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
		};
	}

	internal static void RefreshBackdrop()
	{
		Refresh(zones, (Control c) =>
		{
			c.Invalidate(invalidateChildren: false);
		});
		Refresh(leaves, (Control c) =>
		{
			c.Invalidate();
		});
		Refresh(grids, (Control c) =>
		{
			DataGridView dataGridView = (DataGridView)c;
			Rectangle rc = GridEmpty(dataGridView);
			if (rc.Height >= 12)
			{
				dataGridView.Invalidate(rc);
			}
		});
	}

	private static void Refresh(List<WeakReference> list, Action<Control> act)
	{
		for (int num = list.Count - 1; num >= 0; num--)
		{
			if (list[num].Target is Control { IsDisposed: false } control)
			{
				if (control.IsHandleCreated && control.Visible)
				{
					try
					{
						act(control);
					}
					catch (Exception)
					{
					}
				}
			}
			else
			{
				list.RemoveAt(num);
			}
		}
	}
}
