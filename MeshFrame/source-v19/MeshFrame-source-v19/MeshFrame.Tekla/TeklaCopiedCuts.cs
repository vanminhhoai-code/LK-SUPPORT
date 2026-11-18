using System;
using System.Collections.Generic;
using MeshFrame.Core;
namespace MeshFrame.Tekla;

public sealed partial class TeklaFrameService
{
 private readonly HashSet<int> _matchedCutIds=new HashSet<int>();
 private static void FitSpec(Dictionary<string,List<V3[]>> specs,string key,Frame f,V2 point,V2? cut){if(!cut.HasValue)return;List<V3[]> list;if(!specs.TryGetValue(key,out list))specs[key]=list=new List<V3[]>();V3 o=f.ToWorld(point,0),x=f.U*cut.Value.X+f.V*cut.Value.Y;list.Add(new[]{o,o+x,o+f.N});}
 private Dictionary<string,List<V3[]>> PlannedCutShapes(FrameRecord record){
  var specs=new Dictionary<string,List<V3[]>>();Frame f;V2[] poly;string err;if(!MeshFrameBuilder.TryFrame(record.Corners,record.Settings.AnchorPoint-1,out f,out poly,out err))throw new InvalidOperationException(err);var p=MeshFramePlanner.Plan(record.Settings,poly);
  for(int i=0;i<p.Bars.Count;i++){var b=p.Bars[i];string key="B"+i;FitSpec(specs,key,f,b.Start,b.CutStart);FitSpec(specs,key,f,b.End,b.CutEnd);foreach(var quad in b.Notches){List<V3[]> list;if(!specs.TryGetValue(key,out list))specs[key]=list=new List<V3[]>();var points=new List<V3>();foreach(V2 q in quad){points.Add(f.ToWorld(q,0));points.Add(f.ToWorld(q,b.Thk));}list.Add(points.ToArray());}}
  for(int i=0;i<p.Strips.Count;i++){var s=p.Strips[i];FitSpec(specs,"S"+i,f,s.Start,s.CutStart);FitSpec(specs,"S"+i,f,s.End,s.CutEnd);}return specs;
 }
 private V3[] CutShape(object cut){
  if(A.ModelType("Fitting").IsInstanceOfType(cut)){object plane=A.Get(cut,"Plane");V3 o=PointValue(A.Get(plane,"Origin"));return new[]{o,o+PointValue(A.Get(plane,"AxisX")).Unit(),o+PointValue(A.Get(plane,"AxisY")).Unit()};}
  if(!A.ModelType("BooleanPart").IsInstanceOfType(cut)||Convert.ToString(A.Get(cut,"Type"))!="BOOLEAN_CUT")return null;
  object part=A.Get(cut,"OperativePart");if(part==null)return null;object solid=A.Call(part,"GetSolid");if(solid==null)return null;var points=new List<V3>();var faces=A.Call(solid,"GetFaceEnumerator");while((bool)A.Call(faces,"MoveNext")){var loops=A.Call(A.Get(faces,"Current"),"GetLoopEnumerator");while((bool)A.Call(loops,"MoveNext")){var vertices=A.Call(A.Get(loops,"Current"),"GetVertexEnumerator");while((bool)A.Call(vertices,"MoveNext")){V3 q=PointValue(A.Get(vertices,"Current"));if(!points.Exists(p=>(p-q).Length<1e-5))points.Add(q);}}}return points.ToArray();
 }
 private static bool SameCut(V3[] a,V3[] b){if(a==null||b==null)return false;if(a.Length==3&&b.Length==3){V3 n=(a[1]-a[0]).Cross(a[2]-a[0]).Unit(),m=(b[1]-b[0]).Cross(b[2]-b[0]).Unit();return Math.Abs(n.Dot(m))>1-1e-6&&Math.Abs((b[0]-a[0]).Dot(n))<.05;}if(a.Length!=b.Length)return false;foreach(V3 q in a)if(!Array.Exists(b,p=>(p-q).Length<.05))return false;return true;}
 private void MatchCopiedCuts(FrameRecord record,Dictionary<string,object> parts,V3[] from,V3[] to){
  var expected=PlannedCutShapes(record);foreach(var pair in expected){object part;if(!parts.TryGetValue(pair.Key,out part))continue;object cuts=A.Call(part,"GetBooleans");while((bool)A.Call(cuts,"MoveNext")){object cut=A.Get(cuts,"Current");if(TeklaFrameStore.ReadId(A,cut).Length!=0)continue;V3[] shape=CutShape(cut);if(shape==null)continue;foreach(V3[] spec in pair.Value)if(SameCut(FramePlacement.Map(spec,from,to),shape)){_matchedCutIds.Add(ObjectId(cut));break;}}}
 }
}
