using System;

namespace MeshFrame.Core;

public struct V2(double x, double y)
{
	public readonly double X = x;

	public readonly double Y = y;

	public double Length => Math.Sqrt(X * X + Y * Y);

	public static V2 operator +(V2 a, V2 b)
	{
		return new V2(a.X + b.X, a.Y + b.Y);
	}

	public static V2 operator -(V2 a, V2 b)
	{
		return new V2(a.X - b.X, a.Y - b.Y);
	}

	public static V2 operator *(V2 a, double k)
	{
		return new V2(a.X * k, a.Y * k);
	}

	public double Dot(V2 b)
	{
		return X * b.X + Y * b.Y;
	}

	public double Cross(V2 b)
	{
		return X * b.Y - Y * b.X;
	}

	public V2 Unit()
	{
		double length = Length;
		if (!(length < 1E-12))
		{
			return new V2(X / length, Y / length);
		}
		return new V2(0.0, 0.0);
	}

	public V2 Left()
	{
		return new V2(0.0 - Y, X);
	}

	public static V2 Lerp(V2 a, V2 b, double t)
	{
		return a + (b - a) * t;
	}

	public override string ToString()
	{
		return $"({X:0.##}, {Y:0.##})";
	}
}
