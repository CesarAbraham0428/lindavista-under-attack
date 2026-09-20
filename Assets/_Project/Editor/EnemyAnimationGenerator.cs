using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Creates the enemy animation clips and controllers from the sliced sprite sheets.
/// Existing assets are left untouched so hand-tuned values are preserved.
/// </summary>
public static class EnemyAnimationGenerator
{
    private const string ArtRoot = "Assets/_Project/Art/Characters/Enemies";
    private const string AnimationRoot = "Assets/_Project/Animations/Enemies";

    private sealed class EnemySpec
    {
        public string Id;
        public string BaseTexture;
        public float BasePixelsPerUnit;
        public string ActionTexture;
        public string DefeatTexture;
        public string ActionName;
        public float ActionFps;
        public bool UseActionFirstFrameAsIdle;
    }

    private static readonly EnemySpec[] Specs =
    {
        new EnemySpec
        {
            Id = "Enemy2",
            BaseTexture = "Enemy2_Base.png",
            BasePixelsPerUnit = 283f,
            ActionTexture = "Enemy2_Pistol_Fire_8f.png",
            DefeatTexture = "Enemy2_Defeat_8f.png",
            ActionName = "PistolFire",
            ActionFps = 10f
        },
        new EnemySpec
        {
            Id = "Enemy3",
            BaseTexture = "Enemy3_Base.png",
            BasePixelsPerUnit = 277f,
            ActionTexture = "Enemy3_Shield_HeavyPunch_8f.png",
            DefeatTexture = "Enemy3_Defeat_8f.png",
            ActionName = "HeavyPunch",
            ActionFps = 8f
        },
        new EnemySpec
        {
            Id = "Enemy4",
            BaseTexture = "Enemy4_Base.png",
            BasePixelsPerUnit = 286f,
            ActionTexture = "Enemy4_SMG_Fire_8f.png",
            DefeatTexture = "Enemy4_Defeat_8f.png",
            ActionName = "SMGFire",
            ActionFps = 12f
        },
        new EnemySpec
        {
            Id = "BossTruck",
            ActionTexture = "Boss_Truck_Fire_8f.png",
            DefeatTexture = "Boss_Truck_Defeat_8f.png",
            ActionName = "Fire",
            ActionFps = 8f,
            UseActionFirstFrameAsIdle = true
        }
    };

    [InitializeOnLoadMethod]
    private static void GenerateMissingAssetsAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (Specs.Any(spec =>
                    AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath(spec)) == null))
            {
                GenerateAll();
            }
        };
    }

    [MenuItem("Tools/Lindavista/Generate Missing Enemy Animations")]
    public static void GenerateAll()
    {
        EnsureFolder("Assets/_Project/Animations", "Enemies");

        foreach (EnemySpec spec in Specs)
        {
            GenerateEnemy(spec);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Enemy animations are ready: Enemy2, Enemy3, Enemy4 and BossTruck.");
    }

    private static void GenerateEnemy(EnemySpec spec)
    {
        EnsureFolder(AnimationRoot, spec.Id);

        Sprite[] actionFrames = LoadFrames(spec.ActionTexture);
        Sprite[] defeatFrames = LoadFrames(spec.DefeatTexture);

        if (actionFrames.Length < 8 || defeatFrames.Length < 8)
        {
            Debug.LogError($"{spec.Id}: expected 8 action and defeat frames.");
            return;
        }

        Sprite idleSprite;
        if (spec.UseActionFirstFrameAsIdle)
        {
            idleSprite = actionFrames[0];
        }
        else
        {
            SetPixelsPerUnit(spec.BaseTexture, spec.BasePixelsPerUnit);
            idleSprite = LoadFrames(spec.BaseTexture).FirstOrDefault();
        }

        if (idleSprite == null)
        {
            Debug.LogError($"{spec.Id}: idle sprite could not be loaded.");
            return;
        }

        string folder = $"{AnimationRoot}/{spec.Id}";
        AnimationClip idle = CreateClip(
            $"{folder}/{spec.Id}_Idle.anim", new[] { idleSprite }, 10f, true);
        AnimationClip walk = CreateClip(
            $"{folder}/{spec.Id}_Walk.anim", new[] { idleSprite }, 10f, true);
        AnimationClip action = CreateClip(
            $"{folder}/{spec.Id}_{spec.ActionName}.anim", actionFrames.Take(8).ToArray(),
            spec.ActionFps, false);
        AnimationClip damage = CreateClip(
            $"{folder}/{spec.Id}_Damage.anim", new[] { defeatFrames[0], idleSprite }, 5f, false);
        AnimationClip defeat = CreateClip(
            $"{folder}/{spec.Id}_Defeat.anim", defeatFrames.Take(8).ToArray(), 8f, false);

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath(spec)) == null)
        {
            CreateController(spec, idle, walk, action, damage, defeat);
        }
    }

    private static AnimationClip CreateClip(
        string path, IReadOnlyList<Sprite> sprites, float framesPerSecond, bool loop)
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
        {
            return existing;
        }

        var clip = new AnimationClip
        {
            name = System.IO.Path.GetFileNameWithoutExtension(path),
            frameRate = framesPerSecond
        };

        var keys = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / framesPerSecond,
                value = sprites[i]
            };
        }

        var binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AssetDatabase.CreateAsset(clip, path);
        var serializedClip = new SerializedObject(clip);
        SerializedProperty loopProperty =
            serializedClip.FindProperty("m_AnimationClipSettings.m_LoopTime");
        if (loopProperty != null)
        {
            loopProperty.boolValue = loop;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void CreateController(
        EnemySpec spec,
        AnimationClip idleClip,
        AnimationClip walkClip,
        AnimationClip actionClip,
        AnimationClip damageClip,
        AnimationClip defeatClip)
    {
        AnimatorController controller =
            AnimatorController.CreateAnimatorControllerAtPath(ControllerPath(spec));
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("TakeDamage", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Defeat", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = machine.AddState($"{spec.Id}_Idle", new Vector3(350, 100));
        AnimatorState walk = machine.AddState($"{spec.Id}_Walk", new Vector3(600, 100));
        AnimatorState action = machine.AddState(
            $"{spec.Id}_{spec.ActionName}", new Vector3(350, 260));
        AnimatorState damage = machine.AddState($"{spec.Id}_Damage", new Vector3(600, 260));
        AnimatorState defeat = machine.AddState($"{spec.Id}_Defeat", new Vector3(475, 420));

        idle.motion = idleClip;
        walk.motion = walkClip;
        action.motion = actionClip;
        damage.motion = damageClip;
        defeat.motion = defeatClip;
        machine.defaultState = idle;

        ConfigureImmediateTransition(idle.AddTransition(walk), "IsMoving", true);
        ConfigureImmediateTransition(walk.AddTransition(idle), "IsMoving", false);

        ConfigureTriggerTransition(machine.AddAnyStateTransition(defeat), "Defeat");
        ConfigureTriggerTransition(machine.AddAnyStateTransition(damage), "TakeDamage");
        ConfigureTriggerTransition(machine.AddAnyStateTransition(action), "Attack");
        ConfigureReturnTransition(action.AddTransition(idle));
        ConfigureReturnTransition(damage.AddTransition(idle));

        EditorUtility.SetDirty(controller);
    }

    private static void ConfigureImmediateTransition(
        AnimatorStateTransition transition, string parameter, bool expectedValue)
    {
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(
            expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0f,
            parameter);
    }

    private static void ConfigureTriggerTransition(
        AnimatorStateTransition transition, string trigger)
    {
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static void ConfigureReturnTransition(AnimatorStateTransition transition)
    {
        transition.hasExitTime = true;
        transition.exitTime = 0.95f;
        transition.duration = 0.05f;
        transition.canTransitionToSelf = false;
    }

    private static Sprite[] LoadFrames(string fileName)
    {
        string path = $"{ArtRoot}/{fileName}";
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(sprite => FrameIndex(sprite.name))
            .ToArray();
    }

    private static int FrameIndex(string spriteName)
    {
        int separator = spriteName.LastIndexOf('_');
        return separator >= 0 && int.TryParse(spriteName.Substring(separator + 1), out int index)
            ? index
            : 0;
    }

    private static void SetPixelsPerUnit(string fileName, float pixelsPerUnit)
    {
        string path = $"{ArtRoot}/{fileName}";
        if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
            !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit))
        {
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.SaveAndReimport();
        }
    }

    private static string ControllerPath(EnemySpec spec)
    {
        return $"Assets/_Project/Animations/{spec.Id}.controller";
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
