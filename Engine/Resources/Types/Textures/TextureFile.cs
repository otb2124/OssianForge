using OssianForge.Content.Textures;
using OssianForge.Engine.Ipc;
using Silk.NET.OpenGL;
using StbImageSharp;
using System.IO;

namespace OssianForge.Engine.Resources.TextureFiles
{
    public class TextureFile : Resource
    {
        public uint Handle;
        public string Path;

        public TextureFile(string id, string path)
        {
            Id = id;
            Path = path;
        }

        public override void Load()
        {
            base.Load();
            var gl = Engine.Graphics.Batch.OpenGL;
            Handle = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, Handle);
            bool generateMips = true;

            if (CookerLink.IsCooked)
            {
                var tex = TextureCodec.Read(CookerLink.Source.ReadAllBytes(Path));
                unsafe
                {
                    for (int level = 0; level < tex.Mips.Length; level++)
                        fixed (byte* ptr = tex.Mips[level])
                            gl.TexImage2D(TextureTarget.Texture2D, level, InternalFormat.Rgba,
                                (uint)TextureData.MipWidth(tex.Width, level),
                                (uint)TextureData.MipHeight(tex.Height, level),
                                0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
                }
                generateMips = tex.Mips.Length == 1;
                if (!generateMips)
                    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, tex.Mips.Length - 1);
            }
            else
            {
                StbImage.stbi_set_flip_vertically_on_load(1);
                using var stream = CookerLink.Source.Open(Path);
                var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

                unsafe
                {
                    fixed (byte* ptr = image.Data)
                        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
                            (uint)image.Width, (uint)image.Height, 0,
                            PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
                }
            }

            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            if (generateMips) gl.GenerateMipmap(TextureTarget.Texture2D);

            gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        public void Bind(uint slot = 0)
        {
            Engine.Graphics.Batch.OpenGL.ActiveTexture(TextureUnit.Texture0 + (int)slot);
            Engine.Graphics.Batch.OpenGL.BindTexture(TextureTarget.Texture2D, Handle);
        }

        public void Dispose()
        {
            Engine.Graphics.Batch.OpenGL.DeleteTexture(Handle);
        }
    }
}