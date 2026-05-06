using OssianForge.Engine.Nodes.Props;
using System.Numerics;

namespace OssianForge.Engine.Graphics.RenderTarget
{
    /// <summary>
    /// One fullscreen post-process effect.
    /// Create as many as you need and chain them via PostProcessStack.
    /// </summary>
    public class PostProcessPass : IDisposable
    {
        public Shader Shader;
        public bool Enabled = true;

        // Common built-in uniforms — set these each frame before Render()
        public float Gamma = 1.0f;
        public float Exposure = 1.0f;
        public float Saturation = 1.0f;   // 0 = grayscale, 1 = normal, 2 = oversaturated
        public bool Invert = false;
        public float Vignette = 0.0f;
        public float ChromaStrength = 0.0f;   // 0 = off, 0.003 = subtle, 0.01 = strong
        public Vector3 ColorTint = Vector3.One;  // RGB multiplier, (1,1,1) = neutral

        public PostProcessPass(string vertShaderId, string fragShaderId)
        {
            Shader = new Shader(vertShaderId, fragShaderId);
        }

        /// <summary>
        /// Push all built-in uniforms and any custom ones you set via ShaderResource.Set*()
        /// </summary>
        public void ApplyUniforms(uint inputTexture)
        {
            var gl = Engine.Graphics.OpenGL;

            Shader.Use();

            gl.ActiveTexture(Silk.NET.OpenGL.TextureUnit.Texture0);
            gl.BindTexture(Silk.NET.OpenGL.TextureTarget.Texture2D, inputTexture);
            Shader.SetInt("uScreen", 0);
            Shader.SetFloat("uGamma", Gamma);
            Shader.SetFloat("uExposure", Exposure);
            Shader.SetFloat("uSaturation", Saturation);
            Shader.SetInt("uInvert", Invert ? 1 : 0);
            Shader.SetFloat("uVignette", Vignette);
            Shader.SetFloat("uChroma", ChromaStrength);
            Shader.SetVector3("uColorTint", ColorTint);
        }

        public void Dispose() => Shader.Dispose();
    }
}
