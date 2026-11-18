using System;

namespace MeshFrame.Core;

public struct V3(double x, double y, double z)
{
	public readonly double X = x;

	public readonly double Y = y;

	public readonly double Z = z;

	public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

	public static V3 operator +(V3 a, V3 b)
	{
		return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
	}

	public static V3 operator -(V3 a, V3 b)
	{
		return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
	}

	public static V3 operator *(V3 a, double k)
	{
		return new V3(a.X * k, a.Y * k, a.Z * k);
	}

	public double Dot(V3 b)
	{
		return X * b.X + Y * b.Y + Z * b.Z;
	}

	public V3 Cross(V3 b)
	{
		return new V3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
	}

	public V3 Unit()
	{
		double length = Length;
		if (!(length < 1E-12))
		{
			return new V3(X / length, Y / length, Z / length);
		}
		return new V3(0.0, 0.0, 0.0);
	}
}
