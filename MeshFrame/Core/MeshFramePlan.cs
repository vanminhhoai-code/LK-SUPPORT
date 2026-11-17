// "Bản kế hoạch" - kết quả thuần của bộ tính, toàn số trong hệ 2D của mặt phẳng khung (+ cao độ Z theo pháp tuyến N).
// App nào cũng ghi được vào model bằng cách cài đặt IModelWriter; Core không biết gì về Tekla.
using System.Collections.Generic;

namespace MeshFrame.Core
{
    public enum BarRole { Frame, Stiffener }

    public class PlanBar
    {
        public BarRole Role;
        public V2 Start, End;           // Frame: đường mép ngoài (qua điểm pick). Stiffener: đường tim ô chia (cánh nằm đặt giữa tim)
        public V2? CutStart, CutEnd;    // hướng mặt cắt vát tại đầu (null = cắt vuông)
        public double LegA, LegB, Thk;
        public double ZTop;             // cao độ mặt trên cánh nằm
        public bool VerticalUp;         // true: cánh đứng hướng lên (khung); false: hướng xuống (gia cường)
        public bool Centered;           // cánh nằm đặt giữa đường tham chiếu
        public string Material, Name, Class, Prefix;
        public int StartNo;
        public string Profile { get { return string.Format(System.Globalization.CultureInfo.InvariantCulture, "L{0:0.##}*{1:0.##}*{2:0.##}", LegA, LegB, Thk); } }
    }

    public class PlanStrip
    {
        public V2 Start, End;           // đường tim nẹp
        public V2? CutStart, CutEnd;
        public double Width, Thk, ZBottom;
        public string Material, Name, Class, Prefix;
        public int StartNo;
        public bool OnStiffener;
        public string Profile { get { return string.Format(System.Globalization.CultureInfo.InvariantCulture, "FL{0:0.##}*{1:0.##}", Width, Thk); } }
    }

    public class PlanMesh
    {
        public V2[] Polygon;            // 1 tấm lưới liên tục phủ cả khung (kéo căng qua các gia cường)
        public double ZBottom, Thickness, WireDia, Pitch, WeightKgPerM2;
        public string Grade, Name, Prefix;
        public int Class, StartNo;
    }

    public class PlanFastener
    {
        public V2 Pos;
        public FastenKind Kind;
        public double Dia, Length, ZTop;
        public int StripIndex;          // vít này thuộc nẹp nào (chỉ số trong plan.Strips)
    }

    public class MeshFramePlan
    {
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
