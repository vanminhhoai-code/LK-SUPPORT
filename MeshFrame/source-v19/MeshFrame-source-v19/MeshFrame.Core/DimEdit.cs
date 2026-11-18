using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace MeshFrame.Core;

public static class DimEdit
{
	public static EditResult Apply(MeshFramePlan plan, Frame frame, MeshFrameSettings s, V3[] pts, DimKind kind, int index, double value)
	{
		EditResult editResult = new EditResult();
		if (value <= 0.0 || double.IsNaN(value) || double.IsInfinity(value))
		{
			editResult.Error = "Giá trị phải là số dương.";
			return editResult;
		}
		int anchor = Math.Max(0, Math.Min(pts.Length - 1, s.AnchorPoint - 1));
		V3[] array = (V3[])pts.Clone();
		MeshFrameSettings meshFrameSettings = Clone(s);
		switch (kind)
		{
		case DimKind.Edge:
		{
			int num5 = pts.Length;
			V3 v3 = pts[index];
			V3 v4 = pts[(index + 1) % num5];
			double length = (v4 - v3).Length;
			V3 dir = (v4 - v3).Unit();
			array = Shift(pts, v3, dir, length, value - length, anchor);
			break;
		}
		case DimKind.Cell:
		{
			if (plan == null || plan.Cells.Count < 2 || index < 0 || index >= plan.Cells.Count)
			{
				editResult.Error = "Cần ít nhất hai ô để bù khoảng cách mà giữ nguyên khung chính.";
				return editResult;
			}
			List<double> list = new List<double>(plan.Cells);
			V3 v = frame.U * plan.BaseDir.X + frame.V * plan.BaseDir.Y;
			V3 v2 = frame.ToWorld(plan.BaseOrigin, 0.0);
			double delta = value - list[index];
			// Cell edits redistribute the existing span; only explicit edge/XYZ edits resize the frame.
			double anchorStation=(pts[anchor]-v2).Dot(v);
			int balance=anchorStation<=(plan.BaseMin+plan.BaseMax)/2.0?list.Count-1:0;
			if(balance==index)balance=balance==0?list.Count-1:0;
			double remaining=list[balance]-delta;
			if(remaining<=1e-6||double.IsNaN(remaining)||double.IsInfinity(remaining)){
				editResult.Error="Không đủ chỗ: ô bù "+(balance+1)+" còn "+remaining.ToString("0.###",CultureInfo.InvariantCulture)+" mm. Khung chính giữ nguyên; giảm giá trị nhập hoặc sửa ô khác.";
				return editResult;
			}
			list[index] = value;
			list[balance]=remaining;
			editResult.Summary="Giữ tổng khung "+(plan.BaseMax-plan.BaseMin).ToString("0.###",CultureInfo.InvariantCulture)+" mm; ô "+(index+1)+" = "+value.ToString("0.###",CultureInfo.InvariantCulture)+" mm; ô bù "+(balance+1)+" = "+remaining.ToString("0.###",CultureInfo.InvariantCulture)+" mm. Các ô khác giữ nguyên.";
			meshFrameSettings.StiffBaseEdge = plan.BaseEdge + 1;
			meshFrameSettings.StiffMode = StiffMode.Custom;
			meshFrameSettings.StiffCells = FormatCells(list);
			break;
		}
		default:
			editResult.Error = "Số này không sửa được.";
			return editResult;
		}
		if (!MeshFrameBuilder.TryFrame(array, meshFrameSettings.AnchorPoint - 1, out var _, out var poly, out var error))
		{
			editResult.Error = error;
			return editResult;
		}
		MeshFramePlan meshFramePlan = MeshFramePlanner.Plan(meshFrameSettings, poly);
		if (meshFramePlan.Errors.Count > 0)
		{
			editResult.Error = string.Join(" ", meshFramePlan.Errors.ToArray());
			return editResult;
		}
		editResult.Ok = true;
		editResult.Points = array;
		editResult.Settings = meshFrameSettings;
		var changes = new List<string>();
		for (int i=0;i<pts.Length;i++) {
			double oldLength=(pts[(i+1)%pts.Length]-pts[i]).Length;
			double newLength=(array[(i+1)%array.Length]-array[i]).Length;
			if(Math.Abs(newLength-oldLength)>.001) changes.Add("cạnh "+(i+1)+": "+oldLength.ToString("0.##",CultureInfo.InvariantCulture)+" → "+newLength.ToString("0.##",CultureInfo.InvariantCulture));
		}
		if(changes.Count>0)editResult.Summary=string.Join("; ",changes);
		return editResult;
	}

	public static V3[] Shift(V3[] pts, V3 origin, V3 dir, double thr, double delta, int anchor)
	{
		bool[] array = new bool[pts.Length];
		for (int i = 0; i < pts.Length; i++)
		{
			array[i] = (pts[i] - origin).Dot(dir) >= thr - 1E-06;
		}
		bool flag = !array[anchor];
		V3[] array2 = (V3[])pts.Clone();
		for (int j = 0; j < pts.Length; j++)
		{
			if (array[j] == flag)
			{
				array2[j] = pts[j] + dir * (flag ? delta : (0.0 - delta));
			}
		}
		return array2;
	}

	public static string FormatCells(List<double> cells)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (double cell in cells)
		{
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(' ');
			}
			stringBuilder.Append(cell.ToString("0.###", CultureInfo.InvariantCulture));
		}
		return stringBuilder.ToString();
	}

	public static MeshFrameSettings Clone(MeshFrameSettings s)
	{
		MeshFrameSettings meshFrameSettings = new MeshFrameSettings();
		FieldInfo[] fields = typeof(MeshFrameSettings).GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			fieldInfo.SetValue(meshFrameSettings, fieldInfo.GetValue(s));
		}
		return meshFrameSettings;
	}
}
