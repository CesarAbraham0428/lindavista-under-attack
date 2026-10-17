using System.Collections;

/// <summary>Spread first-use preparation across the level introduction, before combat.</summary>
internal static class CombatWarmup
{
    internal static IEnumerator Prepare()
    {
        yield return null;
        if (GameFlowController.IsMatchEnded) yield break;
        ShotVisual.Prewarm();
        yield return null;
        if (GameFlowController.IsMatchEnded) yield break;
        ShotEffect.Prewarm(48);
        yield return null;
        if (GameFlowController.IsMatchEnded) yield break;
        CombatProjectile.Prewarm();
        yield return null;
        if (GameFlowController.IsMatchEnded) yield break;
        SniperShotVisual.Prewarm();
        yield return null;
        if (GameFlowController.IsMatchEnded) yield break;
        RocketImpact.Prewarm();
    }
}
