using System.Globalization;

namespace MeshFrame.Core;

public class PlanStrip
{
	public V2 Start;

	public V2 End;

	public V2? CutStart;

	public V2? CutEnd;

	public double Width;

	public double Thk;

	public double ZRef;

	public string Material;

	public string Name;

	public string Class;

	public string Prefix;

	public int StartNo;

	public bool OnStiffener;

	public string ProfileOverride;

	public string Profile
	{
		get
		{
			if (string.IsNullOrEmpty(ProfileOverride))
			{
				return string.Format(CultureInfo.InvariantCulture, "FL{0:0.##}*{1:0.##}", Width, Thk);
			}
			return ProfileOverride;
		}
	}
}
