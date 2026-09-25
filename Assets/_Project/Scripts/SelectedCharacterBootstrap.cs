using UnityEngine;
using UnityEngine.SceneManagement;

public static class SelectedCharacterBootstrap
{
    private const string SelectedCharacterKey = "Lindavista.SelectedCharacter";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= ApplySelection;
        SceneManager.sceneLoaded += ApplySelection;
    }

    private static void ApplySelection(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Testing" || !PlayerPrefs.HasKey(SelectedCharacterKey))
            return;

        GameObject marco = GameObject.Find("Player_Marco");
        GameObject cesar = GameObject.Find("Player_Cesar");
        bool selectMarco = PlayerPrefs.GetInt(SelectedCharacterKey, 0) == 0;

        if (marco != null)
            marco.SetActive(selectMarco);

        if (cesar != null)
            cesar.SetActive(!selectMarco);
    }
}
