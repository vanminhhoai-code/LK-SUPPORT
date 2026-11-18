using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using MeshFrame.Core;

namespace MeshFrame.UI;

public sealed class DimLayer
{
	private readonly Control _host;

	private List<DimItem> _items = new List<DimItem>();

	private readonly List<KeyValuePair<Rectangle, DimItem>> _hits = new List<KeyValuePair<Rectangle, DimItem>>();

	private DimItem _selected;

	private TextBox _editor;

	private DimItem _editing;

	public int Anchor;

	public bool Visible = true;

	public Color Back = Color.FromArgb(27, 28, 32);

	public Color TextColor = Color.FromArgb(200, 204, 212);

	public Color Red = Color.FromArgb(240, 80, 70);

	public Color BaseColor = Color.FromArgb(230, 150, 40);

	public Color AnchorColor = Color.FromArgb(74, 163, 255);

	public Color PointColor = Color.FromArgb(214, 90, 90);

	public List<DimItem> Items => _items;

	public event DimEditedHandler Edited;

	public event Action<int> AnchorPicked;

	public DimLayer(Control host)
	{
		_host = host;
	}

	public void SetItems(List<DimItem> items, int anchor)
	{
		_items = items ?? new List<DimItem>();
		Anchor = anchor;
		if (_selected != null)
		{
			DimItem selected = null;
			foreach (DimItem item in _items)
			{
				if (item.Kind == _selected.Kind && item.Index == _selected.Index)
				{
					selected = item;
				}
			}
			_selected = selected;
		}
		CancelEdit();
	}

	public void Draw(Graphics g, Func<V2, PointF> map)
	{
		_hits.Clear();
		if (!Visible)
		{
			return;
		}
		g.SmoothingMode = SmoothingMode.AntiAlias;
		using Font f = new Font(_host.Font.FontFamily, 9f, FontStyle.Bold);
		using Font f2 = new Font(_host.Font.FontFamily, 8.5f);
		using Pen pen = new Pen(Red, 1.6f);
		using Pen pen2 = new Pen(Color.FromArgb(150, Red), 1f)
		{
			DashStyle = DashStyle.Dot
		};
		foreach (DimItem item in _items)
		{
			if (item.Kind != DimKind.Cell)
			{
				continue;
			}
			PointF pointF = map(item.A);
			PointF pointF2 = map(item.B);
			if (item.Extension != null)
			{
				for (int i = 0; i + 1 < item.Extension.Length; i += 2)
				{
					g.DrawLine(pen2, map(item.Extension[i]), map(item.Extension[i + 1]));
				}
			}
			g.DrawLine(pen, pointF, pointF2);
			Arrow(g, pen, pointF, pointF2);
			Arrow(g, pen, pointF2, pointF);
			Label(g, f, item, map(item.TextPos), Red, center: true);
		}
		foreach (DimItem item2 in _items)
		{
			if (item2.Kind == DimKind.Edge)
			{
				Label(g, f2, item2, map(item2.TextPos), item2.IsBaseEdge ? BaseColor : TextColor, center: false);
			}
		}
		foreach (DimItem item3 in _items)
		{
			if (item3.Kind != DimKind.Point)
			{
				continue;
			}
			PointF pointF3 = map(item3.A);
			bool flag = item3.Index == Anchor;
			using (SolidBrush brush = new SolidBrush(flag ? AnchorColor : PointColor))
			{
				g.FillEllipse(brush, pointF3.X - 4f, pointF3.Y - 4f, 8f, 8f);
			}
			if (flag)
			{
				using Pen pen3 = new Pen(AnchorColor, 2f);
				g.DrawEllipse(pen3, pointF3.X - 8f, pointF3.Y - 8f, 16f, 16f);
			}
			_ = item3.Text + (flag ? " (gốc)" : "");
			Label(g, f, item3, new PointF(pointF3.X + 8f, pointF3.Y - 14f), flag ? AnchorColor : PointColor, center: false, leftTop: true);
		}
	}

	private static void Arrow(Graphics g, Pen pen, PointF from, PointF to)
	{
		double num = to.X - from.X;
		double num2 = to.Y - from.Y;
		double num3 = Math.Sqrt(num * num + num2 * num2);
		if (!(num3 < 14.0))
		{
			double num4 = num / num3;
			double num5 = num2 / num3;
			PointF pointF = from;
			PointF pointF2 = new PointF((float)((double)from.X + num4 * 9.0 - num5 * 3.5), (float)((double)from.Y + num5 * 9.0 + num4 * 3.5));
			PointF pointF3 = new PointF((float)((double)from.X + num4 * 9.0 + num5 * 3.5), (float)((double)from.Y + num5 * 9.0 - num4 * 3.5));
			using (SolidBrush brush = new SolidBrush(pen.Color))
			{
				g.FillPolygon(brush, new PointF[3] { pointF, pointF2, pointF3 });
			}
			g.DrawLine(pen, from.X - (float)(num5 * 6.0), from.Y + (float)(num4 * 6.0), from.X + (float)(num5 * 6.0), from.Y - (float)(num4 * 6.0));
		}
	}

	private void Label(Graphics g, Font f, DimItem d, PointF pos, Color col, bool center, bool leftTop = false)
	{
		string text = ((d.Kind == DimKind.Point && d.Index == Anchor) ? (d.Text + " (gốc)") : d.Text);
		SizeF sizeF = g.MeasureString(text, f);
		float val = (leftTop ? pos.X : (pos.X - sizeF.Width / 2f));
		float val2 = (leftTop ? pos.Y : (pos.Y - sizeF.Height / 2f));
		val = Math.Max(2f, Math.Min((float)_host.Width - sizeF.Width - 2f, val));
		val2 = Math.Max(2f, Math.Min((float)_host.Height - sizeF.Height - 2f, val2));
		Rectangle rectangle = new Rectangle((int)val - 2, (int)val2 - 1, (int)sizeF.Width + 4, (int)sizeF.Height + 2);
		bool flag = _selected != null && d.Kind == _selected.Kind && d.Index == _selected.Index;
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(flag ? 235 : 190, Back)))
		{
			g.FillRectangle(brush, rectangle);
		}
		if (flag || d.Editable)
		{
			using Pen pen = new Pen(flag ? AnchorColor : Color.FromArgb(70, col), flag ? 2f : 1f);
			g.DrawRectangle(pen, rectangle);
		}
		using (SolidBrush brush2 = new SolidBrush(col))
		{
			g.DrawString(text, f, brush2, val, val2);
		}
		_hits.Add(new KeyValuePair<Rectangle, DimItem>(rectangle, d));
	}

	public DimItem HitTest(Point p)
	{
		for (int num = _hits.Count - 1; num >= 0; num--)
		{
			if (_hits[num].Key.Contains(p))
			{
				return _hits[num].Value;
			}
		}
		return null;
	}

	public Rectangle RectOf(DimItem d)
	{
		foreach (KeyValuePair<Rectangle, DimItem> hit in _hits)
		{
			if (hit.Value == d)
			{
				return hit.Key;
			}
		}
		return Rectangle.Empty;
	}

	public bool MouseDown(Point p)
	{
		DimItem dimItem = (Visible ? HitTest(p) : null);
		if (dimItem == null)
		{
			if (_selected != null)
			{
				_selected = null;
				_host.Invalidate();
			}
			return false;
		}
		if (dimItem.Kind == DimKind.Point)
		{
			if (AnchorPicked != null)
			{
				AnchorPicked(dimItem.Index);
			}
			return true;
		}
		_selected = dimItem;
		_host.Invalidate();
		return true;
	}

	public bool DoubleClick(Point p)
	{
		DimItem dimItem = (Visible ? HitTest(p) : null);
		if (dimItem == null || !dimItem.Editable)
		{
			return false;
		}
		_selected = dimItem;
		BeginEdit(dimItem);
		return true;
	}

	public bool KeyDown(Keys k)
	{
		if (_selected != null && _editor == null && (k == Keys.F2 || k == Keys.Return) && _selected.Editable)
		{
			BeginEdit(_selected);
			return true;
		}
		return false;
	}

	public void BeginEdit(DimItem d)
	{
		CancelEdit();
		Rectangle rectangle = RectOf(d);
		if (rectangle.IsEmpty)
		{
			return;
		}
		_editing = d;
		_editor = new TextBox
		{
			Text = d.Value.ToString("0.##", CultureInfo.InvariantCulture),
			Bounds = new Rectangle(rectangle.X - 10, rectangle.Y - 2, Math.Max(80, rectangle.Width + 20), rectangle.Height + 6),
			TextAlign = HorizontalAlignment.Center
		};
		_editor.BackColor = Color.FromArgb(44, 47, 53);
		_editor.ForeColor = Color.White;
		_editor.BorderStyle = BorderStyle.FixedSingle;
		_editor.KeyDown += (object s, KeyEventArgs e) =>
		{
			if (e.KeyCode == Keys.Return)
			{
				e.SuppressKeyPress = true;
				Commit();
			}
			else if (e.KeyCode == Keys.Escape)
			{
				e.SuppressKeyPress = true;
				CancelEdit();
			}
		};
		_editor.LostFocus += delegate
		{
			if (_editor != null)
			{
				CancelEdit();
			}
		};
		_host.Controls.Add(_editor);
		_editor.Focus();
		_editor.SelectAll();
	}

	private void Commit()
	{
		if (_editor != null)
		{
			string s = _editor.Text.Trim().Replace(',', '.');
			DimItem editing = _editing;
			bool flag = double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result);
			CancelEdit();
			if (flag && editing != null && Edited != null)
			{
				Edited(editing, result);
			}
		}
	}

	public void CancelEdit()
	{
		if (_editor != null)
		{
			TextBox editor = _editor;
			_editor = null;
			_editing = null;
			if (_host.Controls.Contains(editor))
			{
				_host.Controls.Remove(editor);
			}
			editor.Dispose();
			_host.Focus();
		}
	}
}
