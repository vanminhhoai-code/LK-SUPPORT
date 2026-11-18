// Số đo hiện trên hình (điểm, độ dài cạnh, chuỗi kích thước gia cường) và việc SỬA số đo thành sửa thật.
// Thuần (không WinForms / Tekla): dùng chung cho hình 2D và 3D, test được trên Linux.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MeshFrame.Core
{
    public enum DimKind { Point, Edge, Cell }

    /// <summary>Một số đo trên hình (toạ độ 2D trong mặt phẳng khung).</summary>
    public sealed class DimItem
    {
        public DimKind Kind;
        public int Index;               // Point/Edge: chỉ số 0..N-1;  Cell: chỉ số ô 0..cells-1
        public V2 A, B;                 // đoạn kích thước (Point: A = B = vị trí điểm)
        public V2 TextPos;              // vị trí đặt chữ
        public V2[] Extension;          // đường gióng: các cặp điểm (từ, tới)
        public string Text;
        public double Value;
        public bool Editable;
        public bool IsBaseEdge;
        public int Level;               // chuỗi ô: mức độ cao xen kẽ
    }

    public static class Annotations
    {
        static string N0(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

        /// <summary>Danh sách số đo từ kế hoạch (đa giác plan.Polygon ngược chiều kim đồng hồ, cùng thứ tự điểm pick).</summary>
        public static List<DimItem> Build(MeshFramePlan plan)
        {
            var res = new List<DimItem>();
            if (plan == null || plan.Polygon == null || plan.Polygon.Length < 3) return res;
            V2[] poly = plan.Polygon; int n = poly.Length;
            double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
            foreach (V2 p in poly) { x0 = Math.Min(x0, p.X); x1 = Math.Max(x1, p.X); y0 = Math.Min(y0, p.Y); y1 = Math.Max(y1, p.Y); }
            double off = Math.Max(30, 0.045 * Math.Max(x1 - x0, y1 - y0));

            for (int i = 0; i < n; i++) res.Add(new DimItem { Kind = DimKind.Point, Index = i, A = poly[i], B = poly[i], TextPos = poly[i], Text = "P" + (i + 1), Value = 0 });
            for (int i = 0; i < n; i++)
            {
                V2 a = poly[i], b = poly[(i + 1) % n];
                V2 outDir = (b - a).Unit().Left() * -1;                       // ra ngoài khung (đa giác ngược chiều kim đồng hồ)
                double len = (b - a).Length;
                res.Add(new DimItem
                {
                    Kind = DimKind.Edge, Index = i, A = a, B = b, TextPos = (a + b) * 0.5 + outDir * (off * 0.6),
                    Text = (i + 1) + ": " + N0(len), Value = len, Editable = true, IsBaseEdge = i == plan.BaseEdge
                });
            }
            if (plan.StiffTims.Count > 0 && plan.Cells.Count >= 2)
            {
                V2 u = plan.BaseDir, perp = u.Left(), o = plan.BaseOrigin;
                double ext = 0;                                               // cực ngoài của khung theo hướng ra ngoài cạnh gốc
                foreach (V2 p in poly) ext = Math.Max(ext, -(p - o).Dot(perp));
                var pos = new List<double> { plan.BaseMin };
                pos.AddRange(plan.StiffTims); pos.Add(plan.BaseMax);
                for (int i = 0; i + 1 < pos.Count; i++)
                {
                    int lvl = i % 2;
                    double d = ext + off * (1.2 + lvl * 1.1);
                    V2 a = o + u * pos[i] + perp * (-d), b = o + u * pos[i + 1] + perp * (-d);
                    var item = new DimItem
                    {
                        Kind = DimKind.Cell, Index = i, A = a, B = b, TextPos = (a + b) * 0.5 + perp * (-off * 0.35),
                        Text = N0(pos[i + 1] - pos[i]), Value = pos[i + 1] - pos[i], Editable = true, Level = lvl,
                        Extension = new[] { o + u * pos[i] + perp * (-ext), a, o + u * pos[i + 1] + perp * (-ext), b }
                    };
                    res.Add(item);
                }
            }
            return res;
        }
    }

    /// <summary>Kết quả một lần sửa số đo.</summary>
    public sealed class EditResult
    {
        public bool Ok; public string Error;
        public V3[] Points; public MeshFrameSettings Settings;
    }

    public static class DimEdit
    {
        /// <summary>Sửa số đo thành sửa thật. Điểm gốc (anchor) luôn đứng yên: các điểm ở phía xa điểm gốc dời theo.
        /// Cạnh: kéo giãn dọc cạnh, giữ góc.  Ô gia cường: ô trước ô cuối đổi thì ô cuối nhận phần dư; sửa ô cuối thì kích thước khung đổi.</summary>
        public static EditResult Apply(MeshFramePlan plan, Frame frame, MeshFrameSettings s, V3[] pts, DimKind kind, int index, double value)
        {
            var r = new EditResult();
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value)) { r.Error = "Giá trị phải là số dương."; return r; }
            int anchor = Math.Max(0, Math.Min(pts.Length - 1, s.AnchorPoint - 1));
            V3[] np = (V3[])pts.Clone();
            MeshFrameSettings ns = Clone(s);

            if (kind == DimKind.Edge)
            {
                int n = pts.Length;
                V3 a = pts[index], b = pts[(index + 1) % n];
                double len = (b - a).Length;
                V3 dir = (b - a).Unit();
                np = Shift(pts, a, dir, len, value - len, anchor);
            }
            else if (kind == DimKind.Cell)
            {
                if (plan == null || plan.Cells.Count < 2 || index < 0 || index >= plan.Cells.Count) { r.Error = "Không có ô này."; return r; }
                var cells = new List<double>(plan.Cells);
                V3 u3 = frame.U * plan.BaseDir.X + frame.V * plan.BaseDir.Y;
                V3 o3 = frame.ToWorld(plan.BaseOrigin, 0);
                int last = cells.Count - 1;
                if (index < last)
                {
                    double sumPrev = 0; for (int i = 0; i < last; i++) sumPrev += (i == index ? value : cells[i]);
                    double total = plan.BaseMax - plan.BaseMin;
                    double rest = total - sumPrev;
                    if (rest < 1) { r.Error = "Ô cuối còn lại quá nhỏ (" + rest.ToString("0.#", CultureInfo.InvariantCulture) + " mm): giảm số này hoặc sửa ô cuối."; return r; }
                    cells[index] = value; cells[last] = rest;
                }
                else
                {
                    double sumPrev = 0; for (int i = 0; i < last; i++) sumPrev += cells[i];
                    double delta = value - cells[last];
                    // các điểm ở sau tim V gia cường cuối (phía mép cuối khung) dời theo; điểm gốc đứng yên
                    np = Shift(pts, o3 + u3 * (plan.BaseMin + sumPrev), u3, 0, delta, anchor);
                    cells[last] = value;
                }
                ns.StiffMode = StiffMode.Custom;
                ns.StiffCells = FormatCells(cells);
            }
            else { r.Error = "Số này không sửa được."; return r; }

            // kiểm tra kết quả: khung hợp lệ, kế hoạch không lỗi
            Frame f2; V2[] poly; string err;
            if (!MeshFrameBuilder.TryFrame(np, ns.AnchorPoint - 1, out f2, out poly, out err)) { r.Error = err; return r; }
            MeshFramePlan p2 = MeshFramePlanner.Plan(ns, poly);
            if (p2.Errors.Count > 0) { r.Error = string.Join(" ", p2.Errors.ToArray()); return r; }
            r.Ok = true; r.Points = np; r.Settings = ns;
            return r;
        }

        /// <summary>Dời các điểm có chiếu dọc dir (tính từ origin) ≥ thr thêm delta*dir. Nếu điểm gốc nằm trong nhóm đó thì dời nhóm còn lại -delta*dir.</summary>
        public static V3[] Shift(V3[] pts, V3 origin, V3 dir, double thr, double delta, int anchor)
        {
            var inSet = new bool[pts.Length];
            for (int i = 0; i < pts.Length; i++) inSet[i] = (pts[i] - origin).Dot(dir) >= thr - 1e-6;
            bool moveSet = !inSet[anchor];
            var res = (V3[])pts.Clone();
            for (int i = 0; i < pts.Length; i++)
            {
                if (inSet[i] == moveSet) res[i] = pts[i] + dir * (moveSet ? delta : -delta);
            }
            return res;
        }

        public static string FormatCells(List<double> cells)
        {
            var sb = new StringBuilder();
            foreach (double c in cells) { if (sb.Length > 0) sb.Append(' '); sb.Append(c.ToString("0.###", CultureInfo.InvariantCulture)); }
            return sb.ToString();
        }

        public static MeshFrameSettings Clone(MeshFrameSettings s)
        {
            var c = new MeshFrameSettings();
            foreach (System.Reflection.FieldInfo f in typeof(MeshFrameSettings).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) f.SetValue(c, f.GetValue(s));
            return c;
        }
    }
}
