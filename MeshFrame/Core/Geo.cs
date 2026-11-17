using System;

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

        /// <summary>Đa giác sau khi dịch còn hợp lệ (không bị lật/suy biến) không?</summary>
        public static bool OffsetValid(V2[] orig, V2[] off)
        {
            if (Area(off) <= 0) return false;
            int n = orig.Length;
            for (int i = 0; i < n; i++)
                if ((off[(i + 1) % n] - off[i]).Dot(orig[(i + 1) % n] - orig[i]) <= 0) return false;
            return true;
        }
    }
}
