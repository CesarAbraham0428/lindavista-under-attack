using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>Android-only GPU compression. Original images, sprite slices and other platforms are preserved.</summary>
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
            metadataWithoutAndroid = RemoveAndroidSettings(File.ReadAllText(path + ".meta"));
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
            current = RemoveAndroidSettings(current);
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
                if (!IsConfigured(android) || android.maxTextureSize != snapshot.maximumSize)
                {
                    applied.Add(snapshot);
                    android.name = "Android";
                    android.overridden = true;
                    android.maxTextureSize = snapshot.maximumSize;
                    android.format = TextureImporterFormat.ETC2_RGBA8;
                    android.textureCompression = TextureImporterCompression.Compressed;
                    android.compressionQuality = 50;
                    android.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(android);
                    importer.SaveAndReimport();
                }
                LastStage = "Verifying imported Android settings";
                importer = (TextureImporter)AssetImporter.GetAtPath(snapshot.path);
                android = importer.GetPlatformTextureSettings("Android");
                if (!IsConfigured(android) || android.maxTextureSize != snapshot.maximumSize)
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
        int count = 0;
        foreach (string path in CandidatePaths())
        {
            LastAsset = path;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var android = importer.GetPlatformTextureSettings("Android");
            if (!IsConfigured(android))
                throw new InvalidOperationException("Expected Android ETC2 RGBA8 quality 50 without Crunch: " + path +
                    "; " + Describe(android));
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
        return "ANDROID_TEXTURES_OK: " + count +
            " non-readable PNGs use ETC2 RGBA8 on Android; resolution, sources and all non-Android metadata preserved by Configure. " +
            "Readable runtime atlases and Boss_Base.png excluded.";
    }

    private static IEnumerable<string> CandidatePaths()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                path.Equals(ProtectedPortrait, StringComparison.OrdinalIgnoreCase)) continue;
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.isReadable) continue;
            if (importer.GetDefaultPlatformTextureSettings().textureCompression == TextureImporterCompression.Uncompressed)
                yield return path;
        }
    }

    private static string HashFile(string path)
    {
        using var hash = SHA256.Create();
        using var source = File.OpenRead(path);
        return Convert.ToBase64String(hash.ComputeHash(source));
    }

    private static bool IsConfigured(TextureImporterPlatformSettings settings) => settings.overridden &&
        settings.format == TextureImporterFormat.ETC2_RGBA8 &&
        settings.textureCompression == TextureImporterCompression.Compressed &&
        !settings.crunchedCompression && settings.compressionQuality == 50;

    private static string Describe(TextureImporterPlatformSettings settings) => "override=" + settings.overridden +
        ", format=" + settings.format + ", compression=" + settings.textureCompression +
        ", quality=" + settings.compressionQuality + ", crunch=" + settings.crunchedCompression +
        ", max=" + settings.maxTextureSize;

    private static string FirstDifference(string expected, string current)
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

    private static string Normalize(string text)
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

    private static string RemoveAndroidSettings(string text)
    {
        var lines = new List<string>(Normalize(text).Split('\n'));
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] != "    buildTarget: Android") continue;
            int start = i - 1;
            if (start < 0 || !lines[start].StartsWith("  - serializedVersion:", StringComparison.Ordinal))
                throw new InvalidOperationException("Unrecognized Android texture metadata section.");
            int end = i + 1;
            while (end < lines.Count && (lines[end].StartsWith("    ", StringComparison.Ordinal) || lines[end].Length == 0)) end++;
            lines.RemoveRange(start, end - start);
            break;
        }
        return string.Join("\n", lines);
    }
}
