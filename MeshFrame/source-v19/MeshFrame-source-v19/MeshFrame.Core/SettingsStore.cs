using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace MeshFrame.Core;

public static class SettingsStore
{
	public const int CurrentVersion = 3;

	public static List<string> ToLines(MeshFrameSettings s)
	{
		List<string> list = new List<string>();
		FieldInfo[] fields = typeof(MeshFrameSettings).GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			object value = fieldInfo.GetValue(s);
			list.Add(fieldInfo.Name + "=" + Convert.ToString((value is Enum) ? value.ToString() : value, CultureInfo.InvariantCulture));
		}
		return list;
	}

	public static void Save(MeshFrameSettings s, string path)
	{
		string directoryName = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		List<string> list = new List<string> { "@version=" + 3 };
		list.AddRange(ToLines(s));
		File.WriteAllLines(path, list.ToArray());
	}

	public static MeshFrameSettings Load(string path)
	{
		if (!File.Exists(path))
		{
			return new MeshFrameSettings();
		}
		string[] array = File.ReadAllLines(path);
		int result = 0;
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (text.StartsWith("@version="))
			{
				int.TryParse(text.Substring(9), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
			}
		}
		if (result < 3)
		{
			try
			{
				File.Copy(path, path + ".v" + result + ".bak", overwrite: true);
				File.Delete(path);
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
			return new MeshFrameSettings();
		}
		return FromLines(array);
	}

	public static MeshFrameSettings FromLines(IEnumerable<string> lines)
	{
		MeshFrameSettings meshFrameSettings = new MeshFrameSettings();
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (string line in lines)
		{
			int num = line.IndexOf('=');
			if (num > 0)
			{
				dictionary[line.Substring(0, num).Trim()] = line.Substring(num + 1);
			}
		}
		FieldInfo[] fields = typeof(MeshFrameSettings).GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			if (!dictionary.TryGetValue(fieldInfo.Name, out var value))
			{
				continue;
			}
			try
			{
				object value2;
				if (fieldInfo.FieldType == typeof(string))
				{
					value2 = value;
					goto IL_0159;
				}
				if (fieldInfo.FieldType == typeof(bool))
				{
					value2 = bool.Parse(value);
					goto IL_0159;
				}
				if (fieldInfo.FieldType == typeof(int))
				{
					value2 = int.Parse(value, CultureInfo.InvariantCulture);
					goto IL_0159;
				}
				if (fieldInfo.FieldType == typeof(double))
				{
					value2 = double.Parse(value, CultureInfo.InvariantCulture);
					goto IL_0159;
				}
				if (fieldInfo.FieldType.IsEnum)
				{
					value2 = Enum.Parse(fieldInfo.FieldType, value);
					goto IL_0159;
				}
				continue;
				IL_0159:
				fieldInfo.SetValue(meshFrameSettings, value2);
			}
			catch (FormatException)
			{
			}
			catch (ArgumentException)
			{
			}
			catch (OverflowException)
			{
			}
		}
		return meshFrameSettings;
	}
}
