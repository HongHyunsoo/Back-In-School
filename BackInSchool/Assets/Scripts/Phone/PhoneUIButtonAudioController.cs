using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PhoneUIButtonAudioController : MonoBehaviour
{
    [SerializeField] [Min(0.1f)] private float rebindIntervalSeconds = 0.5f;

    private readonly Dictionary<Button, UnityEngine.Events.UnityAction> callbacksByButton = new();
    private AudioSource audioSource;
    private float nextRebindTime;

    private void OnEnable()
    {
        EnsureAudioSource();
        RebindButtons(forceRefresh: true);
    }

    private void Update()
    {
        EnsureAudioSource();

        if (Time.unscaledTime >= nextRebindTime)
            RebindButtons(forceRefresh: false);
    }

    private void OnDisable()
    {
        nextRebindTime = 0f;
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
            return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
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

        EnsureAudioSource();
        if (audioSource == null || PhoneSystem.Instance == null)
            return;

        var clip = PhoneSystem.Instance.PhoneButtonClickClip;
        if (clip == null)
            return;

        audioSource.PlayOneShot(clip, AudioSettingsService.ScaleSfx(PhoneSystem.Instance.PhoneButtonClickVolume));
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
