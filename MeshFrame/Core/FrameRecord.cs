// Bản ghi một khung đã tạo: mã định danh + thông số + các điểm pick (GLOBAL). Lưu cạnh model để "Sửa khung đã chọn".
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MeshFrame.Core
{
    public sealed class FrameRecord
    {
        public string Id;
        public MeshFrameSettings Settings = new MeshFrameSettings();
        public V3[] Corners = new V3[0];

        public static string NewId() { return Guid.NewGuid().ToString("N"); }

        public void Save(string path)
        {
            var lines = new List<string> { "@id=" + Id, "@n=" + Corners.Length };
            for (int i = 0; i < Corners.Length; i++)
                lines.Add("@p" + i + "=" + string.Join(";", new[] { Fmt(Corners[i].X), Fmt(Corners[i].Y), Fmt(Corners[i].Z) }));
            lines.AddRange(SettingsStore.ToLines(Settings));
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllLines(path, lines.ToArray());
        }

        /// <summary>Nạp bản ghi; null nếu không có file hoặc file hỏng.</summary>
        public static FrameRecord Load(string path)
        {
            if (!File.Exists(path)) return null;
            string[] lines = File.ReadAllLines(path);
            var map = new Dictionary<string, string>();
            foreach (string l in lines) { int i = l.IndexOf('='); if (i > 0) map[l.Substring(0, i)] = l.Substring(i + 1); }
            int n;
            if (!map.ContainsKey("@id") || !map.ContainsKey("@n") || !int.TryParse(map["@n"], NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n < 3) return null;
            var rec = new FrameRecord { Id = map["@id"], Settings = SettingsStore.FromLines(lines), Corners = new V3[n] };
            for (int i = 0; i < n; i++)
            {
                string v;
                if (!map.TryGetValue("@p" + i, out v)) return null;
                string[] t = v.Split(';');
                double x, y, z;
                if (t.Length != 3 || !double.TryParse(t[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !double.TryParse(t[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                    || !double.TryParse(t[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return null;
                rec.Corners[i] = new V3(x, y, z);
            }
            return rec;
        }

        private static string Fmt(double d) { return d.ToString("R", CultureInfo.InvariantCulture); }
    }
}
