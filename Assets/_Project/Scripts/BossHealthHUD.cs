using UnityEngine;
using UnityEngine.UI;

/// <summary>Bottom-center boss health. Updated on damage, not rebuilt every frame.</summary>
public sealed class BossHealthHUD : MonoBehaviour
{
    private BossTruck boss;
    private EnemyHealth health;
    private Canvas canvas;
    private Image fill;
    private Text value, status;
    private BossTruck.CombatPhase lastPhase = (BossTruck.CombatPhase)(-1);
    private bool lastEnraged;
    public void Bind(BossTruck target)
    {
        boss = target;
        health = target.Health;
        // A scaled/moving world actor must never scale or offset a screen-space HUD.
        canvas = ProgressionUI.Canvas(null, "Final Boss Health Canvas", 125);
        var safe = AreaSeguraUI.Crear(canvas.transform);
        var panel = ProgressionUI.Image(safe, "Boss Health Frame", new Color(.08f, .065f, .05f, .98f));
        CartoonUI.Frame(panel, panel.color);
        ProgressionUI.Percent(panel.rectTransform, .27f, .025f, .73f, .128f);
        var title = ProgressionUI.Text(panel.transform, "Boss Title", "EL JEFE · BLINDADO", 29);
        CartoonUI.Typography(title, true);
        ProgressionUI.Percent(title.rectTransform, .025f, .57f, .65f, .98f);
        value = ProgressionUI.Text(panel.transform, "Boss Health Value", "", 25, TextAnchor.MiddleRight);
        CartoonUI.Typography(value);
        ProgressionUI.Percent(value.rectTransform, .65f, .58f, .97f, .98f);
        var track = ProgressionUI.Image(panel.transform, "Boss Health Track", new Color(.20f, .025f, .022f));
        ProgressionUI.Percent(track.rectTransform, .025f, .31f, .975f, .56f);
        fill = ProgressionUI.Image(track.transform, "Boss Health Fill", new Color(.85f, .10f, .06f));
        ProgressionUI.Percent(fill.rectTransform, 0, 0, 1, 1);
        status = ProgressionUI.Text(panel.transform, "Boss Attack Warning", "", 21, TextAnchor.MiddleCenter);
        CartoonUI.Typography(status);
        ProgressionUI.Percent(status.rectTransform, .02f, .015f, .98f, .30f);
        health.HealthChanged += OnHealth;
        OnHealth(health.CurrentHealth, health.MaxHealth);
    }
    private void OnHealth(int current, int maximum)
    {
        value.text = $"{current} / {maximum}";
        fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(current / (float)Mathf.Max(1, maximum)), 1);
        if (current == 0) canvas.enabled = false;
    }
    private void LateUpdate()
    {
        if (canvas == null || boss == null) return;
        canvas.enabled = !health.IsDead && !GameFlowController.IsMatchEnded && !GameFlowController.IsPaused && !PistolUpgradeShop.IsOpen;
        if (lastPhase == boss.Phase && lastEnraged == boss.Enraged) return;
        lastPhase = boss.Phase;
        lastEnraged = boss.Enraged;
        status.text = boss.Phase switch
        {
            BossTruck.CombatPhase.Entering => "EL JEFE SE ACERCA",
            BossTruck.CombatPhase.Warning => "PREPARANDO RÁFAGA · ¡MUÉVETE!",
            BossTruck.CombatPhase.Firing => "¡A CUBIERTO!",
            _ => boss.Enraged ? "BLINDADO DAÑADO · RÁFAGAS MÁS LARGAS" : "RECARGANDO · ¡CONTRAATACA!"
        };
        status.color = boss.Phase == BossTruck.CombatPhase.Warning || boss.Phase == BossTruck.CombatPhase.Firing
            ? new Color(1f, .72f, .29f) : CartoonUI.Cream;
    }
    private void OnDestroy()
    {
        if (health != null) health.HealthChanged -= OnHealth;
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
