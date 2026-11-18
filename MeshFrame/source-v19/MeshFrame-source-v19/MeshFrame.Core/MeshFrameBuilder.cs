namespace MeshFrame.Core;

public static class MeshFrameBuilder
{
	public static bool TryFrame(V3[] corners, out Frame frame, out V2[] poly, out string error)
	{
		return TryFrame(corners, 0, out frame, out poly, out error);
	}

	public static bool TryFrame(V3[] corners, int anchor, out Frame frame, out V2[] poly, out string error)
	{
		poly = null;
		frame = null;
		error = null;
		if (corners == null || corners.Length < 3)
		{
			error = "Cần ít nhất 3 điểm.";
			return false;
		}
		int num = corners.Length;
		anchor = (anchor % num + num) % num;
		V3[] array = new V3[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = corners[(anchor + i) % num];
		}
		if (!Frame.TryCreate(array, out frame, out error))
		{
			return false;
		}
		poly = new V2[corners.Length];
		for (int j = 0; j < corners.Length; j++)
		{
			poly[j] = frame.To2D(corners[j]);
		}
		return true;
	}

	public static BuildResult Build(MeshFrameSettings s, V3[] corners, IModelWriter writer)
	{
		BuildResult buildResult = new BuildResult();
		if (!TryFrame(corners, s.AnchorPoint - 1, out var frame, out var poly, out var error))
		{
			buildResult.Errors.Add(error);
			return buildResult;
		}
		buildResult.Frame = frame;
		buildResult.Plan = MeshFramePlanner.Plan(s, poly);
		if (buildResult.Plan.Errors.Count > 0)
		{
			buildResult.Errors.AddRange(buildResult.Plan.Errors);
			return buildResult;
		}
		writer?.Write(buildResult.Plan, frame);
		buildResult.Success = true;
		return buildResult;
	}
}
