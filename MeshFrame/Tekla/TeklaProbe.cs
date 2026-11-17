// Đọc các đối tượng đang chọn trong Tekla và in TOÀN BỘ thuộc tính (chỉ đọc) - dùng để lấy "lưới mẫu" (RebarMesh) làm chuẩn:
// người dùng tạo tay một Reinforcement Mesh trong Tekla, chọn nó, bấm nút trong app.
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;

namespace MeshFrame.Tekla
{
    public static class TeklaProbe
    {
        public static string OutputPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeshFrame", "selected-object.txt"); }
        }

        /// <summary>Ghi thuộc tính các đối tượng đang chọn (tối đa 5) ra file; trả về thông báo cho người dùng.</summary>
        public static string DumpSelected(TeklaApi api)
        {
            object selector = api.New(api.ModelType("UI.ModelObjectSelector"));
            object en = api.Call(selector, "GetSelectedObjects");
            var sb = new StringBuilder();
            int n = 0;
            while ((bool)api.Call(en, "MoveNext") && n < 5)
            {
                object o = api.Get(en, "Current");
                n++;
                sb.AppendLine("=== Đối tượng " + n + ": " + o.GetType().FullName);
                sb.Append(api.Invoke(delegate { return Dump(o, 0); }));
                sb.AppendLine();
            }
            if (n == 0) return "Chưa chọn đối tượng nào trong Tekla. Chọn lưới mẫu (Reinforcement Mesh) rồi bấm lại.";
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, sb.ToString(), Encoding.UTF8);
            return "Đã ghi thuộc tính " + n + " đối tượng vào:\n" + OutputPath + "\n\nGửi file này cho người hỗ trợ.";
        }

        // Dump chung bằng reflection: không phụ thuộc tên thành viên cụ thể của một phiên bản Tekla.
        internal static string Dump(object o, int depth)
        {
            var sb = new StringBuilder();
            string pad = new string(' ', depth * 2);
            foreach (PropertyInfo p in o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length != 0) continue;
                string text;
                try { text = Format(p.GetValue(o, null), depth); }
                catch (Exception e) { text = "(không đọc được: " + (e.InnerException ?? e).GetType().Name + ")"; }
                sb.AppendLine(pad + p.Name + " = " + text);
            }
            foreach (FieldInfo f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                string text;
                try { text = Format(f.GetValue(o), depth); } catch (Exception e) { text = "(không đọc được: " + e.GetType().Name + ")"; }
                sb.AppendLine(pad + f.Name + " = " + text);
            }
            return sb.ToString();
        }

        internal static string Format(object v, int depth)
        {
            if (v == null) return "(null)";
            Type t = v.GetType();
            if (v is string || t.IsPrimitive || t.IsEnum || v is decimal) return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
            if (v is IEnumerable)
            {
                var items = new StringBuilder("[");
                int i = 0;
                foreach (object x in (IEnumerable)v) { if (i++ > 0) items.Append("; "); if (i > 25) { items.Append("..."); break; } items.Append(Format(x, depth + 1)); }
                return items.Append("]").ToString();
            }
            FieldInfo fx = t.GetField("X"), fy = t.GetField("Y"), fz = t.GetField("Z");
            if (fx != null && fy != null && fz != null)
                return "(" + Format(fx.GetValue(v), depth) + ", " + Format(fy.GetValue(v), depth) + ", " + Format(fz.GetValue(v), depth) + ")";
            if (depth >= 1) return t.Name;
            return t.Name + "{\n" + Dump(v, depth + 1) + new string(' ', depth * 2) + "}";
        }
    }
}
