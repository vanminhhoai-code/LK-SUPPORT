namespace MeshFrame.App;

internal static class TeklaRelease
{
	internal const string V21 = "21.1";

	internal const string V2025 = "2025";

	internal const string V2020 = "2020";

	internal static readonly string[] All = new string[3] { "2025", "2020", "21.1" };

	internal static bool Legacy(string r)
	{
		if (!(r == "21.1"))
		{
			return r == "2020";
		}
		return true;
	}

	internal static string[] Folders(string r)
	{
		if (!(r == "2025"))
		{
			if (r == "2020")
			{
				return new string[2] { "2020.0", "2020" };
			}
			return new string[1] { "21.1" };
		}
		return new string[2] { "2025.0", "2025" };
	}
}
