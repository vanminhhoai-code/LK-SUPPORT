// Ghi MeshFramePlan vào Tekla qua TeklaApi (reflection) -> một bản chạy cho 21.1 / 2020 / 2025.
// Mọi thành viên Tekla được dùng đều bị test hợp đồng (Tests/) đối chiếu với DLL thật khi chạy ci/build.sh.
using System;
using System.Collections;
using System.Collections.Generic;
using MeshFrame.Core;

namespace MeshFrame.Tekla
{
    /// <summary>Cách đặt thép góc/nẹp so với đường tham chiếu (tên enum Tekla dạng chữ). Cần chỉnh 1 lần khi chạy thử trong Tekla.</summary>
    public class TeklaPlacement
    {
        public string VPlane = "LEFT", VRotation = "FRONT", VDepth = "FRONT";
        public string SPlane = "MIDDLE", SRotation = "FRONT", SDepth = "FRONT";
        public string StripPlane = "MIDDLE", StripRotation = "FRONT", StripDepth = "BEHIND";
        /// <summary>Đảo phía bị cắt của Fitting nếu mặt cắt vát ra ngược.</summary>
        public bool FlipMiterCut = false;
        public string BoltStandard = "8.8XOX";
    }

    public sealed class TeklaModelWriter : IModelWriter
    {
        private readonly TeklaApi A;
        private readonly object _model;
        public readonly TeklaPlacement Placement = new TeklaPlacement();
        public readonly List<string> Messages = new List<string>();

        /// <param name="model">Đối tượng Tekla.Structures.Model.Model đã có (Cover Tool: model của gateway); null = tạo mới.</param>
        public TeklaModelWriter(TeklaApi api, object model)
        {
            A = api;
            _model = model ?? A.New(A.ModelType("Model"));
        }

        public void Write(MeshFramePlan plan, Frame f)
        {
            object wph = A.Call(_model, "GetWorkPlaneHandler");
            object old = A.Call(wph, "GetCurrentTransformationPlane");
            try
            {
                // Làm việc trong hệ của khung: X = U, Y = V, Z = pháp tuyến N. Toạ độ của Frame là GLOBAL.
                object tp = A.New(A.ModelType("TransformationPlane"), A.Point(f.Origin.X, f.Origin.Y, f.Origin.Z),
                                  A.Vector(f.U.X, f.U.Y, f.U.Z), A.Vector(f.V.X, f.V.Y, f.V.Z));
                A.Call(wph, "SetCurrentTransformationPlane", tp);

                var bars = new List<object>();
                foreach (PlanBar b in plan.Bars) bars.Add(InsertBar(b));
                if (bars.Count == 0) return;

                var strips = new List<object>();
                foreach (PlanStrip s in plan.Strips) strips.Add(InsertStrip(s));

                if (plan.Mesh != null) InsertMesh(plan.Mesh, bars[0]);
                if (plan.Fasteners.Count > 0) InsertFasteners(plan, strips, bars[0]);

                // Gộp thành 1 assembly, main = V khung đầu tiên
                object asm = A.Call(bars[0], "GetAssembly");
                for (int i = 1; i < bars.Count; i++) A.Call(asm, "Add", bars[i]);
                foreach (object sp in strips) A.Call(asm, "Add", sp);
                object num = A.Get(asm, "AssemblyNumber");
                A.Set(num, "Prefix", plan.AsmPrefix);
                A.Set(num, "StartNumber", plan.AsmStartNo);
                A.Call(asm, "Modify");
            }
            finally
            {
                A.Call(wph, "SetCurrentTransformationPlane", old);
                A.Call(_model, "CommitChanges");
            }
        }

        // ------------------------------------------------------------------ thanh V / nẹp
        private object InsertBar(PlanBar bar)
        {
            bool stiff = bar.Role == BarRole.Stiffener;
            object beam = NewBeam(bar.Start, bar.End, bar.ZTop);
            Describe(beam, bar.Profile, bar.Material, bar.Name, bar.Class, bar.Prefix, bar.StartNo,
                     stiff ? Placement.SPlane : Placement.VPlane, stiff ? Placement.SRotation : Placement.VRotation, stiff ? Placement.SDepth : Placement.VDepth);
            A.Require(beam, "Insert", "thanh " + bar.Profile + " (kiểm tra profile trong catalog)");
            AddMiter(beam, bar.Start, bar.CutStart, bar.ZTop);
            AddMiter(beam, bar.End, bar.CutEnd, bar.ZTop);
            return beam;
        }

        private object InsertStrip(PlanStrip st)
        {
            object beam = NewBeam(st.Start, st.End, st.ZBottom);
            Describe(beam, st.Profile, st.Material, st.Name, st.Class, st.Prefix, st.StartNo,
                     Placement.StripPlane, Placement.StripRotation, Placement.StripDepth);
            A.Require(beam, "Insert", "nẹp " + st.Profile + " (kiểm tra profile trong catalog)");
            AddMiter(beam, st.Start, st.CutStart, st.ZBottom);
            AddMiter(beam, st.End, st.CutEnd, st.ZBottom);
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

        /// <summary>Cắt vát đầu thanh bằng Fitting: mặt phẳng chứa hướng phân giác và pháp tuyến Z.</summary>
        private void AddMiter(object beam, V2 end, V2? cutDir, double z)
        {
            if (!cutDir.HasValue) return;
            V2 d = cutDir.Value;
            if (Placement.FlipMiterCut) d = d * -1;
            object fit = A.New(A.ModelType("Fitting"));
            A.Set(fit, "Father", beam);
            object plane = A.New(A.ModelType("Plane"));
            A.Set(plane, "Origin", A.Point(end.X, end.Y, z));
            A.Set(plane, "AxisX", A.Vector(d.X, d.Y, 0));
            A.Set(plane, "AxisY", A.Vector(0, 0, 1));
            A.Set(fit, "Plane", plane);
            A.Call(fit, "Insert");
        }

        // ------------------------------------------------------------------ lưới (Reinforcement Mesh)
        private void InsertMesh(PlanMesh m, object father)
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
            object ok = A.Call(mesh, "Insert");
            if (ok is bool && !(bool)ok) Messages.Add("Không tạo được lưới (RebarMesh): kiểm tra đường kính " + dia + " / mác " + m.Grade + " trong catalog rebar.");
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
                A.Set(bolt, "FirstPosition", A.Point(first.Pos.X, first.Pos.Y, first.ZTop));
                A.Set(bolt, "SecondPosition", A.Point(first.Pos.X + dir.X, first.Pos.Y + dir.Y, first.ZTop));
                A.Set(bolt, "BoltSize", first.Dia);
                A.Set(bolt, "BoltStandard", Placement.BoltStandard);
                A.Set(bolt, "Tolerance", 0.0);
                A.Set(bolt, "BoltType", A.Enum("BoltGroup+BoltTypeEnum", "BOLT_TYPE_WORKSHOP"));
                A.Set(bolt, "Length", first.Length);
                for (int i = 1; i < pts.Count; i++) A.Call(bolt, "AddBoltDistX", step);
                A.Call(bolt, "AddBoltDistY", 0.0);
                object ok = A.Call(bolt, "Insert");
                if (ok is bool && !(bool)ok) Messages.Add("Không tạo được vít trên nẹp " + (si + 1) + " (kiểm tra bolt standard '" + Placement.BoltStandard + "').");
            }
        }

        private static string Num(double d) { return d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
    }
}
