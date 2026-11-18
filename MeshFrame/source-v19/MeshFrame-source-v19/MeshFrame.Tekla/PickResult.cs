using System;
using MeshFrame.Core;

namespace MeshFrame.Tekla;

public sealed class PickResult
{
	public V3[] Points;

	public int Picked;

	public string StopReason;

	public string DrawNote;

	public Action Cleanup;

	public bool Complete => Points != null;
}
