using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using MeshFrame.Core;
using TeklaQuickAssign;

namespace MeshFrame.UI;

public class MeshFrameDialog : Form
{
	private readonly MeshFrameSettings _s = new MeshFrameSettings();

	private readonly Dictionary<string, Control> _ctl = new Dictionary<string, Control>();

	private readonly Dictionary<string, FieldInfo> _fields = new Dictionary<string, FieldInfo>();

	private readonly Dictionary<string, Label> _labels = new Dictionary<string, Label>();

	private readonly PreviewControl _preview = new PreviewControl
	{
		Dock = DockStyle.Fill,
		Visible = false
	};

	private readonly Preview3D _preview3d = new Preview3D
	{
		Dock = DockStyle.Fill
	};

	private readonly Label _header = new Label
	{
		Dock = DockStyle.Top,
		Height = 28,
		Padding = new Padding(6, 6, 0, 0)
	};

	private readonly CheckBox _chk2d = new CheckBox
	{
		Text = "Mặt bằng 2D (số cạnh)",
		Dock = DockStyle.Right,
		Width = 190,
		Appearance = Appearance.Normal
	};

	private readonly RichTextBox _status = new RichTextBox
	{
		Dock = DockStyle.Bottom,
		Height = 150,
		ReadOnly = true,
		BorderStyle = BorderStyle.FixedSingle
	};

	private readonly Panel _top = new Panel
	{
		Dock = DockStyle.Top,
		Height = 62
	};

	private readonly Label _logo = new Label
	{
		Text = "MF",
		TextAlign = ContentAlignment.MiddleCenter,
		Bounds = new Rectangle(14, 10, 42, 42)
	};

	private readonly Label _title = new Label
	{
		Text = "MESHFRAME",
		AutoSize = true,
		Location = new Point(66, 8)
	};

	private readonly Label _sub = new Label
	{
		AutoSize = true,
		Location = new Point(67, 34)
	};

	private readonly Label _tekla = new Label
	{
		AutoSize = true,
		Location = new Point(330, 14)
	};

	private readonly Label _teklaSub = new Label
	{
		AutoSize = true,
		Location = new Point(330, 34)
	};

	private readonly Label _model = new Label
	{
		AutoSize = true,
		Location = new Point(560, 14)
	};

	private readonly Label _modelSub = new Label
	{
		AutoSize = true,
		Location = new Point(560, 34)
	};

	private readonly Label _pill = new Label
	{
		AutoSize = false,
		TextAlign = ContentAlignment.MiddleCenter,
		Size = new Size(250, 28),
		Anchor = (AnchorStyles.Top | AnchorStyles.Right)
	};

	private readonly DataGridView _grid = new DataGridView();

	private Button _btnCreate;

	private Button _btnRepick;

	private Button _btnEdit;

	private Button _btnDeletePoint;

	private bool _loading;

	private readonly Label _hint = new Label
	{
		Dock = DockStyle.Top,
		Height = 22,
		Padding = new Padding(6, 3, 0, 0),
		Text = ""
	};

	private readonly Timer _hintTimer = new Timer
	{
		Interval = 7000
	};

	private Button _btnUndo;

	private readonly Stack<KeyValuePair<V3[], MeshFrameSettings>> _undo = new Stack<KeyValuePair<V3[], MeshFrameSettings>>();

	private V3[] _points = new V3[0];

	private string _editingId;

	private string[][] _rebarCatalog;

	private Func<MeshFrameSettings, V3[], CreateOutcome> _createHandler;

	public Func<V3[]> PickHandler { get; set; }
    public Func<List<ViewOrientation>> ViewProvider {get;set;}
    private bool _viewSynced;
    private void SyncTeklaView() {
        try {
            var views=ViewProvider?.Invoke();
            if(views==null||views.Count==0)throw new InvalidOperationException("Tekla không có view đang mở.");
            ViewOrientation selected=views[0];
            if(views.Count>1) {
                using var picker=new Form{Text="Chọn view Tekla đang xem",Width=420,Height=165,StartPosition=FormStartPosition.CenterParent};
                var list=new ComboBox{Left=15,Top=15,Width=370,DropDownStyle=ComboBoxStyle.DropDownList};
                for(int i=0;i<views.Count;i++)list.Items.Add((i+1)+". "+views[i].Name);list.SelectedIndex=0;
                var ok=new Button{Text="Dùng view",Left=270,Top=55,Width=115,DialogResult=DialogResult.OK};picker.Controls.Add(list);picker.Controls.Add(ok);picker.AcceptButton=ok;
                if(picker.ShowDialog(this)!=DialogResult.OK)return;selected=views[list.SelectedIndex];
            }
            _preview3d.SetWorldView(selected);_viewSynced=true;
        }catch(Exception ex){MessageBox.Show(this,"Không đọc được hướng view Tekla: "+ex.Message,"MeshFrame");}
    }

	public Func<MeshFrameSettings, V3[], CreateOutcome> CreateHandler
	{
		get
		{
			return _createHandler;
		}
		set
		{
			_createHandler = value;
			UpdateButtons();
		}
	}

	public Func<LoadedFrame> EditHandler { get; set; }

	public Func<string> SelectedObjectProbe { get; set; }

	public Func<string> ApiCheckHandler { get; set; }

	public V3[] Points
	{
		get
		{
			return _points;
		}
		set
		{
			_points = value ?? new V3[0];
			if(_points.Length>0)_s.AnchorPoint=Math.Max(1,Math.Min(_s.AnchorPoint,_points.Length));
			_undo.Clear();
			UpdateUndo();
			RefreshGrid();
			Changed();
		}
	}

	public string EditingId
	{
		get
		{
			return _editingId;
		}
		set
		{
			_editingId = value;
			UpdateButtons();
			Changed();
		}
	}

	public MeshFrameSettings Settings
	{
		get
		{
			Read();
			return _s;
		}
		set
		{
			FieldInfo[] fields = typeof(MeshFrameSettings).GetFields(BindingFlags.Instance | BindingFlags.Public);
			foreach (FieldInfo fieldInfo in fields)
			{
				fieldInfo.SetValue(_s, fieldInfo.GetValue(value));
			}
			PushToControls();
		}
	}

	public void SetConnection(string teklaRelease, string apiNote, string modelName, bool connected)
	{
		_tekla.Text = "Tekla " + teklaRelease;
		_teklaSub.Text = apiNote ?? "";
		_model.Text = (string.IsNullOrEmpty(modelName) ? "Model: (chưa có)" : ("Model: " + modelName));
		_modelSub.Text = (connected ? "Đang kết nối với Tekla" : "Chạy không kết nối Tekla");
		_pill.Text = (connected ? ("●  Đã kết nối · Tekla " + teklaRelease) : "●  Chưa kết nối Tekla");
		StylePill(connected);
	}

	private void StylePill(bool ok)
	{
		_pill.BackColor = (ok ? UiTheme.OkBack : UiTheme.WarnBack);
		_pill.ForeColor = (ok ? UiTheme.OkText : UiTheme.WarnText);
	}

	public void SetRebarCatalog(IList<string[]> items)
	{
		_rebarCatalog = new string[items.Count][];
		for (int i = 0; i < items.Count; i++)
		{
			_rebarCatalog[i] = items[i];
		}
		FillRebarCombos();
	}

	public MeshFrameDialog()
	{
		Text = "MESHFRAME 1.2.8 - Khung lưới: V + lưới + nẹp + gia cường";
		Font = new Font("Segoe UI", 9f);
		ClientSize = new Size(1260, 860);
		MinimumSize = new Size(1100, 760);
		StartPosition = FormStartPosition.CenterScreen;
		FormBorderStyle = FormBorderStyle.Sizable;
		MaximizeBox = true;
		FieldInfo[] fields = typeof(MeshFrameSettings).GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			_fields[fieldInfo.Name] = fieldInfo;
		}
		TabControl tabControl = new TabControl
		{
			Dock = DockStyle.Left,
			Width = 470
		};
		tabControl.TabPages.Add(TabFrame());
		tabControl.TabPages.Add(TabStiff());
		tabControl.TabPages.Add(TabMesh());
		tabControl.TabPages.Add(TabStrip());
		tabControl.TabPages.Add(TabTekla());
		Panel panel = new Panel
		{
			Dock = DockStyle.Fill
		};
		Panel panel2 = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 150
		};
		BuildGrid(panel2);
		panel.Controls.Add(_preview);
		panel.Controls.Add(_preview3d);
		panel.Controls.Add(_hint);
		panel.Controls.Add(_header);
		_header.Controls.Add(_chk2d);
		_btnUndo = new Button
		{
			Text = "↶ Hoàn tác",
			Dock = DockStyle.Right,
			Width = 100,
			Enabled = false
		};
		_btnUndo.Click += delegate
		{
			DoUndo();
		};
		_header.Controls.Add(_btnUndo);
		_hintTimer.Tick += delegate
		{
			_hintTimer.Stop();
			_hint.Text = "";
		};
		Preview3D[] array = new Preview3D[1] { _preview3d };
		foreach (Preview3D obj in array)
		{
			obj.Dims.Edited += OnDimEdited;
			obj.Dims.AnchorPicked += OnAnchorPicked;
		}
		_preview3d.TeklaViewRequested += SyncTeklaView;
        _preview.Dims.Edited += OnDimEdited;
		_preview.Dims.AnchorPicked += OnAnchorPicked;
		_chk2d.CheckedChanged += delegate
		{
			_preview.Visible = _chk2d.Checked;
			_preview3d.Visible = !_chk2d.Checked;
		};
		panel.Controls.Add(panel2);
		panel.Controls.Add(_status);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Bottom,
			Height = 48,
			FlowDirection = FlowDirection.RightToLeft,
			Padding = new Padding(8)
		};
		Button button = new Button
		{
			Text = "Đóng",
			Width = 90,
			Height = 30,
			DialogResult = DialogResult.Cancel
		};
		_btnCreate = new Button
		{
			Text = "Tạo khung trong Tekla",
			Width = 190,
			Height = 30,
			Font = new Font("Segoe UI", 9f, FontStyle.Bold)
		};
		_btnEdit = new Button
		{
			Text = "Sửa khung đã chọn",
			Width = 150,
			Height = 30
		};
		_btnRepick = new Button
		{
			Text = "Pick điểm",
			Width = 120,
			Height = 30
		};
		_btnCreate.Click += delegate
		{
			DoCreate();
		};
		_btnEdit.Click += delegate
		{
			DoEdit();
		};
		_btnRepick.Click += delegate
		{
			DoRepick();
		};
		flowLayoutPanel.Controls.AddRange(new Control[4] { button, _btnCreate, _btnEdit, _btnRepick });
		CancelButton = button;
		Controls.Add(panel);
		Controls.Add(tabControl);
		Controls.Add(flowLayoutPanel);
		_sub.Text = "Khung lưới · v1.2.8";
		_top.Controls.AddRange(new Control[8] { _logo, _title, _sub, _tekla, _teklaSub, _model, _modelSub, _pill });
		Controls.Add(_top);
		_top.SendToBack();
		SetConnection("-", "", "", connected: false);
		_pill.Location = new Point(ClientSize.Width - _pill.Width - 14, 17);
		_top.Resize += delegate
		{
			_pill.Location = new Point(_top.ClientSize.Width - _pill.Width - 14, 17);
		};
		PushToControls();
		RefreshGrid();
		UpdateButtons();
		ApplyTheme();
	}

	private void ApplyTheme()
	{
		UiTheme.Apply(this);
		_top.BackColor = UiTheme.Header;
		foreach (Control control in _top.Controls)
		{
			control.ForeColor = UiTheme.Text;
			if (control != _pill)
			{
				control.BackColor = Color.Transparent;
			}
		}
		_logo.BackColor = UiTheme.OnFill;
		_logo.ForeColor = UiTheme.AccentBar;
		_logo.Font = UiFonts.Semi(13f);
		_title.Font = UiFonts.Semi(14f);
		_sub.Font = UiFonts.Body(8.5f);
		_sub.ForeColor = UiTheme.Muted;
		_tekla.Font = UiFonts.Semi(9.5f);
		_model.Font = UiFonts.Semi(9.5f);
		_teklaSub.Font = UiFonts.Body(8.5f);
		_teklaSub.ForeColor = UiTheme.Muted;
		_modelSub.Font = UiFonts.Body(8.5f);
		_modelSub.ForeColor = UiTheme.Muted;
		_pill.Font = UiFonts.Semi(9.5f);
		StylePill(_pill.Text.IndexOf("Đã kết nối") >= 0);
		_header.ForeColor = UiTheme.Text;
		_header.Font = UiFonts.Semi(9.5f);
		_status.BackColor = UiTheme.FieldInput;
		_status.ForeColor = UiTheme.Text;
		_preview3d.TextColor = UiTheme.Text;
		_hint.ForeColor = UiTheme.InfoText;
		_preview3d.Dims.Back = UiTheme.Bg;
	}

	private TabPage TabFrame()
	{
		TableLayoutPanel tableLayoutPanel = NewPage("Khung V");
		Note(tableLayoutPanel, "Mỗi cạnh của đa giác pick là một thanh thép góc; mép ngoài khung đi qua các điểm pick.");
		Row(tableLayoutPanel,"AnchorPoint","Điểm gốc giữ cố định khi sửa số");
		Note(tableLayoutPanel,"Chọn P1/P2/P3/… làm gốc. XYZ của gốc là tọa độ điểm đó trong bảng; có thể sửa XYZ tại hàng tương ứng. Chọn khung để cập nhật sẽ nạp vị trí hiện tại trong Tekla.");
		Note(tableLayoutPanel, "Số cạnh: 1 = P1–P2, 2 = P2–P3, …, cạnh cuối về P1. Sửa X/Y/Z chỉ đổi điểm đó; chiều dài các cạnh nối với điểm được tính lại. Sửa số đỏ giữ nguyên khung chính; phần tăng/giảm bù vào ô biên phía cuối, các ô khác giữ nguyên. Không giới hạn chiều dài theo chuẩn cố định.");
		Row(tableLayoutPanel, "VLegA", "Cạnh V - a (mm)");
		Row(tableLayoutPanel, "VLegB", "Cạnh V - b (mm)");
		Row(tableLayoutPanel, "VThk", "Độ dày V (mm)");
		Row(tableLayoutPanel, "VMaterial", "Vật liệu");
		Row(tableLayoutPanel, "VName", "Tên");
		Row(tableLayoutPanel, "VClass", "Class");
		Row(tableLayoutPanel, "VPrefix", "Prefix part");
		Row(tableLayoutPanel, "VStartNo", "Số bắt đầu part");
		Row(tableLayoutPanel, "AsmPrefix", "Prefix assembly");
		Row(tableLayoutPanel, "AsmStartNo", "Số bắt đầu assembly");
		Row(tableLayoutPanel, "Corner", "Kiểu nối góc", "Cắt vát 45° (theo phân giác góc)", "Một cạnh chạy dài, một cạnh dừng");
		Note(tableLayoutPanel, "Một cạnh chạy dài, một cạnh dừng: các cạnh SONG SONG cạnh gốc (mặc định cạnh dài nhất, chọn ở tab Gia cường) chạy suốt qua góc; các cạnh còn lại dừng sát mặt trong và cánh nằm được cắt khuyết.");
		return (TabPage)tableLayoutPanel.Parent;
	}

	private TabPage TabStiff()
	{
		TableLayoutPanel tableLayoutPanel = NewPage("Gia cường");
		Note(tableLayoutPanel, "V gia cường CÙNG TIẾT DIỆN V khung, vuông góc với CẠNH GỐC, hai đầu cắt khuyết cánh nằm cho khớp V khung. Các ô đo dọc cạnh gốc, tính tới TIM V gia cường (giữa bề rộng cánh nằm): ô đầu = mép ngoài khung → tim, ô giữa = tim → tim, ô cuối = tim → mép ngoài khung đối diện. Bấm đúp số đỏ trên hình để sửa. Lưới kéo căng liên tục; nẹp PL đi theo từng thanh.");
		Row(tableLayoutPanel, "StiffMode", "Chia ô", "Không gia cường", "Chia đều (mỗi ô tối đa)", "Nhập từng ô");
		Row(tableLayoutPanel, "StiffBaseEdge", "Cạnh gốc (0 = tự chọn cạnh dài nhất)");
		Row(tableLayoutPanel, "StiffMax", "Mỗi ô tối đa (mm)");
		Row(tableLayoutPanel, "StiffCells", "Bề rộng từng ô (mm)");
		Note(tableLayoutPanel, "Nhập từng ô: cách nhau bằng dấu cách, vd  1000 2*1500 800  (n*x = lặp n lần). Sửa số đỏ: tổng khung giữ nguyên. Gốc ở đầu nào thì bù ô cuối phía đối diện; sửa chính ô cuối thì bù ô biên đầu kia. Không đủ chỗ sẽ báo rõ, không kéo dài khung. Số cạnh xem ở hình bên phải (\"1: 3000\" = cạnh 1 dài 3000).");
		Row(tableLayoutPanel, "SMaterial", "Vật liệu");
		Row(tableLayoutPanel, "SName", "Tên");
		Row(tableLayoutPanel, "SClass", "Class");
		Row(tableLayoutPanel, "SPrefix", "Prefix part");
		Row(tableLayoutPanel, "SStartNo", "Số bắt đầu part");
		return (TabPage)tableLayoutPanel.Parent;
	}

	private TabPage TabMesh()
	{
		TableLayoutPanel tableLayoutPanel = NewPage("Lưới");
		Note(tableLayoutPanel, "Lưới nằm phía ngoài cánh nằm V, nẹp ép ngoài lưới. RebarMesh cần mác và cỡ sợi CÓ trong catalog rebar của Tekla; Tekla từ chối thì tự thay bằng tấm mỏng.");
		Row(tableLayoutPanel, "MeshKind", "Kiểu lưới", "RebarMesh (tự chuyển sang tấm nếu lỗi)", "Tấm mỏng (plate)", "Không tạo lưới");
		Row(tableLayoutPanel, "MeshGap", "Lưới lùi cách mép ngoài khung (mm)");
		Row(tableLayoutPanel, "MeshWireDia", "Đường kính sợi (mm)");
		Row(tableLayoutPanel, "MeshPitch", "Bước sợi = ô + sợi (mm)");
		Row(tableLayoutPanel, "RebarGrade", "Mác rebar (trống = mác bên dưới)", editableCombo: true);
		Row(tableLayoutPanel, "RebarSize", "Cỡ sợi rebar (trống = đường kính sợi)", editableCombo: true);
		Row(tableLayoutPanel, "MeshName", "Tên");
		Row(tableLayoutPanel, "MeshGrade", "Mác thép / vật liệu tấm");
		Row(tableLayoutPanel, "MeshPrefix", "Prefix (numbering series)");
		Row(tableLayoutPanel, "MeshStartNo", "Số bắt đầu");
		Row(tableLayoutPanel, "MeshClass", "Class");
		Row(tableLayoutPanel, "MeshWeightKgPerM2", "Khối lượng nhập tay (kg/m²)");
		Note(tableLayoutPanel, "Khối lượng nhập tay: 0 = không dùng. Dùng khi Tekla tính khối lượng lưới không đúng.");
		return (TabPage)tableLayoutPanel.Parent;
	}

	private TabPage TabStrip()
	{
		TableLayoutPanel tableLayoutPanel = NewPage("Nẹp");
		Row(tableLayoutPanel, "UseStrip", "Có nẹp");
		Note(tableLayoutPanel, "Nẹp PL nằm phía ngoài, ép lưới vào cánh nằm V (chuẩn PL-25x1.2, rộng bằng cánh V); chạy quanh khung và theo các thanh gia cường.");
		Row(tableLayoutPanel, "StripPos", "Nẹp V gia cường", "Giữa V (giữa cánh nằm)", "Trái V (sát đầu cánh nằm, phía trong)", "Phải V (sát cánh đứng, phía ngoài)");
		Note(tableLayoutPanel, "Giữa / Trái / Phải tính theo bề rộng cánh nằm V: Phải = mép nẹp sát cánh đứng, Trái = mép nẹp trùng đầu cánh nằm. Nẹp rộng bằng cánh V thì ba vị trí trùng nhau. CHỈ áp cho nẹp trên V gia cường; nẹp quanh khung luôn có mép ngoài trùng mép ngoài V khung.");
		Row(tableLayoutPanel, "StripWidth", "Bề rộng nẹp (mm)");
		Row(tableLayoutPanel, "StripThk", "Độ dày nẹp (mm)");
		Row(tableLayoutPanel, "StripMaterial", "Vật liệu");
		Row(tableLayoutPanel, "StripName", "Tên");
		Row(tableLayoutPanel, "StripClass", "Class");
		Row(tableLayoutPanel, "StripPrefix", "Prefix part");
		Row(tableLayoutPanel, "StripStartNo", "Số bắt đầu part");
		return (TabPage)tableLayoutPanel.Parent;
	}

	private TabPage TabTekla()
	{
		TableLayoutPanel tableLayoutPanel = NewPage("Tekla");
		Note(tableLayoutPanel, "Mặc định app TỰ ĐO hình khối thật mà Tekla tạo ra (thép góc xoay đúng chiều, nẹp đúng vị trí so với cánh V, dưới lưới) rồi canh lại; sau khi tạo còn ĐO LẠI trên từng thanh vừa tạo, thanh nào lệch thì dời và báo số đo trong kết quả. Chỉ tắt khi muốn tự đặt các ô On plane / Rotation / At depth bên dưới. Dùng chữ KHÔNG DẤU cho các ô gửi sang Tekla.");
		Row(tableLayoutPanel, "AutoPlacement", "Tự canh V và nẹp theo hình khối thật (khuyến nghị)");
		Row(tableLayoutPanel, "VProfile", "Profile V khung + gia cường (trống = tự tạo)");
		Row(tableLayoutPanel, "StripProfile", "Profile nẹp (trống = tự tạo FL w*t)");
		Note(tableLayoutPanel, "Hướng đặt thép góc V khung (V gia cường dùng chung):");
		Row(tableLayoutPanel, "VPlane", "  On plane", "Left", "Middle", "Right");
		Row(tableLayoutPanel, "VRotation", "  Rotation", "Front", "Top", "Back", "Below");
		Row(tableLayoutPanel, "VDepth", "  At depth", "Front", "Middle", "Behind");
		Note(tableLayoutPanel, "Hướng đặt nẹp:");
		Row(tableLayoutPanel, "StripPlane", "  On plane", "Left", "Middle", "Right");
		Row(tableLayoutPanel, "StripRotation", "  Rotation", "Front", "Top", "Back", "Below");
		Row(tableLayoutPanel, "StripDepth", "  At depth", "Front", "Middle", "Behind");
		Row(tableLayoutPanel, "VLateral", "Lệch ngang V thêm (mm, + vào trong khung)");
		Row(tableLayoutPanel, "StripLateral", "Lệch ngang nẹp thêm (mm, + vào trong khung)");
		Row(tableLayoutPanel, "FlipMiterCut", "Đảo phía cắt vát (nếu mặt cắt vát ra ngược)");
		Button button = new Button
		{
			Text = "Đọc lưới mẫu đang chọn trong Tekla",
			AutoSize = true,
			Margin = new Padding(3, 10, 3, 3)
		};
		button.Click += delegate
		{
			if (SelectedObjectProbe == null)
			{
				MessageBox.Show("Chỉ dùng khi chạy cùng Tekla.", "MeshFrame");
			}
			else
			{
				string text;
				try
				{
					text = SelectedObjectProbe();
				}
				catch (Exception ex)
				{
					text = "Lỗi: " + ex.Message;
				}
				MessageBox.Show(text, "MeshFrame");
			}
		};
		int row = tableLayoutPanel.RowCount++;
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.Controls.Add(button, 0, row);
		tableLayoutPanel.SetColumnSpan(button, 2);
		Button button2 = new Button
		{
			Text = "Kiểm tra API của bản Tekla này",
			AutoSize = true,
			Margin = new Padding(3, 6, 3, 3)
		};
		button2.Click += delegate
		{
			if (ApiCheckHandler == null)
			{
				MessageBox.Show("Chỉ dùng khi chạy cùng Tekla.", "MeshFrame");
			}
			else
			{
				string text;
				try
				{
					text = ApiCheckHandler();
				}
				catch (Exception ex)
				{
					text = "Lỗi: " + ex.Message;
				}
				MessageBox.Show(text, "MeshFrame");
			}
		};
		int row2 = tableLayoutPanel.RowCount++;
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.Controls.Add(button2, 0, row2);
		tableLayoutPanel.SetColumnSpan(button2, 2);
		return (TabPage)tableLayoutPanel.Parent;
	}

	private void BuildGrid(Panel host)
	{
		_grid.Dock = DockStyle.Fill;
		_grid.AllowUserToAddRows = false;
		_grid.AllowUserToDeleteRows = false;
		_grid.RowHeadersVisible = false;
		_grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
		_grid.MultiSelect = false;
		_grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
		_grid.Columns.Add("pt", "Điểm");
		_grid.Columns[0].ReadOnly = true;
		_grid.Columns[0].Width = 60;
		_grid.Columns.Add("x", "X (mm)");
		_grid.Columns.Add("y", "Y (mm)");
		_grid.Columns.Add("z", "Z (mm)");
		int[] array = new int[3] { 1, 2, 3 };
		foreach (int index in array)
		{
			_grid.Columns[index].Width = 120;
		}
		_grid.Columns.Add("len", "Dài tới điểm kế (mm)");
		_grid.Columns[4].ReadOnly = true;
		_grid.Columns[4].Width = 150;
		_grid.CellEndEdit += (object s, DataGridViewCellEventArgs e) =>
		{
			GridEdited(e.RowIndex, e.ColumnIndex);
		};
		_btnDeletePoint = new Button
		{
			Text = "Xóa điểm đã chọn",
			Dock = DockStyle.Right,
			Width = 130
		};
		_btnDeletePoint.Click += delegate
		{
			_grid.EndEdit();
			if (_grid.CurrentRow != null && _points.Length > 3)
			{
				int index2 = _grid.CurrentRow.Index;
				PushUndo();
				if(index2<_s.AnchorPoint-1)_s.AnchorPoint--;
				List<V3> list = new List<V3>(_points);
				list.RemoveAt(index2);
				_points = list.ToArray();
				_s.AnchorPoint=Math.Max(1,Math.Min(_s.AnchorPoint,_points.Length));
				RefreshGrid();
				Changed();
			}
		};
		Label value = new Label
		{
			Text = "Các điểm pick (sửa được tọa độ; tự nối điểm cuối về điểm đầu)",
			Dock = DockStyle.Top,
			Height = 20,
			Padding = new Padding(4, 3, 0, 0)
		};
		host.Controls.Add(_grid);
		host.Controls.Add(_btnDeletePoint);
		host.Controls.Add(value);
	}

	private void RefreshGrid()
	{
		_loading = true;
		SyncAnchorSelector();
		try {
			// CellEndEdit may run inside SetCurrentCellAddressCore. Keep existing rows/cell focus.
			if (_grid.Rows.Count != _points.Length) {
				_grid.Rows.Clear();
				for (int i = 0; i < _points.Length; i++) _grid.Rows.Add();
			}
			for (int i = 0; i < _points.Length; i++) {
				V3 v = _points[i], next = _points[(i + 1) % _points.Length];
				object[] values = { "P" + (i + 1), F(v.X), F(v.Y), F(v.Z), F((next - v).Length) };
				for (int j=0;j<values.Length;j++) _grid.Rows[i].Cells[j].Value=values[j];
			}
		} finally { _loading = false; }
	}

	private void GridEdited(int row, int col)
	{
		if (!_loading && row >= 0 && col >= 1 && col <= 3)
		{
			if (!double.TryParse(Convert.ToString(_grid.Rows[row].Cells[col].Value).Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || double.IsNaN(result) || double.IsInfinity(result))
			{
				RefreshGrid();
				return;
			}
			V3 v = _points[row];
			double previous=col==1?v.X:col==2?v.Y:v.Z;
			if(previous==result)return;
			PushUndo();
			_points[row] = new V3((col == 1) ? result : v.X, (col == 2) ? result : v.Y, (col == 3) ? result : v.Z);
			RefreshGrid();
			Changed();
		}
	}

	private void Hint(string text, bool error)
	{
		_hint.ForeColor = (error ? UiTheme.ErrText : UiTheme.OkText);
		_hint.Text = (error ? "✖ " : "✔ ") + text;
		_hintTimer.Stop();
		_hintTimer.Start();
	}

	private void PushUndo()
	{
		Read();
		_undo.Push(new KeyValuePair<V3[], MeshFrameSettings>((V3[])_points.Clone(), DimEdit.Clone(_s)));
		while (_undo.Count > 60)
		{
			KeyValuePair<V3[], MeshFrameSettings>[] array = _undo.ToArray();
			_undo.Clear();
			for (int num = 59; num >= 0; num--)
			{
				_undo.Push(array[num]);
			}
		}
		UpdateUndo();
	}

	private void UpdateUndo()
	{
		if (_btnUndo != null)
		{
			_btnUndo.Enabled = _undo.Count > 0;
		}
	}

	private void DoUndo()
	{
		// Finish a pending coordinate edit first: its snapshot must precede the pop.
		_grid.EndEdit();
		if (_undo.Count != 0)
		{
			KeyValuePair<V3[], MeshFrameSettings> keyValuePair = _undo.Pop();
			_points = keyValuePair.Key;
			Settings = keyValuePair.Value;
			RefreshGrid();
			Changed();
			UpdateUndo();
			Hint("Đã hoàn tác.", error: false);
		}
	}

	private void OnAnchorPicked(int index)
	{
		if (index >= 0 && index < _points.Length && _s.AnchorPoint != index + 1)
		{
			PushUndo();
			_s.AnchorPoint = index + 1;
			SyncAnchorSelector();
			Changed();
			Hint("Điểm gốc: P" + (index + 1) + " (đứng yên khi sửa số; là gốc tọa độ khi dựng vào Tekla).", error: false);
		}
	}

	private void OnDimEdited(DimItem item, double value)
	{
		Read();
		if (_points.Length < 3 || !MeshFrameBuilder.TryFrame(_points, _s.AnchorPoint - 1, out var frame, out var poly, out var _))
		{
			Hint("Chưa có khung hợp lệ để sửa số.", error: true);
			return;
		}
		EditResult editResult = DimEdit.Apply(MeshFramePlanner.Plan(_s, poly), frame, _s, _points, item.Kind, item.Index, value);
		if (!editResult.Ok)
		{
			Hint("Không sửa được: " + editResult.Error, error: true);
			return;
		}
		PushUndo();
		_points = editResult.Points;
		Settings = editResult.Settings;
		RefreshGrid();
		Changed();
		Hint("Đã sửa " + ((item.Kind == DimKind.Edge) ? ("cạnh " + (item.Index + 1)) : ("ô " + (item.Index + 1))) + " thành " + value.ToString("0.##", CultureInfo.InvariantCulture) + " mm (điểm gốc P" + _s.AnchorPoint + " đứng yên).", error: false);
		if (!string.IsNullOrEmpty(editResult.Summary))
			Line(item.Kind==DimKind.Cell?editResult.Summary:"Đổi cạnh: "+editResult.Summary+" mm. Các cạnh liên quan được tính lại theo tọa độ.", UiTheme.OkText, bold: false);
	}

	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		if (keyData == (Keys.Z | Keys.Control))
		{
			// Preserve ordinary text undo inside an active text edit. Grid coordinates
			// are document edits and use the same history as deleted points/dimensions.
			Control focused=ActiveControl;
			while(focused is ContainerControl container && container.ActiveControl!=null)focused=container.ActiveControl;
			if(focused is TextBoxBase text && !_grid.ContainsFocus && text.CanUndo)return base.ProcessCmdKey(ref msg,keyData);
			DoUndo();
			return true;
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	private void DoRepick()
	{
		if (PickHandler == null)
		{
			MessageBox.Show("Chỉ dùng khi chạy cùng Tekla.", "MeshFrame");
			return;
		}
		V3[] array = null;
		Visible = false;
		try
		{
			array = PickHandler();
		}
		catch (Exception ex)
		{
			Visible = true;
			MessageBox.Show("Lỗi khi pick: " + ex.Message, "MeshFrame", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		Visible = true;
		Activate();
		if (array != null)
		{
			_viewSynced=false;
			EditingId = null;
			Points = array;
		}
	}

	private void DoEdit()
	{
		if (EditHandler == null)
		{
			MessageBox.Show("Chỉ dùng khi chạy cùng Tekla.", "MeshFrame");
			return;
		}
		LoadedFrame loadedFrame;
		try
		{
			loadedFrame = EditHandler();
		}
		catch (Exception ex)
		{
			MessageBox.Show("Lỗi khi đọc khung: " + ex.Message, "MeshFrame", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		if (loadedFrame == null || loadedFrame.Error != null)
		{
			MessageBox.Show((loadedFrame == null) ? "Không đọc được khung." : loadedFrame.Error, "MeshFrame");
			return;
		}
		_viewSynced=false;
		_points = loadedFrame.Points;
		Settings = loadedFrame.Settings;
		RefreshGrid();
		EditingId = loadedFrame.Id;
	}

	private void DoCreate()
	{
		_grid.EndEdit();
		if (CreateHandler == null)
		{
			MessageBox.Show("Chỉ dùng khi chạy cùng Tekla.", "MeshFrame");
			return;
		}
		Read();
		MeshFramePlan meshFramePlan = CurrentPlan();
		if (meshFramePlan == null || meshFramePlan.Errors.Count > 0)
		{
			string reason = "Chưa tạo được: " + ((meshFramePlan == null) ? "chưa có đủ điểm hoặc các điểm không tạo thành mặt phẳng hợp lệ." : string.Join("\n", meshFramePlan.Errors.ToArray()));
			Line(reason, UiTheme.ErrText, bold: true);
			MessageBox.Show(reason, "MeshFrame");
			return;
		}
		_btnCreate.Enabled = false;
		Cursor = Cursors.WaitCursor;
		CreateOutcome createOutcome;
		try
		{
			createOutcome = CreateHandler(_s, (V3[])_points.Clone());
		}
		catch (Exception ex)
		{
			createOutcome = new CreateOutcome
			{
				Success = false,
				Message = "Lỗi: " + ex.Message
			};
		}
		finally
		{
			Cursor = Cursors.Default;
			_btnCreate.Enabled = true;
		}
		if (createOutcome.Success)
		{
			_editingId = null;
			if(createOutcome.Points!=null){_points=createOutcome.Points;RefreshGrid();}
			Changed();
		}
		AppendResult(createOutcome);
	}

	private void AppendResult(CreateOutcome o)
	{
		_status.SelectionStart = _status.TextLength;
		_status.SelectionColor = (o.Success ? UiTheme.OkText : UiTheme.ErrText);
		_status.AppendText((o.Success ? "✔ " : "✖ ") + o.Message + Environment.NewLine);
		_status.SelectionColor = UiTheme.Text;
		Changed(resetStatus: false);
		if (!o.Success)
		{
			MessageBox.Show(o.Message, "MeshFrame", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
	}

	private void PushToControls()
	{
		_loading = true;
		foreach (KeyValuePair<string, Control> item in _ctl)
		{
			object value = _fields[item.Key].GetValue(_s);
			CheckBox checkBox = item.Value as CheckBox;
			ComboBox comboBox = item.Value as ComboBox;
			if (checkBox != null)
			{
				checkBox.Checked = (bool)value;
			}
			else if (comboBox != null && _fields[item.Key].FieldType.IsEnum)
			{
				comboBox.SelectedIndex = Convert.ToInt32(value);
			}
			else if(item.Key=="AnchorPoint")SyncAnchorSelector();
			else
			{
				item.Value.Text = Convert.ToString(value, CultureInfo.InvariantCulture);
			}
		}
		_loading = false;
		Changed();
	}

	private void Read()
	{
		foreach (KeyValuePair<string, Control> item in _ctl)
		{
			FieldInfo fieldInfo = _fields[item.Key];
			CheckBox checkBox = item.Value as CheckBox;
			ComboBox comboBox = item.Value as ComboBox;
			object obj = null;
			bool flag = true;
			if (checkBox != null)
			{
				obj = checkBox.Checked;
			}
			else if(item.Key=="AnchorPoint"&&comboBox!=null){obj=_points.Length>0?Math.Max(1,comboBox.SelectedIndex+1):_s.AnchorPoint;}
			else if (comboBox != null && fieldInfo.FieldType.IsEnum)
			{
				obj = Enum.ToObject(fieldInfo.FieldType, comboBox.SelectedIndex);
			}
			else if (fieldInfo.FieldType == typeof(string))
			{
				obj = item.Value.Text;
			}
			else if (fieldInfo.FieldType == typeof(double))
			{
				flag = double.TryParse(item.Value.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var result);
				obj = result;
			}
			else if (fieldInfo.FieldType == typeof(int))
			{
				flag = int.TryParse(item.Value.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2);
				obj = result2;
			}
			if (item.Value is TextBox)
			{
				item.Value.BackColor = (flag ? UiTheme.FieldInput : UiTheme.ErrBack);
			}
			if (flag && obj != null)
			{
				fieldInfo.SetValue(_s, obj);
			}
		}
	}

	private void Changed()
	{
		Changed(resetStatus: true);
	}

	private void Changed(bool resetStatus)
	{
		if (!_loading)
		{
			Read();
			UpdateEnabled();
			if (resetStatus) UpdatePreview();
			UpdateButtons();
		}
	}

	private void UpdateEnabled()
	{
		bool useStrip = _s.UseStrip;
		string[] array = new string[8] { "StripPos", "StripWidth", "StripThk", "StripMaterial", "StripName", "StripClass", "StripPrefix", "StripStartNo" };
		foreach (string name in array)
		{
			Enable(name, useStrip);
		}
		array = new string[4] { "VPlane", "VDepth", "StripPlane", "StripDepth" };
		foreach (string name2 in array)
		{
			Enable(name2, !_s.AutoPlacement);
		}
		bool flag = _s.StiffMode != StiffMode.None;
		array = new string[6] { "StiffBaseEdge", "SMaterial", "SName", "SClass", "SPrefix", "SStartNo" };
		foreach (string name3 in array)
		{
			Enable(name3, flag);
		}
		Enable("StiffMax", _s.StiffMode == StiffMode.Equal);
		Enable("StiffCells", _s.StiffMode == StiffMode.Custom);
		bool flag2 = _s.MeshKind != MeshKind.None;
		array = new string[9] { "MeshGap", "MeshWireDia", "MeshPitch", "MeshName", "MeshGrade", "MeshPrefix", "MeshStartNo", "MeshClass", "MeshWeightKgPerM2" };
		foreach (string name4 in array)
		{
			Enable(name4, flag2);
		}
		Enable("RebarGrade", _s.MeshKind == MeshKind.RebarMesh);
		Enable("RebarSize", _s.MeshKind == MeshKind.RebarMesh);
	}

	private void Enable(string name, bool on)
	{
		_ctl[name].Enabled = on;
		if (_labels.ContainsKey(name))
		{
			_labels[name].Enabled = on;
		}
	}

	private void UpdateButtons()
	{
		if (_btnCreate != null)
		{
			bool flag = _editingId != null;
			_btnCreate.Text = (flag ? "Cập nhật khung đã chọn (sửa tại chỗ)" : "Tạo khung trong Tekla");
			_btnCreate.Width = (flag ? 270 : 190);
			MeshFramePlan meshFramePlan = CurrentPlan();
			// Keep the action available; DoCreate validates and explains a failure explicitly.
			_btnCreate.Enabled = CreateHandler != null;
			_btnDeletePoint.Enabled = _points.Length > 3;
		}
	}

	private MeshFramePlan CurrentPlan()
	{
		if (_points.Length < 3 || !MeshFrameBuilder.TryFrame(_points, _s.AnchorPoint - 1, out var _, out var poly, out var _))
		{
			return null;
		}
		return MeshFramePlanner.Plan(_s, poly);
	}

	private void UpdatePreview()
	{
		string error = null;
		bool flag = _points.Length >= 3 && MeshFrameBuilder.TryFrame(_points, _s.AnchorPoint - 1, out var frame, out var poly, out error);
		if (_points.Length < 3)
		{
			_preview.SetDims(null, 0);
			_preview3d.SetDims(null, 0);
			_preview.ShowEmpty(null);
			_preview3d.ShowEmpty(null);
			_header.Text = ((_points.Length == 0) ? "Chưa có điểm" : ("Mới có " + _points.Length + " điểm, cần ít nhất 3"));
			SetStatus(new List<string>(), new List<string> { "Chưa đủ điểm: bấm \"Pick điểm\" để pick đa giác trong Tekla." }, new List<string>(), null);
			return;
		}
		if (!flag)
		{
			_preview.ShowEmpty("Không dựng được khung: " + error);
			_preview3d.ShowEmpty("Không dựng được khung: " + error);
			_header.Text = _points.Length + " điểm";
			SetStatus(new List<string> { error }, new List<string>(), new List<string>(), null);
			return;
		}
		MeshFrameBuilder.TryFrame(_points, _s.AnchorPoint - 1, out frame, out poly, out error);
		MeshFramePlan meshFramePlan = MeshFramePlanner.Plan(_s, poly);
		List<DimItem> items = ((meshFramePlan.Errors.Count == 0) ? Annotations.Build(meshFramePlan) : new List<DimItem>());
		_preview.SetDims(items, _s.AnchorPoint - 1);
		_preview3d.SetDims(items, _s.AnchorPoint - 1);
		_preview.Show((meshFramePlan.Errors.Count == 0) ? meshFramePlan : null, poly, _s.VLegA);
		if (meshFramePlan.Errors.Count == 0)
		{
			_preview3d.SetWorldFrame(frame);
            _preview3d.Show(meshFramePlan, poly);
            if(!_viewSynced && ViewProvider!=null) { _viewSynced=true; BeginInvoke(new Action(SyncTeklaView)); }
		}
		else
		{
			_preview3d.ShowEmpty("Có lỗi nhập liệu: xem phần trạng thái bên dưới.");
		}
		double num = 0.0;
		for (int i = 0; i < _points.Length; i++)
		{
			num += (_points[(i + 1) % _points.Length] - _points[i]).Length;
		}
		double num2 = ((meshFramePlan.Polygon != null) ? (Math.Abs(Geo.Area(meshFramePlan.Polygon)) / 1000000.0) : 0.0);
		_header.Text = _points.Length + " điểm · gốc P" + _s.AnchorPoint + " · chu vi " + F(num) + " mm · diện tích " + num2.ToString("0.###", CultureInfo.InvariantCulture) + " m²" + ((_editingId != null) ? ("   —   ĐANG SỬA KHUNG " + _editingId.Substring(0, Math.Min(8, _editingId.Length))) : "");
		int num3 = 0;
		foreach (PlanBar bar in meshFramePlan.Bars)
		{
			if (bar.Role == BarRole.Stiffener)
			{
				num3++;
			}
		}
		string summary = ((meshFramePlan.Errors.Count > 0) ? null : string.Format("Sẽ tạo: {0} V khung · {1} gia cường · {2} nẹp · lưới {3} · {4} ô", meshFramePlan.Bars.Count - num3, num3, meshFramePlan.Strips.Count, (meshFramePlan.Mesh == null) ? "không" : ((meshFramePlan.Mesh.Kind == MeshKind.RebarMesh) ? "RebarMesh" : "tấm mỏng"), Math.Max(1, meshFramePlan.Cells.Count)));
		SetStatus(meshFramePlan.Errors, meshFramePlan.Warnings, meshFramePlan.Notes, summary);
	}

	private void SetStatus(List<string> errors, List<string> warnings, List<string> notes, string summary)
	{
		_status.Clear();
		if (summary != null)
		{
			Line(summary, UiTheme.Text, bold: true);
		}
		foreach (string error in errors)
		{
			Line("✖ " + error, UiTheme.ErrText, bold: true);
		}
		foreach (string note in notes)
		{
			Line("ℹ " + note, UiTheme.InfoText, bold: false);
		}
		foreach (string warning in warnings)
		{
			Line("⚠ " + warning, UiTheme.WarnText, bold: false);
		}
		if (errors.Count > 0)
		{
			Line("→ Chưa thể tạo khung: sửa các lỗi trên. Bấm Tạo để xem lý do cụ thể.", UiTheme.ErrText, bold: true);
		}
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
		if (_rebarCatalog == null)
		{
			return;
		}
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string[][] rebarCatalog = _rebarCatalog;
		foreach (string[] array in rebarCatalog)
		{
			if (!list.Contains(array[0]))
			{
				list.Add(array[0]);
			}
			if (!list2.Contains(array[1]))
			{
				list2.Add(array[1]);
			}
		}
		ComboBox comboBox = _ctl["RebarGrade"] as ComboBox;
		ComboBox obj = _ctl["RebarSize"] as ComboBox;
		string text = comboBox.Text;
		string text2 = obj.Text;
		comboBox.Items.Clear();
		ComboBox.ObjectCollection items = comboBox.Items;
		object[] items2 = list.ToArray();
		items.AddRange(items2);
		obj.Items.Clear();
		ComboBox.ObjectCollection items3 = obj.Items;
		items2 = list2.ToArray();
		items3.AddRange(items2);
		comboBox.Text = text;
		obj.Text = text2;
	}

	private static string F(double d)
	{
		return d.ToString("0.##", CultureInfo.InvariantCulture);
	}

	private static TableLayoutPanel NewPage(string title)
	{
		TabPage tabPage = new TabPage(title)
		{
			Padding = new Padding(8),
			AutoScroll = true
		};
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			ColumnCount = 2
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215f));
		tabPage.Controls.Add(tableLayoutPanel);
		return tableLayoutPanel;
	}

	private static void Note(TableLayoutPanel t, string text)
	{
		Label control = new Label
		{
			Text = text,
			MaximumSize = new Size(395, 0),
			AutoSize = true,
			Margin = new Padding(3, 6, 3, 6)
		};
		int row = t.RowCount++;
		t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		t.Controls.Add(control, 0, row);
		t.SetColumnSpan(control, 2);
	}

	private void Row(TableLayoutPanel t, string field, string caption, params string[] captions)
	{
		RowCore(t, field, caption, editableCombo: false, captions);
	}

	private void Row(TableLayoutPanel t, string field, string caption, bool editableCombo)
	{
		RowCore(t, field, caption, editableCombo);
	}

	private void RowCore(TableLayoutPanel t, string field, string caption, bool editableCombo, params string[] captions)
	{
		FieldInfo fieldInfo = _fields[field];
		int row = t.RowCount++;
		t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		Control control;
		if (fieldInfo.FieldType == typeof(bool))
		{
			CheckBox checkBox = new CheckBox
			{
				Text = caption,
				AutoSize = true,
				Margin = new Padding(3, 6, 3, 6)
			};
			checkBox.CheckedChanged += delegate
			{
				Changed();
			};
			t.Controls.Add(checkBox, 0, row);
			t.SetColumnSpan(checkBox, 2);
			control = checkBox;
		}
		else
		{
			if(field=="AnchorPoint") {
				var anchor=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=205};
				anchor.SelectedIndexChanged+=delegate {Changed();};control=anchor;
			}
			else if (fieldInfo.FieldType.IsEnum)
			{
				ComboBox comboBox = new ComboBox
				{
					DropDownStyle = ComboBoxStyle.DropDownList,
					Width = 205
				};
				comboBox.Items.AddRange(captions);
				comboBox.SelectedIndexChanged += delegate
				{
					Changed();
				};
				control = comboBox;
			}
			else if (editableCombo)
			{
				ComboBox comboBox2 = new ComboBox();
				comboBox2.DropDownStyle = ComboBoxStyle.DropDown;
				comboBox2.Width = 205;
				comboBox2.TextChanged += delegate
				{
					Changed();
				};
				control = comboBox2;
			}
			else
			{
				TextBox textBox = new TextBox();
				textBox.Width = 205;
				textBox.TextChanged += delegate
				{
					Changed();
				};
				control = textBox;
			}
			Label label = new Label
			{
				Text = caption,
				AutoSize = true,
				MaximumSize = new Size(205, 0),
				Anchor = AnchorStyles.Left,
				Margin = new Padding(3, 8, 3, 3)
			};
			_labels[field] = label;
			t.Controls.Add(label, 0, row);
			t.Controls.Add(control, 1, row);
		}
		_ctl[field] = control;
	}

	private void SyncAnchorSelector()
	{
		Control control;if(!_ctl.TryGetValue("AnchorPoint",out control))return;
		var combo=control as ComboBox;if(combo==null)return;
		bool wasLoading=_loading;_loading=true;
		try{combo.Items.Clear();for(int i=0;i<_points.Length;i++)combo.Items.Add("P"+(i+1));if(_points.Length==0){combo.Items.Add("Chưa pick điểm");combo.SelectedIndex=0;}else{_s.AnchorPoint=Math.Max(1,Math.Min(_s.AnchorPoint,_points.Length));combo.SelectedIndex=_s.AnchorPoint-1;}}finally{_loading=wasLoading;}
	}
}
