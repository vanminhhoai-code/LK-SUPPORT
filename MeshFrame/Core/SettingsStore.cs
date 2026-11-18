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
        /// <summary>Tăng khi mặc định thay đổi: file thiết lập cũ hơn bị bỏ (đổi tên .bak) để mặc định mới có hiệu lực.</summary>
        public const int CurrentVersion = 3;

        /// <summary>Các dòng key=value của thông số (dùng cho file thông số và file bản ghi khung).</summary>
        public static List<string> ToLines(MeshFrameSettings s)
        {
            var lines = new List<string>();
            foreach (FieldInfo f in typeof(MeshFrameSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v = f.GetValue(s);
                lines.Add(f.Name + "=" + Convert.ToString(v is Enum ? (object)v.ToString() : v, CultureInfo.InvariantCulture));
            }
            return lines;
        }

        public static void Save(MeshFrameSettings s, string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var lines = new List<string> { "@version=" + CurrentVersion };
            lines.AddRange(ToLines(s));
            File.WriteAllLines(path, lines.ToArray());
        }

        /// <summary>Nạp từ file; thiếu file hoặc khóa lỗi thì giữ giá trị mặc định.</summary>
        public static MeshFrameSettings Load(string path)
        {
            if (!File.Exists(path)) return new MeshFrameSettings();
            string[] lines = File.ReadAllLines(path);
            int version = 0;
            foreach (string l in lines) if (l.StartsWith("@version=")) int.TryParse(l.Substring(9), NumberStyles.Integer, CultureInfo.InvariantCulture, out version);
            if (version < CurrentVersion)
            {
                // Thiết lập lưu từ bản cũ (vd On plane = Left, vật liệu, tiết diện 50x50x3...) làm mặc định mới không có hiệu lực.
                try { File.Copy(path, path + ".v" + version + ".bak", true); File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                return new MeshFrameSettings();
            }
            return FromLines(lines);
        }

        public static MeshFrameSettings FromLines(IEnumerable<string> lines)
        {
            var s = new MeshFrameSettings();
            var map = new Dictionary<string, string>();
            foreach (string line in lines)
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
