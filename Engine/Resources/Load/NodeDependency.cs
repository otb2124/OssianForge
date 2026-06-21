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
        };

        public HashSet<string> ResourceFileIds { get; } = new();
        public HashSet<string> ResourceIds { get; } = new();

        public NodeDependency()
        {
            
        }

        public void ExtractTree(string treeConfigId)
        {
            ResolveAlwaysInclude();
            var treeConfig = Engine.Resources.GetResourceFile<TreeConfig>(treeConfigId);
            ExtractDocument(treeConfig.Document);
        }

        public void ExtractScene(string sceneConfigId)
        {
            var sceneConfig = Engine.Resources.GetResourceFile<SceneConfig>(sceneConfigId);
            ExtractDocument(sceneConfig.Document);
        }

        public void ExtractDocument(JsonDocument document)
        {
            ExtractFromNode(document.RootElement);
            ResolveResourceFileDependencies();

            Console.WriteLine($"[NODE DEPENDENCY] Extracted {ResourceFileIds.Count} resourceFiles, {ResourceIds.Count} resources");
        }

        private void ResolveAlwaysInclude()
        {
            var allResourceFileRecords = Engine.Resources.ResourceLoader.ResourceFilesConfig.GetAllRecords();
            var allResourceRecords = Engine.Resources.ResourceLoader.ResourcesConfig.GetAllRecords();

            foreach (var pattern in AlwaysInclude)
            {
                foreach (var record in allResourceFileRecords)
                    if (record.Id.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        ResourceFileIds.Add(record.Id);

                foreach (var record in allResourceRecords)
                    if (record.Id.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        ResourceIds.Add(record.Id);
            }
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
            var data = el.TryGetProperty("data", out var d) ? d : (JsonElement?)null;
            if (data == null) return;

            string raw = data.Value.GetRawText();
            ExtractByPrefixes(raw, ResourceFile.Prefixes, ResourceFileIds);
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

        private void ResolveResourceFileDependencies()
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

                        if (ResourceFile.Prefixes.Contains(prefix))
                        {
                            if (ResourceFileIds.Add(dataId)) changed = true;
                        }
                        else if (Resource.Prefixes.Contains(prefix))
                        {
                            if (ResourceIds.Add(dataId)) changed = true;
                        }
                    }
                }
            }
            while (changed);

            Console.WriteLine("[NODE DEPENDENCY] Resolved resource ids:");
            foreach (var id in ResourceIds) Console.WriteLine($"  {id}");
            Console.WriteLine("[NODE DEPENDENCY] Resolved resource file ids:");
            foreach (var id in ResourceFileIds) Console.WriteLine($"  {id}");
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
