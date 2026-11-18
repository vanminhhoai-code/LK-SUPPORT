// Bộ tính hình học thuần: đa giác N điểm (lồi hoặc lõm, không tự cắt) + thông số -> MeshFramePlan.
// Chuẩn 05.TC.01: vật chủ -> cánh nằm V (z 0..t, cánh đứng hướng +Z) -> lưới (z -meshT..0) -> nẹp PL (phía ngoài).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MeshFrame.Core
{
    public static class MeshFramePlanner
    {
        static string F(double d) { return d.ToString("0.#", CultureInfo.InvariantCulture); }

        public static MeshFramePlan Plan(MeshFrameSettings s, V2[] polyIn)
        {
            var plan = new MeshFramePlan { AsmPrefix = s.AsmPrefix, AsmStartNo = s.AsmStartNo };
            plan.Errors.AddRange(s.Validate());
            if (polyIn == null || polyIn.Length < 3) plan.Errors.Add("Cần ít nhất 3 điểm.");
            else if (!Geo.IsSimple(polyIn)) plan.Errors.Add("Các điểm không được tự cắt, chạm nhau hoặc trùng nhau.");
            if (plan.Errors.Count > 0) { plan.Polygon = polyIn; return plan; }

            V2[] poly = (V2[])polyIn.Clone();
            if (Geo.Area(poly) < 0) Array.Reverse(poly);               // luôn ngược chiều kim đồng hồ
            plan.Polygon = poly;
            int n = poly.Length;
            if (Geo.Area(poly) < 1.0) { plan.Errors.Add("Diện tích khung quá nhỏ."); return plan; }

            plan.Placement = new PlanPlacement
            {
                VPlane = PlanPlacement.Name(s.VPlane), VRotation = PlanPlacement.Name(s.VRotation), VDepth = PlanPlacement.Name(s.VDepth),
                StripPlane = PlanPlacement.Name(s.StripPlane), StripRotation = PlanPlacement.Name(s.StripRotation), StripDepth = PlanPlacement.Name(s.StripDepth),
                FlipMiterCut = s.FlipMiterCut, AutoPlacement = s.AutoPlacement, VLateral = s.VLateral, StripLateral = s.StripLateral
            };
            WarnNonAscii(s, plan);

            double tV = s.VThk, tN = s.StripThk, w = s.StripWidth;
            double meshT = 2 * s.MeshWireDia;           // 2 lớp sợi đan
            double zStrip = -meshT;                      // mặt nẹp tiếp xúc lưới

            // Kiểu nối góc "một cạnh chạy dài, một cạnh dừng": cạnh SONG SONG cạnh gốc chạy dài (đè qua góc),
            // các cạnh còn lại dừng sát mặt trong; góc giữa hai cạnh cùng loại vẫn cắt vát.
            bool[] through = null;
            if (s.Corner == CornerKind.OneThrough)
            {
                int bEdge = s.StiffBaseEdge >= 1 && s.StiffBaseEdge <= n ? s.StiffBaseEdge - 1 : LongestEdge(poly);
                through = ThroughEdges(poly, bEdge);
                int tc = 0; foreach (bool t in through) if (t) tc++;
                plan.Notes.Add("Nối góc: " + tc + " cạnh chạy dài (song song cạnh " + (bEdge + 1) + "), " + (n - tc) + " cạnh dừng/cắt gọt.");
            }
            double d0 = s.StripOffset();                 // nẹp phủ [d0, d0 + w] tính từ mép ngoài khung vào trong

            // ---------- Lưới: 1 tấm liên tục, lùi MeshGap so với mép ngoài khung
            V2[] meshPoly = Geo.Offset(poly, s.MeshGap);
            if (!Geo.OffsetValid(poly, meshPoly)) { plan.Errors.Add("Khung quá nhỏ hoặc hẹp so với khoảng lùi lưới."); return plan; }
            string grade = !string.IsNullOrEmpty(s.RebarGrade) ? s.RebarGrade : s.MeshGrade;
            string size = !string.IsNullOrEmpty(s.RebarSize) ? s.RebarSize : s.MeshWireDia.ToString("0.###", CultureInfo.InvariantCulture);
            plan.Mesh = new PlanMesh
            {
                Polygon = meshPoly, ZBottom = -meshT, Thickness = meshT, WireDia = s.MeshWireDia, Pitch = s.MeshPitch,
                WeightKgPerM2 = s.MeshWeightKgPerM2, Grade = s.MeshGrade, Name = s.MeshName, Prefix = s.MeshPrefix,
                RebarGrade = grade, RebarSize = size, Class = s.MeshClass, StartNo = s.MeshStartNo, Kind = s.MeshKind,
                IsRectangle = IsRectangle(meshPoly)
            };
            if (s.MeshKind == MeshKind.None) plan.Mesh = null;

            // ---------- Thanh V khung: một thanh cho mỗi cạnh (đường tham chiếu = mép ngoài cánh đứng, cánh nằm quay vào trong)
            for (int k = 0; k < n; k++)
            {
                V2 a, b; V2? ca, cb;
                EdgeEnds(poly, k, 0, 0, tV, through, out a, out b, out ca, out cb);
                if ((b - a).Dot(poly[(k + 1) % n] - poly[k]) <= 1.0) { plan.Errors.Add("Cạnh " + (k + 1) + " quá ngắn so với V khung."); return plan; }
                var fbar = new PlanBar
                {
                    Role = BarRole.Frame, Start = a, End = b, CutStart = ca, CutEnd = cb,
                    LegA = s.VLegA, LegB = s.VLegB, Thk = tV, ZRef = 0,
                    Material = s.VMaterial, Name = s.VName, Class = s.VClass, Prefix = s.VPrefix, StartNo = s.VStartNo, ProfileOverride = s.VProfile
                };
                if (through != null)
                {
                    // Đầu dừng sát cạnh chạy dài: cánh nằm khuyết phần chồng với cánh nằm của cạnh chạy dài
                    int kp = (k + n - 1) % n, kn = (k + 1) % n;
                    V2 dir = (b - a).Unit(), nl = dir.Left();
                    if (!through[k] && through[kp]) fbar.Notches.Add(NotchQuad(poly, a, dir, nl, kp, tV, s.VLegA));
                    if (!through[k] && through[kn]) fbar.Notches.Add(NotchQuad(poly, b, dir, nl, kn, tV, s.VLegA));
                }
                plan.Bars.Add(fbar);
            }

            // ---------- Nẹp quanh khung: PL nằm dưới cánh nằm V (ngoài lưới), mép ngoài trùng mép ngoài V, rộng StripWidth
            if (s.UseStrip)
            {
                V2[] ringIn = Geo.Offset(poly, d0 + w);
                if (!Geo.OffsetValid(poly, ringIn)) { plan.Errors.Add("Khung quá nhỏ hoặc hẹp so với nẹp (nẹp phủ tới " + F(d0 + w) + " mm tính từ mép ngoài)."); return plan; }
                for (int k = 0; k < n; k++)
                {
                    V2 a, b; V2? ca, cb;
                    EdgeEnds(poly, k, d0 + w / 2, d0, d0 + w, through, out a, out b, out ca, out cb);
                    if ((b - a).Dot(poly[(k + 1) % n] - poly[k]) <= 1.0) { plan.Errors.Add("Cạnh " + (k + 1) + " quá ngắn so với nẹp."); return plan; }
                    plan.Strips.Add(NewStrip(s, a, b, ca, cb, zStrip, false));
                }
            }

            // ---------- Gia cường
            AddStiffeners(plan, s, poly, tV, zStrip, d0);
            WarnLimits(plan, s, poly);
            return plan;
        }

        // ---------------------------------------------------------------- gia cường
        private static void AddStiffeners(MeshFramePlan plan, MeshFrameSettings s, V2[] poly, double tV, double zStrip, double d0)
        {
            if (s.StiffMode == StiffMode.None) { plan.Notes.Add("Gia cường: không dùng."); return; }
            int n = poly.Length;
            int baseEdge = s.StiffBaseEdge >= 1 ? s.StiffBaseEdge - 1 : LongestEdge(poly);
            if (baseEdge >= n) { plan.Errors.Add("Cạnh gốc gia cường " + s.StiffBaseEdge + " không tồn tại (khung có " + n + " cạnh)."); return; }
            plan.BaseEdge = baseEdge;

            // Gia cường vuông góc cạnh gốc; các ô đo dọc cạnh gốc, bắt đầu từ mép khung xa nhất về phía P(gốc).
            V2 u = (poly[(baseEdge + 1) % n] - poly[baseEdge]).Unit();
            V2 perp = u.Left();
            double m0 = double.MaxValue, m1 = double.MinValue;
            foreach (V2 p in poly) { double c = (p - poly[baseEdge]).Dot(u); m0 = Math.Min(m0, c); m1 = Math.Max(m1, c); }
            double total = m1 - m0;

            List<double> cells = CellWidths(s, total, plan);
            if (plan.Errors.Count > 0) return;
            plan.Cells = cells;
            if (cells.Count <= 1)
            {
                plan.Notes.Add("Gia cường: không cần - kích thước dọc cạnh gốc " + F(total) + " mm " +
                               (s.StiffMode == StiffMode.Equal ? "≤ " + F(s.StiffMax) + " mm (ô tối đa)." : "chỉ có 1 ô."));
                return;
            }

            double w = s.StripWidth, legA = s.VLegA;
            V2[] Q = Geo.Offset(poly, tV);                              // mặt trong cánh đứng V khung
            if (!Geo.OffsetValid(poly, Q)) { plan.Errors.Add("Khung quá nhỏ hoặc hẹp để đặt gia cường."); return; }
            V2[] R = s.UseStrip ? Geo.Offset(poly, d0 + w) : null;       // mép trong nẹp chu vi
            if (R != null && !Geo.OffsetValid(poly, R)) R = null;

            double minLen = 2 * legA;                                    // ngắn hơn thì hai khuyết chiếm hết
            int dividers = cells.Count - 1, barsMade = 0, stripsMade = 0, skipped = 0;
            double cum = 0;
            for (int i = 0; i < dividers; i++)
            {
                cum += cells[i];
                V2 O = poly[baseEdge] + u * (m0 + cum);                  // điểm trên đường chia (mặt ngoài cánh đứng gia cường)
                foreach (Geo.LineSeg seg in Geo.ClipLine(Q, O, perp))
                {
                    V2 pa = O + perp * seg.T0, pb = O + perp * seg.T1;   // pa: đầu t nhỏ (cạnh E0), pb: đầu t lớn (cạnh E1)
                    if ((pb - pa).Length < minLen) { skipped++; continue; }
                    // Hướng Start->End = -perp, để bên TRÁI = +u: cánh nằm của gia cường hướng về ô kế tiếp.
                    V2 start = pb, end = pa;
                    int edgeStart = seg.E1, edgeEnd = seg.E0;
                    V2 nLeft = (end - start).Unit().Left();
                    var bar = new PlanBar
                    {
                        Role = BarRole.Stiffener, Start = start, End = end,
                        LegA = s.VLegA, LegB = s.VLegB, Thk = tV, ZRef = 0,        // cùng tiết diện V khung
                        Material = s.SMaterial, Name = s.SName, Class = s.SClass, Prefix = s.SPrefix, StartNo = s.SStartNo, ProfileOverride = s.VProfile
                    };
                    // Cắt khuyết: cánh đứng dừng ở mặt trong cánh đứng V khung (offset tV); cánh nằm dừng ở mép trong cánh nằm V khung
                    // (offset legA) -> khuyết phần cánh nằm (z từ 0 đến tV) trong vùng offset [tV, legA] ở mỗi đầu.
                    bar.Notches.Add(NotchQuad(poly, start, perp, nLeft, edgeStart, tV, legA));
                    bar.Notches.Add(NotchQuad(poly, end, perp, nLeft, edgeEnd, tV, legA));
                    plan.Bars.Add(bar);
                    barsMade++;
                }
                if (R != null)
                {
                    // Nẹp PL nằm ngay dưới cánh nằm gia cường: phủ offset [d0, d0 + w] về phía +u (d0 theo vị trí nẹp Giữa/Trái/Phải).
                    V2 O2 = O + u * (d0 + w / 2);
                    foreach (Geo.LineSeg seg in Geo.ClipLine(R, O2, perp))
                    {
                        V2 na = O2 + perp * seg.T0, nb = O2 + perp * seg.T1;
                        if ((nb - na).Length <= 1.0) continue;
                        plan.Strips.Add(NewStrip(s, na, nb, null, null, zStrip, true));
                        stripsMade++;
                    }
                }
            }
            if (barsMade == 0)
                plan.Warnings.Add("Không tạo được gia cường: " + dividers + " đường chia đều ngắn hơn " + F(minLen) + " mm hoặc nằm ngoài khung.");
            else if (skipped > 0)
                plan.Warnings.Add("Bỏ " + skipped + " đoạn gia cường ngắn hơn " + F(minLen) + " mm (không đủ chỗ cắt khuyết).");
            plan.Notes.Add("Gia cường: " + barsMade + " thanh theo " + cells.Count + " ô, vuông góc cạnh " + (baseEdge + 1) + ".");
            if (s.UseStrip)
                for (int i = 0; i < cells.Count; i++)
                    if (cells[i] <= w + legA) plan.Warnings.Add("Ô " + (i + 1) + " (" + F(cells[i]) + " mm) rất hẹp so với nẹp/gia cường.");
        }

        private static int LongestEdge(V2[] p)
        {
            int best = 0; double len = -1;
            for (int i = 0; i < p.Length; i++)
            {
                double l = (p[(i + 1) % p.Length] - p[i]).Length;
                if (l > len) { len = l; best = i; }
            }
            return best;
        }

        /// <summary>Hình tứ giác khuyết ở cánh nằm gia cường, vùng chồng với cánh nằm V khung: lệch ngang [-1, legA], dọc theo gia cường từ offset tV/2 đến legA.</summary>
        private static V2[] NotchQuad(V2[] poly, V2 end, V2 perp, V2 nLeft, int edge, double tV, double legA)
        {
            V2 l1 = end + nLeft * (-1), l2 = end + nLeft * legA;
            V2 oE1, dE1, oE2, dE2;
            Geo.OffsetLine(poly, edge, tV / 2, out oE1, out dE1);
            Geo.OffsetLine(poly, edge, legA, out oE2, out dE2);
            V2 p00, p01, p11, p10;
            Geo.Intersect(l1, perp, oE1, dE1, out p00);
            Geo.Intersect(l1, perp, oE2, dE2, out p01);
            Geo.Intersect(l2, perp, oE2, dE2, out p11);
            Geo.Intersect(l2, perp, oE1, dE1, out p10);
            return new[] { p00, p01, p11, p10 };
        }

        /// <summary>Cảnh báo khi vượt giới hạn của chuẩn: cạnh dài tối đa 4000, cạnh ngắn tối đa 1200, ô tối đa 1000.</summary>
        private static void WarnLimits(MeshFramePlan plan, MeshFrameSettings s, V2[] poly)
        {
            int b = plan.BaseEdge >= 0 ? plan.BaseEdge : LongestEdge(poly);
            V2 u = (poly[(b + 1) % poly.Length] - poly[b]).Unit(), v = u.Left();
            double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
            foreach (V2 p in poly)
            {
                double cu = (p - poly[b]).Dot(u), cv = (p - poly[b]).Dot(v);
                u0 = Math.Min(u0, cu); u1 = Math.Max(u1, cu); v0 = Math.Min(v0, cv); v1 = Math.Max(v1, cv);
            }
            double e1 = u1 - u0, e2 = v1 - v0, longSide = Math.Max(e1, e2), shortSide = Math.Min(e1, e2);
            if (longSide > 4000 + 0.5) plan.Warnings.Add("Cạnh dài " + F(longSide) + " mm vượt L tối đa 4000 của chuẩn.");
            if (shortSide > 1200 + 0.5) plan.Warnings.Add("Cạnh ngắn " + F(shortSide) + " mm vượt H tối đa 1200 của chuẩn.");
            foreach (double c in plan.Cells)
                if (c > 1000 + 0.5) { plan.Warnings.Add("Có ô lớn hơn 1000 mm (chuẩn: @ tối đa 1000)."); break; }
            if (s.UseStrip && s.StripWidth > s.VLegA + 0.01) plan.Warnings.Add("Nẹp rộng hơn cánh nằm V (chuẩn: PL rộng bằng cánh V).");
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
                // Không khóa kích thước: ô vượt mép khung thì cắt ở mép khung, phần dư bỏ.
                double cum = 0; int used = 0;
                foreach (double d in parsed)
                {
                    if (total - cum <= 0.5) break;
                    res.Add(Math.Min(d, total - cum)); cum += d; used++;
                }
                plan.Warnings.Add("Tổng bề rộng các ô (" + F(sum) + ") lớn hơn kích thước khung dọc cạnh gốc (" + F(total) + "): cắt ở mép khung, dùng " + used + "/" + parsed.Count + " ô.");
                return res;
            }
            res.AddRange(parsed);
            if (sum < total - 0.5)
            {
                res.Add(total - sum);
                plan.Warnings.Add("Tổng các ô nhỏ hơn khung, thêm ô cuối " + F(total - sum) + " mm.");
            }
            return res;
        }

        private static bool IsRectangle(V2[] p)
        {
            if (p.Length != 4) return false;
            for (int i = 0; i < 4; i++)
            {
                V2 a = p[(i + 1) % 4] - p[i], b = p[(i + 2) % 4] - p[(i + 1) % 4];
                if (Math.Abs(a.Dot(b)) > 1e-6 * a.Length * b.Length) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- thanh quanh khung
        /// <summary>Các cạnh song song cạnh gốc (kể cả chính nó): những cạnh này chạy dài ở kiểu nối "một chạy dài, một dừng".</summary>
        private static bool[] ThroughEdges(V2[] poly, int baseEdge)
        {
            int n = poly.Length;
            V2 u = (poly[(baseEdge + 1) % n] - poly[baseEdge]).Unit();
            var t = new bool[n];
            for (int k = 0; k < n; k++)
            {
                V2 d = (poly[(k + 1) % n] - poly[k]).Unit();
                t[k] = Math.Abs(u.X * d.Y - u.Y * d.X) < 1e-6;
            }
            return t;
        }

        /// <summary>
        /// Đầu mút của thanh dọc cạnh k.  c = khoảng dịch của đường tham chiếu (vào trong khung).
        /// through == null: cắt vát mọi góc. Ngược lại, tại góc giữa cạnh chạy dài và cạnh dừng:
        /// cạnh chạy dài kết thúc ở đường offset a của cạnh kề (mép ngoài), cạnh dừng kết thúc ở đường offset b của cạnh kề (mặt trong).
        /// Góc giữa hai cạnh cùng loại vẫn cắt vát.
        /// </summary>
        private static void EdgeEnds(V2[] poly, int k, double c, double a, double b, bool[] through,
                                     out V2 s, out V2 e, out V2? cutS, out V2? cutE)
        {
            int n = poly.Length, kp = (k + n - 1) % n, kn = (k + 1) % n;
            Vertex(poly, k, kp, k, c, a, b, through, out s, out cutS);
            Vertex(poly, k, kn, kn, c, a, b, through, out e, out cutE);
        }

        private static void Vertex(V2[] poly, int k, int other, int vertex, double c, double a, double b, bool[] through, out V2 pt, out V2? cut)
        {
            if (through == null || through[k] == through[other])
            {
                V2[] q = Geo.Offset(poly, c);
                pt = q[vertex]; cut = Geo.Bisector(poly, vertex);
                return;
            }
            double clr = through[k] ? a : b;
            V2 o, d, op, dp;
            Geo.OffsetLine(poly, k, c, out o, out d);
            Geo.OffsetLine(poly, other, clr, out op, out dp);
            Geo.Intersect(o, d, op, dp, out pt);
            cut = null;
        }

        private static PlanStrip NewStrip(MeshFrameSettings s, V2 a, V2 b, V2? ca, V2? cb, double z, bool onStiff)
        {
            return new PlanStrip
            {
                Start = a, End = b, CutStart = ca, CutEnd = cb, Width = s.StripWidth, Thk = s.StripThk, ZRef = z,
                Material = s.StripMaterial, Name = s.StripName, Class = s.StripClass, Prefix = s.StripPrefix,
                StartNo = s.StripStartNo, OnStiffener = onStiff, ProfileOverride = s.StripProfile
            };
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
        /// <summary>Hệ trục + đa giác 2D từ các điểm 3D (GLOBAL). Dùng chung cho xem trước trong app và tạo trong Tekla.</summary>
        public static bool TryFrame(V3[] corners, out Frame frame, out V2[] poly, out string error)
        {
            poly = null;
            if (!Frame.TryCreate(corners, out frame, out error)) return false;
            poly = new V2[corners.Length];
            for (int i = 0; i < corners.Length; i++) poly[i] = frame.To2D(corners[i]);
            return true;
        }

        /// <summary>Tính kế hoạch từ các điểm 3D rồi (nếu có writer) ghi vào model.</summary>
        public static BuildResult Build(MeshFrameSettings s, V3[] corners, IModelWriter writer)
        {
            var r = new BuildResult();
            Frame f; V2[] poly; string err;
            if (!TryFrame(corners, out f, out poly, out err)) { r.Errors.Add(err); return r; }
            r.Frame = f;
            r.Plan = MeshFramePlanner.Plan(s, poly);
            if (r.Plan.Errors.Count > 0) { r.Errors.AddRange(r.Plan.Errors); return r; }
            if (writer != null) writer.Write(r.Plan, f);
            r.Success = true;
            return r;
        }
    }
}
