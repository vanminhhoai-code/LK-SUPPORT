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
        public static bool RejectInsert, RejectRebarMesh, RejectPolygonMesh, NonAsciiPrompt, NoSolid, NullAssembly;
        public static int SectionTurn;                                                    // xoay thêm tiết diện giả (độ) để thử quy ước đặt khác
        public static int FailBeamAt = -1, BeamCount, PickFailAt = -1, Picked;
        public static readonly List<string> BeamPositions = new List<string>();
        public static readonly List<Beam> Beams = new List<Beam>();                     // thanh THẬT (không tính thanh mẫu MF_CALIB)
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
            Entries.Clear(); BeamPositions.Clear(); BeamStarts.Clear(); Beams.Clear(); SectionTurn = 0; NoSolid = false; NullAssembly = false; Meshes.Clear(); Prompts.Clear(); Uda.Clear(); Selected.Clear();
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
            if (b != null && b.Name != "MF_CALIB")
            {
                FakeLog.BeamCount++;
                if (FakeLog.BeamCount == FakeLog.FailBeamAt) return false;
                FakeLog.BeamPositions.Add(b.Position.Plane + "/" + b.Position.Rotation + "/" + b.Position.Depth);
                FakeLog.BeamStarts.Add(new[] { b.StartPoint.X, b.StartPoint.Y, b.StartPoint.Z });
                FakeLog.Beams.Add(b);
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
        public Assembly GetAssembly() { if (FakeLog.NullAssembly) return null; Assembly a; if (!asm.TryGetValue(this, out a)) { a = new Assembly(this); asm[this] = a; } return a; }
    }
    /// <summary>Hình khối giả: lăng trụ của tiết diện (y, z) dọc trục X. Dùng để thử thuật toán tự canh với một quy ước đặt KHÁC giả định của app.</summary>
    public class Solid
    {
        public Point MinimumPoint { get; set; }
        public Point MaximumPoint { get; set; }
        internal List<double[]> Poly = new List<double[]>();   // (y, z) tuyệt đối
        internal double X0, X1;

        public ArrayList Intersect(Point p1, Point p2)
        {
            var res = new ArrayList();
            if (p1.X < X0 - 1e-9 || p1.X > X1 + 1e-9) return res;
            double dy = p2.Y - p1.Y, dz = p2.Z - p1.Z;
            var hits = new List<double[]>();
            for (int i = 0; i < Poly.Count; i++)
            {
                double[] a = Poly[i], b = Poly[(i + 1) % Poly.Count];
                double fy = b[0] - a[0], fz = b[1] - a[1];
                double den = dy * fz - dz * fy;
                if (System.Math.Abs(den) < 1e-12) continue;
                double t = ((a[0] - p1.Y) * fz - (a[1] - p1.Z) * fy) / den, u = ((a[0] - p1.Y) * dz - (a[1] - p1.Z) * dy) / den;
                if (t >= 0 && t <= 1 && u >= 0 && u <= 1) hits.Add(new[] { t, p1.Y + t * dy, p1.Z + t * dz });
            }
            hits.Sort((h1, h2) => h1[0].CompareTo(h2[0]));
            foreach (double[] h in hits) res.Add(new Point(p1.X, h[1], h[2]));
            return res;
        }
    }

    public class Beam : Part
    {
        public Point StartPoint { get; set; } public Point EndPoint { get; set; }
        public Beam(Point s, Point e) { StartPoint = s; EndPoint = e; }

        // Quy ước ĐẶT GIẢ (cố ý khác giả định của app): Rotation FRONT = 90°, TOP = 180°, BACK = 270°, BELOW = 0°.
        // Plane LEFT: tham chiếu ở mép y lớn; RIGHT: mép y nhỏ. Depth FRONT: tham chiếu ở mép z lớn; BEHIND: mép z nhỏ.
        public Solid GetSolid()
        {
            if (FakeLog.NoSolid) throw new System.NotSupportedException("GetSolid không có trên bản Tekla này");
            var sec = new List<double[]>();
            string pr = Profile.ProfileString ?? "";
            if (pr.StartsWith("FL"))
            {
                string[] t = pr.Substring(2).Split('*'); double w = double.Parse(t[0], System.Globalization.CultureInfo.InvariantCulture), th = double.Parse(t[1], System.Globalization.CultureInfo.InvariantCulture);
                sec.Add(new[] { 0.0, 0 }); sec.Add(new[] { w, 0 }); sec.Add(new[] { w, th }); sec.Add(new[] { 0.0, th });
            }
            else if (pr.StartsWith("L"))
            {
                string[] t = pr.Substring(1).Split('*'); var ci = System.Globalization.CultureInfo.InvariantCulture;
                double a = double.Parse(t[0], ci), b = double.Parse(t[1], ci), th = double.Parse(t[2], ci);
                sec.Add(new[] { 0.0, 0 }); sec.Add(new[] { a, 0 }); sec.Add(new[] { a, th }); sec.Add(new[] { th, th }); sec.Add(new[] { th, b }); sec.Add(new[] { 0.0, b });
            }
            else { sec.Add(new[] { 0.0, 0 }); sec.Add(new[] { 10.0, 0 }); sec.Add(new[] { 10.0, 10 }); sec.Add(new[] { 0.0, 10 }); }

            double ang = Position.Rotation == Position.RotationEnum.FRONT ? 90 : Position.Rotation == Position.RotationEnum.TOP ? 180 : Position.Rotation == Position.RotationEnum.BACK ? 270 : 0;
            ang += FakeLog.SectionTurn;
            double c = System.Math.Round(System.Math.Cos(ang * System.Math.PI / 180)), sn = System.Math.Round(System.Math.Sin(ang * System.Math.PI / 180));
            double y0 = double.MaxValue, y1 = double.MinValue, z0 = double.MaxValue, z1 = double.MinValue;
            for (int i = 0; i < sec.Count; i++)
            {
                double y = sec[i][0] * c - sec[i][1] * sn, z = sec[i][0] * sn + sec[i][1] * c;
                sec[i] = new[] { y, z };
                y0 = System.Math.Min(y0, y); y1 = System.Math.Max(y1, y); z0 = System.Math.Min(z0, z); z1 = System.Math.Max(z1, z);
            }
            double dy = Position.Plane == Position.PlaneEnum.MIDDLE ? -(y0 + y1) / 2 : Position.Plane == Position.PlaneEnum.LEFT ? -y1 : -y0;
            double dz = Position.Depth == Position.DepthEnum.MIDDLE ? -(z0 + z1) / 2 : Position.Depth == Position.DepthEnum.FRONT ? -z1 : -z0;
            var s = new Solid { X0 = System.Math.Min(StartPoint.X, EndPoint.X), X1 = System.Math.Max(StartPoint.X, EndPoint.X) };
            double miny = double.MaxValue, maxy = double.MinValue, minz = double.MaxValue, maxz = double.MinValue;
            foreach (double[] p in sec)
            {
                double y = p[0] + dy + StartPoint.Y, z = p[1] + dz + StartPoint.Z;
                s.Poly.Add(new[] { y, z });
                miny = System.Math.Min(miny, y); maxy = System.Math.Max(maxy, y); minz = System.Math.Min(minz, z); maxz = System.Math.Max(maxz, z);
            }
            s.MinimumPoint = new Point(s.X0, miny, minz); s.MaximumPoint = new Point(s.X1, maxy, maxz);
            return s;
        }
    }
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
