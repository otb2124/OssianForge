using OssianForge.Engine.Nodes.Types;
using OssianForge.Engine.Resources.MeshFiles;
using OssianForge.Engine.Resources.Shaders;
using OssianForge.Engine.Resources.TextureFiles;
using OssianForge.Engine.Resources.Textures;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class MaterialProperty : NodeProperty
    {
        
        public TextureResource MaterialResource;
        public ShaderResource ShaderResource;

        public MaterialProperty(string materialId, string shaderId)
        {
            MaterialResource = Engine.Resources.GetResource(materialId) as TextureResource
                    ?? throw new Exception($"MaterialResource not found: '{materialId}'");

            ShaderResource = Engine.Resources.GetResource(shaderId) as ShaderResource
                    ?? throw new Exception($"ShaderResource not found: '{shaderId}'");
        }

        public void Apply(Matrix4x4 model, Matrix4x4 view, Matrix4x4 proj)
        {
            ShaderResource.Apply();

            if (MaterialResource.Texture != null)
            {
                MaterialResource.Texture.Bind(0);
                ShaderResource.SetInt("uTexture", 0);
            }

            if (MaterialResource.NormalTexture != null)
            {
                MaterialResource.NormalTexture.Bind(1);
                ShaderResource.SetInt("uNormalTexture", 1);
                ShaderResource.SetInt("uHasNormalTexture", 1);
            }
            else
            {
                ShaderResource.SetInt("uHasNormalTexture", 0);
            }

            ShaderResource.SetMatrix4("uModel", model);
            ShaderResource.SetMatrix4("uView", view);
            ShaderResource.SetMatrix4("uProjection", proj);

            // In MaterialProperty.Apply — check location before setting
            var lights = Engine.Nodes.NodeManager.GetNodesOfType(typeof(Node3D))
                .Select(n => n as Node3D)
                .Where(n => n?.GetProperty<LightProperty>() != null)
                .ToList();

            if (lights.Count > 0)
            {
                int lightPosLoc = Engine.Graphics.OpenGL.GetUniformLocation(ShaderResource.Handle, "uLightPos");
                if (lightPosLoc >= 0) // only set if uniform exists in this shader
                {
                    var lightNode = lights[0];
                    var light = lightNode.GetProperty<LightProperty>();
                    ShaderResource.SetVector3("uLightPos", lightNode.Transform.Position);
                    ShaderResource.SetVector3("uLightColor", light.Color);
                    ShaderResource.SetFloat("uLightIntensity", light.Intensity);
                    ShaderResource.SetFloat("uLightRadius", light.Radius);
                }
            }
        }
    }
}
