using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button storeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button closeStoreButton;
    [SerializeField] private Button closeSettingsButton;
    [SerializeField] private GameObject storePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;

    private const string MasterVolumeKey = "Lindavista.MasterVolume";

    private void Awake()
    {
        CartoonMenuStyle.Apply(transform);
        if (playButton != null)
            playButton.onClick.AddListener(OpenCharacterSelection);

        if (storeButton != null)
            storeButton.onClick.AddListener(OpenStore);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettings);

        if (closeStoreButton != null)
            closeStoreButton.onClick.AddListener(ClosePanels);

        if (closeSettingsButton != null)
            closeSettingsButton.onClick.AddListener(ClosePanels);

        if (masterVolumeSlider != null)
        {
            float savedVolume = PlayerPrefs.GetFloat(MasterVolumeKey, AudioListener.volume);
            masterVolumeSlider.SetValueWithoutNotify(savedVolume);
            AudioListener.volume = savedVolume;
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (settingsPanel != null)
        {
            var shop = ProgressionUI.Button(settingsPanel.transform, "TIENDA · VIDA Y ARMAS", PistolUpgradeShop.Open);
            CartoonUI.ButtonStyle(shop);
            ProgressionUI.Percent((RectTransform)shop.transform, 0.3f, 0.37f, 0.7f, 0.45f);
        }
        ClosePanels();
    }

    public void OpenCharacterSelection()
    {
        SceneManager.LoadScene("CharacterSelection");
    }

    public void OpenStore()
    {
        SetPanelState(storePanel, false);
        SetPanelState(settingsPanel, false);
        PistolUpgradeShop.Open();
    }

    public void OpenSettings()
    {
        SetPanelState(storePanel, false);
        SetPanelState(settingsPanel, true);
    }

    public void ClosePanels()
    {
        SetPanelState(storePanel, false);
        SetPanelState(settingsPanel, false);
    }

    public void SetMasterVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(MasterVolumeKey, value);
    }

    private static void SetPanelState(GameObject panel, bool isVisible)
    {
        if (panel != null)
            panel.SetActive(isVisible);
    }
}
