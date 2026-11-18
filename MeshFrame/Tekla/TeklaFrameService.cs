// Dịch vụ tạo / thay / đọc lại khung trong Tekla: dùng chung cho nút "Tạo khung trong Tekla" và "Sửa khung đã chọn".
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MeshFrame.Core;

namespace MeshFrame.Tekla
{
    public sealed class ServiceResult
    {
        public bool Success;
        public string FrameId;
        public string Message;                       // nhiều dòng: kết quả + cảnh báo
    }

    public sealed class TeklaFrameService
    {
        private readonly TeklaApi A;
        private readonly object _model;
        private object _editMain;                    // part chính của khung đang sửa (sau LoadSelected)
        private string _editId;

        public TeklaFrameService(TeklaApi api, object model) { A = api; _model = model; }

        /// <summary>Đang ở chế độ sửa khung có sẵn (mã khung).</summary>
        public string EditingId { get { return _editMain != null ? _editId : null; } }

        public void StopEditing() { _editMain = null; _editId = null; }

        /// <summary>Tạo khung mới; nếu đang sửa thì tạo khung mới rồi xóa khung cũ (lỗi thì giữ nguyên khung cũ).</summary>
        public ServiceResult Create(MeshFrameSettings settings, V3[] corners)
        {
            bool replacing = _editMain != null;
            string id = replacing ? _editId : FrameRecord.NewId();
            var writer = new TeklaModelWriter(A, _model) { FrameId = id };
            var sb = new StringBuilder();
            BuildResult r;
            try { r = MeshFrameBuilder.Build(settings, corners, writer); }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                return new ServiceResult { Success = false, Message = "Lỗi khi tạo trong Tekla: " + ex.Message + (replacing ? "\nKhung cũ được giữ nguyên." : "") };
            }
            if (!r.Success) return new ServiceResult { Success = false, Message = string.Join("\n", r.Errors.ToArray()) };

            MeshFramePlan p = r.Plan;
            int stiff = 0; foreach (PlanBar b in p.Bars) if (b.Role == BarRole.Stiffener) stiff++;
            sb.AppendLine(string.Format("Đã tạo: {0} V khung, {1} gia cường, {2} nẹp.", p.Bars.Count - stiff, stiff, p.Strips.Count));

            if (replacing)
            {
                try { int n = TeklaFrameStore.DeleteFrame(A, _editMain); sb.AppendLine("Đã thay khung cũ (xóa " + n + " part)."); }
                catch (Exception ex) { sb.AppendLine("⚠ Không xóa được khung cũ: " + Root(ex).Message + " (xóa tay khung cũ)."); }
                _editMain = null; _editId = null;
            }
            // bản ghi để "Sửa khung đã chọn" sau này
            try
            {
                string folder = TeklaFrameStore.ModelFolder(A, _model);
                if (string.IsNullOrEmpty(folder)) sb.AppendLine("⚠ Model chưa lưu: không lưu được bản ghi để sửa khung sau này.");
                else new FrameRecord { Id = id, Settings = settings, Corners = corners }.Save(TeklaFrameStore.RecordPath(folder, id));
            }
            catch (Exception ex) { sb.AppendLine("⚠ Không lưu được bản ghi khung: " + Root(ex).Message); }

            foreach (string w in writer.Notes) sb.AppendLine("ℹ " + w);
            foreach (string w in p.Warnings) sb.AppendLine("⚠ " + w);
            foreach (string w in writer.Messages) sb.AppendLine("⚠ " + w);
            return new ServiceResult { Success = true, FrameId = id, Message = sb.ToString().TrimEnd() };
        }

        /// <summary>Đọc khung đang chọn trong Tekla (đối tượng hoặc assembly của nó có mã khung) -> bản ghi để sửa. null + lý do nếu không được.</summary>
        public FrameRecord LoadSelected(out string error)
        {
            error = null;
            object main;
            string id = TeklaFrameStore.FindSelected(A, out main);
            if (id.Length == 0) { error = "Chưa chọn khung do MeshFrame tạo. Chọn một part của khung trong Tekla rồi bấm lại."; return null; }
            string folder = TeklaFrameStore.ModelFolder(A, _model);
            FrameRecord rec = string.IsNullOrEmpty(folder) ? null : FrameRecord.Load(TeklaFrameStore.RecordPath(folder, id));
            if (rec == null) { error = "Tìm thấy khung (mã " + id + ") nhưng không có bản ghi thông số trong thư mục model\\MeshFrame (đã bị xóa hoặc model khác)."; return null; }
            _editMain = main; _editId = id;
            return rec;
        }

        private static Exception Root(Exception e) { while (e.InnerException != null) e = e.InnerException; return e; }
    }
}
