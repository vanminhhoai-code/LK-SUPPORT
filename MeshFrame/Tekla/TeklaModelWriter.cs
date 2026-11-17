// Bộ ghi plan vào model Tekla bằng API gốc (Tekla.Structures.Model).
// Mã nguồn DUY NHẤT cho mọi phiên bản; biên dịch riêng cho từng bản Tekla (21.1 / 2020 / 2025) bằng build-writer.bat,
// vì DLL Tekla gắn với đúng phiên bản của nó. Cover Tool chọn DLL theo --tekla-version.
using System;
using System.Collections;
using System.Collections.Generic;
using MeshFrame.Core;
using TSG = Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;

namespace MeshFrame.Tekla
{
    /// <summary>Cách đặt thép góc/nẹp so với đường tham chiếu. Đây là phần cần chỉnh 1 lần khi chạy thử trong Tekla.</summary>
    public class TeklaPlacement
    {
        public TSM.Position.PlaneEnum VPlane = TSM.Position.PlaneEnum.LEFT;
        public TSM.Position.RotationEnum VRotation = TSM.Position.RotationEnum.FRONT;
        public TSM.Position.DepthEnum VDepth = TSM.Position.DepthEnum.FRONT;

        public TSM.Position.PlaneEnum SPlane = TSM.Position.PlaneEnum.MIDDLE;
        public TSM.Position.RotationEnum SRotation = TSM.Position.RotationEnum.FRONT;
        public TSM.Position.DepthEnum SDepth = TSM.Position.DepthEnum.FRONT;

        public TSM.Position.PlaneEnum StripPlane = TSM.Position.PlaneEnum.MIDDLE;
        public TSM.Position.RotationEnum StripRotation = TSM.Position.RotationEnum.FRONT;
        public TSM.Position.DepthEnum StripDepth = TSM.Position.DepthEnum.BEHIND;

        /// <summary>Đảo phía bị cắt của Fitting nếu mặt cắt vát ra ngược.</summary>
        public bool FlipMiterCut = false;
        public string BoltStandard = "8.8XOX";
    }

    public sealed class TeklaModelWriter : IModelWriter
    {
        private readonly TSM.Model _model = new TSM.Model();
        public readonly TeklaPlacement Placement = new TeklaPlacement();
        public readonly List<string> Messages = new List<string>();

        public void Write(MeshFramePlan plan, Frame f)
        {
            TSM.WorkPlaneHandler wph = _model.GetWorkPlaneHandler();
            TSM.TransformationPlane old = wph.GetCurrentTransformationPlane();
            try
            {
                // Làm việc trong hệ của khung: X = U, Y = V, Z = pháp tuyến N. Toạ độ 3 điểm của Frame là toạ độ GLOBAL.
                wph.SetCurrentTransformationPlane(new TSM.TransformationPlane(G(f.Origin), Vc(f.U), Vc(f.V)));

                var barParts = new List<TSM.Beam>();
                foreach (PlanBar b in plan.Bars) barParts.Add(InsertBar(b, plan));
                if (barParts.Count == 0) return;

                var stripParts = new List<TSM.Beam>();
                foreach (PlanStrip s in plan.Strips) stripParts.Add(InsertStrip(s));

                // Lưới: gắn vào cánh nằm của V khung đầu tiên
                if (plan.Mesh != null) InsertMesh(plan.Mesh, barParts[0]);

                if (plan.Fasteners.Count > 0) InsertFasteners(plan, stripParts, barParts[0]);

                // Gộp thành 1 assembly, main = V khung đầu tiên
                TSM.Assembly asm = barParts[0].GetAssembly();
                for (int i = 1; i < barParts.Count; i++) asm.Add(barParts[i]);
                foreach (TSM.Beam sp in stripParts) asm.Add(sp);
                asm.AssemblyNumber.Prefix = plan.AsmPrefix;
                asm.AssemblyNumber.StartNumber = plan.AsmStartNo;
                asm.Modify();
            }
            finally
            {
                wph.SetCurrentTransformationPlane(old);
                _model.CommitChanges();
            }
        }

        // ------------------------------------------------------------------ thanh V
        private TSM.Beam InsertBar(PlanBar bar, MeshFramePlan plan)
        {
            var p = Placement;
            bool stiff = bar.Role == BarRole.Stiffener;
            var beam = new TSM.Beam(Pt(bar.Start, bar.ZTop), Pt(bar.End, bar.ZTop));
            beam.Profile.ProfileString = bar.Profile;
            beam.Material.MaterialString = bar.Material;
            beam.Name = bar.Name;
            beam.Class = bar.Class;
            beam.PartNumber.Prefix = bar.Prefix;
            beam.PartNumber.StartNumber = bar.StartNo;
            beam.Position.Plane = stiff ? p.SPlane : p.VPlane;
            beam.Position.Rotation = stiff ? p.SRotation : p.VRotation;
            beam.Position.Depth = stiff ? p.SDepth : p.VDepth;
            if (!beam.Insert()) throw new InvalidOperationException("Không tạo được thanh " + bar.Profile + " (kiểm tra profile trong catalog).");
            AddMiter(beam, bar.Start, bar.CutStart, bar.ZTop);
            AddMiter(beam, bar.End, bar.CutEnd, bar.ZTop);
            return beam;
        }

        private TSM.Beam InsertStrip(PlanStrip st)
        {
            var p = Placement;
            double zc = st.ZBottom;
            var beam = new TSM.Beam(Pt(st.Start, zc), Pt(st.End, zc));
            beam.Profile.ProfileString = st.Profile;
            beam.Material.MaterialString = st.Material;
            beam.Name = st.Name;
            beam.Class = st.Class;
            beam.PartNumber.Prefix = st.Prefix;
            beam.PartNumber.StartNumber = st.StartNo;
            beam.Position.Plane = p.StripPlane;
            beam.Position.Rotation = p.StripRotation;
            beam.Position.Depth = p.StripDepth;
            if (!beam.Insert()) throw new InvalidOperationException("Không tạo được nẹp " + st.Profile + " (kiểm tra profile trong catalog).");
            AddMiter(beam, st.Start, st.CutStart, zc);
            AddMiter(beam, st.End, st.CutEnd, zc);
            return beam;
        }

        /// <summary>Cắt vát đầu thanh bằng Fitting: mặt phẳng chứa hướng phân giác và pháp tuyến Z.</summary>
        private void AddMiter(TSM.Beam beam, V2 end, V2? cutDir, double z)
        {
            if (!cutDir.HasValue) return;
            V2 d = cutDir.Value;
            if (Placement.FlipMiterCut) d = d * -1;
            var fit = new TSM.Fitting();
            fit.Father = beam;
            fit.Plane = new TSM.Plane();
            fit.Plane.Origin = Pt(end, z);
            fit.Plane.AxisX = new TSG.Vector(d.X, d.Y, 0);
            fit.Plane.AxisY = new TSG.Vector(0, 0, 1);
            fit.Insert();
        }

        // ------------------------------------------------------------------ lưới (Reinforcement Mesh)
        private void InsertMesh(PlanMesh m, TSM.Beam father)
        {
            var poly = new TSM.Polygon();
            foreach (V2 v in m.Polygon) poly.Points.Add(Pt(v, m.ZBottom + m.Thickness / 2));

            var mesh = new TSM.RebarMesh();
            mesh.Father = father;
            mesh.Polygon = poly;
            mesh.MeshType = TSM.RebarMesh.RebarMeshTypeEnum.POLYGON_MESH;
            mesh.Name = m.Name;
            mesh.Grade = m.Grade;
            mesh.Class = m.Class;
            mesh.NumberingSeries.Prefix = m.Prefix;
            mesh.NumberingSeries.StartNumber = m.StartNo;
            mesh.LongitudinalSize = Num(m.WireDia);
            mesh.CrossSize = Num(m.WireDia);
            mesh.LongitudinalDistances = new ArrayList { m.Pitch };
            mesh.CrossDistances = new ArrayList { m.Pitch };
            mesh.StartPoint = Pt(m.Polygon[0], m.ZBottom + m.Thickness / 2);
            mesh.EndPoint = Pt(m.Polygon[1], m.ZBottom + m.Thickness / 2);
            if (!mesh.Insert()) Messages.Add("Không tạo được lưới (RebarMesh): kiểm tra đường kính " + Num(m.WireDia) + " / mác " + m.Grade + " trong catalog rebar.");
        }

        // ------------------------------------------------------------------ vít / bu lông
        private void InsertFasteners(MeshFramePlan plan, List<TSM.Beam> strips, TSM.Beam bar0)
        {
            for (int si = 0; si < plan.Strips.Count; si++)
            {
                var pts = plan.Fasteners.FindAll(x => x.StripIndex == si);
                if (pts.Count == 0) continue;
                if (pts[0].Kind != FastenKind.Screw) { Messages.Add("Hàn điểm: chưa ghi vào model (chỉ vít/bu lông)."); return; }

                PlanFastener first = pts[0];
                double step = pts.Count > 1 ? (pts[1].Pos - pts[0].Pos).Length : 0;
                V2 dir = pts.Count > 1 ? (pts[1].Pos - pts[0].Pos).Unit() : new V2(1, 0);

                var bolt = new TSM.BoltArray();
                bolt.PartToBeBolted = strips[si];
                bolt.PartToBoltTo = bar0;
                bolt.FirstPosition = Pt(first.Pos, first.ZTop);
                bolt.SecondPosition = Pt(first.Pos + dir, first.ZTop);
                bolt.BoltSize = first.Dia;
                bolt.BoltStandard = Placement.BoltStandard;
                bolt.Tolerance = 0;
                bolt.BoltType = TSM.BoltGroup.BoltTypeEnum.BOLT_TYPE_WORKSHOP;
                bolt.Length = first.Length;
                bolt.AddBoltDistX(step);
                for (int i = 1; i < pts.Count; i++) bolt.AddBoltDistX(step);
                bolt.AddBoltDistY(0);
                if (!bolt.Insert()) Messages.Add("Không tạo được vít trên nẹp " + (si + 1) + " (kiểm tra bolt standard '" + Placement.BoltStandard + "').");
            }
        }

        // ------------------------------------------------------------------ tiện ích
        private static TSG.Point Pt(V2 v, double z) { return new TSG.Point(v.X, v.Y, z); }
        private static TSG.Point G(V3 v) { return new TSG.Point(v.X, v.Y, v.Z); }
        private static TSG.Vector Vc(V3 v) { return new TSG.Vector(v.X, v.Y, v.Z); }
        private static string Num(double d) { return d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
    }
}
