// Lưu / nạp MeshFrameSettings dạng key=value (nhớ thông số lần trước). Thuần, không phụ thuộc Tekla.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace MeshFrame.Core
{
    public static class SettingsStore
    {
        public static void Save(MeshFrameSettings s, string path)
        {
            var lines = new List<string>();
            foreach (FieldInfo f in typeof(MeshFrameSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v = f.GetValue(s);
                lines.Add(f.Name + "=" + Convert.ToString(v is Enum ? (object)v.ToString() : v, CultureInfo.InvariantCulture));
            }
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllLines(path, lines.ToArray());
        }

        /// <summary>Nạp từ file; thiếu file hoặc khóa lỗi thì giữ giá trị mặc định.</summary>
        public static MeshFrameSettings Load(string path)
        {
            var s = new MeshFrameSettings();
            if (!File.Exists(path)) return s;
            var map = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(path))
            {
                int i = line.IndexOf('=');
                if (i > 0) map[line.Substring(0, i).Trim()] = line.Substring(i + 1);
            }
            foreach (FieldInfo f in typeof(MeshFrameSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                string raw;
                if (!map.TryGetValue(f.Name, out raw)) continue;
                try
                {
                    object v;
                    if (f.FieldType == typeof(string)) v = raw;
                    else if (f.FieldType == typeof(bool)) v = bool.Parse(raw);
                    else if (f.FieldType == typeof(int)) v = int.Parse(raw, CultureInfo.InvariantCulture);
                    else if (f.FieldType == typeof(double)) v = double.Parse(raw, CultureInfo.InvariantCulture);
                    else if (f.FieldType.IsEnum) v = Enum.Parse(f.FieldType, raw);
                    else continue;
                    f.SetValue(s, v);
                }
                catch (FormatException) { }
                catch (ArgumentException) { }
                catch (OverflowException) { }
            }
            return s;
        }
    }
}
