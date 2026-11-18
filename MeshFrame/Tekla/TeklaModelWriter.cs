// Ghi MeshFramePlan vào Tekla qua TeklaApi (reflection) -> một bản chạy cho 21.1 / 2020 / 2025.
// Mọi thành viên Tekla được dùng đều bị test hợp đồng (Tests/) đối chiếu với DLL thật khi chạy ci/build.sh.
//
// Độ bền: lỗi nặng (thanh V / nẹp không tạo được) -> xóa mọi thứ đã tạo rồi ném lỗi (không để model dở dang).
//         lỗi nhẹ (lưới, gộp assembly) -> ghi cảnh báo vào Messages và vẫn hoàn tất phần còn lại.
//         RebarMesh bị Tekla từ chối -> thử dạng chữ nhật (nếu khung chữ nhật) -> cuối cùng thay bằng lưới dạng tấm mỏng.
using System;
using System.Collections;
using System.Collections.Generic;
using MeshFrame.Core;

namespace MeshFrame.Tekla
{
    public sealed class TeklaModelWriter : IModelWriter
    {
        private readonly TeklaApi A;
        private readonly object _model;
        private PlanPlacement P;                              // cách đặt lấy từ plan (chỉnh trong tab Tekla)
        private readonly List<object> _created = new List<object>();
        public readonly List<string> Messages = new List<string>();
        /// <summary>Thông tin (không phải cảnh báo): vd kết quả tự canh theo hình khối thật.</summary>
        public readonly List<string> Notes = new List<string>();

        private sealed class Calib { public string Rotation; public double Lat, Z; }
        private Calib _vCal, _sCal;                           // kết quả tự canh (null = dùng đặt theo thông số)

        /// <summary>Mã khung: nếu có thì ghi vào UDA của part chính để "Sửa khung đã chọn" tìm lại được.</summary>
        public string FrameId;

        /// <summary>Part chính (V khung đầu tiên) của khung vừa tạo; null nếu chưa tạo.</summary>
        public object MainPart { get; private set; }

        /// <summary>Part chính của khung cũ cần CẬP NHẬT TẠI CHỖ: part nào còn tương ứng thì sửa chính part đó (giữ GUID, số hiệu), thiếu thì thêm, thừa thì xóa.</summary>
        public object ReuseMain;
        private Dictionary<string, object> _old;
        private readonly List<object> _oldLeft = new List<object>();
        private readonly HashSet<object> _reused = new HashSet<object>();
        private int _updated, _added;

        /// <param name="model">Đối tượng Tekla.Structures.Model.Model đã có (Cover Tool: model của gateway); null = tạo mới.</param>
        public TeklaModelWriter(TeklaApi api, object model)
        {
            A = api;
            _model = model ?? A.New(A.ModelType("Model"));
        }

        public void Write(MeshFramePlan plan, Frame f)
        {
            P = plan.Placement;
            _created.Clear(); _reused.Clear(); _oldLeft.Clear(); _old = null; _updated = 0; _added = 0;
            MainPart = null;
            object wph = A.Call(_model, "GetWorkPlaneHandler");
            object old = A.Call(wph, "GetCurrentTransformationPlane");
            try
            {
                // Làm việc trong hệ của khung: X = U, Y = V, Z = pháp tuyến N. Toạ độ của Frame là GLOBAL.
                object tp = A.New(A.ModelType("TransformationPlane"), A.Point(f.Origin.X, f.Origin.Y, f.Origin.Z),
                                  A.Vector(f.U.X, f.U.Y, f.U.Z), A.Vector(f.V.X, f.V.Y, f.V.Z));
                A.Call(wph, "SetCurrentTransformationPlane", tp);

                _vCal = null; _sCal = null;
                if (P.AutoPlacement) Calibrate(plan);
                if (ReuseMain != null) CollectOld();

                var bars = new List<object>();
                var strips = new List<object>();
                try
                {
                    for (int i = 0; i < plan.Bars.Count; i++) bars.Add(InsertBar(plan.Bars[i], "B" + i));
                    for (int i = 0; i < plan.Strips.Count; i++) strips.Add(InsertStrip(plan.Strips[i], "S" + i));
                }
                catch (Exception)
                {
                    Rollback();                                // lỗi nặng: không để lại model dở dang
                    throw;
                }
                if (bars.Count == 0) return;
                MainPart = bars[0];
                int removed = 0;
                if (ReuseMain != null) removed = RemoveLeftovers();
                if (P.AutoPlacement) Verify(plan, bars, strips);       // đo lại trên CHÍNH các thanh vừa tạo, dời nếu lệch

                object meshPart = null;
                if (plan.Mesh != null)
                {
                    try { meshPart = InsertMesh(plan.Mesh, bars[0], plan.Polygon); if (meshPart != null) TeklaFrameStore.WriteKey(A, meshPart, "M"); }
                    catch (Exception ex) { Messages.Add("Lưới không tạo được: " + Root(ex).Message + ". Các phần khác vẫn được tạo."); }
                }

                // Gộp thành 1 assembly, main = V khung đầu tiên
                try
                {
                    object asm = A.Call(bars[0], "GetAssembly");
                    for (int i = 1; i < bars.Count; i++) if (!_reused.Contains(bars[i])) A.Call(asm, "Add", bars[i]);
                    foreach (object sp in strips) if (!_reused.Contains(sp)) A.Call(asm, "Add", sp);
                    if (meshPart != null) A.Call(asm, "Add", meshPart);        // lưới dạng tấm là part; RebarMesh thuộc part cha
                    object num = A.Get(asm, "AssemblyNumber");
                    A.Set(num, "Prefix", plan.AsmPrefix);
                    A.Set(num, "StartNumber", plan.AsmStartNo);
                    A.Call(asm, "Modify");
                }
                catch (Exception ex) { Messages.Add("Chưa gộp được assembly: " + Root(ex).Message); }

                if (ReuseMain != null)
                    Notes.Add("Cập nhật tại chỗ: sửa " + _updated + " part, thêm " + _added + ", xóa " + removed + " (lưới được tạo lại). Số hiệu và GUID của part được sửa giữ nguyên.");
                if (!string.IsNullOrEmpty(FrameId))
                {
                    try { TeklaFrameStore.WriteId(A, bars[0], FrameId); }
                    catch (Exception ex) { Messages.Add("Không gắn được mã khung (không thể 'Sửa khung đã chọn' sau này): " + Root(ex).Message); }
                }
            }
            finally
            {
                A.Call(wph, "SetCurrentTransformationPlane", old);
                A.Call(_model, "CommitChanges");
            }
        }

        private void Rollback()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                try { A.Call(_created[i], "Delete"); } catch (Exception) { }
            }
            _created.Clear();
        }

        // ------------------------------------------------------------------ thanh V / nẹp
        /// <summary>Đặt V trong Tekla. Tự canh (mặc định): xoay + lệch đã ĐO từ hình khối thật Tekla tạo ra, nên cánh nằm luôn ở dưới,
        /// cánh đứng ở mép ngoài hướng lên, đúng chiều bất kể quy ước On plane/Rotation/At depth của bản Tekla.
        /// Không tự canh: dùng các ô On plane/Rotation/At depth (Middle thì đặt theo tim) + lệch người dùng.</summary>
        // ------------------------------------------------------------------ cập nhật tại chỗ
        private static bool IsBeam(object o) { return o != null && o.GetType().Name == "Beam"; }

        /// <summary>Lập bảng khóa -> part của khung cũ (khóa B{i} / S{i} / M lưu trong UDA). Part chính cũ không có khóa thì coi là B0.</summary>
        private void CollectOld()
        {
            _old = new Dictionary<string, object>();
            object asm = A.Call(ReuseMain, "GetAssembly");
            var parts = new List<object>();
            parts.Add(ReuseMain);
            IEnumerable secs = asm == null ? null : A.Call(asm, "GetSecondaries") as IEnumerable;
            if (secs != null) foreach (object o in secs) parts.Add(o);
            foreach (object p in parts)
            {
                string key = TeklaFrameStore.ReadKey(A, p);
                if (key.Length == 0 && ReferenceEquals(p, ReuseMain)) key = "B0";
                if (key.Length > 0 && !_old.ContainsKey(key)) _old[key] = p; else _oldLeft.Add(p);
            }
        }

        /// <summary>Part cũ không còn trong kế hoạch + lưới cũ bị xóa (lưới tạo lại). Trả về số part đã xóa.</summary>
        private int RemoveLeftovers()
        {
            int n = 0;
            var all = new List<object>(_old.Values); all.AddRange(_oldLeft);
            TeklaFrameStore.DeleteReinforcements(A, ReuseMain);
            foreach (object o in all)
            {
                try { TeklaFrameStore.DeleteReinforcements(A, o); A.Call(o, "Delete"); n++; }
                catch (Exception ex) { Messages.Add("Không xóa được part thừa của khung cũ: " + Root(ex).Message); }
            }
            _old.Clear(); _oldLeft.Clear();
            return n;
        }

        /// <summary>Lấy part cũ cùng khóa để sửa (xóa các vết cắt cũ của nó, đặt lại điểm đầu / cuối), hoặc null nếu phải tạo mới.</summary>
        private object TakeOld(string key, V2 a, V2 b, double z)
        {
            object old;
            if (_old == null || !_old.TryGetValue(key, out old) || !IsBeam(old)) return null;
            _old.Remove(key);
            var cuts = new List<object>();
            object en = A.Call(old, "GetBooleans");
            while (en != null && (bool)A.Call(en, "MoveNext")) cuts.Add(A.Get(en, "Current"));
            foreach (object c in cuts) A.Call(c, "Delete");
            A.Set(old, "StartPoint", A.Point(a.X, a.Y, z));
            A.Set(old, "EndPoint", A.Point(b.X, b.Y, z));
            return old;
        }

        private void Commit(object beam, object old, string what, string key)
        {
            if (old != null) { A.Require(beam, "Modify", "cập nhật " + what); _reused.Add(beam); _updated++; }
            else { A.Require(beam, "Insert", what); _created.Add(beam); TeklaFrameStore.WriteKey(A, beam, key); _added++; }
        }

        private object InsertBar(PlanBar bar, string key)
        {
            V2 n = (bar.End - bar.Start).Unit().Left();                       // phía cánh nằm (vào trong khung)
            string plane = P.VPlane, rot = P.VRotation, depth = P.VDepth;
            double lat, dz;
            if (_vCal != null) { plane = "MIDDLE"; depth = "MIDDLE"; rot = _vCal.Rotation; lat = _vCal.Lat + P.VLateral; dz = _vCal.Z; }
            else { lat = (P.VPlane == "MIDDLE" ? bar.LegA / 2 : 0) + P.VLateral; dz = P.VDepth == "MIDDLE" ? bar.LegB / 2 : 0; }
            object old = TakeOld(key, bar.Start + n * lat, bar.End + n * lat, bar.ZRef + dz);
            object beam = old ?? NewBeam(bar.Start + n * lat, bar.End + n * lat, bar.ZRef + dz);
            Describe(beam, bar.Profile, bar.Material, bar.Name, bar.Class, bar.Prefix, bar.StartNo, plane, rot, depth);
            Commit(beam, old, "thanh " + bar.Profile + " (kiểm tra profile trong catalog)", key);
            AddMiter(beam, bar.Start, bar.CutStart);
            AddMiter(beam, bar.End, bar.CutEnd);
            foreach (V2[] notch in bar.Notches) AddNotch(beam, notch, bar.Thk, bar.Material);
            return beam;
        }

        private object InsertStrip(PlanStrip st, string key)
        {
            V2 n = (st.End - st.Start).Unit().Left();
            string plane = P.StripPlane, rot = P.StripRotation, depth = P.StripDepth;
            double lat, dz;
            if (_sCal != null) { plane = "MIDDLE"; depth = "MIDDLE"; rot = _sCal.Rotation; lat = _sCal.Lat + P.StripLateral; dz = _sCal.Z; }
            else { lat = P.StripLateral; dz = P.StripDepth == "MIDDLE" ? -st.Thk / 2 : 0; }   // nẹp nằm phía ngoài (-Z) mặt tiếp xúc lưới
            object old = TakeOld(key, st.Start + n * lat, st.End + n * lat, st.ZRef + dz);
            object beam = old ?? NewBeam(st.Start + n * lat, st.End + n * lat, st.ZRef + dz);
            Describe(beam, st.Profile, st.Material, st.Name, st.Class, st.Prefix, st.StartNo, plane, rot, depth);
            Commit(beam, old, "nẹp " + st.Profile + " (kiểm tra profile trong catalog)", key);
            AddMiter(beam, st.Start, st.CutStart);
            AddMiter(beam, st.End, st.CutEnd);
            return beam;
        }

        // ------------------------------------------------------------------ tự canh theo hình khối thật
        private static readonly string[] Rotations = { "FRONT", "TOP", "BACK", "BELOW" };

        private void Calibrate(MeshFramePlan plan)
        {
            PlanBar bar = null; foreach (PlanBar b in plan.Bars) if (b.Role == BarRole.Frame) { bar = b; break; }
            PlanStrip strip = plan.Strips.Count > 0 ? plan.Strips[0] : null;
            try
            {
                if (bar != null) _vCal = CalibrateAngle(bar, P.VRotation);
                if (strip != null) _sCal = CalibrateFlat(strip, P.StripRotation);
                if (bar != null && _vCal == null) Messages.Add("Không tự canh được V theo hình khối thật (không tìm được hướng xoay cho thép góc " + bar.Profile + "): dùng đặt theo thông số, có thể lệch.");
                if (strip != null && _sCal == null) Messages.Add("Không tự canh được nẹp theo hình khối thật (profile " + strip.Profile + " không ra dạng dẹt " + F(strip.Width) + "x" + F(strip.Thk) + "): dùng đặt theo thông số, có thể lệch.");
                if (_vCal != null || _sCal != null)
                    Notes.Add("Tự canh theo hình khối thật Tekla: " + (_vCal == null ? "" : "V xoay " + _vCal.Rotation + ", lệch ngang " + F(_vCal.Lat) + " mm, lệch cao " + F(_vCal.Z) + " mm")
                              + (_vCal != null && _sCal != null ? "; " : "") + (_sCal == null ? "" : "nẹp xoay " + _sCal.Rotation + ", lệch ngang " + F(_sCal.Lat) + " mm, lệch cao " + F(_sCal.Z) + " mm") + ".");
            }
            catch (Exception ex)
            {
                _vCal = null; _sCal = null;
                Messages.Add("Không đo được hình khối Tekla (" + Root(ex).GetType().Name + ": " + Root(ex).Message + "): dùng đặt theo thông số, có thể lệch.");
            }
        }

        private static List<string> RotationOrder(string first)
        {
            var l = new List<string> { first };
            foreach (string r in Rotations) if (r != first) l.Add(r);
            return l;
        }

        private sealed class Box { public double XMin, XMax, YMin, YMax, ZMin, ZMax; public object Solid; }

        /// <summary>Tạo thanh mẫu dọc trục X của mặt phẳng khung, On plane / At depth = MIDDLE, đo hình khối thật rồi xóa thanh mẫu.</summary>
        private T WithSample<T>(string profile, string material, string rotation, Func<Box, T> use)
        {
            object beam = NewBeam(new V2(0, 0), new V2(300, 0), 0);
            try
            {
                Describe(beam, profile, material, "MF_CALIB", "0", "X", 1, "MIDDLE", rotation, "MIDDLE");
                A.Require(beam, "Insert", "thanh mẫu " + profile);
                A.Call(_model, "CommitChanges");
                object solid = A.Call(beam, "GetSolid");
                if (solid == null) throw new InvalidOperationException("Tekla không trả về hình khối của thanh mẫu.");
                object mn = A.Get(solid, "MinimumPoint"), mx = A.Get(solid, "MaximumPoint");
                var box = new Box
                {
                    Solid = solid,
                    XMin = A.GetDouble(mn, "X"), YMin = A.GetDouble(mn, "Y"), ZMin = A.GetDouble(mn, "Z"),
                    XMax = A.GetDouble(mx, "X"), YMax = A.GetDouble(mx, "Y"), ZMax = A.GetDouble(mx, "Z")
                };
                if (Math.Abs((box.XMax - box.XMin) - 300) > 1.0)
                    throw new InvalidOperationException("hình khối đo được không khớp thanh mẫu (chiều dài " + F(box.XMax - box.XMin) + " thay vì 300) - toạ độ của Tekla khác work plane?");
                return use(box);
            }
            finally { try { A.Call(beam, "Delete"); } catch (Exception) { } }
        }

        /// <summary>Độ trải (max - min) theo trục 'Y' hay 'Z' của các điểm giao giữa đoạn thẳng và hình khối.</summary>
        private double Span(object solid, double x, double y1, double z1, double y2, double z2, char axis)
        {
            IList pts = A.Call(solid, "Intersect", A.Point(x, y1, z1), A.Point(x, y2, z2)) as IList;
            if (pts == null || pts.Count < 2) return 0;
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (object p in pts) { double v = A.GetDouble(p, axis == 'Y' ? "Y" : "Z"); lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
            return hi - lo;
        }

        /// <summary>Thép góc: tìm hướng xoay để cánh nằm (legA) ở dưới, cánh đứng (legB) ở mép NGOÀI (y nhỏ) hướng lên (+Z); rồi tính lệch để góc ngoài-dưới trùng đường tham chiếu.</summary>
        private Calib CalibrateAngle(PlanBar bar, string first)
        {
            foreach (string rot in RotationOrder(first))
            {
                Calib c = WithSample<Calib>(bar.Profile, bar.Material, rot, delegate (Box m)
                {
                    double sy = m.YMax - m.YMin, sz = m.ZMax - m.ZMin;
                    if (Math.Abs(sy - bar.LegA) > 0.6 || Math.Abs(sz - bar.LegB) > 0.6) return null;      // cánh dài sai trục
                    double x = (m.XMin + m.XMax) / 2, t = bar.Thk;
                    bool hBottom = Span(m.Solid, x, m.YMin - 2, m.ZMin + t / 2, m.YMax + 2, m.ZMin + t / 2, 'Y') > 0.7 * sy
                                   && Span(m.Solid, x, m.YMin - 2, m.ZMax - t / 2, m.YMax + 2, m.ZMax - t / 2, 'Y') < 0.7 * sy;
                    bool vOuter = Span(m.Solid, x, m.YMin + t / 2, m.ZMin - 2, m.YMin + t / 2, m.ZMax + 2, 'Z') > 0.7 * sz
                                  && Span(m.Solid, x, m.YMax - t / 2, m.ZMin - 2, m.YMax - t / 2, m.ZMax + 2, 'Z') < 0.7 * sz;
                    return hBottom && vOuter ? new Calib { Rotation = rot, Lat = -m.YMin, Z = -m.ZMin } : null;
                });
                if (c != null) return c;
            }
            return null;
        }

        /// <summary>Thép dẹt: tìm hướng xoay để bề rộng nằm ngang (y) và bề dày theo Z; rồi canh tim ngang trùng đường tham chiếu, mặt trên chạm mặt lưới (z = 0).</summary>
        private Calib CalibrateFlat(PlanStrip st, string first)
        {
            foreach (string rot in RotationOrder(first))
            {
                Calib c = WithSample<Calib>(st.Profile, st.Material, rot, delegate (Box m)
                {
                    double sy = m.YMax - m.YMin, sz = m.ZMax - m.ZMin;
                    if (Math.Abs(sy - st.Width) > 0.3 || Math.Abs(sz - st.Thk) > 0.3) return null;
                    return new Calib { Rotation = rot, Lat = -(m.YMin + m.YMax) / 2, Z = -m.ZMax };
                });
                if (c != null) return c;
            }
            return null;
        }

        // ------------------------------------------------------------------ đo lại trên các thanh vừa tạo (vòng kín)
        private sealed class Offs { public double Lat, Z; }

        /// <summary>Các toạ độ (ngang theo n, hoặc z) của giao điểm giữa đoạn thăm dò và hình khối thật; rỗng nếu không trúng.</summary>
        private List<double> Probe(object solid, V2 origin, V2 n, double lat1, double z1, double lat2, double z2, bool wantLat)
        {
            V2 a = origin + n * lat1, b = origin + n * lat2;
            IList pts = A.Call(solid, "Intersect", A.Point(a.X, a.Y, z1), A.Point(b.X, b.Y, z2)) as IList;
            var res = new List<double>();
            if (pts == null) return res;
            foreach (object p in pts)
            {
                if (wantLat) res.Add((new V2(A.GetDouble(p, "X"), A.GetDouble(p, "Y")) - origin).Dot(n));
                else res.Add(A.GetDouble(p, "Z"));
            }
            return res;
        }

        private static void MinMax(List<double> v, out double lo, out double hi)
        {
            lo = double.MaxValue; hi = double.MinValue;
            foreach (double d in v) { lo = Math.Min(lo, d); hi = Math.Max(hi, d); }
        }

        /// <summary>Đo cánh nằm (ngang) và cánh đứng của V tại giữa thanh. Trả về độ lệch so với kế hoạch, hoặc null + lý do.</summary>
        private Offs MeasureBar(PlanBar b, object beam, out string why)
        {
            why = null;
            V2 d = (b.End - b.Start).Unit(), n = d.Left(), mid = (b.Start + b.End) * 0.5;
            object solid = A.Call(beam, "GetSolid");
            if (solid == null) { why = "Tekla không trả về hình khối"; return null; }
            double t = b.Thk, e = 30;
            // 1) tia đứng qua giữa cánh nằm -> cao độ cánh nằm;  2) tia ngang giữa độ dày cánh nằm -> mép ngang;  3) tia đứng qua cánh đứng -> chiều cao
            List<double> v0 = Probe(solid, mid, n, b.LegA / 2, b.ZRef - e, b.LegA / 2, b.ZRef + b.LegB + e, false);
            if (v0.Count < 2) { why = "đường thăm dò không trúng hình khối"; return null; }
            double z0, z1; MinMax(v0, out z0, out z1);
            List<double> h = Probe(solid, mid, n, -e, (z0 + z1) / 2, b.LegA + e, (z0 + z1) / 2, true);
            if (h.Count < 2) { why = "đường thăm dò không trúng hình khối"; return null; }
            double lo, hi; MinMax(h, out lo, out hi);
            List<double> v = Probe(solid, mid, n, lo + t / 2, b.ZRef - e, lo + t / 2, b.ZRef + b.LegB + e, false);
            if (v.Count < 2) { why = "đường thăm dò không trúng hình khối"; return null; }
            double zlo, zhi; MinMax(v, out zlo, out zhi);
            if (Math.Abs((hi - lo) - b.LegA) > 1.0 || Math.Abs((zhi - zlo) - b.LegB) > 1.0)
            { why = "tiết diện đo được " + F(hi - lo) + "x" + F(zhi - zlo) + " khác " + F(b.LegA) + "x" + F(b.LegB); return null; }
            return new Offs { Lat = lo, Z = zlo - b.ZRef };
        }

        /// <summary>Đo nẹp tại giữa thanh: lệch tim ngang so với đường tim kế hoạch và lệch cao so với mặt tiếp xúc lưới.</summary>
        private Offs MeasureStrip(PlanStrip st, object beam, out string why)
        {
            why = null;
            V2 d = (st.End - st.Start).Unit(), n = d.Left(), mid = (st.Start + st.End) * 0.5;
            object solid = A.Call(beam, "GetSolid");
            if (solid == null) { why = "Tekla không trả về hình khối"; return null; }
            double e = 30, w = st.Width, t = st.Thk;
            List<double> v = Probe(solid, mid, n, 0, st.ZRef - t - e, 0, st.ZRef + e, false);
            if (v.Count < 2) { why = "đường thăm dò không trúng hình khối"; return null; }
            double zlo, zhi; MinMax(v, out zlo, out zhi);
            List<double> h = Probe(solid, mid, n, -(w / 2 + e), (zlo + zhi) / 2, w / 2 + e, (zlo + zhi) / 2, true);
            if (h.Count < 2) { why = "đường thăm dò không trúng hình khối"; return null; }
            double lo, hi; MinMax(h, out lo, out hi);
            if (Math.Abs((hi - lo) - w) > 1.0 || Math.Abs((zhi - zlo) - t) > 1.0)
            { why = "tiết diện đo được " + F(hi - lo) + "x" + F(zhi - zlo) + " khác " + F(w) + "x" + F(t); return null; }
            return new Offs { Lat = (lo + hi) / 2, Z = zhi - st.ZRef };
        }

        /// <summary>Dời thanh: ngang theo n (mm) và theo z (mm), rồi Modify.</summary>
        private void Shift(object beam, V2 n, double dLat, double dZ)
        {
            object sp = A.Get(beam, "StartPoint"), ep = A.Get(beam, "EndPoint");
            A.Set(beam, "StartPoint", A.Point(A.GetDouble(sp, "X") + n.X * dLat, A.GetDouble(sp, "Y") + n.Y * dLat, A.GetDouble(sp, "Z") + dZ));
            A.Set(beam, "EndPoint", A.Point(A.GetDouble(ep, "X") + n.X * dLat, A.GetDouble(ep, "Y") + n.Y * dLat, A.GetDouble(ep, "Z") + dZ));
            A.Call(beam, "Modify");
        }

        /// <summary>Vòng kín: đo hình khối THẬT của từng V và nẹp vừa tạo (cánh nằm, cánh đứng, nẹp), dời cho đúng vị trí kế hoạch
        /// (nẹp so với V theo Giữa / Trái / Phải) rồi đo lại. Báo số đo trong kết quả.</summary>
        private void Verify(MeshFramePlan plan, List<object> bars, List<object> strips)
        {
            try
            {
                A.Call(_model, "CommitChanges");
                int total = bars.Count + strips.Count, measured = 0, moved = 0;
                double before = 0, after = 0; string firstWhy = null;
                for (int i = 0; i < total; i++)
                {
                    bool isBar = i < bars.Count;
                    PlanBar b = isBar ? plan.Bars[i] : null; PlanStrip st = isBar ? null : plan.Strips[i - bars.Count];
                    object beam = isBar ? bars[i] : strips[i - bars.Count];
                    V2 n = isBar ? (b.End - b.Start).Unit().Left() : (st.End - st.Start).Unit().Left();
                    string why;
                    Offs o = isBar ? MeasureBar(b, beam, out why) : MeasureStrip(st, beam, out why);
                    if (o == null) { if (firstWhy == null) firstWhy = (isBar ? "V " : "nẹp ") + (isBar ? i + 1 : i - bars.Count + 1) + ": " + why; continue; }
                    measured++;
                    double dev = Math.Max(Math.Abs(o.Lat), Math.Abs(o.Z));
                    before = Math.Max(before, dev);
                    if (dev > 0.05)
                    {
                        Shift(beam, n, -o.Lat, -o.Z); moved++;
                        string why2;
                        Offs o2 = isBar ? MeasureBar(b, beam, out why2) : MeasureStrip(st, beam, out why2);
                        dev = o2 == null ? dev : Math.Max(Math.Abs(o2.Lat), Math.Abs(o2.Z));
                    }
                    after = Math.Max(after, dev);
                }
                if (measured == 0)
                    Messages.Add("Không đo lại được hình khối thật của các thanh vừa tạo (" + firstWhy + "): vị trí V / nẹp chưa được kiểm chứng.");
                else
                {
                    Notes.Add("Đo lại trên hình khối thật: " + measured + "/" + total + " thanh; dời " + moved + " thanh (lệch lớn nhất " + F(before) + " mm, sau khi dời còn " + F(after) + " mm).");
                    if (measured < total) Messages.Add("Chưa đo được " + (total - measured) + " thanh (" + firstWhy + ").");
                    if (after > 0.5) Messages.Add("Sau khi dời, một số thanh vẫn lệch tới " + F(after) + " mm so với kế hoạch.");
                }
            }
            catch (Exception ex) { Messages.Add("Không đo lại được hình khối (" + Root(ex).GetType().Name + ": " + Root(ex).Message + "): vị trí V / nẹp chưa được kiểm chứng."); }
        }

        private static string F(double d) { return d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }

        private object NewBeam(V2 a, V2 b, double z)
        {
            return A.New(A.ModelType("Beam"), A.Point(a.X, a.Y, z), A.Point(b.X, b.Y, z));
        }

        private void Describe(object beam, string profile, string material, string name, string cls, string prefix, int startNo,
                              string plane, string rotation, string depth)
        {
            A.Set(A.Get(beam, "Profile"), "ProfileString", profile);
            A.Set(A.Get(beam, "Material"), "MaterialString", material);
            A.Set(beam, "Name", name);
            A.Set(beam, "Class", cls);
            object pn = A.Get(beam, "PartNumber");
            A.Set(pn, "Prefix", prefix);
            A.Set(pn, "StartNumber", startNo);
            object pos = A.Get(beam, "Position");
            A.Set(pos, "Plane", A.Enum("Position+PlaneEnum", plane));
            A.Set(pos, "Rotation", A.Enum("Position+RotationEnum", rotation));
            A.Set(pos, "Depth", A.Enum("Position+DepthEnum", depth));
        }

        /// <summary>Cắt vát đầu thanh bằng Fitting: mặt phẳng đi qua góc khung, chứa hướng phân giác và pháp tuyến Z.</summary>
        private void AddMiter(object beam, V2 corner, V2? cutDir)
        {
            if (!cutDir.HasValue) return;
            V2 d = cutDir.Value;
            if (P.FlipMiterCut) d = d * -1;
            object fit = A.New(A.ModelType("Fitting"));
            A.Set(fit, "Father", beam);
            object plane = A.New(A.ModelType("Plane"));
            A.Set(plane, "Origin", A.Point(corner.X, corner.Y, 0));
            A.Set(plane, "AxisX", A.Vector(d.X, d.Y, 0));
            A.Set(plane, "AxisY", A.Vector(0, 0, 1));
            A.Set(fit, "Plane", plane);
            A.Call(fit, "Insert");
        }

        /// <summary>Khuyết cánh nằm: tấm ContourPlate làm "operative part" (class BlOpCl) + BooleanPart cắt, rồi xoá tấm.
        /// Theo ví dụ BooleanPart của Tekla Developer Center. Tấm nằm trong mặt phẳng khung, z từ 0 đến độ dày cánh.</summary>
        private void AddNotch(object beam, V2[] quad, double thk, string material)
        {
            object cutter = A.New(A.ModelType("ContourPlate"));
            foreach (V2 v in quad)
                A.Call(cutter, "AddContourPoint", A.New(A.ModelType("ContourPoint"), A.Point(v.X, v.Y, thk / 2), null));
            A.Set(A.Get(cutter, "Profile"), "ProfileString", "PL" + Num(thk));
            A.Set(A.Get(cutter, "Material"), "MaterialString", material);
            A.Set(cutter, "Class", A.Static(A.ModelType("BooleanPart"), "BooleanOperativeClassName"));
            A.Set(A.Get(cutter, "Position"), "Depth", A.Enum("Position+DepthEnum", "MIDDLE"));
            A.Require(cutter, "Insert", "tấm cắt khuyết");

            object cut = A.New(A.ModelType("BooleanPart"));
            A.Set(cut, "Father", beam);
            A.Call(cut, "SetOperativePart", cutter);
            object ok = A.Call(cut, "Insert");
            if (ok is bool && !(bool)ok) Messages.Add("Không cắt được khuyết đầu V gia cường.");
            A.Call(cutter, "Delete");
        }

        // ------------------------------------------------------------------ lưới
        /// <summary>Tạo lưới theo PlanMesh.Kind. Trả về part (lưới dạng tấm) để đưa vào assembly, hoặc null (RebarMesh / không có).</summary>
        private object InsertMesh(PlanMesh m, object father, V2[] frame)
        {
            if (m.Kind == MeshKind.Plate) return InsertMeshPlate(m);

            string why = null;
            // 1) mác thép + cỡ sợi phải có trong catalog rebar (nếu đọc được catalog)
            why = TeklaCatalog.Check(A, m.RebarGrade, m.RebarSize);
            if (why == null)
            {
                // 2) đa giác với đủ thuộc tính theo ví dụ chính thức
                try { InsertRebarMesh(m, father, true); return null; }
                catch (Exception ex) { why = "đa giác: " + Root(ex).Message; }
                // 3) khung chữ nhật -> thử RECTANGULAR_MESH
                if (m.IsRectangle)
                {
                    try { InsertRebarMesh(m, father, false); Messages.Add("RebarMesh dạng đa giác bị Tekla từ chối (" + why + "); đã tạo dạng chữ nhật."); return null; }
                    catch (Exception ex2) { why += " | chữ nhật: " + Root(ex2).Message; }
                }
            }
            Messages.Add("RebarMesh không tạo được: " + why + " -> đã thay bằng lưới dạng tấm mỏng.");
            return InsertMeshPlate(m);
        }

        /// <summary>Lưới dạng tấm mỏng (ContourPlate dày = 2 lớp sợi) nằm dưới cánh nằm V, phía ngoài. Khối lượng: nhập tay.</summary>
        private object InsertMeshPlate(PlanMesh m)
        {
            double zc = m.ZBottom + m.Thickness / 2;
            object cp = A.New(A.ModelType("ContourPlate"));
            foreach (V2 v in m.Polygon)
                A.Call(cp, "AddContourPoint", A.New(A.ModelType("ContourPoint"), A.Point(v.X, v.Y, zc), null));
            A.Set(A.Get(cp, "Profile"), "ProfileString", "PL" + Num(m.Thickness));
            A.Set(A.Get(cp, "Material"), "MaterialString", m.Grade);
            A.Set(cp, "Name", m.Name);
            A.Set(cp, "Class", m.Class.ToString(System.Globalization.CultureInfo.InvariantCulture));
            object pn = A.Get(cp, "PartNumber");
            A.Set(pn, "Prefix", m.Prefix);
            A.Set(pn, "StartNumber", m.StartNo);
            A.Set(A.Get(cp, "Position"), "Depth", A.Enum("Position+DepthEnum", "MIDDLE"));
            A.Require(cp, "Insert", "tấm lưới (kiểm tra mác thép/profile PL" + Num(m.Thickness) + ")");
            return cp;
        }

        /// <summary>RebarMesh với ĐỦ thuộc tính như ví dụ chính thức của Tekla (trang RebarMesh Class). Các thuộc tính kiểu liệt kê
        /// mặc định là UNDEFINED (MeshType, LongitudinalSpacingMethod, CrossBarLocation) nên bắt buộc phải đặt.</summary>
        private void InsertRebarMesh(PlanMesh m, object father, bool polygonType)
        {
            double zc = m.ZBottom + m.Thickness / 2;
            V2 a = m.Polygon[0], b = m.Polygon[1];
            V2 u = (b - a).Unit(), v = u.Left();
            double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
            foreach (V2 p in m.Polygon)
            {
                double cu = (p - a).Dot(u), cv = (p - a).Dot(v);
                u0 = Math.Min(u0, cu); u1 = Math.Max(u1, cu); v0 = Math.Min(v0, cv); v1 = Math.Max(v1, cv);
            }

            object mesh = A.New(A.ModelType("RebarMesh"));
            A.Set(mesh, "MeshType", A.Enum("RebarMesh+RebarMeshTypeEnum", polygonType ? "POLYGON_MESH" : "RECTANGULAR_MESH"));
            A.Set(mesh, "Father", father);
            if (polygonType)
            {
                object poly = A.New(A.ModelType("Polygon"));
                IList pts = (IList)A.Get(poly, "Points");
                foreach (V2 p in m.Polygon) pts.Add(A.Point(p.X, p.Y, zc));
                A.Set(mesh, "Polygon", poly);
            }
            A.Set(mesh, "StartPoint", A.Point(a.X, a.Y, zc));
            A.Set(mesh, "EndPoint", A.Point(b.X, b.Y, zc));
            A.Set(mesh, "CatalogName", "MeshFrame");
            A.Set(mesh, "LongitudinalSize", m.RebarSize);
            A.Set(mesh, "CrossSize", m.RebarSize);
            A.Set(mesh, "LeftOverhangCross", 0.0);
            A.Set(mesh, "LeftOverhangLongitudinal", 0.0);
            A.Set(mesh, "RightOverhangCross", 0.0);
            A.Set(mesh, "RightOverhangLongitudinal", 0.0);
            A.Set(mesh, "LongitudinalSpacingMethod", A.Enum("RebarMesh+RebarMeshSpacingMethodEnum", "SPACING_TYPE_SAME_DISTANCE"));
            ((IList)A.Get(mesh, "LongitudinalDistances")).Add(m.Pitch);
            ((IList)A.Get(mesh, "CrossDistances")).Add(m.Pitch);
            A.Set(mesh, "Name", m.Name);
            A.Set(mesh, "Class", m.Class);
            object ns = A.Get(mesh, "NumberingSeries");
            A.Set(ns, "Prefix", m.Prefix);
            A.Set(ns, "StartNumber", m.StartNo);
            A.Set(mesh, "Grade", m.RebarGrade);
            ((IList)A.Get(mesh, "OnPlaneOffsets")).Add(0.0);
            A.Set(mesh, "FromPlaneOffset", 0.0);
            A.Set(mesh, "StartPointOffsetType", A.Enum("Reinforcement+RebarOffsetTypeEnum", "OFFSET_TYPE_COVER_THICKNESS"));
            A.Set(mesh, "StartPointOffsetValue", 0.0);
            A.Set(mesh, "CutByFatherPartCuts", false);
            A.Set(mesh, "CrossBarLocation", A.Enum("RebarMesh+RebarMeshCrossBarLocationEnum", "LOCATION_TYPE_ABOVE"));
            A.Set(mesh, "Length", v1 - v0);
            A.Set(mesh, "Width", u1 - u0);
            A.Require(mesh, "Insert", "RebarMesh cỡ " + m.RebarSize + " mác " + m.RebarGrade);
        }

        private static string Num(double d) { return d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
        private static Exception Root(Exception e) { while (e.InnerException != null) e = e.InnerException; return e; }
    }
}
