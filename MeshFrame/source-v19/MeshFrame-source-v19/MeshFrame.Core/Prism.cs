using System;

namespace MeshFrame.Core;

public sealed class Prism
{
	public V2[] Poly;

	public double Z0;

	public double Z1;

	public PartKind Kind;

	public int Index;

	public double Volume => Math.Abs(Geo.Area(Poly)) * (Z1 - Z0);
}
