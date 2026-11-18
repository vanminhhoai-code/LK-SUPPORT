using System;
using System.Collections.Generic;

namespace MeshFrame.Core;

public class MeshFrameSettings
{
	public double VLegA = 25.0;

	public double VLegB = 25.0;

	public double VThk = 2.0;

	public string VMaterial = "MA KEM";

	public string VName = "KHUNG V";

	public string VClass = "4";

	public string VPrefix = "V";

	public int VStartNo = 1;

	public string AsmPrefix = "KL";

	public int AsmStartNo = 1;

	public CornerKind Corner;

	public int AnchorPoint = 1;

	public StiffMode StiffMode = StiffMode.Equal;

	public int StiffBaseEdge;

	public double StiffMax = 1000.0;

	public string StiffCells = "1000 1000";

	public string SMaterial = "MA KEM";

	public string SName = "GIA CUONG";

	public string SClass = "7";

	public string SPrefix = "GC";

	public int SStartNo = 1;

	public MeshKind MeshKind;

	public double MeshGap;

	public double MeshWireDia = 0.37;

	public double MeshPitch = 2.37;

	public string MeshName = "LUOI INOX 2x2";

	public string MeshGrade = "SUS304";

	public string MeshPrefix = "LI";

	public int MeshClass = 6;

	public int MeshStartNo = 1;

	public double MeshWeightKgPerM2;

	public string RebarGrade = "";

	public string RebarSize = "";

	public bool UseStrip = true;

	public StripPosKind StripPos;

	public double StripWidth = 25.0;

	public double StripThk = 1.2;

	public string StripMaterial = "SUS304";

	public string StripName = "NEP LUOI";

	public string StripClass = "5";

	public string StripPrefix = "N";

	public int StripStartNo = 1;

	public string VProfile = "";

	public string StripProfile = "";

	public PlaneKind VPlane = PlaneKind.Middle;

	public PlaneKind StripPlane = PlaneKind.Middle;

	public RotationKind VRotation;

	public RotationKind StripRotation;

	public DepthKind VDepth = DepthKind.Middle;

	public DepthKind StripDepth = DepthKind.Middle;

	public double VLateral;

	public double StripLateral;

	public bool AutoPlacement = true;

	public bool FlipMiterCut;

	public double StripOffset()
	{
		if (StripPos == StripPosKind.Right)
		{
			return 0.0;
		}
		if (StripPos == StripPosKind.Left)
		{
			return VLegA - StripWidth;
		}
		return (VLegA - StripWidth) / 2.0;
	}

	public List<string> Validate()
	{
		List<string> list = new List<string>();
		if (VLegA <= 0.0 || VLegB <= 0.0 || VThk <= 0.0)
		{
			list.Add("Kích thước V phải > 0.");
		}
		else if (VThk >= Math.Min(VLegA, VLegB))
		{
			list.Add("Độ dày V phải nhỏ hơn cạnh V.");
		}
		if (MeshGap < 0.0)
		{
			list.Add("Khoảng lùi lưới không được âm.");
		}
		if (MeshWireDia <= 0.0 || MeshPitch <= MeshWireDia)
		{
			list.Add("Bước sợi phải lớn hơn đường kính sợi.");
		}
		if (UseStrip)
		{
			if (StripWidth <= 0.0 || StripThk <= 0.0)
			{
				list.Add("Kích thước nẹp phải > 0.");
			}
			else if (StripWidth <= MeshGap)
			{
				list.Add("Nẹp phải đè lên mép lưới: mép trong của nẹp phải nằm sâu hơn khoảng lùi lưới.");
			}
		}
		if (StiffMode != StiffMode.None)
		{
			if (StiffMode == StiffMode.Equal && StiffMax <= 0.0)
			{
				list.Add("Khoảng cách ô tối đa phải > 0.");
			}
			if (StiffBaseEdge < 0)
			{
				list.Add("Cạnh gốc gia cường không hợp lệ.");
			}
			if (StiffMode == StiffMode.Custom && !CellParser.TryParse(StiffCells, out var _, out var error))
			{
				list.Add("Bề rộng các ô: " + error);
			}
		}
		return list;
	}
}
