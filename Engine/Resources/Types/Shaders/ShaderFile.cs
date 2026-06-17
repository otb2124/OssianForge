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

            string globalPath = ResourceFile.CONTENT_FOLDER_PATH + "/" + Path;
            if (!File.Exists(globalPath))
                throw new Exception($"ShaderFile not found: '{globalPath}'");
            string raw = File.ReadAllText(globalPath);

            uint shader = Engine.Graphics.Batch.OpenGL.CreateShader(FileExtensionHelper.ExtensionToShaderType(GetExtension()));
            Engine.Graphics.Batch.OpenGL.ShaderSource(shader, raw);
            Engine.Graphics.Batch.OpenGL.CompileShader(shader);

            Engine.Graphics.Batch.OpenGL.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status == 0)
                throw new Exception(Engine.Graphics.Batch.OpenGL.GetShaderInfoLog(shader));

            Compiled = shader;
        }

        
    }
}
