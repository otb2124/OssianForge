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
            string globalPath = CONTENT_FOLDER_PATH + "/" + Path;
            string raw = File.ReadAllText(globalPath);
            Document = JsonDocument.Parse(raw);
        }

        public Node GetTreeNode()
        {
            if (!Document.RootElement.TryGetProperty("node", out var nodeEl))
                throw new Exception($"[TREE CONFIG] '{Id}' is missing a 'node' field.");

            return SceneConfig.ParseNode(nodeEl);
        }

        public void SetCurrentScene(string sceneId)
        {
            Set("mainScene", sceneId);
            Save();
        }
    }
}