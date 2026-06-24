using OssianForge.Engine.Resources.Fonts;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Collections.Generic;
using OssianForge.Engine.Resources.Shaders;
using static System.Runtime.InteropServices.JavaScript.JSType;
using OssianForge.Engine.Utils;

namespace OssianForge.Engine.Nodes.Props
{
    public enum TextAlignment { Left, Center, Right }

    public class TextMaterialProperty : MaterialProperty
    {
        // ── Public properties ────────────────────────────────────────────
        public FontResource FontResource;
        public string Content = "Hello World";
        public float FontSize = 64f;
        public Vector4 Color = Vector4.One;
        public TextAlignment Alignment = TextAlignment.Left;
        public int TextureWidth = 512;
        public int TextureHeight = 128;

        // ── Dirty tracking ───────────────────────────────────────────────
        private string _lastContent;
        private float _lastFontSize;
        private Vector4 _lastColor;
        private bool NeedsRedraw =>
            _lastContent != Content ||
            _lastFontSize != FontSize ||
            _lastColor != Color;

        // ── GPU resources ────────────────────────────────────────────────
        private uint _rtTexture;
        private uint _bakeVao;
        private uint _bakeVbo;
        private uint _bakeVertexCount;

        public bool AutoSizeTransform = true;
        private bool _transformInitialized = false;

        // ────────────────────────────────────────────────────────────────
        public TextMaterialProperty(string fontResourceId, string shaderId, params RenderAction[] actions) : base(shaderId, actions)
        {
            FontResource = Engine.Resources.GetResource<FontResource>(fontResourceId)
                ?? throw new Exception($"FontResource not found: '{fontResourceId}'");

            _lastContent = null;
            _lastFontSize = -1f;
            _lastColor = new Vector4(-1f);

            // Init GPU resources once at construction, same as TextureMaterialProperty
            // which receives ready-made resources from TextureResource.
            InitGpuResources();
        }

        public TextMaterialProperty(string content, float fontSize, Vector4 color, string fontResourceId, string shaderId, params RenderAction[] actions) : base(shaderId, actions)
        {
            FontResource = Engine.Resources.GetResource<FontResource>(fontResourceId)
                ?? throw new Exception($"FontResource not found: '{fontResourceId}'");

            FontSize = fontSize;
            Color = color;
            Content = content;

            var (w, h) = FontUtils.MeasureText(content, FontSize, FontResource);
            TextureWidth = w;
            TextureHeight = h;

            _lastContent = null;
            _lastFontSize = -1f;
            _lastColor = new Vector4(-1f);

            InitGpuResources();
        }

        public override void OnStart(Node node)
        {
            base.OnStart(node);
        }

        public override void OnUpdate(Node node, double delta)
        {
            base.OnUpdate(node, delta);

            if (!AutoSizeTransform) return;

            var transform = node.GetProperty<TransformProperty>();
            if (transform == null || !transform.Started) return;

            if (!_transformInitialized || NeedsRedraw)
            {
                ApplyTransformScale(node);
                _transformInitialized = true;
            }
        }

        private void ApplyTransformScale(Node node)
        {
            var transform = node.GetProperty<TransformProperty>();
            if (transform == null) return;

            if (transform.RenderSpace == RenderSpace.ScreenSpace)
            {
                transform._transform.Position = transform.InitialTransform.Position;
                transform._transform.Scale = new Vector3(TextureWidth, TextureHeight, transform._transform.Scale.Z);
                transform.ApplyRenderSpaceDefaults(node);
                transform.RecomputeWorldTransform(node);
            }
        }

        public void SetContent(object content)
        {
            string text = content?.ToString() ?? string.Empty;
            Content = text;

            var (w, h) = FontUtils.MeasureText(text, FontSize, FontResource);

            if (w != TextureWidth || h != TextureHeight)
                Resize(w, h);

            BakeToTexture();

            var node = Engine.Nodes.NodeManager.GetNode(NodeId);
            if (AutoSizeTransform && node != null)
                ApplyTransformScale(node);
        }

        public void Resize(int width, int height)
        {
            var gl = Engine.Graphics.Batch.OpenGL;
            if (_rtTexture != 0) gl.DeleteTexture(_rtTexture);
            TextureWidth = width;
            TextureHeight = height;
            _rtTexture = Engine.Graphics.Batch.CreateRenderTexture(TextureWidth, TextureHeight);
        }

        // ── Apply (scene render pass) ────────────────────────────────────
        public override void Apply(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, Matrix4x4[] palette)
        {
            if (string.IsNullOrEmpty(Content)) return;

            if (NeedsRedraw)
                BakeToTexture();

            ShaderResource.Use();

            ShaderResource.Apply(new ApplyContext
            {
                Model = model,
                View = view,
                Projection = projection,
                ViewNoTranslation = Engine.Graphics.GetCurrentCamera().GetViewNoTranslation(),
                DiffuseTextureSlot = 0,
                Palette = palette
            });

            var gl = Engine.Graphics.Batch.OpenGL;
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, _rtTexture);
            ShaderResource.SetInt("uTexture", 0);
            ShaderResource.SetVector4("uTextColor", Color);
        }

        public override void PostApply() { }

        // ── GPU resource init ────────────────────────────────────────────
        private unsafe void InitGpuResources()
        {
            var gl = Engine.Graphics.Batch.OpenGL;

            _rtTexture = Engine.Graphics.Batch.CreateRenderTexture(TextureWidth, TextureHeight);
            (_bakeVao, _bakeVbo) = CreateBakeVao(gl);
        }

        

        private unsafe (uint vao, uint vbo) CreateBakeVao(GL gl)
        {
            uint vao = gl.GenVertexArray();
            uint vbo = gl.GenBuffer();

            gl.BindVertexArray(vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

            const uint stride = 8 * sizeof(float);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, GLEnum.Float, false, stride, (void*)0);
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 3, GLEnum.Float, false, stride, (void*)(3 * sizeof(float)));
            gl.EnableVertexAttribArray(2);
            gl.VertexAttribPointer(2, 2, GLEnum.Float, false, stride, (void*)(6 * sizeof(float)));

            gl.BindVertexArray(0);
            return (vao, vbo);
        }

        // ── Bake pass ────────────────────────────────────────────────────
        private void BakeToTexture()
        {
            var batch = Engine.Graphics.Batch;
            var verts = BuildGlyphVertices();
            if (verts.Length == 0) return;

            batch.UploadDynamicBuffer(_bakeVbo, verts);
            _bakeVertexCount = (uint)(verts.Length / 8);

            uint fbo = batch.BeginOffscreenPass(_rtTexture, TextureWidth, TextureHeight);
            if (fbo == 0) return;

            DrawGlyphs(batch.OpenGL);
            batch.EndOffscreenPass(fbo);

            _lastContent = Content;
            _lastFontSize = FontSize;
            _lastColor = Color;
        }

        private float[] BuildGlyphVertices()
        {
            var atlas = FontResource.AtlasData;
            float emToPx = FontSize;
            float lineHeight = atlas.LineHeight * emToPx;
            float cursor = 0f;
            int currentLine = 0;
            var verts = new List<float>();

            foreach (char c in Content)
            {
                if (c == '\n')
                {
                    cursor = 0f;
                    currentLine++;
                    continue;
                }

                float baseline = lineHeight * (currentLine + 0.75f);

                if (!atlas.Glyphs.TryGetValue(c, out var g))
                {
                    cursor += emToPx * 0.3f;
                    continue;
                }

                float left = cursor + g.PlaneBoundsLeft * emToPx;
                float right = cursor + g.PlaneBoundsRight * emToPx;
                float top = baseline - g.PlaneBoundsTop * emToPx;
                float bottom = baseline - g.PlaneBoundsBottom * emToPx;

                float ndcX0 = (left / TextureWidth) * 2f - 1f;
                float ndcX1 = (right / TextureWidth) * 2f - 1f;
                float ndcY0 = 1f - (top / TextureHeight) * 2f;
                float ndcY1 = 1f - (bottom / TextureHeight) * 2f;

                float u0 = g.AtlasX / atlas.AtlasWidth;
                float u1 = (g.AtlasX + g.AtlasW) / atlas.AtlasWidth;
                float vBottom = g.AtlasY / atlas.AtlasHeight;
                float vTop = (g.AtlasY + g.AtlasH) / atlas.AtlasHeight;

                verts.AddRange(new[] { ndcX0, ndcY1, 0f, 0f, 0f, 1f, u0, vBottom });
                verts.AddRange(new[] { ndcX1, ndcY1, 0f, 0f, 0f, 1f, u1, vBottom });
                verts.AddRange(new[] { ndcX0, ndcY0, 0f, 0f, 0f, 1f, u0, vTop });
                verts.AddRange(new[] { ndcX1, ndcY1, 0f, 0f, 0f, 1f, u1, vBottom });
                verts.AddRange(new[] { ndcX1, ndcY0, 0f, 0f, 0f, 1f, u1, vTop });
                verts.AddRange(new[] { ndcX0, ndcY0, 0f, 0f, 0f, 1f, u0, vTop });

                cursor += g.Advance * emToPx;
            }

            return verts.ToArray();
        }


        private void DrawGlyphs(GL gl)
        {
            var atlas = FontResource.AtlasData;

            ShaderResource.Use();
            ShaderResource.Apply(new ApplyContext
            {
                Model = Matrix4x4.Identity,
                View = Matrix4x4.Identity,
                Projection = Matrix4x4.Identity,
                DiffuseTextureSlot = 0,
            });

            FontResource.Bind(0);
            ShaderResource.SetVector4("uTextColor", Color);
            ShaderResource.SetFloat("uDistanceRange", atlas.DistanceRange);

            gl.BindVertexArray(_bakeVao);
            gl.DrawArrays(PrimitiveType.Triangles, 0, _bakeVertexCount);
            gl.BindVertexArray(0);
        }


        public float GetTextureSizeAspect()
        {
            return (float)TextureWidth / TextureHeight;
        }
        

        // ── Cleanup ──────────────────────────────────────────────────────
        public void Dispose()
        {
            var gl = Engine.Graphics.Batch.OpenGL;
            if (_bakeVao != 0) gl.DeleteVertexArray(_bakeVao);
            if (_bakeVbo != 0) gl.DeleteBuffer(_bakeVbo);
            if (_rtTexture != 0) gl.DeleteTexture(_rtTexture);
        }
    }
}