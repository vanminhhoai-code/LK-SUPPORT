// SDK GIẢ - Tekla.Structures.Catalogs.dll giả: catalog rebar để test kiểm tra mác / cỡ sợi.
using System.Collections.Generic;

namespace Tekla.Structures.Catalogs
{
    public static class FakeCatalog
    {
        public static readonly List<string[]> Items = new List<string[]>();
        public static bool Connected = true;
        public static void Reset() { Items.Clear(); Items.Add(new[] { "SUS304", "0.37" }); Items.Add(new[] { "B500B", "6" }); Items.Add(new[] { "B500B", "8" }); Connected = true; }
    }
    public class RebarItem { public string Grade { get; set; } public string Size { get; set; } }
    public class RebarItemEnumerator
    {
        private int _i = -1;
        public bool MoveNext() { _i++; return _i < FakeCatalog.Items.Count; }
        public RebarItem Current { get { return new RebarItem { Grade = FakeCatalog.Items[_i][0], Size = FakeCatalog.Items[_i][1] }; } }
        public int GetSize() { return FakeCatalog.Items.Count; }
    }
    public class CatalogHandler
    {
        public bool GetConnectionStatus() { return FakeCatalog.Connected; }
        public RebarItemEnumerator GetRebarItems() { return new RebarItemEnumerator(); }
    }
}
