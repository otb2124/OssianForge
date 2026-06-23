using Silk.NET.OpenGL;
using OssianForge.Engine.Resources.MeshFiles;
using OssianForge.Engine.Resources.ShaderFiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using OssianForge.Engine.Resources.Meshes;
using OssianForge.Engine.Resources.Textures;

namespace OssianForge.Engine.Nodes.Props
{
    public class MeshProperty : NodeProperty
    {

        public string MeshResourceId;
        public MeshResource MeshResource;
        public MeshProperty(string meshId)
        {
            MeshResourceId = meshId;
            MeshResource = Engine.Resources.GetResource<MeshResource>(MeshResourceId)
                    ?? throw new Exception($"MeshResource not found: '{meshId}'");
        }

        public override void OnRender(Node node, double delta)
        {
            var transform = node.GetProperty<TransformProperty>();
            var materials = node.GetProperties<MaterialProperty>();
            var animation = node.GetProperty<AnimationProperty>();

            if (transform != null && materials.Count > 0)
                Engine.Graphics.Batch.DrawMesh(this, materials, transform, animation);
        }
    }


    
}
