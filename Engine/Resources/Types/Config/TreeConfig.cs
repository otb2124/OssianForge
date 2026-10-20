using OssianForge.Content;
using OssianForge.Engine.Ipc;
using OssianForge.Engine.Nodes;
using System.Text.Json;

namespace OssianForge.Engine.Resources.Config
{
    public class TreeConfig : ConfigFile
    {
        public JsonDocument Document;

        public string MainScene => GetString("mainScene", "");
        public string Version => GetString("version", "1.0.0");

        public TreeConfig(string id, string path) : base(id, path) { }

        public override void Load()
        {
            base.Load();
            string raw = CookerLink.Source.ReadAllText(Path);
            Document = JsonDocument.Parse(raw);
        }

        public Node GetTreeNode()
        {
            if (!Document.RootElement.TryGetProperty("node", out var nodeEl))
                throw new Exception($"[TREE CONFIG] '{Id}' is missing a 'node' field.");

            return SceneBuilder.Build(nodeEl);
        }

        public void SetCurrentScene(string sceneId)
        {
            Set("mainScene", sceneId);
            Save();
        }
    }
}