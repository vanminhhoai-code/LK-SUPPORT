namespace MeshFrame.Tekla;

public sealed class ServiceResult
{
	public bool Success;

	public string FrameId;

	public string Message;
	public int[] ObjectIds = new int[0];
	public MeshFrame.Core.V3[] Corners;
}
