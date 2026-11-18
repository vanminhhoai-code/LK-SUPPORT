using System;
using System.Collections.Generic;
using System.Globalization;
using MeshFrame.Core;

namespace MeshFrame.Tekla;

public static class TeklaPicker
{
	public static string Prompt(int index, double lastLen, double perimeter)
	{
		string text = ((index > 2) ? (" | doan truoc " + lastLen.ToString("0.0", CultureInfo.InvariantCulture) + " mm | chu vi " + perimeter.ToString("0.0", CultureInfo.InvariantCulture) + " mm") : "");
		return "MeshFrame: pick diem P" + index + text + " | chuot giua = ket thuc (>= 3 diem)";
	}

	public static PickResult PickPolygon(TeklaApi api, object model)
	{
		object picker = api.New(api.ModelType("UI.Picker"));
		object target = api.Call(api.Call(model, "GetWorkPlaneHandler"), "GetCurrentTransformationPlane");
		object target2 = api.Get(target, "TransformationMatrixToGlobal");
		PickResult pickResult = new PickResult();
		object drawer = null;
		try
		{
			drawer = api.New(api.ModelType("UI.GraphicsDrawer"));
		}
		catch (Exception)
		{
			pickResult.DrawNote = "Tekla bản này không có GraphicsDrawer: số đo chỉ hiện trên thanh trạng thái.";
		}
		List<object> ids = new List<object>();
		pickResult.Cleanup = () =>
		{
			Remove(api, drawer, ids);
			ids.Clear();
		};
		List<V3> list = new List<V3>();
		List<object> list2 = new List<object>();
		double num = 0.0;
		double num2 = 0.0;
		bool useRef = true;
		while (true)
		{
			int count = list.Count;
			object obj;
			try
			{
				string prompt = Prompt(count + 1, num2, num);
				obj = PickOne(api, picker, prompt, (count > 0) ? list2[count - 1] : null, ref useRef);
			}
			catch (Exception innerException)
			{
				while (innerException.InnerException != null)
				{
					innerException = innerException.InnerException;
				}
				if (IsApiError(innerException))
				{
					throw;
				}
				pickResult.StopReason = innerException.GetType().Name + ": " + innerException.Message;
				break;
			}
			object target3 = api.Call(target2, "Transform", obj);
			V3 v = new V3(api.GetDouble(target3, "X"), api.GetDouble(target3, "Y"), api.GetDouble(target3, "Z"));
			if (count > 0)
			{
				num2 = (v - list[count - 1]).Length;
				num += num2;
			}
			list.Add(v);
			list2.Add(obj);
			pickResult.Picked = list.Count;
			if (drawer != null)
			{
				try
				{
					Draw(api, drawer, ids, list2, count, num2);
				}
				catch (Exception ex2)
				{
					drawer = null;
					pickResult.DrawNote = "Không vẽ được đồ họa tạm (" + ex2.GetType().Name + "): số đo chỉ hiện trên thanh trạng thái.";
				}
			}
		}
		if (list.Count >= 3)
		{
			pickResult.Points = list.ToArray();
		}
		return pickResult;
	}

	public static bool IsApiError(Exception ex)
	{
		if (!(ex is MissingMemberException) && !(ex is TypeLoadException) && !(ex is MethodAccessException))
		{
			return ex is InvalidCastException;
		}
		return true;
	}

	private static object PickOne(TeklaApi api, object picker, string prompt, object previous, ref bool useRef)
	{
		if ((previous != null) & useRef)
		{
			try
			{
				return api.Call(picker, "PickPoint", prompt, previous);
			}
			catch (MissingMethodException)
			{
				useRef = false;
			}
		}
		return api.Call(picker, "PickPoint", prompt);
	}

	private static void Draw(TeklaApi api, object drawer, List<object> ids, List<object> local, int k, double len)
	{
		object obj = api.New(api.ModelType("UI.Color"), 1.0, 0.0, 0.0);
		object obj2 = api.New(api.ModelType("UI.Color"), 0.0, 0.3, 1.0);
		double num = api.GetDouble(local[k], "X");
		double num2 = api.GetDouble(local[k], "Y");
		double num3 = api.GetDouble(local[k], "Z");
		ids.Add(api.Call(drawer, "DrawLineSegment", api.Point(num - 60.0, num2, num3), api.Point(num + 60.0, num2, num3), obj));
		ids.Add(api.Call(drawer, "DrawLineSegment", api.Point(num, num2 - 60.0, num3), api.Point(num, num2 + 60.0, num3), obj));
		ids.Add(api.Call(drawer, "DrawText", api.Point(num + 60.0, num2 + 60.0, num3), "P" + (k + 1), obj));
		if (k > 0)
		{
			double num4 = api.GetDouble(local[k - 1], "X");
			double num5 = api.GetDouble(local[k - 1], "Y");
			double num6 = api.GetDouble(local[k - 1], "Z");
			ids.Add(api.Call(drawer, "DrawLineSegment", api.Point(num4, num5, num6), api.Point(num, num2, num3), obj2));
			ids.Add(api.Call(drawer, "DrawText", api.Point((num4 + num) / 2.0, (num5 + num2) / 2.0, (num6 + num3) / 2.0), len.ToString("0.0", CultureInfo.InvariantCulture) + " mm", obj2));
		}
	}

	private static void Remove(TeklaApi api, object drawer, List<object> ids)
	{
		if (drawer == null || ids.Count == 0)
		{
			return;
		}
		try
		{
			List<int> list = new List<int>();
			foreach (object id in ids)
			{
				if (id is int)
				{
					list.Add((int)id);
				}
			}
			api.Call(drawer, "RemoveTemporaryGraphicsObjects", list);
		}
		catch (Exception)
		{
		}
	}
}
