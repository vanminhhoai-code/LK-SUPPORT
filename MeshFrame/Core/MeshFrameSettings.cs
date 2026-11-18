// Toàn bộ thông số do người dùng nhập. Không phụ thuộc giao diện / Tekla.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MeshFrame.Core
{
    public enum CornerKind { Miter = 0, OneThrough = 1 }
    public enum StiffMode { None = 0, Equal = 1, Custom = 2 }
    public enum MeshKind { RebarMesh = 0, Plate = 1, None = 2 }   // lưới: tấm mỏng / Reinforcement Mesh / không tạo
    // Hướng đặt thép góc / nẹp trong Tekla (Position.Plane / Rotation / Depth). Thứ tự khớp tên enum Tekla.
    public enum PlaneKind { Left = 0, Middle = 1, Right = 2 }
    public enum RotationKind { Front = 0, Top = 1, Back = 2, Below = 3 }
    public enum DepthKind { Front = 0, Middle = 1, Behind = 2 }

    public class MeshFrameSettings
    {
        // ---- Khung V (một thanh cho mỗi cạnh của đa giác pick, mép ngoài trùng đường nối điểm)
        public double VLegA = 25, VLegB = 25, VThk = 2;   // chuẩn V-25x2 (nhập được tiết diện khác)
        public string VMaterial = "MA KEM", VName = "KHUNG V", VClass = "4", VPrefix = "V";
        public int VStartNo = 1;
        public string AsmPrefix = "KL";
        public int AsmStartNo = 1;
        public CornerKind Corner = CornerKind.Miter;

        // ---- Gia cường: thanh V CÙNG TIẾT DIỆN với V khung, vuông góc đố trên/dưới, hai đầu cắt khuyết cho khớp V khung
        public StiffMode StiffMode = StiffMode.Equal;
        public int StiffBaseEdge = 0;                     // cạnh gốc (1..N); 0 = tự chọn cạnh dài nhất. Gia cường vuông góc cạnh gốc
        public double StiffMax = 1000;                    // Equal: mỗi ô tối đa
        public string StiffCells = "1000 1000";           // Custom: bề rộng từng ô, vd "1000 2*1500 800"
        public string SMaterial = "MA KEM", SName = "GIA CUONG", SClass = "7", SPrefix = "GC";
        public int SStartNo = 1;

        // ---- Lưới
        public MeshKind MeshKind = MeshKind.RebarMesh;    // RebarMesh; nếu Tekla từ chối thì tự chuyển sang tấm mỏng
        public double MeshGap = 0, MeshWireDia = 0.37, MeshPitch = 2.37;   // MeshGap: lưới lùi vào so với MÉP NGOÀI khung; chuẩn: ô 2x2, sợi 0.37
        public string MeshName = "LUOI INOX 2x2", MeshGrade = "SUS304", MeshPrefix = "LI";
        public int MeshClass = 6, MeshStartNo = 1;
        public double MeshWeightKgPerM2 = 0;   // 0 = không dùng
        public string RebarGrade = "", RebarSize = "";   // RebarMesh: mác và cỡ sợi phải CÓ trong catalog rebar Tekla; trống = dùng MeshGrade / MeshWireDia

        // ---- Nẹp (tùy chọn)
        public bool UseStrip = true;
        public double StripWidth = 25, StripThk = 1.2;   // chuẩn PL-25x1.2 (nhập được tiết diện khác)
        public string StripMaterial = "SUS304", StripName = "NEP LUOI", StripClass = "5", StripPrefix = "N";
        public int StripStartNo = 1;

        // ---- Tekla: profile (để trống = tự tạo), bolt standard, hướng đặt. Chỉnh khi kết quả trong Tekla lệch.
        public string VProfile = "", StripProfile = "";   // trống = tự tạo; V gia cường dùng chung profile V khung
        public PlaneKind VPlane = PlaneKind.Middle, StripPlane = PlaneKind.Middle;   // Middle: đặt theo tim -> V và nẹp trùng tim
        public RotationKind VRotation = RotationKind.Front, StripRotation = RotationKind.Front;
        public DepthKind VDepth = DepthKind.Middle, StripDepth = DepthKind.Middle;
        public double VLateral = 0, StripLateral = 0;   // lệch ngang tinh chỉnh (mm, dương = vào trong khung)
        public bool AutoPlacement = true;               // tự đo hình khối thật Tekla tạo ra rồi canh V và nẹp (khuyến nghị); tắt = dùng đúng các ô On plane/Rotation/At depth
        public bool FlipMiterCut = false;

        /// <summary>Kiểm tra dữ liệu nhập. Rỗng = hợp lệ.</summary>
        public List<string> Validate()
        {
            var e = new List<string>();
            if (VLegA <= 0 || VLegB <= 0 || VThk <= 0) e.Add("Kích thước V phải > 0.");
            else if (VThk >= Math.Min(VLegA, VLegB)) e.Add("Độ dày V phải nhỏ hơn cạnh V.");
            if (MeshGap < 0) e.Add("Khoảng lùi lưới không được âm.");
            if (MeshWireDia <= 0 || MeshPitch <= MeshWireDia) e.Add("Bước sợi phải lớn hơn đường kính sợi.");
            if (UseStrip)
            {
                if (StripWidth <= 0 || StripThk <= 0) e.Add("Kích thước nẹp phải > 0.");
                else if (StripWidth <= MeshGap) e.Add("Bề rộng nẹp phải lớn hơn khoảng lùi lưới (nẹp phải đè lên mép lưới).");
            }
            if (StiffMode != StiffMode.None)
            {
                if (StiffMode == StiffMode.Equal && StiffMax <= 0) e.Add("Khoảng cách ô tối đa phải > 0.");
                if (StiffBaseEdge < 0) e.Add("Cạnh gốc gia cường không hợp lệ.");
                if (StiffMode == StiffMode.Custom)
                {
                    List<double> cells; string err;
                    if (!CellParser.TryParse(StiffCells, out cells, out err)) e.Add("Bề rộng các ô: " + err);
                }
            }
            return e;
        }
    }

    /// <summary>Đọc danh sách bề rộng ô: "1000 2*1500, 800" (cách nhau bằng khoảng trắng, dấu phẩy hoặc chấm phẩy; n*x = lặp n lần).</summary>
    public static class CellParser
    {
        public static bool TryParse(string text, out List<double> cells, out string error)
        {
            cells = new List<double>(); error = null;
            if (string.IsNullOrWhiteSpace(text)) { error = "chưa nhập."; return false; }
            foreach (string tok in text.Split(new[] { ' ', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int count = 1; string val = tok;
                int star = tok.IndexOf('*');
                if (star >= 0)
                {
                    if (!int.TryParse(tok.Substring(0, star), NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1)
                    { error = "'" + tok + "' sai (dạng n*x, n nguyên dương)."; return false; }
                    val = tok.Substring(star + 1);
                }
                double d;
                if (!double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d) || d <= 0)
                { error = "'" + tok + "' không phải số dương."; return false; }
                for (int i = 0; i < count; i++) cells.Add(d);
            }
            return true;
        }
    }
}
