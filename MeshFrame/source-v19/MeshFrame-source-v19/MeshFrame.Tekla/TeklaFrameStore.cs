using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace MeshFrame.Tekla;

public static class TeklaFrameStore
{
	public const string IdKey = "MESHFRAME_ID";

	public const string KeyKey = "MESHFRAME_KEY";

	public static string ModelFolder(TeklaApi api, object model)
	{
		object target = api.Call(model, "GetInfo");
		return Convert.ToString(api.Get(target, "ModelPath")) ?? "";
	}

	public static string RecordPath(string modelFolder, string id)
	{
		return Path.Combine(modelFolder, "MeshFrame", id + ".mfr");
	}

	public static void WriteId(TeklaApi api, object mainPart, string id)
	{
		api.Call(mainPart, "SetUserProperty", "MESHFRAME_ID", id);
	}

	public static void WriteKey(TeklaApi api, object part, string key)
	{
		api.Call(part, "SetUserProperty", "MESHFRAME_KEY", key);
	}

	public static string ReadKey(TeklaApi api, object obj)
	{
		return ReadProp(api, obj, "MESHFRAME_KEY");
	}

	public static string ReadId(TeklaApi api, object obj)
	{
		return ReadProp(api, obj, "MESHFRAME_ID");
	}

	private static string ReadProp(TeklaApi api, object obj, string name)
	{
		try
		{
			return Convert.ToString(api.CallRef(obj, "GetUserProperty", name, "")[1]) ?? "";
		}
		catch (MissingMethodException)
		{
			return "";
		}
	}

	public static object ResolveMain(TeklaApi api,object obj)
	{
		if(obj==null)return null;
		if(api.ModelType("Reinforcement").IsInstanceOfType(obj))obj=api.Get(obj,"Father");
		if(obj==null)return null;
		object asm=api.Call(obj,"GetAssembly");
		return asm==null?null:api.Call(asm,"GetMainPart");
	}

	public static string FindSelected(TeklaApi api, out object mainPart)
	{
		mainPart=null;
		object en=api.Call(api.New(api.ModelType("UI.ModelObjectSelector")),"GetSelectedObjects");
		while((bool)api.Call(en,"MoveNext")) {
			object obj=api.Get(en,"Current");
			if(obj==null)continue;
			try {
				object main=ResolveMain(api,obj);
				if(main==null)continue;
				string id=ReadId(api,main);
				if(id.Length>0){mainPart=main;return id;}
			} catch(MissingMethodException) { }
		}
		return "";
	}
}
