using System;
using System.Text;
using MeshFrame.Core;

namespace MeshFrame.Tekla;

public sealed partial class TeklaFrameService
{
	private readonly TeklaApi A;

	private readonly object _model;

	private object _editMain;

	private string _editId;

	public string EditingId
	{
		get
		{
			if (_editMain == null)
			{
				return null;
			}
			return _editId;
		}
	}

	public TeklaFrameService(TeklaApi api, object model)
	{
		A = api;
		_model = model;
	}

	public void StopEditing()
	{
		_editMain = null;
		_editId = null;
		_editRecord=null; _sourceFrameId=null; _loadedReference=null;
		_matchedCutIds.Clear();
	}

	public ServiceResult Create(MeshFrameSettings settings, V3[] corners)
	{
		bool flag = _editMain != null;
		string text = (flag ? _editId : FrameRecord.NewId());
		if(flag) {
			try { corners=RebaseEditedCorners(corners); }
			catch(Exception error){return new ServiceResult{Success=false,Message="Không đọc được vị trí hiện tại của khung: "+Root(error).Message};}
		}
		StringBuilder stringBuilder = new StringBuilder();
		TeklaModelWriter teklaModelWriter = new TeklaModelWriter(A, _model)
		{
			FrameId = text,
			ReuseMain = (flag ? _editMain : null)
		};
		teklaModelWriter.ExistingFrameId = flag ? _sourceFrameId : text;
		if(flag)foreach(int id in _matchedCutIds)teklaModelWriter.MatchedInstanceCutIds.Add(id);
		if(flag) {
			var record=_editRecord??FrameRecord.Load(TeklaFrameStore.RecordPath(TeklaFrameStore.ModelFolder(A,_model),text));
			if(record==null||record.ObjectIds.Count==0)return new ServiceResult{Success=false,Message="Khung cũ chưa có danh sách ID object đã kiểm chứng. Không tự sửa/xóa khung này. Pick điểm để tạo khung mới bằng bản hiện tại."};
			foreach(int id in record.ObjectIds)teklaModelWriter.AllowedExistingIds.Add(id);
		}
		BuildResult buildResult;
		try
		{
			buildResult = MeshFrameBuilder.Build(settings, corners, teklaModelWriter);
		}
		catch (Exception error)
		{
			string message="Lỗi khi tạo/cập nhật: "+Root(error).Message+". Giữ object đã tạo/sửa để kiểm tra; KHÔNG tự dựng thay và xóa khung cũ.";
			try { teklaModelWriter.Audit("FAILED "+error); } catch {}
			return new ServiceResult { Success=false, Message=message };
		}
		if (!buildResult.Success)
		{
			return new ServiceResult
			{
				Success = false,
				Message = string.Join("\n", buildResult.Errors.ToArray())
			};
		}
		MeshFramePlan plan = buildResult.Plan;
		int num = 0;
		foreach (PlanBar bar in plan.Bars)
		{
			if (bar.Role == BarRole.Stiffener)
			{
				num++;
			}
		}
		stringBuilder.AppendLine(string.Format((flag) ? "Đã cập nhật khung: {0} V khung, {1} gia cường, {2} nẹp." : "Đã tạo: {0} V khung, {1} gia cường, {2} nẹp.", plan.Bars.Count - num, num, plan.Strips.Count));
		if (flag)
		{
			_editMain = null;
			_editId = null;
		}
		try
		{
			string text2 = TeklaFrameStore.ModelFolder(A, _model);
			if (string.IsNullOrEmpty(text2))
			{
				stringBuilder.AppendLine("⚠ Model chưa lưu: không lưu được bản ghi để sửa khung sau này.");
			}
			else
			{
				FrameRecord frameRecord = new FrameRecord();
				frameRecord.Id = text;
				frameRecord.Settings = settings;
				frameRecord.Corners = corners;
				frameRecord.ObjectIds.AddRange(teklaModelWriter.VerifiedIds);
				CaptureSnapshot(frameRecord,teklaModelWriter.MainPart);
				frameRecord.Save(TeklaFrameStore.RecordPath(text2, text));
			}
		}
		catch (Exception e2)
		{
			stringBuilder.AppendLine("⚠ Không lưu được bản ghi khung: " + Root(e2).Message);
		}
		foreach (string note in teklaModelWriter.Notes)
		{
			stringBuilder.AppendLine("ℹ " + note);
		}
		foreach (string warning in plan.Warnings)
		{
			stringBuilder.AppendLine("⚠ " + warning);
		}
		foreach (string message in teklaModelWriter.Messages)
		{
			stringBuilder.AppendLine("⚠ " + message);
		}
		return new ServiceResult
		{
			Success = true,
			FrameId = text,
			ObjectIds = teklaModelWriter.VerifiedIds.ToArray(),
			Corners = corners,
			Message = stringBuilder.ToString().TrimEnd()
		};
	}

	public string ShowCreated(ServiceResult result,V3[] corners)
	{
		if(!result.Success||result.ObjectIds.Length==0)return "";
		object handler=A.Call(_model,"GetWorkPlaneHandler"),old=A.Call(handler,"GetCurrentTransformationPlane");
		try {
			A.Require(handler,"SetCurrentTransformationPlane","chọn khung trong global",A.New(A.ModelType("TransformationPlane")));
			var parts=new System.Collections.ArrayList();
			foreach(int id in result.ObjectIds){object part=A.Call(_model,"SelectModelObject",A.New(A.GeometryAssembly.GetType("Tekla.Structures.Identifier",true),id));if(part==null)throw new InvalidOperationException("Object mất khi chọn ID="+id);parts.Add(part);}
			A.Require(A.New(A.ModelType("UI.ModelObjectSelector")),"Select","chọn khung vừa tạo",parts);
			double minx=double.MaxValue,miny=minx,minz=minx,maxx=double.MinValue,maxy=maxx,maxz=maxx;
			foreach(V3 p in corners){minx=Math.Min(minx,p.X);miny=Math.Min(miny,p.Y);minz=Math.Min(minz,p.Z);maxx=Math.Max(maxx,p.X);maxy=Math.Max(maxy,p.Y);maxz=Math.Max(maxz,p.Z);}
			object box=A.New(A.GeoType("AABB"),A.Point(minx-200,miny-200,minz-200),A.Point(maxx+200,maxy+200,maxz+200));
			var method=A.ModelType("UI.ViewHandler").GetMethod("ZoomToBoundingBox",new[]{A.GeoType("AABB")});
			bool zoom=method!=null&&Convert.ToBoolean(method.Invoke(null,new[]{box}));
			return "Đã chọn "+parts.Count+" object khung vừa tạo. "+(zoom?"Đã zoom tới khung trong Tekla.":"Không zoom được view hiện tại; khung vẫn được xác nhận bằng ID.");
		} catch(Exception e) {return "Khung đã được xác nhận nhưng chưa chọn/zoom được: "+Root(e).Message;}
		finally {A.Require(handler,"SetCurrentTransformationPlane","khôi phục work plane",old);}
	}

	public FrameRecord LoadSelected(out string error)
	{
		StopEditing();
		error = null;
		string text = TeklaFrameStore.FindSelected(A, out var mainPart);
		if (text.Length == 0)
		{
			error = "Chưa chọn khung do MeshFrame tạo. Chọn một part của khung trong Tekla rồi bấm lại.";
			return null;
		}
		return LoadFromPart(mainPart,out error);
	}

	private static Exception Root(Exception e)
	{
		while (e.InnerException != null)
		{
			e = e.InnerException;
		}
		return e;
	}
}
