using OssianForge.Engine.Resources.Shaders;
using OssianForge.Engine.Resources.Textures;
using Silk.NET.Assimp;
using Silk.NET.OpenGL;
using System.Numerics;
using static OssianForge.Engine.Utils.MathUtils;

namespace OssianForge.Engine.Nodes.Props
{
    public class MaterialProperty : NodeProperty
    {
        public ShaderResource ShaderResource;

        public Action BeginAction;
        public Action EndAction;

        public MaterialProperty(string shaderId)
        {
            ShaderResource = Engine.Resources.GetResource(shaderId) as ShaderResource
                ?? throw new Exception($"ShaderResource not found: '{shaderId}'");
        }

        public virtual void Apply(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, Matrix4x4[] palette) { }

        public virtual void PostApply() { }

        

        
    }
}
