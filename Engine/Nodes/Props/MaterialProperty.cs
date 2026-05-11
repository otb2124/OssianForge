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
            var gl = Engine.Graphics.Batch.OpenGL;

            ShaderResource.Use();

            // Bind textures and track which slots are used
            uint? diffuseSlot = null;
            uint? normalSlot = null;

            if (TextureResource.Texture != null)
            {
                TextureResource.Texture.Bind(0);
                diffuseSlot = 0;
            }

            if (TextureResource.NormalTexture != null)
            {
                TextureResource.NormalTexture.Bind(1);
                normalSlot = 1;
            }

            var lights = Engine.Nodes.NodeManager
                .GetNodesOfType(typeof(Node))
                .Select(n => new { Node = n, Emission = n.GetProperty<EmissionProperty>() })
                .Where(x => x.Emission != null)
                .Select(x => new LightData
                {
                    Position = x.Node.GetProperty<TransformProperty>().Transform.Position,
                    Color = x.Emission.Color,
                    Intensity = x.Emission.Intensity,
                    Radius = x.Emission.Radius,
                })
                .Take(16)
                .ToList();

            var view = Engine.Graphics.Camera.GetView();
            var viewNoTranslation = new Matrix4x4(
                view.M11, view.M12, view.M13, 0,
                view.M21, view.M22, view.M23, 0,
                view.M31, view.M32, view.M33, 0,
                0, 0, 0, 1);

            var context = new ApplyContext
            {
                Model = transform,
                View = view,
                Projection = Engine.Graphics.Camera.GetProjection(),
                ViewNoTranslation = viewNoTranslation,
                DiffuseTextureSlot = diffuseSlot,
                NormalTextureSlot = normalSlot,
                HasNormalTexture = normalSlot.HasValue,
                Lights = lights,
            };

            if (IsCull)
                ApplyCull(context);
            else
                ShaderResource.Apply(context);
        }

        // Skybox still handled separately since it's fundamentally different
        private void ApplyCull(ApplyContext context)
        {
            ShaderResource.SetVector3("uTopColor", new Vector3(0.4f, 0.6f, 1.0f));
            ShaderResource.SetVector3("uBottomColor", new Vector3(0.8f, 0.85f, 1.0f));
            ShaderResource.SetMatrix4("uView", context.ViewNoTranslation);
            ShaderResource.SetMatrix4("uProjection", context.Projection);
            ShaderResource.SetMatrix4("uModel", Matrix4x4.Identity);
        }
    }
}
