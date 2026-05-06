using Silk.NET.OpenGL;
using OssianForge.Engine.Utils;


namespace OssianForge.Engine.Resources.ShaderFiles
{
    public class ShaderFile : ResourceFile
    {

        public uint Compiled;

        public ShaderFile(string id, string path)
        {
            Id = id;
            Path = path;
        }

        public override void Load()
        {
            base.Load();

            uint shader = Engine.Graphics.OpenGL.CreateShader(FileExtensionHelper.ExtensionToShaderType(GetExtension()));
            Engine.Graphics.OpenGL.ShaderSource(shader, Raw);
            Engine.Graphics.OpenGL.CompileShader(shader);

            Engine.Graphics.OpenGL.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status == 0)
                throw new Exception(Engine.Graphics.OpenGL.GetShaderInfoLog(shader));

            Compiled = shader;
        }

        
    }
}
