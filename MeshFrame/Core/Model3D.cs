// Dựng hình khối 3D THẬT từ MeshFramePlan (thuần, không phụ thuộc WinForms / Tekla): mỗi chi tiết là một hoặc vài lăng trụ đa giác.
//  - Thép góc V: cánh nằm (z 0..t, rộng legA) + cánh đứng (rộng t, cao tới legB) -> đúng tiết diện chữ L; cắt vát và khuyết theo kế hoạch.
//  - Nẹp: tấm dẹt rộng w dày t quanh đường tim, nằm phía ngoài lưới.   - Lưới: tấm đa giác dày 2 lớp sợi.
// Cắt vát / khuyết = cắt đa giác bằng nửa mặt phẳng (giữ phía có thân thanh), nên chính xác với mọi góc.
using System;
using System.Collections.Generic;

namespace MeshFrame.Core
{
    public enum PartKind { FrameBar, Stiffener, Strip, Mesh }

    public sealed class Prism
    {
        public V2[] Poly;            // đa giác đáy (ngược chiều kim đồng hồ)
        public double Z0, Z1;
        public PartKind Kind;
        public int Index;            // chỉ số thanh trong plan (Bars / Strips)
        public double Volume { get { return Math.Abs(Geo.Area(Poly)) * (Z1 - Z0); } }
    }

    public sealed class Model3D
    {
        public readonly List<Prism> Prisms = new List<Prism>();
        public double MeshPitch;
        public V2[] MeshPoly;
        public double MeshZTop, MeshZBottom;

        public static Model3D Build(MeshFramePlan plan)
        {
            var m = new Model3D();
            if (plan == null) return m;
            for (int i = 0; i < plan.Bars.Count; i++) m.AddBar(plan.Bars[i], i);
            for (int i = 0; i < plan.Strips.Count; i++) m.AddStrip(plan.Strips[i], i);
            if (plan.Mesh != null && plan.Mesh.Polygon != null && plan.Mesh.Polygon.Length >= 3)
            {
                V2[] mp = (V2[])plan.Mesh.Polygon.Clone();
                if (Geo.Area(mp) < 0) Array.Reverse(mp);
                m.Prisms.Add(new Prism { Poly = mp, Z0 = plan.Mesh.ZBottom, Z1 = 0, Kind = PartKind.Mesh, Index = 0 });
                m.MeshPoly = mp; m.MeshPitch = plan.Mesh.Pitch; m.MeshZTop = 0; m.MeshZBottom = plan.Mesh.ZBottom;
            }
            return m;
        }

        private static List<V2> Rect(V2 o, V2 d, V2 n, double s0, double s1, double l0, double l1)
        {
            return new List<V2> { o + d * s0 + n * l0, o + d * s1 + n * l0, o + d * s1 + n * l1, o + d * s0 + n * l1 };
        }

        private void AddBar(PlanBar b, int idx)
        {
            V2 d = (b.End - b.Start).Unit(), n = d.Left();
            double len = (b.End - b.Start).Length, t = b.Thk;
            V2 keep = b.Start + d * (len / 2) + n * (b.LegA / 2);
            PartKind kind = b.Role == BarRole.Stiffener ? PartKind.Stiffener : PartKind.FrameBar;

            // cánh nằm: ngang [0, legA], z [ZRef, ZRef + t]; trừ các vùng khuyết
            List<V2> h = Rect(b.Start, d, n, 0, len, 0, b.LegA);
            h = ClipEnds(h, b, keep);
            foreach (V2[] q in b.Notches)
            {
                if (h == null) break;
                // cạnh trong của vùng khuyết = đoạn p01 -> p11 (đường offset legA của cạnh khung): giữ phía có thân thanh
                h = ClipHalfPlane(h, q[1], q[2] - q[1], keep);
            }
            Add(h, b.ZRef, b.ZRef + t, kind, idx);

            // cánh đứng: ngang [0, t], z [ZRef + t, ZRef + legB]
            if (b.LegB > t + 1e-9)
            {
                List<V2> v = Rect(b.Start, d, n, 0, len, 0, t);
                v = ClipEnds(v, b, keep);
                Add(v, b.ZRef + t, b.ZRef + b.LegB, kind, idx);
            }
        }

        private void AddStrip(PlanStrip s, int idx)
        {
            V2 d = (s.End - s.Start).Unit(), n = d.Left();
            double len = (s.End - s.Start).Length;
            V2 keep = s.Start + d * (len / 2);
            List<V2> r = Rect(s.Start, d, n, 0, len, -s.Width / 2, s.Width / 2);
            if (s.CutStart.HasValue) r = ClipHalfPlane(r, s.Start, s.CutStart.Value, keep);
            if (r != null && s.CutEnd.HasValue) r = ClipHalfPlane(r, s.End, s.CutEnd.Value, keep);
            Add(r, s.ZRef - s.Thk, s.ZRef, PartKind.Strip, idx);
        }

        private static List<V2> ClipEnds(List<V2> r, PlanBar b, V2 keep)
        {
            if (b.CutStart.HasValue) r = ClipHalfPlane(r, b.Start, b.CutStart.Value, keep);
            if (r != null && b.CutEnd.HasValue) r = ClipHalfPlane(r, b.End, b.CutEnd.Value, keep);
            return r;
        }

        private void Add(List<V2> poly, double z0, double z1, PartKind kind, int idx)
        {
            if (poly == null || poly.Count < 3) return;
            V2[] a = poly.ToArray();
            double ar = Geo.Area(a);
            if (Math.Abs(ar) < 1e-9 || z1 - z0 < 1e-9) return;
            if (ar < 0) Array.Reverse(a);
            Prisms.Add(new Prism { Poly = a, Z0 = z0, Z1 = z1, Kind = kind, Index = idx });
        }

        /// <summary>Cắt đa giác lồi bằng nửa mặt phẳng: giữ phía chứa điểm 'keep' của đường qua p theo hướng dir. Trả về null nếu không còn gì.</summary>
        public static List<V2> ClipHalfPlane(List<V2> poly, V2 p, V2 dir, V2 keep)
        {
            double sk = dir.Cross(keep - p);
            double sign = sk >= 0 ? 1 : -1;
            var res = new List<V2>();
            for (int i = 0; i < poly.Count; i++)
            {
                V2 a = poly[i], b = poly[(i + 1) % poly.Count];
                double da = sign * dir.Cross(a - p), db = sign * dir.Cross(b - p);
                if (da >= 0) res.Add(a);
                if ((da > 0 && db < 0) || (da < 0 && db > 0)) res.Add(a + (b - a) * (da / (da - db)));
            }
            return res.Count < 3 ? null : res;
        }

        /// <summary>Chia đa giác đơn (lồi hoặc lõm) thành tam giác (cắt tai). Đa giác phải ngược chiều kim đồng hồ.</summary>
        public static List<int[]> Triangulate(V2[] p)
        {
            var tris = new List<int[]>();
            var idx = new List<int>(); for (int i = 0; i < p.Length; i++) idx.Add(i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 10000)
            {
                bool cut = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    V2 a = p[ia], b = p[ib], c = p[ic];
                    if ((b - a).Cross(c - b) <= 1e-12) continue;                 // không lồi
                    bool ear = true;
                    foreach (int j in idx)
                    {
                        if (j == ia || j == ib || j == ic) continue;
                        if (InTri(p[j], a, b, c)) { ear = false; break; }
                    }
                    if (!ear) continue;
                    tris.Add(new[] { ia, ib, ic }); idx.RemoveAt(i); cut = true; break;
                }
                if (!cut) { tris.Add(new[] { idx[0], idx[1], idx[2] }); idx.RemoveAt(1); }   // đa giác suy biến: vẫn thoát vòng lặp
            }
            if (idx.Count == 3) tris.Add(new[] { idx[0], idx[1], idx[2] });
            return tris;
        }

        private static bool InTri(V2 q, V2 a, V2 b, V2 c)
        {
            double d1 = (b - a).Cross(q - a), d2 = (c - b).Cross(q - b), d3 = (a - c).Cross(q - c);
            return d1 > -1e-9 && d2 > -1e-9 && d3 > -1e-9;
        }
    }
}
