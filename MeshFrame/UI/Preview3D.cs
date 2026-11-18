// Xem trước 3D THẬT: dựng lăng trụ đúng tiết diện (Core.Model3D) rồi vẽ bằng bộ tô z-buffer tự viết (không cần thư viện ngoài).
// Chuột: con lăn = zoom (tới vị trí con trỏ); GIỮ chuột giữa + kéo = kéo tới lui (dịch hình);
//        Ctrl + giữ chuột giữa + kéo = xoay. Nút nhỏ góc trên phải: 3D / Trên / Dưới / Vừa khung.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MeshFrame.Core;

namespace MeshFrame.UI
{
    public class Preview3D : Control
    {
        private Model3D _model = new Model3D();
        private MeshFramePlan _plan;
        private string _empty = "Chưa có điểm. Bấm \"Pick lại điểm\" để pick đa giác trong Tekla.";
        // camera
        private double _yaw = -35, _pitch = 48, _scale = 1, _panX, _panY;
        private double _cx, _cy, _cz;                       // tâm xoay (tâm hộp bao)
        private bool _fitted;
        // tô
        private Bitmap _bmp; private int[] _pix; private float[] _zb; private int _w, _h;
        // chuột
        private Point _last; private bool _pan, _rot;
        private readonly List<KeyValuePair<Rectangle, string>> _buttons = new List<KeyValuePair<Rectangle, string>>();

        public Color BackgroundTop = Color.FromArgb(27, 28, 32), BackgroundBottom = Color.FromArgb(42, 45, 51), TextColor = Color.FromArgb(200, 204, 212);
        public Color FrameColor = Color.FromArgb(86, 156, 214), StiffColor = Color.FromArgb(78, 201, 176), StripColor = Color.FromArgb(230, 170, 60), MeshColor = Color.FromArgb(120, 130, 150);

        public Preview3D()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = BackgroundTop;
            TabStop = true;
        }

        public void Show(MeshFramePlan plan, V2[] poly)
        {
            bool first = _model.Prisms.Count == 0;
            _plan = plan;
            _model = plan == null ? new Model3D() : Model3D.Build(plan);
            if (_model.Prisms.Count == 0) { Invalidate(); return; }
            ComputeCenter();
            if (!_fitted || first) { FitView(); _fitted = true; }
            Invalidate();
        }

        public void ShowEmpty(string text) { _plan = null; _model = new Model3D(); if (text != null) _empty = text; _fitted = false; Invalidate(); }

        // ---------------------------------------------------------------- camera
        private void ComputeCenter()
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
            foreach (Prism p in _model.Prisms)
            {
                foreach (V2 v in p.Poly) { x0 = Math.Min(x0, v.X); x1 = Math.Max(x1, v.X); y0 = Math.Min(y0, v.Y); y1 = Math.Max(y1, v.Y); }
                z0 = Math.Min(z0, p.Z0); z1 = Math.Max(z1, p.Z1);
            }
            _cx = (x0 + x1) / 2; _cy = (y0 + y1) / 2; _cz = (z0 + z1) / 2;
        }

        /// <summary>Thấy hết khung ở góc nhìn hiện tại.</summary>
        public void FitView()
        {
            if (_model.Prisms.Count == 0 || Width < 10 || Height < 10) return;
            _panX = _panY = 0; _scale = 1;
            double minx = double.MaxValue, maxx = double.MinValue, miny = double.MaxValue, maxy = double.MinValue;
            foreach (Prism p in _model.Prisms)
                foreach (V2 v in p.Poly)
                    foreach (double z in new[] { p.Z0, p.Z1 })
                    {
                        double sx, sy, d; Project(v.X, v.Y, z, out sx, out sy, out d);
                        minx = Math.Min(minx, sx); maxx = Math.Max(maxx, sx); miny = Math.Min(miny, sy); maxy = Math.Max(maxy, sy);
                    }
            double bw = Math.Max(maxx - minx, 1e-6), bh = Math.Max(maxy - miny, 1e-6);
            _scale = 0.88 * Math.Min(Width / bw, (Height - 20) / bh);
            double mx = (minx + maxx) / 2, my = (miny + maxy) / 2;       // đo ở scale = 1 quanh tâm màn hình
            _panX = -(mx - Width / 2.0) * _scale; _panY = -(my - Height / 2.0) * _scale;
        }

        public void SetView(double yaw, double pitch) { _yaw = yaw; _pitch = pitch; FitView(); Invalidate(); }

        // (x, y, z) mô hình -> màn hình (sx, sy) và độ sâu d (lớn = gần người xem)
        private void Project(double x, double y, double z, out double sx, out double sy, out double d)
        {
            double ya = _yaw * Math.PI / 180, pa = _pitch * Math.PI / 180;
            double px = x - _cx, py = y - _cy, pz = z - _cz;
            double x1 = px * Math.Cos(ya) - py * Math.Sin(ya), y1 = px * Math.Sin(ya) + py * Math.Cos(ya);
            double y2 = y1 * Math.Cos(pa) - pz * Math.Sin(pa), z2 = y1 * Math.Sin(pa) + pz * Math.Cos(pa);
            sx = Width / 2.0 + _panX + _scale * x1;
            sy = Height / 2.0 + _panY - _scale * y2;
            d = z2;
        }

        // ---------------------------------------------------------------- chuột
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); if (!Focused && FindForm() != null && FindForm().ActiveControl is Preview3D == false && !(FindForm().ActiveControl is TextBoxBase)) Focus(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _last = e.Location;
            if (e.Button == MouseButtons.Middle)
            {
                if ((ModifierKeys & Keys.Control) == Keys.Control) { _rot = true; _pan = false; } else { _pan = true; _rot = false; }
                Cursor = _rot ? Cursors.SizeAll : Cursors.Hand;
            }
            else if (e.Button == MouseButtons.Left)
                foreach (var kv in _buttons)
                    if (kv.Key.Contains(e.Location)) { RunButton(kv.Value); return; }
        }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (e.Button == MouseButtons.Middle) { _pan = _rot = false; Cursor = Cursors.Default; } }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int dx = e.X - _last.X, dy = e.Y - _last.Y;
            _last = e.Location;
            if (_rot)
            {
                _yaw += dx * 0.5; _pitch = Math.Max(0, Math.Min(180, _pitch + dy * 0.5));
                Invalidate();
            }
            else if (_pan) { _panX += dx; _panY += dy; Invalidate(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ZoomAt(e.Location, Math.Pow(1.2, e.Delta / 120.0));
        }

        /// <summary>Zoom hệ số k, giữ nguyên điểm dưới vị trí 'at'.</summary>
        public void ZoomAt(Point at, double k)
        {
            if (_model.Prisms.Count == 0) return;
            var e = new { X = at.X, Y = at.Y };
            double ns = Math.Max(1e-4, Math.Min(1e4, _scale * k)); k = ns / _scale;
            // giữ nguyên điểm dưới con trỏ: (m - centre - pan) tỉ lệ theo scale
            double ox = e.X - Width / 2.0 - _panX, oy = e.Y - Height / 2.0 - _panY;
            _panX = e.X - Width / 2.0 - ox * k; _panY = e.Y - Height / 2.0 - oy * k;
            _scale = ns;
            Invalidate();
        }

        private void RunButton(string id)
        {
            if (id == "3D") SetView(-35, 48);
            else if (id == "Top") SetView(0, 0);
            else if (id == "Under") SetView(0, 180);
            else if (id == "Fit") { FitView(); Invalidate(); }
        }

        protected override bool IsInputKey(Keys k) { return k == Keys.Home || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Home) { SetView(-35, 48); } }

        // ---------------------------------------------------------------- vẽ
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle, BackgroundTop, BackgroundBottom, 90f)) g.FillRectangle(br, ClientRectangle);
            if (_model.Prisms.Count == 0)
            {
                using (var b = new SolidBrush(TextColor)) g.DrawString(_empty, Font, b, new RectangleF(16, 16, Width - 32, Height - 32));
                return;
            }
            if (Width < 8 || Height < 8) return;
            Render();
            g.DrawImageUnscaled(_bmp, 0, 0);
            DrawOverlay(g);
        }

        private struct Pt { public double X, Y, D; }

        private void Render()
        {
            if (_bmp == null || _w != Width || _h != Height)
            {
                if (_bmp != null) _bmp.Dispose();
                _w = Width; _h = Height;
                _bmp = new Bitmap(_w, _h, PixelFormat.Format32bppRgb);
                _pix = new int[_w * _h]; _zb = new float[_w * _h];
            }
            for (int y = 0; y < _h; y++)
            {
                double t = _h <= 1 ? 0 : (double)y / (_h - 1);
                int c = Lerp(BackgroundTop, BackgroundBottom, t).ToArgb();
                int o = y * _w;
                for (int x = 0; x < _w; x++) { _pix[o + x] = c; _zb[o + x] = float.NegativeInfinity; }
            }

            foreach (Prism p in _model.Prisms) DrawPrism(p);
            if (_model.MeshPoly != null) DrawMeshGrid();

            var bd = _bmp.LockBits(new Rectangle(0, 0, _w, _h), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            Marshal.Copy(_pix, 0, bd.Scan0, _pix.Length);
            _bmp.UnlockBits(bd);
        }

        private static Color Lerp(Color a, Color b, double t) { return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }

        private Color BaseColor(PartKind k) { return k == PartKind.FrameBar ? FrameColor : k == PartKind.Stiffener ? StiffColor : k == PartKind.Strip ? StripColor : MeshColor; }

        private Pt P3(double x, double y, double z) { Pt p; Project(x, y, z, out p.X, out p.Y, out p.D); return p; }

        private void DrawPrism(Prism pr)
        {
            Color baseC = BaseColor(pr.Kind);
            int n = pr.Poly.Length;
            Pt[] lo = new Pt[n], hi = new Pt[n];
            for (int i = 0; i < n; i++) { lo[i] = P3(pr.Poly[i].X, pr.Poly[i].Y, pr.Z0); hi[i] = P3(pr.Poly[i].X, pr.Poly[i].Y, pr.Z1); }
            // đáy + nắp
            foreach (int[] t in Model3D.Triangulate(pr.Poly))
            {
                Fill(hi[t[0]], hi[t[1]], hi[t[2]], Shade(baseC, 0, 0, 1));
                Fill(lo[t[0]], lo[t[2]], lo[t[1]], Shade(baseC, 0, 0, -1));
            }
            // mặt bên
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                V2 e = pr.Poly[j] - pr.Poly[i]; V2 nrm = new V2(e.Y, -e.X).Unit();     // pháp tuyến ngoài (đa giác ngược chiều kim đồng hồ)
                Color c = Shade(baseC, nrm.X, nrm.Y, 0);
                Fill(lo[i], lo[j], hi[j], c); Fill(lo[i], hi[j], hi[i], c);
            }
            // viền (có kiểm tra độ sâu): thấy rõ tiết diện chữ L và đường cắt
            Color edge = Lerp(baseC, Color.White, 0.4);
            double bias = 1.8 / _scale;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Line(hi[i], hi[j], edge, bias); Line(lo[i], lo[j], edge, bias); Line(lo[i], hi[i], edge, bias);
            }
        }

        // ánh sáng cố định theo camera: pháp tuyến mặt (trong hệ mô hình) -> độ sáng
        private Color Shade(Color c, double nx, double ny, double nz)
        {
            double ya = _yaw * Math.PI / 180, pa = _pitch * Math.PI / 180;
            double x1 = nx * Math.Cos(ya) - ny * Math.Sin(ya), y1 = nx * Math.Sin(ya) + ny * Math.Cos(ya);
            double y2 = y1 * Math.Cos(pa) - nz * Math.Sin(pa), z2 = y1 * Math.Sin(pa) + nz * Math.Cos(pa);
            double lx = -0.35, ly = 0.45, lz = 0.82, ll = Math.Sqrt(lx * lx + ly * ly + lz * lz);
            double dot = Math.Abs((x1 * lx + y2 * ly + z2 * lz) / ll);
            double k = 0.38 + 0.62 * dot;
            return Color.FromArgb(Clamp(c.R * k), Clamp(c.G * k), Clamp(c.B * k));
        }

        private static int Clamp(double v) { return v < 0 ? 0 : v > 255 ? 255 : (int)v; }

        private void Fill(Pt a, Pt b, Pt c, Color col)
        {
            int minx = (int)Math.Max(0, Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X)))), maxx = (int)Math.Min(_w - 1, Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
            int miny = (int)Math.Max(0, Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y)))), maxy = (int)Math.Min(_h - 1, Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
            if (minx > maxx || miny > maxy) return;
            double den = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(den) < 1e-12) return;
            int argb = col.ToArgb();
            for (int y = miny; y <= maxy; y++)
            {
                double py = y + 0.5;
                for (int x = minx; x <= maxx; x++)
                {
                    double px = x + 0.5;
                    double l1 = ((b.Y - c.Y) * (px - c.X) + (c.X - b.X) * (py - c.Y)) / den;
                    double l2 = ((c.Y - a.Y) * (px - c.X) + (a.X - c.X) * (py - c.Y)) / den;
                    double l3 = 1 - l1 - l2;
                    if (l1 < -1e-9 || l2 < -1e-9 || l3 < -1e-9) continue;
                    float d = (float)(l1 * a.D + l2 * b.D + l3 * c.D);
                    int i = y * _w + x;
                    if (d > _zb[i]) { _zb[i] = d; _pix[i] = argb; }
                }
            }
        }

        private void Line(Pt a, Pt b, Color col, double bias)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            int steps = (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)));
            if (steps <= 0) return;
            if (steps > 20000) return;
            int argb = col.ToArgb();
            for (int s = 0; s <= steps; s++)
            {
                double t = (double)s / steps;
                int x = (int)Math.Round(a.X + dx * t), y = (int)Math.Round(a.Y + dy * t);
                if (x < 0 || y < 0 || x >= _w || y >= _h) continue;
                float d = (float)(a.D + (b.D - a.D) * t);
                int i = y * _w + x;
                if (d >= _zb[i] - bias) { _zb[i] = Math.Max(_zb[i], d); _pix[i] = argb; }
            }
        }

        /// <summary>Lưới ô trên mặt lưới (chỉ khi một ô đủ lớn trên màn hình): thấy rõ sợi khi zoom gần.</summary>
        private void DrawMeshGrid()
        {
            double pitch = _model.MeshPitch;
            if (pitch <= 0 || pitch * _scale < 4) return;
            V2[] poly = _model.MeshPoly;
            V2 a0 = poly[0], u = (poly[1] - poly[0]).Unit(), v = u.Left();
            double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
            foreach (V2 p in poly) { double cu = (p - a0).Dot(u), cv = (p - a0).Dot(v); u0 = Math.Min(u0, cu); u1 = Math.Max(u1, cu); v0 = Math.Min(v0, cv); v1 = Math.Max(v1, cv); }
            // chỉ vẽ phần nằm trong màn hình: lấy khoảng u, v nhìn thấy bằng cách chiếu ngược 4 góc màn hình lên mặt z = đỉnh lưới
            double su0 = double.MaxValue, su1 = double.MinValue, sv0 = double.MaxValue, sv1 = double.MinValue;
            foreach (Point sc in new[] { new Point(0, 0), new Point(_w, 0), new Point(0, _h), new Point(_w, _h) })
            {
                V2 w; if (!Unproject(sc.X, sc.Y, _model.MeshZTop, out w)) { su0 = u0; su1 = u1; sv0 = v0; sv1 = v1; break; }
                double cu = (w - a0).Dot(u), cv = (w - a0).Dot(v);
                su0 = Math.Min(su0, cu); su1 = Math.Max(su1, cu); sv0 = Math.Min(sv0, cv); sv1 = Math.Max(sv1, cv);
            }
            su0 = Math.Max(su0, u0); su1 = Math.Min(su1, u1); sv0 = Math.Max(sv0, v0); sv1 = Math.Min(sv1, v1);
            if (su0 >= su1 || sv0 >= sv1) return;
            double sU0 = Math.Ceiling((su0 - u0) / pitch), sU1 = Math.Floor((su1 - u0) / pitch), sV0 = Math.Ceiling((sv0 - v0) / pitch), sV1 = Math.Floor((sv1 - v0) / pitch);
            if ((sU1 - sU0 + 1) + (sV1 - sV0 + 1) > 1500) return;
            Color lc = Lerp(MeshColor, Color.Black, 0.35);
            double bias = 1.2 / _scale;
            for (double z = _model.MeshZTop; ; z = _model.MeshZBottom)
            {
                for (double i = sU0; i <= sU1; i++)
                    foreach (Geo.LineSeg sg in Geo.ClipLine(poly, a0 + u * (u0 + i * pitch), v))
                        Line(P3((a0 + u * (u0 + i * pitch) + v * sg.T0).X, (a0 + u * (u0 + i * pitch) + v * sg.T0).Y, z), P3((a0 + u * (u0 + i * pitch) + v * sg.T1).X, (a0 + u * (u0 + i * pitch) + v * sg.T1).Y, z), lc, bias);
                for (double j = sV0; j <= sV1; j++)
                    foreach (Geo.LineSeg sg in Geo.ClipLine(poly, a0 + v * (v0 + j * pitch), u))
                        Line(P3((a0 + v * (v0 + j * pitch) + u * sg.T0).X, (a0 + v * (v0 + j * pitch) + u * sg.T0).Y, z), P3((a0 + v * (v0 + j * pitch) + u * sg.T1).X, (a0 + v * (v0 + j * pitch) + u * sg.T1).Y, z), lc, bias);
                if (z == _model.MeshZBottom) break;
            }
        }

        /// <summary>Điểm màn hình -> điểm trên mặt phẳng z = zPlane của mô hình.</summary>
        private bool Unproject(double sx, double sy, double zPlane, out V2 w)
        {
            w = new V2(0, 0);
            double ya = _yaw * Math.PI / 180, pa = _pitch * Math.PI / 180;
            double x1 = (sx - Width / 2.0 - _panX) / _scale, y2 = -(sy - Height / 2.0 - _panY) / _scale;
            double pz = zPlane - _cz;
            // y2 = y1 cos(pa) - pz sin(pa)  ->  y1
            double cp = Math.Cos(pa);
            if (Math.Abs(cp) < 1e-3) return false;
            double y1 = (y2 + pz * Math.Sin(pa)) / cp;
            double px = x1 * Math.Cos(ya) + y1 * Math.Sin(ya), py = -x1 * Math.Sin(ya) + y1 * Math.Cos(ya);
            w = new V2(px + _cx, py + _cy);
            return true;
        }

        private void DrawOverlay(Graphics g)
        {
            _buttons.Clear();
            string[][] defs = { new[] { "3D", "3D" }, new[] { "Top", "Trên" }, new[] { "Under", "Dưới" }, new[] { "Fit", "Vừa khung" } };
            int x = Width - 8;
            using (var f = new Font(Font.FontFamily, 8.5f))
            using (var br = new SolidBrush(Color.FromArgb(200, 42, 45, 51)))
            using (var pen = new Pen(Color.FromArgb(90, 100, 110)))
            using (var tb = new SolidBrush(TextColor))
            {
                for (int i = defs.Length - 1; i >= 0; i--)
                {
                    SizeF sz = g.MeasureString(defs[i][1], f);
                    var r = new Rectangle((int)(x - sz.Width - 14), 8, (int)sz.Width + 14, 22);
                    g.FillRectangle(br, r); g.DrawRectangle(pen, r);
                    g.DrawString(defs[i][1], f, tb, r.X + 7, r.Y + 3);
                    _buttons.Add(new KeyValuePair<Rectangle, string>(r, defs[i][0]));
                    x = r.X - 6;
                }
                g.DrawString("Cuộn: zoom · Giữ chuột giữa: kéo · Ctrl + chuột giữa: xoay", f, tb, 8, Height - 20);
                DrawAxes(g, f);
            }
        }

        private void DrawAxes(Graphics g, Font f)
        {
            // trục X (đỏ), Y (xanh lá), Z (xanh dương) ở góc dưới trái
            double ya = _yaw * Math.PI / 180, pa = _pitch * Math.PI / 180;
            Point o = new Point(44, Height - 52);
            var axes = new[] { new { n = "X", v = new[] { 1.0, 0, 0 }, c = Color.FromArgb(230, 90, 90) }, new { n = "Y", v = new[] { 0.0, 1, 0 }, c = Color.FromArgb(110, 200, 110) }, new { n = "Z", v = new[] { 0.0, 0, 1 }, c = Color.FromArgb(100, 160, 240) } };
            foreach (var a in axes)
            {
                double x1 = a.v[0] * Math.Cos(ya) - a.v[1] * Math.Sin(ya), y1 = a.v[0] * Math.Sin(ya) + a.v[1] * Math.Cos(ya);
                double y2 = y1 * Math.Cos(pa) - a.v[2] * Math.Sin(pa);
                var e = new PointF((float)(o.X + 26 * x1), (float)(o.Y - 26 * y2));
                using (var p = new Pen(a.c, 2)) g.DrawLine(p, o, e);
                using (var b = new SolidBrush(a.c)) g.DrawString(a.n, f, b, e.X + 2, e.Y - 7);
            }
        }

        protected override void Dispose(bool disposing) { if (disposing && _bmp != null) { _bmp.Dispose(); _bmp = null; } base.Dispose(disposing); }
    }
}
