using OssianForge.Engine.Resources.Fonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OssianForge.Engine.Utils.MathUtils;

namespace OssianForge.Engine.Utils
{
    public static class FontUtils
    {
        public static (int width, int height) MeasureText(string content, float fontSize, string fontResourceId)
        {
            var fontResource = Engine.Resources.GetResource<FontResource>(fontResourceId)
                ?? throw new Exception($"FontResource not found: '{fontResourceId}'");

            return MeasureText(content, fontSize, fontResource);
        }

        public static (int width, int height) MeasureText(string content, float fontSize, FontResource font)
        {
            var atlas = font.AtlasData;
            float emToPx = fontSize;

            string[] lines = content.Split('\n');

            float maxWidth = 0f;
            foreach (var line in lines)
            {
                float cursor = 0f;
                foreach (char c in line)
                {
                    if (atlas.Glyphs.TryGetValue(c, out var g))
                        cursor += g.Advance * emToPx;
                    else
                        cursor += emToPx * 0.3f;
                }
                if (cursor > maxWidth) maxWidth = cursor;
            }

            float lineHeight = atlas.LineHeight * emToPx;
            int height = (int)MathF.Ceiling(lineHeight * lines.Length);
            int width = (int)MathF.Ceiling(maxWidth);

            return (width, height);
        }


        public static float GetAspect(string content, float fontSize, string fontResourceId)
        {
            var (width, height) = MeasureText(content, fontSize, fontResourceId);
            return (float)width / height;
        }

        public static float GetAspect(string content, float fontSize, FontResource font)
        {
            var (width, height) = MeasureText(content, fontSize, font);
            return (float)width / height;
        }


        public static Transform MakeTextTransform(string content, float fontSize, string fontResourceId, Vector3 position, float worldHeight = 1f)
        {
            float aspect = GetAspect(content, fontSize, fontResourceId);
            return new Transform(position, Vector3.Zero, new Vector3(aspect * worldHeight, worldHeight, 1f));
        }
    }
}
