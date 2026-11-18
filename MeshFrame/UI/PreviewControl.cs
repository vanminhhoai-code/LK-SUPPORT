// Vẽ xem trước mặt bằng từ MeshFramePlan (chỉ dùng số trong plan, không tính lại hình học) trên ĐA GIÁC THẬT đã pick.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using MeshFrame.Core;

namespace MeshFrame.UI
{
    public class PreviewControl : Control
    {
        private MeshFramePlan _plan;
        private V2[] _poly;
        private double _legA;
        public readonly DimLayer Dims;
        private string _empty = "Chưa có điểm. Bấm \"Pick lại\" để pick đa giác trong Tekla.";

        public PreviewControl()
        {
            DoubleBuffered = true;
            Dims = new DimLayer(this);
            BackColor = Color.FromArgb(27, 28, 32);
        }

        /// <param name="plan">Kế hoạch (null nếu có lỗi: chỉ vẽ viền đa giác).</param>
        /// <param name="poly">Đa giác 2D (nếu plan != null thì lấy plan.Polygon).</param>
        public void Show(MeshFramePlan plan, V2[] poly, double frameLegA)
        {
            _plan = plan; _poly = plan != null && plan.Polygon != null ? plan.Polygon : poly; _legA = frameLegA;
            Invalidate();
        }

        public void SetDims(System.Collections.Generic.List<DimItem> items, int anchor) { Dims.SetItems(items, anchor); Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { Focus(); Dims.MouseDown(e.Location); } }
        protected override void OnDoubleClick(EventArgs e) { base.OnDoubleClick(e); Dims.DoubleClick(PointToClient(Cursor.Position)); }
        protected override bool IsInputKey(Keys k) { return k == Keys.F2 || k == Keys.Enter || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); Dims.KeyDown(e.KeyCode); }

        public void ShowEmpty(string text) { _plan = null; _poly = null; if (text != null) _empty = text; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (_poly == null || _poly.Length < 3)
            {
                using (var f = new Font("Segoe UI", 10f)) g.DrawString(_empty, f, Brushes.Silver, 20, 20);
                return;
            }

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (V2 p in _poly) { minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X); minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y); }
            foreach (DimItem d in Dims.Items) foreach (V2 p in new[] { d.A, d.B }) { minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X); minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y); }
            double bw = maxX - minX, bh = maxY - minY;
            if (bw < 1 || bh < 1) return;
            const int pad = 60;
            double k = Math.Min((Width - 2 * pad) / bw, (Height - 2 * pad) / bh);
            double ox = (Width - bw * k) / 2, oy = (Height - bh * k) / 2;
            System.Func<V2, PointF> T = p => new PointF((float)(ox + (p.X - minX) * k), (float)(Height - (oy + (p.Y - minY) * k)));
            System.Func<V2[], PointF[]> TA = a => { var r = new PointF[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = T(a[i]); return r; };

            if (_plan == null)
            {
                using (var pen = new Pen(Color.Firebrick, 1.5f) { DashStyle = DashStyle.Dash }) g.DrawPolygon(pen, TA(_poly));
                DrawLabels(g, T);
                return;
            }

            // lưới
            if (_plan.Mesh != null)
            {
                using (var br = new SolidBrush(Color.FromArgb(120, 135, 190, 230))) g.FillPolygon(br, TA(_plan.Mesh.Polygon));
                using (var hatch = new HatchBrush(HatchStyle.SmallGrid, Color.FromArgb(110, 70, 130, 180), Color.Transparent)) g.FillPolygon(hatch, TA(_plan.Mesh.Polygon));
            }

            // V khung: vòng giữa đa giác ngoài và đa giác dịch vào bằng bề rộng cánh nằm
            V2[] inner = Geo.Offset(_poly, _legA);
            using (var path = new GraphicsPath(FillMode.Alternate))
            {
                path.AddPolygon(TA(_poly)); path.AddPolygon(TA(inner));
                using (var br = new SolidBrush(Color.FromArgb(235, 230, 126, 34))) g.FillPath(br, path);
            }

            foreach (PlanBar b in _plan.Bars)
                if (b.Role == BarRole.Stiffener) FillBand(g, T, b.Start, b.End, 0, b.LegA, Color.FromArgb(235, 214, 100, 20));
            foreach (PlanStrip s in _plan.Strips)
                FillBand(g, T, s.Start, s.End, -s.Width / 2, s.Width / 2, Color.FromArgb(200, 39, 174, 96));

            using (var pen = new Pen(Color.FromArgb(150, 158, 170), 1.2f)) g.DrawPolygon(pen, TA(_poly));
            if (Dims.Items.Count > 0) { Dims.Back = BackColor; Dims.Draw(g, T); } else DrawLabels(g, T);
        }

        // Tên điểm P1..PN, số cạnh và chiều dài từng cạnh (cạnh gốc gia cường tô đậm)
        private void DrawLabels(Graphics g, System.Func<V2, PointF> T)
        {
            int n = _poly.Length;
            int baseEdge = _plan != null ? _plan.BaseEdge : -1;
            using (var fb = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var fs = new Font("Segoe UI", 8f))
            {
                for (int i = 0; i < n; i++)
                {
                    PointF c = T(_poly[i]);
                    g.FillEllipse(Brushes.Firebrick, c.X - 3, c.Y - 3, 6, 6);
                    g.DrawString("P" + (i + 1), fb, Brushes.Firebrick, c.X + 4, c.Y - 18);
                }
                for (int i = 0; i < n; i++)
                {
                    V2 a = _poly[i], b = _poly[(i + 1) % n];
                    PointF m = T(new V2((a.X + b.X) / 2, (a.Y + b.Y) / 2));
                    V2 outDir = (b - a).Unit().Left() * -1;                    // ra ngoài khung
                    string txt = (i + 1) + ": " + (b - a).Length.ToString("0", CultureInfo.InvariantCulture);
                    var sz = g.MeasureString(txt, fs);
                    float dx = (float)outDir.X * (sz.Width / 2 + 6), dy = -(float)outDir.Y * (sz.Height / 2 + 4);
                    float lx = Math.Max(2, Math.Min(Width - sz.Width - 2, m.X + dx - sz.Width / 2)), ly = Math.Max(2, Math.Min(Height - sz.Height - 2, m.Y + dy - sz.Height / 2));
                    g.DrawString(txt, fs, i == baseEdge ? Brushes.DarkOrange : Brushes.Silver, lx, ly);
                }
            }
        }

        /// <summary>Dải từ lệch d0 đến d1 so với đường a-b, đo về bên TRÁI của hướng a->b.</summary>
        private static void FillBand(Graphics g, System.Func<V2, PointF> T, V2 a, V2 b, double d0, double d1, Color col)
        {
            V2 n = (b - a).Unit().Left();
            var pts = new[] { T(a + n * d0), T(b + n * d0), T(b + n * d1), T(a + n * d1) };
            using (var br = new SolidBrush(col)) g.FillPolygon(br, pts);
        }
    }
}
