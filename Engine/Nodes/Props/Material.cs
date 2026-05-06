using OssianForge.Engine.Nodes.Types;
using OssianForge.Engine.Resources.TextureFiles;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class Material : NodeProperty
    {
        public Shader Shader;
        public TextureFile Texture;
        public TextureFile NormalTexture;


        public Material(string vertShaderId, string fragShaderId, string textureId = null, string normalTextureId = null)
        {
            Shader = new Shader(vertShaderId, fragShaderId);

            if (textureId != null)
                Texture = Engine.Resources.GetResourceFile(textureId) as TextureFile
                    ?? throw new Exception($"Texture not found: '{textureId}'");

            if (normalTextureId != null)
                NormalTexture = Engine.Resources.GetResourceFile(normalTextureId) as TextureFile
                    ?? throw new Exception($"Normal texture not found: '{normalTextureId}'");
        }

        public void Apply(Matrix4x4 model, Matrix4x4 view, Matrix4x4 proj)
        {
            Shader.Use();

            if (Texture != null)
            {
                Texture.Bind(0);
                Shader.SetInt("uTexture", 0);
            }

            if (NormalTexture != null)
            {
                NormalTexture.Bind(1);
                Shader.SetInt("uNormalTexture", 1);
                Shader.SetInt("uHasNormalTexture", 1);
            }
            else
            {
                Shader.SetInt("uHasNormalTexture", 0);
            }

            Shader.SetMatrix4("uModel", model);
            Shader.SetMatrix4("uView", view);
            Shader.SetMatrix4("uProjection", proj);

            // In Material.Apply — check location before setting
            var lights = Engine.Nodes.NodeManager.GetNodesOfType(typeof(Node3D))
                .Select(n => n as Node3D)
                .Where(n => n?.GetProperty<Light>() != null)
                .ToList();

            if (lights.Count > 0)
            {
                int lightPosLoc = Engine.Graphics.OpenGL.GetUniformLocation(Shader.Handle, "uLightPos");
                if (lightPosLoc >= 0) // only set if uniform exists in this shader
                {
                    var lightNode = lights[0];
                    var light = lightNode.GetProperty<Light>();
                    Shader.SetVector3("uLightPos", lightNode.Transform.Position);
                    Shader.SetVector3("uLightColor", light.Color);
                    Shader.SetFloat("uLightIntensity", light.Intensity);
                    Shader.SetFloat("uLightRadius", light.Radius);
                }
            }
        }

        public void SetMatrix4(string name, Matrix4x4 value) => Shader.SetMatrix4(name, value);
        public void SetInt(string name, int value) => Shader.SetInt(name, value);
        public void SetFloat(string name, float value) => Shader.SetFloat(name, value);
        public void SetVector3(string name, Vector3 value) => Shader.SetVector3(name, value);
    }
}
