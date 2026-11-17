// Đối chiếu mọi thành viên Tekla mà bộ ghi dùng với DLL THẬT của một bản Tekla (metadata, không chạy Tekla).
// Dùng: ContractCheck.exe <file thành viên> <thư mục có Tekla.Structures.dll + Tekla.Structures.Model.dll thật>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

static class ContractCheck
{
    static Assembly model, geo;

    static Type Find(string full)
    {
        return model.GetType(full) ?? geo.GetType(full);
    }

    static bool Compat(Type param, string chain)
    {
        if (chain == "null") return !param.IsValueType;
        foreach (string n in chain.Split('>')) if (n == param.FullName) return true;
        if (param == typeof(object)) return true;
        if (chain.StartsWith("Tekla.")) return false;
        Type st = Type.GetType(chain);
        if (st == null) return false;
        if (param.IsAssignableFrom(st)) return true;
        if (param.IsEnum) return st == typeof(string);
        return (param.IsPrimitive || param == typeof(string)) && typeof(IConvertible).IsAssignableFrom(st);
    }

    static bool ParamsMatch(ParameterInfo[] ps, string[] chains)
    {
        if (ps.Length != chains.Length) return false;
        for (int i = 0; i < ps.Length; i++) if (ps[i].ParameterType.IsByRef || !Compat(ps[i].ParameterType, chains[i])) return false;
        return true;
    }

    static int Main(string[] a)
    {
        string dir = a[1];
        AppDomain.CurrentDomain.AssemblyResolve += delegate (object s, ResolveEventArgs e)
        {
            string p = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        geo = Assembly.LoadFrom(Path.Combine(dir, "Tekla.Structures.dll"));
        model = Assembly.LoadFrom(Path.Combine(dir, "Tekla.Structures.Model.dll"));

        int ok = 0; var bad = new List<string>();
        foreach (string line in File.ReadAllLines(a[0]))
        {
            if (line.Length == 0) continue;
            int sp = line.IndexOf(' ');
            string kind = line.Substring(0, sp), rest = line.Substring(sp + 1);
            if (!rest.StartsWith("Tekla.") && !rest.StartsWith("System.Collections") ) { }
            string err = null;
            try
            {
                if (kind == "new")
                {
                    int lp = rest.IndexOf('('); string tn = rest.Substring(0, lp);
                    string[] ch = rest.Substring(lp + 1, rest.Length - lp - 2).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    Type t = Find(tn);
                    if (t == null) { if (tn.StartsWith("Tekla.")) err = "không có kiểu"; }
                    else if (!t.GetConstructors().Any(c => ParamsMatch(c.GetParameters(), ch))) err = "không có hàm tạo (" + string.Join(", ", ch) + ")";
                }
                else if (kind == "enum")
                {
                    int i = rest.IndexOf("::"); Type t = Find(rest.Substring(0, i));
                    if (t == null || !t.IsEnum) err = "không có enum"; else if (!Enum.GetNames(t).Contains(rest.Substring(i + 2))) err = "không có giá trị";
                }
                else
                {
                    int i = rest.IndexOf("::"); string tn = rest.Substring(0, i); string m = rest.Substring(i + 2);
                    Type t = Find(tn);
                    if (t == null) { if (tn.StartsWith("Tekla.")) err = "không có kiểu"; }
                    else if (kind == "get" || kind == "set")
                    {
                        string name = kind == "set" ? m.Substring(0, m.IndexOf('=')) : m;
                        PropertyInfo p = null; FieldInfo fi = null;
                        for (Type l = t; l != null && p == null && fi == null; l = l.BaseType)
                        {
                            p = l.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            if (p == null) fi = l.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        }
                        if (p == null && fi != null)
                        {
                            if (kind == "set" && !Compat(fi.FieldType, m.Substring(m.IndexOf('=') + 1))) err = "kiểu " + fi.FieldType.FullName + " không nhận " + m.Substring(m.IndexOf('=') + 1);
                        }
                        else if (p == null) err = "không có thuộc tính/field " + name;
                        else if (kind == "get" && !p.CanRead) err = "thuộc tính không đọc được";
                        else if (kind == "set")
                        {
                            if (!p.CanWrite) err = "thuộc tính chỉ đọc";
                            else if (!Compat(p.PropertyType, m.Substring(m.IndexOf('=') + 1))) err = "kiểu " + p.PropertyType.FullName + " không nhận " + m.Substring(m.IndexOf('=') + 1);
                        }
                    }
                    else // call
                    {
                        int lp = m.IndexOf('('); string name = m.Substring(0, lp);
                        string[] ch = m.Substring(lp + 1, m.Length - lp - 2).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        bool found = false;
                        for (Type l = t; l != null && !found; l = l.BaseType)
                            foreach (MethodInfo mi in l.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                                if (mi.Name == name && ParamsMatch(mi.GetParameters(), ch)) { found = true; break; }
                        if (!found) err = "không có hàm " + name + "(" + string.Join(", ", ch) + ")";
                    }
                }
            }
            catch (Exception e) { err = e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
            if (err == null) ok++; else bad.Add(line + "   <-- " + err);
        }
        foreach (string b in bad) Console.WriteLine("MISSING: " + b);
        Console.WriteLine("contract " + Path.GetFileName(dir.TrimEnd('/', '\\')) + ": ok " + ok + ", missing " + bad.Count);
        return bad.Count == 0 ? 0 : 1;
    }
}
