// Đọc catalog rebar của Tekla (CatalogHandler.GetRebarItems) để kiểm tra mác thép / cỡ sợi TRƯỚC khi tạo RebarMesh:
// Tekla chỉ nhận mác và cỡ sợi có trong catalog rebar của model.
using System;
using System.Collections.Generic;
using System.Linq;

namespace MeshFrame.Tekla
{
    public sealed class RebarEntry { public string Grade, Size; }

    public static class TeklaCatalog
    {
        /// <summary>Danh sách (mác, cỡ sợi) trong catalog rebar. Ném lỗi nếu không đọc được.</summary>
        public static List<RebarEntry> ReadRebars(TeklaApi api)
        {
            object handler = api.New(api.CatalogType("CatalogHandler"));
            if (!(bool)api.Call(handler, "GetConnectionStatus")) throw new InvalidOperationException("Không kết nối được catalog Tekla.");
            object en = api.Call(handler, "GetRebarItems");
            var list = new List<RebarEntry>();
            while ((bool)api.Call(en, "MoveNext"))
            {
                object it = api.Get(en, "Current");
                list.Add(new RebarEntry { Grade = Convert.ToString(api.Get(it, "Grade")), Size = Convert.ToString(api.Get(it, "Size")) });
            }
            return list;
        }

        /// <summary>null = hợp lệ (hoặc không đọc được catalog nên bỏ qua). Ngược lại: thông báo nêu rõ cái gì không có và cái gì có.</summary>
        public static string Check(TeklaApi api, string grade, string size)
        {
            if (!api.HasCatalogs) return null;
            List<RebarEntry> all;
            try { all = ReadRebars(api); }
            catch (Exception) { return null; }
            if (all.Count == 0) return null;
            if (all.Any(e => e.Grade == grade && e.Size == size)) return null;
            var grades = all.Select(e => e.Grade).Distinct().ToList();
            if (!grades.Contains(grade))
                return "Mác '" + grade + "' không có trong catalog rebar của Tekla. Các mác có: " + Cut(grades) + ".";
            var sizes = all.Where(e => e.Grade == grade).Select(e => e.Size).Distinct().ToList();
            return "Cỡ sợi '" + size + "' không có cho mác '" + grade + "' trong catalog rebar. Các cỡ có: " + Cut(sizes) + ".";
        }

        private static string Cut(List<string> items)
        {
            const int max = 25;
            return string.Join(", ", items.Take(max).ToArray()) + (items.Count > max ? ", ... (" + items.Count + " mục)" : "");
        }
    }
}
