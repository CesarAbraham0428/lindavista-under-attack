using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One click/tap uses one saved pill; the same action is bound to H on desktop.</summary>
public sealed class HealthConsumableHUD : MonoBehaviour
{
    private static HealthConsumableHUD active;
    private Canvas canvas;
    private Button use;
    private Text label;
    private PlayerActions player;
    private float nextSearch;
    private string previousStatus;
    private int previousAmount = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Gameplay" && active == null)
            new GameObject("Health Consumable HUD").AddComponent<HealthConsumableHUD>();
    }

    private void Awake()
    {
        active = this;
        canvas = ProgressionUI.Canvas(transform, "Health Consumable Canvas", 125);
        var safe = AreaSeguraUI.Crear(canvas.transform);
        use = ProgressionUI.Button(safe, "Usar pastilla", Use);
        use.gameObject.name = "Use Health Consumable";
        CartoonUI.ButtonStyle(use);
        use.image.color = new Color(.30f, .43f, .22f);
        var rect = (RectTransform)use.transform;
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-24f, -140f);
        rect.sizeDelta = new Vector2(270f, 150f);
        label = use.GetComponentInChildren<Text>();
        label.resizeTextMaxSize = label.fontSize = 30;
        label.resizeTextMinSize = 22;
        ProgressionUI.Percent(label.rectTransform, .40f, .08f, .97f, .92f);
        var icon = ProgressionUI.Image(use.transform, "Pastilla", Color.white);
        var assets = HealthConsumableAssets.Load();
        icon.sprite = assets != null ? assets.icon : null;
        icon.preserveAspect = true;
        ProgressionUI.Percent(icon.rectTransform, .025f, .10f, .40f, .90f);
    }

    private void Update()
    {
        bool visible = !GameFlowController.IsMatchEnded && !GameFlowController.IsPaused &&
            !PistolUpgradeShop.IsOpen && !BossIntroduction.IsActive;
        if (canvas.gameObject.activeSelf != visible) canvas.gameObject.SetActive(visible);
        if (!visible) return;
        if ((player == null || !player.gameObject.activeInHierarchy) && Time.unscaledTime >= nextSearch)
        {
            player = FindFirstObjectByType<PlayerActions>();
            nextSearch = Time.unscaledTime + .5f;
        }
        use.interactable = player != null && player.CanUseHealthConsumable;
        int amount = ProgressionService.HealthConsumables(player != null ? player.Health.IsCesar : CharacterWeapons.SelectedCesar);
        string status = player != null && player.IsUsingHealthConsumable ? "USANDO…" :
            player != null && !player.Health.CanHeal ? "VIDA EXTRA" :
            Application.isMobilePlatform || MobileControlsHUD.IsVisible ? "USAR +1" : "H · VIDA +1";
        if (previousStatus == status && previousAmount == amount) return;
        previousStatus = status;
        previousAmount = amount;
        label.text = status + "\n" + amount + " / " + ShopBalance.MaxHealthConsumables;
    }

    private void Use()
    {
        if (player != null) player.TryUseHealthConsumable();
    }

    private void OnDestroy()
    {
        if (active == this) active = null;
    }
}
