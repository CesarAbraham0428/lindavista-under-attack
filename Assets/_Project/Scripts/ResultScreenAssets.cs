using UnityEngine;

/// <summary>Existing menu artwork shared by the runtime defeat screen.</summary>
[CreateAssetMenu(menuName = "Lindavista/Result screen assets")]
public sealed class ResultScreenAssets : ScriptableObject
{
    [SerializeField] private Sprite woodSign;
    public Sprite WoodSign => woodSign;
}
