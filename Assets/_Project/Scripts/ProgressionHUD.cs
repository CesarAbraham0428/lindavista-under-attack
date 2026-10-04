using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ProgressionHUD : MonoBehaviour
{
    private static ProgressionHUD active;
    private RectTransform safeArea;
    private Text wallet, notice;
    private Image coinIcon;
    private HUDSpriteVariants coinArtwork;
    private GameObject collection;
    private float refreshAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if ((scene.name == "Gameplay" || scene.name == "Testing") && active == null)
            new GameObject("Progression HUD").AddComponent<ProgressionHUD>();
    }
    private void Awake()
    {
        active = this;
        var canvas = ProgressionUI.Canvas(transform, "Progression Canvas", 115);
        safeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
        safeArea.SetParent(canvas.transform, false);
        var artwork = Resources.Load<WeaponHUDAssets>("WeaponHUDAssets");
        coinArtwork = new HUDSpriteVariants(artwork != null && artwork.Coin != null ? artwork.Coin : ProgressionVisuals.Coin);
        coinIcon = ProgressionUI.Image(safeArea, "Coin Icon", Color.white);
        coinIcon.preserveAspect = true;
        ProgressionUI.Top(coinIcon.rectTransform, new Vector2(0, 1), new Vector2(32, -114), new Vector2(60, 60));
        wallet = ProgressionUI.Text(safeArea, "Coins", "", 44);
        wallet.color = ProgressionUI.Gold;
        ProgressionUI.Top(wallet.rectTransform, new Vector2(0, 1), new Vector2(100, -114), new Vector2(82, 60));
        var outline = wallet.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        var finish = ProgressionUI.Button(safeArea, "FINALIZAR NIVEL", GameFlowController.FinishLevel);
        ProgressionUI.Percent((RectTransform)finish.transform, 0.34f, 0.03f, 0.66f, 0.13f);
        collection = finish.gameObject;
        collection.SetActive(false);
        notice = ProgressionUI.Text(safeArea, "Notice", "", 30, TextAnchor.MiddleCenter);
        ProgressionUI.Percent(notice.rectTransform, 0.15f, 0.14f, 0.85f, 0.21f);
        ProgressionService.Changed += Refresh;
        Refresh();
    }
    private void Update()
    {
        if (Screen.width > 0 && Screen.height > 0)
        {
            Rect area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
        if (Time.unscaledTime >= refreshAt)
        {
            Refresh();
            refreshAt = Time.unscaledTime + 0.25f;
        }
    }
    private void Refresh()
    {
        int coins = ProgressionService.Coins;
        wallet.text = coins.ToString();
        wallet.color = coins > 0 ? ProgressionUI.Gold : new Color(0.65f, 0.65f, 0.65f);
        coinIcon.sprite = coins > 0 ? coinArtwork.ColorSprite : coinArtwork.GraySprite;
        coinIcon.color = coins > 0 ? Color.white : new Color(0.65f, 0.65f, 0.65f);
        bool collecting = GameFlowController.IsCollectionPhase && !GameFlowController.IsMatchEnded;
        collection.SetActive(collecting);
        notice.text = ProgressionService.LastError ?? (collecting ? "¡Oleada eliminada! Recoge tus monedas antes de finalizar." : "");
    }
    private void OnDestroy()
    {
        ProgressionService.Changed -= Refresh;
        coinArtwork?.Dispose();
        if (active == this) active = null;
    }
}
