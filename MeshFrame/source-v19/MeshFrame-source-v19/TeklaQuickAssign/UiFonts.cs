using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace TeklaQuickAssign;

internal static class UiFonts
{
	private static bool loaded;

	private static FontFamily regular;

	private static FontFamily semibold;

	private static readonly PrivateFontCollection collection = new PrivateFontCollection();

	private static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();

	internal static bool Available
	{
		get
		{
			Load();
			return regular != null;
		}
	}

	[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
	private static extern int AddFontResourceEx(string file, uint flags, IntPtr reserved);

	private static void Load()
	{
		if (loaded)
		{
			return;
		}
		loaded = true;
		if (TeklaConnectionHost.IsWindows)
		{
			try
			{
				string path = TeklaConnectionHost.SourceFile("Fonts");
				string[] array = new string[3] { "Inter-Regular.ttf", "Inter-SemiBold.ttf", "Inter-Bold.ttf" };
				foreach (string path2 in array)
				{
					string text = Path.Combine(path, path2);
					if (!File.Exists(text))
					{
						continue;
					}
					collection.AddFontFile(text);
					if (TeklaConnectionHost.IsWindows)
					{
						try
						{
							AddFontResourceEx(text, 16u, IntPtr.Zero);
						}
						catch (Exception)
						{
						}
					}
				}
				FontFamily[] families = collection.Families;
				foreach (FontFamily fontFamily in families)
				{
					if (fontFamily.Name == "Inter")
					{
						regular = fontFamily;
					}
					else if (fontFamily.Name == "Inter SemiBold")
					{
						semibold = fontFamily;
					}
				}
			}
			catch (Exception)
			{
				regular = (semibold = null);
			}
		}
		if (regular == null)
		{
			try
			{
				regular = new FontFamily("Inter");
			}
			catch (Exception)
			{
			}
		}
		if (semibold != null)
		{
			return;
		}
		try
		{
			semibold = new FontFamily("Inter SemiBold");
		}
		catch (Exception)
		{
		}
	}

	internal static bool Replaceable(Font font)
	{
		switch (font.FontFamily.Name)
		{
		case "Arial":
			return font.Size < 12f;
		default:
			return false;
		case "Segoe UI":
		case "Microsoft Sans Serif":
		case "Tahoma":
		case "Segoe UI Semibold":
			return true;
		}
	}

	private static Font Make(string key, Func<Font> create)
	{
		if (cache.TryGetValue(key, out var value))
		{
			return value;
		}
		try
		{
			value = create();
		}
		catch (Exception)
		{
			value = new Font("Segoe UI", 9f);
		}
		cache[key] = value;
		return value;
	}

	internal static Font Body(float size, FontStyle style = FontStyle.Regular)
	{
		Load();
		size = (float)Math.Round(size * 4f) / 4f;
		return Make("b" + size + style, () => (regular == null) ? new Font("Segoe UI", size, style) : new Font(regular, size, style));
	}

	internal static Font Semi(float size)
	{
		Load();
		size = (float)Math.Round(size * 4f) / 4f;
		return Make("s" + size, () =>
		{
			Font result = ((semibold == null) ? ((regular == null) ? new Font("Segoe UI Semibold", size) : new Font(regular, size, FontStyle.Bold)) : new Font(semibold, size));
			return result;
		});
	}

	internal static Font Mono(float size)
	{
		return Make("m" + size, () => new Font(TeklaConnectionHost.IsWindows ? "Consolas" : "DejaVu Sans Mono", size));
	}
}
