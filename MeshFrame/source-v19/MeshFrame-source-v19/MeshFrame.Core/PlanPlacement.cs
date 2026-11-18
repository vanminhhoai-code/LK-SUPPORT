namespace MeshFrame.Core;

public class PlanPlacement
{
	public string VPlane;

	public string VRotation;

	public string VDepth;

	public string StripPlane;

	public string StripRotation;

	public string StripDepth;

	public bool FlipMiterCut;

	public bool AutoPlacement;

	public double VLateral;

	public double StripLateral;

	public static string Name(PlaneKind k)
	{
		return k.ToString().ToUpperInvariant();
	}

	public static string Name(RotationKind k)
	{
		return k.ToString().ToUpperInvariant();
	}

	public static string Name(DepthKind k)
	{
		return k.ToString().ToUpperInvariant();
	}
}
