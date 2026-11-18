using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace MeshFrame.Tekla;

public static class TeklaProbe
{
	public static string OutputPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeshFrame", "selected-object.txt");

	public static string DumpSelected(TeklaApi api)
	{
		object target = api.New(api.ModelType("UI.ModelObjectSelector"));
		object target2 = api.Call(target, "GetSelectedObjects");
		StringBuilder stringBuilder = new StringBuilder();
		int num = 0;
		while ((bool)api.Call(target2, "MoveNext") && num < 5)
		{
			object o = api.Get(target2, "Current");
			num++;
			stringBuilder.AppendLine("=== Đối tượng " + num + ": " + o.GetType().FullName);
			stringBuilder.Append(api.Invoke(() => Dump(o, 0)));
			stringBuilder.AppendLine();
		}
		if (num == 0)
		{
			return "Chưa chọn đối tượng nào trong Tekla. Chọn lưới mẫu (Reinforcement Mesh) rồi bấm lại.";
		}
		Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
		File.WriteAllText(OutputPath, stringBuilder.ToString(), Encoding.UTF8);
		return "Đã ghi thuộc tính " + num + " đối tượng vào:\n" + OutputPath + "\n\nGửi file này cho người hỗ trợ.";
	}

	internal static string Dump(object o, int depth)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string text = new string(' ', depth * 2);
		PropertyInfo[] properties = o.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
		foreach (PropertyInfo propertyInfo in properties)
		{
			if (propertyInfo.GetIndexParameters().Length == 0)
			{
				string text2;
				try
				{
					text2 = Format(propertyInfo.GetValue(o, null), depth);
				}
				catch (Exception ex)
				{
					text2 = "(không đọc được: " + (ex.InnerException ?? ex).GetType().Name + ")";
				}
				stringBuilder.AppendLine(text + propertyInfo.Name + " = " + text2);
			}
		}
		FieldInfo[] fields = o.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			string text3;
			try
			{
				text3 = Format(fieldInfo.GetValue(o), depth);
			}
			catch (Exception ex2)
			{
				text3 = "(không đọc được: " + ex2.GetType().Name + ")";
			}
			stringBuilder.AppendLine(text + fieldInfo.Name + " = " + text3);
		}
		return stringBuilder.ToString();
	}

	internal static string Format(object v, int depth)
	{
		if (v == null)
		{
			return "(null)";
		}
		Type type = v.GetType();
		if (v is string || type.IsPrimitive || type.IsEnum || v is decimal)
		{
			return Convert.ToString(v, CultureInfo.InvariantCulture);
		}
		if (v is IEnumerable)
		{
			StringBuilder stringBuilder = new StringBuilder("[");
			int num = 0;
			foreach (object item in (IEnumerable)v)
			{
				if (num++ > 0)
				{
					stringBuilder.Append("; ");
				}
				if (num > 25)
				{
					stringBuilder.Append("...");
					break;
				}
				stringBuilder.Append(Format(item, depth + 1));
			}
			return stringBuilder.Append("]").ToString();
		}
		FieldInfo field = type.GetField("X");
		FieldInfo field2 = type.GetField("Y");
		FieldInfo field3 = type.GetField("Z");
		if (field != null && field2 != null && field3 != null)
		{
			return "(" + Format(field.GetValue(v), depth) + ", " + Format(field2.GetValue(v), depth) + ", " + Format(field3.GetValue(v), depth) + ")";
		}
		if (depth >= 1)
		{
			return type.Name;
		}
		return type.Name + "{\n" + Dump(v, depth + 1) + new string(' ', depth * 2) + "}";
	}
}
