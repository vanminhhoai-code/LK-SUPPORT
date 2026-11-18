// Hộp thoại WinForms thuần (không dính Tekla) -> đưa vào Cover Tool như một Form bình thường.
// Luồng: pick đa giác trong Tekla -> HÌNH THẬT hiện ở đây để chỉnh (thông số, toạ độ điểm) -> nút "Tạo khung trong Tekla".
// Mọi ô nhập gắn với field của MeshFrameSettings bằng reflection: thêm field mới chỉ cần thêm 1 dòng Row(...).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using MeshFrame.Core;

namespace MeshFrame.UI
{
    public class CreateOutcome { public bool Success; public string Message; }
    public class LoadedFrame { public MeshFrameSettings Settings; public V3[] Points; public string Id; public string Error; }

    public class MeshFrameDialog : Form
    {
        private readonly MeshFrameSettings _s = new MeshFrameSettings();
        private readonly Dictionary<string, Control> _ctl = new Dictionary<string, Control>();
        private readonly Dictionary<string, FieldInfo> _fields = new Dictionary<string, FieldInfo>();
        private readonly Dictionary<string, Label> _labels = new Dictionary<string, Label>();

        private readonly PreviewControl _preview = new PreviewControl { Dock = DockStyle.Fill };
        private readonly Label _header = new Label { Dock = DockStyle.Top, Height = 28, Padding = new Padding(6, 6, 0, 0), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        private readonly RichTextBox _status = new RichTextBox { Dock = DockStyle.Bottom, Height = 150, ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
        private readonly DataGridView _grid = new DataGridView();
        private Button _btnCreate, _btnRepick, _btnEdit, _btnDeletePoint;
        private bool _loading;
        private V3[] _points = new V3[0];
        private string _editingId;
        private string[][] _rebarCatalog;

        // ---------------------------------------------------------------- API cho app chủ (Tekla / Cover Tool)
        /// <summary>Pick lại đa giác trong Tekla: trả về các điểm mới (GLOBAL) hoặc null nếu hủy.</summary>
        public Func<V3[]> PickHandler { get; set; }
        /// <summary>Tạo khung trong Tekla với thông số + điểm hiện tại (hoặc thay khung đang sửa).</summary>
        public Func<MeshFrameSettings, V3[], CreateOutcome> CreateHandler { get; set; }
        /// <summary>Đọc khung đang chọn trong Tekla để sửa.</summary>
        public Func<LoadedFrame> EditHandler { get; set; }
        /// <summary>Đọc đối tượng đang chọn trong Tekla (lưới mẫu): trả về thông báo hiển thị.</summary>
        public Func<string> SelectedObjectProbe { get; set; }
        /// <summary>Kiểm tra bản Tekla đang dùng có đủ các hàm API mà MeshFrame cần: trả về thông báo hiển thị.</summary>
        public Func<string> ApiCheckHandler { get; set; }

        /// <summary>Các điểm pick (toạ độ GLOBAL). Gán để hiện hình thật trong app.</summary>
        public V3[] Points
        {
            get { return _points; }
            set { _points = value ?? new V3[0]; RefreshGrid(); Changed(); }
        }

        /// <summary>Mã khung đang sửa (null = tạo khung mới).</summary>
        public string EditingId
        {
            get { return _editingId; }
            set { _editingId = value; UpdateButtons(); Changed(); }
        }

        /// <summary>Danh sách (mác, cỡ sợi) trong catalog rebar của Tekla -> ô chọn mác / cỡ sợi của lưới.</summary>
        public void SetRebarCatalog(IList<string[]> items)
        {
            _rebarCatalog = new string[items.Count][];
            for (int i = 0; i < items.Count; i++) _rebarCatalog[i] = items[i];
            FillRebarCombos();
        }

        /// <summary>Thông số hiện tại (đọc từ ô nhập / gán để nạp vào ô nhập).</summary>
        public MeshFrameSettings Settings
        {
            get { Read(); return _s; }
            set
            {
                foreach (FieldInfo f in typeof(MeshFrameSettings).GetFields(BindingFlags.Public | BindingFlags.Instance)) f.SetValue(_s, f.GetValue(value));
                PushToControls();
            }
        }

        public MeshFrameDialog()
        {
            Text = "MeshFrame - Khung lưới: V + lưới + nẹp + gia cường";
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(1220, 780);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            foreach (FieldInfo f in typeof(MeshFrameSettings).GetFields(BindingFlags.Public | BindingFlags.Instance)) _fields[f.Name] = f;

            var tabs = new TabControl { Dock = DockStyle.Left, Width = 470 };
            tabs.TabPages.Add(TabFrame());
            tabs.TabPages.Add(TabStiff());
            tabs.TabPages.Add(TabMesh());
            tabs.TabPages.Add(TabStrip());
            tabs.TabPages.Add(TabTekla());

            // ---- bên phải: tiêu đề, hình, bảng điểm, trạng thái
            var right = new Panel { Dock = DockStyle.Fill };
            var gridPanel = new Panel { Dock = DockStyle.Bottom, Height = 150 };
            BuildGrid(gridPanel);
            right.Controls.Add(_preview);
            right.Controls.Add(_header);
            right.Controls.Add(gridPanel);
            right.Controls.Add(_status);

            // ---- nút
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var close = new Button { Text = "Đóng", Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
            _btnCreate = new Button { Text = "Tạo khung trong Tekla", Width = 190, Height = 30, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _btnEdit = new Button { Text = "Sửa khung đã chọn", Width = 150, Height = 30 };
            _btnRepick = new Button { Text = "Pick lại điểm", Width = 120, Height = 30 };
            _btnCreate.Click += delegate { DoCreate(); };
            _btnEdit.Click += delegate { DoEdit(); };
            _btnRepick.Click += delegate { DoRepick(); };
            buttons.Controls.AddRange(new Control[] { close, _btnCreate, _btnEdit, _btnRepick });
            CancelButton = close;

            Controls.Add(right);
            Controls.Add(tabs);
            Controls.Add(buttons);

            PushToControls();
            RefreshGrid();
            UpdateButtons();
        }

        // ------------------------------------------------------------ các tab
        private TabPage TabFrame()
        {
            var t = NewPage("Khung V");
            Note(t, "Mỗi cạnh của đa giác pick là một thanh thép góc; mép ngoài khung đi qua các điểm pick.");
            Row(t, "VLegA", "Cạnh V - a (mm)");
            Row(t, "VLegB", "Cạnh V - b (mm)");
            Row(t, "VThk", "Độ dày V (mm)");
            Row(t, "VMaterial", "Vật liệu");
            Row(t, "VName", "Tên");
            Row(t, "VClass", "Class");
            Row(t, "VPrefix", "Prefix part");
            Row(t, "VStartNo", "Số bắt đầu part");
            Row(t, "AsmPrefix", "Prefix assembly");
            Row(t, "AsmStartNo", "Số bắt đầu assembly");
            Row(t, "Corner", "Ghép góc", "Cắt vát theo phân giác góc", "Một cạnh chạy suốt (chỉ khi 4 điểm)");
            return (TabPage)t.Parent;
        }

        private TabPage TabStiff()
        {
            var t = NewPage("Gia cường");
            Note(t, "V gia cường CÙNG TIẾT DIỆN V khung, vuông góc với CẠNH GỐC, hai đầu cắt khuyết cánh nằm cho khớp V khung. Các ô đo dọc cạnh gốc. Lưới kéo căng liên tục; nẹp PL đi theo từng thanh.");
            Row(t, "StiffMode", "Chia ô", "Không gia cường", "Chia đều (mỗi ô tối đa)", "Nhập từng ô");
            Row(t, "StiffBaseEdge", "Cạnh gốc (0 = tự chọn cạnh dài nhất)");
            Row(t, "StiffMax", "Mỗi ô tối đa (mm)");
            Row(t, "StiffCells", "Bề rộng từng ô (mm)");
            Note(t, "Nhập từng ô: cách nhau bằng dấu cách, vd  1000 2*1500 800  (n*x = lặp n lần). Tổng nhỏ hơn khung thì tự thêm ô cuối. Số cạnh xem ở hình bên phải (\"1: 3000\" = cạnh 1 dài 3000).");
            Row(t, "SMaterial", "Vật liệu");
            Row(t, "SName", "Tên");
            Row(t, "SClass", "Class");
            Row(t, "SPrefix", "Prefix part");
            Row(t, "SStartNo", "Số bắt đầu part");
            return (TabPage)t.Parent;
        }

        private TabPage TabMesh()
        {
            var t = NewPage("Lưới");
            Note(t, "Lưới nằm phía ngoài cánh nằm V, nẹp ép ngoài lưới. RebarMesh cần mác và cỡ sợi CÓ trong catalog rebar của Tekla; Tekla từ chối thì tự thay bằng tấm mỏng.");
            Row(t, "MeshKind", "Kiểu lưới", "RebarMesh (tự chuyển sang tấm nếu lỗi)", "Tấm mỏng (plate)", "Không tạo lưới");   // đúng thứ tự enum MeshKind
            Row(t, "MeshGap", "Lưới lùi cách mép ngoài khung (mm)");
            Row(t, "MeshWireDia", "Đường kính sợi (mm)");
            Row(t, "MeshPitch", "Bước sợi = ô + sợi (mm)");
            Row(t, "RebarGrade", "Mác rebar (trống = mác bên dưới)", true);
            Row(t, "RebarSize", "Cỡ sợi rebar (trống = đường kính sợi)", true);
            Row(t, "MeshName", "Tên");
            Row(t, "MeshGrade", "Mác thép / vật liệu tấm");
            Row(t, "MeshPrefix", "Prefix (numbering series)");
            Row(t, "MeshStartNo", "Số bắt đầu");
            Row(t, "MeshClass", "Class");
            Row(t, "MeshWeightKgPerM2", "Khối lượng nhập tay (kg/m²)");
            Note(t, "Khối lượng nhập tay: 0 = không dùng. Dùng khi Tekla tính khối lượng lưới không đúng.");
            return (TabPage)t.Parent;
        }

        private TabPage TabStrip()
        {
            var t = NewPage("Nẹp");
            Row(t, "UseStrip", "Có nẹp");
            Note(t, "Nẹp PL nằm phía ngoài, ép lưới vào cánh nằm V (chuẩn PL-25x1.2, rộng bằng cánh V); chạy quanh khung và theo các thanh gia cường.");
            Row(t, "StripWidth", "Bề rộng nẹp (mm)");
            Row(t, "StripThk", "Độ dày nẹp (mm)");
            Row(t, "StripMaterial", "Vật liệu");
            Row(t, "StripName", "Tên");
            Row(t, "StripClass", "Class");
            Row(t, "StripPrefix", "Prefix part");
            Row(t, "StripStartNo", "Số bắt đầu part");
            return (TabPage)t.Parent;
        }

        private TabPage TabTekla()
        {
            var t = NewPage("Tekla");
            Note(t, "Mặc định app TỰ ĐO hình khối thật mà Tekla tạo ra (thép góc xoay đúng chiều, nẹp nằm giữa cánh V, dưới lưới) rồi canh lại. Chỉ tắt khi muốn tự đặt các ô On plane / Rotation / At depth bên dưới. Dùng chữ KHÔNG DẤU cho các ô gửi sang Tekla.");
            Row(t, "AutoPlacement", "Tự canh V và nẹp theo hình khối thật (khuyến nghị)");
            Row(t, "VProfile", "Profile V khung + gia cường (trống = tự tạo)");
            Row(t, "StripProfile", "Profile nẹp (trống = tự tạo FL w*t)");
            Note(t, "Hướng đặt thép góc V khung (V gia cường dùng chung):");
            Row(t, "VPlane", "  On plane", "Left", "Middle", "Right");
            Row(t, "VRotation", "  Rotation", "Front", "Top", "Back", "Below");
            Row(t, "VDepth", "  At depth", "Front", "Middle", "Behind");
            Note(t, "Hướng đặt nẹp:");
            Row(t, "StripPlane", "  On plane", "Left", "Middle", "Right");
            Row(t, "StripRotation", "  Rotation", "Front", "Top", "Back", "Below");
            Row(t, "StripDepth", "  At depth", "Front", "Middle", "Behind");
            Row(t, "VLateral", "Lệch ngang V thêm (mm, + vào trong khung)");
            Row(t, "StripLateral", "Lệch ngang nẹp thêm (mm, + vào trong khung)");
            Row(t, "FlipMiterCut", "Đảo phía cắt vát (nếu mặt cắt vát ra ngược)");
            var probe = new Button { Text = "Đọc lưới mẫu đang chọn trong Tekla", AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
            probe.Click += delegate
            {
                if (SelectedObjectProbe == null) { MessageBox.Show("Chỉ dùng khi chạy cùng Tekla.", "MeshFrame"); return; }
                string msg;
                try { msg = SelectedObjectProbe(); } catch (Exception ex) { msg = "Lỗi: " + ex.Message; }
                MessageBox.Show(msg, "MeshFrame");
            };
            int pr = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(probe, 0, pr);
            t.SetColumnSpan(probe, 2);
            var apiCheck = new Button { Text = "Kiểm tra API của bản Tekla này", AutoSize = true, Margin = new Padding(3, 6, 3, 3) };
            apiCheck.Click += delegate
            {
                if (ApiCheckHandler == null) { MessageBox.Show("Chỉ dùng khi chạy cùng Tekla.", "MeshFrame"); return; }
                string msg;
                try { msg = ApiCheckHandler(); } catch (Exception ex) { msg = "Lỗi: " + ex.Message; }
                MessageBox.Show(msg, "MeshFrame");
            };
            int pr2 = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(apiCheck, 0, pr2);
            t.SetColumnSpan(apiCheck, 2);
            return (TabPage)t.Parent;
        }

        // ------------------------------------------------------------ bảng điểm
        private void BuildGrid(Panel host)
        {
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.BackgroundColor = Color.White;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            _grid.Columns.Add("pt", "Điểm"); _grid.Columns[0].ReadOnly = true; _grid.Columns[0].Width = 60;
            _grid.Columns.Add("x", "X (mm)"); _grid.Columns.Add("y", "Y (mm)"); _grid.Columns.Add("z", "Z (mm)");
            foreach (int i in new[] { 1, 2, 3 }) _grid.Columns[i].Width = 120;
            _grid.Columns.Add("len", "Dài tới điểm kế (mm)"); _grid.Columns[4].ReadOnly = true; _grid.Columns[4].Width = 150;
            _grid.CellEndEdit += delegate (object s, DataGridViewCellEventArgs e) { GridEdited(e.RowIndex, e.ColumnIndex); };

            _btnDeletePoint = new Button { Text = "Xóa điểm đã chọn", Dock = DockStyle.Right, Width = 130 };
            _btnDeletePoint.Click += delegate
            {
                if (_grid.CurrentRow == null || _points.Length <= 3) return;
                int idx = _grid.CurrentRow.Index;
                var list = new List<V3>(_points); list.RemoveAt(idx);
                _points = list.ToArray(); RefreshGrid(); Changed();
            };
            var title = new Label { Text = "Các điểm pick (sửa được tọa độ; tự nối điểm cuối về điểm đầu)", Dock = DockStyle.Top, Height = 20, ForeColor = Color.DimGray, Padding = new Padding(4, 3, 0, 0) };
            host.Controls.Add(_grid);
            host.Controls.Add(_btnDeletePoint);
            host.Controls.Add(title);
        }

        private void RefreshGrid()
        {
            _loading = true;
            _grid.Rows.Clear();
            for (int i = 0; i < _points.Length; i++)
            {
                V3 p = _points[i], q = _points[(i + 1) % _points.Length];
                _grid.Rows.Add("P" + (i + 1), F(p.X), F(p.Y), F(p.Z), F((q - p).Length));
            }
            _loading = false;
        }

        private void GridEdited(int row, int col)
        {
            if (_loading || row < 0 || col < 1 || col > 3) return;
            double v;
            if (!double.TryParse(Convert.ToString(_grid.Rows[row].Cells[col].Value).Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            { RefreshGrid(); return; }                                              // nhập sai: trả lại giá trị cũ
            V3 p = _points[row];
            _points[row] = new V3(col == 1 ? v : p.X, col == 2 ? v : p.Y, col == 3 ? v : p.Z);
            RefreshGrid();
            Changed();
        }

        // ------------------------------------------------------------ hành động
        private void DoRepick()
        {
            if (PickHandler == null) { MessageBox.Show("Chỉ dùng khi chạy cùng Tekla.", "MeshFrame"); return; }
            V3[] p = null;
            Visible = false;                                                       // ẩn hộp thoại để pick trong Tekla
            try { p = PickHandler(); }
            catch (Exception ex) { Visible = true; MessageBox.Show("Lỗi khi pick: " + ex.Message, "MeshFrame", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            Visible = true; Activate();
            if (p != null) { EditingId = null; Points = p; }
        }

        private void DoEdit()
        {
            if (EditHandler == null) { MessageBox.Show("Chỉ dùng khi chạy cùng Tekla.", "MeshFrame"); return; }
            LoadedFrame lf;
            try { lf = EditHandler(); }
            catch (Exception ex) { MessageBox.Show("Lỗi khi đọc khung: " + ex.Message, "MeshFrame", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (lf == null || lf.Error != null) { MessageBox.Show(lf == null ? "Không đọc được khung." : lf.Error, "MeshFrame"); return; }
            Settings = lf.Settings;
            _points = lf.Points; RefreshGrid();
            EditingId = lf.Id;
        }

        private void DoCreate()
        {
            if (CreateHandler == null) { MessageBox.Show("Chỉ dùng khi chạy cùng Tekla.", "MeshFrame"); return; }
            Read();
            MeshFramePlan plan = CurrentPlan();
            if (plan == null || plan.Errors.Count > 0) { MessageBox.Show("Chưa tạo được: " + (plan == null ? "chưa có đủ điểm." : string.Join("\n", plan.Errors.ToArray())), "MeshFrame"); return; }
            _btnCreate.Enabled = false; Cursor = Cursors.WaitCursor;
            CreateOutcome o;
            try { o = CreateHandler(_s, (V3[])_points.Clone()); }
            catch (Exception ex) { o = new CreateOutcome { Success = false, Message = "Lỗi: " + ex.Message }; }
            finally { Cursor = Cursors.Default; _btnCreate.Enabled = true; }
            AppendResult(o);
            if (o.Success) EditingId = null;                                       // xong: trở về chế độ tạo mới
        }

        private void AppendResult(CreateOutcome o)
        {
            _status.SelectionStart = _status.TextLength;
            _status.SelectionColor = o.Success ? Color.DarkGreen : Color.Firebrick;
            _status.AppendText((o.Success ? "✔ " : "✖ ") + o.Message + Environment.NewLine);
            _status.SelectionColor = _status.ForeColor;
            Changed(false);
            if (!o.Success) MessageBox.Show(o.Message, "MeshFrame", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ------------------------------------------------------------ đọc / ghi / bật tắt / xem trước
        private void PushToControls()
        {
            _loading = true;
            foreach (var kv in _ctl)
            {
                object v = _fields[kv.Key].GetValue(_s);
                var cb = kv.Value as CheckBox; var combo = kv.Value as ComboBox;
                if (cb != null) cb.Checked = (bool)v;
                else if (combo != null && _fields[kv.Key].FieldType.IsEnum) combo.SelectedIndex = Convert.ToInt32(v);
                else kv.Value.Text = Convert.ToString(v, CultureInfo.InvariantCulture);
            }
            _loading = false;
            Changed();
        }

        private void Read()
        {
            foreach (var kv in _ctl)
            {
                FieldInfo f = _fields[kv.Key];
                var cb = kv.Value as CheckBox; var combo = kv.Value as ComboBox;
                object val = null; bool ok = true;
                if (cb != null) val = cb.Checked;
                else if (combo != null && f.FieldType.IsEnum) val = Enum.ToObject(f.FieldType, combo.SelectedIndex);
                else if (f.FieldType == typeof(string)) val = kv.Value.Text;
                else if (f.FieldType == typeof(double))
                {
                    double d; ok = double.TryParse(kv.Value.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d); val = d;
                }
                else if (f.FieldType == typeof(int))
                {
                    int d; ok = int.TryParse(kv.Value.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out d); val = d;
                }
                if (kv.Value is TextBox) kv.Value.BackColor = ok ? SystemColors.Window : Color.MistyRose;
                if (ok && val != null) f.SetValue(_s, val);
            }
        }

        private void Changed() { Changed(true); }

        private void Changed(bool resetStatus)
        {
            if (_loading) return;
            Read();
            UpdateEnabled();
            UpdatePreview();
            UpdateButtons();
        }

        private void UpdateEnabled()
        {
            bool strip = _s.UseStrip;
            foreach (string n in new[] { "StripWidth", "StripThk", "StripMaterial", "StripName", "StripClass", "StripPrefix", "StripStartNo" }) Enable(n, strip);
            foreach (string n in new[] { "VPlane", "VDepth", "StripPlane", "StripDepth" }) Enable(n, !_s.AutoPlacement);       // tự canh: Plane/Depth do app quyết định (Middle)
            bool stiff = _s.StiffMode != StiffMode.None;
            foreach (string n in new[] { "StiffBaseEdge", "SMaterial", "SName", "SClass", "SPrefix", "SStartNo" }) Enable(n, stiff);
            Enable("StiffMax", _s.StiffMode == StiffMode.Equal);
            Enable("StiffCells", _s.StiffMode == StiffMode.Custom);
            bool mesh = _s.MeshKind != MeshKind.None;
            foreach (string n in new[] { "MeshGap", "MeshWireDia", "MeshPitch", "MeshName", "MeshGrade", "MeshPrefix", "MeshStartNo", "MeshClass", "MeshWeightKgPerM2" }) Enable(n, mesh);
            Enable("RebarGrade", _s.MeshKind == MeshKind.RebarMesh); Enable("RebarSize", _s.MeshKind == MeshKind.RebarMesh);
        }

        private void Enable(string name, bool on)
        {
            _ctl[name].Enabled = on;
            if (_labels.ContainsKey(name)) _labels[name].Enabled = on;
        }

        private void UpdateButtons()
        {
            if (_btnCreate == null) return;
            bool editing = _editingId != null;
            _btnCreate.Text = editing ? "Cập nhật khung trong Tekla (thay khung cũ)" : "Tạo khung trong Tekla";
            _btnCreate.Width = editing ? 300 : 190;
            MeshFramePlan plan = CurrentPlan();
            _btnCreate.Enabled = plan != null && plan.Errors.Count == 0 && CreateHandler != null;
            _btnDeletePoint.Enabled = _points.Length > 3;
        }

        /// <summary>Kế hoạch từ các điểm + thông số hiện tại (null nếu chưa đủ 3 điểm hoặc điểm không cùng mặt phẳng).</summary>
        private MeshFramePlan CurrentPlan()
        {
            Frame f; V2[] poly; string err;
            if (_points.Length < 3 || !MeshFrameBuilder.TryFrame(_points, out f, out poly, out err)) return null;
            return MeshFramePlanner.Plan(_s, poly);
        }

        private void UpdatePreview()
        {
            Frame f; V2[] poly; string err = null;
            bool framed = _points.Length >= 3 && MeshFrameBuilder.TryFrame(_points, out f, out poly, out err);
            if (_points.Length < 3)
            {
                _preview.ShowEmpty(null);
                _header.Text = _points.Length == 0 ? "Chưa có điểm" : "Mới có " + _points.Length + " điểm, cần ít nhất 3";
                SetStatus(new List<string>(), new List<string> { "Chưa đủ điểm: bấm \"Pick lại điểm\" để pick đa giác trong Tekla." }, new List<string>(), null);
                return;
            }
            if (!framed)
            {
                _preview.ShowEmpty("Không dựng được khung: " + err);
                _header.Text = _points.Length + " điểm";
                SetStatus(new List<string> { err }, new List<string>(), new List<string>(), null);
                return;
            }
            MeshFrameBuilder.TryFrame(_points, out f, out poly, out err);
            MeshFramePlan plan = MeshFramePlanner.Plan(_s, poly);
            _preview.Show(plan.Errors.Count == 0 ? plan : null, poly, _s.VLegA);

            double per = 0; for (int i = 0; i < _points.Length; i++) per += (_points[(i + 1) % _points.Length] - _points[i]).Length;
            double area = plan.Polygon != null ? Math.Abs(Geo.Area(plan.Polygon)) / 1e6 : 0;
            _header.Text = _points.Length + " điểm · chu vi " + F(per) + " mm · diện tích " + area.ToString("0.###", CultureInfo.InvariantCulture) + " m²"
                           + (_editingId != null ? "   —   ĐANG SỬA KHUNG " + _editingId.Substring(0, Math.Min(8, _editingId.Length)) : "");

            int stiff = 0; foreach (PlanBar b in plan.Bars) if (b.Role == BarRole.Stiffener) stiff++;
            string summary = plan.Errors.Count > 0 ? null :
                string.Format("Sẽ tạo: {0} V khung · {1} gia cường · {2} nẹp · lưới {3} · {4} ô", plan.Bars.Count - stiff, stiff, plan.Strips.Count,
                              plan.Mesh == null ? "không" : plan.Mesh.Kind == MeshKind.RebarMesh ? "RebarMesh" : "tấm mỏng", Math.Max(1, plan.Cells.Count));
            SetStatus(plan.Errors, plan.Warnings, plan.Notes, summary);
        }

        private void SetStatus(List<string> errors, List<string> warnings, List<string> notes, string summary)
        {
            // Giữ lại các dòng KẾT QUẢ tạo khung đã ghi; chỉ thay phần "xem trước" ở đầu.
            _status.Clear();
            if (summary != null) Line(summary, Color.Black, true);
            foreach (string e in errors) Line("✖ " + e, Color.Firebrick, true);
            foreach (string n in notes) Line("ℹ " + n, Color.DimGray, false);
            foreach (string w in warnings) Line("⚠ " + w, Color.DarkOrange, false);
            if (errors.Count > 0) Line("→ Chưa thể tạo khung: sửa các lỗi trên (nút Tạo khung đang khóa).", Color.Firebrick, true);
        }

        private void Line(string text, Color c, bool bold)
        {
            _status.SelectionStart = _status.TextLength;
            _status.SelectionColor = c;
            _status.SelectionFont = new Font(_status.Font, bold ? FontStyle.Bold : FontStyle.Regular);
            _status.AppendText(text + Environment.NewLine);
        }

        private void FillRebarCombos()
        {
            if (_rebarCatalog == null) return;
            var grades = new List<string>(); var sizes = new List<string>();
            foreach (string[] it in _rebarCatalog) { if (!grades.Contains(it[0])) grades.Add(it[0]); if (!sizes.Contains(it[1])) sizes.Add(it[1]); }
            var cg = _ctl["RebarGrade"] as ComboBox; var cs = _ctl["RebarSize"] as ComboBox;
            string g0 = cg.Text, s0 = cs.Text;
            cg.Items.Clear(); cg.Items.AddRange(grades.ToArray());
            cs.Items.Clear(); cs.Items.AddRange(sizes.ToArray());
            cg.Text = g0; cs.Text = s0;
        }

        private static string F(double d) { return d.ToString("0.##", CultureInfo.InvariantCulture); }

        // ------------------------------------------------------------ dựng ô nhập
        private static TableLayoutPanel NewPage(string title)
        {
            var page = new TabPage(title) { Padding = new Padding(8), AutoScroll = true };
            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215));
            page.Controls.Add(t);
            return t;
        }

        private static void Note(TableLayoutPanel t, string text)
        {
            var l = new Label { Text = text, ForeColor = Color.DimGray, MaximumSize = new Size(395, 0), AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
            int row = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(l, 0, row);
            t.SetColumnSpan(l, 2);
        }

        /// <summary>Thêm 1 ô nhập gắn với field cùng tên của MeshFrameSettings. captions: tên các lựa chọn nếu field là enum.</summary>
        private void Row(TableLayoutPanel t, string field, string caption, params string[] captions) { RowCore(t, field, caption, false, captions); }
        private void Row(TableLayoutPanel t, string field, string caption, bool editableCombo) { RowCore(t, field, caption, editableCombo); }

        private void RowCore(TableLayoutPanel t, string field, string caption, bool editableCombo, params string[] captions)
        {
            FieldInfo f = _fields[field];
            Control c;
            int row = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            if (f.FieldType == typeof(bool))
            {
                var cb = new CheckBox { Text = caption, AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
                cb.CheckedChanged += delegate { Changed(); };
                t.Controls.Add(cb, 0, row);
                t.SetColumnSpan(cb, 2);
                c = cb;
            }
            else
            {
                if (f.FieldType.IsEnum)
                {
                    var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 205 };
                    combo.Items.AddRange(captions);
                    combo.SelectedIndexChanged += delegate { Changed(); };
                    c = combo;
                }
                else if (editableCombo)
                {
                    var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 205 };
                    combo.TextChanged += delegate { Changed(); };
                    c = combo;
                }
                else
                {
                    var tb = new TextBox { Width = 205 };
                    tb.TextChanged += delegate { Changed(); };
                    c = tb;
                }
                var lbl = new Label { Text = caption, AutoSize = true, MaximumSize = new Size(205, 0), Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) };
                _labels[field] = lbl;
                t.Controls.Add(lbl, 0, row);
                t.Controls.Add(c, 1, row);
            }
            _ctl[field] = c;
        }
    }
}
