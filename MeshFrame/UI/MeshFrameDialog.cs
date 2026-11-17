// Hộp thoại WinForms thuần (không dính Tekla.Structures.Dialog) -> đưa vào Cover Tool như một Form bình thường.
// Mọi ô nhập gắn với field của MeshFrameSettings bằng reflection: thêm field mới chỉ cần thêm 1 dòng Row(...).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using MeshFrame.Core;

namespace MeshFrame.UI
{
    public class MeshFrameDialog : Form
    {
        private readonly MeshFrameSettings _s = new MeshFrameSettings();
        private readonly Dictionary<string, Control> _ctl = new Dictionary<string, Control>();
        private readonly Dictionary<string, FieldInfo> _fields = new Dictionary<string, FieldInfo>();
        private readonly Dictionary<string, Label> _labels = new Dictionary<string, Label>();

        private readonly PreviewControl _preview = new PreviewControl { Dock = DockStyle.Fill };
        private readonly TextBox _pvL = new TextBox { Text = "4000", Width = 70 };
        private readonly TextBox _pvH = new TextBox { Text = "1200", Width = 70 };
        private readonly TextBox _pvSkew = new TextBox { Text = "0", Width = 70 };
        private readonly Label _info = new Label { Dock = DockStyle.Bottom, Height = 96, Padding = new Padding(6) };
        private bool _loading;

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
            ClientSize = new Size(1100, 700);
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

            var right = new Panel { Dock = DockStyle.Fill };
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(6, 8, 0, 0) };
            top.Controls.AddRange(new Control[]
            {
                new Label { Text = "Xem trước:  Dài L", AutoSize = true, Margin = new Padding(3, 5, 3, 0) }, _pvL,
                new Label { Text = "Cao H", AutoSize = true, Margin = new Padding(12, 5, 3, 0) }, _pvH,
                new Label { Text = "Lệch cạnh trên", AutoSize = true, Margin = new Padding(12, 5, 3, 0) }, _pvSkew
            });
            right.Controls.Add(_preview);
            right.Controls.Add(top);
            right.Controls.Add(_info);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var cancel = new Button { Text = "Hủy", Width = 90, Height = 28, DialogResult = DialogResult.Cancel };
            var ok = new Button { Text = "OK", Width = 90, Height = 28 };
            ok.Click += delegate
            {
                Read();
                var errs = _s.Validate();
                if (errs.Count > 0) { MessageBox.Show(string.Join("\n", errs.ToArray()), "Thông số chưa hợp lệ"); return; }
                DialogResult = DialogResult.OK;
            };
            buttons.Controls.AddRange(new Control[] { cancel, ok });
            AcceptButton = ok; CancelButton = cancel;

            Controls.Add(right);
            Controls.Add(tabs);
            Controls.Add(buttons);

            foreach (TextBox t in new[] { _pvL, _pvH, _pvSkew }) t.TextChanged += delegate { Changed(); };
            PushToControls();
        }

        // ------------------------------------------------------------ các tab
        private TabPage TabFrame()
        {
            var t = NewPage("Khung V");
            Note(t, "Khung = 4 thanh thép góc quanh 4 điểm pick; mép ngoài khung đi qua các điểm pick.");
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
            Row(t, "Corner", "Ghép góc", "Cắt vát theo phân giác góc", "Một cạnh chạy suốt, cạnh kia cắt vuông");
            return (TabPage)t.Parent;
        }

        private TabPage TabStiff()
        {
            var t = NewPage("Gia cường");
            Note(t, "V gia cường CÙNG TIẾT DIỆN V khung, luôn vuông góc với đố trên/dưới (cạnh gốc), hai đầu cắt khuyết cánh nằm cho khớp V khung. Các ô đo dọc cạnh gốc. Lưới kéo căng liên tục; nẹp PL đi theo từng thanh.");
            Row(t, "StiffMode", "Chia ô", "Không gia cường", "Chia đều (mỗi ô tối đa)", "Nhập từng ô");
            Row(t, "StiffDir", "Chia theo", "Dọc cạnh P1-P2 (thanh đứng)", "Dọc cạnh P1-P4 (thanh ngang)");
            Row(t, "StiffMax", "Mỗi ô tối đa (mm)");
            Row(t, "StiffCells", "Bề rộng từng ô (mm)");
            Note(t, "Nhập từng ô: cách nhau bằng dấu cách, vd  1000 2*1500 800  (n*x = lặp n lần). Tổng nhỏ hơn khung thì tự thêm ô cuối.");
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
            Note(t, "Lưới là Reinforcement Mesh theo đa giác 4 điểm. Không bắt buộc đúng 100% lưới SUS304, có thể đặt tên gợi nhớ.");
            Row(t, "MeshGap", "Lưới lùi cách mép ngoài khung (mm)");
            Row(t, "MeshWireDia", "Đường kính sợi (mm)");
            Row(t, "MeshPitch", "Bước sợi = ô + sợi (mm)");
            Row(t, "MeshName", "Tên");
            Row(t, "MeshGrade", "Mác thép (Grade)");
            Row(t, "MeshPrefix", "Prefix (numbering series)");
            Row(t, "MeshStartNo", "Số bắt đầu");
            Row(t, "MeshClass", "Class");
            Row(t, "MeshWeightKgPerM2", "Khối lượng nhập tay (kg/m²)");
            Note(t, "Khối lượng nhập tay: 0 = không dùng. Dùng khi Tekla tính khối lượng lưới không đúng.");
            return (TabPage)t.Parent;
        }

        private TabPage TabStrip()
        {
            var t = NewPage("Nẹp && liên kết");
            Row(t, "UseStrip", "Có nẹp");
            Note(t, "Nẹp PL nằm phía ngoài, ép lưới vào cánh nằm V (chuẩn PL-25x1.2, rộng bằng cánh V); chạy quanh khung và theo các thanh gia cường. Bỏ tick: khóa nẹp và liên kết.");
            Row(t, "StripWidth", "Bề rộng nẹp (mm)");
            Row(t, "StripThk", "Độ dày nẹp (mm)");
            Row(t, "StripMaterial", "Vật liệu");
            Row(t, "StripName", "Tên");
            Row(t, "StripClass", "Class");
            Row(t, "StripPrefix", "Prefix part");
            Row(t, "StripStartNo", "Số bắt đầu part");
            Row(t, "Fasten", "Liên kết", "Không", "Vít / bu lông", "Hàn điểm");
            Row(t, "FastenDia", "Đường kính vít (mm)");
            Row(t, "FastenPitch", "Bước @ tối đa (mm)");
            Row(t, "FastenEdge", "Cách đầu thanh (mm)");
            return (TabPage)t.Parent;
        }

        private TabPage TabTekla()
        {
            var t = NewPage("Tekla");
            Note(t, "Chỉnh khi kết quả trong Tekla lệch. Dùng chữ KHÔNG DẤU cho các ô gửi sang Tekla (tên, prefix, profile...).");
            Row(t, "VProfile", "Profile V khung + gia cường (trống = tự tạo)");
            Row(t, "StripProfile", "Profile nẹp (trống = tự tạo FL w*t)");
            Row(t, "BoltStandard", "Bolt standard (vít)");
            Note(t, "Hướng đặt thép góc V khung (V gia cường dùng chung):");
            Row(t, "VPlane", "  On plane", "Left", "Middle", "Right");
            Row(t, "VRotation", "  Rotation", "Front", "Top", "Back", "Below");
            Row(t, "VDepth", "  At depth", "Front", "Middle", "Behind");
            Note(t, "Hướng đặt nẹp:");
            Row(t, "StripPlane", "  On plane", "Left", "Middle", "Right");
            Row(t, "StripRotation", "  Rotation", "Front", "Top", "Back", "Below");
            Row(t, "StripDepth", "  At depth", "Front", "Middle", "Behind");
            Row(t, "FlipMiterCut", "Đảo phía cắt vát (nếu mặt cắt vát ra ngược)");
            Row(t, "FlipBolt", "Đảo chiều vít (nếu vít ra ngược)");
            return (TabPage)t.Parent;
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
                else if (combo != null) combo.SelectedIndex = Convert.ToInt32(v);
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
                else if (combo != null) val = Enum.ToObject(f.FieldType, combo.SelectedIndex);
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

        private void Changed()
        {
            if (_loading) return;
            Read();
            UpdateEnabled();
            UpdatePreview();
        }

        private void UpdateEnabled()
        {
            bool strip = _s.UseStrip;
            foreach (string n in new[] { "StripWidth", "StripThk", "StripMaterial", "StripName", "StripClass", "StripPrefix", "StripStartNo", "Fasten" })
                Enable(n, strip);
            bool fasten = strip && _s.Fasten != FastenKind.None;
            foreach (string n in new[] { "FastenDia", "FastenPitch", "FastenEdge" }) Enable(n, fasten);

            bool stiff = _s.StiffMode != StiffMode.None;
            foreach (string n in new[] { "StiffDir", "SMaterial", "SName", "SClass", "SPrefix", "SStartNo" }) Enable(n, stiff);
            Enable("StiffMax", _s.StiffMode == StiffMode.Equal);
            Enable("StiffCells", _s.StiffMode == StiffMode.Custom);
        }

        private void Enable(string name, bool on)
        {
            _ctl[name].Enabled = on;
            if (_labels.ContainsKey(name)) _labels[name].Enabled = on;
        }

        private void UpdatePreview()
        {
            double L, H, skew;
            if (!Num(_pvL, out L) || !Num(_pvH, out H) || !Num(_pvSkew, out skew) || L <= 0 || H <= 0) { _info.Text = "Nhập L, H xem trước hợp lệ."; return; }
            var poly = new[] { new V2(0, 0), new V2(L, 0), new V2(L + skew, H), new V2(skew, H) };
            MeshFramePlan plan = MeshFramePlanner.Plan(_s, poly);
            _preview.Show(plan.Errors.Count == 0 ? plan : null, poly, _s.VLegA);

            var sb = new System.Text.StringBuilder();
            if (plan.Errors.Count > 0) { _info.ForeColor = Color.Firebrick; foreach (string e in plan.Errors) sb.AppendLine("✖ " + e); }
            else
            {
                _info.ForeColor = SystemColors.ControlText;
                int stiff = 0; foreach (PlanBar b in plan.Bars) if (b.Role == BarRole.Stiffener) stiff++;
                sb.AppendLine(string.Format("V khung 4 · gia cường {0} · nẹp {1} · vít {2} · ô {3}", stiff, plan.Strips.Count, plan.Fasteners.Count, Math.Max(1, plan.Cells.Count)));
                if (plan.Cells.Count > 1)
                {
                    var parts = new List<string>(); foreach (double c in plan.Cells) parts.Add(c.ToString("0.#", CultureInfo.InvariantCulture));
                    sb.AppendLine("Bề rộng các ô: " + string.Join(" · ", parts.ToArray()));
                }
                sb.AppendLine(string.Format("Lưới: {0} lớp sợi {1:0.##} mm, nằm dưới cánh V (phía ngoài), nẹp ngoài lưới", "2", plan.Mesh.WireDia));
                foreach (string w in plan.Warnings) sb.AppendLine("⚠ " + w);
            }
            _info.Text = sb.ToString();
        }

        private static bool Num(TextBox t, out double d)
        {
            return double.TryParse(t.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d);
        }

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
        private void Row(TableLayoutPanel t, string field, string caption, params string[] captions)
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
                else
                {
                    var tb = new TextBox { Width = 205 };
                    tb.TextChanged += delegate { Changed(); };
                    c = tb;
                }
                var lbl = new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) };
                _labels[field] = lbl;
                t.Controls.Add(lbl, 0, row);
                t.Controls.Add(c, 1, row);
            }
            _ctl[field] = c;
        }
    }
}
