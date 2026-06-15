using OssianForge.Engine.Resources.Config;
using OssianForge.Engine.Resources.Scripts;


namespace OssianForge.Engine.Resources
{
    public class Resources
    {


        public ResourceLoader ResourceLoader;
        public TreeConfig TreeConfig;


        public Resources()
        {
            ResourceLoader = new ResourceLoader();
            TreeConfig = new TreeConfig("configfile.tree", "ConfigFiles/Core/Tree/tree.json");
        }

        public void Initialize()
        {
            ResourceLoader.Initialize();
        }

        public void OnLoad()
        {
            ResourceLoader.OnLoad();
            TreeConfig.Load();
        }

        public T GetResourceFile<T>(string id) where T : ResourceFile
        {
            return ResourceLoader.ResourceFilesConfig.GetInstanceById<T>(id);
        }

        public T GetResource<T>(string id) where T : Resource
        {
            return ResourceLoader.ResourcesConfig.GetInstanceById<T>(id);
        }

        public object CreateScriptResourceInstance(string packOrFileId, string typeName, params object[] args)
            => ResourceLoader.ResourceFilesConfig.FindScriptFile(packOrFileId, typeName).CreateInstance(typeName, args);

        public T CreateScriptResourceInstance<T>(string packOrFileId, string typeName, params object[] args) where T : class
            => ResourceLoader.ResourceFilesConfig.FindScriptFile(packOrFileId, typeName).CreateInstance<T>(typeName, args);

        public Type GetScriptType(string packOrFileId, string typeName)
        {
            var scriptFile = ResourceLoader.ResourceFilesConfig.FindScriptFile(packOrFileId, typeName);
            return scriptFile.CompiledAssembly.GetExportedTypes()
                .FirstOrDefault(t => t.Name == typeName)
                ?? throw new Exception($"Type '{typeName}' not found in '{packOrFileId}'");
        }
    }
}
