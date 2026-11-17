// Vẽ xem trước mặt bằng từ MeshFramePlan (chỉ dùng số trong plan, không tính lại hình học).
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MeshFrame.Core;

namespace MeshFrame.UI
{
    public class PreviewControl : Control
    {
        private MeshFramePlan _plan;
        private V2[] _poly;
        private double _legA;

        public PreviewControl()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
        }

        public void Show(MeshFramePlan plan, V2[] poly, double frameLegA)
        {
            _plan = plan; _poly = poly; _legA = frameLegA;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (_poly == null) return;

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (V2 p in _poly) { minX = System.Math.Min(minX, p.X); maxX = System.Math.Max(maxX, p.X); minY = System.Math.Min(minY, p.Y); maxY = System.Math.Max(maxY, p.Y); }
            double bw = maxX - minX, bh = maxY - minY;
            if (bw < 1 || bh < 1) return;
            const int pad = 24;
            double k = System.Math.Min((Width - 2 * pad) / bw, (Height - 2 * pad) / bh);
            double ox = (Width - bw * k) / 2, oy = (Height - bh * k) / 2;
            System.Func<V2, PointF> T = p => new PointF((float)(ox + (p.X - minX) * k), (float)(Height - (oy + (p.Y - minY) * k)));
            System.Func<V2[], PointF[]> TA = a => { var r = new PointF[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = T(a[i]); return r; };

            if (_plan == null || _plan.Mesh == null)
            {
                using (var pen = new Pen(Color.Gray)) g.DrawPolygon(pen, TA(_poly));
                return;
            }

            // lưới
            using (var br = new SolidBrush(Color.FromArgb(120, 135, 190, 230))) g.FillPolygon(br, TA(_plan.Mesh.Polygon));
            using (var hatch = new HatchBrush(HatchStyle.SmallGrid, Color.FromArgb(110, 70, 130, 180), Color.Transparent)) g.FillPolygon(hatch, TA(_plan.Mesh.Polygon));

            // V khung: vòng giữa đa giác ngoài và đa giác dịch vào bằng bề rộng cánh nằm
            V2[] inner = Geo.Offset(_poly, _legA);
            using (var path = new GraphicsPath(FillMode.Alternate))
            {
                path.AddPolygon(TA(_poly)); path.AddPolygon(TA(inner));
                using (var br = new SolidBrush(Color.FromArgb(235, 230, 126, 34))) g.FillPath(br, path);
            }

            // V gia cường (đặt giữa tim)
            foreach (PlanBar b in _plan.Bars)
                if (b.Role == BarRole.Stiffener) FillBand(g, T, b.Start, b.End, 0, b.LegA, Color.FromArgb(235, 214, 100, 20));

            // nẹp
            foreach (PlanStrip s in _plan.Strips)
                FillBand(g, T, s.Start, s.End, -s.Width / 2, s.Width / 2, Color.FromArgb(200, 39, 174, 96));

            // vít
            using (var br = new SolidBrush(Color.Black))
                foreach (PlanFastener f in _plan.Fasteners)
                {
                    PointF c = T(f.Pos);
                    float r = f.Kind == FastenKind.Screw ? 2.6f : 2.0f;
                    g.FillEllipse(br, c.X - r, c.Y - r, 2 * r, 2 * r);
                }

            using (var pen = new Pen(Color.FromArgb(60, 60, 60), 1.2f)) g.DrawPolygon(pen, TA(_poly));
            using (var f = new Font("Segoe UI", 9f, FontStyle.Bold))
                for (int i = 0; i < 4; i++)
                {
                    PointF c = T(_poly[i]);
                    g.DrawString("P" + (i + 1), f, Brushes.Firebrick, c.X + (i == 0 || i == 3 ? -22 : 4), c.Y + (i < 2 ? 2 : -18));
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
