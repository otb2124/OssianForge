using OssianForge.Engine.Resources.Config;
using System.Text.Json;

namespace OssianForge.Engine.Resources
{
    public class NodeDependency
    {
        public static readonly HashSet<string> AlwaysInclude = new()
        {
            //for now
            "configfile.pronouns",
            "configfile.actions",
            "configfile.inputKeys",
            "configfile.inputAxis",
            "configfile.statemachine",
            "shader.wireframe",
            "configfile.modes",
        };

        public HashSet<string> ResourceIds { get; } = new();

        public NodeDependency()
        {

        }

        public void ExtractTree(string treeConfigId)
        {
            ResolveAlwaysInclude();
            var treeConfig = Engine.Resources.GetResource<TreeConfig>(treeConfigId);
            ExtractDocument(treeConfig.Document);
        }

        public void ExtractScene(string sceneConfigId)
        {
            var sceneConfig = Engine.Resources.GetResource<SceneConfig>(sceneConfigId);
            ExtractDocument(sceneConfig.Document);
        }

        public void ExtractDocument(JsonDocument document)
        {
            ExtractFromNode(document.RootElement);
            ResolveDependencies();

            Console.WriteLine($"[NODE DEPENDENCY] Extracted {ResourceIds.Count} resource(s)");
        }

        private void ResolveAlwaysInclude()
        {
            var allRecords = Engine.Resources.ResourceLoader.ResourcesConfig.GetAllRecords();

            foreach (var pattern in AlwaysInclude)
                foreach (var record in allRecords)
                    if (record.Id.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        ResourceIds.Add(record.Id);
        }

        private void ExtractFromNode(JsonElement el)
        {
            if (el.TryGetProperty("properties", out var props))
                foreach (var prop in props.EnumerateArray())
                    ExtractFromProperty(prop);

            if (el.TryGetProperty("children", out var children))
                foreach (var child in children.EnumerateArray())
                    ExtractFromNode(child);
        }

        private void ExtractFromProperty(JsonElement el)
        {
            if (!el.TryGetProperty("data", out var data)) return;

            string raw = data.GetRawText();
            ExtractByPrefixes(raw, Resource.Prefixes, ResourceIds);
        }

        private static void ExtractByPrefixes(string raw, HashSet<string> prefixes, HashSet<string> ids)
        {
            foreach (var prefix in prefixes)
            {
                int start = 0;
                while (true)
                {
                    int idx = raw.IndexOf('"' + prefix, start, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) break;
                    int begin = idx + 1;
                    int end = raw.IndexOf('"', begin);
                    if (end < 0) break;
                    ids.Add(raw[begin..end]);
                    start = end + 1;
                }
            }
        }

        private void ResolveDependencies()
        {
            var allRecords = Engine.Resources.ResourceLoader.ResourcesConfig.GetAllRecords();
            bool changed;

            do
            {
                changed = false;
                foreach (var record in allRecords)
                {
                    if (!ResourceIds.Contains(record.Id)) continue;

                    foreach (var dataId in record.Data)
                    {
                        string prefix = dataId.Split('.')[0] + ".";
                        if (!Resource.Prefixes.Contains(prefix)) continue;

                        if (ResourceIds.Add(dataId)) changed = true;
                    }
                }
            }
            while (changed);

            Console.WriteLine("[NODE DEPENDENCY] Resolved resource ids:");
            foreach (var id in ResourceIds) Console.WriteLine($"  {id}");
        }

        public List<string> GetSortedResourceIds()
        {
            var allRecords = Engine.Resources.ResourceLoader.ResourcesConfig.GetAllRecords()
                .Where(r => ResourceIds.Contains(r.Id))
                .ToDictionary(r => r.Id);

            var sorted = new List<string>();
            var visited = new HashSet<string>();

            void Visit(string id)
            {
                if (!visited.Add(id)) return;
                if (!allRecords.TryGetValue(id, out var record)) return;

                foreach (var dataId in record.Data)
                    if (ResourceIds.Contains(dataId))
                        Visit(dataId);

                sorted.Add(id);
            }

            foreach (var id in ResourceIds)
                Visit(id);

            return sorted;
        }
    }
}