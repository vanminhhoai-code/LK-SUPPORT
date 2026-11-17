// Đối chiếu danh sách thành viên Tekla mà MeshFrame dùng (ApiMembers) với DLL Tekla ĐANG NẠP (reflection, không chạy Tekla).
// Dùng ở 2 nơi: (1) ci/ContractCheck đối chiếu DLL thật 2021/2025; (2) ngay trong app để biết bản Tekla của người dùng (21.1, 2020, ...) thiếu gì.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MeshFrame.Tekla
{
    public static class TeklaContract
    {
        /// <summary>Kiểm tra bản Tekla đang nạp theo ApiMembers. Trả về danh sách dòng thiếu/sai (rỗng = đủ).</summary>
        public static List<string> VerifyLoaded(TeklaApi api)
        {
            return Verify(ApiMembers.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), api.ModelAssembly, api.GeometryAssembly, api.CatalogAssembly);
        }

        public static List<string> Verify(IEnumerable<string> lines, Assembly model, Assembly geo, Assembly cat)
        {
            var bad = new List<string>();
            foreach (string line in lines)
            {
                if (line.Length == 0) continue;
                string err = CheckLine(line, model, geo, cat);
                if (err != null) bad.Add(line + "   <-- " + err);
            }
            return bad;
        }

        private static Type Find(string full, Assembly model, Assembly geo, Assembly cat)
        {
            return model.GetType(full) ?? geo.GetType(full) ?? (cat == null ? null : cat.GetType(full));
        }

        private static bool Compat(Type param, string chain)
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

        private static bool ParamsMatch(ParameterInfo[] ps, string[] chains)
        {
            if (ps.Length != chains.Length) return false;
            for (int i = 0; i < ps.Length; i++)
            {
                bool byRefArg = chains[i].EndsWith("&");
                if (ps[i].ParameterType.IsByRef != byRefArg) return false;
                Type pt = ps[i].ParameterType.IsByRef ? ps[i].ParameterType.GetElementType() : ps[i].ParameterType;
                if (!Compat(pt, chains[i].TrimEnd('&'))) return false;
            }
            return true;
        }

        private static string CheckLine(string line, Assembly model, Assembly geo, Assembly cat)
        {
            int sp = line.IndexOf(' ');
            string kind = line.Substring(0, sp), rest = line.Substring(sp + 1);
            try
            {
                if (kind == "new")
                {
                    int lp = rest.IndexOf('('); string tn = rest.Substring(0, lp);
                    string[] ch = rest.Substring(lp + 1, rest.Length - lp - 2).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    Type t = Find(tn, model, geo, cat);
                    if (t == null) return tn.StartsWith("Tekla.") ? "không có kiểu" : null;
                    if (!t.GetConstructors().Any(c => ParamsMatch(c.GetParameters(), ch))) return "không có hàm tạo (" + string.Join(", ", ch) + ")";
                    return null;
                }
                if (kind == "enum")
                {
                    int i = rest.IndexOf("::"); Type t = Find(rest.Substring(0, i), model, geo, cat);
                    if (t == null || !t.IsEnum) return "không có enum";
                    return Enum.GetNames(t).Contains(rest.Substring(i + 2)) ? null : "không có giá trị";
                }
                if (kind == "static")
                {
                    int i = rest.IndexOf("::"); Type t = Find(rest.Substring(0, i), model, geo, cat);
                    return t == null || t.GetField(rest.Substring(i + 2), BindingFlags.Public | BindingFlags.Static) == null ? "không có field tĩnh" : null;
                }
                int ci = rest.IndexOf("::"); string typeName = rest.Substring(0, ci); string m = rest.Substring(ci + 2);
                Type ty = Find(typeName, model, geo, cat);
                if (ty == null) return typeName.StartsWith("Tekla.") ? "không có kiểu" : null;
                if (kind == "get" || kind == "set")
                {
                    string name = kind == "set" ? m.Substring(0, m.IndexOf('=')) : m;
                    PropertyInfo p = null; FieldInfo fi = null;
                    for (Type l = ty; l != null && p == null && fi == null; l = l.BaseType)
                    {
                        p = l.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (p == null) fi = l.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    }
                    if (p == null && fi == null) return "không có thuộc tính/field " + name;
                    string chain = kind == "set" ? m.Substring(m.IndexOf('=') + 1) : null;
                    if (p != null)
                    {
                        if (kind == "get" && !p.CanRead) return "thuộc tính không đọc được";
                        if (kind == "set")
                        {
                            if (!p.CanWrite) return "thuộc tính chỉ đọc";
                            if (!Compat(p.PropertyType, chain)) return "kiểu " + p.PropertyType.FullName + " không nhận " + chain;
                        }
                    }
                    else if (kind == "set" && !Compat(fi.FieldType, chain)) return "kiểu " + fi.FieldType.FullName + " không nhận " + chain;
                    return null;
                }
                // call
                int lp2 = m.IndexOf('('); string mname = m.Substring(0, lp2);
                string[] ch2 = m.Substring(lp2 + 1, m.Length - lp2 - 2).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (Type l = ty; l != null; l = l.BaseType)
                    foreach (MethodInfo mi in l.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        if (mi.Name == mname && ParamsMatch(mi.GetParameters(), ch2)) return null;
                return "không có hàm " + mname + "(" + string.Join(", ", ch2) + ")";
            }
            catch (Exception e) { return e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
        }
    }
}
