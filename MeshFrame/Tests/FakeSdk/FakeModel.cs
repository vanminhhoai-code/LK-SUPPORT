// SDK GIẢ - Tekla.Structures.Model.dll giả. Ghi log mọi thao tác vào FakeLog để test kiểm thứ tự/số lượng.
using System.Collections;
using System.Collections.Generic;
using Tekla.Structures.Geometry3d;

namespace Tekla.Structures.Model
{
    public static class FakeLog { public static readonly List<string> Entries = new List<string>(); public static bool RejectInsert; public static readonly List<string> BeamPositions = new List<string>(); public static bool NonAsciiPrompt; public static int PickFailAt = -1; public static int Picked; public static void Add(string s) { Entries.Add(s); } }

    public class Profile { public string ProfileString { get; set; } }
    public class Material { public string MaterialString { get; set; } }
    public class NumberingSeries { public string Prefix { get; set; } public int StartNumber { get; set; } }
    public class Position
    {
        public enum PlaneEnum { LEFT, MIDDLE, RIGHT }
        public enum RotationEnum { FRONT, TOP, BACK, BELOW }
        public enum DepthEnum { FRONT, MIDDLE, BEHIND }
        public PlaneEnum Plane { get; set; } public RotationEnum Rotation { get; set; } public DepthEnum Depth { get; set; }
    }
    public class Plane { public Point Origin { get; set; } public Vector AxisX { get; set; } public Vector AxisY { get; set; } }
    public class Polygon { private readonly ArrayList _p = new ArrayList(); public ArrayList Points { get { return _p; } } }

    public abstract class ModelObject
    {
        public bool Insert() { FakeLog.Add("Insert " + GetType().Name); var b = this as Beam; if (b != null) FakeLog.BeamPositions.Add(b.Position.Plane + "/" + b.Position.Rotation + "/" + b.Position.Depth); return !FakeLog.RejectInsert; }
        public bool Modify() { FakeLog.Add("Modify " + GetType().Name); return true; }
    }

    public interface IAssemblable { }
    public class Part : ModelObject, IAssemblable
    {
        public Profile Profile { get; set; } = new Profile();
        public Material Material { get; set; } = new Material();
        public string Name { get; set; }
        public string Class { get; set; }
        public NumberingSeries PartNumber { get; set; } = new NumberingSeries();
        public NumberingSeries AssemblyNumber { get; set; } = new NumberingSeries();
        public Position Position { get; set; } = new Position();
        private static readonly Dictionary<Part, Assembly> asm = new Dictionary<Part, Assembly>();
        public Assembly GetAssembly() { Assembly a; if (!asm.TryGetValue(this, out a)) { a = new Assembly(); asm[this] = a; } return a; }
    }
    public class Beam : Part { public Point StartPoint { get; set; } public Point EndPoint { get; set; } public Beam(Point s, Point e) { StartPoint = s; EndPoint = e; } }
    public class Assembly : ModelObject { public NumberingSeries AssemblyNumber { get; set; } = new NumberingSeries(); public bool Add(IAssemblable o) { FakeLog.Add("Add " + o.GetType().Name); return true; } }
    public class Fitting : ModelObject { public ModelObject Father { get; set; } public Plane Plane { get; set; } }

    public class RebarMesh : ModelObject
    {
        public enum RebarMeshTypeEnum { RECTANGULAR_MESH, POLYGON_MESH, BENT_MESH }
        public ModelObject Father { get; set; } public Polygon Polygon { get; set; } public RebarMeshTypeEnum MeshType { get; set; }
        public string Name { get; set; } public string Grade { get; set; } public int Class { get; set; }
        public NumberingSeries NumberingSeries { get; set; } = new NumberingSeries();
        public string LongitudinalSize { get; set; } public string CrossSize { get; set; }
        public ArrayList LongitudinalDistances { get; set; } public ArrayList CrossDistances { get; set; }
        public Point StartPoint { get; set; } public Point EndPoint { get; set; }
    }

    public abstract class BoltGroup : ModelObject { public enum BoltTypeEnum { BOLT_TYPE_WORKSHOP, BOLT_TYPE_SITE } }
    public class BoltArray : BoltGroup
    {
        public Part PartToBeBolted { get; set; } public Part PartToBoltTo { get; set; }
        public Point FirstPosition { get; set; } public Point SecondPosition { get; set; }
        public double BoltSize { get; set; } public string BoltStandard { get; set; } public double Tolerance { get; set; }
        public BoltTypeEnum BoltType { get; set; } public double Length { get; set; }
        public void AddBoltDistX(double d) { } public void AddBoltDistY(double d) { }
    }

    public class TransformationPlane { public Matrix TransformationMatrixToGlobal { get { return new Matrix(); } } public TransformationPlane(Point o, Vector x, Vector y) { FakeLog.Add("PlaneNew"); } public TransformationPlane() { } }
    public class WorkPlaneHandler
    {
        public TransformationPlane GetCurrentTransformationPlane() { return new TransformationPlane(); }
        public bool SetCurrentTransformationPlane(TransformationPlane p) { FakeLog.Add("SetPlane"); return true; }
    }
    public class Model
    {
        public bool GetConnectionStatus() { return true; }
        public WorkPlaneHandler GetWorkPlaneHandler() { return new WorkPlaneHandler(); }
        public bool CommitChanges() { FakeLog.Add("Commit"); return true; }
    }
}
namespace Tekla.Structures.Model.UI
{
    public class Picker
    {
        public Tekla.Structures.Geometry3d.Point PickPoint(string prompt)
        {
            foreach (char ch in prompt) if (ch > 127) FakeLog.NonAsciiPrompt = true;
            if (FakeLog.Picked == FakeLog.PickFailAt) throw new System.ApplicationException("User interrupted");
            FakeLog.Picked++;
            return new Tekla.Structures.Geometry3d.Point(FakeLog.Picked * 100, FakeLog.Picked * 10, 5);
        }
    }
}
