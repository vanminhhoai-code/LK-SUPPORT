// Bộ tính hình học thuần: 4 điểm (2D, ngược chiều kim đồng hồ) + thông số -> MeshFramePlan.
using System;
using System.Collections.Generic;

namespace MeshFrame.Core
{
    public static class MeshFramePlanner
    {
        public static MeshFramePlan Plan(MeshFrameSettings s, V2[] poly)
        {
            var plan = new MeshFramePlan { AsmPrefix = s.AsmPrefix, AsmStartNo = s.AsmStartNo };
            plan.Errors.AddRange(s.Validate());
            if (poly == null || poly.Length != 4) plan.Errors.Add("Cần đúng 4 điểm.");
            else if (!IsConvexCcw(poly)) plan.Errors.Add("4 điểm phải tạo tứ giác lồi và pick liên tiếp quanh khung (P1-P2-P3-P4).");
            if (plan.Errors.Count > 0) return plan;

            plan.Placement = new PlanPlacement
            {
                VPlane = PlanPlacement.Name(s.VPlane), VRotation = PlanPlacement.Name(s.VRotation), VDepth = PlanPlacement.Name(s.VDepth),
                SPlane = PlanPlacement.Name(s.SPlane), SRotation = PlanPlacement.Name(s.SRotation), SDepth = PlanPlacement.Name(s.SDepth),
                StripPlane = PlanPlacement.Name(s.StripPlane), StripRotation = PlanPlacement.Name(s.StripRotation), StripDepth = PlanPlacement.Name(s.StripDepth),
                FlipMiterCut = s.FlipMiterCut, BoltStandard = s.BoltStandard
            };
            WarnNonAscii(s, plan);

            double tV = s.VThk;
            double meshT = 2 * s.MeshWireDia;           // 2 lớp sợi đan
            double stripZ = tV + meshT;

            // ---------- Lưới: 1 tấm liên tục, lùi MeshGap so với mặt trong cánh đứng V
            V2[] meshPoly = Geo.Offset(poly, tV + s.MeshGap);
            if (!Geo.OffsetValid(poly, meshPoly)) { plan.Errors.Add("Khung quá nhỏ so với V và khoảng lùi lưới."); return plan; }
            plan.Mesh = new PlanMesh
            {
                Polygon = meshPoly, ZBottom = tV, Thickness = meshT, WireDia = s.MeshWireDia, Pitch = s.MeshPitch,
                WeightKgPerM2 = s.MeshWeightKgPerM2, Grade = s.MeshGrade, Name = s.MeshName, Prefix = s.MeshPrefix,
                Class = s.MeshClass, StartNo = s.MeshStartNo
            };

            // ---------- 4 thanh V khung (đường tham chiếu = mép ngoài)
            for (int k = 0; k < 4; k++)
            {
                V2 a, b; V2? ca, cb;
                EdgeEnds(poly, k, 0, 0, tV, s.Corner, out a, out b, out ca, out cb);
                plan.Bars.Add(new PlanBar
                {
                    Role = BarRole.Frame, Start = a, End = b, CutStart = ca, CutEnd = cb,
                    LegA = s.VLegA, LegB = s.VLegB, Thk = tV, ZTop = tV, VerticalUp = true, Centered = false,
                    Material = s.VMaterial, Name = s.VName, Class = s.VClass, Prefix = s.VPrefix, StartNo = s.VStartNo, ProfileOverride = s.VProfile
                });
            }

            // ---------- Nẹp quanh khung: sát cánh đứng V, bề rộng StripWidth, đè lên mép lưới
            double w = s.StripWidth;
            if (s.UseStrip)
            {
                V2[] ringIn = Geo.Offset(poly, tV + w);
                if (!Geo.OffsetValid(poly, ringIn)) { plan.Errors.Add("Khung quá nhỏ so với V và nẹp."); return plan; }
                for (int k = 0; k < 4; k++)
                {
                    V2 a, b; V2? ca, cb;
                    EdgeEnds(poly, k, tV + w / 2, tV, tV + w, s.Corner, out a, out b, out ca, out cb);
                    var st = NewStrip(s, a, b, ca, cb, stripZ, false);
                    plan.Strips.Add(st);
                    AddFasteners(plan, s, a, b, stripZ, tV + meshT + s.StripThk, plan.Strips.Count - 1);
                }
            }

            // ---------- Gia cường: thanh V chia khung thành các ô, nẹp đi theo
            if (s.StiffMode != StiffMode.None) AddStiffeners(plan, s, poly, tV, meshT, stripZ);

            return plan;
        }

        // ---------------------------------------------------------------- gia cường
        private static void AddStiffeners(MeshFramePlan plan, MeshFrameSettings s, V2[] poly, double tV, double meshT, double stripZ)
        {
            bool d0 = s.StiffDir == StiffDir.AcrossP1P2;
            // d0: ô chia dọc cạnh P1->P2, thanh nối cạnh 0 với cạnh 2.  d1: ô chia dọc cạnh P1->P4, thanh nối cạnh 3 với cạnh 1.
            double total = d0 ? (poly[1] - poly[0]).Length : (poly[3] - poly[0]).Length;
            List<double> cells = CellWidths(s, total, plan);
            if (plan.Errors.Count > 0) return;
            plan.Cells = cells;
            if (cells.Count <= 1) return;

            int endA = d0 ? 0 : 3, endB = d0 ? 2 : 1;
            // Gia cường luôn VUÔNG GÓC với cạnh gốc (cạnh P1-P2 hoặc P1-P4); các ô đo dọc cạnh gốc từ P1.
            V2 baseDir = d0 ? (poly[1] - poly[0]).Unit() : (poly[3] - poly[0]).Unit();
            V2 perp = baseDir.Left();
            double w = s.StripWidth, cum = 0;
            for (int i = 0; i < cells.Count - 1; i++)
            {
                cum += cells[i];
                V2 A = poly[0] + baseDir * cum;
                V2 B = A + perp;

                V2 sa, sb;
                if (!ClipToEdges(poly, A, B, endA, endB, tV, out sa, out sb))
                { plan.Errors.Add("Thanh gia cường thứ " + (i + 1) + " vuông góc nằm ngoài khung (khung lệch quá hoặc cạnh trên/dưới không song song)."); return; }
                plan.Bars.Add(new PlanBar
                {
                    Role = BarRole.Stiffener, Start = sa, End = sb,
                    LegA = s.SLegA, LegB = s.SLegB, Thk = s.SThk, ZTop = tV, VerticalUp = false, Centered = true,
                    Material = s.SMaterial, Name = s.SName, Class = s.SClass, Prefix = s.SPrefix, StartNo = s.SStartNo, ProfileOverride = s.SProfile
                });

                if (s.UseStrip)
                {
                    V2 na, nb;
                    if (!ClipToEdges(poly, A, B, endA, endB, tV + w, out na, out nb))
                    { plan.Errors.Add("Nẹp gia cường thứ " + (i + 1) + " nằm ngoài khung."); return; }
                    plan.Strips.Add(NewStrip(s, na, nb, null, null, stripZ, true));
                    AddFasteners(plan, s, na, nb, stripZ, tV + meshT + s.StripThk, plan.Strips.Count - 1);
                }
            }
            if (s.UseStrip)
                for (int i = 0; i < cells.Count; i++)
                    if (cells[i] <= w + s.SLegA) plan.Warnings.Add("Ô " + (i + 1) + " (" + cells[i].ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " mm) rất hẹp so với nẹp/gia cường.");
        }

        private static List<double> CellWidths(MeshFrameSettings s, double total, MeshFramePlan plan)
        {
            var res = new List<double>();
            if (s.StiffMode == StiffMode.Equal)
            {
                int n = (int)Math.Ceiling(total / s.StiffMax - 1e-9);
                if (n < 1) n = 1;
                for (int i = 0; i < n; i++) res.Add(total / n);
                return res;
            }
            List<double> parsed; string err;
            CellParser.TryParse(s.StiffCells, out parsed, out err);
            double sum = 0;
            foreach (double d in parsed) sum += d;
            if (sum > total + 0.5)
            {
                plan.Errors.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Tổng bề rộng các ô ({0:0.#}) lớn hơn kích thước khung ({1:0.#}).", sum, total));
                return res;
            }
            res.AddRange(parsed);
            if (sum < total - 0.5)
            {
                res.Add(total - sum);
                plan.Warnings.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Tổng các ô nhỏ hơn khung, thêm ô cuối {0:0.#} mm.", total - sum));
            }
            return res;
        }

        private static bool ClipToEdges(V2[] poly, V2 a, V2 b, int e1, int e2, double d, out V2 pa, out V2 pb)
        {
            V2 o1, d1, o2, d2;
            Geo.OffsetLine(poly, e1, d, out o1, out d1);
            Geo.OffsetLine(poly, e2, d, out o2, out d2);
            V2 dir = b - a;
            bool ok1 = Geo.Intersect(a, dir, o1, d1, out pa);
            bool ok2 = Geo.Intersect(a, dir, o2, d2, out pb);
            if (!ok1 || !ok2 || (pb - pa).Length <= 1) return false;
            V2[] q = Geo.Offset(poly, d);
            return OnSegment(pa, q[e1], q[(e1 + 1) % 4]) && OnSegment(pb, q[e2], q[(e2 + 1) % 4]);
        }

        /// <summary>Chuỗi gửi sang Tekla (tên, class, prefix, profile...) có ký tự ngoài ASCII thì Tekla có thể hiển thị lỗi font.</summary>
        private static void WarnNonAscii(MeshFrameSettings s, MeshFramePlan plan)
        {
            var bad = new List<string>();
            foreach (System.Reflection.FieldInfo f in typeof(MeshFrameSettings).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (f.FieldType != typeof(string) || f.Name == "StiffCells") continue;
                string v = (string)f.GetValue(s);
                if (v == null) continue;
                foreach (char ch in v) if (ch > 127) { bad.Add(f.Name); break; }
            }
            if (bad.Count > 0) plan.Warnings.Add("Có chữ có dấu/ký tự ngoài ASCII trong: " + string.Join(", ", bad.ToArray()) + ". Tekla có thể hiển thị lỗi font, nên dùng không dấu.");
        }

        private static bool OnSegment(V2 p, V2 a, V2 b)
        {
            V2 ab = b - a;
            double t = (p - a).Dot(ab) / ab.Dot(ab);
            return t >= -1e-6 && t <= 1 + 1e-6;
        }

        // ---------------------------------------------------------------- thanh quanh khung
        /// <summary>
        /// Đầu mút của thanh dọc cạnh k.  c = khoảng dịch của đường tham chiếu;
        /// a = đường (của cạnh kề) mà thanh "chạy suốt" kết thúc;  b = đường mà thanh "bị cắt ngắn" kết thúc.
        /// </summary>
        private static void EdgeEnds(V2[] poly, int k, double c, double a, double b, CornerKind kind,
                                     out V2 s, out V2 e, out V2? cutS, out V2? cutE)
        {
            int n = 4, kp = (k + n - 1) % n, kn = (k + 1) % n;
            V2 o, d;
            Geo.OffsetLine(poly, k, c, out o, out d);
            if (kind == CornerKind.Miter)
            {
                V2[] q = Geo.Offset(poly, c);
                s = q[k]; e = q[kn];
                cutS = Geo.Bisector(poly, k); cutE = Geo.Bisector(poly, kn);
                return;
            }
            double clr = (k % 2 == 0) ? a : b;          // cạnh chẵn chạy suốt, cạnh lẻ cắt ngắn
            V2 op, dp, on, dn;
            Geo.OffsetLine(poly, kp, clr, out op, out dp);
            Geo.OffsetLine(poly, kn, clr, out on, out dn);
            Geo.Intersect(o, d, op, dp, out s);
            Geo.Intersect(o, d, on, dn, out e);
            cutS = null; cutE = null;
        }

        private static PlanStrip NewStrip(MeshFrameSettings s, V2 a, V2 b, V2? ca, V2? cb, double z, bool onStiff)
        {
            return new PlanStrip
            {
                Start = a, End = b, CutStart = ca, CutEnd = cb, Width = s.StripWidth, Thk = s.StripThk, ZBottom = z,
                Material = s.StripMaterial, Name = s.StripName, Class = s.StripClass, Prefix = s.StripPrefix,
                StartNo = s.StripStartNo, OnStiffener = onStiff, ProfileOverride = s.StripProfile
            };
        }

        // ---------------------------------------------------------------- liên kết @
        private static void AddFasteners(MeshFramePlan plan, MeshFrameSettings s, V2 a, V2 b, double zBottom, double length, int stripIndex)
        {
            if (s.Fasten == FastenKind.None) return;
            V2 dir = (b - a).Unit();
            double len = (b - a).Length, span = len - 2 * s.FastenEdge;
            var pos = new List<double>();
            if (span <= 0) pos.Add(len / 2);
            else
            {
                int cnt = (int)Math.Ceiling(span / s.FastenPitch - 1e-9) + 1;
                if (cnt < 2) cnt = 2;
                double step = span / (cnt - 1);
                for (int i = 0; i < cnt; i++) pos.Add(s.FastenEdge + i * step);
            }
            foreach (double t in pos)
                plan.Fasteners.Add(new PlanFastener { Pos = a + dir * t, Kind = s.Fasten, Dia = s.FastenDia, Length = length, ZTop = zBottom + s.StripThk, StripIndex = stripIndex });
        }

        private static bool IsConvexCcw(V2[] p)
        {
            for (int i = 0; i < 4; i++)
                if ((p[(i + 1) % 4] - p[i]).Cross(p[(i + 2) % 4] - p[(i + 1) % 4]) <= 1e-6) return false;
            return true;
        }
    }

    public class BuildResult
    {
        public bool Success;
        public List<string> Errors = new List<string>();
        public MeshFramePlan Plan;
        public Frame Frame;
    }

    public static class MeshFrameBuilder
    {
        /// <summary>Điểm vào cho app khác: tính kế hoạch từ 4 điểm 3D rồi (nếu có writer) ghi vào model.</summary>
        public static BuildResult Build(MeshFrameSettings s, V3[] corners, IModelWriter writer)
        {
            var r = new BuildResult();
            if (corners == null || corners.Length != 4) { r.Errors.Add("Cần đúng 4 điểm."); return r; }
            Frame f; string err;
            if (!Frame.TryCreate(corners, out f, out err)) { r.Errors.Add(err); return r; }
            var poly = new V2[4];
            for (int i = 0; i < 4; i++) poly[i] = f.To2D(corners[i]);
            r.Frame = f;
            r.Plan = MeshFramePlanner.Plan(s, poly);
            if (r.Plan.Errors.Count > 0) { r.Errors.AddRange(r.Plan.Errors); return r; }
            if (writer != null) writer.Write(r.Plan, f);
            r.Success = true;
            return r;
        }
    }
}
