using System;
using System.Collections.Generic;
using System.Linq;

namespace MeshFrame.Tekla;

public static class TeklaCatalog
{
	public static List<RebarEntry> ReadRebars(TeklaApi api)
	{
		object target = api.New(api.CatalogType("CatalogHandler"));
		if (!(bool)api.Call(target, "GetConnectionStatus"))
		{
			throw new InvalidOperationException("Không kết nối được catalog Tekla.");
		}
		object target2 = api.Call(target, "GetRebarItems");
		List<RebarEntry> list = new List<RebarEntry>();
		while ((bool)api.Call(target2, "MoveNext"))
		{
			object target3 = api.Get(target2, "Current");
			list.Add(new RebarEntry
			{
				Grade = Convert.ToString(api.Get(target3, "Grade")),
				Size = Convert.ToString(api.Get(target3, "Size"))
			});
		}
		return list;
	}

	public static string Check(TeklaApi api, string grade, string size)
	{
		if (!api.HasCatalogs)
		{
			return null;
		}
		List<RebarEntry> list;
		try
		{
			list = ReadRebars(api);
		}
		catch (Exception)
		{
			return null;
		}
		if (list.Count == 0)
		{
			return null;
		}
		if (list.Any((RebarEntry e) => e.Grade == grade && e.Size == size))
		{
			return null;
		}
		List<string> list2 = list.Select((RebarEntry e) => e.Grade).Distinct().ToList();
		if (!list2.Contains(grade))
		{
			return "Mác '" + grade + "' không có trong catalog rebar của Tekla. Các mác có: " + Cut(list2) + ".";
		}
		List<string> items = (from e in list
			where e.Grade == grade
			select e.Size).Distinct().ToList();
		return "Cỡ sợi '" + size + "' không có cho mác '" + grade + "' trong catalog rebar. Các cỡ có: " + Cut(items) + ".";
	}

	private static string Cut(List<string> items)
	{
		return string.Join(", ", items.Take(25).ToArray()) + ((items.Count > 25) ? (", ... (" + items.Count + " mục)") : "");
	}
}
