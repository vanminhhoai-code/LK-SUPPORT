using System;
using System.Collections.Generic;
using System.Globalization;

namespace MeshFrame.Core;

public static class Annotations
{
	private static string N0(double v)
	{
		return v.ToString("0.##", CultureInfo.InvariantCulture);
	}

	public static List<DimItem> Build(MeshFramePlan plan)
	{
		List<DimItem> list = new List<DimItem>();
		if (plan == null || plan.Polygon == null || plan.Polygon.Length < 3)
		{
			return list;
		}
		V2[] polygon = plan.Polygon;
		int num = polygon.Length;
		double num2 = double.MaxValue;
		double num3 = double.MinValue;
		double num4 = double.MaxValue;
		double num5 = double.MinValue;
		V2[] array = polygon;
		for (int i = 0; i < array.Length; i++)
		{
			V2 v = array[i];
			num2 = Math.Min(num2, v.X);
			num3 = Math.Max(num3, v.X);
			num4 = Math.Min(num4, v.Y);
			num5 = Math.Max(num5, v.Y);
		}
		double num6 = Math.Max(30.0, 0.045 * Math.Max(num3 - num2, num5 - num4));
		for (int j = 0; j < num; j++)
		{
			list.Add(new DimItem
			{
				Kind = DimKind.Point,
				Index = j,
				A = polygon[j],
				B = polygon[j],
				TextPos = polygon[j],
				Text = "P" + (j + 1),
				Value = 0.0
			});
		}
		for (int k = 0; k < num; k++)
		{
			V2 v2 = polygon[k];
			V2 v3 = polygon[(k + 1) % num];
			V2 v4 = (v3 - v2).Unit().Left() * -1.0;
			double length = (v3 - v2).Length;
			list.Add(new DimItem
			{
				Kind = DimKind.Edge,
				Index = k,
				A = v2,
				B = v3,
				TextPos = (v2 + v3) * 0.5 + v4 * (num6 * 0.6),
				Text = k + 1 + ": " + N0(length),
				Value = length,
				Editable = true,
				IsBaseEdge = (k == plan.BaseEdge)
			});
		}
		if (plan.StiffTims.Count > 0 && plan.Cells.Count >= 2)
		{
			V2 baseDir = plan.BaseDir;
			V2 v5 = baseDir.Left();
			V2 baseOrigin = plan.BaseOrigin;
			double num7 = 0.0;
			array = polygon;
			foreach (V2 v6 in array)
			{
				num7 = Math.Max(num7, 0.0 - (v6 - baseOrigin).Dot(v5));
			}
			List<double> list2 = new List<double> { plan.BaseMin };
			list2.AddRange(plan.StiffTims);
			list2.Add(plan.BaseMax);
			for (int l = 0; l + 1 < list2.Count; l++)
			{
				int num8 = l % 2;
				double num9 = num7 + num6 * (1.2 + (double)num8 * 1.1);
				V2 v7 = baseOrigin + baseDir * list2[l] + v5 * (0.0 - num9);
				V2 v8 = baseOrigin + baseDir * list2[l + 1] + v5 * (0.0 - num9);
				DimItem dimItem = new DimItem();
				dimItem.Kind = DimKind.Cell;
				dimItem.Index = l;
				dimItem.A = v7;
				dimItem.B = v8;
				dimItem.TextPos = (v7 + v8) * 0.5 + v5 * ((0.0 - num6) * 0.35);
				dimItem.Text = N0(list2[l + 1] - list2[l]);
				dimItem.Value = list2[l + 1] - list2[l];
				dimItem.Editable = true;
				dimItem.Level = num8;
				dimItem.Extension = new V2[4]
				{
					baseOrigin + baseDir * list2[l] + v5 * (0.0 - num7),
					v7,
					baseOrigin + baseDir * list2[l + 1] + v5 * (0.0 - num7),
					v8
				};
				DimItem item = dimItem;
				list.Add(item);
			}
		}
		return list;
	}
}
