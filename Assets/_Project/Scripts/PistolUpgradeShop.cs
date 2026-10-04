using UnityEngine;
using UnityEngine.UI;

public sealed class PistolUpgradeShop : MonoBehaviour
{
    private static PistolUpgradeShop active;
    private Text wallet, details, feedback, purchaseLabel;
    private Button purchase;
    public static bool IsOpen => active != null;

    public static void Open()
    {
        if (active != null) return;
        new GameObject("Pistol Upgrade Shop").AddComponent<PistolUpgradeShop>();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = null; }
    private void Awake()
    {
        active = this;
        var canvas = ProgressionUI.Canvas(transform, "Shop Canvas", 400);
        var overlay = ProgressionUI.Image(canvas.transform, "Overlay", new Color(0.01f, 0.02f, 0.03f, 0.92f), true);
        ProgressionUI.Percent(overlay.rectTransform, 0, 0, 1, 1);
        var panel = ProgressionUI.Image(canvas.transform, "Shop Panel", ProgressionUI.PanelColor);
        ProgressionUI.Percent(panel.rectTransform, 0.12f, 0.12f, 0.88f, 0.88f);
        var title = ProgressionUI.Text(panel.transform, "Title", "MEJORAR PISTOLA", 64, TextAnchor.MiddleCenter);
        title.color = ProgressionUI.Gold;
        ProgressionUI.Percent(title.rectTransform, 0.05f, 0.82f, 0.95f, 0.95f);
        wallet = ProgressionUI.Text(panel.transform, "Wallet", "", 40, TextAnchor.MiddleCenter);
        ProgressionUI.Percent(wallet.rectTransform, 0.08f, 0.70f, 0.92f, 0.82f);
        details = ProgressionUI.Text(panel.transform, "Details", "", 42, TextAnchor.MiddleCenter);
        ProgressionUI.Percent(details.rectTransform, 0.08f, 0.43f, 0.92f, 0.68f);
        feedback = ProgressionUI.Text(panel.transform, "Feedback", "", 28, TextAnchor.MiddleCenter);
        ProgressionUI.Percent(feedback.rectTransform, 0.05f, 0.30f, 0.95f, 0.43f);
        purchase = ProgressionUI.Button(panel.transform, "MEJORAR", Buy);
        ProgressionUI.Percent((RectTransform)purchase.transform, 0.18f, 0.17f, 0.82f, 0.29f);
        purchaseLabel = purchase.GetComponentInChildren<Text>();
        var close = ProgressionUI.Button(panel.transform, "VOLVER", () => Destroy(gameObject));
        ProgressionUI.Percent((RectTransform)close.transform, 0.28f, 0.03f, 0.72f, 0.14f);
        ProgressionService.Changed += Refresh;
        Refresh();
    }
    private void Refresh()
    {
        var definition = WeaponDefinition.Pistol;
        int upgrade = ProgressionService.PistolUpgrade;
        int cost = definition.UpgradeCost(upgrade);
        wallet.text = "MONEDAS: " + ProgressionService.Coins;
        details.text = "PISTOLA · NIVEL " + (upgrade + 1) + "\nDaño actual: " + definition.DamageAt(upgrade) +
            (cost > 0 ? "  →  " + definition.DamageAt(upgrade + 1) : "\nMejora máxima alcanzada");
        purchaseLabel.text = cost > 0 ? "MEJORAR · " + cost + " MONEDAS" : "NIVEL MÁXIMO";
        purchase.interactable = cost > 0 && ProgressionService.Coins >= cost && ProgressionService.CanSave;
        feedback.text = !ProgressionService.CanSave ? ProgressionService.LastError :
            cost > ProgressionService.Coins ? "Recoge más monedas para esta mejora." : "Las mejoras se conservan al reintentar.";
    }
    private void Buy()
    {
        if (ProgressionService.TryUpgradePistol()) feedback.text = "¡Pistola mejorada!";
        else feedback.text = ProgressionService.LastError ?? "No se pudo completar la compra.";
    }
    private void OnDestroy()
    {
        ProgressionService.Changed -= Refresh;
        if (active == this) active = null;
    }
}
