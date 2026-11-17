// "Bản kế hoạch" - kết quả thuần của bộ tính, toàn số trong hệ 2D của mặt phẳng khung (+ cao độ Z theo pháp tuyến N).
// App nào cũng ghi được vào model bằng cách cài đặt IModelWriter; Core không biết gì về Tekla.
using System.Collections.Generic;

namespace MeshFrame.Core
{
    public enum BarRole { Frame, Stiffener }

    public class PlanBar
    {
        public BarRole Role;
        public V2 Start, End;           // đường mép ngoài của cánh đứng. Cánh nằm nằm về bên TRÁI của hướng Start->End. Khung: qua điểm pick. Gia cường: đường chia ô.
        public V2? CutStart, CutEnd;    // hướng mặt cắt vát tại đầu (null = cắt vuông)
        public double LegA, LegB, Thk;
        public double ZRef;             // cao độ đường tham chiếu = góc ngoài của V (mặt dưới cánh nằm); cánh đứng hướng lên (+Z)
        public List<V2[]> Notches = new List<V2[]>();   // vùng khuyết cắt ở cánh nằm (đa giác trong mặt phẳng, z từ ZRef đến ZRef+Thk)
        public string Material, Name, Class, Prefix;
        public int StartNo;
        public string ProfileOverride;  // rỗng = tự tạo L a*b*t
        public string Profile { get { return !string.IsNullOrEmpty(ProfileOverride) ? ProfileOverride : string.Format(System.Globalization.CultureInfo.InvariantCulture, "L{0:0.##}*{1:0.##}*{2:0.##}", LegA, LegB, Thk); } }
    }

    public class PlanStrip
    {
        public V2 Start, End;           // đường tim nẹp
        public V2? CutStart, CutEnd;
        public double Width, Thk, ZRef;     // ZRef: mặt tiếp xúc lưới; nẹp nằm phía ngoài (-Z) mặt này
        public string Material, Name, Class, Prefix;
        public int StartNo;
        public bool OnStiffener;
        public string ProfileOverride;  // rỗng = tự tạo FL w*t
        public string Profile { get { return !string.IsNullOrEmpty(ProfileOverride) ? ProfileOverride : string.Format(System.Globalization.CultureInfo.InvariantCulture, "FL{0:0.##}*{1:0.##}", Width, Thk); } }
    }

    public class PlanMesh
    {
        public V2[] Polygon;            // 1 tấm lưới liên tục phủ cả khung (kéo căng qua các gia cường)
        public double ZBottom, Thickness, WireDia, Pitch, WeightKgPerM2;   // lưới nằm dưới cánh nằm V: Z từ ZBottom (<0) đến 0
        public string Grade, Name, Prefix;
        public int Class, StartNo;
    }

    public class PlanFastener
    {
        public V2 Pos;
        public FastenKind Kind;
        public double Dia, Length, ZHead;   // ZHead: cao độ mặt đầu vít (ngoài nẹp); vít đi vào theo +Z
        public int StripIndex;          // vít này thuộc nẹp nào (chỉ số trong plan.Strips)
    }

    /// <summary>Cách đặt trong Tekla, dạng tên enum Tekla (LEFT, FRONT, BEHIND, ...).</summary>
    public class PlanPlacement
    {
        public string VPlane, VRotation, VDepth, StripPlane, StripRotation, StripDepth;
        public bool FlipMiterCut, FlipBolt;
        public string BoltStandard;
        public static string Name(PlaneKind k) { return k.ToString().ToUpperInvariant(); }
        public static string Name(RotationKind k) { return k.ToString().ToUpperInvariant(); }
        public static string Name(DepthKind k) { return k.ToString().ToUpperInvariant(); }
    }

    public class MeshFramePlan
    {
        public PlanPlacement Placement = new PlanPlacement();
        public List<PlanBar> Bars = new List<PlanBar>();
        public List<PlanStrip> Strips = new List<PlanStrip>();
        public PlanMesh Mesh;
        public List<PlanFastener> Fasteners = new List<PlanFastener>();
        public List<double> Cells = new List<double>();    // bề rộng các ô (rỗng nếu không gia cường)
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public string AsmPrefix; public int AsmStartNo;
    }

    /// <summary>Cầu nối ghi vào model. App tích hợp (Cover Tool) cài đặt bằng cách riêng của nó (vd reflection như TeklaGateway).</summary>
    public interface IModelWriter
    {
        void Write(MeshFramePlan plan, Frame frame);
    }
}
