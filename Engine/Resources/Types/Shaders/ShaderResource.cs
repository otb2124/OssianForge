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
        }

        public override void Load()
        {
            base.Load();
            foreach (var subShader in SubShaders)
                subShader.Load();

            Handle = Engine.Graphics.Batch.OpenGL.CreateProgram();

            foreach (var sub in SubShaders)
                Engine.Graphics.Batch.OpenGL.AttachShader(Handle, sub.Handle);

            Engine.Graphics.Batch.OpenGL.LinkProgram(Handle);
            Engine.Graphics.Batch.OpenGL.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int status);
            if (status == 0)
                throw new Exception(Engine.Graphics.Batch.OpenGL.GetProgramInfoLog(Handle));

            // Detach after linking — good practice
            foreach (var sub in SubShaders)
                Engine.Graphics.Batch.OpenGL.DetachShader(Handle, sub.Handle);
        }

        public virtual void Apply(ApplyContext context) { }

        public void Use() => Engine.Graphics.Batch.OpenGL.UseProgram(Handle);

        public void Dispose()
        {
            Engine.Graphics.Batch.OpenGL.DeleteProgram(Handle);
        }

        public void SetMatrix4(string name, Matrix4x4 matrix)
        {
            int loc = Engine.Graphics.Batch.OpenGL.GetUniformLocation(Handle, name);
            unsafe { Engine.Graphics.Batch.OpenGL.UniformMatrix4(loc, 1, false, (float*)&matrix); }
        }

        public void SetInt(string name, int value)
            => Engine.Graphics.Batch.OpenGL.Uniform1(Engine.Graphics.Batch.OpenGL.GetUniformLocation(Handle, name), value);

        public void SetIntIndexed(string array, int index, string field, int value)
            => SetInt($"{array}[{index}].{field}", value);

        public void SetFloat(string name, float value)
            => Engine.Graphics.Batch.OpenGL.Uniform1(Engine.Graphics.Batch.OpenGL.GetUniformLocation(Handle, name), value);

        public void SetVector3(string name, Vector3 value)
            => Engine.Graphics.Batch.OpenGL.Uniform3(Engine.Graphics.Batch.OpenGL.GetUniformLocation(Handle, name), value.X, value.Y, value.Z);

        public void SetVector3Indexed(string array, int i, string field, Vector3 v)
             => SetVector3($"{array}[{i}].{field}", v);

        public void SetVector4(string name, Vector4 v)
        {
            int loc = Engine.Graphics.Batch.OpenGL.GetUniformLocation(Handle, name);
            if (loc >= 0) Engine.Graphics.Batch.OpenGL.Uniform4(loc, v.X, v.Y, v.Z, v.W);
        }

        public void SetFloatIndexed(string array, int i, string field, float v)
            => SetFloat($"{array}[{i}].{field}", v);
    }



    public class SubShader
    {
        public string FileId;
        public ShaderFile ShaderFile;
        public uint Handle;

        public SubShader(string shaderId)
        {
            FileId = shaderId;
        }

        public void Load()
        {
            ShaderFile = Engine.Resources.GetResourceFile<ShaderFile>(FileId)
                ?? throw new Exception($"ShaderFile not found: '{FileId}'");
            Handle = ShaderFile.Compiled;
        }
    }


    public struct ApplyContext
    {
        public Matrix4x4 Model;
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Matrix4x4 ViewNoTranslation;
        public uint? DiffuseTextureSlot;
        public uint? NormalTextureSlot;
        public bool HasNormalTexture;
        public List<LightData> Lights;  // replaces the four LightPos/Color/etc fields
        public uint? CubemapTextureSlot;
        public Matrix4x4[] Palette;
    }

    public enum LightType { Point = 0, Sun = 1, Spot = 2 }

    public struct LightData
    {
        public LightType Type;
        public Vector3 Position;   // Point + Spot
        public Vector3 Direction;  // Sun + Spot
        public Vector3 Color;
        public float Intensity;
        public float Radius;     // Point + Spot falloff
        public float InnerCutoff; // Spot, cosine
        public float OuterCutoff; // Spot, cosine
    }
}