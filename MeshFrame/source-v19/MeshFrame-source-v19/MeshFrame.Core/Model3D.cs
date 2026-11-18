using System;
using System.Collections.Generic;

namespace MeshFrame.Core;

public sealed class Model3D
{
	public readonly List<Prism> Prisms = new List<Prism>();

	public double MeshPitch;

	public V2[] MeshPoly;

	public double MeshZTop;

	public double MeshZBottom;

	public static Model3D Build(MeshFramePlan plan)
	{
		Model3D model3D = new Model3D();
		if (plan == null)
		{
			return model3D;
		}
		for (int i = 0; i < plan.Bars.Count; i++)
		{
			model3D.AddBar(plan.Bars[i], i);
		}
		for (int j = 0; j < plan.Strips.Count; j++)
		{
			model3D.AddStrip(plan.Strips[j], j);
		}
		if (plan.Mesh != null && plan.Mesh.Kind != MeshKind.None && plan.Mesh.Polygon != null && plan.Mesh.Polygon.Length >= 3)
		{
			V2[] array = (V2[])plan.Mesh.Polygon.Clone();
			if (Geo.Area(array) < 0.0)
			{
				Array.Reverse(array);
			}
			model3D.Prisms.Add(new Prism
			{
				Poly = array,
				Z0 = plan.Mesh.ZBottom,
				Z1 = 0.0,
				Kind = PartKind.Mesh,
				Index = 0
			});
			model3D.MeshPoly = array;
			model3D.MeshPitch = plan.Mesh.Pitch;
			model3D.MeshZTop = 0.0;
			model3D.MeshZBottom = plan.Mesh.ZBottom;
		}
		return model3D;
	}

	private static List<V2> Rect(V2 o, V2 d, V2 n, double s0, double s1, double l0, double l1)
	{
		return new List<V2>
		{
			o + d * s0 + n * l0,
			o + d * s1 + n * l0,
			o + d * s1 + n * l1,
			o + d * s0 + n * l1
		};
	}

	private void AddBar(PlanBar b, int idx)
	{
		V2 v = (b.End - b.Start).Unit();
		V2 v2 = v.Left();
		double length = (b.End - b.Start).Length;
		double thk = b.Thk;
		V2 keep = b.Start + v * (length / 2.0) + v2 * (b.LegA / 2.0);
		PartKind kind = ((b.Role == BarRole.Stiffener) ? PartKind.Stiffener : PartKind.FrameBar);
		List<V2> r = Rect(b.Start, v, v2, 0.0, length, 0.0, b.LegA);
		r = ClipEnds(r, b, keep);
		foreach (V2[] notch in b.Notches)
		{
			if (r == null)
			{
				break;
			}
			r = ClipHalfPlane(r, notch[1], notch[2] - notch[1], keep);
		}
		Add(r, b.ZRef, b.ZRef + thk, kind, idx);
		if (b.LegB > thk + 1E-09)
		{
			List<V2> r2 = Rect(b.Start, v, v2, 0.0, length, 0.0, thk);
			r2 = ClipEnds(r2, b, keep);
			Add(r2, b.ZRef + thk, b.ZRef + b.LegB, kind, idx);
		}
	}

	private void AddStrip(PlanStrip s, int idx)
	{
		V2 v = (s.End - s.Start).Unit();
		V2 n = v.Left();
		double length = (s.End - s.Start).Length;
		V2 keep = s.Start + v * (length / 2.0);
		// Native Fitting extends the profile to its cut plane. The nominal beam endpoints
		// are on the offset centre line, not at the outer miter tips.
		double start=0,end=length;
		ExtendToCut(s.Start,v,n,-s.Width/2,s.Width/2,s.Start,s.CutStart,ref start,ref end,true);
		ExtendToCut(s.Start,v,n,-s.Width/2,s.Width/2,s.End,s.CutEnd,ref start,ref end,false);
		List<V2> list = Rect(s.Start, v, n, start, end, (0.0 - s.Width) / 2.0, s.Width / 2.0);
		if (s.CutStart.HasValue)
		{
			list = ClipHalfPlane(list, s.Start, s.CutStart.Value, keep);
		}
		if (list != null && s.CutEnd.HasValue)
		{
			list = ClipHalfPlane(list, s.End, s.CutEnd.Value, keep);
		}
		Add(list, s.ZRef - s.Thk, s.ZRef, PartKind.Strip, idx);
	}

	private static void ExtendToCut(V2 origin,V2 direction,V2 normal,double lateral0,double lateral1,V2 cutOrigin,V2? cutDirection,ref double start,ref double end,bool atStart)
	{
		if(!cutDirection.HasValue)return;
		V2 cut=cutDirection.Value;
		double denominator=cut.Cross(direction);
		if(Math.Abs(denominator)<1e-10)return;
		foreach(double lateral in new[]{lateral0,lateral1}) {
			double station=cut.Cross(cutOrigin-origin-normal*lateral)/denominator;
			if(atStart)start=Math.Min(start,station);else end=Math.Max(end,station);
		}
	}

	private static List<V2> ClipEnds(List<V2> r, PlanBar b, V2 keep)
	{
		if (b.CutStart.HasValue)
		{
			r = ClipHalfPlane(r, b.Start, b.CutStart.Value, keep);
		}
		if (r != null && b.CutEnd.HasValue)
		{
			r = ClipHalfPlane(r, b.End, b.CutEnd.Value, keep);
		}
		return r;
	}

	private void Add(List<V2> poly, double z0, double z1, PartKind kind, int idx)
	{
		if (poly == null || poly.Count < 3)
		{
			return;
		}
		var clean=new List<V2>();
		foreach(V2 p in poly)if(clean.Count==0||(p-clean[clean.Count-1]).Length>1e-7)clean.Add(p);
		if(clean.Count>1&&(clean[0]-clean[clean.Count-1]).Length<1e-7)clean.RemoveAt(clean.Count-1);
		if(clean.Count<3)return;
		V2[] array = clean.ToArray();
		double num = Geo.Area(array);
		if (!(Math.Abs(num) < 1E-09) && !(z1 - z0 < 1E-09))
		{
			if (num < 0.0)
			{
				Array.Reverse(array);
			}
			Prisms.Add(new Prism
			{
				Poly = array,
				Z0 = z0,
				Z1 = z1,
				Kind = kind,
				Index = idx
			});
		}
	}

	public static List<V2> ClipHalfPlane(List<V2> poly, V2 p, V2 dir, V2 keep)
	{
		double num = ((dir.Cross(keep - p) >= 0.0) ? 1 : (-1));
		List<V2> list = new List<V2>();
		for (int i = 0; i < poly.Count; i++)
		{
			V2 v = poly[i];
			V2 v2 = poly[(i + 1) % poly.Count];
			double num2 = num * dir.Cross(v - p);
			double num3 = num * dir.Cross(v2 - p);
			if (num2 >= 0.0)
			{
				list.Add(v);
			}
			if ((num2 > 0.0 && num3 < 0.0) || (num2 < 0.0 && num3 > 0.0))
			{
				list.Add(v + (v2 - v) * (num2 / (num2 - num3)));
			}
		}
		if (list.Count >= 3)
		{
			return list;
		}
		return null;
	}

	public static List<int[]> Triangulate(V2[] p)
	{
		List<int[]> list = new List<int[]>();
		List<int> list2 = new List<int>();
		for (int i = 0; i < p.Length; i++)
		{
			list2.Add(i);
		}
		int num = 0;
		while (list2.Count > 3 && num++ < 10000)
		{
			bool flag = false;
			for (int j = 0; j < list2.Count; j++)
			{
				int num2 = list2[(j + list2.Count - 1) % list2.Count];
				int num3 = list2[j];
				int num4 = list2[(j + 1) % list2.Count];
				V2 v = p[num2];
				V2 v2 = p[num3];
				V2 v3 = p[num4];
				if ((v2 - v).Cross(v3 - v2) <= 1E-12)
				{
					continue;
				}
				bool flag2 = true;
				foreach (int item in list2)
				{
					if (item != num2 && item != num3 && item != num4 && InTri(p[item], v, v2, v3))
					{
						flag2 = false;
						break;
					}
				}
				if (flag2)
				{
					list.Add(new int[3] { num2, num3, num4 });
					list2.RemoveAt(j);
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				list.Add(new int[3]
				{
					list2[0],
					list2[1],
					list2[2]
				});
				list2.RemoveAt(1);
			}
		}
		if (list2.Count == 3)
		{
			list.Add(new int[3]
			{
				list2[0],
				list2[1],
				list2[2]
			});
		}
		return list;
	}

	private static bool InTri(V2 q, V2 a, V2 b, V2 c)
	{
		double num = (b - a).Cross(q - a);
		double num2 = (c - b).Cross(q - b);
		double num3 = (a - c).Cross(q - c);
		if (num > -1E-09 && num2 > -1E-09)
		{
			return num3 > -1E-09;
		}
		return false;
	}
}
