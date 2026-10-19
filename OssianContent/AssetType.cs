namespace OssianForge.Content;

/// <summary>What a pack entry contains. Stored as one byte in the pack TOC.</summary>
public enum AssetType : byte
{
    /// <summary>Verbatim file bytes (configs, xml, json...).</summary>
    Raw = 0,
    /// <summary>Cooked mesh blob, see <see cref="Meshes.MeshCodec"/>.</summary>
    Mesh = 1,
    /// <summary>Cooked animation blob, see <see cref="Animations.AnimationCodec"/>.</summary>
    Animation = 2,
    /// <summary>Cooked texture blob, see <see cref="Textures.TextureCodec"/>.</summary>
    Texture = 3,
    /// <summary>Cooked sound blob, see <see cref="Sounds.SoundCodec"/>.</summary>
    Sound = 4,
    /// <summary>UTF-8 GLSL text with includes resolved.</summary>
    Shader = 5,
    /// <summary>A compiled managed assembly (DLL bytes) built from one .cs script.</summary>
    Script = 6,
}
