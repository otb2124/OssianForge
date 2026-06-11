using OssianForge.Engine.Resources.Fonts;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Collections.Generic;
using OssianForge.Engine.Resources.Shaders;

namespace OssianForge.Engine.Nodes.Props
{
    public enum TextAlignment { Left, Center, Right }

    public class TextMaterialProperty : MaterialProperty
    {
        public FontResource FontResource;

        public string Content = "Hello World";
        public float FontSize = 64f;
        public Vector4 Color = Vector4.One;
        public TextAlignment Alignment = TextAlignment.Left;
        public int TextureWidth = 512;
        public int TextureHeight = 128;

        private string _lastContent;
        private float _lastFontSize;
        private Vector4 _lastColor;
        private bool NeedsRedraw =>
            _lastContent != Content ||
            _lastFontSize != FontSize ||
            _lastColor != Color;

        private uint _rtTexture;
        private bool _initialized;
        private uint _bakeVao;
        private uint _bakeVbo;
        private uint _bakeVertexCount;

        public TextMaterialProperty(string fontResourceId, string shaderId) : base(shaderId)
        {
            FontResource = Engine.Resources.GetResource(fontResourceId) as FontResource
                ?? throw new Exception($"FontResource not found: '{fontResourceId}'");

            _lastContent = null;
            _lastFontSize = -1f;
            _lastColor = new Vector4(-1f);
        }

        public override void Apply(Matrix4x4 transform, Matrix4x4[] palette)
        {
            if (string.IsNullOrEmpty(Content)) return;

            EnsureInitialized();

            if (NeedsRedraw)
                BakeToTexture();

            var gl = Engine.Graphics.Batch.OpenGL;
            ShaderResource.Use();

            var (view, _) = GetViewMatrices();
            ShaderResource.Apply(new ApplyContext
            {
                Model = transform,
                View = view,
                Projection = Engine.Graphics.GetCurrentCamera().GetProjection(),
                DiffuseTextureSlot = 0,
            });

            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, _rtTexture);
            ShaderResource.SetInt("uTexture", 0);
            ShaderResource.SetVec4("uTextColor", Color);
        }

        private unsafe void EnsureInitialized()
        {
            if (_initialized) return;

            var gl = Engine.Graphics.Batch.OpenGL;

            // RGBA8 texture that receives the baked glyphs
            _rtTexture = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, _rtTexture);
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                          (uint)TextureWidth, (uint)TextureHeight, 0,
                          PixelFormat.Rgba, PixelType.UnsignedByte, null);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            gl.BindTexture(TextureTarget.Texture2D, 0);

            // Bake VAO/VBO — pos(3) + normal(3) + uv(2), UV at location 2
            // to match the SDF vertex shader layout
            _bakeVao = gl.GenVertexArray();
            _bakeVbo = gl.GenBuffer();
            gl.BindVertexArray(_bakeVao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _bakeVbo);

            const uint stride = 8 * sizeof(float);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, GLEnum.Float, false, stride, (void*)0);
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 3, GLEnum.Float, false, stride, (void*)(3 * sizeof(float)));
            gl.EnableVertexAttribArray(2);
            gl.VertexAttribPointer(2, 2, GLEnum.Float, false, stride, (void*)(6 * sizeof(float)));

            gl.BindVertexArray(0);

            Console.WriteLine($"[TEXT] Initialized: rtTexture={_rtTexture}");
            _initialized = true;
        }

        private void BakeToTexture()
        {
            var gl = Engine.Graphics.Batch.OpenGL;
            var atlas = FontResource.AtlasData;
            if (atlas == null) { Console.WriteLine("[TEXT] atlas null"); return; }

            float emToPx = FontSize;
            float baseline = TextureHeight * 0.75f;
            float cursor = 0f;
            var verts = new List<float>();

            foreach (char c in Content)
            {
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
                float vBottom = g.AtlasY / atlas.AtlasHeight;               // atlasBounds.bottom → GL bottom
                float vTop = (g.AtlasY + g.AtlasH) / atlas.AtlasHeight; // atlasBounds.top    → GL top

                verts.AddRange(new[] { ndcX0, ndcY1, 0f, 0f, 0f, 1f, u0, vBottom }); // bottom-left
                verts.AddRange(new[] { ndcX1, ndcY1, 0f, 0f, 0f, 1f, u1, vBottom }); // bottom-right
                verts.AddRange(new[] { ndcX0, ndcY0, 0f, 0f, 0f, 1f, u0, vTop }); // top-left
                verts.AddRange(new[] { ndcX1, ndcY1, 0f, 0f, 0f, 1f, u1, vBottom }); // bottom-right
                verts.AddRange(new[] { ndcX1, ndcY0, 0f, 0f, 0f, 1f, u1, vTop }); // top-right
                verts.AddRange(new[] { ndcX0, ndcY0, 0f, 0f, 0f, 1f, u0, vTop }); // top-left

                cursor += g.Advance * emToPx;
            }

            var vertsArray = verts.ToArray();
            _bakeVertexCount = (uint)(vertsArray.Length / 8);
            if (_bakeVertexCount == 0) { Console.WriteLine("[TEXT] no verts"); return; }

            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _bakeVbo);
            unsafe
            {
                fixed (float* ptr = vertsArray)
                    gl.BufferData(BufferTargetARB.ArrayBuffer,
                        (nuint)(vertsArray.Length * sizeof(float)),
                        ptr, BufferUsageARB.DynamicDraw);
            }

            // Throw-away FBO — created here, deleted at the end of this method.
            // Attaching _rtTexture to it lets us render into it; deleting the FBO
            // afterwards leaves the texture intact.
            uint tempFbo = gl.GenFramebuffer();
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, tempFbo);
            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                                    FramebufferAttachment.ColorAttachment0,
                                    TextureTarget.Texture2D, _rtTexture, 0);

            var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            Console.WriteLine($"[TEXT] tempFbo={tempFbo} rtTexture={_rtTexture} status={status}");
            if (status != GLEnum.FramebufferComplete)
            {
                gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                gl.DeleteFramebuffer(tempFbo);
                Console.WriteLine("[TEXT] FBO incomplete, aborting bake");
                return;
            }

            gl.Viewport(0, 0, (uint)TextureWidth, (uint)TextureHeight);
            gl.ClearColor(0f, 0f, 0f, 0f);
            gl.Clear(ClearBufferMask.ColorBufferBit);
            gl.Disable(EnableCap.DepthTest);
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            ShaderResource.Use();
            FontResource.Bind(0);
            ShaderResource.SetVec4("uTextColor", Color);
            ShaderResource.SetFloat("uDistanceRange", atlas.DistanceRange);
            ShaderResource.Apply(new ApplyContext
            {
                Model = Matrix4x4.Identity,
                View = Matrix4x4.Identity,
                Projection = Matrix4x4.Identity,
                DiffuseTextureSlot = 0,
            });
            FontResource.Bind(0); // rebind after Apply in case it stomped slot 0

            gl.BindVertexArray(_bakeVao);
            gl.DrawArrays(PrimitiveType.Triangles, 0, _bakeVertexCount);
            gl.BindVertexArray(0);

            unsafe
            {
                byte[] px = new byte[4];
                fixed (byte* ptr = px)
                    gl.ReadPixels(TextureWidth / 2, TextureHeight / 2, 1, 1,
                                  PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
                Console.WriteLine($"[TEXT] FBO center pixel: r={px[0]} g={px[1]} b={px[2]} a={px[3]}");
            }

            // TextMaterialProperty.cs — end of BakeToTexture
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            gl.DeleteFramebuffer(tempFbo);
            gl.Viewport(0, 0,
                (uint)Engine.Graphics.WindowSize.X,
                (uint)Engine.Graphics.WindowSize.Y);
            // restore normal blend, leave it enabled for the scene
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            gl.Enable(EnableCap.DepthTest);

            _lastContent = Content;
            _lastFontSize = FontSize;
            _lastColor = Color;
        }

        public override void PostApply() { }

        public void Dispose()
        {
            var gl = Engine.Graphics.Batch.OpenGL;
            if (_bakeVao != 0) gl.DeleteVertexArray(_bakeVao);
            if (_bakeVbo != 0) gl.DeleteBuffer(_bakeVbo);
            if (_rtTexture != 0) gl.DeleteTexture(_rtTexture);
        }
    }
}