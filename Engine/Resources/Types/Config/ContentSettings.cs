using System;
using System.IO;

namespace OssianForge.Engine.Resources.Config
{
    public enum ContentMode
    {
        /// <summary>Loose source files; Content.Path is the Content folder.</summary>
        Raw,
        /// <summary>Cooked blobs; Content.Path is the pak file.</summary>
        Pak
    }

    /// <summary>
    /// Where the engine reads content from. Parsed from Ossian.ini (project root, never cooked):
    /// <code>
    /// [Content]
    /// Mode = raw                              ; raw | pak
    /// Path = Content                          ; raw: Content folder   pak: Content.pak file
    /// TreeConfigPath = ConfigFiles/Core/tree.json       ; content-relative; empty = path from resources.json
    /// ResourcesConfigPath = ConfigFiles/Core/resources.json  ; inside the active source (pak: must match cook --manifest)
    /// </code>
    /// Every key is optional; missing keys keep the defaults below.
    /// </summary>
    public sealed class ContentSettings
    {
        public const string IniFileName = "Ossian.ini";

        /// <summary>Folder used for disk writes (ConfigFile.Save) when running from a pak.</summary>
        public const string DefaultFolder = "Content";

        public ContentMode Mode { get; set; } = ContentMode.Raw;
        public string Path { get; set; } = DefaultFolder;

        /// <summary>Overrides the path of the "configfile.tree" record. Empty keeps the path from resources.json.</summary>
        public string TreeConfigPath { get; set; } = "";

        public string ResourcesConfigPath { get; set; } = "ConfigFiles/Core/resources.json";

        public static ContentSettings FromIniFile(string path) => FromIni(File.ReadAllText(path), path);

        public static ContentSettings FromIni(string text, string origin = IniFileName)
        {
            var s = new ContentSettings();
            string section = "";

            foreach (var rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

                if (line[0] == '[' && line[^1] == ']')
                {
                    section = line[1..^1].Trim();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq < 0) continue;

                string key = line[..eq].Trim();
                string value = ParseValue(line[(eq + 1)..]);

                switch ($"{section}.{key}".ToLowerInvariant())
                {
                    case "content.mode":
                        if (!Enum.TryParse<ContentMode>(value, ignoreCase: true, out var mode))
                            throw new Exception($"[CONTENT] {origin}: unknown Content.Mode '{value}' (expected raw or pak).");
                        s.Mode = mode;
                        break;
                    case "content.path": if (value.Length > 0) s.Path = value; break;
                    case "content.tree": s.TreeConfigPath = value; break;
                    case "content.resourcesconfigpath": if (value.Length > 0) s.ResourcesConfigPath = value; break;
                    default:
                        Console.WriteLine($"[CONTENT] {origin}: ignoring unknown key '{section}.{key}'");
                        break;
                }
            }

            return s;
        }

        // Quoted value: everything between the quotes. Otherwise cut at an inline " ;" or " #" comment.
        private static string ParseValue(string raw)
        {
            string v = raw.Trim();
            if (v.Length >= 2 && v[0] == '"')
            {
                int close = v.IndexOf('"', 1);
                if (close > 0) return v[1..close];
            }

            for (int i = 1; i < v.Length; i++)
                if ((v[i] == ';' || v[i] == '#') && char.IsWhiteSpace(v[i - 1]))
                    return v[..i].TrimEnd();

            return v;
        }
    }
}