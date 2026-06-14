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

        public List<ResourceFile> ResourceFiles;
        public Dictionary<string, string> ResourceFileMap;

        public List<Resource> ResourceList;




        public ResourceLoader ResourceLoader;

        public Resources()
        {
            ResourceFiles = new List<ResourceFile>();
            ResourceFileMap = new Dictionary<string, string>();
            ResourceList = new List<Resource>();


            ResourceLoader = new ResourceLoader();
        }

        public void Initialize()
        {
            ResourceFileMap = new Dictionary<string, string>
            {
                { "shaderfile.basic.vert", "ShaderFiles/basic.vert"},
                { "shaderfile.basic.frag", "ShaderFiles/basic.frag"},
                { "shaderfile.unlit.frag", "ShaderFiles/unlit.frag"},
                { "shaderfile.skybox.vert", "ShaderFiles/skybox.vert"},
                { "shaderfile.skybox.frag", "ShaderFiles/skybox.frag"},
                { "shaderfile.post.vert", "ShaderFiles/post.vert"},
                { "shaderfile.post.frag", "ShaderFiles/post.frag"},
                { "shaderfile.sdf.frag", "ShaderFiles/sdf.frag"},
                { "shaderfile.sdf.vert", "ShaderFiles/sdf.vert"},

                { "meshfile.remy", "MeshFiles/remy.fbx"},

                { "animationfile.remy.idle", "AnimationFiles/remy.idle.fbx" },
                { "animationfile.remy.walking", "AnimationFiles/remy.walking.fbx" },
                { "animationfile.remy.jumping", "AnimationFiles/remy.jumping.fbx" },
                { "animationfile.remy.jumping_jacks", "AnimationFiles/remy.jumping_jacks.fbx" },
                { "animationfile.remy.backflip", "AnimationFiles/remy.backflip.fbx" },
                { "animationfile.remy.waving", "AnimationFiles/remy.waving.fbx" },

                { "texturefile.IcingBaseColor",  "TextureFiles/3d/IcingBaseColor.jpg" },
                { "texturefile.DonutBaseColor",  "TextureFiles/3d/DonutBaseColor.jpg" },
                { "texturefile.house.barrel",  "TextureFiles/3d/barrel.jpg" },
                { "texturefile.house.brick",  "TextureFiles/3d/house_brick.jpg" },
                { "texturefile.house.windows",  "TextureFiles/3d/house_windows.jpg" },
                { "texturefile.house.wood",  "TextureFiles/3d/house_wood.jpg" },
                { "texturefile.brick.d",  "TextureFiles/3d/brick_d.jpg" },
                { "texturefile.brick.n",  "TextureFiles/3d/brick_n.jpg" },
                { "texturefile.light",  "TextureFiles/3d/point_light_sprite.png" },
                { "texturefile.dices",  "TextureFiles/3d/dices.png" },
                { "texturefile.cubemap.skybox.sea.right", "TextureFiles/3d/panoramic-sea-right.png" },
                { "texturefile.cubemap.skybox.sea.left", "TextureFiles/3d/panoramic-sea-left.png" },
                { "texturefile.cubemap.skybox.sea.top", "TextureFiles/3d/panoramic-sea-top.png" },
                { "texturefile.cubemap.skybox.sea.bottom", "TextureFiles/3d/panoramic-sea-bottom.png" },
                { "texturefile.cubemap.skybox.sea.front", "TextureFiles/3d/panoramic-sea-front.png" },
                { "texturefile.cubemap.skybox.sea.back", "TextureFiles/3d/panoramic-sea-back.png" },
                { "texturefile.cubemap.skybox.sky.right", "TextureFiles/3d/panoramic-sky-right.png" },
                { "texturefile.cubemap.skybox.sky.left", "TextureFiles/3d/panoramic-sky-left.png" },
                { "texturefile.cubemap.skybox.sky.top", "TextureFiles/3d/panoramic-sky-top.png" },
                { "texturefile.cubemap.skybox.sky.bottom", "TextureFiles/3d/panoramic-sky-bottom.png" },
                { "texturefile.cubemap.skybox.sky.front", "TextureFiles/3d/panoramic-sky-front.png" },
                { "texturefile.cubemap.skybox.sky.back", "TextureFiles/3d/panoramic-sky-back.png" },

                { "texturefile.font.roboto", "TextureFiles/Fonts/roboto.png" },

                //{ "scriptfile.StateProperty", "ScriptFiles/Nodes/Props/StateProperty.cs" }

                { "configfile.font.roboto", "ConfigFiles/Fonts/roboto.json" }
            };



            ResourceList = new List<Resource>
            {
                new MeshResource("mesh.cube", "fastmesh.cube"),
                new MeshResource("mesh.plane", "fastmesh.plane"),
                new MeshResource("mesh.quad", "fastmesh.quad"),
                new MeshResource("mesh.thickquad", "fastmesh.thickquad"),
                new MeshResource("mesh.ball", "fastmesh.ball"),

                new MeshResource("mesh.remy", "meshfile.remy"),

                new AnimationResource("animation.remy", "animationfile.remy.idle", "animationfile.remy.walking", "animationfile.remy.jumping", "animationfile.remy.jumping_jacks", "animationfile.remy.waving", "animationfile.remy.backflip"),

                new BasicShaderResource("shader.basic", "shaderfile.basic.vert", "shaderfile.basic.frag"),
                new BasicShaderResource("shader.unlit", "shaderfile.basic.vert", "shaderfile.unlit.frag"),
                new SkyboxShaderResource("shader.skybox", "shaderfile.skybox.vert", "shaderfile.skybox.frag"),
                new ShaderResource("shader.post", "shaderfile.post.vert", "shaderfile.post.frag"),
                new SdfShaderResource("shader.sdf", "shaderfile.sdf.vert", "shaderfile.sdf.frag"),

                new TextureResource("texture.donut.icing", "texturefile.IcingBaseColor"),
                new TextureResource("texture.donut.base", "texturefile.DonutBaseColor"),
                new TextureResource("texture.house.barrel", "texturefile.house.barrel"),
                new TextureResource("texture.brick", "texturefile.brick.d", "texturefile.brick.n"),
                new TextureResource("texture.house.windows", "texturefile.house.windows"),
                new TextureResource("texture.house.wood", "texturefile.house.wood"),
                new TextureResource("texture.light", "texturefile.light"),
                new TextureResource("texture.dices", "texturefile.dices"),
                new TextureResource("texture.cubemap.skybox.sea.right", "texturefile.cubemap.skybox.sea.right"),
                new TextureResource("texture.cubemap.skybox.sea.left", "texturefile.cubemap.skybox.sea.left"),
                new TextureResource("texture.cubemap.skybox.sea.top", "texturefile.cubemap.skybox.sea.top"),
                new TextureResource("texture.cubemap.skybox.sea.bottom", "texturefile.cubemap.skybox.sea.bottom"),
                new TextureResource("texture.cubemap.skybox.sea.front", "texturefile.cubemap.skybox.sea.front"),
                new TextureResource("texture.cubemap.skybox.sea.back", "texturefile.cubemap.skybox.sea.back"),
                new TextureResource("texture.cubemap.skybox.sky.right", "texturefile.cubemap.skybox.sky.right"),
                new TextureResource("texture.cubemap.skybox.sky.left", "texturefile.cubemap.skybox.sky.left"),
                new TextureResource("texture.cubemap.skybox.sky.top", "texturefile.cubemap.skybox.sky.top"),
                new TextureResource("texture.cubemap.skybox.sky.bottom", "texturefile.cubemap.skybox.sky.bottom"),
                new TextureResource("texture.cubemap.skybox.sky.front", "texturefile.cubemap.skybox.sky.front"),
                new TextureResource("texture.cubemap.skybox.sky.back", "texturefile.cubemap.skybox.sky.back"),

                new CubemapTextureResource("cubemap.skybox.sea", "texture.cubemap.skybox.sea.right", "texture.cubemap.skybox.sea.left", "texture.cubemap.skybox.sea.top", "texture.cubemap.skybox.sea.bottom", "texture.cubemap.skybox.sea.front", "texture.cubemap.skybox.sea.back"),
                new CubemapTextureResource("cubemap.skybox.sky", "texture.cubemap.skybox.sky.right", "texture.cubemap.skybox.sky.left", "texture.cubemap.skybox.sky.top", "texture.cubemap.skybox.sky.bottom", "texture.cubemap.skybox.sky.front", "texture.cubemap.skybox.sky.back"),

                new ColliderResource("collider.ball", "mesh.ball"),
                new ColliderResource("collider.plane", "mesh.plane"),
                new ColliderResource("collider.cube", "mesh.cube"),
                new ColliderResource("collider.thickquad", "mesh.thickquad"),

                new ColliderResource("collider.remy", "mesh.remy"),

                //new ScriptResource("script.StateProperty", "scriptfile.StateProperty")

                new FontResource("font.roboto", "texturefile.font.roboto", "configfile.font.roboto")
            };


            foreach (var record in ResourceFileMap)
            {
                string id = record.Key;
                string path = record.Value;

                ResourceFile resource = id.Split('.')[0] switch
                {
                    "shaderfile" => new ShaderFiles.ShaderFile(id, path),
                    "meshfile" => new MeshFiles.MeshFile(id, path),
                    "texturefile" => new TextureFiles.TextureFile(id, path),
                    "scriptfile" => new Scripts.ScriptFile(id, path),
                    "animationfile" => new Animations.AnimationFile(id, path),
                    "configfile" => new Config.ConfigFile(id, path),
                    _ => throw new Exception($"Unknown resource type for id: '{id}'")
                };

                ResourceFiles.Add(resource);
            }
        }

        public void OnLoad()
        {
            ResourceLoader.Load();


            foreach (var resourceFile in ResourceFiles)
            {
                resourceFile.Load();
            }

            foreach (var resource in ResourceList)
            {
                resource.Load();
            }
        }

        public ResourceFile GetResourceFile(string id)
        {
            return ResourceFiles.FirstOrDefault(r => r.Id == id);
        }

        public Resource GetResource(string id)
        {
            return ResourceList.FirstOrDefault(r => r.Id == id);
        }

        public T CreateScriptResourceInstance<T>(string resourceId, string typeName, params object[] args) where T : class
        {
            return (GetResource(resourceId) as ScriptResource).CreateInstance<T>(typeName, args);
        }
    }
}
