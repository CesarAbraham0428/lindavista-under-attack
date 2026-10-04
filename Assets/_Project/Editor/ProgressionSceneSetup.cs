using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Idempotent content integration; preserves existing enemy/character animation assets.</summary>
public static class ProgressionSceneSetup
{
    private const string Root = "Assets/_Project/";

    [MenuItem("Tools/Lindavista/Configure pistol progression")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before configuring content.");
        string originalScene = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EnsureFolder(Root + "Resources");
        var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Root + "Resources/PistolDefinition.asset");
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            AssetDatabase.CreateAsset(definition, Root + "Resources/PistolDefinition.asset");
        }
        AssetDatabase.SaveAssets();
        GameObject coin = EnsureCoinPrefab();
        for (int i = 1; i <= 4; i++)
        {
            string path = Root + "Prefabs/Enemy" + i + "_Base.prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureLoot(contents, coin, i == 1 ? 5 : i == 2 ? 10 : i == 3 ? 15 : 20);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        foreach (string name in new[] { "Gameplay", "Testing" })
        {
            Scene scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity", OpenSceneMode.Single);
            // Opening a scene can unload references held by the preceding scene.
            definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Root + "Resources/PistolDefinition.asset");
            coin = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Resources/CoinPickup.prefab");
            foreach (PlayerActions player in Object.FindObjectsByType<PlayerActions>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (player.gameObject.scene != scene) continue;
                if (player.GetComponent<PlayerHealth>() == null) player.gameObject.AddComponent<PlayerHealth>();
                var weapon = player.GetComponent<PlayerWeaponController>();
                if (weapon == null) weapon = player.gameObject.AddComponent<PlayerWeaponController>();
                var properties = new SerializedObject(weapon);
                properties.FindProperty("definition").objectReferenceValue = definition;
                properties.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (EnemyHealth enemy in Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (enemy.gameObject.scene != scene) continue;
                int reward = enemy.MaxHealth <= 10 ? 5 : enemy.MaxHealth <= 15 ? 10 : enemy.MaxHealth <= 30 ? 15 : 20;
                ConfigureLoot(enemy.gameObject, coin, reward);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Scene selection = EditorSceneManager.OpenScene(Root + "Scenes/CharacterSelection.unity", OpenSceneMode.Single);
        EditorSceneManager.SaveScene(selection);
        if (!string.IsNullOrEmpty(originalScene)) EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        Debug.Log("Pistol progression configured: characters, four enemy prefabs, coin pickup and weapon definition.");
    }

    private static void ConfigureLoot(GameObject enemy, GameObject coin, int value)
    {
        var loot = enemy.GetComponent<EnemyLootDrop>();
        if (loot == null) loot = enemy.AddComponent<EnemyLootDrop>();
        var properties = new SerializedObject(loot);
        properties.FindProperty("coinValue").intValue = value;
        properties.FindProperty("coinPrefab").objectReferenceValue = coin;
        properties.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject EnsureCoinPrefab()
    {
        string prefabPath = Root + "Resources/CoinPickup.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;
        EnsureFolder(Root + "Art/UI/Progression");
        string imagePath = Root + "Art/UI/Progression/coin.png";
        if (!File.Exists(imagePath))
        {
            Texture2D texture = ProgressionVisuals.CoinTexture();
            File.WriteAllBytes(imagePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(imagePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 64;
        importer.filterMode = FilterMode.Point;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        var coin = new GameObject("Coin Pickup", typeof(SpriteRenderer), typeof(CoinPickup));
        try
        {
            coin.layer = 2;
            var renderer = coin.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
            renderer.sortingOrder = 20;
            var collider = coin.GetComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.6f;
            var body = coin.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0;
            return PrefabUtility.SaveAsPrefabAsset(coin, prefabPath);
        }
        finally { Object.DestroyImmediate(coin); }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
