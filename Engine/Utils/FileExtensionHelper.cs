using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Utils
{
    public static class FileExtensionHelper
    {

        public static ShaderType ExtensionToShaderType(string extension)
        {
            ShaderType type = extension.Equals(".vert")
                ? ShaderType.VertexShader
                : ShaderType.FragmentShader;

            return type;
        }
    }
}
