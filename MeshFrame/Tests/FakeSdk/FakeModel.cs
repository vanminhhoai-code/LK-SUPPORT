// SDK GIẢ - Tekla.Structures.Model.dll giả (dựng theo hình dạng API thật). Ghi log mọi thao tác vào FakeLog.
// RebarMesh giả mô phỏng điều nghi ngờ ở Tekla thật: thiếu thuộc tính kiểu liệt kê (mặc định UNDEFINED) -> "required info missing".
using System.Collections;
using System.Collections.Generic;
using Tekla.Structures.Geometry3d;

namespace Tekla.Structures.Model
{
    public static class FakeLog
    {
        public static readonly List<string> Entries = new List<string>();
        public static bool RejectInsert, RejectRebarMesh, RejectPolygonMesh, NonAsciiPrompt;
        public static int FailBeamAt = -1, BeamCount, PickFailAt = -1, Picked;
        public static readonly List<string> BeamPositions = new List<string>();
        public static readonly List<double[]> BeamStarts = new List<double[]>();
        public static readonly List<ModelObject> Selected = new List<ModelObject>();
        public static readonly List<RebarMesh> Meshes = new List<RebarMesh>();
        public static readonly List<double[]> PickScript = new List<double[]>();     // điểm do Picker trả về lần lượt; hết thì ném lỗi (chuột giữa)
        public static readonly List<string> Prompts = new List<string>();
        public static string ModelPath = "";
        public static readonly Dictionary<ModelObject, Dictionary<string, string>> Uda = new Dictionary<ModelObject, Dictionary<string, string>>();
        public static void Add(string s) { Entries.Add(s); }
        public static void Reset()
        {
            Entries.Clear(); BeamPositions.Clear(); BeamStarts.Clear(); Meshes.Clear(); Prompts.Clear(); Uda.Clear(); Selected.Clear();
            RejectInsert = RejectRebarMesh = RejectPolygonMesh = false; FailBeamAt = -1; BeamCount = 0;
        }
    }

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
        public bool Insert()
        {
            FakeLog.Add("Insert " + GetType().Name);
            var b = this as Beam;
            if (b != null)
            {
                FakeLog.BeamCount++;
                if (FakeLog.BeamCount == FakeLog.FailBeamAt) return false;
                FakeLog.BeamPositions.Add(b.Position.Plane + "/" + b.Position.Rotation + "/" + b.Position.Depth);
                FakeLog.BeamStarts.Add(new[] { b.StartPoint.X, b.StartPoint.Y, b.StartPoint.Z });
            }
            var m = this as RebarMesh;
            if (m != null) m.Validate();
            return !FakeLog.RejectInsert;
        }
        public bool Modify() { FakeLog.Add("Modify " + GetType().Name); return true; }
        public bool Delete() { FakeLog.Add("Delete " + GetType().Name); return true; }
        public bool SetUserProperty(string name, string value)
        {
            Dictionary<string, string> d; if (!FakeLog.Uda.TryGetValue(this, out d)) { d = new Dictionary<string, string>(); FakeLog.Uda[this] = d; }
            d[name] = value; FakeLog.Add("UDA " + name + "=" + value); return true;
        }
        public bool SetUserProperty(string name, int value) { return true; }
        public bool SetUserProperty(string name, double value) { return true; }
        public bool GetUserProperty(string name, ref string value)
        {
            Dictionary<string, string> d; string v;
            if (FakeLog.Uda.TryGetValue(this, out d) && d.TryGetValue(name, out v)) { value = v; return true; }
            return false;
        }
        public bool GetUserProperty(string name, ref int value) { return false; }
        public bool GetUserProperty(string name, ref double value) { return false; }
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
        internal static void Link(Part p, Assembly a) { asm[p] = a; }
        public Assembly GetAssembly() { Assembly a; if (!asm.TryGetValue(this, out a)) { a = new Assembly(this); asm[this] = a; } return a; }
    }
    public class Beam : Part { public Point StartPoint { get; set; } public Point EndPoint { get; set; } public Beam(Point s, Point e) { StartPoint = s; EndPoint = e; } }
    public class Assembly : ModelObject
    {
        private readonly Part _main; private readonly ArrayList _sec = new ArrayList();
        public Assembly(Part main) { _main = main; }
        public NumberingSeries AssemblyNumber { get; set; } = new NumberingSeries();
        public bool Add(IAssemblable o) { FakeLog.Add("Add " + o.GetType().Name); _sec.Add(o); var p = o as Part; if (p != null) Part.Link(p, this); return true; }
        public ModelObject GetMainPart() { return _main; }
        public ArrayList GetSecondaries() { return _sec; }
    }

    public abstract class Boolean : ModelObject { public ModelObject Father { get; set; } }
    public class Fitting : Boolean { public Plane Plane { get; set; } }
    public class BooleanPart : Boolean
    {
        public enum BooleanTypeEnum { BOOLEAN_ADD, BOOLEAN_CUT, BOOLEAN_WELDPREP }
        public const string BooleanOperativeClassName = "BlOpCl";
        public BooleanTypeEnum Type { get; set; } public Part OperativePart { get; private set; }
        public void SetOperativePart(Part p) { OperativePart = p; FakeLog.Add("Cut " + (p.Class ?? "?") + " by plate"); }
    }
    public class Chamfer { }
    public class ContourPoint { public Point Point { get; set; } public Chamfer Chamfer { get; set; } public ContourPoint(Point p, Chamfer c) { Point = p; Chamfer = c; } }
    public class ContourPlate : Part
    {
        private readonly ArrayList _pts = new ArrayList();
        public bool AddContourPoint(ContourPoint p) { _pts.Add(p); return true; }
        public int PointCount { get { return _pts.Count; } }
    }

    public abstract class Reinforcement : ModelObject
    {
        public enum RebarOffsetTypeEnum { OFFSET_TYPE_COVER_THICKNESS, OFFSET_TYPE_LEG_LENGTH }
        public ModelObject Father { get; set; }
        public string Name { get; set; } public string Grade { get; set; } public int Class { get; set; }
        public NumberingSeries NumberingSeries { get; set; } = new NumberingSeries();
        public ArrayList OnPlaneOffsets { get; set; } = new ArrayList();
        public double FromPlaneOffset { get; set; }
        public RebarOffsetTypeEnum StartPointOffsetType { get; set; }
        public double StartPointOffsetValue { get; set; }
    }

    public class RebarMesh : Reinforcement
    {
        public enum RebarMeshTypeEnum { UNKNOWN_MESH, RECTANGULAR_MESH, POLYGON_MESH, BENT_MESH }
        public enum RebarMeshSpacingMethodEnum { SPACING_TYPE_UNDEFINED, SPACING_TYPE_SAME_DISTANCE, SPACING_TYPE_MULTIPLE_VARYING_DISTANCES }
        public enum RebarMeshCrossBarLocationEnum { LOCATION_TYPE_ABOVE, LOCATION_TYPE_BELOW, LOCATION_TYPE_UNDEFINED }
        public RebarMeshTypeEnum MeshType { get; set; } = RebarMeshTypeEnum.UNKNOWN_MESH;
        public RebarMeshSpacingMethodEnum LongitudinalSpacingMethod { get; set; } = RebarMeshSpacingMethodEnum.SPACING_TYPE_UNDEFINED;
        public RebarMeshCrossBarLocationEnum CrossBarLocation { get; set; } = RebarMeshCrossBarLocationEnum.LOCATION_TYPE_UNDEFINED;
        public Polygon Polygon { get; set; }
        public ArrayList LongitudinalDistances { get; set; } = new ArrayList();
        public ArrayList CrossDistances { get; set; } = new ArrayList();
        public Point StartPoint { get; set; } public Point EndPoint { get; set; }
        public double LeftOverhangLongitudinal { get; set; } public double LeftOverhangCross { get; set; }
        public double RightOverhangLongitudinal { get; set; } public double RightOverhangCross { get; set; }
        public string LongitudinalSize { get; set; } public string CrossSize { get; set; }
        public double Width { get; set; } public double Length { get; set; }
        public bool CutByFatherPartCuts { get; set; }
        public string CatalogName { get; set; }

        /// <summary>Mô phỏng Tekla: thiếu thông tin thì từ chối (đúng thông báo của Tekla 21.1 thật).</summary>
        internal void Validate()
        {
            if (FakeLog.RejectRebarMesh || (FakeLog.RejectPolygonMesh && MeshType == RebarMeshTypeEnum.POLYGON_MESH))
                throw new System.ApplicationException("RebarMesh is not valid for insert, required info missing");
            bool ok = MeshType != RebarMeshTypeEnum.UNKNOWN_MESH && LongitudinalSpacingMethod != RebarMeshSpacingMethodEnum.SPACING_TYPE_UNDEFINED
                      && CrossBarLocation != RebarMeshCrossBarLocationEnum.LOCATION_TYPE_UNDEFINED && Father != null && !string.IsNullOrEmpty(CatalogName)
                      && !string.IsNullOrEmpty(LongitudinalSize) && !string.IsNullOrEmpty(CrossSize) && LongitudinalDistances.Count > 0 && CrossDistances.Count > 0
                      && !string.IsNullOrEmpty(Grade) && (MeshType != RebarMeshTypeEnum.POLYGON_MESH || (Polygon != null && Polygon.Points.Count >= 3));
            if (!ok) throw new System.ApplicationException("RebarMesh is not valid for insert, required info missing");
            FakeLog.Meshes.Add(this);
        }
    }

    public abstract class BoltGroup : ModelObject { public enum BoltTypeEnum { BOLT_TYPE_WORKSHOP, BOLT_TYPE_SITE } }

    public class TransformationPlane { public Matrix TransformationMatrixToGlobal { get { return new Matrix(); } } public TransformationPlane(Point o, Vector x, Vector y) { FakeLog.Add("PlaneNew"); } public TransformationPlane() { } }
    public class WorkPlaneHandler
    {
        public TransformationPlane GetCurrentTransformationPlane() { return new TransformationPlane(); }
        public bool SetCurrentTransformationPlane(TransformationPlane p) { FakeLog.Add("SetPlane"); return true; }
    }
    public class ModelInfo { public string ModelPath { get; set; } }
    public class Model
    {
        public bool GetConnectionStatus() { return true; }
        public WorkPlaneHandler GetWorkPlaneHandler() { return new WorkPlaneHandler(); }
        public bool CommitChanges() { FakeLog.Add("Commit"); return true; }
        public ModelInfo GetInfo() { return new ModelInfo { ModelPath = FakeLog.ModelPath }; }
    }

    public class ModelObjectEnumerator
    {
        private int _i = -1;
        public bool MoveNext() { _i++; return _i < FakeLog.Selected.Count; }
        public ModelObject Current { get { return FakeLog.Selected[_i]; } }
        public int GetSize() { return FakeLog.Selected.Count; }
    }
}
namespace Tekla.Structures.Model.UI
{
    public class ModelObjectSelector { public ModelObjectEnumerator GetSelectedObjects() { return new ModelObjectEnumerator(); } }

    public class Color { public double R, G, B; public Color(double r, double g, double b) { R = r; G = g; B = b; } }

    public class GraphicsDrawer
    {
        private int _next = 100;
        public int DrawLineSegment(Point a, Point b, Color c) { FakeLog.Add("DrawLine"); return _next++; }
        public int DrawText(Point p, string text, Color c) { FakeLog.Add("DrawText " + text); return _next++; }
        public bool RemoveTemporaryGraphicsObjects(IEnumerable ids) { int n = 0; foreach (object o in ids) n++; FakeLog.Add("RemoveGraphics " + n); return true; }
    }

    public class Picker
    {
        private Tekla.Structures.Geometry3d.Point Next(string prompt)
        {
            FakeLog.Prompts.Add(prompt);
            foreach (char ch in prompt) if (ch > 127) FakeLog.NonAsciiPrompt = true;
            if (FakeLog.Picked == FakeLog.PickFailAt || FakeLog.Picked >= FakeLog.PickScript.Count) throw new System.ApplicationException("User interrupted");
            double[] c = FakeLog.PickScript[FakeLog.Picked++];
            return new Tekla.Structures.Geometry3d.Point(c[0], c[1], c[2]);
        }
        public Tekla.Structures.Geometry3d.Point PickPoint(string prompt) { return Next(prompt); }
#if !NO_PICK_REF
        public Tekla.Structures.Geometry3d.Point PickPoint(string prompt, Tekla.Structures.Geometry3d.Point reference) { return Next(prompt); }
#endif
    }
}
