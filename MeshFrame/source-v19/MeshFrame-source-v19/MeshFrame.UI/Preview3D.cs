using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MeshFrame.Core;

namespace MeshFrame.UI;

public class Preview3D : Control
{
	private struct Pt
	{
		public double X;

		public double Y;

		public double D;
	}

	private Model3D _model = new Model3D();

	private MeshFramePlan _plan;

	private string _empty = "Chưa có điểm. Bấm \"Pick lại điểm\" để pick đa giác trong Tekla.";

	public readonly ViewCamera Camera = new ViewCamera();
	private Frame _worldFrame;
	public event Action TeklaViewRequested;

	public void SetWorldFrame(Frame frame) {
        V3 right=ViewCamera.World(_worldFrame,Camera.Right),up=ViewCamera.World(_worldFrame,Camera.Up);
        _worldFrame=frame;Camera.Set(ViewCamera.Local(frame,right),ViewCamera.Local(frame,up));
    }
    public void SetWorldView(ViewOrientation view) {
        Camera.Set(ViewCamera.Local(_worldFrame,view.Right),ViewCamera.Local(_worldFrame,view.Up));
        FitView();Invalidate();
    }

	private double _scale = 1.0;

	private double _panX;

	private double _panY;

	private double _cx;

	private double _cy;

	private double _cz;

	private bool _fitted;
	private bool _autoFit = true;
	protected override void OnResize(EventArgs e) { base.OnResize(e); if(_autoFit && _model!=null)FitView(); }

	private Bitmap _bmp;

	private int[] _pix;

	private float[] _zb;

	private int _w;

	private int _h;

	private Point _last;

	private bool _pan;

	private bool _rot;

	private readonly List<KeyValuePair<Rectangle, string>> _buttons = new List<KeyValuePair<Rectangle, string>>();

	public readonly DimLayer Dims;

	public Color BackgroundTop = Color.FromArgb(27, 28, 32);

	public Color BackgroundBottom = Color.FromArgb(42, 45, 51);

	public Color TextColor = Color.FromArgb(200, 204, 212);

	public Color FrameColor = Color.FromArgb(86, 156, 214);

	public Color StiffColor = Color.FromArgb(78, 201, 176);

	public Color StripColor = Color.FromArgb(230, 170, 60);

	public Color MeshColor = Color.FromArgb(120, 130, 150);

	public Preview3D()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = BackgroundTop;
		TabStop = true;
		Dims = new DimLayer(this);
        SetView(-35,48);
	}

	public void SetDims(List<DimItem> items, int anchor)
	{
		Dims.SetItems(items, anchor);
		Invalidate();
	}

	public void Show(MeshFramePlan plan, V2[] poly)
	{
		bool flag = _model.Prisms.Count == 0;
		_plan = plan;
		_model = ((plan == null) ? new Model3D() : Model3D.Build(plan));
		if (_model.Prisms.Count == 0)
		{
			Invalidate();
			return;
		}
		ComputeCenter();
		if (!_fitted | flag)
		{
			FitView();
			_fitted = true;
		}
		Invalidate();
	}

	public void ShowEmpty(string text)
	{
		_plan = null;
		_model = new Model3D();
		if (text != null)
		{
			_empty = text;
		}
		_fitted = false;
		Invalidate();
	}

	private void ComputeCenter()
	{
		double num = double.MaxValue;
		double num2 = double.MaxValue;
		double num3 = double.MaxValue;
		double num4 = double.MinValue;
		double num5 = double.MinValue;
		double num6 = double.MinValue;
		foreach (Prism prism in _model.Prisms)
		{
			V2[] poly = prism.Poly;
			for (int i = 0; i < poly.Length; i++)
			{
				V2 v = poly[i];
				num = Math.Min(num, v.X);
				num4 = Math.Max(num4, v.X);
				num2 = Math.Min(num2, v.Y);
				num5 = Math.Max(num5, v.Y);
			}
			num3 = Math.Min(num3, prism.Z0);
			num6 = Math.Max(num6, prism.Z1);
		}
		foreach (DimItem item in Dims.Items)
		{
			V2[] poly = new V2[3] { item.A, item.B, item.TextPos };
			for (int i = 0; i < poly.Length; i++)
			{
				V2 v2 = poly[i];
				num = Math.Min(num, v2.X);
				num4 = Math.Max(num4, v2.X);
				num2 = Math.Min(num2, v2.Y);
				num5 = Math.Max(num5, v2.Y);
			}
		}
		_cx = (num + num4) / 2.0;
		_cy = (num2 + num5) / 2.0;
		_cz = (num3 + num6) / 2.0;
	}

	public void FitView()
	{
		_autoFit = true;
		if (_model.Prisms.Count == 0 || Width < 10 || Height < 10)
		{
			return;
		}
		_panX = (_panY = 0.0);
		_scale = 1.0;
		double num = double.MaxValue;
		double num2 = double.MinValue;
		double num3 = double.MaxValue;
		double num4 = double.MinValue;
		foreach (Prism prism in _model.Prisms)
		{
			V2[] poly = prism.Poly;
			for (int i = 0; i < poly.Length; i++)
			{
				V2 v = poly[i];
				double[] array = new double[2] { prism.Z0, prism.Z1 };
				foreach (double z in array)
				{
					Project(v.X, v.Y, z, out var sx, out var sy, out var _);
					num = Math.Min(num, sx);
					num2 = Math.Max(num2, sx);
					num3 = Math.Min(num3, sy);
					num4 = Math.Max(num4, sy);
				}
			}
		}
		foreach (DimItem item in Dims.Items)
		{
			V2[] poly = new V2[2] { item.A, item.B };
			for (int i = 0; i < poly.Length; i++)
			{
				V2 v2 = poly[i];
				Project(v2.X, v2.Y, 0.0, out var sx2, out var sy2, out var _);
				num = Math.Min(num, sx2);
				num2 = Math.Max(num2, sx2);
				num3 = Math.Min(num3, sy2);
				num4 = Math.Max(num4, sy2);
			}
		}
		double num5 = Math.Max(num2 - num, 1E-06);
		double num6 = Math.Max(num4 - num3, 1E-06);
		_scale = 0.88 * Math.Min((double)Width / num5, (double)(Height - 20) / num6);
		double num7 = (num + num2) / 2.0;
		double num8 = (num3 + num4) / 2.0;
		_panX = (0.0 - (num7 - (double)Width / 2.0)) * _scale;
		_panY = (0.0 - (num8 - (double)Height / 2.0)) * _scale;
	}

	public void SetView(double yaw, double pitch)
	{
		double a=yaw*Math.PI/180,b=pitch*Math.PI/180;
        Camera.Set(new V3(Math.Cos(a),-Math.Sin(a),0),new V3(Math.Sin(a)*Math.Cos(b),Math.Cos(a)*Math.Cos(b),Math.Sin(b)));
		FitView();
		Invalidate();
	}

	private void Project(double x, double y, double z, out double sx, out double sy, out double d)
	{
        V3 v=new V3(x-_cx,y-_cy,z-_cz);
        sx=Width/2.0+_panX+_scale*v.Dot(Camera.Right);
        sy=Height/2.0+_panY-_scale*v.Dot(Camera.Up);
        d=v.Dot(Camera.Depth);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		base.OnMouseEnter(e);
		if (!Focused && FindForm() != null && !(FindForm().ActiveControl is Preview3D) && !(FindForm().ActiveControl is TextBoxBase))
		{
			Focus();
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		_last = e.Location;
		if (e.Button == MouseButtons.Left && Dims.MouseDown(e.Location))
		{
			return;
		}
		if (e.Button == MouseButtons.Middle)
		{
			if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
			{
				_rot = true;
				_pan = false;
			}
			else
			{
				_pan = true;
				_rot = false;
			}
			Capture=true;
            Cursor = (_rot ? Cursors.SizeAll : Cursors.Hand);
		}
		else
		{
			if (e.Button != MouseButtons.Left)
			{
				return;
			}
			foreach (KeyValuePair<Rectangle, string> button in _buttons)
			{
				if (button.Key.Contains(e.Location))
				{
					RunButton(button.Value);
					break;
				}
			}
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (e.Button == MouseButtons.Middle)
		{
			_pan = (_rot = false);
            Capture=false;
			Cursor = Cursors.Default;
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		int num = e.X - _last.X;
		int num2 = e.Y - _last.Y;
		_last = e.Location;
		if (_rot)
		{
			_autoFit = false;
			Camera.Orbit(num,num2,ViewCamera.Local(_worldFrame,new V3(0,0,1)));
			Invalidate();
		}
		else if (_pan)
		{
			_autoFit = false;
			_panX += num;
			_panY += num2;
			Invalidate();
		}
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		base.OnMouseWheel(e);
		ZoomAt(e.Location, Math.Pow(1.2, (double)e.Delta / 120.0));
	}

	public void ZoomAt(Point at, double k)
	{
		_autoFit = false;
		if (_model.Prisms.Count != 0)
		{
			var anon = new { at.X, at.Y };
			double num = Math.Max(0.0001, Math.Min(10000.0, _scale * k));
			k = num / _scale;
			double num2 = (double)anon.X - (double)Width / 2.0 - _panX;
			double num3 = (double)anon.Y - (double)Height / 2.0 - _panY;
			_panX = (double)anon.X - (double)Width / 2.0 - num2 * k;
			_panY = (double)anon.Y - (double)Height / 2.0 - num3 * k;
			_scale = num;
			Invalidate();
		}
	}

	private void RunButton(string id)
	{
		switch (id)
		{
		case "Tekla":
            TeklaViewRequested?.Invoke();
            break;
        case "3D":
			SetView(-35.0, 48.0);
			break;
		case "Top":
			SetView(0.0, 0.0);
			break;
		case "Under":
			SetView(0.0, 180.0);
			break;
		case "Fit":
			FitView();
			Invalidate();
			break;
		case "Num":
			Dims.Visible = !Dims.Visible;
			Invalidate();
			break;
		}
	}

	protected override void OnDoubleClick(EventArgs e)
	{
		base.OnDoubleClick(e);
		Dims.DoubleClick(PointToClient(Cursor.Position));
	}

	protected override bool IsInputKey(Keys k)
	{
		if (k != Keys.Home)
		{
			return base.IsInputKey(k);
		}
		return true;
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (!Dims.KeyDown(e.KeyCode) && e.KeyCode == Keys.Home)
		{
			FitView(); Invalidate();
		}
	}

    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e);if(!Capture){_rot=_pan=false;Cursor=Cursors.Default;} }

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		using (LinearGradientBrush brush = new LinearGradientBrush(ClientRectangle, BackgroundTop, BackgroundBottom, 90f))
		{
			graphics.FillRectangle(brush, ClientRectangle);
		}
		if (_model.Prisms.Count == 0)
		{
			using (SolidBrush brush2 = new SolidBrush(TextColor))
			{
				graphics.DrawString(_empty, Font, brush2, new RectangleF(16f, 16f, Width - 32, Height - 32));
				return;
			}
		}
		if (Width >= 8 && Height >= 8)
		{
			Render();
			graphics.DrawImageUnscaled(_bmp, 0, 0);
			Dims.Back = BackgroundTop;
			Dims.TextColor = TextColor;
			Dims.Draw(graphics, (V2 v) =>
			{
				Pt pt = P3(v.X, v.Y, 0.0);
				return new PointF((float)pt.X, (float)pt.Y);
			});
			DrawOverlay(graphics);
		}
	}

	private void Render()
	{
		if (_bmp == null || _w != Width || _h != Height)
		{
			if (_bmp != null)
			{
				_bmp.Dispose();
			}
			_w = Width;
			_h = Height;
			_bmp = new Bitmap(_w, _h, PixelFormat.Format32bppRgb);
			_pix = new int[_w * _h];
			_zb = new float[_w * _h];
		}
		for (int i = 0; i < _h; i++)
		{
			double t = ((_h <= 1) ? 0.0 : ((double)i / (double)(_h - 1)));
			int num = Lerp(BackgroundTop, BackgroundBottom, t).ToArgb();
			int num2 = i * _w;
			for (int j = 0; j < _w; j++)
			{
				_pix[num2 + j] = num;
				_zb[num2 + j] = float.NegativeInfinity;
			}
		}
		foreach (Prism prism in _model.Prisms)
		{
			if(prism.Kind!=PartKind.Mesh || _plan.Mesh.Kind==MeshKind.Plate)DrawPrism(prism);
		}
		if (_model.MeshPoly != null && _plan.Mesh.Kind==MeshKind.RebarMesh)
		{
			DrawMeshGrid();
		}
		BitmapData bitmapData = _bmp.LockBits(new Rectangle(0, 0, _w, _h), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
		Marshal.Copy(_pix, 0, bitmapData.Scan0, _pix.Length);
		_bmp.UnlockBits(bitmapData);
	}

	private static Color Lerp(Color a, Color b, double t)
	{
		return Color.FromArgb((int)((double)(int)a.R + (double)(b.R - a.R) * t), (int)((double)(int)a.G + (double)(b.G - a.G) * t), (int)((double)(int)a.B + (double)(b.B - a.B) * t));
	}

	private Color BaseColor(PartKind k)
	{
		return k switch
		{
			PartKind.Strip => StripColor, 
			PartKind.Stiffener => StiffColor, 
			PartKind.FrameBar => FrameColor, 
			_ => MeshColor, 
		};
	}

	private Pt P3(double x, double y, double z)
	{
		Pt result = default;
		Project(x, y, z, out result.X, out result.Y, out result.D);
		return result;
	}

	private void DrawPrism(Prism pr)
	{
		Color color = BaseColor(pr.Kind);
		int num = pr.Poly.Length;
		Pt[] array = new Pt[num];
		Pt[] array2 = new Pt[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = P3(pr.Poly[i].X, pr.Poly[i].Y, pr.Z0);
			array2[i] = P3(pr.Poly[i].X, pr.Poly[i].Y, pr.Z1);
		}
		foreach (int[] item in Model3D.Triangulate(pr.Poly))
		{
			Fill(array2[item[0]], array2[item[1]], array2[item[2]], Shade(color, 0.0, 0.0, 1.0));
			Fill(array[item[0]], array[item[2]], array[item[1]], Shade(color, 0.0, 0.0, -1.0));
		}
		for (int j = 0; j < num; j++)
		{
			int num2 = (j + 1) % num;
			V2 v = pr.Poly[num2] - pr.Poly[j];
			V2 v2 = new V2(v.Y, 0.0 - v.X).Unit();
			Color col = Shade(color, v2.X, v2.Y, 0.0);
			Fill(array[j], array[num2], array2[num2], col);
			Fill(array[j], array2[num2], array2[j], col);
		}
		Color col2 = Lerp(color, Color.White, 0.4);
		double bias = 1.8 / _scale;
		for (int k = 0; k < num; k++)
		{
			int num3 = (k + 1) % num;
			Line(array2[k], array2[num3], col2, bias);
			Line(array[k], array[num3], col2, bias);
			Line(array[k], array2[k], col2, bias);
		}
	}

	private Color Shade(Color c, double nx, double ny, double nz)
	{
        V3 normal=new V3(nx,ny,nz);
        double num3=normal.Dot(Camera.Right),num5=normal.Dot(Camera.Up),num6=normal.Dot(Camera.Depth);
		double num7 = -0.35;
		double num8 = 0.45;
		double num9 = 0.82;
		double num10 = Math.Sqrt(num7 * num7 + num8 * num8 + num9 * num9);
		double num11 = Math.Abs((num3 * num7 + num5 * num8 + num6 * num9) / num10);
		double num12 = 0.38 + 0.62 * num11;
		return Color.FromArgb(Clamp((double)(int)c.R * num12), Clamp((double)(int)c.G * num12), Clamp((double)(int)c.B * num12));
	}

	private static int Clamp(double v)
	{
		if (!(v < 0.0))
		{
			if (!(v > 255.0))
			{
				return (int)v;
			}
			return 255;
		}
		return 0;
	}

	private void Fill(Pt a, Pt b, Pt c, Color col)
	{
		int num = (int)Math.Max(0.0, Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))));
		int num2 = (int)Math.Min(_w - 1, Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
		int num3 = (int)Math.Max(0.0, Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
		int num4 = (int)Math.Min(_h - 1, Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
		if (num > num2 || num3 > num4)
		{
			return;
		}
		double num5 = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
		if (Math.Abs(num5) < 1E-12)
		{
			return;
		}
		int num6 = col.ToArgb();
		for (int i = num3; i <= num4; i++)
		{
			double num7 = (double)i + 0.5;
			for (int j = num; j <= num2; j++)
			{
				double num8 = (double)j + 0.5;
				double num9 = ((b.Y - c.Y) * (num8 - c.X) + (c.X - b.X) * (num7 - c.Y)) / num5;
				double num10 = ((c.Y - a.Y) * (num8 - c.X) + (a.X - c.X) * (num7 - c.Y)) / num5;
				double num11 = 1.0 - num9 - num10;
				if (!(num9 < -1E-09) && !(num10 < -1E-09) && !(num11 < -1E-09))
				{
					float num12 = (float)(num9 * a.D + num10 * b.D + num11 * c.D);
					int num13 = i * _w + j;
					if (num12 > _zb[num13])
					{
						_zb[num13] = num12;
						_pix[num13] = num6;
					}
				}
			}
		}
	}

	private void Line(Pt a, Pt b, Color col, double bias)
	{
		double num = b.X - a.X;
		double num2 = b.Y - a.Y;
		int num3 = (int)Math.Ceiling(Math.Max(Math.Abs(num), Math.Abs(num2)));
		if (num3 <= 0 || num3 > 20000)
		{
			return;
		}
		int num4 = col.ToArgb();
		for (int i = 0; i <= num3; i++)
		{
			double num5 = (double)i / (double)num3;
			int num6 = (int)Math.Round(a.X + num * num5);
			int num7 = (int)Math.Round(a.Y + num2 * num5);
			if (num6 >= 0 && num7 >= 0 && num6 < _w && num7 < _h)
			{
				float num8 = (float)(a.D + (b.D - a.D) * num5);
				int num9 = num7 * _w + num6;
				if ((double)num8 >= (double)_zb[num9] - bias)
				{
					_zb[num9] = Math.Max(_zb[num9], num8);
					_pix[num9] = num4;
				}
			}
		}
	}

    private static double Coverage(double coordinate,double pitch,double diameter,double footprint) {
        double half=Math.Min(pitch,diameter)/2;
        if(footprint>=pitch*3)return Math.Min(1,diameter/pitch);
        double lo=coordinate-footprint/2,hi=coordinate+footprint/2;
        double area=0;
        for(double i=Math.Floor((lo-half)/pitch);i<=Math.Ceiling((hi+half)/pitch);i++)
            area+=Math.Max(0,Math.Min(hi,i*pitch+half)-Math.Max(lo,i*pitch-half));
        return Math.Min(1,area/Math.Max(footprint,1e-9));
    }
    private void DrawMeshGrid() {
        double pitch=_plan.Mesh.Pitch,dia=_plan.Mesh.WireDia;
        if(pitch<=0||dia<=0)return;
        V2[] polygon=_plan.Mesh.Polygon;V2 origin=polygon[0],u=(polygon[1]-origin).Unit(),v=u.Left();
        for(int layer=0;layer<2;layer++) {
            double z=-dia*(layer+.5);
            if(!Unproject(0,0,z,out V2 p0)||!Unproject(1,0,z,out V2 px)||!Unproject(0,1,z,out V2 py))continue;
            V2 axis=layer==0?u:v,dx=px-p0,dy=py-p0;
            double footprint=Math.Max(1e-8,Math.Abs(dx.Dot(axis))+Math.Abs(dy.Dot(axis)));
            var screen=new List<V2>();foreach(V2 p in polygon){Pt q=P3(p.X,p.Y,z);screen.Add(new V2(q.X,q.Y));}
            V2[] screenPoly=screen.ToArray();
            for(int y=0;y<_h;y++)foreach(Geo.LineSeg segment in Geo.ClipLine(screenPoly,new V2(0,y+.5),new V2(1,0))) {
                int first=(int)Math.Max(0,Math.Ceiling(segment.T0-.5)),last=(int)Math.Min(_w-1,Math.Floor(segment.T1-.5));
                for(int x=first;x<=last;x++) {
                    V2 local=p0+dx*(x+.5)+dy*(y+.5);
                    double coverage=Coverage((local-origin).Dot(axis),pitch,dia,footprint);
                    if(coverage<.001)continue;
                    Pt point=P3(local.X,local.Y,z);int index=y*_w+x;
                    if(point.D<_zb[index]-1e-5)continue;
                    _pix[index]=Lerp(Color.FromArgb(_pix[index]),MeshColor,coverage).ToArgb();
                }
            }
        }
    }

	private bool Unproject(double sx, double sy, double zPlane, out V2 w)
	{
        double x=(sx-Width/2.0-_panX)/_scale-(zPlane-_cz)*Camera.Right.Z;
        double y=-(sy-Height/2.0-_panY)/_scale-(zPlane-_cz)*Camera.Up.Z;
        double det=Camera.Right.X*Camera.Up.Y-Camera.Right.Y*Camera.Up.X;
        w=new V2(0,0);if(Math.Abs(det)<1e-8)return false;
        w=new V2(_cx+(x*Camera.Up.Y-Camera.Right.Y*y)/det,_cy+(Camera.Right.X*y-x*Camera.Up.X)/det);return true;
	}

	private void DrawOverlay(Graphics g)
	{
		_buttons.Clear();
		string[][] array = new string[6][]
		{
			new string[2] { "Tekla", "Theo Tekla" },
            new string[2] { "3D", "3D" },
			new string[2] { "Top", "Trên" },
			new string[2] { "Under", "Dưới" },
			new string[2] { "Fit", "Vừa khung" },
			new string[2]
			{
				"Num",
				Dims.Visible ? "Số: bật" : "Số: tắt"
			}
		};
		int num = Width - 8;
		using Font font = new Font(Font.FontFamily, 8.5f);
		using SolidBrush brush = new SolidBrush(Color.FromArgb(200, 42, 45, 51));
		using Pen pen = new Pen(Color.FromArgb(90, 100, 110));
		using SolidBrush brush2 = new SolidBrush(TextColor);
		for (int num2 = array.Length - 1; num2 >= 0; num2--)
		{
			SizeF sizeF = g.MeasureString(array[num2][1], font);
			Rectangle rectangle = new Rectangle((int)((float)num - sizeF.Width - 14f), 8, (int)sizeF.Width + 14, 22);
			g.FillRectangle(brush, rectangle);
			g.DrawRectangle(pen, rectangle);
			g.DrawString(array[num2][1], font, brush2, rectangle.X + 7, rectangle.Y + 3);
			_buttons.Add(new KeyValuePair<Rectangle, string>(rectangle, array[num2][0]));
			num = rectangle.X - 6;
		}
		g.DrawString("Cuộn: zoom · Giữ chuột giữa: kéo · Ctrl + chuột giữa: xoay · Bấm đúp số: sửa · Bấm P#: chọn điểm gốc", font, brush2, 8f, Height - 20);
		DrawAxes(g, font);
	}

	private void DrawAxes(Graphics g, Font f)
	{

		Point point = new Point(44, Height - 52);
		var array = new[]
		{
			new
			{
				n = "X",
				v = new double[3] { 1.0, 0.0, 0.0 },
				c = Color.FromArgb(230, 90, 90)
			},
			new
			{
				n = "Y",
				v = new double[3] { 0.0, 1.0, 0.0 },
				c = Color.FromArgb(110, 200, 110)
			},
			new
			{
				n = "Z",
				v = new double[3] { 0.0, 0.0, 1.0 },
				c = Color.FromArgb(100, 160, 240)
			}
		};
		foreach (var anon in array)
		{
            V3 axis=ViewCamera.Local(_worldFrame,new V3(anon.v[0],anon.v[1],anon.v[2]));
            double num3=axis.Dot(Camera.Right),num4=axis.Dot(Camera.Up);
			PointF pt = new PointF((float)((double)point.X + 26.0 * num3), (float)((double)point.Y - 26.0 * num4));
			using (Pen pen = new Pen(anon.c, 2f))
			{
				g.DrawLine(pen, point, pt);
			}
			using SolidBrush brush = new SolidBrush(anon.c);
			g.DrawString(anon.n, f, brush, pt.X + 2f, pt.Y - 7f);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && _bmp != null)
		{
			_bmp.Dispose();
			_bmp = null;
		}
		base.Dispose(disposing);
	}
}
