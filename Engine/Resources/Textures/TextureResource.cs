using OssianForge.Engine.Nodes.Types;
using OssianForge.Engine.Resources.Shaders;
using OssianForge.Engine.Resources.TextureFiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OssianForge.Engine.Resources.Textures
{
    public class TextureResource : Resource
    {

        public TextureFile Texture;
        public TextureFile NormalTexture;

        public string TextureId;
        public string TextureNormalId;


        public TextureResource(string id, string textureId, string normalTextureId = null)
        {
            //ShaderResource = new ShaderProperty(vertShaderId, fragShaderId);
            Id = id;
            TextureId = textureId;
            TextureNormalId = normalTextureId;
        }

        public override void Load()
        {
            if (TextureId != null)
                Texture = Engine.Resources.GetResourceFile(TextureId) as TextureFile
                    ?? throw new Exception($"Texture not found: '{TextureId}'");

            if (TextureNormalId != null)
                NormalTexture = Engine.Resources.GetResourceFile(TextureNormalId) as TextureFile
                    ?? throw new Exception($"Normal texture not found: '{TextureNormalId}'");
        }


        /*
        public void Apply(Matrix4x4 model, Matrix4x4 view, Matrix4x4 proj)
        {
            ShaderResource.Apply();

            if (Texture != null)
            {
                Texture.Bind(0);
                ShaderResource.SetInt("uTexture", 0);
            }

            if (NormalTexture != null)
            {
                NormalTexture.Bind(1);
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

        public void SetMatrix4(string name, Matrix4x4 value) => ShaderResource.SetMatrix4(name, value);
        public void SetInt(string name, int value) => ShaderResource.SetInt(name, value);
        public void SetFloat(string name, float value) => ShaderResource.SetFloat(name, value);
        public void SetVector3(string name, Vector3 value) => ShaderResource.SetVector3(name, value);

        */

    }
}
