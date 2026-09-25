using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CharacterLevelSelectionController : MonoBehaviour
{
    [SerializeField] private Button[] characterButtons;
    [SerializeField] private Outline[] characterOutlines;
    [SerializeField] private Button[] levelButtons;
    [SerializeField] private Outline[] levelOutlines;
    [SerializeField] private bool[] levelUnlocked;
    [SerializeField] private Image[] levelLockIcons;
    [SerializeField] private Button backButton;
    [SerializeField] private Button startButton;

    private const string SelectedCharacterKey = "Lindavista.SelectedCharacter";
    private const string SelectedLevelKey = "Lindavista.SelectedLevel";

    private int selectedCharacter;
    private int selectedLevel;

    private void Awake()
    {
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
                bool isUnlocked = levelUnlocked == null || index >= levelUnlocked.Length || levelUnlocked[index];
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

        SelectCharacter(0);
        SelectLevel(0);
    }

    public void SelectCharacter(int index)
    {
        if (characterButtons == null || index < 0 || index >= characterButtons.Length)
            return;

        selectedCharacter = index;
        UpdateOutlines(characterOutlines, selectedCharacter);
    }

    public void SelectLevel(int index)
    {
        if (levelButtons == null || index < 0 || index >= levelButtons.Length)
            return;

        if (levelUnlocked != null && index < levelUnlocked.Length && !levelUnlocked[index])
            return;

        selectedLevel = index;
        UpdateOutlines(levelOutlines, selectedLevel);
    }

    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void StartGame()
    {
        PlayerPrefs.SetInt(SelectedCharacterKey, selectedCharacter);
        PlayerPrefs.SetInt(SelectedLevelKey, selectedLevel);
        SceneManager.LoadScene("Testing");
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
