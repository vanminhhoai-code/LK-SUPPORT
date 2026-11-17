// Adapter reflection tới Tekla Open API: một bản build chạy được 21.1 / 2020 / 2025, không tham chiếu cứng DLL Tekla
// (cùng nguyên tắc với TeklaGateway / SdkMetadata của Cover Tool: Create/Get/Set/Call). C# 7.3.
// Khi nhập vào Cover Tool: tạo TeklaApi từ assembly gateway đã nạp và gán Invoke = SdkThread.Invoke.
using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace MeshFrame.Tekla
{
    public sealed class TeklaApi
    {
        private readonly Assembly _model, _geo;

        /// <summary>Mọi lời gọi SDK đi qua đây (Cover Tool gắn SdkThread.Invoke).</summary>
        public Func<Func<object>, object> Invoke = delegate (Func<object> f) { return f(); };

        /// <summary>Ghi lại "Kiểu::Thành viên" đã dùng, để test hợp đồng đối chiếu DLL thật.</summary>
        public Action<string> Trace;

        public TeklaApi(Assembly modelAssembly, Assembly geometryAssembly)
        {
            _model = modelAssembly; _geo = geometryAssembly;
        }

        /// <summary>Nạp Tekla.Structures.dll + Tekla.Structures.Model.dll trong thư mục API của một bản Tekla.</summary>
        public static TeklaApi Load(string folder)
        {
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object s, ResolveEventArgs e)
            {
                string p = Path.Combine(folder, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(p) ? Assembly.LoadFrom(p) : null;
            };
            Assembly geo = Assembly.LoadFrom(Path.Combine(folder, "Tekla.Structures.dll"));
            Assembly mdl = Assembly.LoadFrom(Path.Combine(folder, "Tekla.Structures.Model.dll"));
            return new TeklaApi(mdl, geo);
        }

        public Type ModelType(string name) { return _model.GetType("Tekla.Structures.Model." + name, true); }
        public Type GeoType(string name) { return _geo.GetType("Tekla.Structures.Geometry3d." + name, true); }

        public object New(Type type, params object[] args)
        {
            Note("new " + type.FullName + "(" + Chains(args) + ")");
            return Run(delegate { return Activator.CreateInstance(type, args); });
        }

        public object Point(double x, double y, double z) { return New(GeoType("Point"), x, y, z); }
        public object Vector(double x, double y, double z) { return New(GeoType("Vector"), x, y, z); }

        public object Enum(string modelTypeName, string value)
        {
            Type t = ModelType(modelTypeName);
            Note("enum " + t.FullName + "::" + value);
            return System.Enum.Parse(t, value);
        }

        public object Get(object target, string name)
        {
            for (Type l = target.GetType(); l != null; l = l.BaseType)
            {
                PropertyInfo p = l.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (p != null && p.GetIndexParameters().Length == 0) { Note("get " + target.GetType().FullName + "::" + name); return Run(delegate { return p.GetValue(target, null); }); }
            }
            throw new MissingMemberException(target.GetType().FullName, name);
        }

        public double GetDouble(object target, string name) { return Convert.ToDouble(Get(target, name), CultureInfo.InvariantCulture); }

        public void Set(object target, string name, object value)
        {
            for (Type l = target.GetType(); l != null; l = l.BaseType)
            {
                PropertyInfo p = l.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (p == null || p.GetIndexParameters().Length != 0) continue;
                Note("set " + target.GetType().FullName + "::" + name + "=" + Chain(value));
                object v = Coerce(p.PropertyType, value);
                Run(delegate { p.SetValue(target, v, null); return null; });
                return;
            }
            throw new MissingMemberException(target.GetType().FullName, name);
        }

        public object Call(object target, string name, params object[] args)
        {
            for (Type l = target.GetType(); l != null; l = l.BaseType)
            {
                foreach (MethodInfo m in l.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (m.Name != name || m.IsGenericMethodDefinition) continue;
                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length != args.Length) continue;
                    object[] conv = new object[args.Length];
                    bool ok = true;
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (ps[i].ParameterType.IsByRef) { ok = false; break; }
                        conv[i] = Coerce(ps[i].ParameterType, args[i]);
                        if (conv[i] != null && !ps[i].ParameterType.IsInstanceOfType(conv[i])) { ok = false; break; }
                    }
                    if (!ok) continue;
                    Note("call " + target.GetType().FullName + "::" + name + "(" + Chains(args) + ")");
                    MethodInfo method = m;
                    return Run(delegate { return method.Invoke(target, conv); });
                }
            }
            throw new MissingMethodException(target.GetType().FullName, name + "/" + args.Length);
        }

        /// <summary>Gọi hàm trả bool (Insert/Modify...). Tekla trả false thì ném lỗi kèm mô tả.</summary>
        public void Require(object target, string name, string what, params object[] args)
        {
            object r = Call(target, name, args);
            if (r is bool && !(bool)r) throw new InvalidOperationException("Tekla từ chối " + name + ": " + what);
        }

        private object Run(Func<object> f)
        {
            try { return Invoke(f); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }

        private void Note(string s) { if (Trace != null) Trace(s); }

        // Kiểu của giá trị: kiểu System giữ nguyên; kiểu Tekla ghi cả chuỗi kế thừa (Beam>Part>ModelObject).
        private static string Chain(object v)
        {
            if (v == null) return "null";
            Type t = v.GetType();
            if (!t.FullName.StartsWith("Tekla.")) return t.FullName;
            string r = t.FullName;
            for (Type b = t.BaseType; b != null && b.FullName.StartsWith("Tekla."); b = b.BaseType) r += ">" + b.FullName;
            foreach (Type i in t.GetInterfaces()) if (i.FullName.StartsWith("Tekla.")) r += ">" + i.FullName;   // vd Beam -> IAssemblable
            return r;
        }

        private static string Chains(object[] args)
        {
            string[] parts = new string[args.Length];
            for (int i = 0; i < args.Length; i++) parts[i] = Chain(args[i]);
            return string.Join(";", parts);
        }

        private static object Coerce(Type t, object v)
        {
            if (v == null || t.IsInstanceOfType(v)) return v;
            if (t.IsEnum && v is string) return System.Enum.Parse(t, (string)v);
            if (v is IConvertible && typeof(IConvertible).IsAssignableFrom(t) && !t.IsEnum) return Convert.ChangeType(v, t, CultureInfo.InvariantCulture);
            return v;
        }
    }
}
