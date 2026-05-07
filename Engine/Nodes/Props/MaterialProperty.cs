using OssianForge.Engine.Resources.Shaders;
using OssianForge.Engine.Resources.Textures;
using Silk.NET.Assimp;
using Silk.NET.OpenGL;
using System.Numerics;
using static OssianForge.Engine.Utils.Math;

namespace OssianForge.Engine.Nodes.Props
{
    public class MaterialProperty : NodeProperty
    {
        
        public TextureResource TextureResource;
        public ShaderResource ShaderResource;
        public bool IsCull;

        public MaterialProperty(string textureId, string shaderId, bool isCull = false)
        {
            TextureResource = Engine.Resources.GetResource(textureId) as TextureResource
                    ?? throw new Exception($"TextureResource not found: '{textureId}'");

            ShaderResource = Engine.Resources.GetResource(shaderId) as ShaderResource
                    ?? throw new Exception($"ShaderResource not found: '{shaderId}'");

            IsCull = isCull;
        }

        public void Apply(Matrix4x4 transform)
        {
            if (IsCull)
                ApplyCull(transform);
            else
                ApplyDefault(transform);
        }


        public void ApplyDefault(Matrix4x4 transform)
        {
            ShaderResource.Apply();

            if (TextureResource.Texture != null)
            {
                TextureResource.Texture.Bind(0);
                ShaderResource.SetInt("uTexture", 0);
            }

            if (TextureResource.NormalTexture != null)
            {
                TextureResource.NormalTexture.Bind(1);
                ShaderResource.SetInt("uNormalTexture", 1);
                ShaderResource.SetInt("uHasNormalTexture", 1);
            }
            else
            {
                ShaderResource.SetInt("uHasNormalTexture", 0);
            }

            ShaderResource.SetMatrix4("uModel", transform);
            ShaderResource.SetMatrix4("uView", Engine.Graphics.Camera.GetView());
            ShaderResource.SetMatrix4("uProjection", Engine.Graphics.Camera.GetProjection());

            // In MaterialProperty.Apply — check location before setting
            var lights = Engine.Nodes.NodeManager.GetNodesOfType(typeof(Node))
                .Select(n => n as Node)
                .Where(n => n?.GetProperty<EmissionProperty>() != null)
                .ToList();

            if (lights.Count > 0)
            {
                int lightPosLoc = Engine.Graphics.Batch.OpenGL.GetUniformLocation(ShaderResource.Handle, "uLightPos");
                if (lightPosLoc >= 0) // only set if uniform exists in this shader
                {
                    var lightNode = lights[0];
                    var light = lightNode.GetProperty<EmissionProperty>();
                    ShaderResource.SetVector3("uLightPos", lightNode.GetProperty<TransformProperty>().Transform.Position);
                    ShaderResource.SetVector3("uLightColor", light.Color);
                    ShaderResource.SetFloat("uLightIntensity", light.Intensity);
                    ShaderResource.SetFloat("uLightRadius", light.Radius);
                }
            }
        }


        public void ApplyCull(Matrix4x4 transform)
        {
            ShaderResource.Apply();
            ShaderResource.SetVector3("uTopColor", new Vector3(0.4f, 0.6f, 1.0f));
            ShaderResource.SetVector3("uBottomColor", new Vector3(0.8f, 0.85f, 1.0f));
            var view = Engine.Graphics.Camera.GetView();
            var viewNoTranslation = new Matrix4x4(
                view.M11, view.M12, view.M13, 0,
                view.M21, view.M22, view.M23, 0,
                view.M31, view.M32, view.M33, 0,
                0, 0, 0, 1);
            ShaderResource.SetMatrix4("uView", viewNoTranslation);
            ShaderResource.SetMatrix4("uProjection", Engine.Graphics.Camera.GetProjection());
            ShaderResource.SetMatrix4("uModel", Matrix4x4.Identity);
        }
    }
}
