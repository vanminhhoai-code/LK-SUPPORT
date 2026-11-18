using System;
using System.IO;

namespace TeklaQuickAssign;

internal static class TeklaConnectionHost
{
	internal static bool IsWindows => Path.DirectorySeparatorChar == '\\';

	internal static string SourceFile(string name)
	{
		return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
	}
}
