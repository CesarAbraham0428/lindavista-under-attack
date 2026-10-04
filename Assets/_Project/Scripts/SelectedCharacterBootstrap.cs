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
        if (scene.name != "Gameplay")
            return;

        int selectedLevel = PlayerPrefs.GetInt("Lindavista.SelectedLevel", 0);
        if (!ProgressionService.IsLevelUnlocked(selectedLevel))
            PlayerPrefs.SetInt("Lindavista.SelectedLevel", 0);

        GameObject marco = null;
        GameObject cesar = null;
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == "Player_Marco")
                marco = rootObject;
            else if (rootObject.name == "Player_Cesar")
                cesar = rootObject;
        }

        bool selectMarco = PlayerPrefs.GetInt(SelectedCharacterKey, 0) == 0;

        if (marco != null)
            marco.SetActive(selectMarco);

        if (cesar != null)
            cesar.SetActive(!selectMarco);
    }
}
