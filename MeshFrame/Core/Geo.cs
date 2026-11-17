using System;
using System.Collections.Generic;

namespace MeshFrame.Core
{
    /// <summary>Hình học 2D cho đa giác lồi ngược chiều kim đồng hồ.</summary>
    public static class Geo
    {
        /// <summary>Đường thẳng của cạnh k dịch vào trong d.</summary>
        public static void OffsetLine(V2[] p, int k, double d, out V2 origin, out V2 dir)
        {
            V2 a = p[k], b = p[(k + 1) % p.Length];
            dir = (b - a).Unit();
            origin = a + dir.Left() * d;
        }

        public static bool Intersect(V2 o1, V2 d1, V2 o2, V2 d2, out V2 r)
        {
            double den = d1.Cross(d2);
            if (Math.Abs(den) < 1e-12) { r = o1; return false; }
            double t = (o2 - o1).Cross(d2) / den;
            r = o1 + d1 * t;
            return true;
        }

        /// <summary>Đa giác dịch vào trong d (đỉnh = giao của 2 đường dịch liền kề).</summary>
        public static V2[] Offset(V2[] p, double d)
        {
            int n = p.Length;
            var r = new V2[n];
            for (int i = 0; i < n; i++)
            {
                V2 o1, d1, o2, d2;
                OffsetLine(p, (i + n - 1) % n, d, out o1, out d1);
                OffsetLine(p, i, d, out o2, out d2);
                V2 x;
                r[i] = Intersect(o1, d1, o2, d2, out x) ? x : p[i];
            }
            return r;
        }

        public static double Area(V2[] p)
        {
            double a = 0;
            for (int i = 0; i < p.Length; i++) a += p[i].Cross(p[(i + 1) % p.Length]);
            return a / 2;
        }

        /// <summary>Hướng phân giác tại đỉnh i, chỉ vào trong (dùng làm mặt cắt vát).</summary>
        public static V2 Bisector(V2[] p, int i)
        {
            return (Offset(p, 1)[i] - p[i]).Unit();
        }

        /// <summary>Đa giác sau khi dịch còn hợp lệ (cạnh giữ hướng, không tự cắt, diện tích dương) không?</summary>
        public static bool OffsetValid(V2[] orig, V2[] off)
        {
            if (Area(off) <= 0) return false;
            int n = orig.Length;
            for (int i = 0; i < n; i++)
                if ((off[(i + 1) % n] - off[i]).Dot(orig[(i + 1) % n] - orig[i]) <= 0) return false;
            return IsSimple(off);
        }

        // ---------------------------------------------------------------- đa giác tổng quát (lồi hoặc lõm)
        /// <summary>Đa giác đơn: ≥ 3 đỉnh, không trùng điểm, không cạnh nào cắt/chạm cạnh không kề, không gập 180°.</summary>
        public static bool IsSimple(V2[] p)
        {
            int n = p.Length;
            if (n < 3) return false;
            for (int i = 0; i < n; i++)
            {
                V2 a = p[i], b = p[(i + 1) % n], c = p[(i + 2) % n];
                if ((b - a).Length < 1e-6) return false;
                V2 e1 = b - a, e2 = c - b;
                if (Math.Abs(e1.Cross(e2)) < 1e-9 * e1.Length * e2.Length && e1.Dot(e2) < 0) return false;   // gập ngược
            }
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    if (j == i + 1 || (i == 0 && j == n - 1)) continue;                                      // cạnh kề
                    if (SegmentsTouch(p[i], p[(i + 1) % n], p[j], p[(j + 1) % n])) return false;
                }
            return true;
        }

        private static double Orient(V2 a, V2 b, V2 c) { return (b - a).Cross(c - a); }

        private static bool OnSeg(V2 a, V2 b, V2 p)
        {
            return Math.Min(a.X, b.X) - 1e-9 <= p.X && p.X <= Math.Max(a.X, b.X) + 1e-9 && Math.Min(a.Y, b.Y) - 1e-9 <= p.Y && p.Y <= Math.Max(a.Y, b.Y) + 1e-9;
        }

        /// <summary>Hai đoạn cắt hoặc chạm nhau.</summary>
        public static bool SegmentsTouch(V2 a, V2 b, V2 c, V2 d)
        {
            double o1 = Orient(a, b, c), o2 = Orient(a, b, d), o3 = Orient(c, d, a), o4 = Orient(c, d, b);
            double eps = 1e-9 * Math.Max(1, Math.Max((b - a).Length, (d - c).Length));
            if (((o1 > eps && o2 < -eps) || (o1 < -eps && o2 > eps)) && ((o3 > eps && o4 < -eps) || (o3 < -eps && o4 > eps))) return true;
            if (Math.Abs(o1) <= eps && OnSeg(a, b, c)) return true;
            if (Math.Abs(o2) <= eps && OnSeg(a, b, d)) return true;
            if (Math.Abs(o3) <= eps && OnSeg(c, d, a)) return true;
            if (Math.Abs(o4) <= eps && OnSeg(c, d, b)) return true;
            return false;
        }

        public struct LineSeg { public double T0, T1; public int E0, E1; }

        /// <summary>Các đoạn của đường thẳng (o + t·d) nằm TRONG đa giác p (lồi hoặc lõm), kèm chỉ số cạnh ở hai đầu, t tăng dần.</summary>
        public static List<LineSeg> ClipLine(V2[] p, V2 o, V2 d)
        {
            int n = p.Length;
            var s = new double[n];
            for (int i = 0; i < n; i++) s[i] = d.Cross(p[i] - o);
            var hits = new List<KeyValuePair<double, int>>();
            const double eps = 1e-9;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if ((s[i] > eps) == (s[j] > eps)) continue;          // quy tắc nửa mở: đỉnh nằm trên đường coi như phía không dương
                V2 e = p[j] - p[i];
                double den = d.Cross(e);
                if (Math.Abs(den) < 1e-12) continue;
                hits.Add(new KeyValuePair<double, int>((p[i] - o).Cross(e) / den, i));
            }
            hits.Sort(delegate (KeyValuePair<double, int> x, KeyValuePair<double, int> y) { return x.Key.CompareTo(y.Key); });
            var res = new List<LineSeg>();
            for (int i = 0; i + 1 < hits.Count; i += 2)
                res.Add(new LineSeg { T0 = hits[i].Key, E0 = hits[i].Value, T1 = hits[i + 1].Key, E1 = hits[i + 1].Value });
            return res;
        }
    }
}
