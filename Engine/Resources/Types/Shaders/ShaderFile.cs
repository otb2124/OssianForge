using OssianForge.Content;
using Silk.NET.OpenGL;
using OssianForge.Engine.Utils;
using OssianForge.Engine.Ipc;


namespace OssianForge.Engine.Resources.ShaderFiles
{
    public class ShaderFile : Resource
    {

        public uint Compiled;
        public string Path;

        public ShaderFile(string id, string path)
        {
            Id = id;
            Path = path;
        }

        public override void Load()
        {
            base.Load();

            if (!CookerLink.Source.Exists(Path))
                throw new Exception($"ShaderFile not found: '{Path}'");
            string raw = CookerLink.Source.ReadAllText(Path);

            uint shader = Engine.Graphics.Batch.OpenGL.CreateShader(FileExtensionHelper.ExtensionToShaderType(GetExtension()));
            Engine.Graphics.Batch.OpenGL.ShaderSource(shader, raw);
            Engine.Graphics.Batch.OpenGL.CompileShader(shader);

            Engine.Graphics.Batch.OpenGL.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status == 0)
                throw new Exception(Engine.Graphics.Batch.OpenGL.GetShaderInfoLog(shader));

            Compiled = shader;
        }

        public string GetExtension()
        {
            return System.IO.Path.GetExtension(Path);
        }
    }
}