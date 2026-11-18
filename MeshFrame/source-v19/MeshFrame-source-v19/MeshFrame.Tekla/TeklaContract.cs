using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MeshFrame.Tekla;

public static class TeklaContract
{
	public sealed class Report
	{
		public List<string> Required = new List<string>();

		public List<string> Optional = new List<string>();
	}

	private static readonly string[] OptionalMarkers = new string[3] { "UI.Picker::PickPoint(System.String;Tekla.Structures.Geometry3d.Point)", "UI.GraphicsDrawer::", "UI.Color" };

	public static bool IsOptional(string line)
	{
		string[] optionalMarkers = OptionalMarkers;
		foreach (string value in optionalMarkers)
		{
			if (line.Contains(value))
			{
				return true;
			}
		}
		return false;
	}

	public static Report VerifyLoaded(TeklaApi api)
	{
		Report report = new Report();
		foreach (string item in Verify("call Tekla.Structures.Catalogs.CatalogHandler::GetConnectionStatus()\ncall Tekla.Structures.Catalogs.CatalogHandler::GetRebarItems()\ncall Tekla.Structures.Catalogs.RebarItemEnumerator::MoveNext()\ncall Tekla.Structures.Geometry3d.Matrix::Transform(Tekla.Structures.Geometry3d.Point)\ncall Tekla.Structures.Model.Assembly::Add(Tekla.Structures.Model.Beam>Tekla.Structures.Model.Part>Tekla.Structures.Model.ModelObject>Tekla.Structures.Model.IAssemblable)\ncall Tekla.Structures.Model.Assembly::Add(Tekla.Structures.Model.ContourPlate>Tekla.Structures.Model.Part>Tekla.Structures.Model.ModelObject>Tekla.Structures.Model.IAssemblable)\ncall Tekla.Structures.Model.Assembly::GetMainPart()\ncall Tekla.Structures.Model.Assembly::GetSecondaries()\ncall Tekla.Structures.Model.Assembly::Modify()\ncall Tekla.Structures.Model.Beam::Delete()\ncall Tekla.Structures.Model.Beam::GetAssembly()\ncall Tekla.Structures.Model.Beam::GetBooleans()\ncall Tekla.Structures.Model.Beam::GetReinforcements()\ncall Tekla.Structures.Model.Beam::GetSolid()\ncall Tekla.Structures.Model.Beam::GetUserProperty(System.String;System.String&)\ncall Tekla.Structures.Model.Beam::Insert()\ncall Tekla.Structures.Model.Beam::Modify()\ncall Tekla.Structures.Model.Beam::SetUserProperty(System.String;System.String)\ncall Tekla.Structures.Model.BooleanPart::Delete()\ncall Tekla.Structures.Model.BooleanPart::Insert()\ncall Tekla.Structures.Model.BooleanPart::SetOperativePart(Tekla.Structures.Model.ContourPlate>Tekla.Structures.Model.Part>Tekla.Structures.Model.ModelObject>Tekla.Structures.Model.IAssemblable)\ncall Tekla.Structures.Model.ContourPlate::AddContourPoint(Tekla.Structures.Model.ContourPoint)\ncall Tekla.Structures.Model.ContourPlate::Delete()\ncall Tekla.Structures.Model.ContourPlate::Insert()\ncall Tekla.Structures.Model.ContourPlate::SetUserProperty(System.String;System.String)\ncall Tekla.Structures.Model.Fitting::Delete()\ncall Tekla.Structures.Model.Fitting::Insert()\ncall Tekla.Structures.Model.Model::CommitChanges()\ncall Tekla.Structures.Model.Model::GetInfo()\ncall Tekla.Structures.Model.Model::GetWorkPlaneHandler()\ncall Tekla.Structures.Model.ModelObjectEnumerator::MoveNext()\ncall Tekla.Structures.Model.RebarMesh::Delete()\ncall Tekla.Structures.Model.RebarMesh::Insert()\ncall Tekla.Structures.Model.Solid::Intersect(Tekla.Structures.Geometry3d.Point;Tekla.Structures.Geometry3d.Point)\ncall Tekla.Structures.Model.UI.GraphicsDrawer::DrawLineSegment(Tekla.Structures.Geometry3d.Point;Tekla.Structures.Geometry3d.Point;Tekla.Structures.Model.UI.Color)\ncall Tekla.Structures.Model.UI.GraphicsDrawer::DrawText(Tekla.Structures.Geometry3d.Point;System.String;Tekla.Structures.Model.UI.Color)\ncall Tekla.Structures.Model.UI.GraphicsDrawer::RemoveTemporaryGraphicsObjects(System.Collections.Generic.List`1[[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]])\ncall Tekla.Structures.Model.UI.ModelObjectSelector::GetSelectedObjects()\ncall Tekla.Structures.Model.UI.Picker::PickPoint(System.String)\ncall Tekla.Structures.Model.UI.Picker::PickPoint(System.String;Tekla.Structures.Geometry3d.Point)\ncall Tekla.Structures.Model.WorkPlaneHandler::GetCurrentTransformationPlane()\ncall Tekla.Structures.Model.WorkPlaneHandler::SetCurrentTransformationPlane(Tekla.Structures.Model.TransformationPlane)\nenum Tekla.Structures.Model.Position+DepthEnum::BEHIND\nenum Tekla.Structures.Model.Position+DepthEnum::FRONT\nenum Tekla.Structures.Model.Position+DepthEnum::MIDDLE\nenum Tekla.Structures.Model.Position+PlaneEnum::LEFT\nenum Tekla.Structures.Model.Position+PlaneEnum::MIDDLE\nenum Tekla.Structures.Model.Position+PlaneEnum::RIGHT\nenum Tekla.Structures.Model.Position+RotationEnum::BACK\nenum Tekla.Structures.Model.Position+RotationEnum::BELOW\nenum Tekla.Structures.Model.Position+RotationEnum::FRONT\nenum Tekla.Structures.Model.Position+RotationEnum::TOP\nenum Tekla.Structures.Model.RebarMesh+RebarMeshCrossBarLocationEnum::LOCATION_TYPE_ABOVE\nenum Tekla.Structures.Model.RebarMesh+RebarMeshSpacingMethodEnum::SPACING_TYPE_SAME_DISTANCE\nenum Tekla.Structures.Model.RebarMesh+RebarMeshTypeEnum::POLYGON_MESH\nenum Tekla.Structures.Model.RebarMesh+RebarMeshTypeEnum::RECTANGULAR_MESH\nenum Tekla.Structures.Model.Reinforcement+RebarOffsetTypeEnum::OFFSET_TYPE_COVER_THICKNESS\nget Tekla.Structures.Catalogs.RebarItem::Grade\nget Tekla.Structures.Catalogs.RebarItem::Size\nget Tekla.Structures.Catalogs.RebarItemEnumerator::Current\nget Tekla.Structures.Geometry3d.Point::X\nget Tekla.Structures.Geometry3d.Point::Y\nget Tekla.Structures.Geometry3d.Point::Z\nget Tekla.Structures.Model.Assembly::AssemblyNumber\nget Tekla.Structures.Model.Beam::EndPoint\nget Tekla.Structures.Model.Beam::Material\nget Tekla.Structures.Model.Beam::PartNumber\nget Tekla.Structures.Model.Beam::Position\nget Tekla.Structures.Model.Beam::Profile\nget Tekla.Structures.Model.Beam::StartPoint\nget Tekla.Structures.Model.ContourPlate::Material\nget Tekla.Structures.Model.ContourPlate::PartNumber\nget Tekla.Structures.Model.ContourPlate::Position\nget Tekla.Structures.Model.ContourPlate::Profile\nget Tekla.Structures.Model.ModelInfo::ModelPath\nget Tekla.Structures.Model.ModelObjectEnumerator::Current\nget Tekla.Structures.Model.Polygon::Points\nget Tekla.Structures.Model.RebarMesh::CrossDistances\nget Tekla.Structures.Model.RebarMesh::LongitudinalDistances\nget Tekla.Structures.Model.RebarMesh::NumberingSeries\nget Tekla.Structures.Model.RebarMesh::OnPlaneOffsets\nget Tekla.Structures.Model.Solid::MaximumPoint\nget Tekla.Structures.Model.Solid::MinimumPoint\nget Tekla.Structures.Model.TransformationPlane::TransformationMatrixToGlobal\nnew Tekla.Structures.Catalogs.CatalogHandler()\nnew Tekla.Structures.Geometry3d.Point(System.Double;System.Double;System.Double)\nnew Tekla.Structures.Geometry3d.Vector(System.Double;System.Double;System.Double)\nnew Tekla.Structures.Model.Beam(Tekla.Structures.Geometry3d.Point;Tekla.Structures.Geometry3d.Point)\nnew Tekla.Structures.Model.BooleanPart()\nnew Tekla.Structures.Model.ContourPlate()\nnew Tekla.Structures.Model.ContourPoint(Tekla.Structures.Geometry3d.Point;null)\nnew Tekla.Structures.Model.Fitting()\nnew Tekla.Structures.Model.Model()\nnew Tekla.Structures.Model.Plane()\nnew Tekla.Structures.Model.Polygon()\nnew Tekla.Structures.Model.RebarMesh()\nnew Tekla.Structures.Model.TransformationPlane(Tekla.Structures.Geometry3d.Point;Tekla.Structures.Geometry3d.Vector;Tekla.Structures.Geometry3d.Vector)\nnew Tekla.Structures.Model.UI.Color(System.Double;System.Double;System.Double)\nnew Tekla.Structures.Model.UI.GraphicsDrawer()\nnew Tekla.Structures.Model.UI.ModelObjectSelector()\nnew Tekla.Structures.Model.UI.Picker()\nset Tekla.Structures.Model.Beam::Class=System.String\nset Tekla.Structures.Model.Beam::EndPoint=Tekla.Structures.Geometry3d.Point\nset Tekla.Structures.Model.Beam::Name=System.String\nset Tekla.Structures.Model.Beam::StartPoint=Tekla.Structures.Geometry3d.Point\nset Tekla.Structures.Model.BooleanPart::Father=Tekla.Structures.Model.Beam>Tekla.Structures.Model.Part>Tekla.Structures.Model.ModelObject>Tekla.Structures.Model.IAssemblable\nset Tekla.Structures.Model.ContourPlate::Class=System.String\nset Tekla.Structures.Model.ContourPlate::Name=System.String\nset Tekla.Structures.Model.Fitting::Father=Tekla.Structures.Model.Beam>Tekla.Structures.Model.Part>Tekla.Structures.Model.ModelObject>Tekla.Structures.Model.IAssemblable\nset Tekla.Structures.Model.Fitting::Plane=Tekla.Structures.Model.Plane\nset Tekla.Structures.Model.Material::MaterialString=System.String\nset Tekla.Structures.Model.NumberingSeries::Prefix=System.String\nset Tekla.Structures.Model.NumberingSeries::StartNumber=System.Int32\nset Tekla.Structures.Model.Plane::AxisX=Tekla.Structures.Geometry3d.Vector\nset Tekla.Structures.Model.Plane::AxisY=Tekla.Structures.Geometry3d.Vector\nset Tekla.Structures.Model.Plane::Origin=Tekla.Structures.Geometry3d.Point\nset Tekla.Structures.Model.Position::Depth=Tekla.Structures.Model.Position+DepthEnum\nset Tekla.Structures.Model.Position::Plane=Tekla.Structures.Model.Position+PlaneEnum\nset Tekla.Structures.Model.Position::Rotation=Tekla.Structures.Model.Position+RotationEnum\nset Tekla.Structures.Model.Profile::ProfileString=System.String\nset Tekla.Structures.Model.RebarMesh::CatalogName=System.String\nset Tekla.Structures.Model.RebarMesh::Class=System.Int32\nset Tekla.Structures.Model.RebarMesh::CrossBarLocation=Tekla.Structures.Model.RebarMesh+RebarMeshCrossBarLocationEnum\nset Tekla.Structures.Model.RebarMesh::CrossSize=System.String\nset Tekla.Structures.Model.RebarMesh::CutByFatherPartCuts=System.Boolean\nset Tekla.Structures.Model.RebarMesh::EndPoint=Tekla.Structures.Geometry3d.Point\nset Tekla.Structures.Model.RebarMesh::Father=Tekla.Structures.Model.Beam>Tekla.Structures.Model.Part>Tekla.Structures.Model.ModelObject>Tekla.Structures.Model.IAssemblable\nset Tekla.Structures.Model.RebarMesh::FromPlaneOffset=System.Double\nset Tekla.Structures.Model.RebarMesh::Grade=System.String\nset Tekla.Structures.Model.RebarMesh::LeftOverhangCross=System.Double\nset Tekla.Structures.Model.RebarMesh::LeftOverhangLongitudinal=System.Double\nset Tekla.Structures.Model.RebarMesh::Length=System.Double\nset Tekla.Structures.Model.RebarMesh::LongitudinalSize=System.String\nset Tekla.Structures.Model.RebarMesh::LongitudinalSpacingMethod=Tekla.Structures.Model.RebarMesh+RebarMeshSpacingMethodEnum\nset Tekla.Structures.Model.RebarMesh::MeshType=Tekla.Structures.Model.RebarMesh+RebarMeshTypeEnum\nset Tekla.Structures.Model.RebarMesh::Name=System.String\nset Tekla.Structures.Model.RebarMesh::Polygon=Tekla.Structures.Model.Polygon\nset Tekla.Structures.Model.RebarMesh::RightOverhangCross=System.Double\nset Tekla.Structures.Model.RebarMesh::RightOverhangLongitudinal=System.Double\nset Tekla.Structures.Model.RebarMesh::StartPoint=Tekla.Structures.Geometry3d.Point\nset Tekla.Structures.Model.RebarMesh::StartPointOffsetType=Tekla.Structures.Model.Reinforcement+RebarOffsetTypeEnum\nset Tekla.Structures.Model.RebarMesh::StartPointOffsetValue=System.Double\nset Tekla.Structures.Model.RebarMesh::Width=System.Double\nstatic Tekla.Structures.Model.BooleanPart::BooleanOperativeClassName\n".Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), api.ModelAssembly, api.GeometryAssembly, api.CatalogAssembly))
		{
			(IsOptional(item) ? report.Optional : report.Required).Add(item);
		}
		return report;
	}

	public static List<string> Verify(IEnumerable<string> lines, Assembly model, Assembly geo, Assembly cat)
	{
		List<string> list = new List<string>();
		foreach (string line in lines)
		{
			if (line.Length != 0)
			{
				string text = CheckLine(line, model, geo, cat);
				if (text != null)
				{
					list.Add(line + "   <-- " + text);
				}
			}
		}
		return list;
	}

	private static Type Find(string full, Assembly model, Assembly geo, Assembly cat)
	{
		object obj = model.GetType(full);
		if (obj == null)
		{
			obj = geo.GetType(full);
			if (obj == null)
			{
				if (!(cat == null))
				{
					return cat.GetType(full);
				}
				obj = null;
			}
		}
		return (Type)obj;
	}

	private static bool Compat(Type param, string chain)
	{
		if (chain == "null")
		{
			return !param.IsValueType;
		}
		string[] array = chain.Split('>');
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] == param.FullName)
			{
				return true;
			}
		}
		if (param == typeof(object))
		{
			return true;
		}
		if (chain.StartsWith("Tekla."))
		{
			return false;
		}
		Type type = Type.GetType(chain);
		if (type == null)
		{
			return false;
		}
		if (param.IsAssignableFrom(type))
		{
			return true;
		}
		if (param.IsEnum)
		{
			return type == typeof(string);
		}
		if (param.IsPrimitive || param == typeof(string))
		{
			return typeof(IConvertible).IsAssignableFrom(type);
		}
		return false;
	}

	private static bool ParamsMatch(ParameterInfo[] ps, string[] chains)
	{
		if (ps.Length != chains.Length)
		{
			return false;
		}
		for (int i = 0; i < ps.Length; i++)
		{
			bool flag = chains[i].EndsWith("&");
			if (ps[i].ParameterType.IsByRef != flag)
			{
				return false;
			}
			if (!Compat(ps[i].ParameterType.IsByRef ? ps[i].ParameterType.GetElementType() : ps[i].ParameterType, chains[i].TrimEnd('&')))
			{
				return false;
			}
		}
		return true;
	}

	private static string CheckLine(string line, Assembly model, Assembly geo, Assembly cat)
	{
		int num = line.IndexOf(' ');
		string text = line.Substring(0, num);
		string text2 = line.Substring(num + 1);
		try
		{
			switch (text)
			{
			case "new":
			{
				int num6 = text2.IndexOf('(');
				string text8 = text2.Substring(0, num6);
				string[] ch = text2.Substring(num6 + 1, text2.Length - num6 - 2).Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				Type type6 = Find(text8, model, geo, cat);
				if (type6 == null)
				{
					return text8.StartsWith("Tekla.") ? "không có kiểu" : null;
				}
				if (!type6.GetConstructors().Any((ConstructorInfo c) => ParamsMatch(c.GetParameters(), ch)))
				{
					return "không có hàm tạo (" + string.Join(", ", ch) + ")";
				}
				return null;
			}
			case "enum":
			{
				int num4 = text2.IndexOf("::");
				Type type4 = Find(text2.Substring(0, num4), model, geo, cat);
				if (type4 == null || !type4.IsEnum)
				{
					return "không có enum";
				}
				return Enum.GetNames(type4).Contains(text2.Substring(num4 + 2)) ? null : "không có giá trị";
			}
			case "static":
			{
				int num5 = text2.IndexOf("::");
				Type type5 = Find(text2.Substring(0, num5), model, geo, cat);
				return (type5 == null || type5.GetField(text2.Substring(num5 + 2), BindingFlags.Static | BindingFlags.Public) == null) ? "không có field tĩnh" : null;
			}
			default:
			{
				int num2 = text2.IndexOf("::");
				string text3 = text2.Substring(0, num2);
				string text4 = text2.Substring(num2 + 2);
				Type type = Find(text3, model, geo, cat);
				if (type == null)
				{
					return text3.StartsWith("Tekla.") ? "không có kiểu" : null;
				}
				if (text == "get" || text == "set")
				{
					string text5 = ((text == "set") ? text4.Substring(0, text4.IndexOf('=')) : text4);
					PropertyInfo propertyInfo = null;
					FieldInfo fieldInfo = null;
					Type type2 = type;
					while (type2 != null && propertyInfo == null && fieldInfo == null)
					{
						propertyInfo = type2.GetProperty(text5, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
						if (propertyInfo == null)
						{
							fieldInfo = type2.GetField(text5, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
						}
						type2 = type2.BaseType;
					}
					if (propertyInfo == null && fieldInfo == null)
					{
						return "không có thuộc tính/field " + text5;
					}
					string text6 = ((text == "set") ? text4.Substring(text4.IndexOf('=') + 1) : null);
					if (propertyInfo != null)
					{
						if (text == "get" && !propertyInfo.CanRead)
						{
							return "thuộc tính không đọc được";
						}
						if (text == "set")
						{
							if (!propertyInfo.CanWrite)
							{
								return "thuộc tính chỉ đọc";
							}
							if (!Compat(propertyInfo.PropertyType, text6))
							{
								return "kiểu " + propertyInfo.PropertyType.FullName + " không nhận " + text6;
							}
						}
					}
					else if (text == "set" && !Compat(fieldInfo.FieldType, text6))
					{
						return "kiểu " + fieldInfo.FieldType.FullName + " không nhận " + text6;
					}
					return null;
				}
				int num3 = text4.IndexOf('(');
				string text7 = text4.Substring(0, num3);
				string[] array = text4.Substring(num3 + 1, text4.Length - num3 - 2).Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				Type type3 = type;
				while (type3 != null)
				{
					MethodInfo[] methods = type3.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
					foreach (MethodInfo methodInfo in methods)
					{
						if (methodInfo.Name == text7 && ParamsMatch(methodInfo.GetParameters(), array))
						{
							return null;
						}
					}
					type3 = type3.BaseType;
				}
				return "không có hàm " + text7 + "(" + string.Join(", ", array) + ")";
			}
			}
		}
		catch (Exception ex)
		{
			return ex.GetType().Name + ": " + ex.Message.Split('\n')[0];
		}
	}
}
