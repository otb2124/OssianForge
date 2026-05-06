using Silk.NET.OpenGL;
using OssianForge.Engine.Resources.ShaderFiles;
using OssianForge.Engine.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Nodes.Props
{
    public class Shader : NodeProperty, IDisposable
    {

        public ShaderFile VertexShaderFile;
        public ShaderFile FragmentShaderFile;
        public uint Handle;

        public Shader(string vertexShaderFileId, string fragmentShaderFileId)
        {
            VertexShaderFile = Engine.Resources.GetResourceFile(vertexShaderFileId) as ShaderFile;
            FragmentShaderFile = Engine.Resources.GetResourceFile(fragmentShaderFileId) as ShaderFile;
            Compile();
        }

        public void SetMatrix4(string name, Matrix4x4 matrix)
        {
            int location = Engine.Graphics.OpenGL.GetUniformLocation(Handle, name);
            unsafe
            {
                Engine.Graphics.OpenGL.UniformMatrix4(location, 1, false, (float*)&matrix);
            }
        }

        public void SetInt(string name, int value)
        {
            int location = Engine.Graphics.OpenGL.GetUniformLocation(Handle, name);
            Engine.Graphics.OpenGL.Uniform1(location, value);
        }

        public void SetFloat(string name, float value)
        {
            int location = Engine.Graphics.OpenGL.GetUniformLocation(Handle, name);
            Engine.Graphics.OpenGL.Uniform1(location, value);
        }

        public void SetVector3(string name, Vector3 value)
        {
            int location = Engine.Graphics.OpenGL.GetUniformLocation(Handle, name);
            Engine.Graphics.OpenGL.Uniform3(location, value.X, value.Y, value.Z);
        }

        private void Compile()
        {
            uint vert = VertexShaderFile.Compiled;
            uint frag = FragmentShaderFile.Compiled;

            Handle = Engine.Graphics.OpenGL.CreateProgram();
            Engine.Graphics.OpenGL.AttachShader(Handle, vert);
            Engine.Graphics.OpenGL.AttachShader(Handle, frag);
            Engine.Graphics.OpenGL.LinkProgram(Handle);

            Engine.Graphics.OpenGL.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int status);
            if (status == 0)
                throw new Exception(Engine.Graphics.OpenGL.GetProgramInfoLog(Handle));

            Engine.Graphics.OpenGL.DeleteShader(vert);
            Engine.Graphics.OpenGL.DeleteShader(frag);
        }

        public void Use() => Engine.Graphics.OpenGL.UseProgram(Handle);

        public void Dispose() => Engine.Graphics.OpenGL.DeleteProgram(Handle);

    }
}
