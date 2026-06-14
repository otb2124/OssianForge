using OssianForge.Engine.Resources.Animations;
using OssianForge.Engine.Resources.Colliders;
using OssianForge.Engine.Resources.Fonts;
using OssianForge.Engine.Resources.Meshes;
using OssianForge.Engine.Resources.Scripts;
using OssianForge.Engine.Resources.Shaders;
using OssianForge.Engine.Resources.Textures;

namespace OssianForge.Engine.Resources
{
    public class Resources
    {


        public ResourceLoader ResourceLoader;

        public Resources()
        {
            ResourceLoader = new ResourceLoader();
        }

        public void Initialize()
        {
            ResourceLoader.Initialize();
        }

        public void OnLoad()
        {
            ResourceLoader.OnLoad();
        }

        public T GetResourceFile<T>(string id) where T : ResourceFile
        {
            return ResourceLoader.ResourceFilesConfig.GetInstanceById<T>(id);
        }

        public T GetResource<T>(string id) where T : Resource
        {
            return ResourceLoader.ResourcesConfig.GetInstanceById<T>(id);
        }

        public T CreateScriptResourceInstance<T>(string resourceId, string typeName, params object[] args) where T : class
        {
            return GetResource<ScriptResource>(resourceId).CreateInstance<T>(typeName, args);
        }
    }
}
