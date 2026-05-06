using OssianForge.Engine.Resources.Meshes;
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
                { "shaderfile.Basic.vert", "ShaderFiles/Basic.vert"},
                { "shaderfile.Basic.frag", "ShaderFiles/Basic.frag"},
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


            ResourceList = new List<Resource>
            {
                new MeshResource("123", "123")
            };
        }

        public void OnLoad()
        {
            foreach (var resource in ResourceFiles)
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
