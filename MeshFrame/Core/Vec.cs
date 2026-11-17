// Kiểu vector thuần - Core KHÔNG phụ thuộc Tekla hay WinForms (cùng kiểu với BaoChe.FlashingDim.Module).
using System;

namespace MeshFrame.Core
{
    public struct V2
    {
        public readonly double X, Y;
        public V2(double x, double y) { X = x; Y = y; }
        public static V2 operator +(V2 a, V2 b) { return new V2(a.X + b.X, a.Y + b.Y); }
        public static V2 operator -(V2 a, V2 b) { return new V2(a.X - b.X, a.Y - b.Y); }
        public static V2 operator *(V2 a, double k) { return new V2(a.X * k, a.Y * k); }
        public double Dot(V2 b) { return X * b.X + Y * b.Y; }
        public double Cross(V2 b) { return X * b.Y - Y * b.X; }
        public double Length { get { return Math.Sqrt(X * X + Y * Y); } }
        public V2 Unit() { double l = Length; return l < 1e-12 ? new V2(0, 0) : new V2(X / l, Y / l); }
        public V2 Left() { return new V2(-Y, X); }   // quay +90 độ (hướng vào trong khi đa giác ngược chiều kim đồng hồ)
        public static V2 Lerp(V2 a, V2 b, double t) { return a + (b - a) * t; }
        public override string ToString() { return string.Format("({0:0.##}, {1:0.##})", X, Y); }
    }

    public struct V3
    {
        public readonly double X, Y, Z;
        public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) { return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static V3 operator -(V3 a, V3 b) { return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static V3 operator *(V3 a, double k) { return new V3(a.X * k, a.Y * k, a.Z * k); }
        public double Dot(V3 b) { return X * b.X + Y * b.Y + Z * b.Z; }
        public V3 Cross(V3 b) { return new V3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X); }
        public double Length { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }
        public V3 Unit() { double l = Length; return l < 1e-12 ? new V3(0, 0, 0) : new V3(X / l, Y / l, Z / l); }
    }

    /// <summary>Hệ trục của mặt phẳng khung: gốc P1, U dọc P1-&gt;P2, V trong mặt phẳng, N pháp tuyến (phía "trên").</summary>
    public sealed class Frame
    {
        public V3 Origin, U, V, N;

        public V3 ToWorld(V2 p, double z) { return Origin + U * p.X + V * p.Y + N * z; }
        public V2 To2D(V3 w) { V3 d = w - Origin; return new V2(d.Dot(U), d.Dot(V)); }

        /// <summary>Tạo hệ trục từ 4 điểm pick. N theo quy tắc bàn tay phải: pick ngược chiều kim đồng hồ nhìn từ trên thì N hướng lên.</summary>
        public static bool TryCreate(V3[] c, out Frame f, out string error)
        {
            f = null; error = null;
            V3 u = (c[1] - c[0]).Unit();
            V3 n = u.Cross(c[3] - c[0]).Unit();
            if (u.Length < 0.5 || n.Length < 0.5) { error = "P1, P2, P4 thẳng hàng hoặc trùng nhau."; return false; }
            if (System.Math.Abs((c[2] - c[0]).Dot(n)) > 1.0) { error = "4 điểm pick không cùng một mặt phẳng (lệch > 1 mm)."; return false; }
            f = new Frame { Origin = c[0], U = u, N = n, V = n.Cross(u) };
            return true;
        }
    }
}
