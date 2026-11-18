using System;
using System.Collections.Generic;
using System.IO;

namespace MeshFrame.App;

internal static class TeklaLocator
{
	internal static bool Resolve(string[] args, out string release, out string bin)
	{
		string text = null;
		string text2 = null;
		string[] array = args;
		foreach (string text3 in array)
		{
			if (text3.StartsWith("--tekla-version=", StringComparison.OrdinalIgnoreCase))
			{
				text = text3.Substring(16).Trim('"');
			}
			else if (text3.StartsWith("--tekla-bin=", StringComparison.OrdinalIgnoreCase))
			{
				text2 = text3.Substring(12).Trim('"');
			}
		}
		string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MeshFrame.ini");
		if (text2 == null && File.Exists(path))
		{
			array = File.ReadAllLines(path);
			foreach (string text4 in array)
			{
				if (text4.StartsWith("TeklaBin=", StringComparison.OrdinalIgnoreCase))
				{
					text2 = text4.Substring(9).Trim().Trim('"');
				}
			}
		}
		if (!string.IsNullOrEmpty(text2) && Ok(text2))
		{
			release = text ?? Guess(text2);
			bin = text2;
			return true;
		}
		array = ((text == null) ? TeklaRelease.All : new string[1] { text });
		foreach (string text5 in array)
		{
			string text6 = Find(text5);
			if (text6 != null)
			{
				release = text5;
				bin = text6;
				return true;
			}
		}
		release = null;
		bin = null;
		return false;
	}

	internal static string Find(string release)
	{
		List<string> list = new List<string>();
		string[] array = new string[3]
		{
			"C:\\Program Files",
			Environment.GetEnvironmentVariable("ProgramW6432"),
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
		};
		foreach (string text in array)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				list.Add(Path.Combine(text, "Tekla Structures"));
				list.Add(Path.Combine(text, "TeklaStructures"));
			}
		}
		list.Add("C:\\TeklaStructures");
		foreach (string item in list)
		{
			array = TeklaRelease.Folders(release);
			foreach (string path in array)
			{
				string[] array2 = ((!TeklaRelease.Legacy(release)) ? new string[4] { "bin", "bin\\plugins", "nt\\bin\\plugins", "nt\\bin" } : new string[4] { "nt\\bin\\plugins", "nt\\bin", "bin", "bin\\plugins" });
				foreach (string path2 in array2)
				{
					string text2 = Path.Combine(item, path, path2);
					if (Ok(text2))
					{
						return text2;
					}
				}
			}
		}
		return null;
	}

	private static string Guess(string bin)
	{
		if (bin.IndexOf("2025", StringComparison.Ordinal) >= 0)
		{
			return "2025";
		}
		if (bin.IndexOf("2020", StringComparison.Ordinal) >= 0)
		{
			return "2020";
		}
		return "21.1";
	}

	private static bool Ok(string dir)
	{
		if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, "Tekla.Structures.dll")))
		{
			return File.Exists(Path.Combine(dir, "Tekla.Structures.Model.dll"));
		}
		return false;
	}
}
