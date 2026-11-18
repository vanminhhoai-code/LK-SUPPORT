using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace MeshFrame.Tekla;

public sealed class TeklaApi
{
	private readonly Assembly _model;

	private readonly Assembly _geo;

	private readonly Assembly _cat;

	public Func<Func<object>, object> Invoke = (Func<object> f) => f();

	public Action<string> Trace;

	public Assembly ModelAssembly => _model;

	public Assembly GeometryAssembly => _geo;

	public Assembly CatalogAssembly => _cat;

	public bool HasCatalogs => _cat != null;

	public TeklaApi(Assembly modelAssembly, Assembly geometryAssembly)
		: this(modelAssembly, geometryAssembly, null)
	{
	}

	public TeklaApi(Assembly modelAssembly, Assembly geometryAssembly, Assembly catalogAssembly)
	{
		_model = modelAssembly;
		_geo = geometryAssembly;
		_cat = catalogAssembly;
	}

	public static TeklaApi Load(string folder)
	{
		AppDomain.CurrentDomain.AssemblyResolve += (object s, ResolveEventArgs e) =>
		{
			string text2 = Path.Combine(folder, new AssemblyName(e.Name).Name + ".dll");
			return (!File.Exists(text2)) ? null : Assembly.LoadFrom(text2);
		};
		Assembly geometryAssembly = Assembly.LoadFrom(Path.Combine(folder, "Tekla.Structures.dll"));
		Assembly modelAssembly = Assembly.LoadFrom(Path.Combine(folder, "Tekla.Structures.Model.dll"));
		string text = Path.Combine(folder, "Tekla.Structures.Catalogs.dll");
		Assembly catalogAssembly = null;
		if (File.Exists(text))
		{
			try
			{
				catalogAssembly = Assembly.LoadFrom(text);
			}
			catch (Exception)
			{
				catalogAssembly = null;
			}
		}
		return new TeklaApi(modelAssembly, geometryAssembly, catalogAssembly);
	}

	public Type ModelType(string name)
	{
		return _model.GetType("Tekla.Structures.Model." + name, throwOnError: true);
	}

	public Type CatalogType(string name)
	{
		if (_cat == null)
		{
			throw new InvalidOperationException("Không có Tekla.Structures.Catalogs.dll.");
		}
		return _cat.GetType("Tekla.Structures.Catalogs." + name, throwOnError: true);
	}

	public Type GeoType(string name)
	{
		return _geo.GetType("Tekla.Structures.Geometry3d." + name, throwOnError: true);
	}

	public object New(Type type, params object[] args)
	{
		Note("new " + type.FullName + "(" + Chains(args) + ")");
		return Run(() => Activator.CreateInstance(type, args));
	}

	public object Point(double x, double y, double z)
	{
		return New(GeoType("Point"), x, y, z);
	}

	public object Vector(double x, double y, double z)
	{
		return New(GeoType("Vector"), x, y, z);
	}

	public object Enum(string modelTypeName, string value)
	{
		Type type = ModelType(modelTypeName);
		Note("enum " + type.FullName + "::" + value);
		return System.Enum.Parse(type, value);
	}

	private static void NeedTarget(object target, string name)
	{
		if (target == null)
		{
			throw new InvalidOperationException("Tekla trả về null khi cần đối tượng cho '" + name + "' (đối tượng không tồn tại hoặc không thuộc assembly).");
		}
	}

	public object Get(object target, string name)
	{
		NeedTarget(target, name);
		Type type = target.GetType();
		while (type != null)
		{
			PropertyInfo p = type.GetProperty(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
			if (p != null && p.GetIndexParameters().Length == 0)
			{
				Note("get " + target.GetType().FullName + "::" + name);
				return Run(() => p.GetValue(target, null));
			}
			FieldInfo f = type.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
			if (f != null)
			{
				Note("get " + target.GetType().FullName + "::" + name);
				return Run(() => f.GetValue(target));
			}
			type = type.BaseType;
		}
		throw new MissingMemberException(target.GetType().FullName, name);
	}

	public object Static(Type type, string name)
	{
		FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.Public);
		if (field == null)
		{
			throw new MissingMemberException(type.FullName, name);
		}
		Note("static " + type.FullName + "::" + name);
		if (!field.IsLiteral)
		{
			return field.GetValue(null);
		}
		return field.GetRawConstantValue();
	}

	public double GetDouble(object target, string name)
	{
		return Convert.ToDouble(Get(target, name), CultureInfo.InvariantCulture);
	}

	public void Set(object target, string name, object value)
	{
		NeedTarget(target, name);
		Type type = target.GetType();
		while (type != null)
		{
			PropertyInfo p = type.GetProperty(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
			if (p != null && p.GetIndexParameters().Length == 0)
			{
				Note("set " + target.GetType().FullName + "::" + name + "=" + Chain(value));
				object v = Coerce(p.PropertyType, value);
				Run(() =>
				{
					p.SetValue(target, v, null);
					return (object)null;
				});
				return;
			}
			FieldInfo f = type.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
			if (f != null)
			{
				Note("set " + target.GetType().FullName + "::" + name + "=" + Chain(value));
				object v2 = Coerce(f.FieldType, value);
				Run(() =>
				{
					f.SetValue(target, v2);
					return (object)null;
				});
				return;
			}
			type = type.BaseType;
		}
		throw new MissingMemberException(target.GetType().FullName, name);
	}

	public object Call(object target, string name, params object[] args)
	{
		NeedTarget(target, name);
		Type type = target.GetType();
		while (type != null)
		{
			MethodInfo[] methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
			foreach (MethodInfo methodInfo in methods)
			{
				if (methodInfo.Name != name || methodInfo.IsGenericMethodDefinition)
				{
					continue;
				}
				ParameterInfo[] parameters = methodInfo.GetParameters();
				if (parameters.Length != args.Length)
				{
					continue;
				}
				object[] conv = new object[args.Length];
				bool flag = true;
				for (int j = 0; j < parameters.Length; j++)
				{
					if (parameters[j].ParameterType.IsByRef)
					{
						flag = false;
						break;
					}
					conv[j] = Coerce(parameters[j].ParameterType, args[j]);
					if (conv[j] != null && !parameters[j].ParameterType.IsInstanceOfType(conv[j]))
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					Note("call " + target.GetType().FullName + "::" + name + "(" + Chains(args) + ")");
					MethodInfo method = methodInfo;
					return Run(() => method.Invoke(target, conv));
				}
			}
			type = type.BaseType;
		}
		throw new MissingMethodException(target.GetType().FullName, name + "/" + args.Length);
	}

	public object[] CallRef(object target, string name, params object[] args)
	{
		NeedTarget(target, name);
		Type type = target.GetType();
		while (type != null)
		{
			MethodInfo[] methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
			foreach (MethodInfo methodInfo in methods)
			{
				if (methodInfo.Name != name || methodInfo.IsGenericMethodDefinition)
				{
					continue;
				}
				ParameterInfo[] parameters = methodInfo.GetParameters();
				if (parameters.Length != args.Length)
				{
					continue;
				}
				object[] conv = new object[args.Length];
				bool flag = true;
				bool flag2 = false;
				for (int j = 0; j < parameters.Length; j++)
				{
					Type type2 = parameters[j].ParameterType;
					if (type2.IsByRef)
					{
						flag2 = true;
						type2 = type2.GetElementType();
					}
					conv[j] = Coerce(type2, args[j]);
					if (conv[j] != null && !type2.IsInstanceOfType(conv[j]))
					{
						flag = false;
						break;
					}
				}
				if (flag && flag2)
				{
					string[] array = new string[args.Length];
					for (int k = 0; k < parameters.Length; k++)
					{
						array[k] = Chain(args[k]) + (parameters[k].ParameterType.IsByRef ? "&" : "");
					}
					Note("call " + target.GetType().FullName + "::" + name + "(" + string.Join(";", array) + ")");
					MethodInfo method = methodInfo;
					Run(() => method.Invoke(target, conv));
					return conv;
				}
			}
			type = type.BaseType;
		}
		throw new MissingMethodException(target.GetType().FullName, name + "/" + args.Length + " (ref)");
	}

	public void Require(object target, string name, string what, params object[] args)
	{
		object obj = Call(target, name, args);
		if (obj is bool && !(bool)obj)
		{
			throw new InvalidOperationException("Tekla từ chối " + name + ": " + what);
		}
	}

	private object Run(Func<object> f)
	{
		try
		{
			return Invoke(f);
		}
		catch (TargetInvocationException ex)
		{
			throw ex.InnerException ?? ex;
		}
	}

	private void Note(string s)
	{
		if (Trace != null)
		{
			Trace(s);
		}
	}

	private static string Chain(object v)
	{
		if (v == null)
		{
			return "null";
		}
		Type type = v.GetType();
		if (!type.FullName.StartsWith("Tekla."))
		{
			return type.FullName;
		}
		string text = type.FullName;
		Type baseType = type.BaseType;
		while (baseType != null && baseType.FullName.StartsWith("Tekla."))
		{
			text = text + ">" + baseType.FullName;
			baseType = baseType.BaseType;
		}
		Type[] interfaces = type.GetInterfaces();
		foreach (Type type2 in interfaces)
		{
			if (type2.FullName.StartsWith("Tekla."))
			{
				text = text + ">" + type2.FullName;
			}
		}
		return text;
	}

	private static string Chains(object[] args)
	{
		string[] array = new string[args.Length];
		for (int i = 0; i < args.Length; i++)
		{
			array[i] = Chain(args[i]);
		}
		return string.Join(";", array);
	}

	private static object Coerce(Type t, object v)
	{
		if (v == null || t.IsInstanceOfType(v))
		{
			return v;
		}
		if (t.IsEnum && v is string)
		{
			return System.Enum.Parse(t, (string)v);
		}
		if (v is string)
		{
			return v;
		}
		if (v is IConvertible && typeof(IConvertible).IsAssignableFrom(t) && !t.IsEnum)
		{
			return Convert.ChangeType(v, t, CultureInfo.InvariantCulture);
		}
		return v;
	}
}
