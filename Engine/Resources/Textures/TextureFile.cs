using Silk.NET.OpenGL;
using StbImageSharp;
using System.IO;

namespace OssianForge.Engine.Resources.TextureFiles
{
    public class TextureFile : ResourceFile
    {
        public uint Handle;

        public TextureFile(string id, string path)
        {
            Id = id;
            Path = path;
        }

        public override void Load()
        {
            string globalPath = ResourceFile.CONTENT_FOLDER_PATH + "/" + Path;

            StbImage.stbi_set_flip_vertically_on_load(1);
            using var stream = File.OpenRead(globalPath);
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            var gl = Engine.Graphics.OpenGL;

            Handle = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, Handle);

            unsafe
            {
                fixed (byte* ptr = image.Data)
                    gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
                        (uint)image.Width, (uint)image.Height, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }

            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.GenerateMipmap(TextureTarget.Texture2D);

            gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        public void Bind(uint slot = 0)
        {
            Engine.Graphics.OpenGL.ActiveTexture(TextureUnit.Texture0 + (int)slot);
            Engine.Graphics.OpenGL.BindTexture(TextureTarget.Texture2D, Handle);
        }

        public void Dispose()
        {
            Engine.Graphics.OpenGL.DeleteTexture(Handle);
        }
    }
}