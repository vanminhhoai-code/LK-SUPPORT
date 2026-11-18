namespace MeshFrame.Core;

public sealed class DimItem
{
	public DimKind Kind;

	public int Index;

	public V2 A;

	public V2 B;

	public V2 TextPos;

	public V2[] Extension;

	public string Text;

	public double Value;

	public bool Editable;

	public bool IsBaseEdge;

	public int Level;
}
