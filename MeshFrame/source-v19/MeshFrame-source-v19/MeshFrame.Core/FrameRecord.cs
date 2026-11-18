using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MeshFrame.Core;

public sealed class FrameRecord
{
	public string Id;

	public MeshFrameSettings Settings = new MeshFrameSettings();

	public V3[] Corners = new V3[0];
	public List<int> ObjectIds = new List<int>();
	public V3[] NativeReference = new V3[0];
	public Dictionary<string,V3[]> NativeBars = new Dictionary<string,V3[]>();

	public static string NewId()
	{
		return Guid.NewGuid().ToString("N");
	}

	public void Save(string path)
	{
		List<string> list = new List<string>
		{
			"@id=" + Id,
			"@n=" + Corners.Length
		};
		for (int i = 0; i < Corners.Length; i++)
		{
			list.Add("@p" + i + "=" + string.Join(";", Fmt(Corners[i].X), Fmt(Corners[i].Y), Fmt(Corners[i].Z)));
		}
		list.Add("@objects="+string.Join(",",ObjectIds));
		for(int i=0;i<NativeReference.Length;i++)list.Add("@ref"+i+"="+PointText(NativeReference[i]));
		foreach(var pair in NativeBars)list.Add("@bar."+pair.Key+"="+PointText(pair.Value[0])+";"+PointText(pair.Value[1]));
		list.AddRange(SettingsStore.ToLines(Settings));
		string directoryName = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		File.WriteAllLines(path, list.ToArray());
	}

	public static FrameRecord Load(string path)
	{
		if (!File.Exists(path))
		{
			return null;
		}
		string[] array = File.ReadAllLines(path);
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		string[] array2 = array;
		foreach (string text in array2)
		{
			int num = text.IndexOf('=');
			if (num > 0)
			{
				dictionary[text.Substring(0, num)] = text.Substring(num + 1);
			}
		}
		if (!dictionary.ContainsKey("@id") || !dictionary.ContainsKey("@n") || !int.TryParse(dictionary["@n"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || result < 3)
		{
			return null;
		}
		FrameRecord frameRecord = new FrameRecord
		{
			Id = dictionary["@id"],
			Settings = SettingsStore.FromLines(array),
			Corners = new V3[result]
		};
		string ids;
		if(dictionary.TryGetValue("@objects",out ids))foreach(string id in ids.Split(',')){int n;if(int.TryParse(id,out n)&&n>0)frameRecord.ObjectIds.Add(n);}
		if(dictionary.ContainsKey("@ref0")&&dictionary.ContainsKey("@ref1")&&dictionary.ContainsKey("@ref2")) {
			frameRecord.NativeReference=new V3[3];for(int i=0;i<3;i++){var p=dictionary["@ref"+i].Split(';');V3 v;if(!TryPoint(p,0,out v))return null;frameRecord.NativeReference[i]=v;}
		}
		foreach(var pair in dictionary)if(pair.Key.StartsWith("@bar.")){var p=pair.Value.Split(';');V3 s,e;if(!TryPoint(p,0,out s)||!TryPoint(p,3,out e))return null;frameRecord.NativeBars[pair.Key.Substring(5)]=new[]{s,e};}
		for (int j = 0; j < result; j++)
		{
			if (!dictionary.TryGetValue("@p" + j, out var value))
			{
				return null;
			}
			string[] array3 = value.Split(';');
			if (array3.Length != 3 || !double.TryParse(array3[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) || !double.TryParse(array3[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3) || !double.TryParse(array3[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result4))
			{
				return null;
			}
			frameRecord.Corners[j] = new V3(result2, result3, result4);
		}
		return frameRecord;
	}

	private static string Fmt(double d)
	{
		return d.ToString("R", CultureInfo.InvariantCulture);
	}
	private static string PointText(V3 p){return Fmt(p.X)+";"+Fmt(p.Y)+";"+Fmt(p.Z);}
	private static bool TryPoint(string[] p,int start,out V3 v){v=new V3();double x,y,z;if(p.Length<start+3||!double.TryParse(p[start],NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!double.TryParse(p[start+1],NumberStyles.Float,CultureInfo.InvariantCulture,out y)||!double.TryParse(p[start+2],NumberStyles.Float,CultureInfo.InvariantCulture,out z))return false;v=new V3(x,y,z);return true;}
}
