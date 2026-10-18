using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Android-only GPU compression. Original images, sprite slices and other platforms are preserved.
/// ETC2 can only encode textures whose imported width and height are multiples of 4; Unity silently
/// keeps any other size as uncompressed RGBA32. Those textures use ASTC 4x4 instead: the same 8 bits
/// per pixel as ETC2 RGBA8, without the block-size restriction. Devices without ASTC decompress at load.
/// </summary>
public static class AndroidTextureSetup
{
    private const string Root = "Assets/_Project";
    private const string ProtectedPortrait = Root + "/Art/Characters/Enemies/Boss_Base.png";
    private static List<Snapshot> lastConfiguration;
    private static Snapshot portraitBeforeConfiguration;
    public static string LastError { get; private set; } = "";
    public static string LastAsset { get; private set; } = "";
    public static string LastStage { get; private set; } = "Not started";
    public static int ProcessedCount { get; private set; }

    private sealed class Snapshot
    {
        internal string path, sourceHash, metadataWithoutAndroid;
        internal byte[] metadata;
        internal int maximumSize;

        internal Snapshot(string path, TextureImporter importer)
        {
            this.path = path;
            sourceHash = HashFile(path);
            metadata = File.ReadAllBytes(path + ".meta");
            metadataWithoutAndroid = RemovePlatformSettings(File.ReadAllText(path + ".meta"), "Android");
            var android = importer.GetPlatformTextureSettings("Android");
            maximumSize = android.overridden ? android.maxTextureSize : importer.maxTextureSize;
        }

        internal void VerifyPreserved(bool includeAndroid = false)
        {
            if (HashFile(path) != sourceHash)
                throw new InvalidOperationException("Original image changed: " + path);
            if (includeAndroid)
            {
                if (!SameBytes(metadata, File.ReadAllBytes(path + ".meta")))
                    throw new InvalidOperationException("Protected portrait metadata bytes changed: " + path);
                return;
            }
            string current = File.ReadAllText(path + ".meta");
            string expected = metadataWithoutAndroid;
            current = RemovePlatformSettings(current, "Android");
            if (Normalize(current) != Normalize(expected))
                throw new InvalidOperationException("Metadata outside Android changed (GUID, slices, defaults or other platforms): " +
                    path + "; " + FirstDifference(expected, current));
        }
    }

    [MenuItem("Tools/Lindavista/Configure Android texture compression")]
    private static void ConfigureMenu() => Debug.Log(Configure());

    [MenuItem("Tools/Lindavista/Verify Android texture compression")]
    private static void VerifyMenu() => Debug.Log(Verify());

    public static string Configure()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Configure textures outside Play Mode.");
        LastError = "";
        LastAsset = "";
        LastStage = "Collecting source snapshots";
        ProcessedCount = 0;
        var changes = new List<Snapshot>();
        var applied = new List<Snapshot>();
        try
        {
            foreach (string path in CandidatePaths())
            {
                LastAsset = path;
                changes.Add(new Snapshot(path, (TextureImporter)AssetImporter.GetAtPath(path)));
            }
            LastAsset = ProtectedPortrait;
            var portrait = new Snapshot(ProtectedPortrait, (TextureImporter)AssetImporter.GetAtPath(ProtectedPortrait));
            // SaveAndReimport must complete before inspecting the freshly loaded
            // importer. Batching with StartAssetEditing defers those imports.
            foreach (Snapshot snapshot in changes)
            {
                LastAsset = snapshot.path;
                LastStage = "Applying Android settings";
                var importer = (TextureImporter)AssetImporter.GetAtPath(snapshot.path);
                var android = importer.GetPlatformTextureSettings("Android");
                TextureImporterFormat format = FormatFor(snapshot.path);
                if (!IsConfigured(android, format) || android.maxTextureSize != snapshot.maximumSize)
                {
                    applied.Add(snapshot);
                    android.name = "Android";
                    android.overridden = true;
                    android.maxTextureSize = snapshot.maximumSize;
                    android.format = format;
                    android.textureCompression = TextureImporterCompression.Compressed;
                    android.compressionQuality = 50;
                    android.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(android);
                    importer.SaveAndReimport();
                }
                LastStage = "Verifying imported Android settings";
                importer = (TextureImporter)AssetImporter.GetAtPath(snapshot.path);
                android = importer.GetPlatformTextureSettings("Android");
                if (!IsConfigured(android, format) || android.maxTextureSize != snapshot.maximumSize)
                    throw new InvalidOperationException("Android settings did not persist: " + snapshot.path +
                        "; " + Describe(android));
                LastStage = "Verifying source and non-Android metadata";
                snapshot.VerifyPreserved();
                ProcessedCount++;
            }

            // Compare all non-Android YAML, not just selected importer fields.
            // This protects GUIDs, slices/local IDs, filtering, default and iOS settings.
            LastAsset = ProtectedPortrait;
            LastStage = "Verifying protected boss portrait";
            portrait.VerifyPreserved(true);
            lastConfiguration = changes;
            portraitBeforeConfiguration = portrait;
            LastStage = "Final verification";
            string result = Verify();
            LastStage = "Complete";
            LastAsset = "";
            return result;
        }
        catch (Exception exception)
        {
            // Persist the original failure before rollback or an external tool
            // timeout can hide the exception returned by the configure call.
            LastError = "Stage: " + LastStage + "; asset: " + LastAsset + "; completed: " + ProcessedCount +
                "; " + exception;
            Debug.LogError("ANDROID_TEXTURES_FAILED: " + LastError);
            // A failed preservation check must not leave a partially changed import.
            try
            {
                foreach (Snapshot snapshot in applied)
                {
                    File.WriteAllBytes(snapshot.path + ".meta", snapshot.metadata);
                    AssetDatabase.ImportAsset(snapshot.path,
                        ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
            }
            catch (Exception rollbackException)
            {
                LastError += "\nRollback also failed: " + rollbackException;
                Debug.LogError("ANDROID_TEXTURES_ROLLBACK_FAILED: " + LastError);
                throw new AggregateException("Android texture configuration and rollback both failed.", exception, rollbackException);
            }
            throw;
        }
    }

    public static string Verify()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Verify textures outside Play Mode.");
        int count = 0, etc2 = 0, astc = 0;
        bool androidActive = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
        foreach (string path in CandidatePaths())
        {
            LastAsset = path;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var android = importer.GetPlatformTextureSettings("Android");
            TextureImporterFormat format = FormatFor(path);
            if (!IsConfigured(android, format))
                throw new InvalidOperationException("Expected Android " + format + " quality 50 without Crunch: " + path +
                    "; " + Describe(android));
            // The override alone is not proof: check what the importer actually produced.
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            TextureFormat expected = format == TextureImporterFormat.ASTC_4x4 ? TextureFormat.ASTC_4x4 : TextureFormat.ETC2_RGBA8;
            if (androidActive && texture.format != expected)
                throw new InvalidOperationException("Imported Android texture is " + texture.format + ", expected " + expected + ": " + path);
            if (format == TextureImporterFormat.ASTC_4x4) astc++; else etc2++;
            count++;
        }
        if (count == 0) throw new InvalidOperationException("No uncompressed non-readable textures found to verify.");
        if (lastConfiguration != null)
        {
            foreach (Snapshot snapshot in lastConfiguration)
            {
                LastAsset = snapshot.path;
                snapshot.VerifyPreserved();
                var importer = (TextureImporter)AssetImporter.GetAtPath(snapshot.path);
                if (importer.GetPlatformTextureSettings("Android").maxTextureSize != snapshot.maximumSize)
                    throw new InvalidOperationException("Android texture resolution changed: " + snapshot.path);
            }
            LastAsset = ProtectedPortrait;
            portraitBeforeConfiguration.VerifyPreserved(true);
        }
        return "ANDROID_TEXTURES_OK: " + count + " non-readable PNGs compressed on Android (" + etc2 + " ETC2 RGBA8, " +
            astc + " ASTC 4x4 for sizes that are not multiples of 4)" +
            (androidActive ? "; imported formats confirmed" : "; imported formats not checked (Android is not the active target)") +
            ". Resolution, sources and all non-Android metadata preserved by Configure. Boss_Base.png excluded.";
    }

    /// <summary>ETC2 needs 4x4 blocks; other imported sizes would silently stay uncompressed.</summary>
    public static TextureImporterFormat FormatFor(string path)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return texture.width % 4 == 0 && texture.height % 4 == 0
            ? TextureImporterFormat.ETC2_RGBA8 : TextureImporterFormat.ASTC_4x4;
    }

    private static IEnumerable<string> CandidatePaths()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                path.Equals(ProtectedPortrait, StringComparison.OrdinalIgnoreCase)) continue;
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.isReadable) continue;
            // Explicitly uncompressed imports, plus "automatic" ones whose ETC2 attempt falls back to RGBA32.
            if (importer.GetDefaultPlatformTextureSettings().textureCompression == TextureImporterCompression.Uncompressed ||
                FormatFor(path) == TextureImporterFormat.ASTC_4x4)
                yield return path;
        }
    }

    internal static string HashFile(string path)
    {
        using var hash = SHA256.Create();
        using var source = File.OpenRead(path);
        return Convert.ToBase64String(hash.ComputeHash(source));
    }

    private static bool IsConfigured(TextureImporterPlatformSettings settings, TextureImporterFormat format) => settings.overridden &&
        settings.format == format &&
        settings.textureCompression == TextureImporterCompression.Compressed &&
        !settings.crunchedCompression && settings.compressionQuality == 50;

    private static string Describe(TextureImporterPlatformSettings settings) => "override=" + settings.overridden +
        ", format=" + settings.format + ", compression=" + settings.textureCompression +
        ", quality=" + settings.compressionQuality + ", crunch=" + settings.crunchedCompression +
        ", max=" + settings.maxTextureSize;

    internal static string FirstDifference(string expected, string current)
    {
        string[] before = Normalize(expected).Split('\n');
        string[] after = Normalize(current).Split('\n');
        int length = Math.Max(before.Length, after.Length);
        for (int i = 0; i < length; i++)
        {
            string previousLine = i < before.Length ? before[i] : "<end>";
            string currentLine = i < after.Length ? after[i] : "<end>";
            if (previousLine != currentLine)
                return "first difference at line " + (i + 1) + ": '" + previousLine + "' -> '" + currentLine + "'";
        }
        return "no textual difference";
    }

    internal static string Normalize(string text)
    {
        // Unity may serialize empty values as "key:" or "key: ". Ignore
        // trailing whitespace/newlines while preserving indentation and values.
        string[] lines = text.Replace("\r\n", "\n").TrimStart('\uFEFF').Split('\n');
        for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].TrimEnd(' ', '\t', '\r');
        return string.Join("\n", lines).TrimEnd('\n');
    }

    private static bool SameBytes(byte[] expected, byte[] current)
    {
        if (expected.Length != current.Length) return false;
        for (int i = 0; i < expected.Length; i++)
            if (expected[i] != current[i]) return false;
        return true;
    }

    internal static string RemovePlatformSettings(string text, string platform)
    {
        var lines = new List<string>(Normalize(text).Split('\n'));
        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].StartsWith("    buildTarget: ", StringComparison.Ordinal)) continue;
            int start = i - 1;
            if (start < 0 || !lines[start].StartsWith("  - serializedVersion:", StringComparison.Ordinal))
                throw new InvalidOperationException("Unrecognized texture platform metadata section.");
            int end = i + 1;
            while (end < lines.Count && (lines[end].StartsWith("    ", StringComparison.Ordinal) || lines[end].Length == 0)) end++;
            string target = lines[i].Substring("    buildTarget: ".Length);
            // Saving a first Android override also serializes default blocks for other platforms
            // (for example WebGL). A block with "overridden: 0" carries no settings, so it is ignored.
            bool inert = target != "DefaultTexturePlatform" && lines.GetRange(start, end - start).Contains("    overridden: 0");
            if (target != platform && !inert) continue;
            lines.RemoveRange(start, end - start);
            i = start - 1;
        }
        return string.Join("\n", lines);
    }
}
