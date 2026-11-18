using System;
using System.Collections.Generic;

namespace MeshFrame.Core;

public static class Geo
{
	public struct LineSeg
	{
		public double T0;

		public double T1;

		public int E0;

		public int E1;
	}

	public static void OffsetLine(V2[] p, int k, double d, out V2 origin, out V2 dir)
	{
		V2 v = p[k];
		V2 v2 = p[(k + 1) % p.Length];
		dir = (v2 - v).Unit();
		origin = v + dir.Left() * d;
	}

	public static bool Intersect(V2 o1, V2 d1, V2 o2, V2 d2, out V2 r)
	{
		double num = d1.Cross(d2);
		if (Math.Abs(num) < 1E-12)
		{
			r = o1;
			return false;
		}
		double num2 = (o2 - o1).Cross(d2) / num;
		r = o1 + d1 * num2;
		return true;
	}

	public static V2[] Offset(V2[] p, double d)
	{
		int num = p.Length;
		V2[] array = new V2[num];
		for (int i = 0; i < num; i++)
		{
			OffsetLine(p, (i + num - 1) % num, d, out var origin, out var dir);
			OffsetLine(p, i, d, out var origin2, out var dir2);
			array[i] = (Intersect(origin, dir, origin2, dir2, out var r) ? r : p[i]);
		}
		return array;
	}

	public static double Area(V2[] p)
	{
		double num = 0.0;
		for (int i = 0; i < p.Length; i++)
		{
			num += p[i].Cross(p[(i + 1) % p.Length]);
		}
		return num / 2.0;
	}

	public static V2 Bisector(V2[] p, int i)
	{
		return (Offset(p, 1.0)[i] - p[i]).Unit();
	}

	public static bool OffsetValid(V2[] orig, V2[] off)
	{
		if (Area(off) <= 0.0)
		{
			return false;
		}
		int num = orig.Length;
		for (int i = 0; i < num; i++)
		{
			if ((off[(i + 1) % num] - off[i]).Dot(orig[(i + 1) % num] - orig[i]) <= 0.0)
			{
				return false;
			}
		}
		return IsSimple(off);
	}

	public static bool IsSimple(V2[] p)
	{
		int num = p.Length;
		if (num < 3)
		{
			return false;
		}
		for (int i = 0; i < num; i++)
		{
			V2 v = p[i];
			V2 v2 = p[(i + 1) % num];
			V2 v3 = p[(i + 2) % num];
			if ((v2 - v).Length < 1E-06)
			{
				return false;
			}
			V2 v4 = v2 - v;
			V2 b = v3 - v2;
			if (Math.Abs(v4.Cross(b)) < 1E-09 * v4.Length * b.Length && v4.Dot(b) < 0.0)
			{
				return false;
			}
		}
		for (int j = 0; j < num; j++)
		{
			for (int k = j + 1; k < num; k++)
			{
				if (k != j + 1 && (j != 0 || k != num - 1) && SegmentsTouch(p[j], p[(j + 1) % num], p[k], p[(k + 1) % num]))
				{
					return false;
				}
			}
		}
		return true;
	}

	private static double Orient(V2 a, V2 b, V2 c)
	{
		return (b - a).Cross(c - a);
	}

	private static bool OnSeg(V2 a, V2 b, V2 p)
	{
		if (Math.Min(a.X, b.X) - 1E-09 <= p.X && p.X <= Math.Max(a.X, b.X) + 1E-09 && Math.Min(a.Y, b.Y) - 1E-09 <= p.Y)
		{
			return p.Y <= Math.Max(a.Y, b.Y) + 1E-09;
		}
		return false;
	}

	public static bool SegmentsTouch(V2 a, V2 b, V2 c, V2 d)
	{
		double num = Orient(a, b, c);
		double num2 = Orient(a, b, d);
		double num3 = Orient(c, d, a);
		double num4 = Orient(c, d, b);
		double num5 = 1E-09 * Math.Max(1.0, Math.Max((b - a).Length, (d - c).Length));
		if (((num > num5 && num2 < 0.0 - num5) || (num < 0.0 - num5 && num2 > num5)) && ((num3 > num5 && num4 < 0.0 - num5) || (num3 < 0.0 - num5 && num4 > num5)))
		{
			return true;
		}
		if (Math.Abs(num) <= num5 && OnSeg(a, b, c))
		{
			return true;
		}
		if (Math.Abs(num2) <= num5 && OnSeg(a, b, d))
		{
			return true;
		}
		if (Math.Abs(num3) <= num5 && OnSeg(c, d, a))
		{
			return true;
		}
		if (Math.Abs(num4) <= num5 && OnSeg(c, d, b))
		{
			return true;
		}
		return false;
	}

	public static List<LineSeg> ClipLine(V2[] p, V2 o, V2 d)
	{
		int num = p.Length;
		double[] array = new double[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = d.Cross(p[i] - o);
		}
		List<KeyValuePair<double, int>> list = new List<KeyValuePair<double, int>>();
		for (int j = 0; j < num; j++)
		{
			int num2 = (j + 1) % num;
			if (array[j] > 1E-09 != array[num2] > 1E-09)
			{
				V2 b = p[num2] - p[j];
				double num3 = d.Cross(b);
				if (!(Math.Abs(num3) < 1E-12))
				{
					list.Add(new KeyValuePair<double, int>((p[j] - o).Cross(b) / num3, j));
				}
			}
		}
		list.Sort((KeyValuePair<double, int> x, KeyValuePair<double, int> y) => x.Key.CompareTo(y.Key));
		List<LineSeg> list2 = new List<LineSeg>();
		for (int num4 = 0; num4 + 1 < list.Count; num4 += 2)
		{
			list2.Add(new LineSeg
			{
				T0 = list[num4].Key,
				E0 = list[num4].Value,
				T1 = list[num4 + 1].Key,
				E1 = list[num4 + 1].Value
			});
		}
		return list2;
	}
}
