using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MeshFrame.Core;

namespace MeshFrame.Tekla;

public sealed class TeklaModelWriter : IModelWriter
{
	private sealed class Calib
	{
		public string Rotation;

		public double Lat;

		public double Z;
	}

	private sealed class Box
	{
		public double XMin;

		public double XMax;

		public double YMin;

		public double YMax;

		public double ZMin;

		public double ZMax;

		public object Solid;
	}

	private sealed class Offs
	{
		public double Lat;

		public double Z;
	}

	private readonly TeklaApi A;

	private readonly object _model;

	private PlanPlacement P;

	private readonly List<object> _created = new List<object>();

	public readonly List<string> Messages = new List<string>();

	public readonly List<string> Notes = new List<string>();

	private Calib _vCal;

	private Calib _sCal;

	public string FrameId;

	public object ReuseMain;
	public readonly HashSet<int> AllowedExistingIds = new HashSet<int>();
	public string ExistingFrameId;
	public readonly HashSet<int> MatchedInstanceCutIds = new HashSet<int>();
	private bool WasOwned(object part){string id=TeklaFrameStore.ReadId(A,part);return id==FrameId||(!string.IsNullOrEmpty(ExistingFrameId)&&id==ExistingFrameId)||MatchedInstanceCutIds.Contains(Id(part));}
	public readonly List<int> VerifiedIds = new List<int>();
	private readonly Dictionary<int,V3[]> _expectedEnds = new Dictionary<int,V3[]>();
	private Frame _writeFrame;
	public void Audit(string message) {
		string folder=Path.Combine(TeklaFrameStore.ModelFolder(A,_model),"MeshFrame");
		Directory.CreateDirectory(folder);
		File.AppendAllText(Path.Combine(folder,"operations.log"),DateTime.Now.ToString("s")+" frame="+FrameId+" "+message+Environment.NewLine);
	}
	private int Id(object part) { return Convert.ToInt32(A.Get(A.Get(part,"Identifier"),"ID")); }
	private V3 ReadPoint(object p) { return new V3(A.GetDouble(p,"X"),A.GetDouble(p,"Y"),A.GetDouble(p,"Z")); }
	private static string XYZ(V3 p){return F(p.X)+","+F(p.Y)+","+F(p.Z);}
	private void Track(object part,string key,bool added) {
		int id=Id(part); if(id<=0)throw new InvalidOperationException("Tekla không trả ID hợp lệ: "+key);
		TeklaFrameStore.WriteKey(A,part,key); TeklaFrameStore.WriteId(A,part,FrameId);
		if(added&&!_created.Contains(part))_created.Add(part);
		if(IsBeam(part)) {
			V3 s=ReadPoint(A.Get(part,"StartPoint")),e=ReadPoint(A.Get(part,"EndPoint"));
			_expectedEnds[id]=new[]{_writeFrame.ToWorld(new V2(s.X,s.Y),s.Z),_writeFrame.ToWorld(new V2(e.X,e.Y),e.Z)};
		}
		Audit((added?"INSERT":"MODIFY")+" id="+id+" key="+key);
	}
	private void VerifyCommitted() {
		VerifiedIds.Clear();
		var seen=new HashSet<int>();
		foreach(object part in _created)seen.Add(Id(part));
		foreach(object part in _reused)seen.Add(Id(part));
		foreach(int id in seen){
			object fresh=A.Call(_model,"SelectModelObject",A.New(A.GeometryAssembly.GetType("Tekla.Structures.Identifier",true),id));
			if(fresh==null)throw new InvalidOperationException("Object mất sau commit: ID="+id);
			if(TeklaFrameStore.ReadId(A,fresh)!=FrameId)throw new InvalidOperationException("Mã sở hữu object không khớp sau commit: ID="+id);
			if(A.Call(fresh,"GetSolid")==null)throw new InvalidOperationException("Object không có hình khối sau commit: ID="+id);
			V3[] expected;
			if(_expectedEnds.TryGetValue(id,out expected)) {
				V3 s=ReadPoint(A.Get(fresh,"StartPoint")),e=ReadPoint(A.Get(fresh,"EndPoint"));
				if((s-expected[0]).Length>.1||(e-expected[1]).Length>.1)
					throw new InvalidOperationException("Sai tọa độ sau commit ID="+id+"; expected="+XYZ(expected[0])+" actual="+XYZ(s));
			}
			VerifiedIds.Add(id); Audit("EXISTS_AFTER_COMMIT id="+id);
		}
		if(VerifiedIds.Count==0)throw new InvalidOperationException("Không có object nào được xác nhận sau commit.");
		Notes.Add("Đã đọc lại "+VerifiedIds.Count+" ID từ model sau commit; tọa độ global khớp. ID: "+string.Join(",",VerifiedIds));
	}

	private Dictionary<string, object> _old;

	private readonly List<object> _oldLeft = new List<object>();

	private readonly HashSet<object> _reused = new HashSet<object>();

	private int _updated;

	private int _added;

	private static readonly string[] Rotations = new string[4] { "FRONT", "TOP", "BACK", "BELOW" };

	public object MainPart { get; private set; }

	public TeklaModelWriter(TeklaApi api, object model)
	{
		A = api;
		_model = model ?? A.New(A.ModelType("Model"));
	}

	public void Write(MeshFramePlan plan, Frame f)
	{
		P = plan.Placement;
		_writeFrame=f; _expectedEnds.Clear(); VerifiedIds.Clear();
		if(string.IsNullOrEmpty(FrameId))FrameId=FrameRecord.NewId();
		Audit("BEGIN mode="+(ReuseMain==null?"CREATE":"UPDATE"));
		_created.Clear();
		_reused.Clear();
		_oldLeft.Clear();
		_old = null;
		_updated = 0;
		_added = 0;
		MainPart = null;
		object target = A.Call(_model, "GetWorkPlaneHandler");
		object obj = A.Call(target, "GetCurrentTransformationPlane");
		try
		{
			// Frame/Pick points are GLOBAL. Constructing the next plane while a rotated,
			// translated work plane is active composes the old transform a second time.
			A.Require(target,"SetCurrentTransformationPlane","về global trước dựng mặt phẳng khung",A.New(A.ModelType("TransformationPlane")));
			object obj2 = A.New(A.ModelType("TransformationPlane"), A.Point(f.Origin.X, f.Origin.Y, f.Origin.Z), A.Vector(f.U.X, f.U.Y, f.U.Z), A.Vector(f.V.X, f.V.Y, f.V.Z));
			A.Require(target, "SetCurrentTransformationPlane", "đặt mặt phẳng khung", obj2);
			_vCal = null;
			_sCal = null;
			if (P.AutoPlacement)
			{
				Calibrate(plan);
			}
			if (ReuseMain != null)
			{
				CollectOld();
			}
			List<object> list = new List<object>();
			List<object> list2 = new List<object>();
			try
			{
				for (int i = 0; i < plan.Bars.Count; i++)
				{
					list.Add(InsertBar(plan.Bars[i], "B" + i));
				}
				for (int j = 0; j < plan.Strips.Count; j++)
				{
					list2.Add(InsertStrip(plan.Strips[j], "S" + j));
				}
			}
			catch (Exception error)
			{
				Audit("FAILED_KEEP_PARTIAL "+error.Message);
				throw;
			}
			if (list.Count == 0)
			{
				throw new InvalidOperationException("Không có thanh khung được tạo.");
			}
			MainPart = list[0];
			if (P.AutoPlacement)
			{
				Verify(plan, list, list2);
			}
			object obj3 = null;
			if (plan.Mesh != null)
			{
				try
				{
					obj3 = InsertMesh(plan.Mesh, list[0], plan.Polygon);
					if (obj3 != null)
					{
						Track(obj3,"M",true);
					}
				}
				catch (Exception e)
				{
					throw new InvalidOperationException("Lưới không tạo được; giữ các phần đã tạo để kiểm tra: "+Root(e).Message,e);
				}
			}
			try
			{
				object target2 = A.Call(list[0], "GetAssembly");
				for (int k = 1; k < list.Count; k++)
				{
					if (!_reused.Contains(list[k]))
					{
						A.Require(target2, "Add", "gộp V vào assembly", list[k]);
					}
				}
				foreach (object item in list2)
				{
					if (!_reused.Contains(item))
					{
						A.Require(target2, "Add", "gộp nẹp vào assembly", item);
					}
				}
				if (obj3 != null && A.ModelType("Part").IsInstanceOfType(obj3))
				{
					A.Require(target2, "Add", "gộp tấm lưới vào assembly", obj3);
				}
				object target3 = A.Get(target2, "AssemblyNumber");
				A.Set(target3, "Prefix", plan.AsmPrefix);
				A.Set(target3, "StartNumber", plan.AsmStartNo);
				A.Require(target2, "Modify", "lưu assembly khung");
			}
			catch (Exception e2)
			{
				throw new InvalidOperationException("Chưa gộp được assembly; giữ object đã tạo: "+Root(e2).Message,e2);
			}
			if (ReuseMain != null)
			{
				Notes.Add("Cập nhật tại chỗ: sửa " + _updated + " part, thêm " + _added + "; chưa dọn part dư cho tới sau kiểm chứng commit.");
			}
			if (string.IsNullOrEmpty(FrameId))
			{
				return;
			}
			try
			{
				TeklaFrameStore.WriteId(A, list[0], FrameId);
			}
			catch (Exception e3)
			{
				throw new InvalidOperationException("Không gắn được mã khung: "+Root(e3).Message,e3);
			}
		}
		finally
		{
			try { A.Require(_model,"CommitChanges","commit object khung"); Audit("COMMIT"); }
			finally { A.Require(target,"SetCurrentTransformationPlane","khôi phục work plane",obj); }
		}
		try {
			A.Require(target,"SetCurrentTransformationPlane","đọc tọa độ global",A.New(A.ModelType("TransformationPlane")));
			VerifyCommitted();
			if(ReuseMain!=null) { int deleted=RemoveLeftovers(); A.Require(_model,"CommitChanges","commit cập nhật đã kiểm chứng"); VerifyCommitted(); Notes.Add("Dọn "+deleted+" part dư thuộc đúng khung sau kiểm chứng."); }
		} finally { A.Require(target,"SetCurrentTransformationPlane","khôi phục work plane",obj); }
	}

	private static bool IsBeam(object o)
	{
		if (o != null)
		{
			return o.GetType().Name == "Beam";
		}
		return false;
	}

	private void CollectOld()
	{
		_old = new Dictionary<string, object>();
		object obj = A.Call(ReuseMain, "GetAssembly");
		List<object> list = new List<object>();
		list.Add(ReuseMain);
		IEnumerable enumerable = ((obj == null) ? null : (A.Call(obj, "GetSecondaries") as IEnumerable));
		if (enumerable != null)
		{
			foreach (object item in enumerable)
			{
				list.Add(item);
			}
		}
		foreach (object item2 in list)
		{
			if(!AllowedExistingIds.Contains(Id(item2))) continue;
			if(!WasOwned(item2))throw new InvalidOperationException("Object không thuộc khung đang sửa: ID="+Id(item2));
			string text = TeklaFrameStore.ReadKey(A, item2);
			if (text.Length == 0 && item2 == ReuseMain)
			{
				text = "B0";
			}
			if (text.Length > 0 && !_old.ContainsKey(text))
			{
				_old[text] = item2;
			}
			else
			{
				_oldLeft.Add(item2);
			}
		}
	}

	private int RemoveLeftovers()
	{
		int num = 0;
		List<object> list = new List<object>(_old.Values);
		list.AddRange(_oldLeft);
		foreach (object item in list)
		{
			try
			{
				int id=Id(item);
				if(!AllowedExistingIds.Contains(id)||!WasOwned(item)||id==Id(MainPart))throw new InvalidOperationException("Không cho xóa object chưa xác nhận quyền sở hữu ID="+id);
				foreach(string children in new[]{"GetReinforcements","GetBooleans"}) {
					object en=A.Call(item,children); while(en!=null&&(bool)A.Call(en,"MoveNext")) {
						object child=A.Get(en,"Current"); if(!WasOwned(child))throw new InvalidOperationException("Giữ part dư vì có cắt/rebar ngoài MeshFrame: "+id);
					}
				}
				Audit("DELETE_OWNED_SURPLUS id="+id);
				A.Require(item,"Delete","xóa part dư đã kiểm chứng ID="+id);
				num++;
			}
			catch (Exception e)
			{
				Messages.Add("Không xóa được part thừa của khung cũ: " + Root(e).Message);
			}
		}
		_old.Clear();
		_oldLeft.Clear();
		return num;
	}

	private object TakeOld(string key, V2 a, V2 b, double z)
	{
		if (_old == null || !_old.TryGetValue(key, out var value) || !IsBeam(value))
		{
			return null;
		}
		_old.Remove(key);
		List<object> list = new List<object>();
		object obj = A.Call(value, "GetBooleans");
		while (obj != null && (bool)A.Call(obj, "MoveNext"))
		{
			list.Add(A.Get(obj, "Current"));
		}
		foreach (object item in list)
		{
			if(WasOwned(item)) {
				Audit("DELETE_OWNED_CUT id="+Id(item)); A.Require(item,"Delete","thay cắt của đúng khung");
			}
		}
		A.Set(value, "StartPoint", A.Point(a.X, a.Y, z));
		A.Set(value, "EndPoint", A.Point(b.X, b.Y, z));
		return value;
	}

	private void Commit(object beam, object old, string what, string key)
	{
		if (old != null)
		{
			A.Require(beam, "Modify", "cập nhật " + what);
			_reused.Add(beam);
			Track(beam,key,false);
			_updated++;
		}
		else
		{
			A.Require(beam, "Insert", what);
			Track(beam,key,true);
			_added++;
		}
	}

	private object InsertBar(PlanBar bar, string key)
	{
		V2 v = (bar.End - bar.Start).Unit().Left();
		string plane = P.VPlane;
		string rotation = P.VRotation;
		string depth = P.VDepth;
		double num;
		double num2;
		if (_vCal != null)
		{
			plane = "MIDDLE";
			depth = "MIDDLE";
			rotation = _vCal.Rotation;
			num = _vCal.Lat + P.VLateral;
			num2 = _vCal.Z;
		}
		else
		{
			num = ((P.VPlane == "MIDDLE") ? (bar.LegA / 2.0) : 0.0) + P.VLateral;
			num2 = ((P.VDepth == "MIDDLE") ? (bar.LegB / 2.0) : 0.0);
		}
		object obj = TakeOld(key, bar.Start + v * num, bar.End + v * num, bar.ZRef + num2);
		object obj2 = obj ?? NewBeam(bar.Start + v * num, bar.End + v * num, bar.ZRef + num2);
		Describe(obj2, bar.Profile, bar.Material, bar.Name, bar.Class, bar.Prefix, bar.StartNo, plane, rotation, depth);
		Commit(obj2, obj, "thanh " + bar.Profile + " (kiểm tra profile trong catalog)", key);
		AddMiter(obj2, bar.Start, bar.CutStart);
		AddMiter(obj2, bar.End, bar.CutEnd);
		foreach (V2[] notch in bar.Notches)
		{
			AddNotch(obj2, notch, bar.Thk, bar.Material);
		}
		return obj2;
	}

	private object InsertStrip(PlanStrip st, string key)
	{
		V2 v = (st.End - st.Start).Unit().Left();
		string plane = P.StripPlane;
		string rotation = P.StripRotation;
		string depth = P.StripDepth;
		double num;
		double num2;
		if (_sCal != null)
		{
			plane = "MIDDLE";
			depth = "MIDDLE";
			rotation = _sCal.Rotation;
			num = _sCal.Lat + P.StripLateral;
			num2 = _sCal.Z;
		}
		else
		{
			num = P.StripLateral;
			num2 = ((P.StripDepth == "MIDDLE") ? ((0.0 - st.Thk) / 2.0) : 0.0);
		}
		object obj = TakeOld(key, st.Start + v * num, st.End + v * num, st.ZRef + num2);
		object obj2 = obj ?? NewBeam(st.Start + v * num, st.End + v * num, st.ZRef + num2);
		Describe(obj2, st.Profile, st.Material, st.Name, st.Class, st.Prefix, st.StartNo, plane, rotation, depth);
		Commit(obj2, obj, "nẹp " + st.Profile + " (kiểm tra profile trong catalog)", key);
		AddMiter(obj2, st.Start, st.CutStart);
		AddMiter(obj2, st.End, st.CutEnd);
		return obj2;
	}

	private void Calibrate(MeshFramePlan plan)
	{
		PlanBar planBar = null;
		foreach (PlanBar bar in plan.Bars)
		{
			if (bar.Role == BarRole.Frame)
			{
				planBar = bar;
				break;
			}
		}
		PlanStrip planStrip = ((plan.Strips.Count > 0) ? plan.Strips[0] : null);
		try
		{
			if (planBar != null)
			{
				_vCal = CalibrateAngle(planBar, P.VRotation);
			}
			if (planStrip != null)
			{
				_sCal = CalibrateFlat(planStrip, P.StripRotation);
			}
			if (planBar != null && _vCal == null)
			{
				Messages.Add("Không tự canh được V theo hình khối thật (không tìm được hướng xoay cho thép góc " + planBar.Profile + "): dùng đặt theo thông số, có thể lệch.");
			}
			if (planStrip != null && _sCal == null)
			{
				Messages.Add("Không tự canh được nẹp theo hình khối thật (profile " + planStrip.Profile + " không ra dạng dẹt " + F(planStrip.Width) + "x" + F(planStrip.Thk) + "): dùng đặt theo thông số, có thể lệch.");
			}
			if (_vCal != null || _sCal != null)
			{
				Notes.Add("Tự canh theo hình khối thật Tekla: " + ((_vCal == null) ? "" : ("V xoay " + _vCal.Rotation + ", lệch ngang " + F(_vCal.Lat) + " mm, lệch cao " + F(_vCal.Z) + " mm")) + ((_vCal != null && _sCal != null) ? "; " : "") + ((_sCal == null) ? "" : ("nẹp xoay " + _sCal.Rotation + ", lệch ngang " + F(_sCal.Lat) + " mm, lệch cao " + F(_sCal.Z) + " mm")) + ".");
			}
		}
		catch (Exception e)
		{
			_vCal = null;
			_sCal = null;
			Messages.Add("Không đo được hình khối Tekla (" + Root(e).GetType().Name + ": " + Root(e).Message + "): dùng đặt theo thông số, có thể lệch.");
		}
	}

	private static List<string> RotationOrder(string first)
	{
		List<string> list = new List<string> { first };
		string[] rotations = Rotations;
		foreach (string text in rotations)
		{
			if (text != first)
			{
				list.Add(text);
			}
		}
		return list;
	}

	private T WithSample<T>(string profile, string material, string rotation, Func<Box, T> use)
	{
		object obj = NewBeam(new V2(0.0, 0.0), new V2(300.0, 0.0), 0.0);
		try
		{
			Describe(obj, profile, material, "MF_CALIB", "0", "X", 1, "MIDDLE", rotation, "MIDDLE");
			A.Require(obj, "Insert", "thanh mẫu " + profile);
			A.Call(_model, "CommitChanges");
			object obj2 = A.Call(obj, "GetSolid");
			if (obj2 == null)
			{
				throw new InvalidOperationException("Tekla không trả về hình khối của thanh mẫu.");
			}
			object target = A.Get(obj2, "MinimumPoint");
			object target2 = A.Get(obj2, "MaximumPoint");
			Box box = new Box
			{
				Solid = obj2,
				XMin = A.GetDouble(target, "X"),
				YMin = A.GetDouble(target, "Y"),
				ZMin = A.GetDouble(target, "Z"),
				XMax = A.GetDouble(target2, "X"),
				YMax = A.GetDouble(target2, "Y"),
				ZMax = A.GetDouble(target2, "Z")
			};
			if (Math.Abs(box.XMax - box.XMin - 300.0) > 1.0)
			{
				throw new InvalidOperationException("hình khối đo được không khớp thanh mẫu (chiều dài " + F(box.XMax - box.XMin) + " thay vì 300) - toạ độ của Tekla khác work plane?");
			}
			return use(box);
		}
		finally
		{
			try
			{
				Audit("DELETE_CALIBRATION_SAMPLE id="+Id(obj));
				A.Call(obj, "Delete");
			}
			catch (Exception)
			{
			}
		}
	}

	private double Span(object solid, double x, double y1, double z1, double y2, double z2, char axis)
	{
		if (!(A.Call(solid, "Intersect", A.Point(x, y1, z1), A.Point(x, y2, z2)) is IList { Count: >=2 } list))
		{
			return 0.0;
		}
		double num = double.MaxValue;
		double num2 = double.MinValue;
		foreach (object item in list)
		{
			double val = A.GetDouble(item, (axis == 'Y') ? "Y" : "Z");
			num = Math.Min(num, val);
			num2 = Math.Max(num2, val);
		}
		return num2 - num;
	}

	private Calib CalibrateAngle(PlanBar bar, string first)
	{
		foreach (string rot in RotationOrder(first))
		{
			Calib calib = WithSample(bar.Profile, bar.Material, rot, (Box m) =>
			{
				double num = m.YMax - m.YMin;
				double num2 = m.ZMax - m.ZMin;
				if (Math.Abs(num - bar.LegA) > 0.6 || Math.Abs(num2 - bar.LegB) > 0.6)
				{
					return (Calib)null;
				}
				double x = (m.XMin + m.XMax) / 2.0;
				double thk = bar.Thk;
				bool num3 = Span(m.Solid, x, m.YMin - 2.0, m.ZMin + thk / 2.0, m.YMax + 2.0, m.ZMin + thk / 2.0, 'Y') > 0.7 * num && Span(m.Solid, x, m.YMin - 2.0, m.ZMax - thk / 2.0, m.YMax + 2.0, m.ZMax - thk / 2.0, 'Y') < 0.7 * num;
				bool flag = Span(m.Solid, x, m.YMin + thk / 2.0, m.ZMin - 2.0, m.YMin + thk / 2.0, m.ZMax + 2.0, 'Z') > 0.7 * num2 && Span(m.Solid, x, m.YMax - thk / 2.0, m.ZMin - 2.0, m.YMax - thk / 2.0, m.ZMax + 2.0, 'Z') < 0.7 * num2;
				return (!(num3 & flag)) ? null : new Calib
				{
					Rotation = rot,
					Lat = 0.0 - m.YMin,
					Z = 0.0 - m.ZMin
				};
			});
			if (calib != null)
			{
				return calib;
			}
		}
		return null;
	}

	private Calib CalibrateFlat(PlanStrip st, string first)
	{
		foreach (string rot in RotationOrder(first))
		{
			Calib calib = WithSample(st.Profile, st.Material, rot, (Box m) =>
			{
				double num = m.YMax - m.YMin;
				double num2 = m.ZMax - m.ZMin;
				return (Math.Abs(num - st.Width) > 0.3 || Math.Abs(num2 - st.Thk) > 0.3) ? null : new Calib
				{
					Rotation = rot,
					Lat = (0.0 - (m.YMin + m.YMax)) / 2.0,
					Z = 0.0 - m.ZMax
				};
			});
			if (calib != null)
			{
				return calib;
			}
		}
		return null;
	}

	private List<double> Probe(object solid, V2 origin, V2 n, double lat1, double z1, double lat2, double z2, bool wantLat)
	{
		V2 v = origin + n * lat1;
		V2 v2 = origin + n * lat2;
		IList list = A.Call(solid, "Intersect", A.Point(v.X, v.Y, z1), A.Point(v2.X, v2.Y, z2)) as IList;
		List<double> list2 = new List<double>();
		if (list == null)
		{
			return list2;
		}
		foreach (object item in list)
		{
			if (wantLat)
			{
				list2.Add((new V2(A.GetDouble(item, "X"), A.GetDouble(item, "Y")) - origin).Dot(n));
			}
			else
			{
				list2.Add(A.GetDouble(item, "Z"));
			}
		}
		return list2;
	}

	private static void MinMax(List<double> v, out double lo, out double hi)
	{
		lo = double.MaxValue;
		hi = double.MinValue;
		foreach (double item in v)
		{
			lo = Math.Min(lo, item);
			hi = Math.Max(hi, item);
		}
	}

	private Offs MeasureBar(PlanBar b, object beam, out string why)
	{
		why = null;
		V2 n = (b.End - b.Start).Unit().Left();
		V2 origin = (b.Start + b.End) * 0.5;
		object obj = A.Call(beam, "GetSolid");
		if (obj == null)
		{
			why = "Tekla không trả về hình khối";
			return null;
		}
		double thk = b.Thk;
		double num = 30.0;
		List<double> list = Probe(obj, origin, n, b.LegA / 2.0, b.ZRef - num, b.LegA / 2.0, b.ZRef + b.LegB + num, wantLat: false);
		if (list.Count < 2)
		{
			why = "đường thăm dò không trúng hình khối";
			return null;
		}
		MinMax(list, out var lo, out var hi);
		List<double> list2 = Probe(obj, origin, n, 0.0 - num, (lo + hi) / 2.0, b.LegA + num, (lo + hi) / 2.0, wantLat: true);
		if (list2.Count < 2)
		{
			why = "đường thăm dò không trúng hình khối";
			return null;
		}
		MinMax(list2, out var lo2, out var hi2);
		List<double> list3 = Probe(obj, origin, n, lo2 + thk / 2.0, b.ZRef - num, lo2 + thk / 2.0, b.ZRef + b.LegB + num, wantLat: false);
		if (list3.Count < 2)
		{
			why = "đường thăm dò không trúng hình khối";
			return null;
		}
		MinMax(list3, out var lo3, out var hi3);
		if (Math.Abs(hi2 - lo2 - b.LegA) > 1.0 || Math.Abs(hi3 - lo3 - b.LegB) > 1.0)
		{
			why = "tiết diện đo được " + F(hi2 - lo2) + "x" + F(hi3 - lo3) + " khác " + F(b.LegA) + "x" + F(b.LegB);
			return null;
		}
		return new Offs
		{
			Lat = lo2,
			Z = lo3 - b.ZRef
		};
	}

	private Offs MeasureStrip(PlanStrip st, object beam, out string why)
	{
		why = null;
		V2 n = (st.End - st.Start).Unit().Left();
		V2 origin = (st.Start + st.End) * 0.5;
		object obj = A.Call(beam, "GetSolid");
		if (obj == null)
		{
			why = "Tekla không trả về hình khối";
			return null;
		}
		double num = 30.0;
		double width = st.Width;
		double thk = st.Thk;
		List<double> list = Probe(obj, origin, n, 0.0, st.ZRef - thk - num, 0.0, st.ZRef + num, wantLat: false);
		if (list.Count < 2)
		{
			why = "đường thăm dò không trúng hình khối";
			return null;
		}
		MinMax(list, out var lo, out var hi);
		List<double> list2 = Probe(obj, origin, n, 0.0 - (width / 2.0 + num), (lo + hi) / 2.0, width / 2.0 + num, (lo + hi) / 2.0, wantLat: true);
		if (list2.Count < 2)
		{
			why = "đường thăm dò không trúng hình khối";
			return null;
		}
		MinMax(list2, out var lo2, out var hi2);
		if (Math.Abs(hi2 - lo2 - width) > 1.0 || Math.Abs(hi - lo - thk) > 1.0)
		{
			why = "tiết diện đo được " + F(hi2 - lo2) + "x" + F(hi - lo) + " khác " + F(width) + "x" + F(thk);
			return null;
		}
		return new Offs
		{
			Lat = (lo2 + hi2) / 2.0,
			Z = hi - st.ZRef
		};
	}

	private void Shift(object beam, V2 n, double dLat, double dZ)
	{
		object target = A.Get(beam, "StartPoint");
		object target2 = A.Get(beam, "EndPoint");
		A.Set(beam, "StartPoint", A.Point(A.GetDouble(target, "X") + n.X * dLat, A.GetDouble(target, "Y") + n.Y * dLat, A.GetDouble(target, "Z") + dZ));
		A.Set(beam, "EndPoint", A.Point(A.GetDouble(target2, "X") + n.X * dLat, A.GetDouble(target2, "Y") + n.Y * dLat, A.GetDouble(target2, "Z") + dZ));
		A.Call(beam, "Modify");
	}

	private void Verify(MeshFramePlan plan, List<object> bars, List<object> strips)
	{
		try
		{
			A.Call(_model, "CommitChanges");
			int num = bars.Count + strips.Count;
			int num2 = 0;
			int num3 = 0;
			double num4 = 0.0;
			double num5 = 0.0;
			string text = null;
			for (int i = 0; i < num; i++)
			{
				bool flag = i < bars.Count;
				PlanBar planBar = (flag ? plan.Bars[i] : null);
				PlanStrip planStrip = (flag ? null : plan.Strips[i - bars.Count]);
				object beam = (flag ? bars[i] : strips[i - bars.Count]);
				V2 n = (flag ? (planBar.End - planBar.Start).Unit().Left() : (planStrip.End - planStrip.Start).Unit().Left());
				Offs offs = (flag ? MeasureBar(planBar, beam, out var why) : MeasureStrip(planStrip, beam, out why));
				if (offs == null)
				{
					if (text == null)
					{
						text = (flag ? "V " : "nẹp ") + (flag ? (i + 1) : (i - bars.Count + 1)) + ": " + why;
					}
					continue;
				}
				num2++;
				double num6 = Math.Max(Math.Abs(offs.Lat), Math.Abs(offs.Z));
				num4 = Math.Max(num4, num6);
				if (num6 > 0.05)
				{
					Shift(beam, n, 0.0 - offs.Lat, 0.0 - offs.Z);
					num3++;
					Offs offs2 = (flag ? MeasureBar(planBar, beam, out var why2) : MeasureStrip(planStrip, beam, out why2));
					num6 = ((offs2 == null) ? num6 : Math.Max(Math.Abs(offs2.Lat), Math.Abs(offs2.Z)));
				}
				num5 = Math.Max(num5, num6);
			}
			if (num2 == 0)
			{
				Messages.Add("Không đo lại được hình khối thật của các thanh vừa tạo (" + text + "): vị trí V / nẹp chưa được kiểm chứng.");
				return;
			}
			Notes.Add("Đo lại trên hình khối thật: " + num2 + "/" + num + " thanh; dời " + num3 + " thanh (lệch lớn nhất " + F(num4) + " mm, sau khi dời còn " + F(num5) + " mm).");
			if (num2 < num)
			{
				Messages.Add("Chưa đo được " + (num - num2) + " thanh (" + text + ").");
			}
			if (num5 > 0.5)
			{
				Messages.Add("Sau khi dời, một số thanh vẫn lệch tới " + F(num5) + " mm so với kế hoạch.");
			}
		}
		catch (Exception e)
		{
			Messages.Add("Không đo lại được hình khối (" + Root(e).GetType().Name + ": " + Root(e).Message + "): vị trí V / nẹp chưa được kiểm chứng.");
		}
	}

	private static string F(double d)
	{
		return d.ToString("0.##", CultureInfo.InvariantCulture);
	}

	private object NewBeam(V2 a, V2 b, double z)
	{
		return A.New(A.ModelType("Beam"), A.Point(a.X, a.Y, z), A.Point(b.X, b.Y, z));
	}

	private void Describe(object beam, string profile, string material, string name, string cls, string prefix, int startNo, string plane, string rotation, string depth)
	{
		A.Set(A.Get(beam, "Profile"), "ProfileString", profile);
		A.Set(A.Get(beam, "Material"), "MaterialString", material);
		A.Set(beam, "Name", name);
		A.Set(beam, "Class", cls);
		object target = A.Get(beam, "PartNumber");
		A.Set(target, "Prefix", prefix);
		A.Set(target, "StartNumber", startNo);
		object target2 = A.Get(beam, "Position");
		A.Set(target2, "Plane", A.Enum("Position+PlaneEnum", plane));
		A.Set(target2, "Rotation", A.Enum("Position+RotationEnum", rotation));
		A.Set(target2, "Depth", A.Enum("Position+DepthEnum", depth));
		if(P.AutoPlacement){
			// Tekla's rotated/tilted copy can leave an additional rotation angle on a part.
			// Automatic placement is calibrated at zero offsets; reusing that copy must use
			// the same basis rather than adding the inherited angle a second time.
			A.Set(target2,"RotationOffset",0.0);A.Set(target2,"PlaneOffset",0.0);A.Set(target2,"DepthOffset",0.0);
		}
	}

	private void AddMiter(object beam, V2 corner, V2? cutDir)
	{
		if (cutDir.HasValue)
		{
			V2 value = cutDir.Value;
			if (P.FlipMiterCut)
			{
				value *= -1.0;
			}
			object target = A.New(A.ModelType("Fitting"));
			A.Set(target, "Father", beam);
			object obj = A.New(A.ModelType("Plane"));
			A.Set(obj, "Origin", A.Point(corner.X, corner.Y, 0.0));
			A.Set(obj, "AxisX", A.Vector(value.X, value.Y, 0.0));
			A.Set(obj, "AxisY", A.Vector(0.0, 0.0, 1.0));
			A.Set(target, "Plane", obj);
			A.Require(target, "Insert", "tạo fitting đầu thanh");
			TeklaFrameStore.WriteId(A,target,FrameId);
		}
	}

	private void AddNotch(object beam, V2[] quad, double thk, string material)
	{
		object obj = A.New(A.ModelType("ContourPlate"));
		for (int i = 0; i < quad.Length; i++)
		{
			V2 v = quad[i];
			A.Call(obj, "AddContourPoint", A.New(A.ModelType("ContourPoint"), A.Point(v.X, v.Y, thk / 2.0), null));
		}
		A.Set(A.Get(obj, "Profile"), "ProfileString", "PL" + Num(thk));
		A.Set(A.Get(obj, "Material"), "MaterialString", material);
		A.Set(obj, "Class", A.Static(A.ModelType("BooleanPart"), "BooleanOperativeClassName"));
		A.Set(A.Get(obj, "Position"), "Depth", A.Enum("Position+DepthEnum", "MIDDLE"));
		A.Require(obj, "Insert", "tấm cắt khuyết");
		object target = A.New(A.ModelType("BooleanPart"));
		A.Set(target, "Father", beam);
		A.Call(target, "SetOperativePart", obj);
		object obj2 = A.Call(target, "Insert");
		if(!(obj2 is bool)|| (bool)obj2)TeklaFrameStore.WriteId(A,target,FrameId);
		if (obj2 is bool && !(bool)obj2)
		{
			Messages.Add("Không cắt được khuyết đầu V gia cường.");
		}
		Audit("DELETE_NOTCH_OPERATIVE id="+Id(obj));
		A.Require(obj, "Delete", "dọn dao cắt tạm");
	}

	private object InsertMesh(PlanMesh m, object father, V2[] frame)
	{
		if (m.Kind == MeshKind.Plate)
		{
			return InsertMeshPlate(m);
		}
		string text = null;
		text = TeklaCatalog.Check(A, m.RebarGrade, m.RebarSize);
		if (text == null)
		{
			try
			{
				return InsertRebarMesh(m, father, polygonType: true);
			}
			catch (Exception e)
			{
				text = "đa giác: " + Root(e).Message;
			}
			if (m.IsRectangle)
			{
				try
				{
					object rebar=InsertRebarMesh(m, father, polygonType: false);
					Messages.Add("RebarMesh dạng đa giác bị Tekla từ chối (" + text + "); đã tạo dạng chữ nhật.");
					return rebar;
				}
				catch (Exception e2)
				{
					text = text + " | chữ nhật: " + Root(e2).Message;
				}
			}
		}
		Messages.Add("RebarMesh không tạo được: " + text + " -> đã thay bằng lưới dạng tấm mỏng.");
		return InsertMeshPlate(m);
	}

	private object InsertMeshPlate(PlanMesh m)
	{
		double z = m.ZBottom + m.Thickness / 2.0;
		object obj = A.New(A.ModelType("ContourPlate"));
		V2[] polygon = m.Polygon;
		for (int i = 0; i < polygon.Length; i++)
		{
			V2 v = polygon[i];
			A.Call(obj, "AddContourPoint", A.New(A.ModelType("ContourPoint"), A.Point(v.X, v.Y, z), null));
		}
		A.Set(A.Get(obj, "Profile"), "ProfileString", "PL" + Num(m.Thickness));
		A.Set(A.Get(obj, "Material"), "MaterialString", m.Grade);
		A.Set(obj, "Name", m.Name);
		A.Set(obj, "Class", m.Class.ToString(CultureInfo.InvariantCulture));
		object target = A.Get(obj, "PartNumber");
		A.Set(target, "Prefix", m.Prefix);
		A.Set(target, "StartNumber", m.StartNo);
		A.Set(A.Get(obj, "Position"), "Depth", A.Enum("Position+DepthEnum", "MIDDLE"));
		A.Require(obj, "Insert", "tấm lưới (kiểm tra mác thép/profile PL" + Num(m.Thickness) + ")");
		return obj;
	}

	private object InsertRebarMesh(PlanMesh m, object father, bool polygonType)
	{
		double z = m.ZBottom + m.Thickness / 2.0;
		V2 v = m.Polygon[0];
		V2 v2 = m.Polygon[1];
		V2 b = (v2 - v).Unit();
		V2 b2 = b.Left();
		double num = double.MaxValue;
		double num2 = double.MinValue;
		double num3 = double.MaxValue;
		double num4 = double.MinValue;
		V2[] polygon = m.Polygon;
		foreach (V2 v3 in polygon)
		{
			double val = (v3 - v).Dot(b);
			double val2 = (v3 - v).Dot(b2);
			num = Math.Min(num, val);
			num2 = Math.Max(num2, val);
			num3 = Math.Min(num3, val2);
			num4 = Math.Max(num4, val2);
		}
		object target = A.New(A.ModelType("RebarMesh"));
		A.Set(target, "MeshType", A.Enum("RebarMesh+RebarMeshTypeEnum", polygonType ? "POLYGON_MESH" : "RECTANGULAR_MESH"));
		A.Set(target, "Father", father);
		if (polygonType)
		{
			object obj = A.New(A.ModelType("Polygon"));
			IList list = (IList)A.Get(obj, "Points");
			polygon = m.Polygon;
			for (int i = 0; i < polygon.Length; i++)
			{
				V2 v4 = polygon[i];
				list.Add(A.Point(v4.X, v4.Y, z));
			}
			A.Set(target, "Polygon", obj);
		}
		A.Set(target, "StartPoint", A.Point(v.X, v.Y, z));
		A.Set(target, "EndPoint", A.Point(v2.X, v2.Y, z));
		A.Set(target, "CatalogName", "MeshFrame");
		A.Set(target, "LongitudinalSize", m.RebarSize);
		A.Set(target, "CrossSize", m.RebarSize);
		A.Set(target, "LeftOverhangCross", 0.0);
		A.Set(target, "LeftOverhangLongitudinal", 0.0);
		A.Set(target, "RightOverhangCross", 0.0);
		A.Set(target, "RightOverhangLongitudinal", 0.0);
		A.Set(target, "LongitudinalSpacingMethod", A.Enum("RebarMesh+RebarMeshSpacingMethodEnum", "SPACING_TYPE_SAME_DISTANCE"));
		((IList)A.Get(target, "LongitudinalDistances")).Add(m.Pitch);
		((IList)A.Get(target, "CrossDistances")).Add(m.Pitch);
		A.Set(target, "Name", m.Name);
		A.Set(target, "Class", m.Class);
		object target2 = A.Get(target, "NumberingSeries");
		A.Set(target2, "Prefix", m.Prefix);
		A.Set(target2, "StartNumber", m.StartNo);
		A.Set(target, "Grade", m.RebarGrade);
		((IList)A.Get(target, "OnPlaneOffsets")).Add(0.0);
		A.Set(target, "FromPlaneOffset", 0.0);
		A.Set(target, "StartPointOffsetType", A.Enum("Reinforcement+RebarOffsetTypeEnum", "OFFSET_TYPE_COVER_THICKNESS"));
		A.Set(target, "StartPointOffsetValue", 0.0);
		A.Set(target, "CutByFatherPartCuts", false);
		A.Set(target, "CrossBarLocation", A.Enum("RebarMesh+RebarMeshCrossBarLocationEnum", "LOCATION_TYPE_ABOVE"));
		A.Set(target, "Length", num4 - num3);
		A.Set(target, "Width", num2 - num);
		A.Require(target, "Insert", "RebarMesh cỡ " + m.RebarSize + " mác " + m.RebarGrade);
		return target;
	}

	private static string Num(double d)
	{
		return d.ToString("0.###", CultureInfo.InvariantCulture);
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
