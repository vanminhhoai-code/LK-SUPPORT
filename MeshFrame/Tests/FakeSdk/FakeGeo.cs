// SDK GIẢ (chỉ để test trên Mono) - dựng theo hình dạng API Tekla thật. Biên dịch thành Tekla.Structures.dll giả.
namespace Tekla.Structures.Geometry3d
{
    public class Point { public double X, Y, Z; public Point(double x, double y, double z) { X = x; Y = y; Z = z; } }
    public class Vector { public double X, Y, Z; public Vector(double x, double y, double z) { X = x; Y = y; Z = z; } }
    public class Matrix { public Point Transform(Point p) { return new Point(p.X, p.Y, p.Z); } }
}
