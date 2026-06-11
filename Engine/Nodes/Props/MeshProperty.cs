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
    public class MeshProperty : NodeProperty, IDisposable
    {
        public MeshResource MeshResource;
        public MeshProperty(string meshId)
        {
            MeshResource = Engine.Resources.GetResource(meshId) as MeshResource
                    ?? throw new Exception($"MeshResource not found: '{meshId}'");
        }
        public virtual void Draw()  
        {
            MeshResource.Draw();
        }

        public virtual void Dispose()
        {
            MeshResource?.Dispose();
        }

        public override void OnRender(Node node, double delta)
        {
            var transform = node.GetProperty<TransformProperty>();
            var mesh = node.GetProperty<MeshProperty>();
            var materials = node.GetProperties<MaterialProperty>();
            var animation = node.GetProperty<AnimationProperty>(); // grab it

            if (transform != null && mesh != null && materials.Count > 0)
                Engine.Graphics.Batch.DrawMesh(mesh, materials, transform, animation);
        }
    }


    
}
