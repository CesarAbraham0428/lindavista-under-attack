using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Reproducible Android configuration; other platform defaults are preserved.</summary>
public static class AndroidBuildTools
{
    public const string PipelinePath = "Assets/_Project/Settings/AndroidPipeline.asset";
    private const string QualityName = "Android";
    public static readonly string[] Scenes = {
        "Assets/_Project/Scenes/MainMenu.unity",
        "Assets/_Project/Scenes/CharacterSelection.unity",
        "Assets/_Project/Scenes/Gameplay.unity"
    };

    [MenuItem("Tools/Lindavista/Android/Configurar Android")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Stop Play Mode and builds before configuring Android.");
        var source = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Project/Settings/UniversalRP.asset");
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            pipeline = UnityEngine.Object.Instantiate(source);
            pipeline.name = "AndroidPipeline";
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
        }
        pipeline.supportsHDR = false;
        pipeline.msaaSampleCount = 1;
        pipeline.supportsCameraDepthTexture = false;
        pipeline.supportsCameraOpaqueTexture = false;
        var pipelineData = new SerializedObject(pipeline);
        pipelineData.FindProperty("m_MainLightShadowsSupported").boolValue = false;
        pipelineData.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
        pipelineData.FindProperty("m_SoftShadowsSupported").boolValue = false;
        pipelineData.ApplyModifiedPropertiesWithoutUndo();
        pipeline.useSRPBatcher = true;
        pipeline.renderScale = 1f; // Runtime caps world pixels for each display; UI stays native.
        EditorUtility.SetDirty(pipeline);

        var quality = new SerializedObject(QualitySettings.GetQualitySettings());
        var levels = quality.FindProperty("m_QualitySettings");
        int index = Array.IndexOf(QualitySettings.names, QualityName);
        if (index < 0)
        {
            index = levels.arraySize;
            levels.InsertArrayElementAtIndex(index);
        }
        var mobile = levels.GetArrayElementAtIndex(index);
        mobile.FindPropertyRelative("name").stringValue = QualityName;
        mobile.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
        mobile.FindPropertyRelative("vSyncCount").intValue = 0;
        mobile.FindPropertyRelative("antiAliasing").intValue = 0;
        mobile.FindPropertyRelative("shadows").intValue = 0;
        mobile.FindPropertyRelative("softParticles").boolValue = false;
        mobile.FindPropertyRelative("realtimeReflectionProbes").boolValue = false;
        mobile.FindPropertyRelative("globalTextureMipmapLimit").intValue = 0;
        mobile.FindPropertyRelative("anisotropicTextures").intValue = 0;
        mobile.FindPropertyRelative("pixelLightCount").intValue = 0;
        mobile.FindPropertyRelative("skinWeights").intValue = 2;
        mobile.FindPropertyRelative("softVegetation").boolValue = false;
        mobile.FindPropertyRelative("realtimeGICPUUsage").intValue = 25;
        mobile.FindPropertyRelative("lodBias").floatValue = 1f;
        mobile.FindPropertyRelative("particleRaycastBudget").intValue = 16;
        var defaults = quality.FindProperty("m_PerPlatformDefaultQuality");
        var excluded = mobile.FindPropertyRelative("excludedTargetPlatforms");
        excluded.ClearArray();
        for (int i = 0; i < defaults.arraySize; i++)
        {
            var entry = defaults.GetArrayElementAtIndex(i);
            string platform = entry.FindPropertyRelative("first").stringValue;
            if (platform == "Android") entry.FindPropertyRelative("second").intValue = index;
            else Append(excluded, platform);
        }
        for (int i = 0; i < levels.arraySize; i++)
        {
            if (i == index) continue;
            var platforms = levels.GetArrayElementAtIndex(i).FindPropertyRelative("excludedTargetPlatforms");
            bool contains = false;
            for (int j = 0; j < platforms.arraySize; j++) contains |= platforms.GetArrayElementAtIndex(j).stringValue == "Android";
            if (!contains) Append(platforms, "Android");
        }
        quality.ApplyModifiedPropertiesWithoutUndo();

        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        PlayerSettings.Android.optimizedFramePacing = true;
        var currentApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) || currentApis.Length != 1 ||
            currentApis[0] != GraphicsDeviceType.OpenGLES3)
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        }
        if (EditorUserBuildSettings.androidBuildSubtarget != MobileTextureSubtarget.ETC2)
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ETC2;
        foreach (string path in Scenes)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Missing game scene", path);
        }
        AssetDatabase.SaveAssets();
        Debug.Log(Verify());
    }

    private static void Append(SerializedProperty array, string value)
    {
        int index = array.arraySize;
        array.InsertArrayElementAtIndex(index);
        array.GetArrayElementAtIndex(index).stringValue = value;
    }

    public static string Verify()
    {
        int checks = 0;
        void Check(bool condition, string label) { checks++; if (!condition) throw new Exception("Android configuration: " + label); }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        Check(pipeline != null && !pipeline.supportsHDR && pipeline.msaaSampleCount == 1, "bounded color buffers");
        Check(!pipeline.supportsCameraDepthTexture && !pipeline.supportsCameraOpaqueTexture, "no unused camera copies");
        var active = QualitySettings.GetActiveQualityLevelsForPlatform("Android");
        Check(active.Length == 1 && QualitySettings.GetRenderPipelineAssetAt(active[0]) == pipeline, "Android-only pipeline in build");
        Check(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP, "IL2CPP");
        Check((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0 &&
            (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARMv7) != 0, "32/64-bit ARM");
        Check(PlayerSettings.Android.optimizedFramePacing, "frame pacing");
        var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        Check(!PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) && apis.Length == 1 &&
            apis[0] == GraphicsDeviceType.OpenGLES3, "OpenGLES3 compatibility");
        Check(EditorUserBuildSettings.androidBuildSubtarget == MobileTextureSubtarget.ETC2, "ETC2 texture target");
        Check(ConfiguracionMovil.CalcularCadencia(90, 30) == 30 && ConfiguracionMovil.CalcularCadencia(120, 30) == 30 &&
            ConfiguracionMovil.CalcularCadencia(double.NaN) == 60 &&
            ConfiguracionMovil.CalcularCadencia(85) == 43 && ConfiguracionMovil.CalcularCadencia(90.1) == 46 &&
            ConfiguracionMovil.CalcularCadencia(59.94) == 60, "refresh divisors, fractions and fallback");
        Check(Mathf.Approximately(RendimientoAndroid.CalcularEscala(2400, 1080, false), 900f / 1080f) &&
            Mathf.Approximately(RendimientoAndroid.CalcularEscala(2400, 1080, true), 720f / 1080f) &&
            RendimientoAndroid.CalcularEscala(1280, 720, false) == 1f, "world resolution cap without upscaling");
        foreach (string scene in Scenes) Check(File.Exists(scene), "scene exists: " + scene);
        return "ANDROID_CONFIGURATION_OK: " + checks + " assertions; ARMv7/ARM64, GLES3/ETC2, native overlay UI and 30/60 targets.";
    }
}
