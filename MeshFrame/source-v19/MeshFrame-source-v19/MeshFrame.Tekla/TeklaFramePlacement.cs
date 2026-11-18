using System;
using System.Collections;
using System.Collections.Generic;
using MeshFrame.Core;
namespace MeshFrame.Tekla;

public sealed partial class TeklaFrameService
{
 private FrameRecord _editRecord;
 private string _sourceFrameId;
 private V3[] _loadedReference;
 private int ObjectId(object part){return Convert.ToInt32(A.Get(A.Get(part,"Identifier"),"ID"));}
 private V3 PointValue(object p){return new V3(A.GetDouble(p,"X"),A.GetDouble(p,"Y"),A.GetDouble(p,"Z"));}
 private object Fresh(object part){return A.Call(_model,"SelectModelObject",A.New(A.GeometryAssembly.GetType("Tekla.Structures.Identifier",true),ObjectId(part)));}
 private T InGlobal<T>(Func<T> read){object h=A.Call(_model,"GetWorkPlaneHandler"),old=A.Call(h,"GetCurrentTransformationPlane");try{A.Require(h,"SetCurrentTransformationPlane","đọc vị trí global hiện tại",A.New(A.ModelType("TransformationPlane")));return read();}finally{A.Require(h,"SetCurrentTransformationPlane","khôi phục work plane",old);}}
 private V3[] ReadReferenceGlobal(object main){main=Fresh(main);if(main==null)throw new InvalidOperationException("Khung đã bị xóa khỏi model.");object cs=A.Call(main,"GetCoordinateSystem");V3 o=PointValue(A.Get(cs,"Origin")),x=PointValue(A.Get(cs,"AxisX")).Unit(),y=PointValue(A.Get(cs,"AxisY")).Unit();return new[]{o,o+x,o+y};}
 private V3[] ReadReference(object main){return InGlobal(()=>ReadReferenceGlobal(main));}
 private V3[] RebaseEditedCorners(V3[] corners){return InGlobal(()=>{var main=Fresh(_editMain);var current=ReadReferenceGlobal(main);var actual=BarPositions(InstanceParts(main,_sourceFrameId));foreach(var pair in _editRecord.NativeBars){V3[] value;if(!actual.TryGetValue(pair.Key,out value))throw new InvalidOperationException("Khung đã thay đổi/mất part "+pair.Key+" sau khi mở sửa.");for(int i=0;i<2;i++)if((FramePlacement.Map(pair.Value[i],_loadedReference,current)-value[i]).Length>.2)throw new InvalidOperationException("Khung bị sửa hình học trên model sau khi mở app; nạp lại khung trước khi cập nhật.");}return FramePlacement.Map(corners,_loadedReference,current);});}
 private Dictionary<string,object> InstanceParts(object main,string owner){
  var parts=new Dictionary<string,object>();var all=new List<object>{main};object asm=A.Call(main,"GetAssembly");var sec=A.Call(asm,"GetSecondaries") as IEnumerable;if(sec!=null)foreach(object o in sec)all.Add(o);
  foreach(object part in all){if(TeklaFrameStore.ReadId(A,part)!=owner)continue;string key=TeklaFrameStore.ReadKey(A,part);if(key.Length==0)continue;if(parts.ContainsKey(key)&&ObjectId(parts[key])!=ObjectId(part))throw new InvalidOperationException("Khung có mã part trùng "+key+"; dừng để tránh sửa nhầm.");parts[key]=part;
   try{object e=A.Call(part,"GetReinforcements");while(e!=null&&(bool)A.Call(e,"MoveNext")){object r=A.Get(e,"Current");if(TeklaFrameStore.ReadId(A,r)==owner){string k=TeklaFrameStore.ReadKey(A,r);if(k.Length>0)parts[k]=r;}}}catch(MissingMethodException){}
  }return parts;
 }
 private Dictionary<string,V3[]> BarPositions(Dictionary<string,object> parts){var result=new Dictionary<string,V3[]>();foreach(var pair in parts)if(A.ModelType("Beam").IsInstanceOfType(pair.Value)){var p=Fresh(pair.Value);result[pair.Key]=new[]{PointValue(A.Get(p,"StartPoint")),PointValue(A.Get(p,"EndPoint"))};}return result;}
 private Dictionary<string,V3[]> LegacyBars(FrameRecord record){Frame f;V2[] polygon;string error;if(!MeshFrameBuilder.TryFrame(record.Corners,record.Settings.AnchorPoint-1,out f,out polygon,out error))throw new InvalidOperationException(error);var plan=MeshFramePlanner.Plan(record.Settings,polygon);var result=new Dictionary<string,V3[]>();for(int i=0;i<plan.Bars.Count;i++){var b=plan.Bars[i];V2 n=(b.End-b.Start).Unit().Left();double lat=(record.Settings.AutoPlacement||record.Settings.VPlane==PlaneKind.Middle?b.LegA/2:0)+record.Settings.VLateral;double z=b.ZRef+(record.Settings.AutoPlacement||record.Settings.VDepth==DepthKind.Middle?b.LegB/2:0);result["B"+i]=new[]{f.ToWorld(b.Start+n*lat,z),f.ToWorld(b.End+n*lat,z)};}return result;}

 public FrameRecord LoadFromPart(object selected,out string error)
 {
  StopEditing();string failure=null;FrameRecord loaded=null;
  try{loaded=InGlobal(()=>{
   object main=TeklaFrameStore.ResolveMain(A,Fresh(selected));if(main==null)throw new InvalidOperationException("Không xác định được assembly khung đang chọn.");main=Fresh(main);string owner=TeklaFrameStore.ReadId(A,main);if(owner.Length==0)throw new InvalidOperationException("Part không thuộc khung MeshFrame.");var record=FrameRecord.Load(TeklaFrameStore.RecordPath(TeklaFrameStore.ModelFolder(A,_model),owner));if(record==null||record.ObjectIds.Count==0)throw new InvalidOperationException("Khung chưa có bản ghi ID để cập nhật an toàn.");
   var parts=InstanceParts(main,owner);var actual=BarPositions(parts);V3[] from,to;var current=ReadReferenceGlobal(main);Dictionary<string,V3[]> expected;
   if(record.NativeReference.Length==3&&record.NativeBars.Count>0){expected=record.NativeBars;from=record.NativeReference;to=current;}
   else{ReadLegacyGeometry(record,actual,parts);expected=LegacyBars(record);from=current;to=current;}
   foreach(var pair in expected){V3[] a;if(!actual.TryGetValue(pair.Key,out a))throw new InvalidOperationException("Khung thiếu thanh "+pair.Key+"; không dùng lại tọa độ cũ.");for(int i=0;i<2;i++)if((FramePlacement.Map(pair.Value[i],from,to)-a[i]).Length>.2)throw new InvalidOperationException("Hình khung đã đổi ngoài thao tác di chuyển/xoay tại "+pair.Key+". Dừng thay vì kéo khung về tọa độ cũ.");}
   MatchCopiedCuts(record,parts,from,to);
   bool copy=!record.ObjectIds.Contains(ObjectId(main));record.Corners=FramePlacement.Map(record.Corners,from,to);record.Id=copy?FrameRecord.NewId():owner;record.ObjectIds.Clear();foreach(var p in parts.Values)record.ObjectIds.Add(ObjectId(p));record.NativeReference=current;record.NativeBars=actual;
   _editMain=main;_sourceFrameId=owner;_editId=record.Id;_editRecord=record;_loadedReference=current;return record;
  });}catch(Exception e){failure=Root(e).Message;StopEditing();}
  error=failure;return loaded;
 }
 private void CaptureSnapshot(FrameRecord record,object main){InGlobal(()=>{main=Fresh(main);record.NativeReference=ReadReferenceGlobal(main);record.NativeBars=BarPositions(InstanceParts(main,record.Id));return true;});}
}
