using OssianForge.Engine.Resources.Shaders;
using OssianForge.Engine.Resources.Textures;
using System;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class ProbeMaterialProperty : MaterialProperty
    {
        public TextureResource TextureResource;
        public ProbeTextureResource ProbeResource;
        public float Reflectivity;
        public ProbeUpdateMode UpdateMode;

        private bool _hasRenderedOnce = false;

        public enum ProbeUpdateMode
        {
            Once,
            Always
        }

        public ProbeMaterialProperty(
            string textureId,
            string probeResourceId,
            string shaderId,
            ProbeUpdateMode updateMode = ProbeUpdateMode.Once,
            float reflectivity = 0.5f,
            params RenderAction[] actions)
            : base(shaderId, actions)
        {
            TextureResource = Engine.Resources.GetResource<TextureResource>(textureId)
                ?? throw new Exception($"TextureResource not found: '{textureId}'");
            ProbeResource = Engine.Resources.GetResource<ProbeTextureResource>(probeResourceId)
                ?? throw new Exception($"ProbeTextureResource not found: '{probeResourceId}'");
            Reflectivity = reflectivity;

            UpdateMode = updateMode;
        }


        public override void OnRender(Node node, double delta)
        {
            base.OnRender(node, delta);

            bool shouldUpdate = UpdateMode == ProbeUpdateMode.Always
                || (UpdateMode == ProbeUpdateMode.Once && !_hasRenderedOnce);

            if (!shouldUpdate) return;

            var transform = node.GetProperty<TransformProperty>();
            if (transform == null)
                throw new Exception($"ProbeProperty on node '{node.Id}' requires a TransformProperty for position.");

            ProbeResource.Position = transform.WorldTransform.Position;
            ProbeResource.Render(delta);

            if (UpdateMode == ProbeUpdateMode.Once)
                _hasRenderedOnce = true;
        }

        public override void Apply(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, Matrix4x4[] palette)
        {
            ShaderResource.Use();

            uint? diffuseSlot = null;
            uint? normalSlot = null;

            if (TextureResource.TextureFiles.Count > 0 && TextureResource.TextureFiles[0] != null)
            {
                TextureResource.TextureFiles[0].Bind(0);
                diffuseSlot = 0;
            }
            if (TextureResource.TextureFiles.Count > 1 && TextureResource.TextureFiles[1] != null)
            {
                TextureResource.TextureFiles[1].Bind(1);
                normalSlot = 1;
            }

            // Slot 2, same convention as ReflectiveMaterialProperty — the
            // only difference from that class is WHERE this cubemap's
            // pixels came from (rendered scene vs. static skybox images).
            // The shader consuming it doesn't need to know or care.
            ProbeResource.Bind(2);

            ShaderResource.Apply(new ApplyContext
            {
                Model = model,
                View = view,
                Projection = projection,
                ViewNoTranslation = Engine.Graphics.GetCurrentCamera().GetViewNoTranslation(),
                DiffuseTextureSlot = diffuseSlot,
                NormalTextureSlot = normalSlot,
                HasNormalTexture = normalSlot.HasValue,
                Lights = Engine.Nodes.NodeManager.GetLights(),
                CubemapTextureSlot = 2,
                Palette = palette
            });

            ShaderResource.SetFloat("uReflectivity", Reflectivity);
            ShaderResource.SetVector3("uCameraPos", Engine.Graphics.GetCurrentCamera().Position);
        }
    }
}