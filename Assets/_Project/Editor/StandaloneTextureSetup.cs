using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Windows/macOS/Linux GPU compression for the PNGs imported as uncompressed RGBA32. BC7 keeps
/// 8 bits per pixel (a quarter of RGBA32) with the best quality of the BCn formats. Like ETC2, BCn
/// only encodes textures whose width and height are multiples of 4, and desktop GPUs have no ASTC,
/// so other sizes stay RGBA32 and are left untouched. Only the Standalone block of each .meta changes.
/// </summary>
public static class StandaloneTextureSetup
{
    private const string Root = "Assets/_Project";
    private const string ProtectedPortrait = Root + "/Art/Characters/Enemies/Boss_Base.png";
    private const string Platform = "Standalone";

    private sealed class Snapshot
    {
        internal readonly string path, sourceHash, metadataWithoutStandalone;
        internal readonly byte[] metadata;
        internal readonly int maximumSize;

        internal Snapshot(string path, TextureImporter importer)
        {
            this.path = path;
            sourceHash = AndroidTextureSetup.HashFile(path);
            metadata = File.ReadAllBytes(path + ".meta");
            metadataWithoutStandalone = AndroidTextureSetup.RemovePlatformSettings(File.ReadAllText(path + ".meta"), Platform);
            maximumSize = importer.maxTextureSize;
        }

        internal void VerifyPreserved()
        {
            if (AndroidTextureSetup.HashFile(path) != sourceHash)
                throw new InvalidOperationException("Original image changed: " + path);
            string current = AndroidTextureSetup.RemovePlatformSettings(File.ReadAllText(path + ".meta"), Platform);
            if (AndroidTextureSetup.Normalize(current) != AndroidTextureSetup.Normalize(metadataWithoutStandalone))
                throw new InvalidOperationException("Metadata outside Standalone changed: " + path + "; " +
                    AndroidTextureSetup.FirstDifference(metadataWithoutStandalone, current));
        }
    }

    [MenuItem("Tools/Lindavista/Configure desktop texture compression")]
    private static void ConfigureMenu() => Debug.Log(Configure());

    [MenuItem("Tools/Lindavista/Verify desktop texture compression")]
    private static void VerifyMenu() => Debug.Log(Verify());

    public static string Configure()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Configure textures outside Play Mode.");
        var applied = new List<Snapshot>();
        try
        {
            foreach (string path in CandidatePaths())
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = importer.GetPlatformTextureSettings(Platform);
                if (IsConfigured(settings, importer.maxTextureSize)) continue;
                var snapshot = new Snapshot(path, importer);
                applied.Add(snapshot);
                settings.name = Platform;
                settings.overridden = true;
                settings.maxTextureSize = snapshot.maximumSize;
                settings.format = TextureImporterFormat.BC7;
                settings.textureCompression = TextureImporterCompression.Compressed;
                settings.compressionQuality = 50;
                settings.crunchedCompression = false;
                importer.SetPlatformTextureSettings(settings);
                importer.SaveAndReimport();
                importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (!IsConfigured(importer.GetPlatformTextureSettings(Platform), snapshot.maximumSize))
                    throw new InvalidOperationException("Standalone settings did not persist: " + path);
                snapshot.VerifyPreserved();
            }
            return Verify() + " Changed now: " + applied.Count + ".";
        }
        catch (Exception exception)
        {
            Debug.LogError("DESKTOP_TEXTURES_FAILED: " + exception);
            // A failed preservation check must not leave a partially changed import.
            foreach (Snapshot snapshot in applied)
            {
                File.WriteAllBytes(snapshot.path + ".meta", snapshot.metadata);
                AssetDatabase.ImportAsset(snapshot.path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            throw;
        }
    }

    public static string Verify()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Verify textures outside Play Mode.");
        bool desktopActive = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget) == BuildTargetGroup.Standalone;
        int count = 0, uncompressed = 0;
        foreach (string path in CandidatePaths())
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!IsConfigured(importer.GetPlatformTextureSettings(Platform), importer.maxTextureSize))
                throw new InvalidOperationException("Expected Standalone BC7 quality 50 at the default size: " + path);
            // The override alone is not proof: check what the importer produced when desktop is active.
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (desktopActive && texture.format != TextureFormat.BC7)
                throw new InvalidOperationException("Imported desktop texture is " + texture.format + ": " + path);
            count++;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
            if (texture != null && (texture.width % 4 != 0 || texture.height % 4 != 0)) uncompressed++;
        }
        if (count == 0) throw new InvalidOperationException("No desktop textures to verify.");
        return "DESKTOP_TEXTURES_OK: " + count + " PNGs use BC7 on Standalone" +
            (desktopActive ? " (imported formats confirmed)" : " (imported formats not checked: Standalone is not active)") +
            "; " + uncompressed + " textures with sizes that are not multiples of 4 stay RGBA32 on desktop.";
    }

    private static IEnumerable<string> CandidatePaths()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                path.Equals(ProtectedPortrait, StringComparison.OrdinalIgnoreCase)) continue;
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.isReadable) continue;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture.width % 4 != 0 || texture.height % 4 != 0) continue;
            var settings = importer.GetPlatformTextureSettings(Platform);
            if (settings.overridden ? settings.format == TextureImporterFormat.BC7
                : importer.GetDefaultPlatformTextureSettings().textureCompression == TextureImporterCompression.Uncompressed)
                yield return path;
        }
    }

    private static bool IsConfigured(TextureImporterPlatformSettings settings, int maximumSize) => settings.overridden &&
        settings.format == TextureImporterFormat.BC7 && settings.textureCompression == TextureImporterCompression.Compressed &&
        !settings.crunchedCompression && settings.compressionQuality == 50 && settings.maxTextureSize == maximumSize;
}
