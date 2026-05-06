using OssianForge.Engine.Resources.ShaderFiles;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources.Shaders
{
    public class ShaderResource : Resource
    {
        public List<SubShader> SubShaders;
        public uint Handle;

        public ShaderResource(string id, params string[] shaderFileIds)
        {
            Id = id;

            SubShaders = new();
            foreach (var shaderId in shaderFileIds)
                SubShaders.Add(new SubShader(shaderId));

            Link();
        }

        private void Link()
        {
            Handle = Engine.Graphics.OpenGL.CreateProgram();

            foreach (var sub in SubShaders)
                Engine.Graphics.OpenGL.AttachShader(Handle, sub.Handle);

            Engine.Graphics.OpenGL.LinkProgram(Handle);
            Engine.Graphics.OpenGL.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int status);
            if (status == 0)
                throw new Exception(Engine.Graphics.OpenGL.GetProgramInfoLog(Handle));

            // Detach after linking — good practice
            foreach (var sub in SubShaders)
                Engine.Graphics.OpenGL.DetachShader(Handle, sub.Handle);
        }

        public void Use() => Engine.Graphics.OpenGL.UseProgram(Handle);

        public void Dispose()
        {
            Engine.Graphics.OpenGL.DeleteProgram(Handle);
        }

        public void SetMatrix4(string name, Matrix4x4 matrix)
        {
            int loc = Engine.Graphics.OpenGL.GetUniformLocation(Handle, name);
            unsafe { Engine.Graphics.OpenGL.UniformMatrix4(loc, 1, false, (float*)&matrix); }
        }

        public void SetInt(string name, int value)
            => Engine.Graphics.OpenGL.Uniform1(Engine.Graphics.OpenGL.GetUniformLocation(Handle, name), value);

        public void SetFloat(string name, float value)
            => Engine.Graphics.OpenGL.Uniform1(Engine.Graphics.OpenGL.GetUniformLocation(Handle, name), value);

        public void SetVector3(string name, Vector3 value)
            => Engine.Graphics.OpenGL.Uniform3(Engine.Graphics.OpenGL.GetUniformLocation(Handle, name), value.X, value.Y, value.Z);
    }



    public class SubShader
    {
        public ShaderFile ShaderFile;
        public uint Handle;

        public SubShader(string shaderId)
        {
            ShaderFile = Engine.Resources.GetResourceFile(shaderId) as ShaderFile
                ?? throw new Exception($"ShaderFile not found: '{shaderId}'");
            Handle = ShaderFile.Compiled;
        }
    }
}
