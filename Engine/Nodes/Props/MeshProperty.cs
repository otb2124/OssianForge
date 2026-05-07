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

namespace OssianForge.Engine.Nodes.Props
{
    public class MeshProperty : NodeProperty, IDisposable
    {
        public MeshResource MeshResource;

        public MeshProperty(string meshId)
        {
            MeshResource = Engine.Resources.GetResource(meshId) as MeshResource;
        }
        public virtual void Draw()  
        {
            MeshResource.Draw();
        }

        public virtual void Dispose()
        {
            MeshResource?.Dispose();
        }
    }


    
}
