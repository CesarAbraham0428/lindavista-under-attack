using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CharacterLevelSelectionController : MonoBehaviour
{
    [SerializeField] private Button[] characterButtons;
    [SerializeField] private Outline[] characterOutlines;
    [SerializeField] private Button[] levelButtons;
    [SerializeField] private Outline[] levelOutlines;
    [SerializeField] private Image[] levelLockIcons;
    [SerializeField] private Button backButton;
    [SerializeField] private Button startButton;

    private const string SelectedCharacterKey = "Lindavista.SelectedCharacter";
    private const string SelectedLevelKey = "Lindavista.SelectedLevel";

    private int selectedCharacter;
    private int selectedLevel;
    private bool loadingScene;

    private void Awake()
    {
        AdaptacionMenusUI.Configurar(GetComponent<Canvas>());
        // La flecha original se diseñó para ratón: una superficie mayor evita fallos con el pulgar.
        if (backButton != null)
            ((RectTransform)backButton.transform).sizeDelta = new Vector2(144f, 144f);
        if (characterButtons != null)
        {
            for (int i = 0; i < characterButtons.Length; i++)
            {
                int index = i;
                if (characterButtons[i] != null)
                    characterButtons[i].onClick.AddListener(() => SelectCharacter(index));
            }
        }

        if (levelButtons != null)
        {
            for (int i = 0; i < levelButtons.Length; i++)
            {
                int index = i;
                bool isUnlocked = ProgressionService.IsLevelUnlocked(index);
                if (levelButtons[i] != null)
                {
                    levelButtons[i].interactable = isUnlocked;
                }

                if (levelLockIcons != null && index < levelLockIcons.Length && levelLockIcons[index] != null)
                    levelLockIcons[index].gameObject.SetActive(!isUnlocked);

                if (levelButtons[i] != null && isUnlocked)
                    levelButtons[i].onClick.AddListener(() => SelectLevel(index));
            }
        }

        if (backButton != null)
            backButton.onClick.AddListener(ReturnToMainMenu);

        if (startButton != null)
            startButton.onClick.AddListener(StartGame);

        int previousCharacter = PlayerPrefs.GetInt(SelectedCharacterKey, 0);
        SelectCharacter(characterButtons != null && characterButtons.Length > 0 ? Mathf.Clamp(previousCharacter, 0, characterButtons.Length - 1) : 0);
        int previousLevel = PlayerPrefs.GetInt(SelectedLevelKey, 0);
        if (levelButtons != null && levelButtons.Length > 0)
            previousLevel = Mathf.Clamp(previousLevel, 0, levelButtons.Length - 1);
        SelectLevel(ProgressionService.IsLevelUnlocked(previousLevel) ? previousLevel : 0);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ReturnToMainMenu();
    }

    public void SelectCharacter(int index)
    {
        if (characterButtons == null || index < 0 || index >= characterButtons.Length)
            return;

        selectedCharacter = index;
        PlayerPrefs.SetInt(SelectedCharacterKey, selectedCharacter);
        UpdateOutlines(characterOutlines, selectedCharacter);
    }

    public void SelectLevel(int index)
    {
        if (levelButtons == null || index < 0 || index >= levelButtons.Length)
            return;

        if (!ProgressionService.IsLevelUnlocked(index))
            return;

        selectedLevel = index;
        PlayerPrefs.SetInt(SelectedLevelKey, selectedLevel);
        UpdateOutlines(levelOutlines, selectedLevel);
    }

    public void ReturnToMainMenu()
    {
        if (loadingScene) return;
        loadingScene = true;
        PlayerPrefs.Save();
        SceneManager.LoadScene("MainMenu");
    }

    public void StartGame()
    {
        if (loadingScene || !ProgressionService.IsLevelUnlocked(selectedLevel)) return;
        loadingScene = true;
        PlayerPrefs.SetInt(SelectedCharacterKey, selectedCharacter);
        PlayerPrefs.SetInt(SelectedLevelKey, selectedLevel);
        PlayerPrefs.Save();
        SceneManager.LoadScene("Gameplay");
    }

    private static void UpdateOutlines(Outline[] outlines, int selectedIndex)
    {
        if (outlines == null)
            return;

        for (int i = 0; i < outlines.Length; i++)
        {
            if (outlines[i] != null)
                outlines[i].enabled = i == selectedIndex;
        }
    }
}
