using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PhoneUIButtonAudioController : MonoBehaviour
{
    [SerializeField] [Min(0.1f)] private float rebindIntervalSeconds = 0.5f;

    private readonly Dictionary<Button, UnityEngine.Events.UnityAction> callbacksByButton = new();
    private float nextRebindTime;

    private void OnEnable()
    {
        RebindButtons(forceRefresh: true);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRebindTime)
            RebindButtons(forceRefresh: false);
    }

    private void OnDisable()
    {
        nextRebindTime = 0f;
    }

    private void RebindButtons(bool forceRefresh)
    {
        nextRebindTime = Time.unscaledTime + Mathf.Max(0.1f, rebindIntervalSeconds);

        CleanupDeadButtons();

        var buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button == null)
                continue;

            if (!callbacksByButton.TryGetValue(button, out var callback))
            {
                callback = () => PlayClick(button);
                callbacksByButton[button] = callback;
            }

            button.onClick.RemoveListener(callback);
            button.onClick.AddListener(callback);
        }
    }

    private void CleanupDeadButtons()
    {
        if (callbacksByButton.Count == 0)
            return;

        var dead = ListPool<Button>.Get();
        foreach (var pair in callbacksByButton)
        {
            if (pair.Key != null)
                continue;

            dead.Add(pair.Key);
        }

        for (int i = 0; i < dead.Count; i++)
            callbacksByButton.Remove(dead[i]);

        ListPool<Button>.Release(dead);
    }

    private void PlayClick(Button sourceButton)
    {
        if (sourceButton == null || !sourceButton.IsActive() || !sourceButton.interactable)
            return;

        var phone = PhoneSystem.Instance;
        if (phone == null)
            return;

        string buttonName = sourceButton.name ?? string.Empty;
        if (IsPhoneCloseButton(buttonName))
            return;

        if (IsBackButton(buttonName))
        {
            phone.PlayPhoneBackSfx();
            return;
        }

        if (sourceButton.GetComponentInParent<ChatRoomItemUI>() != null)
            return;

        // The new message itself plays Blip. Avoid an extra confirm sound from the send button.
        if (sourceButton.GetComponentInParent<ChatRoomDetailUI>() != null)
            return;

        if (IsRuleTabButton(buttonName))
        {
            phone.PlayPhoneFocusSfx();
            return;
        }

        phone.PlayPhoneButtonClickSfx();
    }

    private static bool IsPhoneCloseButton(string buttonName)
    {
        return Contains(buttonName, "ClosePhone") || Contains(buttonName, "Power");
    }

    private static bool IsBackButton(string buttonName)
    {
        return Contains(buttonName, "Back");
    }

    private static bool IsRuleTabButton(string buttonName)
    {
        return Contains(buttonName, "Btn_Rule")
            || Contains(buttonName, "Btn_SchoolMeal")
            || Contains(buttonName, "Btn_Penalty");
    }

    private static bool Contains(string value, string token)
    {
        return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> Pool = new();

        public static List<T> Get()
        {
            return Pool.Count > 0 ? Pool.Pop() : new List<T>();
        }

        public static void Release(List<T> list)
        {
            list.Clear();
            Pool.Push(list);
        }
    }
}
