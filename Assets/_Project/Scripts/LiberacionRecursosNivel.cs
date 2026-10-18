using System.Collections;
using UnityEngine;

/// <summary>Unloads, once, the assets that only the destroyed character and level variants referenced.</summary>
public sealed class LiberacionRecursosNivel : MonoBehaviour
{
    private IEnumerator Start()
    {
        // Destroy() completes at the end of the frame; release afterwards, during the static intro.
        yield return null;
        Resources.UnloadUnusedAssets();
        Destroy(gameObject);
    }
}
