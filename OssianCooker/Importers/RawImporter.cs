using OssianForge.Content;

namespace OssianForge.Cooker.Importers;

internal sealed class RawImporter : IImporter
{
    public AssetType Type => AssetType.Raw;
    public int Version => 1;
    public bool Cacheable => false;
    public byte[] Import(ImportContext ctx) => ctx.ReadSource();
}
