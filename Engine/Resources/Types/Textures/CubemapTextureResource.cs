using Silk.NET.OpenGL;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace OssianForge.Engine.Resources.Textures
{
    public class CubemapTextureResource : Resource
    {
        public uint Handle;

        // right, left, top, bottom, front, back
        public TextureResource[] Faces;
        public string[] FaceIds;

        public CubemapTextureResource(string id, params string[] textureResourceIds)
        {
            if (textureResourceIds?.Length != 6)
                throw new ArgumentException("Cubemap requires exactly 6 face TextureResources.");

            Id = id;
            FaceIds = textureResourceIds;
            Faces = new TextureResource[FaceIds.Length];
        }

        public override void Load()
        {
            base.Load();
            for (int i = 0; i < FaceIds.Length; i++)
            {
                Faces[i] = Engine.Resources.GetResource<TextureResource>(FaceIds[i]);
            }

            var gl = Engine.Graphics.Batch.OpenGL;
            Handle = gl.GenTexture();
            gl.BindTexture(TextureTarget.TextureCubeMap, Handle);

            var targets = new[]
            {
                TextureTarget.TextureCubeMapPositiveX, // right
                TextureTarget.TextureCubeMapNegativeX, // left
                TextureTarget.TextureCubeMapPositiveY, // top
                TextureTarget.TextureCubeMapNegativeY, // bottom
                TextureTarget.TextureCubeMapPositiveZ, // front
                TextureTarget.TextureCubeMapNegativeZ, // back
            };

            StbImage.stbi_set_flip_vertically_on_load(0);

            for (int i = 0; i < 6; i++)
            {
                var faceFile = Faces[i].TextureFiles[0];
                string globalPath = CONTENT_FOLDER_PATH + "/" + faceFile.Path;
                using var stream = File.OpenRead(globalPath);
                var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

                unsafe
                {
                    fixed (byte* ptr = image.Data)
                        gl.TexImage2D(targets[i], 0, InternalFormat.Rgba,
                            (uint)image.Width, (uint)image.Height, 0,
                            PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
                }
            }

            StbImage.stbi_set_flip_vertically_on_load(1);

            gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

            gl.BindTexture(TextureTarget.TextureCubeMap, 0);
        }

        public void Bind(uint slot = 0)
        {
            Engine.Graphics.Batch.OpenGL.ActiveTexture(TextureUnit.Texture0 + (int)slot);
            Engine.Graphics.Batch.OpenGL.BindTexture(TextureTarget.TextureCubeMap, Handle);
        }

        public void Dispose()
        {
            Engine.Graphics.Batch.OpenGL.DeleteTexture(Handle);
        }
    }
}