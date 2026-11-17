// SDK GIẢ (chỉ để test trên Mono) - dựng theo hình dạng API Tekla thật. Biên dịch thành Tekla.Structures.dll giả.
namespace Tekla.Structures.Geometry3d
{
    public class Point { public double X { get; set; } public double Y { get; set; } public double Z { get; set; } public Point(double x, double y, double z) { X = x; Y = y; Z = z; } }
    public class Vector { public double X { get; set; } public double Y { get; set; } public double Z { get; set; } public Vector(double x, double y, double z) { X = x; Y = y; Z = z; } }
    public class Matrix { public Point Transform(Point p) { return new Point(p.X, p.Y, p.Z); } }
}
