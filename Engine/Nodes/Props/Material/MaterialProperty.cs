using OssianForge.Engine.Resources.Shaders;
using Silk.NET.OpenGL;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    /// <summary>
    /// Stateful OpenGL toggles that can be applied around a draw call.
    /// Each value encodes both the enable (BeginAction) and restore (EndAction) side.
    ///
    /// Usage:
    ///   new TextureMaterialProperty("texture.brick", "shader.unlit",
    ///       RenderAction.DisableDepthTest)
    ///
    /// Multiple actions can be combined:
    ///   RenderAction.DisableDepthTest | RenderAction.DisableDepthWrite
    /// </summary>
    [Flags]
    public enum RenderAction
    {
        None = 0,

        // Disables depth testing for this draw call, re-enables it after.
        // Use for screen-space / UI elements that must always appear on top.
        DisableDepthTest = 1 << 0,

        // Disables writing to the depth buffer (GL_DEPTH_WRITEMASK = false).
        // Useful for transparent or overlay geometry that shouldn't occlude others.
        DisableDepthWrite = 1 << 1,

        // Enables additive blending (src + dst) instead of the default alpha blend.
        // Restore sets it back to SrcAlpha / OneMinusSrcAlpha.
        AdditiveBlend = 1 << 2,

        // Disables face culling. Useful for double-sided quads / UI elements.
        DisableCulling = 1 << 3,

        // Enables wireframe polygon mode. Restore sets it back to Fill.
        Wireframe = 1 << 4,
    }

    public class MaterialProperty : NodeProperty
    {
        public ShaderResource ShaderResource;

        // Called immediately before Apply() — set up any extra GL state here.
        public Action BeginAction;

        // Called immediately after PostApply() — restore GL state changed in BeginAction.
        public Action EndAction;

        public MaterialProperty(string shaderId, params RenderAction[] actions)
        {
            ShaderResource = Engine.Resources.GetResource<ShaderResource>(shaderId)
                ?? throw new Exception($"ShaderResource not found: '{shaderId}'");

            if (actions.Length > 0)
            {
                // Fold all requested actions into a single combined flag.
                RenderAction combined = RenderAction.None;
                foreach (var a in actions)
                    combined |= a;

                BuildActionPair(combined);
            }
        }

        public virtual void Apply(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, Matrix4x4[] palette) { }
        public virtual void PostApply() { }

        // -----------------------------------------------------------------------
        // Action pair builder
        // -----------------------------------------------------------------------

        private void BuildActionPair(RenderAction flags)
        {
            var gl = Engine.Graphics.Batch.OpenGL;

            var begins = new List<Action>();
            var ends = new List<Action>();

            if (flags.HasFlag(RenderAction.DisableDepthTest))
            {
                begins.Add(() => gl.Disable(EnableCap.DepthTest));
                ends.Add(() => gl.Enable(EnableCap.DepthTest));
            }

            if (flags.HasFlag(RenderAction.DisableDepthWrite))
            {
                begins.Add(() => gl.DepthMask(false));
                ends.Add(() => gl.DepthMask(true));
            }

            if (flags.HasFlag(RenderAction.AdditiveBlend))
            {
                begins.Add(() => gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One));
                ends.Add(() => gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha));
            }

            if (flags.HasFlag(RenderAction.DisableCulling))
            {
                begins.Add(() => gl.Disable(EnableCap.CullFace));
                ends.Add(() => gl.Enable(EnableCap.CullFace));
            }

            if (flags.HasFlag(RenderAction.Wireframe))
            {
                begins.Add(() => gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line));
                ends.Add(() => gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill));
            }

            if (begins.Count == 0) return;

            // Compose into single delegates so Batch.DrawSubMesh pays one call each.
            BeginAction = () => { foreach (var a in begins) a(); };
            EndAction = () => { foreach (var a in ends) a(); };
        }
    }
}