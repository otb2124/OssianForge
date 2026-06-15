using System;

namespace OssianForge.Engine.Resources.Config
{
    public class TreeConfig : ConfigFile
    {
        public string CurrentScene => GetString("currentScene", "");
        public string Version => GetString("version", "1.0.0");

        public TreeConfig(string id, string path) : base(id, path) { }

        public void SetCurrentScene(string sceneId)
        {
            Set("currentScene", sceneId);
            Save();
        }
    }
}