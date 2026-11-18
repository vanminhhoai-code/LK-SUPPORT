using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using MeshFrame.Core;

namespace MeshFrame.UI;

public class PreviewControl : Control
{
	private MeshFramePlan _plan;

	private V2[] _poly;

	private double _legA;
    private double _zoom=1,_panX,_panY,_centerX,_centerY;
    private bool _fitted,_panning,_autoFit=true;private Point _last;
    protected override void OnResize(EventArgs e){base.OnResize(e);if(_autoFit)_fitted=false;}
    public PointF Project(V2 p)=>new PointF((float)(Width/2.0+_panX+(p.X-_centerX)*_zoom),(float)(Height/2.0+_panY-(p.Y-_centerY)*_zoom));
    public void FitView(){_autoFit=true;_fitted=false;Invalidate();}
    public void ZoomAt(Point p,double factor){_autoFit=false;double next=Math.Max(.0001,Math.Min(10000,_zoom*factor));factor=next/_zoom;_panX=p.X-Width/2.0-(p.X-Width/2.0-_panX)*factor;_panY=p.Y-Height/2.0-(p.Y-Height/2.0-_panY)*factor;_zoom=next;Invalidate();}
    protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);ZoomAt(e.Location,Math.Pow(1.2,e.Delta/120.0));}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(_panning){_autoFit=false;_panX+=e.X-_last.X;_panY+=e.Y-_last.Y;Invalidate();}_last=e.Location;}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button==MouseButtons.Middle){_panning=false;Capture=false;Cursor=Cursors.Default;}}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){_panning=false;Cursor=Cursors.Default;}}


	public readonly DimLayer Dims;

	private string _empty = "Chưa có điểm. Bấm \"Pick lại\" để pick đa giác trong Tekla.";

	public PreviewControl()
	{
		DoubleBuffered = true;TabStop=true;SetStyle(ControlStyles.Selectable,true);
		Dims = new DimLayer(this);
		BackColor = Color.FromArgb(27, 28, 32);
	}

	public void Show(MeshFramePlan plan, V2[] poly, double frameLegA)
	{
		if(_poly==null)_fitted=false;
        _plan = plan;
		_poly = ((plan != null && plan.Polygon != null) ? plan.Polygon : poly);
		_legA = frameLegA;
		Invalidate();
	}

	public void SetDims(List<DimItem> items, int anchor)
	{
		Dims.SetItems(items, anchor);
		Invalidate();
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
        Focus();if(e.Button==MouseButtons.Middle){_last=e.Location;_panning=true;Capture=true;Cursor=Cursors.Hand;return;}
        if(e.Button==MouseButtons.Left&&e.X>Width-90&&e.Y<30){FitView();return;}
		if (e.Button == MouseButtons.Left)
		{
			Focus();
			Dims.MouseDown(e.Location);
		}
	}

	protected override void OnDoubleClick(EventArgs e)
	{
		base.OnDoubleClick(e);
		Dims.DoubleClick(PointToClient(Cursor.Position));
	}

	protected override bool IsInputKey(Keys k)
	{
		if (k != Keys.F2 && k != Keys.Return && k != Keys.Home)
		{
			return base.IsInputKey(k);
		}
		return true;
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if(e.KeyCode==Keys.Home)FitView();else Dims.KeyDown(e.KeyCode);
	}

	public void ShowEmpty(string text)
	{
		_plan = null;
		_poly = null;_fitted=false;
		if (text != null)
		{
			_empty = text;
		}
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		if (_poly == null || _poly.Length < 3)
		{
			using (Font font = new Font("Segoe UI", 10f))
			{
				graphics.DrawString(_empty, font, Brushes.Silver, 20f, 20f);
				return;
			}
		}
		double minX = double.MaxValue;
		double minY = double.MaxValue;
		double num = double.MinValue;
		double num2 = double.MinValue;
		V2[] poly = _poly;
		for (int i = 0; i < poly.Length; i++)
		{
			V2 v = poly[i];
			minX = Math.Min(minX, v.X);
			num = Math.Max(num, v.X);
			minY = Math.Min(minY, v.Y);
			num2 = Math.Max(num2, v.Y);
		}
		foreach (DimItem item in Dims.Items)
		{
			poly = new V2[2] { item.A, item.B };
			for (int i = 0; i < poly.Length; i++)
			{
				V2 v2 = poly[i];
				minX = Math.Min(minX, v2.X);
				num = Math.Max(num, v2.X);
				minY = Math.Min(minY, v2.Y);
				num2 = Math.Max(num2, v2.Y);
			}
		}
		double num3 = num - minX;
		double num4 = num2 - minY;
		if (num3 < 1.0 || num4 < 1.0)
		{
			return;
		}
        if(!_fitted){_zoom=Math.Max(.0001,Math.Min(Math.Max(10,Width-120)/num3,Math.Max(10,Height-120)/num4));_centerX=(minX+num)/2;_centerY=(minY+num2)/2;_panX=_panY=0;_fitted=true;}
        Func<V2,PointF> T=Project;

		Func<V2[], PointF[]> func = (V2[] a) =>
		{
			PointF[] array = new PointF[a.Length];
			for (int j = 0; j < a.Length; j++)
			{
				array[j] = T(a[j]);
			}
			return array;
		};
		if (_plan == null)
		{
			using (Pen pen = new Pen(Color.Firebrick, 1.5f)
			{
				DashStyle = DashStyle.Dash
			})
			{
				graphics.DrawPolygon(pen, func(_poly));
			}
			DrawLabels(graphics, T);
			return;
		}
        if(_plan.Mesh!=null)MeshPainter.Draw(graphics,_plan.Mesh,T,_zoom);

		V2[] arg = Geo.Offset(_poly, _legA);
		using (GraphicsPath graphicsPath = new GraphicsPath(FillMode.Alternate))
		{
			graphicsPath.AddPolygon(func(_poly));
			graphicsPath.AddPolygon(func(arg));
			using SolidBrush brush3 = new SolidBrush(Color.FromArgb(235, 230, 126, 34));
			graphics.FillPath(brush3, graphicsPath);
		}
		foreach (PlanBar bar in _plan.Bars)
		{
			if (bar.Role == BarRole.Stiffener)
			{
				FillBand(graphics, T, bar.Start, bar.End, 0.0, bar.LegA, Color.FromArgb(235, 214, 100, 20));
			}
		}
		foreach (PlanStrip strip in _plan.Strips)
		{
			FillBand(graphics, T, strip.Start, strip.End, (0.0 - strip.Width) / 2.0, strip.Width / 2.0, Color.FromArgb(200, 39, 174, 96));
		}
		using (Pen pen2 = new Pen(Color.FromArgb(150, 158, 170), 1.2f))
		{
			graphics.DrawPolygon(pen2, func(_poly));
		}
		if (Dims.Items.Count > 0)
		{
			Dims.Back = BackColor;
			Dims.Draw(graphics, T);
		}
		else
		{
			DrawLabels(graphics, T);
		}
        DrawNavigation(graphics);
	}

    protected override void OnPaintBackground(PaintEventArgs e){base.OnPaintBackground(e);}
    private void DrawNavigation(Graphics g){using var brush=new SolidBrush(Color.Silver);using var back=new SolidBrush(BackColor);g.FillRectangle(back,Width-90,0,90,28);g.DrawString("Vừa khung",Font,brush,Width-85,7);g.DrawString("Cuộn: zoom · Chuột giữa: kéo · Home: vừa khung",Font,brush,8,Height-20);}

	private void DrawLabels(Graphics g, Func<V2, PointF> T)
	{
		int num = _poly.Length;
		int num2 = ((_plan != null) ? _plan.BaseEdge : (-1));
		using Font font = new Font("Segoe UI", 9f, FontStyle.Bold);
		using Font font2 = new Font("Segoe UI", 8f);
		for (int i = 0; i < num; i++)
		{
			PointF pointF = T(_poly[i]);
			g.FillEllipse(Brushes.Firebrick, pointF.X - 3f, pointF.Y - 3f, 6f, 6f);
			g.DrawString("P" + (i + 1), font, Brushes.Firebrick, pointF.X + 4f, pointF.Y - 18f);
		}
		for (int j = 0; j < num; j++)
		{
			V2 v = _poly[j];
			V2 v2 = _poly[(j + 1) % num];
			PointF pointF2 = T(new V2((v.X + v2.X) / 2.0, (v.Y + v2.Y) / 2.0));
			V2 v3 = (v2 - v).Unit().Left() * -1.0;
			string s = j + 1 + ": " + (v2 - v).Length.ToString("0", CultureInfo.InvariantCulture);
			SizeF sizeF = g.MeasureString(s, font2);
			float num3 = (float)v3.X * (sizeF.Width / 2f + 6f);
			float num4 = (0f - (float)v3.Y) * (sizeF.Height / 2f + 4f);
			float num5 = Math.Max(2f, Math.Min((float)Width - sizeF.Width - 2f, pointF2.X + num3 - sizeF.Width / 2f));
			float num6 = Math.Max(2f, Math.Min((float)Height - sizeF.Height - 2f, pointF2.Y + num4 - sizeF.Height / 2f));
			g.DrawString(s, font2, (j == num2) ? Brushes.DarkOrange : Brushes.Silver, num5, num6);
		}
	}

	private static void FillBand(Graphics g, Func<V2, PointF> T, V2 a, V2 b, double d0, double d1, Color col)
	{
		V2 v = (b - a).Unit().Left();
		PointF[] points = new PointF[4]
		{
			T(a + v * d0),
			T(b + v * d0),
			T(b + v * d1),
			T(a + v * d1)
		};
		using SolidBrush brush = new SolidBrush(col);
		g.FillPolygon(brush, points);
	}
}
