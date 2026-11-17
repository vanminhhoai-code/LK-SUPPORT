// Đối chiếu mọi thành viên Tekla mà bộ ghi dùng với DLL THẬT của một bản Tekla (metadata, không chạy Tekla).
// Dùng: ContractCheck.exe <file thành viên> <thư mục có Tekla.Structures.dll + Tekla.Structures.Model.dll thật>
using System;
using System.IO;
using System.Reflection;
using MeshFrame.Tekla;

static class ContractCheck
{
    static int Main(string[] a)
    {
        string dir = a[1];
        AppDomain.CurrentDomain.AssemblyResolve += delegate (object s, ResolveEventArgs e)
        {
            string p = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        Assembly geo = Assembly.LoadFrom(Path.Combine(dir, "Tekla.Structures.dll"));
        Assembly model = Assembly.LoadFrom(Path.Combine(dir, "Tekla.Structures.Model.dll"));
        string catPath = Path.Combine(dir, "Tekla.Structures.Catalogs.dll");
        Assembly cat = File.Exists(catPath) ? Assembly.LoadFrom(catPath) : null;

        var bad = TeklaContract.Verify(File.ReadAllLines(a[0]), model, geo, cat);
        foreach (string b in bad) Console.WriteLine("MISSING: " + b);
        Console.WriteLine("contract " + Path.GetFileName(dir.TrimEnd('/', '\\')) + ": " + (File.ReadAllLines(a[0]).Length - bad.Count) + " ok, missing " + bad.Count);
        return bad.Count == 0 ? 0 : 1;
    }
}
