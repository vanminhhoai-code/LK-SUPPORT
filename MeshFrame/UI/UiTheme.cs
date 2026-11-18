using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TeklaQuickAssign
{
    // 1.7.99 giao diện tối; 1.7.100 vẽ lại theo 5 ảnh mẫu người dùng duyệt (6/11/2026):
    // khung bo góc (card), nút/ô tích/ô chọn/hàng tab tự vẽ, bảng có nhãn trạng thái, font Inter (nạp riêng cho app,
    // không cài vào máy), nhà xưởng xoay phía sau mọi vùng trống của vùng làm việc (UiBackdrop).
    // Chỉ đổi cách vẽ: không đổi vị trí logic, sự kiện, dữ liệu. Theme=classic thì không làm gì (giao diện cũ).
    // Vùng "zone" = khung chứa nằm ngoài card: nền là nhà xưởng xoay. Trong card: nền đặc Surface (không vẽ lại mỗi nhịp).
    internal static class UiTheme
    {
        internal static readonly Color Bg = Color.FromArgb(27, 28, 32),
            Surface = Color.FromArgb(35, 37, 42),
            Field = Color.FromArgb(44, 47, 53),
            FieldInput = Color.FromArgb(27, 29, 33),
            Border = Color.FromArgb(61, 65, 72),
            CardBorder = Color.FromArgb(51, 54, 61),
            Text = Color.FromArgb(227, 230, 234),
            Muted = Color.FromArgb(141, 149, 161),
            HeaderText = Color.FromArgb(170, 178, 189),
            Accent = Color.FromArgb(59, 142, 234),
            AccentHover = Color.FromArgb(78, 157, 242),
            AccentBar = Color.FromArgb(74, 163, 255),
            Nav = Color.FromArgb(20, 21, 25),
            NavDark = Color.FromArgb(38, 42, 49),
            NavText = Color.FromArgb(180, 186, 195),
            NavMuted = Color.FromArgb(94, 101, 112),
            Header = Color.FromArgb(42, 45, 51),
            AltRow = Color.FromArgb(38, 40, 45),
            RowLine = Color.FromArgb(44, 47, 53),
            Selection = Color.FromArgb(30, 58, 92),
            SelectionText = Color.FromArgb(230, 241, 255),
            Danger = Color.FromArgb(255, 122, 112),
            Toolbar = Color.FromArgb(26, 28, 32),
            TabStrip = Color.FromArgb(22, 23, 27),
            OnFill = Color.FromArgb(32, 54, 83),
            OnBorder = Color.FromArgb(47, 95, 149),
            OkBack = Color.FromArgb(23, 58, 39), OkText = Color.FromArgb(111, 220, 151),
            InfoBack = Color.FromArgb(30, 58, 92), InfoText = Color.FromArgb(156, 203, 255),
            WarnBack = Color.FromArgb(61, 50, 24), WarnText = Color.FromArgb(242, 194, 91),
            ErrBack = Color.FromArgb(63, 31, 29), ErrText = Color.FromArgb(255, 138, 128);
        // 1.7.101: màu khóa trong suốt (TransparencyKey) — nền, khung, ô trống bảng của vùng làm việc là "lỗ" để thấy
        // cửa sổ nền nhà xưởng phía sau. KeyMode chỉ bật khi cửa sổ nền chạy được (Windows + MP4).
        internal static readonly Color Key = Color.FromArgb(11, 12, 14);
        internal static bool KeyMode;
        // Đang zoom (UiZoom đặt; 250 ms sau nấc cuối): chưa gửi lại vị trí khung cho tiến trình nền.
        internal static DateTime ZoomBusyUntil = DateTime.MinValue;
        internal static bool ZoomBusy { get { return DateTime.UtcNow < ZoomBusyUntil; } }
        internal const int BackdropVeil = 60;
        internal static readonly Color CardFill = Color.FromArgb(158, 30, 32, 37), GlassFill = Color.FromArgb(185, 14, 16, 20);
        // 1.7.111: vùng người dùng bấm (dòng bảng, ô tích, ô chọn một) KHÔNG được vẽ màu khoá: Windows cho chuột ở điểm ảnh màu khoá
        // đi xuyên xuống cửa sổ video. Dùng màu đặc gần với thẻ/kính phủ trên video để click luôn tới đúng control.
        internal static readonly Color HitCard = Color.FromArgb(34, 36, 41), HitGlass = Color.FromArgb(21, 23, 28), HitGlassAlt = Color.FromArgb(25, 27, 32);
        // Màu đặc cho vùng bấm của control đang nằm trên nền video (null khi không ở chế độ video).
        // 1.7.113: khung xem trước bản vẽ tự vẽ trên nền giấy trắng → hiện thành "giấy tối": đảo độ sáng nhưng giữ sắc màu
        // (đảo RGB rồi xoay sắc độ 180°): trắng → nền tối PaperDark, đen → nét sáng, xanh vẫn xanh, đỏ vẫn đỏ (sáng hơn).
        internal const float PaperDark = 0.10f, PaperLight = 0.92f;
        private static readonly float[,] HueHalfTurn = { { -0.574f, 1.430f, 0.144f }, { 0.426f, 0.430f, 0.144f }, { 0.426f, 1.430f, -0.856f } };
        internal static System.Drawing.Imaging.ColorMatrix PaperMatrix()
        {
            float k = PaperLight - PaperDark; var m = new float[5][];
            for (int i = 0; i < 5; i++) m[i] = new float[5];
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) m[i][j] = -k * HueHalfTurn[j, i];
            m[3][3] = 1; m[4][0] = m[4][1] = m[4][2] = PaperLight; m[4][4] = 1;
            return new System.Drawing.Imaging.ColorMatrix(m);
        }
        internal static Color PaperColor(Color c)
        {
            float k = PaperLight - PaperDark; float[] v = { c.R / 255f, c.G / 255f, c.B / 255f }; var o = new int[3];
            for (int j = 0; j < 3; j++) { float x = PaperLight; for (int i = 0; i < 3; i++) x -= k * HueHalfTurn[j, i] * v[i]; o[j] = (int)Math.Round(Math.Max(0, Math.Min(1, x)) * 255); }
            return Color.FromArgb(c.A, o[0], o[1], o[2]);
        }
        internal static void PaintPaper(Control control, PaintEventArgs e, Action<PaintEventArgs> body)
        {
            Size size = control.ClientSize;
            if (!Enabled || size.Width <= 0 || size.Height <= 0) { body(e); return; }
            using (var bitmap = new Bitmap(size.Width, size.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(control.BackColor.A == 255 ? control.BackColor : Color.White);
                    body(new PaintEventArgs(g, new Rectangle(Point.Empty, size)));
                }
                using (var attributes = new System.Drawing.Imaging.ImageAttributes())
                {
                    attributes.SetColorMatrix(PaperMatrix());
                    e.Graphics.DrawImage(bitmap, new Rectangle(Point.Empty, size), 0, 0, size.Width, size.Height, GraphicsUnit.Pixel, attributes);
                }
            }
        }

        // 1.7.113: ToolTip tối theo chủ đề.
        internal static ToolTip Styled(ToolTip tip) { StyleToolTip(tip); return tip; }
        internal static void StyleToolTip(ToolTip tip)
        {
            if (!Enabled || tip == null || tip.OwnerDraw) return;
            tip.OwnerDraw = true;
            tip.Draw += delegate(object sender, DrawToolTipEventArgs e)
            {
                using (var back = new SolidBrush(Surface)) e.Graphics.FillRectangle(back, e.Bounds);
                using (var pen = new Pen(Border)) e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1));
                TextRenderer.DrawText(e.Graphics, e.ToolTipText, e.Font, e.Bounds, Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.LeftAndRightPadding | TextFormatFlags.NoPrefix);
            };
        }

        // 1.7.113: tạm dừng vẽ một vùng trong lúc đổi menu/tab rồi vẽ lại MỘT lần (hết cảnh trang vẽ dần từng phần).
        internal static void Freeze(Control control, Action change)
        {
            bool redraw = TeklaConnectionHost.IsWindows && control != null && control.IsHandleCreated;
            if (redraw) SendMessage(control.Handle, 0x000B, IntPtr.Zero, IntPtr.Zero);
            try { change(); }
            finally { if (redraw) { SendMessage(control.Handle, 0x000B, new IntPtr(1), IntPtr.Zero); control.Invalidate(true); control.Update(); } }
        }

        // 1.7.113: hộp thoại đổi màu NGAY khi tạo/hiện (không đợi app rảnh) — hết chớp trắng.
        internal static void Prepare(Form form)
        {
            if (!Enabled || form == null) return;
            object marker;
            if (forms.TryGetValue(form, out marker)) return;
            forms.Add(form, new object());
            MapFonts(form); Apply(form);
            form.Deactivate += delegate { PrepareOpenForms(); };
        }
        private static void PrepareOpenForms() { try { foreach (Form open in Application.OpenForms) Prepare(open); } catch (Exception) { } }

        internal static Color? HitFill(Control control) { return InHost(control) ? (Color?)(InCard(control) ? HitCard : HitGlass) : null; }
        internal struct OverlayShape { internal Rectangle Bounds; internal int Radius; internal Color Fill; }
        private static readonly List<WeakReference> cardOwners = new List<WeakReference>();
        private static readonly ConditionalWeakTable<Control, object> glass = new ConditionalWeakTable<Control, object>();
        private static void Own(Control c) { cardOwners.Add(new WeakReference(c)); }
        private static bool InHost(Control c) { return KeyMode && UiBackdrop.Inside(c); }

        // Khung xem trước tự vẽ (profile CAD...): nền trong suốt, cửa sổ nền phủ một lớp tối.
        internal static void Glass(Control control)
        {
            object m;
            if (!Enabled || control == null || glass.TryGetValue(control, out m)) return;
            glass.Add(control, new object()); Own(control);
        }
        internal static bool IsGlass(Control control) { object m; return control != null && glass.TryGetValue(control, out m); }

        // Hình các khung (tọa độ vùng làm việc) để cửa sổ nền vẽ lớp trong suốt.
        internal static List<OverlayShape> Shapes(Control host)
        {
            var list = new List<OverlayShape>();
            if (host == null || !host.IsHandleCreated) return list;
            for (int i = cardOwners.Count - 1; i >= 0; i--)
            {
                var c = cardOwners[i].Target as Control;
                if (c == null || c.IsDisposed) { cardOwners.RemoveAt(i); continue; }
                if (!c.IsHandleCreated || !c.Visible || !host.Contains(c)) continue;
                Point off;
                try { off = host.PointToClient(c.PointToScreen(Point.Empty)); } catch (Exception) { continue; }
                var layout = c as TableLayoutPanel;
                List<int[]> b;
                if (layout != null && bands.TryGetValue(layout, out b))
                    foreach (var band in BandRects(layout)) { Rectangle r = band.Key; r.Offset(off); list.Add(new OverlayShape { Bounds = r, Radius = 8, Fill = CardFill }); }
                else if (c is GroupBox) { var r = new Rectangle(off.X, off.Y, c.Width - 1, c.Height - 4); list.Add(new OverlayShape { Bounds = r, Radius = 8, Fill = CardFill }); }
                else if (IsWholeCard(c)) { Rectangle r = Inset(c.ClientRectangle, 4); r.Offset(off); list.Add(new OverlayShape { Bounds = r, Radius = 8, Fill = CardFill }); }
                else if (IsGlass(c)) list.Add(new OverlayShape { Bounds = new Rectangle(off, c.ClientSize), Radius = 6, Fill = GlassFill });
            }
            list.Reverse();
            return list;
        }

        private static readonly Color ClassicNavActive = Color.FromArgb(0, 112, 192), ClassicNav = Color.FromArgb(31, 54, 77);

        // Chốt theo lúc mở app: đổi ô 'Giao diện tối' chỉ có tác dụng khi mở lại app.
        private static bool? active;
        internal static bool Enabled { get { if (!active.HasValue) active = UiPreferences.Dark; return active.Value; } }
        internal static void ResetForTests() { active = null; }
        internal static Color NavActive { get { return Enabled ? NavDark : ClassicNavActive; } }
        internal static Color NavBack { get { return Enabled ? Nav : ClassicNav; } }
        internal const int NavWidth = 212;

        [ThreadStatic] private static bool busy;
        private static readonly ConditionalWeakTable<Control, object> hooked = new ConditionalWeakTable<Control, object>();
        private static readonly ConditionalWeakTable<Control, object> ours = new ConditionalWeakTable<Control, object>();
        private static void Ours(Control c) { object m; if (!ours.TryGetValue(c, out m)) ours.Add(c, new object()); }
        private static readonly ConditionalWeakTable<Control, object> zoneSet = new ConditionalWeakTable<Control, object>();
        private static readonly ConditionalWeakTable<Control, object> wholeCards = new ConditionalWeakTable<Control, object>();
        private static readonly ConditionalWeakTable<Control, object> headers = new ConditionalWeakTable<Control, object>();
        private static readonly ConditionalWeakTable<TableLayoutPanel, List<int[]>> bands = new ConditionalWeakTable<TableLayoutPanel, List<int[]>>();
        private static readonly List<WeakReference> zones = new List<WeakReference>(), leaves = new List<WeakReference>(), grids = new List<WeakReference>();

        // ---- cửa sổ phụ ----
        private static bool installed;
        private static readonly ConditionalWeakTable<Form, object> forms = new ConditionalWeakTable<Form, object>();
        internal static void Install()
        {
            if (installed || !Enabled) return;
            installed = true;
            Application.Idle += delegate
            {
                try
                {
                    foreach (Form form in Application.OpenForms)
                    {
                        Prepare(form);
                    }
                }
                catch (InvalidOperationException) { }
            };
        }

        internal static void Apply(Control root)
        {
            Form form = root as Form;
            object seen;
            if (form != null && !forms.TryGetValue(form, out seen)) forms.Add(form, new object());
            if (!Enabled || root == null) return;
            Walk(root);
        }

        // ---- bố cục card (đăng ký trước khi Apply) ----
        // Các dòng from..to của bảng bố cục vẽ chung một card; header=true: dòng đầu là tiêu đề card (có vạch dưới).
        internal static void Bands(TableLayoutPanel layout, params int[] spec)
        {
            if (!Enabled || layout == null) return;
            var list = new List<int[]>();
            for (int i = 0; i + 2 < spec.Length; i += 3) list.Add(new[] { spec[i], spec[i + 1], spec[i + 2] });
            bands.Remove(layout); bands.Add(layout, list); Own(layout);
            foreach (Control child in layout.Controls)
            {
                int[] band = BandOf(layout, child);
                if (band == null) continue;
                TableLayoutPanelCellPosition cell = layout.GetPositionFromControl(child);
                Padding m = child.Margin;
                // 1.7.102: chỉ thêm lề ngang. Lề dọc trong hàng cao cố định làm cụt đáy nút (APPLY PROPERTIES… ở 1.7.101).
                child.Margin = new Padding(Math.Max(m.Left, 14), m.Top, Math.Max(m.Right, 14), m.Bottom);
            }
        }

        internal static void WholeCard(Control control)
        {
            if (!Enabled || control == null) return;
            object marker;
            if (wholeCards.TryGetValue(control, out marker)) return;
            wholeCards.Add(control, new object()); Own(control);
            Padding p = control.Padding;
            control.Padding = new Padding(p.Left + 14, p.Top + 12, p.Right + 14, p.Bottom + 12);
        }

        internal static void MarkHeader(Control label)
        {
            if (!Enabled || label == null) return;
            object marker;
            if (!headers.TryGetValue(label, out marker)) headers.Add(label, new object());
            if (label is Label) label.Padding = new Padding(label.Padding.Left, Math.Max(label.Padding.Top, 6), label.Padding.Right, label.Padding.Bottom);
            label.Font = (Font)UiFonts.Semi(10f).Clone();
        }

        private static int[] BandOf(TableLayoutPanel layout, Control child)
        {
            List<int[]> list;
            if (!bands.TryGetValue(layout, out list)) return null;
            TableLayoutPanelCellPosition cell = layout.GetPositionFromControl(child);
            if (cell.Row < 0) return null;
            foreach (int[] band in list) if (cell.Row >= band[0] && cell.Row <= band[1]) return band;
            return null;
        }

        private static bool IsWholeCard(Control c) { object m; return c != null && wholeCards.TryGetValue(c, out m); }
        internal static bool IsZone(Control c) { object m; return c != null && zoneSet.TryGetValue(c, out m); }

        internal static bool InCard(Control control)
        {
            for (Control x = control; x != null && x.Parent != null; x = x.Parent)
            {
                Control p = x.Parent;
                if (IsWholeCard(p) || p is GroupBox || p is DataGridView) return true;
                var layout = p as TableLayoutPanel;
                if (layout != null && BandOf(layout, x) != null) return true;
            }
            return false;
        }

        // Màu nền ngay dưới control (null = nhà xưởng xoay).
        internal static Color? Under(Control control)
        {
            if (InHost(control)) return Key;
            Control p = control.Parent;
            if (p == null) return Bg;
            var layout = p as TableLayoutPanel;
            if (layout != null && BandOf(layout, control) != null) return Surface;
            if (IsWholeCard(p)) return Surface;
            if (IsZone(p)) return null;
            if (p.BackColor.A < 255) return Under(p);
            return p.BackColor;
        }

        internal static void PaintUnder(Control control, Graphics g, Rectangle area)
        {
            Color? under = Under(control);
            if (under.HasValue) using (var b = new SolidBrush(under.Value)) g.FillRectangle(b, area);
            else UiBackdrop.Paint(control, g, area);
        }

        // ---- duyệt cây ----
        private static void Walk(Control control)
        {
            if (!Painted(control)) Style(control); else DoubleBuffer(control); // 1.7.113: khung tự vẽ không nháy khi vẽ lại
            foreach (Control child in control.Controls) Walk(child);
            if (control.ContextMenuStrip != null) StyleStrip(control.ContextMenuStrip);
            object marker;
            if (!hooked.TryGetValue(control, out marker))
            {
                hooked.Add(control, new object());
                control.ControlAdded += delegate(object sender, ControlEventArgs e) { if (Enabled) { MapFonts(e.Control); Walk(e.Control); } };
            }
        }

        // ---- màu ----
        internal static Color MapBack(Color color, Color fallback)
        {
            if (color.A < 255) return color;
            if (color.GetBrightness() <= 0.6f) return color;
            if (Chroma(color) < 0.08f) return fallback;
            return FromHsl(color.GetHue(), Math.Min(color.GetSaturation(), 0.35f), 0.18f);
        }

        internal static Color MapFore(Color color)
        {
            if (color.A < 255) return color;
            float light = color.GetBrightness();
            if (light >= 0.55f) return color;
            if (color.GetSaturation() < 0.2f) return light < 0.3f ? Text : Muted;
            return FromHsl(color.GetHue(), Math.Min(color.GetSaturation(), 0.85f), 0.72f);
        }

        internal static float Chroma(Color color) { return (Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B))) / 255f; }
        internal static bool IsLight(Color color) { return color.A == 255 && color.GetBrightness() > 0.6f; }

        internal static Color FromHsl(float hue, float saturation, float lightness)
        {
            float c = (1 - Math.Abs(2 * lightness - 1)) * saturation, h = hue / 60f, x = c * (1 - Math.Abs(h % 2 - 1)), m = lightness - c / 2;
            float r = 0, g = 0, b = 0;
            if (h < 1) { r = c; g = x; } else if (h < 2) { r = x; g = c; } else if (h < 3) { g = c; b = x; }
            else if (h < 4) { g = x; b = c; } else if (h < 5) { r = x; b = c; } else { r = c; b = x; }
            Func<float, int> to = v => Math.Max(0, Math.Min(255, (int)Math.Round((v + m) * 255)));
            return Color.FromArgb(to(r), to(g), to(b));
        }

        // ---- control tự vẽ của app (khung xem trước bản vẽ...) giữ nguyên ----
        internal static bool Painted(Control control)
        {
            if (control is PictureBox || control.BackgroundImage != null) return true;
            if (control is FactoryWelcome || control is FactoryNavPanel || control is StatusChip) return true;
            Type type = control.GetType();
            Assembly forms = typeof(Control).Assembly;
            if (type.Assembly != forms)
            {
                foreach (string name in new[] { "OnPaint", "OnPaintBackground" })
                {
                    MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(PaintEventArgs) }, null);
                    if (method != null && method.DeclaringType != null && method.DeclaringType.Assembly != forms) return true;
                }
            }
            object marker;
            if (ours.TryGetValue(control, out marker)) return false; // Paint do skin gắn
            return HasPaintHandler(control);
        }

        private static bool HasPaintHandler(Control control)
        {
            try
            {
                object key = null;
                foreach (string name in new[] { "EventPaint", "PaintEvent" })
                {
                    FieldInfo field = typeof(Control).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
                    if (field != null) { key = field.GetValue(null); break; }
                }
                if (key == null) return false;
                PropertyInfo events = typeof(System.ComponentModel.Component).GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic);
                var list = events == null ? null : events.GetValue(control, null) as System.ComponentModel.EventHandlerList;
                return list != null && list[key] != null;
            }
            catch (Exception) { return false; }
        }

        // ---- font ----
        // Chạy TRƯỚC UiZoom.Attach để Ctrl + lăn chuột phóng to đúng font Inter.
        internal static void MapFonts(Control control)
        {
            if (!Enabled || control == null || control is FactoryWelcome) return;
            try
            {
                object marker;
                Font f = control.Font;
                bool ambient = control.Parent != null && Equals(f, control.Parent.Font);
                if (headers.TryGetValue(control, out marker)) { }
                // Mỗi control một bản font riêng: control bị hủy có thể hủy luôn font của nó.
                else if (control is Form) control.Font = (Font)UiFonts.Body(9f).Clone();
                // Ô cỡ cố định (không AutoSize): giữ Segoe UI như cũ để chữ không bị cắt (ảnh 1.7.100 của người dùng).
                else if ((control is Label || control is ButtonBase) && !control.AutoSize && !(control is Button && headers.TryGetValue(control, out marker)))
                    control.Font = new Font(f.Name == "Inter" || f.Name == "Inter SemiBold" ? "Segoe UI" : f.Name, f.Size, f.Style);
                else if (control is ButtonBase || control is TabControl) control.Font = (Font)UiFonts.Semi(Math.Max(8.25f, Math.Min(f.Size, 9.5f) * 0.97f)).Clone();
                else if (!ambient && UiFonts.Replaceable(f))
                    control.Font = (Font)(f.Size >= 13f && f.Bold ? UiFonts.Semi(10.5f) : f.Bold ? UiFonts.Semi(f.Size * 0.97f) : UiFonts.Body(f.Size * 0.97f, f.Style)).Clone();
            }
            catch (Exception) { }
            if (control is DataGridView) return;
            foreach (Control child in control.Controls) MapFonts(child);
        }

        // ---- áp màu + bộ vẽ theo loại ----
        private static void Style(Control control)
        {
            busy = true;
            try { StyleCore(control); }
            catch (Exception) { }
            finally { busy = false; }
            Watch(control);
        }

        private static bool IsContainer(Control c)
        {
            return c is Panel || c is UserControl || c is TableLayoutPanel || c is FlowLayoutPanel || c is SplitContainer || c is SplitterPanel || c is TabPage || c is ContainerControl;
        }

        private static void StyleCore(Control control)
        {
            if (control is Form)
            {
                control.BackColor = Bg; control.ForeColor = Text;
                DarkTitle((Form)control);
            }
            else if (control is DataGridView) StyleGrid((DataGridView)control);
            else if (control is Button) StyleButton((Button)control);
            else if (control is CheckBox || control is RadioButton) StyleCheck((ButtonBase)control);
            else if (control is ComboBox) StyleCombo((ComboBox)control);
            else if (control is TextBoxBase)
            {
                control.BackColor = MapBack(control.BackColor, FieldInput); control.ForeColor = MapFore(control.ForeColor);
                var box = control as TextBox; if (box != null && box.BorderStyle == BorderStyle.Fixed3D) box.BorderStyle = BorderStyle.FixedSingle;
                var rich = control as RichTextBox; if (rich != null) rich.BorderStyle = BorderStyle.FixedSingle;
                DarkScroll(control);
            }
            else if (control is UpDownBase)
            {
                control.BackColor = MapBack(control.BackColor, FieldInput); control.ForeColor = MapFore(control.ForeColor);
                ((UpDownBase)control).BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is ListBox || control is ListView || control is TreeView)
            {
                control.BackColor = MapBack(control.BackColor, FieldInput); control.ForeColor = MapFore(control.ForeColor);
                var list = control as ListBox; if (list != null && list.BorderStyle == BorderStyle.Fixed3D) list.BorderStyle = BorderStyle.FixedSingle;
                DarkScroll(control);
            }
            else if (control is TabControl) StyleTabs((TabControl)control);
            else if (control is ToolStrip) StyleStrip((ToolStrip)control);
            else if (control is GroupBox) StyleGroup((GroupBox)control);
            else if (control is LinkLabel)
            {
                var link = (LinkLabel)control; link.LinkColor = AccentBar; link.ActiveLinkColor = Text; link.VisitedLinkColor = AccentBar;
                link.BackColor = MapBack(link.BackColor, Color.Transparent); link.ForeColor = MapFore(link.ForeColor);
                Leaf(link);
            }
            else if (control is Label)
            {
                Color? under = Under(control);
                if (InHost(control) && !IsLight(control.BackColor)) control.BackColor = Key;
                else control.BackColor = MapBack(control.BackColor, under.HasValue && !IsZone(control.Parent) ? under.Value : Color.Transparent);
                control.ForeColor = IsLight(control.BackColor) ? Color.FromArgb(20, 20, 20) : MapFore(control.ForeColor);
                object m;
                if (headers.TryGetValue(control, out m)) control.ForeColor = Text;
                Leaf(control);
            }
            else if (control is ProgressBar) StyleProgress((ProgressBar)control);
            else if (IsContainer(control))
            {
                if (control is TabPage) ((TabPage)control).UseVisualStyleBackColor = false;
                if (InHost(control)) { control.BackColor = Key; if (!InCard(control)) Zone(control); }
                else if (InCard(control)) control.BackColor = Surface;
                else { control.BackColor = Bg; Zone(control); }
                control.ForeColor = Text;
                DoubleBuffer(control);
                var scroll = control as ScrollableControl; if (scroll != null && scroll.AutoScroll) DarkScroll(control);
            }
            else
            {
                control.BackColor = MapBack(control.BackColor, Bg);
                control.ForeColor = IsLight(control.BackColor) ? Color.FromArgb(20, 20, 20) : MapFore(control.ForeColor);
            }
        }

        private static void Leaf(Control control)
        {
            if (control.BackColor.A == 255 || Under(control).HasValue) return;
            leaves.Add(new WeakReference(control));
            DoubleBuffer(control);
        }

        private static void Zone(Control control)
        {
            object marker;
            if (zoneSet.TryGetValue(control, out marker)) return;
            zoneSet.Add(control, new object());
            zones.Add(new WeakReference(control)); Ours(control);
            control.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (!Enabled) return;
                bool outline = InHost(control);
                if (!outline) UiBackdrop.Paint(control, e.Graphics, e.ClipRectangle);
                var layout = control as TableLayoutPanel;
                if (layout != null) foreach (var band in BandRects(layout)) DrawCard(e.Graphics, band.Key, null, band.Value, outline);
                if (IsWholeCard(control)) DrawCard(e.Graphics, Inset(control.ClientRectangle, 4), null, 0, outline);
            };
        }

        // Control tự vẽ nền nhà xưởng (màn hình chờ): vẽ lại mỗi nhịp, nút con lấy nền nhà xưởng.
        internal static void BackdropSurface(Control control)
        {
            object marker;
            if (!Enabled || zoneSet.TryGetValue(control, out marker)) return;
            zoneSet.Add(control, new object());
            zones.Add(new WeakReference(control));
        }

        private static Rectangle Inset(Rectangle r, int d) { r.Inflate(-d, -d); return r; }

        // Hình chữ nhật của từng card trong bảng bố cục + vị trí vạch dưới tiêu đề (0 = không có).
        internal static List<KeyValuePair<Rectangle, int>> BandRects(TableLayoutPanel layout)
        {
            var result = new List<KeyValuePair<Rectangle, int>>();
            List<int[]> list;
            if (!bands.TryGetValue(layout, out list)) return result;
            int[] heights = layout.GetRowHeights();
            int top = layout.Padding.Top;
            var starts = new int[heights.Length + 1];
            for (int i = 0; i < heights.Length; i++) { starts[i] = top; top += heights[i]; }
            starts[heights.Length] = top;
            foreach (int[] band in list)
            {
                if (band[0] >= heights.Length) continue;
                int from = starts[band[0]], to = starts[Math.Min(band[1] + 1, heights.Length)];
                if (to - from < 8) continue;
                // Khung lấn ra phần padding sẵn có của bảng bố cục (không ép nội dung); giữa hai khung chừa 6 px.
                int cardTop = band[0] == 0 ? from - Math.Min(8, layout.Padding.Top) : from + 3;
                int cardBottom = band[1] >= heights.Length - 1 ? to + Math.Min(8, layout.Padding.Bottom) : to - 3;
                var r = new Rectangle(layout.Padding.Left - Math.Min(6, layout.Padding.Left), cardTop, layout.ClientSize.Width - layout.Padding.Horizontal + 2 * Math.Min(6, layout.Padding.Left), cardBottom - cardTop);
                result.Add(new KeyValuePair<Rectangle, int>(r, band[2] != 0 ? starts[band[0] + 1] : 0));
            }
            return result;
        }

        internal static void DrawCard(Graphics g, Rectangle r, string title, int headerLine, bool outline = false)
        {
            if (r.Width <= 4 || r.Height <= 4) return;
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = outline ? SmoothingMode.None : SmoothingMode.AntiAlias;
            if (outline) using (GraphicsPath path = Round(r, 8)) using (var pen = new Pen(CardBorder)) g.DrawPath(pen, path);
            else using (GraphicsPath path = Round(r, 8))
            {
                using (var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) { g.TranslateTransform(0, 3); g.FillPath(shadow, path); g.TranslateTransform(0, -3); }
                using (var fill = new SolidBrush(Surface)) g.FillPath(fill, path);
                using (var pen = new Pen(CardBorder)) g.DrawPath(pen, path);
            }
            g.SmoothingMode = old;
            if (headerLine > r.Top && headerLine < r.Bottom) using (var pen = new Pen(CardBorder)) g.DrawLine(pen, r.Left + 1, headerLine, r.Right - 1, headerLine);
            if (title != null)
                TextRenderer.DrawText(g, title, UiFonts.Semi(9.5f), new Rectangle(r.Left + 14, r.Top + 1, r.Width - 28, 34), Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        internal static GraphicsPath Round(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 1) { path.AddRectangle(r); return path; }
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static readonly MethodInfo setStyle = typeof(Control).GetMethod("SetStyle", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static void DoubleBuffer(Control control)
        {
            try { if (setStyle != null) setStyle.Invoke(control, new object[] { ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true }); }
            catch (Exception) { }
        }

        private static void Watch(Control control)
        {
            object marker;
            if (hooked.TryGetValue(control, out marker)) return;
            hooked.Add(control, new object());
            control.ControlAdded += delegate(object sender, ControlEventArgs e) { if (Enabled) { MapFonts(e.Control); Walk(e.Control); } };
            EventHandler again = delegate { if (!busy && Enabled) Style(control); };
            control.BackColorChanged += again; control.ForeColorChanged += again;
            if (control is ButtonBase) control.EnabledChanged += delegate { control.Invalidate(); };
        }

        // ---- GroupBox = card có tiêu đề ----
        private static void StyleGroup(GroupBox box)
        {
            box.BackColor = InHost(box) ? Key : Surface; box.ForeColor = Text;
            object marker;
            if (hooked.TryGetValue(box, out marker)) return;
            Padding p = box.Padding;
            box.Padding = new Padding(Math.Max(p.Left, 8), Math.Max(p.Top, 22), Math.Max(p.Right, 8), Math.Max(p.Bottom, 8));
            Ours(box); Own(box);
            box.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (!Enabled) return;
                PaintUnder(box, e.Graphics, box.ClientRectangle);
                DrawCard(e.Graphics, new Rectangle(0, 0, box.Width - 1, box.Height - 4), (box.Text ?? "").Replace("&&", "&").ToUpperInvariant(), 36, InHost(box));
            };
        }

        // ---- nút ----
        private static readonly string[] primaryTexts = { "KẾT NỐI MODEL", "APPLY PROPERTIES", "XUẤT FILE", "CẬP NHẬT KHỔ TỪ SELECTION", "IMPORT TÊN MỚI", "CHẠY", "GHI MÃ VÀO PART", "GHI NAME", "ÁP DỤNG", "LƯU CẤU HÌNH" };
        private sealed class ButtonState { internal bool Hover, Down; internal Color Original; }
        private static readonly ConditionalWeakTable<ButtonBase, ButtonState> buttonStates = new ConditionalWeakTable<ButtonBase, ButtonState>();

        internal static bool IsPrimary(Button button)
        {
            string text = (button.Text ?? "").Trim().ToUpperInvariant();
            foreach (string p in primaryTexts) if (text == p || text.StartsWith(p + " ") || text.StartsWith(p + "(")) return true;
            return text.StartsWith("GHI ");
        }

        private static void StyleButton(Button button)
        {
            ButtonState state;
            if (!buttonStates.TryGetValue(button, out state))
            {
                state = new ButtonState { Original = button.BackColor };
                buttonStates.Add(button, state);
                button.MouseEnter += delegate { state.Hover = true; button.Invalidate(); };
                button.MouseLeave += delegate { state.Hover = false; state.Down = false; button.Invalidate(); };
                button.MouseDown += delegate { state.Down = true; button.Invalidate(); };
                button.MouseUp += delegate { state.Down = false; button.Invalidate(); };
                button.TextChanged += delegate { button.Invalidate(); };
                Ours(button); button.Paint += delegate(object sender, PaintEventArgs e) { if (Enabled) PaintButton(button, state, e.Graphics); };
            }
            else if (!busy) state.Original = button.BackColor;
            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            bool inherited = button.Parent != null && state.Original.ToArgb() == button.Parent.BackColor.ToArgb();
            button.BackColor = inherited || IsLight(state.Original) || state.Original.A < 255 ? Field : MapBack(state.Original, Field);
            if (Chroma(state.Original) > 0.25f && state.Original.GetBrightness() < 0.6f) button.BackColor = Field; // màu nhấn của code: vẽ kiểu "đang bật"
            button.ForeColor = Text;
        }

        private static void PaintButton(Button b, ButtonState s, Graphics g)
        {
            Rectangle r = new Rectangle(0, 0, b.Width - 1, b.Height - 1);
            PaintUnder(b, g, b.ClientRectangle);
            string text = (b.Text ?? "").Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&");
            string upper = text.Trim().ToUpperInvariant();
            bool primary = IsPrimary(b), on = !primary && Chroma(s.Original) > 0.25f && s.Original.GetBrightness() < 0.6f;
            bool danger = upper.StartsWith("HỦY") || upper.StartsWith("XÓA") || upper.StartsWith("DELETE") || upper.StartsWith("DỪNG");
            Color fill = primary ? (s.Down ? Color.FromArgb(48, 124, 210) : s.Hover ? AccentHover : Accent) : on ? OnFill : s.Down ? Color.FromArgb(61, 66, 75) : s.Hover ? Color.FromArgb(53, 57, 65) : Field;
            Color border = primary ? fill : on ? OnBorder : Border;
            Color fore = primary ? Color.White : on ? Color.FromArgb(207, 230, 255) : danger ? Danger : Text;
            if (!b.Enabled) { fill = Color.FromArgb(38, 40, 44); border = Color.FromArgb(51, 54, 61); fore = Color.FromArgb(98, 104, 114); }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Round(r, 6))
            {
                using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                using (var pen = new Pen(border)) g.DrawPath(pen, path);
            }
            g.SmoothingMode = SmoothingMode.None;
            Rectangle area = new Rectangle(4, 0, b.Width - 8, b.Height);
            if (b.Image != null)
            {
                int y = (b.Height - b.Image.Height) / 2;
                g.DrawImage(b.Image, 8, y, b.Image.Width, b.Image.Height);
                area = new Rectangle(12 + b.Image.Width, 0, b.Width - 16 - b.Image.Width, b.Height);
            }
            TextRenderer.DrawText(g, text, b.Font, area, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        // ---- ô tích / ô chọn một ----
        private static void StyleCheck(ButtonBase box)
        {
            box.BackColor = MapBack(box.BackColor, Color.Transparent);
            box.ForeColor = Text;
            ButtonState state;
            if (buttonStates.TryGetValue(box, out state)) return;
            state = new ButtonState();
            buttonStates.Add(box, state);
            var check = box as CheckBox; var radio = box as RadioButton;
            if (check != null && check.Appearance == Appearance.Button || radio != null && radio.Appearance == Appearance.Button) return;
            Ours(box);
            box.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (!Enabled) return;
                Graphics g = e.Graphics;
                Color? hit = HitFill(box);
                if (hit.HasValue) using (var fill = new SolidBrush(hit.Value)) g.FillRectangle(fill, box.ClientRectangle); // cả ô bấm được, không có điểm màu khoá
                else PaintUnder(box, g, box.ClientRectangle);
                bool on = check != null ? check.CheckState != CheckState.Unchecked : radio.Checked;
                bool mixed = check != null && check.CheckState == CheckState.Indeterminate;
                int size = 13, y = Math.Max(0, (box.Height - size) / 2);
                if (box.TextAlign == ContentAlignment.TopLeft || box.Height > 30) y = Math.Max(0, (box.Font.Height - size) / 2 + 1);
                var r = new Rectangle(1, y, size, size);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (radio != null)
                {
                    using (var fill = new SolidBrush(FieldInput)) g.FillEllipse(fill, r);
                    using (var pen = new Pen(on ? Accent : Color.FromArgb(88, 96, 112), 1.5f)) g.DrawEllipse(pen, r);
                    if (on) using (var dot = new SolidBrush(Accent)) g.FillEllipse(dot, r.X + 3.5f, r.Y + 3.5f, size - 7, size - 7);
                }
                else
                {
                    using (GraphicsPath path = Round(r, 3))
                    {
                        using (var fill = new SolidBrush(on ? Accent : FieldInput)) g.FillPath(fill, path);
                        using (var pen = new Pen(on ? Accent : Color.FromArgb(88, 96, 112), 1.5f)) g.DrawPath(pen, path);
                    }
                    if (mixed) using (var bar = new SolidBrush(Color.White)) g.FillRectangle(bar, r.X + 3, r.Y + 6, size - 6, 2);
                    else if (on) using (var pen = new Pen(Color.White, 2f)) g.DrawLines(pen, new[] { new PointF(r.X + 3.2f, r.Y + 7.2f), new PointF(r.X + 6f, r.Y + 10f), new PointF(r.X + 10.8f, r.Y + 4.2f) });
                }
                g.SmoothingMode = SmoothingMode.None;
                Color fore = box.Enabled ? (box.ForeColor.GetBrightness() < 0.5f ? Text : box.ForeColor) : Color.FromArgb(98, 104, 114);
                // 1.7.102: chữ đúng chỗ Windows chừa (sau ô 13 px + 3 px), một dòng: không rớt dòng bị cắt (SELECTED PARTS, SELECT ALL…).
                TextRenderer.DrawText(g, box.Text, box.Font, new Rectangle(16, 0, Math.Max(1, box.Width - 16), box.Height), fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            box.EnabledChanged += delegate { box.Invalidate(); };
            if (box.AutoSize) { Padding p = box.Padding; box.Padding = new Padding(p.Left, p.Top, p.Right + 4, p.Bottom); }
            Leaf(box);
        }

        // ---- ô chọn ----
        private static void StyleCombo(ComboBox box)
        {
            box.FlatStyle = FlatStyle.Flat;
            box.BackColor = box.DropDownStyle == ComboBoxStyle.DropDownList ? Field : FieldInput;
            box.ForeColor = Text;
            if (TeklaConnectionHost.IsWindows) ComboSkin.Attach(box); // 1.7.113: cả ô combo gõ được (nền tối khi bị khoá)
        }

        private sealed class ComboSkin : NativeWindow
        {
            private readonly ComboBox box;
            private static readonly ConditionalWeakTable<ComboBox, ComboSkin> all = new ConditionalWeakTable<ComboBox, ComboSkin>();
            private ComboSkin(ComboBox box) { this.box = box; }
            internal static void Attach(ComboBox box)
            {
                ComboSkin skin;
                if (all.TryGetValue(box, out skin)) return;
                skin = new ComboSkin(box); all.Add(box, skin);
                if (box.IsHandleCreated) skin.AssignHandle(box.Handle);
                box.HandleCreated += delegate { skin.ReleaseHandle(); skin.AssignHandle(box.Handle); };
                box.HandleDestroyed += delegate { skin.ReleaseHandle(); };
            }
            protected override void WndProc(ref Message m)
            {
                // 1.7.113: ô gõ của combo bị khoá xin màu qua WM_CTLCOLORSTATIC → trả nền tối, chữ mờ (thay xám sáng của Windows).
                if (m.Msg == 0x0138 && Enabled && !box.Enabled && box.DropDownStyle != ComboBoxStyle.DropDownList)
                {
                    SetTextColor(m.WParam, ColorTranslator.ToWin32(Muted)); SetBkColor(m.WParam, ColorTranslator.ToWin32(DisabledField));
                    m.Result = DisabledBrush(); return;
                }
                base.WndProc(ref m);
                if (m.Msg != 0x000F || !Enabled || box.DropDownStyle != ComboBoxStyle.DropDownList) return; // WM_PAINT
                try { using (Graphics g = Graphics.FromHwnd(box.Handle)) Draw(g); } catch (Exception) { }
            }
            private void Draw(Graphics g)
            {
                Rectangle r = new Rectangle(0, 0, box.Width - 1, box.Height - 1);
                using (var under = new SolidBrush(Under(box) ?? Bg)) g.FillRectangle(under, box.ClientRectangle);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = Round(r, 6))
                {
                    using (var fill = new SolidBrush(box.Enabled ? Field : Color.FromArgb(38, 40, 44))) g.FillPath(fill, path);
                    using (var pen = new Pen(box.Focused ? Color.FromArgb(47, 95, 149) : Border)) g.DrawPath(pen, path);
                }
                int cx = box.Width - 14, cy = box.Height / 2 - 1;
                using (var pen = new Pen(Color.FromArgb(154, 164, 178), 1.6f)) g.DrawLines(pen, new[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
                g.SmoothingMode = SmoothingMode.None;
                TextRenderer.DrawText(g, box.Text, box.Font, new Rectangle(8, 0, box.Width - 30, box.Height), box.Enabled ? Text : Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        // 1.7.113: thanh tiến độ tối (tắt kiểu Windows của riêng control để màu nền/thanh có hiệu lực).
        private static void StyleProgress(ProgressBar bar)
        {
            Action dark = delegate { try { if (TeklaConnectionHost.IsWindows && bar.IsHandleCreated) SetWindowTheme(bar.Handle, "", ""); bar.BackColor = Field; bar.ForeColor = Accent; } catch (Exception) { } };
            dark(); bar.HandleCreated += delegate { dark(); };
        }

        // ---- bảng ----
        private static void StyleGrid(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            bool key = InHost(grid);
            grid.BackgroundColor = key ? Key : Surface; grid.GridColor = RowLine;
            grid.BorderStyle = BorderStyle.None;
            if (grid.CellBorderStyle == DataGridViewCellBorderStyle.Single || grid.CellBorderStyle == DataGridViewCellBorderStyle.Raised || grid.CellBorderStyle == DataGridViewCellBorderStyle.Sunken)
                grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.DefaultCellStyle.BackColor = MapBack(grid.DefaultCellStyle.BackColor.IsEmpty ? Color.White : grid.DefaultCellStyle.BackColor, Surface);
            grid.DefaultCellStyle.ForeColor = MapFore(grid.DefaultCellStyle.ForeColor.IsEmpty ? Color.Black : grid.DefaultCellStyle.ForeColor);
            grid.DefaultCellStyle.SelectionBackColor = Selection; grid.DefaultCellStyle.SelectionForeColor = SelectionText;
            grid.DefaultCellStyle.Padding = new Padding(6, 0, 4, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = key ? HitGlassAlt : AltRow; // 1.7.111: dòng bảng đặc (bấm được) trên nền video
            if (key) grid.DefaultCellStyle.BackColor = HitGlass;
            foreach (DataGridViewCellStyle header in new[] { grid.ColumnHeadersDefaultCellStyle, grid.RowHeadersDefaultCellStyle })
            {
                header.BackColor = Header; header.ForeColor = HeaderText;
                header.SelectionBackColor = Header; header.SelectionForeColor = HeaderText;
                header.Padding = new Padding(6, 0, 4, 0);
                header.Font = UiFonts.Semi(8.25f);
            }
            if (grid.ColumnHeadersHeightSizeMode != DataGridViewColumnHeadersHeightSizeMode.AutoSize && grid.ColumnHeadersHeight < 30) grid.ColumnHeadersHeight = 30;
            if (grid.RowTemplate.Height < 28) grid.RowTemplate.Height = 28;
            grid.ForeColor = Text; grid.BackColor = key ? Key : Surface;
            object marker;
            if (hooked.TryGetValue(grid, out marker)) return;
            grids.Add(new WeakReference(grid)); Ours(grid);
            grid.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs e)
            {
                if (!Enabled || e.CellStyle == null) return;
                if (IsLight(e.CellStyle.BackColor)) e.CellStyle.BackColor = MapBack(e.CellStyle.BackColor, Surface);
                e.CellStyle.ForeColor = MapFore(e.CellStyle.ForeColor);
                if (IsLight(e.CellStyle.SelectionBackColor)) e.CellStyle.SelectionBackColor = Selection;
                if (e.CellStyle.SelectionForeColor.GetBrightness() < 0.55f) e.CellStyle.SelectionForeColor = SelectionText;
                if (e.Value is int || e.Value is long || e.Value is double || e.Value is decimal || e.Value is float) e.CellStyle.Font = UiFonts.Mono(8.75f);
            };
            grid.CellPainting += delegate(object sender, DataGridViewCellPaintingEventArgs e) { if (Enabled) PaintCell(grid, e); };
            grid.CellMouseUp += delegate(object sender, DataGridViewCellMouseEventArgs e) { if (Enabled && e.Button == MouseButtons.Left) ToggleCheckCell(grid, e.ColumnIndex, e.RowIndex, e.Location); };
            grid.EditingControlShowing += delegate(object sender, DataGridViewEditingControlShowingEventArgs e)
            {
                if (!Enabled || e.Control == null) return;
                e.Control.BackColor = FieldInput; e.Control.ForeColor = Text;
            };
            grid.Paint += delegate(object sender, PaintEventArgs e) { if (Enabled) PaintGridEmpty(grid, e.Graphics); };
            grid.ControlAdded += delegate(object sender, ControlEventArgs e) { if (e.Control is ScrollBar) DarkScroll(e.Control); };
            foreach (Control child in grid.Controls) if (child is ScrollBar) DarkScroll(child);
        }

        internal static Rectangle GridEmpty(DataGridView grid)
        {
            int bottom = grid.ColumnHeadersVisible ? grid.ColumnHeadersHeight : 0;
            int last = grid.Rows.GetLastRow(DataGridViewElementStates.Displayed);
            if (last >= 0) { Rectangle row = grid.GetRowDisplayRectangle(last, true); bottom = Math.Max(bottom, row.Bottom); }
            int width = grid.ClientSize.Width, height = grid.ClientSize.Height;
            foreach (Control child in grid.Controls)
                if (child.Visible && child is VScrollBar) width -= child.Width; else if (child.Visible && child is HScrollBar) height -= child.Height;
            return new Rectangle(0, bottom + 1, Math.Max(0, width), Math.Max(0, height - bottom - 1));
        }

        private static void PaintGridEmpty(DataGridView grid, Graphics g)
        {
            if (InHost(grid)) return;
            Rectangle empty = GridEmpty(grid);
            if (empty.Height < 12 || empty.Width < 12) return;
            UiBackdrop.Paint(grid, g, empty);
            using (var veil = new SolidBrush(Color.FromArgb(185, Surface))) g.FillRectangle(veil, empty);
        }

        private static bool IsStatusColumn(DataGridViewColumn column)
        {
            if (column == null) return false;
            string h = (column.HeaderText ?? "").Trim().ToUpperInvariant(), n = (column.Name ?? "").ToUpperInvariant();
            return h == "TRẠNG THÁI" || h == "KẾT QUẢ" || h == "STATUS" || n == "STATUS";
        }

        internal static int Tone(string text)
        {
            string t = (text ?? "").ToLowerInvariant();
            if (t.Length == 0 || t == "—" || t == "-") return 0;
            foreach (string k in new[] { "lỗi", "trùng", "từ chối", "sai", "chặn", "fail", "không đọc", "không được", "error" }) if (t.Contains(k)) return 4;
            foreach (string k in new[] { "thiếu", "cảnh báo", "issue", "cần ", "lệch", "bỏ qua", "warning" }) if (t.Contains(k)) return 3;
            foreach (string k in new[] { "đã đúng", "khớp", "đã gán", "đã ghi", "xong", " ok", "ok", "giữ nguyên", "không đổi" }) if (t.Contains(k)) return 1;
            return 2;
        }

        private static void PaintCell(DataGridView grid, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Handled) return;
            DataGridViewColumn column = grid.Columns[e.ColumnIndex];
            if (e.CellStyle == null) return;
            if (column is DataGridViewCheckBoxColumn)
            {
                e.PaintBackground(e.CellBounds, true);
                object v = e.FormattedValue;
                bool on = v is bool ? (bool)v : v is CheckState ? (CheckState)v != CheckState.Unchecked : false;
                var r = CheckGlyph(grid, e.ColumnIndex, e.RowIndex, e.CellBounds); // 1.7.112: đúng chỗ ô tích thật (vùng Windows nhận click)
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = Round(r, 3))
                {
                    using (var fill = new SolidBrush(on ? Accent : FieldInput)) e.Graphics.FillPath(fill, path);
                    using (var pen = new Pen(on ? Accent : Color.FromArgb(88, 96, 112), 1.5f)) e.Graphics.DrawPath(pen, path);
                }
                if (on) using (var pen = new Pen(Color.White, 2f)) e.Graphics.DrawLines(pen, new[] { new PointF(r.X + 3.2f, r.Y + 7.2f), new PointF(r.X + 6f, r.Y + 10f), new PointF(r.X + 10.8f, r.Y + 4.2f) });
                e.Graphics.SmoothingMode = SmoothingMode.None;
                e.Handled = true;
                return;
            }
            if (!IsStatusColumn(column)) return;
            string text = Convert.ToString(e.FormattedValue);
            int tone = Tone(text);
            if (tone == 0) return;
            e.PaintBackground(e.CellBounds, true);
            Color back = tone == 1 ? OkBack : tone == 3 ? WarnBack : tone == 4 ? ErrBack : InfoBack, fore = tone == 1 ? OkText : tone == 3 ? WarnText : tone == 4 ? ErrText : InfoText;
            Font font = UiFonts.Semi(8f);
            Size size = TextRenderer.MeasureText(e.Graphics, text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int w = Math.Min(size.Width + 16, e.CellBounds.Width - 10), h = Math.Min(20, e.CellBounds.Height - 6);
            var pill = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + (e.CellBounds.Height - h) / 2, Math.Max(10, w), h);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Round(pill, h / 2)) using (var brush = new SolidBrush(back)) e.Graphics.FillPath(brush, path);
            e.Graphics.SmoothingMode = SmoothingMode.None;
            TextRenderer.DrawText(e.Graphics, text, font, pill, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            e.Handled = true;
        }

        // 1.7.112: ô tích vẽ ĐÚNG vùng nội dung thật của ô (Windows đổi tích khi bấm trong vùng này). Trước đây vẽ lệch trái
        // 10 điểm ảnh nên bấm vào ô nhìn thấy thường trượt vùng thật: dòng được chọn nhưng tích không đổi.
        internal static Rectangle CheckGlyph(DataGridView grid, int column, int row, Rectangle cell)
        {
            Rectangle content = Rectangle.Empty;
            try { content = grid.Rows[row].Cells[column].GetContentBounds(row); } catch (Exception) { }
            int cx = content.Width > 0 ? cell.X + content.X + content.Width / 2 : cell.X + cell.Width / 2;
            int cy = content.Height > 0 ? cell.Y + content.Y + content.Height / 2 : cell.Y + cell.Height / 2;
            return new Rectangle(cx - 7, cy - 7, 14, 14);
        }

        // 1.7.112: bấm chuột trái ở BẤT KỲ chỗ nào trong ô tích (không chỉ ô vuông nhỏ) cũng đổi tích; trong vùng thật thì để
        // DataGridView tự đổi (không đổi hai lần). Ô / cột / bảng chỉ đọc thì không đổi.
        internal static bool ToggleCheckCell(DataGridView grid, int column, int row, Point inCell)
        {
            if (row < 0 || column < 0 || row >= grid.Rows.Count || column >= grid.Columns.Count) return false;
            if (!(grid.Columns[column] is DataGridViewCheckBoxColumn) || grid.ReadOnly) return false;
            DataGridViewCell cell = grid.Rows[row].Cells[column];
            if (cell.ReadOnly || cell.GetContentBounds(row).Contains(inCell)) return false;
            object value = cell.Value;
            bool on = value is bool ? (bool)value : value is CheckState ? (CheckState)value != CheckState.Unchecked : false;
            if (grid.IsCurrentCellInEditMode) grid.EndEdit();
            cell.Value = value is CheckState ? (object)(on ? CheckState.Unchecked : CheckState.Checked) : (object)!on;
            return true;
        }

        // ---- hàng tab ----
        private static void StyleTabs(TabControl tabs)
        {
            object marker;
            if (tabs.DrawMode != TabDrawMode.Normal || hooked.TryGetValue(tabs, out marker)) return;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            // 1.7.113: đổi tab: dừng vẽ cả TabControl tới khi trang mới bố cục xong, rồi vẽ một lần.
            bool frozen = false;
            tabs.Selecting += delegate { if (TeklaConnectionHost.IsWindows && tabs.IsHandleCreated && !frozen) { frozen = true; SendMessage(tabs.Handle, 0x000B, IntPtr.Zero, IntPtr.Zero); } };
            tabs.Selected += delegate { if (frozen) { frozen = false; SendMessage(tabs.Handle, 0x000B, new IntPtr(1), IntPtr.Zero); tabs.Invalidate(true); } };
            tabs.Padding = new Point(16, 8);
            tabs.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= tabs.TabPages.Count) return;
                DrawTab(tabs, e.Graphics, e.Index, e.Bounds);
            };
            if (TeklaConnectionHost.IsWindows) TabSkin.Attach(tabs);
        }

        internal static void DrawTab(TabControl tabs, Graphics g, int index, Rectangle area)
        {
            bool selected = index == tabs.SelectedIndex;
            using (var back = new SolidBrush(selected ? Surface : TabStrip)) g.FillRectangle(back, area);
            if (selected) using (var bar = new SolidBrush(AccentBar)) g.FillRectangle(bar, area.Left, area.Bottom - 2, area.Width, 2);
            TextRenderer.DrawText(g, tabs.TabPages[index].Text, tabs.Font, area, selected ? Text : Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        // Windows: vẽ đè cả dải tab (kể cả phần trống bên phải) và viền khung trang sau khi Windows vẽ xong.
        private sealed class TabSkin : NativeWindow
        {
            private readonly TabControl tabs;
            private static readonly ConditionalWeakTable<TabControl, TabSkin> all = new ConditionalWeakTable<TabControl, TabSkin>();
            private TabSkin(TabControl tabs) { this.tabs = tabs; }
            internal static void Attach(TabControl tabs)
            {
                TabSkin skin;
                if (all.TryGetValue(tabs, out skin)) return;
                skin = new TabSkin(tabs); all.Add(tabs, skin);
                if (tabs.IsHandleCreated) skin.AssignHandle(tabs.Handle);
                tabs.HandleCreated += delegate { skin.ReleaseHandle(); skin.AssignHandle(tabs.Handle); };
                tabs.HandleDestroyed += delegate { skin.ReleaseHandle(); };
            }
            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg != 0x000F || !Enabled || tabs.TabCount == 0) return;
                try
                {
                    using (Graphics g = Graphics.FromHwnd(tabs.Handle))
                    {
                        Rectangle page = tabs.DisplayRectangle;
                        int strip = Math.Max(0, page.Top - 3);
                        using (var back = new SolidBrush(TabStrip)) g.FillRectangle(back, 0, 0, tabs.Width, strip);
                        using (var fill = new SolidBrush(InHost(tabs) ? Key : Bg))
                        {
                            g.FillRectangle(fill, 0, strip, tabs.Width, page.Top - strip);
                            g.FillRectangle(fill, 0, page.Top, page.Left, tabs.Height - page.Top);
                            g.FillRectangle(fill, page.Right, page.Top, tabs.Width - page.Right, tabs.Height - page.Top);
                            g.FillRectangle(fill, 0, page.Bottom, tabs.Width, tabs.Height - page.Bottom);
                        }
                        using (var pen = new Pen(CardBorder)) g.DrawLine(pen, 0, strip - 1, tabs.Width, strip - 1);
                        for (int i = 0; i < tabs.TabCount; i++)
                        {
                            Rectangle r = tabs.GetTabRect(i);
                            DrawTab(tabs, g, i, new Rectangle(r.Left, 0, r.Width, strip - 1));
                        }
                    }
                }
                catch (Exception) { }
            }
        }

        // ---- menu, thanh trạng thái ----
        internal static void StyleStrip(ToolStrip strip)
        {
            if (!Enabled || strip == null) return;
            strip.Renderer = new ToolStripProfessionalRenderer(new DarkColors()) { RoundedEdges = false };
            strip.BackColor = strip is StatusStrip ? Toolbar : Surface; strip.ForeColor = strip is StatusStrip ? Muted : Text;
            foreach (ToolStripItem item in strip.Items) StyleItem(item, strip is StatusStrip);
            object marker;
            if (hooked.TryGetValue(strip, out marker)) return;
            hooked.Add(strip, new object());
            strip.ItemAdded += delegate(object sender, ToolStripItemEventArgs e) { if (Enabled) StyleItem(e.Item, strip is StatusStrip); };
        }

        private static void StyleItem(ToolStripItem item, bool status)
        {
            item.ForeColor = status ? Muted : MapFore(item.ForeColor);
            var menu = item as ToolStripDropDownItem;
            if (menu != null && menu.DropDown != null) StyleStrip(menu.DropDown);
        }

        private sealed class DarkColors : ProfessionalColorTable
        {
            internal DarkColors() { UseSystemColors = false; }
            public override Color ToolStripGradientBegin { get { return Surface; } }
            public override Color ToolStripGradientMiddle { get { return Surface; } }
            public override Color ToolStripGradientEnd { get { return Surface; } }
            public override Color ToolStripBorder { get { return CardBorder; } }
            public override Color ToolStripDropDownBackground { get { return Surface; } }
            public override Color StatusStripGradientBegin { get { return Toolbar; } }
            public override Color StatusStripGradientEnd { get { return Toolbar; } }
            public override Color MenuStripGradientBegin { get { return Surface; } }
            public override Color MenuStripGradientEnd { get { return Surface; } }
            public override Color MenuBorder { get { return Border; } }
            public override Color MenuItemBorder { get { return Accent; } }
            public override Color MenuItemSelected { get { return Selection; } }
            public override Color MenuItemSelectedGradientBegin { get { return Selection; } }
            public override Color MenuItemSelectedGradientEnd { get { return Selection; } }
            public override Color MenuItemPressedGradientBegin { get { return Header; } }
            public override Color MenuItemPressedGradientEnd { get { return Header; } }
            public override Color ImageMarginGradientBegin { get { return Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Surface; } }
            public override Color ImageMarginGradientEnd { get { return Surface; } }
            public override Color SeparatorDark { get { return Border; } }
            public override Color SeparatorLight { get { return Border; } }
            public override Color ButtonSelectedHighlight { get { return Selection; } }
            public override Color ButtonSelectedGradientBegin { get { return Selection; } }
            public override Color ButtonSelectedGradientEnd { get { return Selection; } }
            public override Color ButtonSelectedBorder { get { return Accent; } }
        }

        // ---- Windows: thanh tiêu đề và thanh cuộn tối ----
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("gdi32.dll")] private static extern int SetTextColor(IntPtr hdc, int color);
        [DllImport("gdi32.dll")] private static extern int SetBkColor(IntPtr hdc, int color);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(int color);
        internal static readonly Color DisabledField = Color.FromArgb(38, 40, 44);
        private static IntPtr disabledBrush;
        private static IntPtr DisabledBrush() { if (disabledBrush == IntPtr.Zero) disabledBrush = CreateSolidBrush(ColorTranslator.ToWin32(DisabledField)); return disabledBrush; }
        private static void DarkTitle(Form form)
        {
            if (!TeklaConnectionHost.IsWindows) return;
            EventHandler set = delegate
            {
                try { int on = 1; if (DwmSetWindowAttribute(form.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(form.Handle, 19, ref on, 4); }
                catch (Exception) { }
            };
            if (form.IsHandleCreated) set(form, EventArgs.Empty); else form.HandleCreated += set;
        }

        internal static void DarkScroll(Control control)
        {
            if (!TeklaConnectionHost.IsWindows) return;
            EventHandler set = delegate { try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); } catch (Exception) { } };
            if (control.IsHandleCreated) set(control, EventArgs.Empty);
            control.HandleCreated += set;
        }

        // ---- thanh trái (mục menu tự vẽ) ----
        internal static void MarkNav(Label item)
        {
            object marker;
            if (!hooked.TryGetValue(item, out marker)) hooked.Add(item, new object());
            Ours(item);
            item.Paint += delegate(object sender, PaintEventArgs e)
            {
                bool on = item.BackColor.ToArgb() == NavActive.ToArgb();
                if (!Enabled) return;
                using (var back = new SolidBrush(on ? NavDark : Nav)) e.Graphics.FillRectangle(back, item.ClientRectangle);
                if (on) using (var bar = new SolidBrush(AccentBar)) e.Graphics.FillRectangle(bar, 0, 0, 3, item.Height);
                TextRenderer.DrawText(e.Graphics, item.Text, UiFonts.Semi(9f), new Rectangle(18, 0, item.Width - 20, item.Height), on ? Color.White : NavText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
        }

        // ---- nhịp nhà xưởng xoay: vẽ lại vùng nền ----
        internal static void RefreshBackdrop()
        {
            Refresh(zones, delegate(Control c) { c.Invalidate(false); });
            Refresh(leaves, delegate(Control c) { c.Invalidate(); });
            Refresh(grids, delegate(Control c) { var grid = (DataGridView)c; Rectangle r = GridEmpty(grid); if (r.Height >= 12) grid.Invalidate(r); });
        }

        private static void Refresh(List<WeakReference> list, Action<Control> act)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var c = list[i].Target as Control;
                if (c == null || c.IsDisposed) { list.RemoveAt(i); continue; }
                if (!c.IsHandleCreated || !c.Visible) continue;
                try { act(c); } catch (Exception) { }
            }
        }
    }

    // Font Inter đi kèm app (App\Fonts, SIL OFL): nạp riêng cho tiến trình (GDI+ PrivateFontCollection, GDI AddFontResourceEx
    // FR_PRIVATE), không cài vào Windows, không cần quyền admin. Thiếu file thì dùng Segoe UI.
    internal static class UiFonts
    {
        private static bool loaded;
        private static FontFamily regular, semibold;
        private static readonly PrivateFontCollection collection = new PrivateFontCollection();
        private static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern int AddFontResourceEx(string file, uint flags, IntPtr reserved);

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            if (TeklaConnectionHost.IsWindows) try
            {
                string folder = TeklaConnectionHost.SourceFile("Fonts");
                foreach (string name in new[] { "Inter-Regular.ttf", "Inter-SemiBold.ttf", "Inter-Bold.ttf" })
                {
                    string path = Path.Combine(folder, name);
                    if (!File.Exists(path)) continue;
                    collection.AddFontFile(path);
                    if (TeklaConnectionHost.IsWindows) try { AddFontResourceEx(path, 0x10, IntPtr.Zero); } catch (Exception) { }
                }
                foreach (FontFamily family in collection.Families)
                {
                    if (family.Name == "Inter") regular = family;
                    else if (family.Name == "Inter SemiBold") semibold = family;
                }
            }
            catch (Exception) { regular = semibold = null; }
            // libgdiplus (Mono, máy test Linux) không đo được Inter: ở đó dùng font mặc định.
            if (regular == null) try { regular = new FontFamily("Inter"); } catch (Exception) { }
            if (semibold == null) try { semibold = new FontFamily("Inter SemiBold"); } catch (Exception) { }
        }

        internal static bool Available { get { Load(); return regular != null; } }

        internal static bool Replaceable(Font font)
        {
            string n = font.FontFamily.Name;
            return n == "Segoe UI" || n == "Microsoft Sans Serif" || n == "Tahoma" || n == "Segoe UI Semibold" || n == "Arial" && font.Size < 12;
        }

        private static Font Make(string key, Func<Font> create)
        {
            Font font;
            if (cache.TryGetValue(key, out font)) return font;
            try { font = create(); } catch (Exception) { font = new Font("Segoe UI", 9f); }
            cache[key] = font;
            return font;
        }

        internal static Font Body(float size, FontStyle style = FontStyle.Regular)
        {
            Load();
            size = (float)Math.Round(size * 4) / 4f;
            return Make("b" + size + style, () => regular != null ? new Font(regular, size, style) : new Font("Segoe UI", size, style));
        }

        internal static Font Semi(float size)
        {
            Load();
            size = (float)Math.Round(size * 4) / 4f;
            return Make("s" + size, () => semibold != null ? new Font(semibold, size) : regular != null ? new Font(regular, size, FontStyle.Bold) : new Font("Segoe UI Semibold", size));
        }

        internal static Font Mono(float size)
        {
            return Make("m" + size, () => new Font(TeklaConnectionHost.IsWindows ? "Consolas" : "DejaVu Sans Mono", size));
        }
    }
}
