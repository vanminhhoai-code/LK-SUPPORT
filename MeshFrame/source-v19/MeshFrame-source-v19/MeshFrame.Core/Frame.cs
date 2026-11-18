using System;

namespace MeshFrame.Core;

public sealed class Frame
{
	public V3 Origin;

	public V3 U;

	public V3 V;

	public V3 N;

	public V3 ToWorld(V2 p, double z)
	{
		return Origin + U * p.X + V * p.Y + N * z;
	}

	public V2 To2D(V3 w)
	{
		V3 v = w - Origin;
		return new V2(v.Dot(U), v.Dot(V));
	}

	public static bool TryCreate(V3[] c, out Frame f, out string error)
	{
		f = null;
		error = null;
		if (c == null || c.Length < 3)
		{
			error = "Cần ít nhất 3 điểm.";
			return false;
		}
		V3 v = new V3(0.0, 0.0, 0.0);
		for (int i = 0; i < c.Length; i++)
		{
			V3 v2 = c[i];
			V3 v3 = c[(i + 1) % c.Length];
			v += new V3((v2.Y - v3.Y) * (v2.Z + v3.Z), (v2.Z - v3.Z) * (v2.X + v3.X), (v2.X - v3.X) * (v2.Y + v3.Y));
		}
		if (v.Length < 1E-06)
		{
			error = "Các điểm thẳng hàng hoặc quá gần nhau.";
			return false;
		}
		v = v.Unit();
		V3 v4 = (c[1] - c[0]).Unit();
		if (v4.Length < 0.5)
		{
			error = "Hai điểm đầu trùng nhau.";
			return false;
		}
		v4 = (v4 - v * v4.Dot(v)).Unit();
		for (int j = 0; j < c.Length; j++)
		{
			if (Math.Abs((c[j] - c[0]).Dot(v)) > 1.0)
			{
				error = "Các điểm pick không cùng một mặt phẳng (lệch > 1 mm).";
				return false;
			}
		}
		f = new Frame
		{
			Origin = c[0],
			U = v4,
			N = v,
			V = v.Cross(v4)
		};
		return true;
	}
}
