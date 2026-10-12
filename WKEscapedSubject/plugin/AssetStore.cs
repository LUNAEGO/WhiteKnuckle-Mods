using System;
using System.Collections.Generic;
using System.IO;

namespace WKEscapedSubject
{
    // 그림·소리 파일 읽기. 모든 파일은 DLL 안에 리소스로 들어 있다 (csproj의 EmbeddedResource, 이름 "Assets/<경로>").
    // 모드 매니저(Thunderstore/r2modman)는 설치할 때 폴더 구조를 펼쳐 같은 이름의 파일이 서로 덮어쓰므로,
    // 플러그인 폴더의 파일에 기대지 않는다. DLL 안에 없는 파일만 플러그인 폴더의 Assets/ 에서 찾는다 (개발 중 교체용).
    // 경로는 "Overlays/stage1/HND_B1A1.png"처럼 '/'로 구분
    internal static class AssetStore
    {
        const string Prefix = "Assets/";
        static Dictionary<string, string> res; // 경로 → 리소스 이름

        static void Init()
        {
            if (res != null) return;
            res = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string n in typeof(Plugin).Assembly.GetManifestResourceNames())
            {
                string p = n.Replace('\\', '/');
                if (p.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) res[p.Substring(Prefix.Length)] = n;
            }
            Plugin.Log.LogInfo("Embedded assets: " + res.Count);
        }

        static string DiskPath(string rel) { return Path.Combine(Plugin.AssetDir, rel.Replace('/', Path.DirectorySeparatorChar)); }

        public static byte[] Read(string rel)
        {
            Init();
            string name;
            if (res.TryGetValue(rel, out name))
                using (Stream s = typeof(Plugin).Assembly.GetManifestResourceStream(name))
                using (var m = new MemoryStream())
                {
                    s.CopyTo(m);
                    return m.ToArray();
                }
            string disk = DiskPath(rel);
            return File.Exists(disk) ? File.ReadAllBytes(disk) : null;
        }

        public static string[] ReadLines(string rel)
        {
            byte[] b = Read(rel);
            if (b == null) return null;
            return System.Text.Encoding.UTF8.GetString(b).Replace("\r", "").Split('\n');
        }

        public static bool DirExists(string relDir)
        {
            Init();
            string pre = relDir.TrimEnd('/') + "/";
            foreach (string k in res.Keys) if (k.StartsWith(pre, StringComparison.OrdinalIgnoreCase)) return true;
            return Directory.Exists(DiskPath(relDir));
        }

        // 폴더 바로 아래 파일들의 경로 (하위 폴더 제외)
        public static List<string> List(string relDir, string ext)
        {
            Init();
            string pre = relDir.TrimEnd('/') + "/";
            var list = new List<string>();
            foreach (string k in res.Keys)
                if (k.StartsWith(pre, StringComparison.OrdinalIgnoreCase) && k.IndexOf('/', pre.Length) < 0 && k.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    list.Add(k);
            if (list.Count == 0 && Directory.Exists(DiskPath(relDir)))
                foreach (string f in Directory.GetFiles(DiskPath(relDir), "*" + ext))
                    list.Add(pre + Path.GetFileName(f));
            return list;
        }
    }
}
