using OssianForge.Content;

namespace OssianForge.Tools.Cook.Importers;

internal sealed class ImportContext
{
    public required string FullPath { get; init; }
    public required string RelPath { get; init; }
    public required ImportSettings Settings { get; init; }
    public required CookOptions Options { get; init; }

    public byte[] ReadSource() => File.ReadAllBytes(FullPath);
}

internal interface IImporter
{
    AssetType Type { get; }

    /// <summary>Bump when the importer's output changes, so cached blobs are invalidated.</summary>
    int Version { get; }

    /// <summary>False for importers that just copy bytes.</summary>
    bool Cacheable => true;

    /// <summary>Extra text mixed into the cache key (e.g. included shader files, engine assembly identity).</summary>
    string Fingerprint(ImportContext ctx) => "";

    byte[] Import(ImportContext ctx);
}
