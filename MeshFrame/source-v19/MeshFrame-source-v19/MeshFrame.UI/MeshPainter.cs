using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using MeshFrame.Core;
namespace MeshFrame.UI;

public static class MeshPainter
{
    public static void Draw(Graphics g,PlanMesh mesh,Func<V2,PointF> project,double scale) {
        if(mesh.Kind==MeshKind.None)return;
        PointF[] points=Array.ConvertAll(mesh.Polygon,p=>project(p));
        if(mesh.Kind==MeshKind.Plate){using var plate=new SolidBrush(Color.FromArgb(120,130,150));g.FillPolygon(plate,points);return;}
        double pitch=mesh.Pitch,dia=mesh.WireDia;if(pitch<=0||dia<=0)return;
        // Subpixel wires converge to their actual covered fraction, rather than a fake hatch.
        if(pitch*scale<3){double c=1-Math.Pow(1-Math.Min(1,dia/pitch),2);using var coverage=new SolidBrush(Color.FromArgb((int)(255*c),120,130,150));g.FillPolygon(coverage,points);return;}
        var state=g.Save();using var path=new GraphicsPath();path.AddPolygon(points);g.SetClip(path,CombineMode.Intersect);
        V2 origin=mesh.Polygon[0],u=(mesh.Polygon[1]-origin).Unit(),v=u.Left();
        using var pen=new Pen(Color.FromArgb(180,190,205),(float)Math.Max(.1,dia*scale));
        for(int direction=0;direction<2;direction++){
            V2 across=direction==0?u:v,along=direction==0?v:u;
            double lo=double.MaxValue,hi=double.MinValue;
            foreach(V2 p in mesh.Polygon){double t=(p-origin).Dot(across);lo=Math.Min(lo,t);hi=Math.Max(hi,t);}
            PointF zero=project(new V2(0,0));RectangleF visible=g.VisibleClipBounds;
            double viewLo=double.MaxValue,viewHi=double.MinValue;
            foreach(PointF corner in new[]{new PointF(visible.Left,visible.Top),new PointF(visible.Right,visible.Top),new PointF(visible.Left,visible.Bottom),new PointF(visible.Right,visible.Bottom)}){
                V2 local=new V2((corner.X-zero.X)/scale,-(corner.Y-zero.Y)/scale);
                double t=(local-origin).Dot(across);viewLo=Math.Min(viewLo,t);viewHi=Math.Max(viewHi,t);
            }
            lo=Math.Max(lo,viewLo-dia);hi=Math.Min(hi,viewHi+dia);
            for(double i=Math.Ceiling(lo/pitch);i<=Math.Floor(hi/pitch);i++){
                V2 p=origin+across*(i*pitch);
                foreach(var seg in Geo.ClipLine(mesh.Polygon,p,along))g.DrawLine(pen,project(p+along*seg.T0),project(p+along*seg.T1));
            }
        }g.Restore(state);
    }
}
