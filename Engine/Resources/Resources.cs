using OssianForge.Engine.Resources.Colliders;
using OssianForge.Engine.Resources.Meshes;
using OssianForge.Engine.Resources.Scripts;
using OssianForge.Engine.Resources.Shaders;
using OssianForge.Engine.Resources.Textures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources
{
    public class Resources
    {

        public List<ResourceFile> ResourceFiles;
        public Dictionary<string, string> ResourceFileMap;

        public List<Resource> ResourceList;

        public Resources()
        {
            ResourceFiles = new List<ResourceFile>();
            ResourceFileMap = new Dictionary<string, string>();
            ResourceList = new List<Resource>();
        }

        public void Initialize()
        {
            ResourceFileMap = new Dictionary<string, string>
            {
                { "shaderfile.basic.vert", "ShaderFiles/Basic.vert"},
                { "shaderfile.basic.frag", "ShaderFiles/Basic.frag"},
                { "shaderfile.skybox.vert", "ShaderFiles/skybox.vert"},
                { "shaderfile.skybox.frag", "ShaderFiles/skybox.frag"},
                { "shaderfile.sprite.vert", "ShaderFiles/sprite.vert"},
                { "shaderfile.sprite.frag", "ShaderFiles/sprite.frag"},
                { "shaderfile.post.vert", "ShaderFiles/post.vert"},
                { "shaderfile.post.frag", "ShaderFiles/post.frag"},

                { "meshfile.donut", "MeshFiles/donut-v2.obj"},
                { "meshfile.donut56", "MeshFiles/donut-v56.obj"},
                { "meshfile.house", "MeshFiles/house.obj"},

                { "texturefile.IcingBaseColor",  "TextureFiles/IcingBaseColor.jpg" },
                { "texturefile.DonutBaseColor",  "TextureFiles/DonutBaseColor.jpg" },
                { "texturefile.house.barrel",  "TextureFiles/barrel.jpg" },
                { "texturefile.house.brick",  "TextureFiles/house_brick.jpg" },
                { "texturefile.house.windows",  "TextureFiles/house_windows.jpg" },
                { "texturefile.house.wood",  "TextureFiles/house_wood.jpg" },
                { "texturefile.brick.d",  "TextureFiles/brick_d.jpg" },
                { "texturefile.brick.n",  "TextureFiles/brick_n.jpg" },
                { "texturefile.light",  "TextureFiles/point_light_sprite.png" },
                { "texturefile.dices",  "TextureFiles/dices.png" },

                { "texturefile.cubemap.skybox.sea.right", "TextureFiles/panoramic-sea-right.png" },
                { "texturefile.cubemap.skybox.sea.left", "TextureFiles/panoramic-sea-left.png" },
                { "texturefile.cubemap.skybox.sea.top", "TextureFiles/panoramic-sea-top.png" },
                { "texturefile.cubemap.skybox.sea.bottom", "TextureFiles/panoramic-sea-bottom.png" },
                { "texturefile.cubemap.skybox.sea.front", "TextureFiles/panoramic-sea-front.png" },
                { "texturefile.cubemap.skybox.sea.back", "TextureFiles/panoramic-sea-back.png" },

                { "texturefile.cubemap.skybox.sky.right", "TextureFiles/panoramic-sky-right.png" },
                { "texturefile.cubemap.skybox.sky.left", "TextureFiles/panoramic-sky-left.png" },
                { "texturefile.cubemap.skybox.sky.top", "TextureFiles/panoramic-sky-top.png" },
                { "texturefile.cubemap.skybox.sky.bottom", "TextureFiles/panoramic-sky-bottom.png" },
                { "texturefile.cubemap.skybox.sky.front", "TextureFiles/panoramic-sky-front.png" },
                { "texturefile.cubemap.skybox.sky.back", "TextureFiles/panoramic-sky-back.png" },

                //{ "scriptfile.StateProperty", "ScriptFiles/Nodes/Props/StateProperty.cs" }
            };



            ResourceList = new List<Resource>
            {
                new MeshResource("mesh.donut", "meshfile.donut"),
                new MeshResource("mesh.donut56", "meshfile.donut56"),
                new MeshResource("mesh.house", "meshfile.house"),

                new MeshResource("mesh.cube", "fastmesh.cube"),
                new MeshResource("mesh.plane", "fastmesh.plane"),
                new MeshResource("mesh.quad", "fastmesh.quad"),
                new MeshResource("mesh.ball", "fastmesh.ball"),

                new BasicShaderResource("shader.basic", "shaderfile.basic.vert", "shaderfile.basic.frag"),
                new SkyboxShaderResource("shader.skybox", "shaderfile.skybox.vert", "shaderfile.skybox.frag"),
                new SpriteShaderResource("shader.sprite", "shaderfile.sprite.vert", "shaderfile.sprite.frag"),
                new ShaderResource("shader.post", "shaderfile.post.vert", "shaderfile.post.frag"),

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

                new ColliderResource("collider.house", "mesh.house"),
                new ColliderResource("collider.ball", "mesh.ball"),
                new ColliderResource("collider.plane", "mesh.plane"),
                new ColliderResource("collider.cube", "mesh.cube"),

                //new ScriptResource("script.StateProperty", "scriptfile.StateProperty")
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
                    _ => throw new Exception($"Unknown resource type for id: '{id}'")
                };

                ResourceFiles.Add(resource);
            }
        }

        public void OnLoad()
        {
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
