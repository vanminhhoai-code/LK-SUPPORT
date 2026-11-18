using System;
namespace MeshFrame.Core;

public static class FramePlacement
{
 public static V3 Map(V3 point,V3[] from,V3[] to)
 {
  Frame a,b;string error;
  if(from==null||to==null||from.Length<3||to.Length<3||!Frame.TryCreate(from,out a,out error)||!Frame.TryCreate(to,out b,out error))
   throw new InvalidOperationException("Không xác định được hệ tọa độ hiện tại của khung.");
  V3 d=point-a.Origin;
  return b.Origin+b.U*d.Dot(a.U)+b.V*d.Dot(a.V)+b.N*d.Dot(a.N);
 }
 public static V3[] Map(V3[] points,V3[] from,V3[] to)
 {
  var result=new V3[points.Length];for(int i=0;i<points.Length;i++)result[i]=Map(points[i],from,to);return result;
 }
}
