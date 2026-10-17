using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Creates missing references only; never overwrites tuned assets or imports Boss_Base.</summary>
public static class LevelFourAssetSetup
{
    public const string AssetPath = "Assets/_Project/Resources/LevelFourAssets.asset";
    public const string PrefabPath = "Assets/_Project/Prefabs/BossTruck.prefab";
    private const string Art = "Assets/_Project/Art/Characters/Enemies/";

    [InitializeOnLoadMethod]
    private static void PrepareAfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                AssetDatabase.LoadAssetAtPath<LevelFourAssets>(AssetPath) == null)
                Prepare();
        };
    }

    [MenuItem("Tools/Lindavista/Prepare level 4 assets")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var portrait = AssetDatabase.LoadAllAssetsAtPath(Art + "Boss_Base.png").OfType<Sprite>().FirstOrDefault();
        var frames = AssetDatabase.LoadAllAssetsAtPath(Art + "Boss_Truck_Fire_8f.png")
            .OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Animations/BossTruck.controller");
        if (portrait == null || frames.Length < 8 || controller == null)
        {
            Debug.LogError("Level 4: Boss_Base, the eight truck fire frames and BossTruck.controller are required.");
            return;
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            var go = new GameObject("BossTruck");
            go.SetActive(false);
            try
            {
                Sprite idle = frames[0];
                var sprite = go.AddComponent<SpriteRenderer>();
                sprite.sprite = idle;
                sprite.sortingOrder = 6;
                // Reuse the same URP 2D material as the other actors, if present.
                var ordinary = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy4_Base.prefab");
                var material = ordinary != null ? ordinary.GetComponent<SpriteRenderer>().sharedMaterial : null;
                if (material != null) sprite.sharedMaterial = material;
                go.transform.localScale = Vector3.one * (8.8f / idle.bounds.size.x);
                var animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.offset = new Vector2(0f, -.18f);
                box.size = new Vector2(idle.bounds.size.x * .88f, idle.bounds.size.y * .78f);
                var health = go.AddComponent<EnemyHealth>();
                var serializedHealth = new SerializedObject(health);
                serializedHealth.FindProperty("maxHealth").intValue = 300;
                serializedHealth.FindProperty("defeatDespawnDelay").floatValue = 1.4f;
                serializedHealth.FindProperty("playDamageAnimation").boolValue = false;
                serializedHealth.ApplyModifiedPropertiesWithoutUndo();
                var brain = go.AddComponent<BossTruck>();
                var settings = new SerializedObject(brain);
                settings.FindProperty("standingFootY").floatValue =
                    (idle.rect.height * .045f - idle.pivot.y) / idle.pixelsPerUnit;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var loot = go.AddComponent<EnemyLootDrop>();
                var rewards = new SerializedObject(loot);
                rewards.FindProperty("coinValue").intValue = 150;
                rewards.FindProperty("healthDropChance").floatValue = 0;
                rewards.FindProperty("ammoDropChance").floatValue = 0;
                rewards.ApplyModifiedPropertiesWithoutUndo();
                go.SetActive(true);
                prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        var assets = AssetDatabase.LoadAssetAtPath<LevelFourAssets>(AssetPath);
        if (assets == null)
        {
            assets = ScriptableObject.CreateInstance<LevelFourAssets>();
            AssetDatabase.CreateAsset(assets, AssetPath);
        }
        var references = new SerializedObject(assets);
        // Fill missing links only, preserving manual Inspector choices.
        if (references.FindProperty("portrait").objectReferenceValue == null)
            references.FindProperty("portrait").objectReferenceValue = portrait;
        if (references.FindProperty("truckPrefab").objectReferenceValue == null)
            references.FindProperty("truckPrefab").objectReferenceValue = prefab;
        references.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
        Debug.Log("Level 4 ready: original portrait, 300-health truck, four existing enemy types and final encounter.");
    }
}
