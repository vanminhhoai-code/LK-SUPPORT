using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace MeshFrame.Core;

public static class MeshFramePlanner
{
	private static string F(double d)
	{
		return d.ToString("0.#", CultureInfo.InvariantCulture);
	}

	public static MeshFramePlan Plan(MeshFrameSettings s, V2[] polyIn)
	{
		MeshFramePlan meshFramePlan = new MeshFramePlan
		{
			AsmPrefix = s.AsmPrefix,
			AsmStartNo = s.AsmStartNo
		};
		meshFramePlan.Errors.AddRange(s.Validate());
		if (polyIn == null || polyIn.Length < 3)
		{
			meshFramePlan.Errors.Add("Cần ít nhất 3 điểm.");
		}
		else if (!Geo.IsSimple(polyIn))
		{
			meshFramePlan.Errors.Add("Các điểm không được tự cắt, chạm nhau hoặc trùng nhau.");
		}
		if (meshFramePlan.Errors.Count > 0)
		{
			meshFramePlan.Polygon = polyIn;
			return meshFramePlan;
		}
		V2[] array = (V2[])polyIn.Clone();
		if (Geo.Area(array) < 0.0)
		{
			Array.Reverse(array);
		}
		meshFramePlan.Polygon = array;
		int num = array.Length;
		if (Geo.Area(array) < 1.0)
		{
			meshFramePlan.Errors.Add("Diện tích khung quá nhỏ.");
			return meshFramePlan;
		}
		meshFramePlan.Placement = new PlanPlacement
		{
			VPlane = PlanPlacement.Name(s.VPlane),
			VRotation = PlanPlacement.Name(s.VRotation),
			VDepth = PlanPlacement.Name(s.VDepth),
			StripPlane = PlanPlacement.Name(s.StripPlane),
			StripRotation = PlanPlacement.Name(s.StripRotation),
			StripDepth = PlanPlacement.Name(s.StripDepth),
			FlipMiterCut = s.FlipMiterCut,
			AutoPlacement = s.AutoPlacement,
			VLateral = s.VLateral,
			StripLateral = s.StripLateral
		};
		WarnNonAscii(s, meshFramePlan);
		double vThk = s.VThk;
		_ = s.StripThk;
		double stripWidth = s.StripWidth;
		double num2 = 2.0 * s.MeshWireDia;
		double num3 = 0.0 - num2;
		bool[] array2 = null;
		if (s.Corner == CornerKind.OneThrough)
		{
			int num4 = ((s.StiffBaseEdge >= 1 && s.StiffBaseEdge <= num) ? (s.StiffBaseEdge - 1) : LongestEdge(array));
			array2 = ThroughEdges(array, num4);
			int num5 = 0;
			bool[] array3 = array2;
			for (int i = 0; i < array3.Length; i++)
			{
				if (array3[i])
				{
					num5++;
				}
			}
			meshFramePlan.Notes.Add("Nối góc: " + num5 + " cạnh chạy dài (song song cạnh " + (num4 + 1) + "), " + (num - num5) + " cạnh dừng/cắt gọt.");
		}
		V2[] array4 = Geo.Offset(array, s.MeshGap);
		if (!Geo.OffsetValid(array, array4))
		{
			meshFramePlan.Errors.Add("Khung quá nhỏ hoặc hẹp so với khoảng lùi lưới.");
			return meshFramePlan;
		}
		string rebarGrade = ((!string.IsNullOrEmpty(s.RebarGrade)) ? s.RebarGrade : s.MeshGrade);
		string rebarSize = ((!string.IsNullOrEmpty(s.RebarSize)) ? s.RebarSize : s.MeshWireDia.ToString("0.###", CultureInfo.InvariantCulture));
		meshFramePlan.Mesh = new PlanMesh
		{
			Polygon = array4,
			ZBottom = 0.0 - num2,
			Thickness = num2,
			WireDia = s.MeshWireDia,
			Pitch = s.MeshPitch,
			WeightKgPerM2 = s.MeshWeightKgPerM2,
			Grade = s.MeshGrade,
			Name = s.MeshName,
			Prefix = s.MeshPrefix,
			RebarGrade = rebarGrade,
			RebarSize = rebarSize,
			Class = s.MeshClass,
			StartNo = s.MeshStartNo,
			Kind = s.MeshKind,
			IsRectangle = IsRectangle(array4)
		};
		if (s.MeshKind == MeshKind.None)
		{
			meshFramePlan.Mesh = null;
		}
		for (int j = 0; j < num; j++)
		{
			EdgeEnds(array, j, 0.0, 0.0, vThk, array2, out var s2, out var e, out var cutS, out var cutE);
			if ((e - s2).Dot(array[(j + 1) % num] - array[j]) <= 1.0)
			{
				meshFramePlan.Errors.Add("Cạnh " + (j + 1) + " quá ngắn so với V khung.");
				return meshFramePlan;
			}
			PlanBar planBar = new PlanBar
			{
				Role = BarRole.Frame,
				Start = s2,
				End = e,
				CutStart = cutS,
				CutEnd = cutE,
				LegA = s.VLegA,
				LegB = s.VLegB,
				Thk = vThk,
				ZRef = 0.0,
				Material = s.VMaterial,
				Name = s.VName,
				Class = s.VClass,
				Prefix = s.VPrefix,
				StartNo = s.VStartNo,
				ProfileOverride = s.VProfile
			};
			if (array2 != null)
			{
				int num6 = (j + num - 1) % num;
				int num7 = (j + 1) % num;
				V2 perp = (e - s2).Unit();
				V2 nLeft = perp.Left();
				if (!array2[j] && array2[num6])
				{
					planBar.Notches.Add(NotchQuad(array, s2, perp, nLeft, num6, vThk, s.VLegA));
				}
				if (!array2[j] && array2[num7])
				{
					planBar.Notches.Add(NotchQuad(array, e, perp, nLeft, num7, vThk, s.VLegA));
				}
			}
			meshFramePlan.Bars.Add(planBar);
		}
		if (s.UseStrip)
		{
			V2[] off = Geo.Offset(array, 0.0 + stripWidth);
			if (!Geo.OffsetValid(array, off))
			{
				meshFramePlan.Errors.Add("Khung quá nhỏ hoặc hẹp so với nẹp (nẹp phủ tới " + F(0.0 + stripWidth) + " mm tính từ mép ngoài).");
				return meshFramePlan;
			}
			for (int k = 0; k < num; k++)
			{
				EdgeEnds(array, k, 0.0 + stripWidth / 2.0, 0.0, 0.0 + stripWidth, array2, out var s3, out var e2, out var cutS2, out var cutE2);
				if ((e2 - s3).Dot(array[(k + 1) % num] - array[k]) <= 1.0)
				{
					meshFramePlan.Errors.Add("Cạnh " + (k + 1) + " quá ngắn so với nẹp.");
					return meshFramePlan;
				}
				meshFramePlan.Strips.Add(NewStrip(s, s3, e2, cutS2, cutE2, num3, onStiff: false));
			}
		}
		AddStiffeners(meshFramePlan, s, array, vThk, num3, s.StripOffset());
		return meshFramePlan;
	}

	private static void AddStiffeners(MeshFramePlan plan, MeshFrameSettings s, V2[] poly, double tV, double zStrip, double d0s)
	{
		if (s.StiffMode == StiffMode.None)
		{
			plan.Notes.Add("Gia cường: không dùng.");
			return;
		}
		int num = poly.Length;
		int num2 = ((s.StiffBaseEdge >= 1) ? (s.StiffBaseEdge - 1) : LongestEdge(poly));
		if (num2 >= num)
		{
			plan.Errors.Add("Cạnh gốc gia cường " + s.StiffBaseEdge + " không tồn tại (khung có " + num + " cạnh).");
			return;
		}
		plan.BaseEdge = num2;
		V2 v = (poly[(num2 + 1) % num] - poly[num2]).Unit();
		V2 v2 = v.Left();
		double num3 = double.MaxValue;
		double num4 = double.MinValue;
		for (int i = 0; i < poly.Length; i++)
		{
			double val = (poly[i] - poly[num2]).Dot(v);
			num3 = Math.Min(num3, val);
			num4 = Math.Max(num4, val);
		}
		double num5 = num4 - num3;
		List<double> list = CellWidths(s, num5, plan);
		if (plan.Errors.Count > 0)
		{
			return;
		}
		plan.Cells = list;
		plan.BaseOrigin = poly[num2];
		plan.BaseDir = v;
		plan.BaseMin = num3;
		plan.BaseMax = num4;
		double num6 = 0.0;
		for (int j = 0; j < list.Count - 1; j++)
		{
			num6 += list[j];
			plan.StiffTims.Add(num3 + num6);
		}
		if (list.Count <= 1)
		{
			plan.Notes.Add("Gia cường: không cần - kích thước dọc cạnh gốc " + F(num5) + " mm " + ((s.StiffMode == StiffMode.Equal) ? ("≤ " + F(s.StiffMax) + " mm (ô tối đa).") : "chỉ có 1 ô."));
			return;
		}
		double stripWidth = s.StripWidth;
		double vLegA = s.VLegA;
		V2[] array = Geo.Offset(poly, tV);
		if (!Geo.OffsetValid(poly, array))
		{
			plan.Errors.Add("Khung quá nhỏ hoặc hẹp để đặt gia cường.");
			return;
		}
		V2[] array2 = (s.UseStrip ? Geo.Offset(poly, stripWidth) : null);
		if (array2 != null && !Geo.OffsetValid(poly, array2))
		{
			array2 = null;
		}
		double num7 = 2.0 * vLegA;
		int num8 = list.Count - 1;
		int num9 = 0;
		int num10 = 0;
		int num11 = 0;
		double num12 = 0.0;
		for (int k = 0; k < num8; k++)
		{
			num12 += list[k];
			V2 v3 = poly[num2] + v * (num3 + num12 - vLegA / 2.0);
			foreach (Geo.LineSeg item in Geo.ClipLine(array, v3, v2))
			{
				V2 v4 = v3 + v2 * item.T0;
				V2 v5 = v3 + v2 * item.T1;
				if ((v5 - v4).Length < num7)
				{
					num11++;
					continue;
				}
				V2 v6 = v5;
				V2 v7 = v4;
				int e = item.E1;
				int e2 = item.E0;
				V2 nLeft = (v7 - v6).Unit().Left();
				PlanBar planBar = new PlanBar
				{
					Role = BarRole.Stiffener,
					Start = v6,
					End = v7,
					LegA = s.VLegA,
					LegB = s.VLegB,
					Thk = tV,
					ZRef = 0.0,
					Material = s.SMaterial,
					Name = s.SName,
					Class = s.SClass,
					Prefix = s.SPrefix,
					StartNo = s.SStartNo,
					ProfileOverride = s.VProfile
				};
				planBar.Notches.Add(NotchQuad(poly, v6, v2, nLeft, e, tV, vLegA));
				planBar.Notches.Add(NotchQuad(poly, v7, v2, nLeft, e2, tV, vLegA));
				plan.Bars.Add(planBar);
				num9++;
			}
			if (array2 == null)
			{
				continue;
			}
			V2 v8 = v3 + v * (d0s + stripWidth / 2.0);
			foreach (Geo.LineSeg item2 in Geo.ClipLine(array2, v8, v2))
			{
				V2 v9 = v8 + v2 * item2.T0;
				V2 v10 = v8 + v2 * item2.T1;
				if (!((v10 - v9).Length <= 1.0))
				{
					plan.Strips.Add(NewStrip(s, v9, v10, null, null, zStrip, onStiff: true));
					num10++;
				}
			}
		}
		if (num9 == 0)
		{
			plan.Warnings.Add("Không tạo được gia cường: " + num8 + " đường chia đều ngắn hơn " + F(num7) + " mm hoặc nằm ngoài khung.");
		}
		else if (num11 > 0)
		{
			plan.Warnings.Add("Bỏ " + num11 + " đoạn gia cường ngắn hơn " + F(num7) + " mm (không đủ chỗ cắt khuyết).");
		}
		if (list[0] < tV + vLegA / 2.0 + 1.0 || list[list.Count - 1] < tV + vLegA / 2.0 + 1.0)
		{
			plan.Warnings.Add("Ô đầu hoặc ô cuối (tính tới tim V gia cường) quá hẹp: V gia cường chồng lên V khung (cần ≥ " + F(tV + vLegA / 2.0) + " mm).");
		}
		plan.Notes.Add("Gia cường: " + num9 + " thanh theo " + list.Count + " ô (đo tới tim V), vuông góc cạnh " + (num2 + 1) + ".");
		if (!s.UseStrip)
		{
			return;
		}
		for (int l = 0; l < list.Count; l++)
		{
			if (list[l] <= stripWidth + vLegA)
			{
				plan.Warnings.Add("Ô " + (l + 1) + " (" + F(list[l]) + " mm) rất hẹp so với nẹp/gia cường.");
			}
		}
	}

	private static int LongestEdge(V2[] p)
	{
		int result = 0;
		double num = -1.0;
		for (int i = 0; i < p.Length; i++)
		{
			double length = (p[(i + 1) % p.Length] - p[i]).Length;
			if (length > num)
			{
				num = length;
				result = i;
			}
		}
		return result;
	}

	private static V2[] NotchQuad(V2[] poly, V2 end, V2 perp, V2 nLeft, int edge, double tV, double legA)
	{
		V2 o = end + nLeft * -1.0;
		V2 o2 = end + nLeft * legA;
		Geo.OffsetLine(poly, edge, tV / 2.0, out var origin, out var dir);
		Geo.OffsetLine(poly, edge, legA, out var origin2, out var dir2);
		Geo.Intersect(o, perp, origin, dir, out var r);
		Geo.Intersect(o, perp, origin2, dir2, out var r2);
		Geo.Intersect(o2, perp, origin2, dir2, out var r3);
		Geo.Intersect(o2, perp, origin, dir, out var r4);
		return new V2[4] { r, r2, r3, r4 };
	}

	private static List<double> CellWidths(MeshFrameSettings s, double total, MeshFramePlan plan)
	{
		List<double> list = new List<double>();
		if (s.StiffMode == StiffMode.Equal)
		{
			int num = (int)Math.Ceiling(total / s.StiffMax - 1E-09);
			if (num < 1)
			{
				num = 1;
			}
			for (int i = 0; i < num; i++)
			{
				list.Add(total / (double)num);
			}
			return list;
		}
		CellParser.TryParse(s.StiffCells, out var cells, out var _);
		double num2 = 0.0;
		foreach (double item in cells)
		{
			num2 += item;
		}
		if (num2 > total + 0.5)
		{
			double num3 = 0.0;
			int num4 = 0;
			foreach (double item2 in cells)
			{
				if (total - num3 <= 0.5)
				{
					break;
				}
				list.Add(Math.Min(item2, total - num3));
				num3 += item2;
				num4++;
			}
			plan.Warnings.Add("Tổng bề rộng các ô (" + F(num2) + ") lớn hơn kích thước khung dọc cạnh gốc (" + F(total) + "): cắt ở mép khung, dùng " + num4 + "/" + cells.Count + " ô.");
			return list;
		}
		list.AddRange(cells);
		if (num2 < total - 0.5)
		{
			list.Add(total - num2);
			plan.Warnings.Add("Tổng các ô nhỏ hơn khung, thêm ô cuối " + F(total - num2) + " mm.");
		}
		return list;
	}

	private static bool IsRectangle(V2[] p)
	{
		if (p.Length != 4)
		{
			return false;
		}
		for (int i = 0; i < 4; i++)
		{
			V2 v = p[(i + 1) % 4] - p[i];
			V2 b = p[(i + 2) % 4] - p[(i + 1) % 4];
			if (Math.Abs(v.Dot(b)) > 1E-06 * v.Length * b.Length)
			{
				return false;
			}
		}
		return true;
	}

	private static bool[] ThroughEdges(V2[] poly, int baseEdge)
	{
		int num = poly.Length;
		V2 v = (poly[(baseEdge + 1) % num] - poly[baseEdge]).Unit();
		bool[] array = new bool[num];
		for (int i = 0; i < num; i++)
		{
			V2 v2 = (poly[(i + 1) % num] - poly[i]).Unit();
			array[i] = Math.Abs(v.X * v2.Y - v.Y * v2.X) < 1E-06;
		}
		return array;
	}

	private static void EdgeEnds(V2[] poly, int k, double c, double a, double b, bool[] through, out V2 s, out V2 e, out V2? cutS, out V2? cutE)
	{
		int num = poly.Length;
		int other = (k + num - 1) % num;
		int num2 = (k + 1) % num;
		Vertex(poly, k, other, k, c, a, b, through, out s, out cutS);
		Vertex(poly, k, num2, num2, c, a, b, through, out e, out cutE);
	}

	private static void Vertex(V2[] poly, int k, int other, int vertex, double c, double a, double b, bool[] through, out V2 pt, out V2? cut)
	{
		if (through == null || through[k] == through[other])
		{
			V2[] array = Geo.Offset(poly, c);
			pt = array[vertex];
			cut = Geo.Bisector(poly, vertex);
		}
		else
		{
			double d = (through[k] ? a : b);
			Geo.OffsetLine(poly, k, c, out var origin, out var dir);
			Geo.OffsetLine(poly, other, d, out var origin2, out var dir2);
			Geo.Intersect(origin, dir, origin2, dir2, out pt);
			cut = null;
		}
	}

	private static PlanStrip NewStrip(MeshFrameSettings s, V2 a, V2 b, V2? ca, V2? cb, double z, bool onStiff)
	{
		return new PlanStrip
		{
			Start = a,
			End = b,
			CutStart = ca,
			CutEnd = cb,
			Width = s.StripWidth,
			Thk = s.StripThk,
			ZRef = z,
			Material = s.StripMaterial,
			Name = s.StripName,
			Class = s.StripClass,
			Prefix = s.StripPrefix,
			StartNo = s.StripStartNo,
			OnStiffener = onStiff,
			ProfileOverride = s.StripProfile
		};
	}

	private static void WarnNonAscii(MeshFrameSettings s, MeshFramePlan plan)
	{
		List<string> list = new List<string>();
		FieldInfo[] fields = typeof(MeshFrameSettings).GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			if (fieldInfo.FieldType != typeof(string) || fieldInfo.Name == "StiffCells")
			{
				continue;
			}
			string text = (string)fieldInfo.GetValue(s);
			if (text == null)
			{
				continue;
			}
			string text2 = text;
			for (int j = 0; j < text2.Length; j++)
			{
				if (text2[j] > '\u007f')
				{
					list.Add(fieldInfo.Name);
					break;
				}
			}
		}
		if (list.Count > 0)
		{
			plan.Warnings.Add("Có chữ có dấu/ký tự ngoài ASCII trong: " + string.Join(", ", list.ToArray()) + ". Tekla có thể hiển thị lỗi font, nên dùng không dấu.");
		}
	}
}
