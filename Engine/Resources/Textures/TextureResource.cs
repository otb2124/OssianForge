using OssianForge.Engine.Resources.TextureFiles;
using OssianForge.Engine.Resources;
using OssianForge.Engine;

public class TextureResource : Resource
{
    public List<TextureFile> TextureFiles = new();
    public List<string> TextureIds;

    public TextureResource(string id, params string[] textureIds)
    {
        Id = id;
        TextureIds = textureIds.ToList();
    }

    public override void Load()
    {
        TextureFiles.Clear();
        foreach (var textureId in TextureIds)
        {
            var file = Engine.Resources.GetResourceFile<TextureFile>(textureId)
                ?? throw new Exception($"Texture not found: '{textureId}'");
            TextureFiles.Add(file);
        }
    }
}