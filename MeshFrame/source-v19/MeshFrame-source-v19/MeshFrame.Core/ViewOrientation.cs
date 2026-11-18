using System;
namespace MeshFrame.Core;

public sealed class ViewOrientation
{
    public V3 Right, Up;
    public string Name;
}

// Camera directions are vectors, never accumulated Euler angles. Local-frame changes
// re-express these vectors without rotating the world view.
public sealed class ViewCamera
{
    public V3 Right = new V3(1,0,0), Up = new V3(0,1,0);
    public V3 Depth => Right.Cross(Up).Unit();
    public void Set(V3 right, V3 up) {
        if(right.Length<1e-9 || right.Cross(up).Length<1e-9)throw new ArgumentException("Hệ trục view không hợp lệ.");
        Right=right.Unit(); Up=(up-Right*up.Dot(Right)).Unit();
    }
    public static V3 Rotate(V3 v,V3 axis,double radians) {
        axis=axis.Unit(); double c=Math.Cos(radians),s=Math.Sin(radians);
        return v*c+axis.Cross(v)*s+axis*(axis.Dot(v)*(1-c));
    }
    public void Orbit(double dx,double dy,V3 vertical) {
        double a=-dx*Math.PI/360,b=-dy*Math.PI/360;
        V3 r=Rotate(Right,vertical,a),u=Rotate(Up,vertical,a);
        Set(r,Rotate(u,r,b));
    }
    public static V3 Local(Frame f,V3 v)=>f==null?v:new V3(v.Dot(f.U),v.Dot(f.V),v.Dot(f.N));
    public static V3 World(Frame f,V3 v)=>f==null?v:f.U*v.X+f.V*v.Y+f.N*v.Z;
}
