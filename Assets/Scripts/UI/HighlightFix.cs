using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


/// <summary>
/// This Script is from:
/// https://forum.unity.com/members/daterre.121542/ 
/// Then an updated version of it from:
/// https://forum.unity.com/members/aydin_khp.5126488/
/// Found on the forum here:
/// https://forum.unity.com/threads/button-keyboard-and-mouse-highlighting.294147/
/// It fixes the annoying highlight / selection issue with buttons when using both keyboard/gamepad and mouse to interact with UI
/// It must be placed on the button / selectable UI in order to function
/// </summary>
[RequireComponent(typeof(Selectable))]
public class HighlightFix : MonoBehaviour, IPointerEnterHandler, IDeselectHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null || !isActiveAndEnabled)
            return;

        if (!eventSystem.alreadySelecting)
            eventSystem.SetSelectedGameObject(gameObject);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null || !isActiveAndEnabled)
            return;

        if (eventSystem.currentSelectedGameObject == gameObject && !eventSystem.alreadySelecting)
            eventSystem.SetSelectedGameObject(null);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        var selectable = GetComponent<Selectable>();
        if (selectable != null)
            selectable.OnPointerExit(null);
    }
}
