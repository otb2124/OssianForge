using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml;
namespace OssianForge.Engine.Resources.Config
{
    public enum ConfigFormat
    {
        Json,
        Ini,
        Xml
    }
    public class ConfigFile : ResourceFile
    {
        public ConfigFormat Format { get; private set; }
        private Dictionary<string, string> _data = new();
        public ConfigFile(string id, string path)
        {
            Id = id;
            Path = path;
            Format = DetectFormat(path);
        }
        public ConfigFile(string id, string path, ConfigFormat format)
        {
            Id = id;
            Path = path;
            Format = format;
        }
        public override void Load()
        {
            base.Load();
            string globalPath = ResourceFile.CONTENT_FOLDER_PATH + "/" + Path;
            if (!File.Exists(globalPath))
                throw new Exception($"ConfigFile not found: '{globalPath}'");
            string raw = File.ReadAllText(globalPath);
            _data = Format switch
            {
                ConfigFormat.Json => ParseJson(raw),
                ConfigFormat.Ini => ParseIni(raw),
                ConfigFormat.Xml => ParseXml(raw),
                _ => throw new Exception($"Unknown config format: {Format}")
            };
            Console.WriteLine($"[CONFIG] Loaded '{Id}' ({Format}) with {_data.Count} entries");
        }
        // --- getters ---
        public string GetString(string key, string fallback = "")
            => _data.TryGetValue(key, out var val) ? val : fallback;
        public int GetInt(string key, int fallback = 0)
            => _data.TryGetValue(key, out var val) && int.TryParse(val, out var result) ? result : fallback;
        public float GetFloat(string key, float fallback = 0f)
            => _data.TryGetValue(key, out var val) && float.TryParse(val, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : fallback;
        public bool GetBool(string key, bool fallback = false)
            => _data.TryGetValue(key, out var val) && bool.TryParse(val, out var result) ? result : fallback;
        public bool HasKey(string key)
            => _data.ContainsKey(key);
        public IReadOnlyDictionary<string, string> GetAll()
            => _data;
        // --- setters ---
        public void Set(string key, string value)
            => _data[key] = value;
        public void Set(string key, int value)
            => _data[key] = value.ToString();
        public void Set(string key, float value)
            => _data[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public void Set(string key, bool value)
            => _data[key] = value.ToString().ToLower();
        // --- save ---
        public void Save()
        {
            string globalPath = ResourceFile.CONTENT_FOLDER_PATH + "/" + Path;
            string content = Format switch
            {
                ConfigFormat.Json => SerializeJson(),
                ConfigFormat.Ini => SerializeIni(),
                ConfigFormat.Xml => SerializeXml(),
                _ => throw new Exception($"Unknown config format: {Format}")
            };
            File.WriteAllText(globalPath, content);
            Console.WriteLine($"[CONFIG] Saved '{Id}' to '{globalPath}'");
        }
        // --- parsers ---
        private static Dictionary<string, string> ParseJson(string raw)
        {
            var result = new Dictionary<string, string>();
            using var doc = JsonDocument.Parse(raw);
            ParseJsonElement(doc.RootElement, "", result);
            return result;
        }
        private static void ParseJsonElement(JsonElement element, string prefix, Dictionary<string, string> result)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        string key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                        ParseJsonElement(prop.Value, key, result);
                    }
                    break;
                case JsonValueKind.Array:
                    int i = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        ParseJsonElement(item, $"{prefix}[{i++}]", result);
                    }
                    break;
                default:
                    result[prefix] = element.ToString();
                    break;
            }
        }
        private static Dictionary<string, string> ParseIni(string raw)
        {
            var result = new Dictionary<string, string>();
            string currentSection = "";
            foreach (var rawLine in raw.Split('\n'))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
                    continue;
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    currentSection = line[1..^1].Trim();
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line[..eq].Trim();
                string value = line[(eq + 1)..].Trim();
                string fullKey = string.IsNullOrEmpty(currentSection) ? key : $"{currentSection}.{key}";
                result[fullKey] = value;
            }
            return result;
        }
        private static Dictionary<string, string> ParseXml(string raw)
        {
            var result = new Dictionary<string, string>();
            var doc = new XmlDocument();
            doc.LoadXml(raw);
            ParseXmlNode(doc.DocumentElement, "", result);
            return result;
        }
        private static void ParseXmlNode(XmlNode node, string prefix, Dictionary<string, string> result)
        {
            string key = string.IsNullOrEmpty(prefix) ? node.Name : $"{prefix}.{node.Name}";
            if (node.HasChildNodes && node.FirstChild is XmlText text)
            {
                result[key] = text.Value?.Trim() ?? "";
                return;
            }
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child is XmlElement)
                    ParseXmlNode(child, key, result);
            }
        }
        // --- serializers ---
        private string SerializeJson()
        {
            // rebuild nested structure from flat keys
            var root = new Dictionary<string, object>();
            foreach (var (key, value) in _data)
            {
                var parts = key.Split('.');
                var current = root;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (!current.ContainsKey(parts[i]))
                        current[parts[i]] = new Dictionary<string, object>();
                    current = (Dictionary<string, object>)current[parts[i]];
                }
                current[parts[^1]] = value;
            }
            return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
        }
        private string SerializeIni()
        {
            var sb = new StringBuilder();
            var sections = new Dictionary<string, List<(string key, string value)>>();
            foreach (var (fullKey, value) in _data)
            {
                int dot = fullKey.IndexOf('.');
                if (dot < 0)
                {
                    if (!sections.ContainsKey("")) sections[""] = new();
                    sections[""].Add((fullKey, value));
                }
                else
                {
                    string section = fullKey[..dot];
                    string key = fullKey[(dot + 1)..];
                    if (!sections.ContainsKey(section)) sections[section] = new();
                    sections[section].Add((key, value));
                }
            }
            foreach (var (section, entries) in sections)
            {
                if (!string.IsNullOrEmpty(section))
                    sb.AppendLine($"[{section}]");
                foreach (var (key, value) in entries)
                    sb.AppendLine($"{key} = {value}");
                sb.AppendLine();
            }
            return sb.ToString();
        }
        private string SerializeXml()
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement("Config");
            doc.AppendChild(root);
            foreach (var (key, value) in _data)
            {
                var parts = key.Split('.');
                XmlElement current = root;
                foreach (var part in parts[..^1])
                {
                    var child = current.SelectSingleNode(part) as XmlElement;
                    if (child == null)
                    {
                        child = doc.CreateElement(part);
                        current.AppendChild(child);
                    }
                    current = child;
                }
                var leaf = doc.CreateElement(parts[^1]);
                leaf.InnerText = value;
                current.AppendChild(leaf);
            }
            using var sw = new StringWriter();
            using var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true });
            doc.WriteTo(xw);
            xw.Flush();
            return sw.ToString();
        }
        // --- format detection ---
        private static ConfigFormat DetectFormat(string path)
        {
            return System.IO.Path.GetExtension(path).ToLower() switch
            {
                ".json" => ConfigFormat.Json,
                ".ini" => ConfigFormat.Ini,
                ".xml" => ConfigFormat.Xml,
                _ => throw new Exception($"Cannot detect config format from extension: '{path}'")
            };
        }
    }
}