using System.Collections.Generic;
using System.Globalization;

namespace MeshFrame.Core;

public class PlanBar
{
	public BarRole Role;

	public V2 Start;

	public V2 End;

	public V2? CutStart;

	public V2? CutEnd;

	public double LegA;

	public double LegB;

	public double Thk;

	public double ZRef;

	public List<V2[]> Notches = new List<V2[]>();

	public string Material;

	public string Name;

	public string Class;

	public string Prefix;

	public int StartNo;

	public string ProfileOverride;

	public string Profile
	{
		get
		{
			if (string.IsNullOrEmpty(ProfileOverride))
			{
				return string.Format(CultureInfo.InvariantCulture, "L{0:0.##}*{1:0.##}*{2:0.##}", LegA, LegB, Thk);
			}
			return ProfileOverride;
		}
	}
}
