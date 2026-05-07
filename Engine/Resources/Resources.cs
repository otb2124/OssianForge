using OssianForge.Engine.Resources.Meshes;
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
            };



            ResourceList = new List<Resource>
            {
                new MeshResource("mesh.donut", "meshfile.donut"),
                new MeshResource("mesh.donut56", "meshfile.donut56"),
                new MeshResource("mesh.house", "meshfile.house"),

                new MeshResource("mesh.cube", "fastmesh.cube"),
                new MeshResource("mesh.plane", "fastmesh.plane"),
                new MeshResource("mesh.quad", "fastmesh.quad"),
                //new MeshResource("mesh.house", "meshfile.house"),

                new ShaderResource("shader.basic", "shaderfile.basic.vert", "shaderfile.basic.frag"),
                new ShaderResource("shader.skybox", "shaderfile.skybox.vert", "shaderfile.skybox.frag"),
                new ShaderResource("shader.sprite", "shaderfile.sprite.vert", "shaderfile.sprite.frag"),
                new ShaderResource("shader.post", "shaderfile.post.vert", "shaderfile.post.frag"),

                new TextureResource("texture.donut.icing", "texturefile.IcingBaseColor"),
                new TextureResource("texture.donut.base", "texturefile.DonutBaseColor"),
                new TextureResource("texture.house.barrel", "texturefile.house.barrel"),
                new TextureResource("texture.brick", "texturefile.brick.d", "texturefile.brick.n"),
                new TextureResource("texture.house.windows", "texturefile.house.windows"),
                new TextureResource("texture.house.wood", "texturefile.house.wood"),
                new TextureResource("texture.light", "texturefile.light"),
                new TextureResource("texture.dices", "texturefile.dices"),
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
    }
}
