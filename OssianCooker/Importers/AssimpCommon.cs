using Silk.NET.Assimp;
using System;

namespace OssianForge.Cooker.Importers;

/// <summary>Import-time logic shared by the mesh and animation importers (moved out of MeshFile/AnimationFile).</summary>
internal static class AssimpCommon
{
    /// <summary>Assimp is not safe to call concurrently from several threads here; imports are serialized.</summary>
    public static readonly object Lock = new();

    private static readonly string[] PivotSuffixes =
    {
        "_$AssimpFbx$_Translation",
        "_$AssimpFbx$_PreRotation",
        "_$AssimpFbx$_Rotation",
        "_$AssimpFbx$_PostRotation",
        "_$AssimpFbx$_Scaling",
        "_$AssimpFbx$_GeometricTranslation",
        "_$AssimpFbx$_GeometricRotation",
        "_$AssimpFbx$_GeometricScaling",
    };

    public static string StripPivotSuffix(string name)
    {
        foreach (var suffix in PivotSuffixes)
            if (name.EndsWith(suffix, StringComparison.Ordinal))
                return name[..^suffix.Length];
        return name;
    }

    /// <summary>FBX "UnitScaleFactor" (cm per unit) converted to a scale to meters; 1 when the scene has no such metadata.</summary>
    public static unsafe float ReadUnitScaleFactor(Scene* scene)
    {
        float unitScale = 1f;
        var metadata = scene->MMetaData;
        if (metadata == null) return unitScale;

        for (uint k = 0; k < metadata->MNumProperties; k++)
        {
            string key = metadata->MKeys[k].AsString;
            if (key != "UnitScaleFactor") continue;

            var entry = metadata->MValues[k];
            double raw = entry.MType switch
            {
                MetadataType.Double => *(double*)entry.MData,
                MetadataType.Float => *(float*)entry.MData,
                MetadataType.Int32 => *(int*)entry.MData,
                MetadataType.Int64 => *(long*)entry.MData,
                _ => 1.0
            };
            unitScale = (float)(raw * 0.01);
        }
        return unitScale;
    }
}
