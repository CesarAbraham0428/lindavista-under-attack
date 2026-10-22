using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public sealed class MenuInputModuleBootstrap : MonoBehaviour
{
    private void Awake()
    {
        EventSystem eventSystem = GetComponent<EventSystem>();
        if (eventSystem == null)
            eventSystem = gameObject.AddComponent<EventSystem>();

        InputSystemUIInputModule inputModule = GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
            inputModule = gameObject.AddComponent<InputSystemUIInputModule>();

        if (inputModule.actionsAsset == null)
            inputModule.AssignDefaultActions();
    }
}
