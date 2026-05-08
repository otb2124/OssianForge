// Engine/Graphics/Debug/DebugRenderer.cs
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources.Colliders;
using Silk.NET.OpenGL;
using System.Numerics;

namespace OssianForge.Engine.Graphics.Batch
{
    public class DebugRenderer : IDisposable
    {
        public bool Enabled = true;

        private uint _vao, _vbo;
        private uint _shaderHandle;

        private readonly List<float> _lineVerts = new();

        public DebugRenderer()
        {

        }

        public void Init()
        {
            CreateShader();
            CreateBuffers();
        }

        // ----------------------------------------------------------------
        // Public API — call from your render loop
        // ----------------------------------------------------------------

        public void BeginFrame() => _lineVerts.Clear();

        public void DrawBox(Vector3 min, Vector3 max, Vector3 color)
        {
            // 12 edges of a box
            var corners = new Vector3[]
            {
                new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z),
                new(max.X, min.Y, max.Z), new(min.X, min.Y, max.Z),
                new(min.X, max.Y, min.Z), new(max.X, max.Y, min.Z),
                new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z),
            };
            int[] edges = {
                0,1, 1,2, 2,3, 3,0,   // bottom
                4,5, 5,6, 6,7, 7,4,   // top
                0,4, 1,5, 2,6, 3,7    // verticals
            };
            foreach (var (a, b) in edges.Chunk(2).Select(e => (corners[e[0]], corners[e[1]])))
                AddLine(a, b, color);
        }

        public void DrawSphere(Vector3 center, float radius, Vector3 color, int segments = 16)
        {
            // Three circles: XY, XZ, YZ planes
            DrawCircle(center, radius, Vector3.UnitZ, Vector3.UnitX, color, segments);
            DrawCircle(center, radius, Vector3.UnitY, Vector3.UnitX, color, segments);
            DrawCircle(center, radius, Vector3.UnitX, Vector3.UnitY, color, segments);
        }

        public void DrawTriangles(IEnumerable<SubCollider.Triangle> triangles,
                                  TransformProperty t, Vector3 color)
        {
            foreach (var tri in triangles)
            {
                var wa = tri.A * t.Transform.Scale + t.Transform.Position;
                var wb = tri.B * t.Transform.Scale + t.Transform.Position;
                var wc = tri.C * t.Transform.Scale + t.Transform.Position;
                AddLine(wa, wb, color);
                AddLine(wb, wc, color);
                AddLine(wc, wa, color);
            }
        }

        /// <summary>
        /// Auto-dispatches based on collider type — call once per node.
        /// </summary>
        public void DrawCollider(ColliderProperty col, TransformProperty t)
        {
            if (!Enabled) return;

            var color = col.IsTrigger
                ? new Vector3(0.2f, 0.8f, 1.0f)   // cyan  = trigger
                : new Vector3(0.1f, 1.0f, 0.2f);   // green = solid


            foreach (var sub in col.ColliderResource.SubColliders)
                DrawTriangles(sub.Triangles, t, color);

        }

        /// <summary>
        /// Flush all queued lines to GPU and draw.
        /// </summary>
        public void EndFrame()
        {
            if (!Enabled || _lineVerts.Count == 0) return;

            var gl = Engine.Graphics.Batch.OpenGL;
            var verts = _lineVerts.ToArray();

            gl.BindVertexArray(_vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            unsafe
            {
                fixed (float* p = verts)
                    gl.BufferData(BufferTargetARB.ArrayBuffer,
                        (nuint)(verts.Length * sizeof(float)),
                        p, BufferUsageARB.StreamDraw);
            }

            gl.UseProgram(_shaderHandle);
            SetMat4("uView", Engine.Graphics.Camera.GetView());
            SetMat4("uProj", Engine.Graphics.Camera.GetProjection());

            gl.Disable(EnableCap.DepthTest);   // always visible, even through walls
            gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(_lineVerts.Count / 6));
            gl.Enable(EnableCap.DepthTest);

            gl.BindVertexArray(0);
        }

        // ----------------------------------------------------------------
        // Internals
        // ----------------------------------------------------------------

        private void AddLine(Vector3 a, Vector3 b, Vector3 color)
        {
            // pos(3) + color(3) per vertex
            _lineVerts.AddRange(new[] { a.X, a.Y, a.Z, color.X, color.Y, color.Z });
            _lineVerts.AddRange(new[] { b.X, b.Y, b.Z, color.X, color.Y, color.Z });
        }

        private void DrawCircle(Vector3 center, float radius,
                                Vector3 axisA, Vector3 axisB,
                                Vector3 color, int segments)
        {
            Vector3 prev = center + axisA * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = 2f * MathF.PI * i / segments;
                var next = center + (axisA * MathF.Cos(angle) + axisB * MathF.Sin(angle)) * radius;
                AddLine(prev, next, color);
                prev = next;
            }
        }

        private unsafe void CreateBuffers()
        {
            var gl = Engine.Graphics.Batch.OpenGL;

            _vao = gl.GenVertexArray();
            _vbo = gl.GenBuffer();

            gl.BindVertexArray(_vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            uint stride = 6 * sizeof(float);

            gl.EnableVertexAttribArray(0); // position
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);

            gl.EnableVertexAttribArray(1); // color
            gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));

            gl.BindVertexArray(0);
        }

        private void CreateShader()
        {
            var gl = Engine.Graphics.Batch.OpenGL;

            const string vert = @"
#version 330 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aColor;
out vec3 vColor;
uniform mat4 uView;
uniform mat4 uProj;
void main() {
    vColor = aColor;
    gl_Position = uProj * uView * vec4(aPos, 1.0);
}";
            const string frag = @"
#version 330 core
in vec3 vColor;
out vec4 FragColor;
void main() {
    FragColor = vec4(vColor, 1.0);
}";
            uint v = gl.CreateShader(ShaderType.VertexShader);
            gl.ShaderSource(v, vert);
            gl.CompileShader(v);

            uint f = gl.CreateShader(ShaderType.FragmentShader);
            gl.ShaderSource(f, frag);
            gl.CompileShader(f);

            _shaderHandle = gl.CreateProgram();
            gl.AttachShader(_shaderHandle, v);
            gl.AttachShader(_shaderHandle, f);
            gl.LinkProgram(_shaderHandle);
            gl.DeleteShader(v);
            gl.DeleteShader(f);
        }

        private unsafe void SetMat4(string name, Matrix4x4 m)
        {
            var gl = Engine.Graphics.Batch.OpenGL;
            int loc = gl.GetUniformLocation(_shaderHandle, name);
            gl.UniformMatrix4(loc, 1, false, (float*)&m);
        }

        public void Dispose()
        {
            var gl = Engine.Graphics.Batch.OpenGL;
            gl.DeleteVertexArray(_vao);
            gl.DeleteBuffer(_vbo);
            gl.DeleteProgram(_shaderHandle);
        }
    }
}