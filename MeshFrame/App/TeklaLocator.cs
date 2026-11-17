// Tìm thư mục API Tekla theo bản phát hành - cùng quy tắc với TeklaRelease/TeklaApiLocation của Cover Tool.
// 21.1 và 2020: DLL trong nt\bin\plugins. 2025: trong bin.
using System;
using System.Collections.Generic;
using System.IO;

namespace MeshFrame.App
{
    internal static class TeklaRelease
    {
        internal const string V21 = "21.1", V2025 = "2025", V2020 = "2020";
        internal static readonly string[] All = { V2025, V2020, V21 };

        internal static bool Legacy(string r) { return r == V21 || r == V2020; }
        internal static string[] Folders(string r) { return r == V2025 ? new[] { "2025.0", "2025" } : r == V2020 ? new[] { "2020.0", "2020" } : new[] { "21.1" }; }
    }

    internal static class TeklaLocator
    {
        /// <summary>Đọc --tekla-version / --tekla-bin / MeshFrame.ini. Không chỉ định bản thì thử lần lượt 2025, 2020, 21.1.</summary>
        internal static bool Resolve(string[] args, out string release, out string bin)
        {
            string wantRelease = null, wantBin = null;
            foreach (string a in args)
            {
                if (a.StartsWith("--tekla-version=", StringComparison.OrdinalIgnoreCase)) wantRelease = a.Substring(16).Trim('"');
                else if (a.StartsWith("--tekla-bin=", StringComparison.OrdinalIgnoreCase)) wantBin = a.Substring(12).Trim('"');
            }
            string ini = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MeshFrame.ini");
            if (wantBin == null && File.Exists(ini))
                foreach (string line in File.ReadAllLines(ini))
                    if (line.StartsWith("TeklaBin=", StringComparison.OrdinalIgnoreCase)) wantBin = line.Substring(9).Trim().Trim('"');

            if (!string.IsNullOrEmpty(wantBin) && Ok(wantBin)) { release = wantRelease ?? Guess(wantBin); bin = wantBin; return true; }
            foreach (string r in wantRelease != null ? new[] { wantRelease } : TeklaRelease.All)
            {
                string found = Find(r);
                if (found != null) { release = r; bin = found; return true; }
            }
            release = null; bin = null; return false;
        }

        internal static string Find(string release)
        {
            var roots = new List<string>();
            foreach (string pf in new[] { @"C:\Program Files", Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
                if (!string.IsNullOrWhiteSpace(pf)) { roots.Add(Path.Combine(pf, "Tekla Structures")); roots.Add(Path.Combine(pf, "TeklaStructures")); }
            roots.Add(@"C:\TeklaStructures");
            foreach (string root in roots)
                foreach (string f in TeklaRelease.Folders(release))
                    foreach (string layout in TeklaRelease.Legacy(release)
                        ? new[] { @"nt\bin\plugins", @"nt\bin", "bin", @"bin\plugins" }
                        : new[] { "bin", @"bin\plugins", @"nt\bin\plugins", @"nt\bin" })
                    {
                        string p = Path.Combine(root, f, layout);
                        if (Ok(p)) return p;
                    }
            return null;
        }

        private static string Guess(string bin)
        {
            if (bin.IndexOf("2025", StringComparison.Ordinal) >= 0) return TeklaRelease.V2025;
            if (bin.IndexOf("2020", StringComparison.Ordinal) >= 0) return TeklaRelease.V2020;
            return TeklaRelease.V21;
        }

        private static bool Ok(string dir) { return Directory.Exists(dir) && File.Exists(Path.Combine(dir, "Tekla.Structures.dll")) && File.Exists(Path.Combine(dir, "Tekla.Structures.Model.dll")); }
    }
}
