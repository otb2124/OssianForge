using OssianForge.Engine.Graphics;
using OssianForge.Engine.Resources.Config;
using OssianForge.Engine.Resources.Scripts;


namespace OssianForge.Engine.Resources
{
    public class Resources
    {

        public ResourceScheduler ResourceScheduler;
        public ResourceLoader ResourceLoader;


        public Resources()
        {
            ResourceScheduler = new ResourceScheduler();
            ResourceLoader = new ResourceLoader();
        }

        public void Initialize()
        {
            ResourceLoader.InitializeCore();
            ResourceScheduler.Initialize();
            ResourceLoader.InitializeResources();
        }

        public void OnLoad()
        {
            ResourceLoader.OnLoad();
        }

        public void OnUpdate() 
        {
            SystemStats.Update();
        }

        public T GetResource<T>(string id) where T : Resource
        {
            return ResourceLoader.ResourcesConfig.GetInstanceById<T>(id);
        }

        public List<T> GetResources<T>() where T : Resource
            => ResourceLoader.ResourcesConfig.GetInstances<T>();

        public object CreateScriptResourceInstance(string packOrFileId, string typeName, params object[] args)
            => ResourceLoader.ResourcesConfig.FindScriptFile(packOrFileId, typeName).CreateInstance(typeName, args);

        public T CreateScriptResourceInstance<T>(string packOrFileId, string typeName, params object[] args) where T : class
            => ResourceLoader.ResourcesConfig.FindScriptFile(packOrFileId, typeName).CreateInstance<T>(typeName, args);

        public Type GetScriptType(string packOrFileId, string typeName)
        {
            var scriptFile = ResourceLoader.ResourcesConfig.FindScriptFile(packOrFileId, typeName);
            return scriptFile.CompiledAssembly.GetExportedTypes()
                .FirstOrDefault(t => t.Name == typeName)
                ?? throw new Exception($"Type '{typeName}' not found in '{packOrFileId}'");
        }

        public void InvokeAction(string actionId, object context = null, double? delta = null)
        {
            foreach (var config in GetResources<ActionsConfig>())
            {
                if (config.GetById(actionId) != null)
                {
                    config.Execute(actionId, context, delta);
                    return;
                }
            }
            throw new Exception($"[RESOURCES] Action '{actionId}' not found in any ActionsConfig.");
        }
    }
}
