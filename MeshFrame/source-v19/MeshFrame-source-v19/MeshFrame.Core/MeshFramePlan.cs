using System.Collections.Generic;

namespace MeshFrame.Core;

public class MeshFramePlan
{
	public PlanPlacement Placement = new PlanPlacement();

	public V2[] Polygon;

	public List<PlanBar> Bars = new List<PlanBar>();

	public List<PlanStrip> Strips = new List<PlanStrip>();

	public PlanMesh Mesh;

	public List<double> Cells = new List<double>();

	public int BaseEdge = -1;

	public V2 BaseOrigin;

	public V2 BaseDir;

	public double BaseMin;

	public double BaseMax;

	public List<double> StiffTims = new List<double>();

	public List<string> Errors = new List<string>();

	public List<string> Warnings = new List<string>();

	public List<string> Notes = new List<string>();

	public string AsmPrefix;

	public int AsmStartNo;
}
