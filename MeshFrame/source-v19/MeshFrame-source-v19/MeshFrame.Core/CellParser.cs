using System;
using System.Collections.Generic;
using System.Globalization;

namespace MeshFrame.Core;

public static class CellParser
{
	public static bool TryParse(string text, out List<double> cells, out string error)
	{
		cells = new List<double>();
		error = null;
		if (string.IsNullOrWhiteSpace(text))
		{
			error = "chưa nhập.";
			return false;
		}
		string[] array = text.Split(new char[4] { ' ', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text2 in array)
		{
			int result = 1;
			string s = text2;
			int num = text2.IndexOf('*');
			if (num >= 0)
			{
				if (!int.TryParse(text2.Substring(0, num), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) || result < 1)
				{
					error = "'" + text2 + "' sai (dạng n*x, n nguyên dương).";
					return false;
				}
				s = text2.Substring(num + 1);
			}
			if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) || result2 <= 0.0)
			{
				error = "'" + text2 + "' không phải số dương.";
				return false;
			}
			for (int j = 0; j < result; j++)
			{
				cells.Add(result2);
			}
		}
		return true;
	}
}
