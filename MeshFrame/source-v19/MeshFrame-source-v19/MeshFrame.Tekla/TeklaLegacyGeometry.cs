using System;using System.Linq;using System.Globalization;using System.Collections.Generic;using System.Text.RegularExpressions;using MeshFrame.Core;
namespace MeshFrame.Tekla;
public sealed partial class TeklaFrameService
{
 private void ReadLegacyGeometry(FrameRecord record,Dictionary<string,V3[]> actual,Dictionary<string,object> parts)
 {
  int count=record.Corners.Length;var s=record.Settings;object root;if(!parts.TryGetValue("B0",out root))throw new InvalidOperationException("Không có thanh B0 trong khung đang chọn.");
  string profile=Convert.ToString(A.Get(A.Get(root,"Profile"),"ProfileString"));var match=Regex.Match(profile??"",@"^L([0-9.]+)[*Xx]([0-9.]+)[*Xx]([0-9.]+)$",RegexOptions.IgnoreCase);if(match.Success){s.VLegA=double.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture);s.VLegB=double.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture);s.VThk=double.Parse(match.Groups[3].Value,CultureInfo.InvariantCulture);s.VProfile=profile;}
  var starts=new V3[count];for(int i=0;i<count;i++){V3[] ends;if(!actual.TryGetValue("B"+i,out ends))throw new InvalidOperationException("Thiếu thanh biên B"+i);starts[i]=ends[0];}
  Frame f;string error;if(!Frame.TryCreate(starts,out f,out error))throw new InvalidOperationException("Khung thực không đồng phẳng: "+error);
  double lat=(s.AutoPlacement||s.VPlane==PlaneKind.Middle?s.VLegA/2:0)+s.VLateral;double z=s.AutoPlacement||s.VDepth==DepthKind.Middle?s.VLegB/2:0;f.Origin=f.Origin-f.N*z;
  var lines=new V2[count];var dirs=new V2[count];for(int i=0;i<count;i++){var ends=actual["B"+i];V3 d=(ends[1]-ends[0]).Unit(),n=f.N.Cross(d).Unit();lines[i]=f.To2D(ends[0]-n*lat-f.N*z);dirs[i]=new V2(d.Dot(f.U),d.Dot(f.V));}
  var corners=new V3[count];for(int i=0;i<count;i++){V2 q;if(!Geo.Intersect(lines[(i+count-1)%count],dirs[(i+count-1)%count],lines[i],dirs[i],out q))throw new InvalidOperationException("Hai cạnh khung song song/trùng nhau; không suy được góc "+i);corners[i]=f.ToWorld(q,0);}record.Corners=corners;
  var stiff=actual.Where(p=>p.Key.StartsWith("B")&&int.Parse(p.Key.Substring(1))>=count).ToList();if(stiff.Count==0){s.StiffMode=StiffMode.None;return;}
  V3 gc=(stiff[0].Value[1]-stiff[0].Value[0]).Unit();int edge=s.StiffBaseEdge-1;if(edge<0||edge>=count||Math.Abs((corners[(edge+1)%count]-corners[edge]).Unit().Dot(gc))>.001){edge=-1;double longest=-1;for(int i=0;i<count;i++){var d=corners[(i+1)%count]-corners[i];if(Math.Abs(d.Unit().Dot(gc))<.001&&d.Length>longest){edge=i;longest=d.Length;}}}if(edge<0)throw new InvalidOperationException("Không xác định được hướng gia cường hiện tại.");s.StiffBaseEdge=edge+1;
  V3 u=(corners[(edge+1)%count]-corners[edge]).Unit();double min=corners.Min(p=>(p-corners[edge]).Dot(u)),max=corners.Max(p=>(p-corners[edge]).Dot(u));var stations=new List<double>();foreach(var item in stiff){V3 mid=(item.Value[0]+item.Value[1])*.5;double at=(mid-corners[edge]).Dot(u)+s.VLegA/2-lat-min;if(at<=.1||at>=max-min-.1)throw new InvalidOperationException("Gia cường nằm ngoài khung thực.");if(!stations.Any(p=>Math.Abs(p-at)<.05))stations.Add(at);}stations.Sort();var cells=new List<double>();double previous=0;foreach(double at in stations){cells.Add(at-previous);previous=at;}cells.Add(max-min-previous);s.StiffMode=StiffMode.Custom;s.StiffCells=DimEdit.FormatCells(cells);
 }
}
