using OssianForge.Engine.Resources.Config;
using OssianForge.Engine.Resources.TextureFiles;
using Silk.NET.OpenGL;
using System;

namespace OssianForge.Engine.Resources.Fonts
{

    public class GlyphData
    {
        public float AtlasX;          // atlasBounds.left   (image pixels)
        public float AtlasY;          // atlasBounds.bottom (image pixels, Y=0 at top)
        public float AtlasW;
        public float AtlasH;
        public float PlaneBoundsLeft;
        public float PlaneBoundsBottom;
        public float PlaneBoundsRight;
        public float PlaneBoundsTop;
        public float Advance;
    }

    public class FontAtlasData
    {
        public int AtlasWidth;
        public int AtlasHeight;
        public float LineHeight;
        public float DistanceRange;
        public Dictionary<char, GlyphData> Glyphs = new();
    }

    public class FontResource : Resource
    {
        public TextureFile AtlasTextureFile;
        public ConfigFile AtlasConfigFile;
        private readonly string _atlasTextureFileId;
        private readonly string _atlasConfigFileId;

        public FontAtlasData AtlasData { get; private set; }

        public FontResource(string id, string atlasTextureId, string atlasConfigId)
        {
            Id = id;
            _atlasTextureFileId = atlasTextureId;
            _atlasConfigFileId = atlasConfigId;
        }

        public override void Load()
        {
            base.Load();
            AtlasTextureFile = Engine.Resources.GetResource<TextureFile>(_atlasTextureFileId)
                ?? throw new Exception($"Atlas texture not found: '{_atlasTextureFileId}'");
            AtlasConfigFile = Engine.Resources.GetResource<ConfigFile>(_atlasConfigFileId)
                ?? throw new Exception($"Atlas config not found: '{_atlasConfigFileId}'");

            AtlasData = ParseAtlasData(AtlasConfigFile);
        }

        public void Bind(uint slot = 0)
        {
            // delegate to TextureFile.Bind — Handle lives there
            AtlasTextureFile.Bind(slot);
        }

        private static FontAtlasData ParseAtlasData(ConfigFile config)
        {
            var data = new FontAtlasData
            {
                AtlasWidth = config.GetInt("atlas.width"),
                AtlasHeight = config.GetInt("atlas.height"),
                LineHeight = config.GetFloat("metrics.lineHeight"),
                DistanceRange = config.GetFloat("atlas.distanceRange"),
            };

            // glyphs are under keys like glyphs[0].unicode, glyphs[0].advance etc
            int i = 0;
            while (config.HasKey($"glyphs[{i}].unicode"))
            {
                int unicode = config.GetInt($"glyphs[{i}].unicode");
                char c = (char)unicode;

                // in ParseAtlasData:
                data.Glyphs[c] = new GlyphData
                {
                    AtlasX = config.GetFloat($"glyphs[{i}].atlasBounds.left"),
                    AtlasY = config.GetFloat($"glyphs[{i}].atlasBounds.bottom"),  // back to .bottom
                    AtlasW = config.GetFloat($"glyphs[{i}].atlasBounds.right")
                           - config.GetFloat($"glyphs[{i}].atlasBounds.left"),
                    AtlasH = config.GetFloat($"glyphs[{i}].atlasBounds.top")
                           - config.GetFloat($"glyphs[{i}].atlasBounds.bottom"),
                    PlaneBoundsLeft = config.GetFloat($"glyphs[{i}].planeBounds.left"),
                    PlaneBoundsBottom = config.GetFloat($"glyphs[{i}].planeBounds.bottom"),
                    PlaneBoundsRight = config.GetFloat($"glyphs[{i}].planeBounds.right"),
                    PlaneBoundsTop = config.GetFloat($"glyphs[{i}].planeBounds.top"),
                    Advance = config.GetFloat($"glyphs[{i}].advance"),
                };
                i++;
            }

            Console.WriteLine($"[FONT] Parsed {data.Glyphs.Count} glyphs, lineHeight={data.LineHeight}");
            return data;
        }
    }
}