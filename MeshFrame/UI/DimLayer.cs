// Lớp vẽ + chọn + sửa số đo trên hình (dùng chung cho hình 2D và 3D): điểm P#, độ dài cạnh, chuỗi kích thước gia cường.
// Bấm số = chọn; bấm đúp (hoặc F2 / Enter) = gõ số mới; bấm nhãn điểm P# = chọn làm điểm gốc.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using MeshFrame.Core;

namespace MeshFrame.UI
{
    public delegate void DimEditedHandler(DimItem item, double value);

    public sealed class DimLayer
    {
        private readonly Control _host;
        private List<DimItem> _items = new List<DimItem>();
        private readonly List<KeyValuePair<Rectangle, DimItem>> _hits = new List<KeyValuePair<Rectangle, DimItem>>();
        private DimItem _selected;
        private TextBox _editor;
        private DimItem _editing;

        public int Anchor;                    // chỉ số điểm gốc
        public bool Visible = true;
        public Color Back = Color.FromArgb(27, 28, 32), TextColor = Color.FromArgb(200, 204, 212), Red = Color.FromArgb(240, 80, 70),
                     BaseColor = Color.FromArgb(230, 150, 40), AnchorColor = Color.FromArgb(74, 163, 255), PointColor = Color.FromArgb(214, 90, 90);
        public event DimEditedHandler Edited;
        public event Action<int> AnchorPicked;

        public DimLayer(Control host) { _host = host; }

        public List<DimItem> Items { get { return _items; } }

        public void SetItems(List<DimItem> items, int anchor)
        {
            _items = items ?? new List<DimItem>(); Anchor = anchor;
            if (_selected != null)
            {
                DimItem again = null;
                foreach (DimItem d in _items) if (d.Kind == _selected.Kind && d.Index == _selected.Index) again = d;
                _selected = again;
            }
            CancelEdit();
        }

        public void Draw(Graphics g, Func<V2, PointF> map)
        {
            _hits.Clear();
            if (!Visible) return;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var fb = new Font(_host.Font.FontFamily, 9f, FontStyle.Bold))
            using (var fs = new Font(_host.Font.FontFamily, 8.5f))
            using (var redPen = new Pen(Red, 1.6f))
            using (var extPen = new Pen(Color.FromArgb(150, Red), 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
            {
                foreach (DimItem d in _items)
                {
                    if (d.Kind != DimKind.Cell) continue;
                    PointF a = map(d.A), b = map(d.B);
                    if (d.Extension != null)
                        for (int i = 0; i + 1 < d.Extension.Length; i += 2) g.DrawLine(extPen, map(d.Extension[i]), map(d.Extension[i + 1]));
                    g.DrawLine(redPen, a, b);
                    Arrow(g, redPen, a, b); Arrow(g, redPen, b, a);
                    Label(g, fb, d, map(d.TextPos), Red, true);
                }
                foreach (DimItem d in _items)
                    if (d.Kind == DimKind.Edge) Label(g, fs, d, map(d.TextPos), d.IsBaseEdge ? BaseColor : TextColor, false);
                foreach (DimItem d in _items)
                {
                    if (d.Kind != DimKind.Point) continue;
                    PointF c = map(d.A);
                    bool anchor = d.Index == Anchor;
                    using (var br = new SolidBrush(anchor ? AnchorColor : PointColor)) g.FillEllipse(br, c.X - 4, c.Y - 4, 8, 8);
                    if (anchor) using (var pen = new Pen(AnchorColor, 2f)) g.DrawEllipse(pen, c.X - 8, c.Y - 8, 16, 16);
                    string t = d.Text + (anchor ? " (gốc)" : "");
                    Label(g, fb, d, new PointF(c.X + 8, c.Y - 14), anchor ? AnchorColor : PointColor, false, true);
                }
            }
        }

        private static void Arrow(Graphics g, Pen pen, PointF from, PointF to)
        {
            double dx = to.X - from.X, dy = to.Y - from.Y, l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 14) return;
            double ux = dx / l, uy = dy / l;
            var tip = from; var p1 = new PointF((float)(from.X + ux * 9 - uy * 3.5), (float)(from.Y + uy * 9 + ux * 3.5)); var p2 = new PointF((float)(from.X + ux * 9 + uy * 3.5), (float)(from.Y + uy * 9 - ux * 3.5));
            using (var br = new SolidBrush(pen.Color)) g.FillPolygon(br, new[] { tip, p1, p2 });
            g.DrawLine(pen, from.X - (float)(uy * 6), from.Y + (float)(ux * 6), from.X + (float)(uy * 6), from.Y - (float)(ux * 6));
        }

        private void Label(Graphics g, Font f, DimItem d, PointF pos, Color col, bool center, bool leftTop = false)
        {
            string t = d.Kind == DimKind.Point && d.Index == Anchor ? d.Text + " (gốc)" : d.Text;
            SizeF sz = g.MeasureString(t, f);
            float x = leftTop ? pos.X : pos.X - sz.Width / 2, y = leftTop ? pos.Y : pos.Y - sz.Height / 2;
            x = Math.Max(2, Math.Min(_host.Width - sz.Width - 2, x)); y = Math.Max(2, Math.Min(_host.Height - sz.Height - 2, y));
            var r = new Rectangle((int)x - 2, (int)y - 1, (int)sz.Width + 4, (int)sz.Height + 2);
            bool sel = _selected != null && d.Kind == _selected.Kind && d.Index == _selected.Index;
            using (var br = new SolidBrush(Color.FromArgb(sel ? 235 : 190, Back))) g.FillRectangle(br, r);
            if (sel || d.Editable)
                using (var pen = new Pen(sel ? AnchorColor : Color.FromArgb(70, col), sel ? 2f : 1f)) g.DrawRectangle(pen, r);
            using (var br = new SolidBrush(col)) g.DrawString(t, f, br, x, y);
            _hits.Add(new KeyValuePair<Rectangle, DimItem>(r, d));
        }

        // ---------------------------------------------------------------- chuột / phím
        public DimItem HitTest(Point p)
        {
            for (int i = _hits.Count - 1; i >= 0; i--) if (_hits[i].Key.Contains(p)) return _hits[i].Value;
            return null;
        }

        public Rectangle RectOf(DimItem d)
        {
            foreach (var kv in _hits) if (kv.Value == d) return kv.Key;
            return Rectangle.Empty;
        }

        /// <summary>Trả về true nếu bấm trúng một số đo (đã xử lý).</summary>
        public bool MouseDown(Point p)
        {
            DimItem d = Visible ? HitTest(p) : null;
            if (d == null) { if (_selected != null) { _selected = null; _host.Invalidate(); } return false; }
            if (d.Kind == DimKind.Point) { if (AnchorPicked != null) AnchorPicked(d.Index); return true; }
            _selected = d; _host.Invalidate();
            return true;
        }

        public bool DoubleClick(Point p)
        {
            DimItem d = Visible ? HitTest(p) : null;
            if (d == null || !d.Editable) return false;
            _selected = d; BeginEdit(d);
            return true;
        }

        public bool KeyDown(Keys k)
        {
            if (_selected != null && _editor == null && (k == Keys.F2 || k == Keys.Enter) && _selected.Editable) { BeginEdit(_selected); return true; }
            return false;
        }

        public void BeginEdit(DimItem d)
        {
            CancelEdit();
            Rectangle r = RectOf(d);
            if (r.IsEmpty) return;
            _editing = d;
            _editor = new TextBox { Text = d.Value.ToString("0.##", CultureInfo.InvariantCulture), Bounds = new Rectangle(r.X - 10, r.Y - 2, Math.Max(80, r.Width + 20), r.Height + 6), TextAlign = HorizontalAlignment.Center };
            _editor.BackColor = Color.FromArgb(44, 47, 53); _editor.ForeColor = Color.White; _editor.BorderStyle = BorderStyle.FixedSingle;
            _editor.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Commit(); }
                else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; CancelEdit(); }
            };
            _editor.LostFocus += delegate { if (_editor != null) CancelEdit(); };
            _host.Controls.Add(_editor);
            _editor.Focus(); _editor.SelectAll();
        }

        private void Commit()
        {
            if (_editor == null) return;
            double v; string txt = _editor.Text.Trim().Replace(',', '.');
            DimItem d = _editing;
            bool ok = double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
            CancelEdit();
            if (ok && d != null && Edited != null) Edited(d, v);
        }

        public void CancelEdit()
        {
            if (_editor == null) return;
            var e = _editor; _editor = null; _editing = null;
            if (_host.Controls.Contains(e)) _host.Controls.Remove(e);
            e.Dispose();
            _host.Focus();
        }
    }
}
