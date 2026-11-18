using System.Collections.Generic;

namespace MeshFrame.Core;

public class BuildResult
{
	public bool Success;

	public List<string> Errors = new List<string>();

	public MeshFramePlan Plan;

	public Frame Frame;
}
