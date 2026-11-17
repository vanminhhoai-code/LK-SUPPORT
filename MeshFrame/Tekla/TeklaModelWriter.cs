// Ghi MeshFramePlan vào Tekla qua TeklaApi (reflection) -> một bản chạy cho 21.1 / 2020 / 2025.
// Mọi thành viên Tekla được dùng đều bị test hợp đồng (Tests/) đối chiếu với DLL thật khi chạy ci/build.sh.
//
// Độ bền: lỗi nặng (thanh V / nẹp không tạo được) -> xóa mọi thứ đã tạo rồi ném lỗi (không để model dở dang).
//         lỗi nhẹ (lưới, vít, gộp assembly) -> ghi cảnh báo vào Messages và vẫn hoàn tất phần còn lại.
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

        /// <param name="model">Đối tượng Tekla.Structures.Model.Model đã có (Cover Tool: model của gateway); null = tạo mới.</param>
        public TeklaModelWriter(TeklaApi api, object model)
        {
            A = api;
            _model = model ?? A.New(A.ModelType("Model"));
        }

        public void Write(MeshFramePlan plan, Frame f)
        {
            P = plan.Placement;
            _created.Clear();
            object wph = A.Call(_model, "GetWorkPlaneHandler");
            object old = A.Call(wph, "GetCurrentTransformationPlane");
            try
            {
                // Làm việc trong hệ của khung: X = U, Y = V, Z = pháp tuyến N. Toạ độ của Frame là GLOBAL.
                object tp = A.New(A.ModelType("TransformationPlane"), A.Point(f.Origin.X, f.Origin.Y, f.Origin.Z),
                                  A.Vector(f.U.X, f.U.Y, f.U.Z), A.Vector(f.V.X, f.V.Y, f.V.Z));
                A.Call(wph, "SetCurrentTransformationPlane", tp);

                var bars = new List<object>();
                var strips = new List<object>();
                try
                {
                    foreach (PlanBar b in plan.Bars) bars.Add(InsertBar(b));
                    foreach (PlanStrip s in plan.Strips) strips.Add(InsertStrip(s));
                }
                catch (Exception)
                {
                    Rollback();                                // lỗi nặng: không để lại model dở dang
                    throw;
                }
                if (bars.Count == 0) return;

                object meshPart = null;
                if (plan.Mesh != null)
                {
                    try { meshPart = InsertMesh(plan.Mesh, bars[0]); }
                    catch (Exception ex) { Messages.Add("Lưới (" + plan.Mesh.Kind + ") không tạo được: " + Root(ex).Message + ". Các phần khác vẫn được tạo."); }
                }

                if (plan.Fasteners.Count > 0)
                {
                    try { InsertFasteners(plan, strips, bars[0]); }
                    catch (Exception ex) { Messages.Add("Vít không tạo được: " + Root(ex).Message + ". Các phần khác vẫn được tạo."); }
                }

                // Gộp thành 1 assembly, main = V khung đầu tiên
                try
                {
                    object asm = A.Call(bars[0], "GetAssembly");
                    for (int i = 1; i < bars.Count; i++) A.Call(asm, "Add", bars[i]);
                    foreach (object sp in strips) A.Call(asm, "Add", sp);
                    if (meshPart != null) A.Call(asm, "Add", meshPart);        // lưới dạng tấm là part; RebarMesh thuộc part cha
                    object num = A.Get(asm, "AssemblyNumber");
                    A.Set(num, "Prefix", plan.AsmPrefix);
                    A.Set(num, "StartNumber", plan.AsmStartNo);
                    A.Call(asm, "Modify");
                }
                catch (Exception ex) { Messages.Add("Chưa gộp được assembly: " + Root(ex).Message); }
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
        /// <summary>Đường tham chiếu của V trong Tekla: On plane = Middle thì đặt theo tim cánh nằm (cách mép ngoài legA/2 vào trong),
        /// At depth = Middle thì theo tim chiều cao (legB/2); cộng thêm lệch ngang người dùng nhập.</summary>
        private object InsertBar(PlanBar bar)
        {
            V2 n = (bar.End - bar.Start).Unit().Left();                       // phía cánh nằm (vào trong khung)
            double lat = (P.VPlane == "MIDDLE" ? bar.LegA / 2 : 0) + P.VLateral;
            double dz = P.VDepth == "MIDDLE" ? bar.LegB / 2 : 0;
            object beam = NewBeam(bar.Start + n * lat, bar.End + n * lat, bar.ZRef + dz);
            Describe(beam, bar.Profile, bar.Material, bar.Name, bar.Class, bar.Prefix, bar.StartNo, P.VPlane, P.VRotation, P.VDepth);
            A.Require(beam, "Insert", "thanh " + bar.Profile + " (kiểm tra profile trong catalog)");
            _created.Add(beam);
            AddMiter(beam, bar.Start, bar.CutStart);
            AddMiter(beam, bar.End, bar.CutEnd);
            foreach (V2[] notch in bar.Notches) AddNotch(beam, notch, bar.Thk, bar.Material);
            return beam;
        }

        private object InsertStrip(PlanStrip st)
        {
            V2 n = (st.End - st.Start).Unit().Left();
            double lat = P.StripLateral;
            double dz = P.StripDepth == "MIDDLE" ? -st.Thk / 2 : 0;           // nẹp nằm phía ngoài (-Z) mặt tiếp xúc lưới
            object beam = NewBeam(st.Start + n * lat, st.End + n * lat, st.ZRef + dz);
            Describe(beam, st.Profile, st.Material, st.Name, st.Class, st.Prefix, st.StartNo, P.StripPlane, P.StripRotation, P.StripDepth);
            A.Require(beam, "Insert", "nẹp " + st.Profile + " (kiểm tra profile trong catalog)");
            _created.Add(beam);
            AddMiter(beam, st.Start, st.CutStart);
            AddMiter(beam, st.End, st.CutEnd);
            return beam;
        }

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
        private object InsertMesh(PlanMesh m, object father)
        {
            return m.Kind == MeshKind.RebarMesh ? InsertRebarMesh(m, father) : InsertMeshPlate(m);
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

        private object InsertRebarMesh(PlanMesh m, object father)
        {
            double zc = m.ZBottom + m.Thickness / 2;
            object poly = A.New(A.ModelType("Polygon"));
            IList pts = (IList)A.Get(poly, "Points");
            foreach (V2 v in m.Polygon) pts.Add(A.Point(v.X, v.Y, zc));

            object mesh = A.New(A.ModelType("RebarMesh"));
            A.Set(mesh, "Father", father);
            A.Set(mesh, "Polygon", poly);
            A.Set(mesh, "MeshType", A.Enum("RebarMesh+RebarMeshTypeEnum", "POLYGON_MESH"));
            A.Set(mesh, "Name", m.Name);
            A.Set(mesh, "Grade", m.Grade);
            A.Set(mesh, "Class", m.Class);
            object ns = A.Get(mesh, "NumberingSeries");
            A.Set(ns, "Prefix", m.Prefix);
            A.Set(ns, "StartNumber", m.StartNo);
            string dia = Num(m.WireDia);
            A.Set(mesh, "LongitudinalSize", dia);
            A.Set(mesh, "CrossSize", dia);
            A.Set(mesh, "LongitudinalDistances", new ArrayList { m.Pitch });
            A.Set(mesh, "CrossDistances", new ArrayList { m.Pitch });
            A.Set(mesh, "StartPoint", A.Point(m.Polygon[0].X, m.Polygon[0].Y, zc));
            A.Set(mesh, "EndPoint", A.Point(m.Polygon[1].X, m.Polygon[1].Y, zc));
            A.Require(mesh, "Insert", "RebarMesh đường kính " + dia + " mác " + m.Grade);
            return null;                                                    // RebarMesh không phải part: không đưa vào assembly
        }

        // ------------------------------------------------------------------ vít / bu lông
        private void InsertFasteners(MeshFramePlan plan, List<object> strips, object bar0)
        {
            for (int si = 0; si < plan.Strips.Count; si++)
            {
                List<PlanFastener> pts = plan.Fasteners.FindAll(x => x.StripIndex == si);
                if (pts.Count == 0) continue;
                if (pts[0].Kind != FastenKind.Screw) { Messages.Add("Hàn điểm: chưa ghi vào model (chỉ vít/bu lông)."); return; }

                PlanFastener first = pts[0];
                double step = pts.Count > 1 ? (pts[1].Pos - pts[0].Pos).Length : 0;
                V2 dir = pts.Count > 1 ? (pts[1].Pos - pts[0].Pos).Unit() : new V2(1, 0);

                object bolt = A.New(A.ModelType("BoltArray"));
                A.Set(bolt, "PartToBeBolted", strips[si]);
                A.Set(bolt, "PartToBoltTo", bar0);
                double z0 = P.FlipBolt ? first.ZHead + first.Length : first.ZHead;   // mặc định đầu vít ở mặt ngoài nẹp
                A.Set(bolt, "FirstPosition", A.Point(first.Pos.X, first.Pos.Y, z0));
                A.Set(bolt, "SecondPosition", A.Point(first.Pos.X + dir.X, first.Pos.Y + dir.Y, z0));
                A.Set(bolt, "BoltSize", first.Dia);
                A.Set(bolt, "BoltStandard", P.BoltStandard);
                A.Set(bolt, "Tolerance", 0.0);
                A.Set(bolt, "BoltType", A.Enum("BoltGroup+BoltTypeEnum", "BOLT_TYPE_WORKSHOP"));
                A.Set(bolt, "Length", first.Length);
                for (int i = 1; i < pts.Count; i++) A.Call(bolt, "AddBoltDistX", step);
                A.Call(bolt, "AddBoltDistY", 0.0);
                object ok = A.Call(bolt, "Insert");
                if (ok is bool && !(bool)ok) Messages.Add("Không tạo được vít trên nẹp " + (si + 1) + " (kiểm tra bolt standard '" + P.BoltStandard + "').");
            }
        }

        private static string Num(double d) { return d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
        private static Exception Root(Exception e) { while (e.InnerException != null) e = e.InnerException; return e; }
    }
}
