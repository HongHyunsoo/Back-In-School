using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Subway(CHAT) 전원 버튼 하차 조건 + 자가진단 벌점 규칙.
/// - 채팅 미완료: "아직 내릴 역이 아니야"를 Robot 위 말풍선으로 출력.
/// - 채팅 완료 후 전원:
///   - 건강 자가진단 완료: 벌점 0
///   - 미완료: 벌점 +1
/// </summary>
public class PhoneSubwayFlowGate : MonoBehaviour
{
    private static readonly HashSet<int> healthCheckedDays = new();

    private Button powerButton;
    private Button closePhoneButton;

    private Transform sceneRobotTransform;
    private RectTransform warningBubbleRoot;
    private TextMeshProUGUI warningNameText;
    private TextMeshProUGUI warningBodyText;

    private Coroutine messageRoutine;
    private bool isProcessingPower;
    private PhoneAppManager phoneAppManager;

    private void Start()
    {
        powerButton = FindButtonByName("Btn_Power");
        if (powerButton != null)
        {
            powerButton.onClick.AddListener(OnPowerPressed);
            Debug.Log("[PhoneSubwayFlowGate] Btn_Power listener attached.");
        }
        else
        {
            Debug.LogWarning("[PhoneSubwayFlowGate] Btn_Power not found.");
        }

        closePhoneButton = FindButtonByName("Btn_ClosePhone");
        if (closePhoneButton != null)
        {
            closePhoneButton.onClick.AddListener(OnPowerPressed);
            Debug.Log("[PhoneSubwayFlowGate] Btn_ClosePhone listener attached.");
        }
        else
        {
            Debug.LogWarning("[PhoneSubwayFlowGate] Btn_ClosePhone not found.");
        }

        phoneAppManager = FindAnyObjectByType<PhoneAppManager>();
        if (phoneAppManager != null)
        {
            phoneAppManager.OnRequestPower -= OnPowerPressed;
            phoneAppManager.OnRequestPower += OnPowerPressed;
            phoneAppManager.OnRequestClosePhone -= OnPowerPressed;
            phoneAppManager.OnRequestClosePhone += OnPowerPressed;
            Debug.Log("[PhoneSubwayFlowGate] PhoneAppManager.OnRequestPower listener attached.");
        }

        sceneRobotTransform = FindSceneRobotTransform();
        EnsureRobotBubble();
    }

    private void OnDestroy()
    {
        if (powerButton != null)
            powerButton.onClick.RemoveListener(OnPowerPressed);
        if (closePhoneButton != null)
            closePhoneButton.onClick.RemoveListener(OnPowerPressed);

        if (phoneAppManager != null)
        {
            phoneAppManager.OnRequestPower -= OnPowerPressed;
            phoneAppManager.OnRequestClosePhone -= OnPowerPressed;
        }
    }

    private void Update()
    {
        if (warningBubbleRoot != null && warningBubbleRoot.gameObject.activeSelf)
            PositionBubbleAtRobot();
    }

    private void OnPowerPressed()
    {
        Debug.Log("[PhoneSubwayFlowGate] Power pressed. FLOW_TYPE=" + PlayerPrefs.GetString("FLOW_TYPE", ""));
        if (!IsSubwayContext() || isProcessingPower) return;

        if (!CanLeaveSubwayNow())
        {
            ShowMessage("\uC544\uC9C1 \uB0B4\uB9B4 \uC5ED\uC774 \uC544\uB2C8\uC57C");
            return;
        }

        isProcessingPower = true;

        int day = GetCurrentDay();
        bool checkedHealth = healthCheckedDays.Contains(day);
        int penalty = checkedHealth ? 0 : 1;

        Debug.Log("[PhoneSubwayFlowGate] Complete subway event. day=" + day + ", penaltyDelta=" + penalty);
        StartCoroutine(CoCompleteAfterFeedback(penalty));
    }

    private bool IsSubwayContext()
    {
        return PlayerPrefs.GetString("FLOW_TYPE", "") == "CHAT";
    }

    private bool CanLeaveSubwayNow()
    {
        if (ChatService.Instance == null || LocalizationManager.Instance == null)
            return false;

        if (ChatService.Instance.HasActiveSession)
            return false;

        int day = GetCurrentDay();
        var segs = LocalizationManager.Instance.GetChatSegments(day, GameState.Subway);
        if (segs == null || segs.Count == 0)
        {
            // 폴백: 세그먼트가 없으면 현재 저장 상태 기준으로 판정
            int totalUnread = ChatService.Instance.GetTotalUnread();
            bool hasAnySession = ChatService.Instance.Data != null && ChatService.Instance.Data.sessions != null && ChatService.Instance.Data.sessions.Count > 0;
            bool anySessionProgressed = false;
            if (hasAnySession)
            {
                var sessions = ChatService.Instance.Data.sessions;
                for (int i = 0; i < sessions.Count; i++)
                {
                    if (sessions[i].completed || sessions[i].progressIndex > 0)
                    {
                        anySessionProgressed = true;
                        break;
                    }
                }
            }

            bool fallbackPass = (totalUnread <= 0) && anySessionProgressed;
            Debug.Log("[PhoneSubwayFlowGate] Fallback leave check: totalUnread=" + totalUnread + ", anySessionProgressed=" + anySessionProgressed + ", pass=" + fallbackPass);
            return fallbackPass;
        }

        var requiredConversations = new HashSet<string>();
        var requiredRooms = new HashSet<string>();
        for (int i = 0; i < segs.Count; i++)
        {
            if (!string.IsNullOrEmpty(segs[i].conversationId))
                requiredConversations.Add(segs[i].conversationId);
            if (!string.IsNullOrEmpty(segs[i].roomId))
                requiredRooms.Add(segs[i].roomId);
        }

        if (requiredConversations.Count == 0)
        {
            Debug.LogWarning("[PhoneSubwayFlowGate] Subway segments have no conversation ids. Blocking leave.");
            return false;
        }

        // 요구된 채팅방의 unread가 0이어야 함 (유저가 확인했다는 뜻)
        foreach (var roomId in requiredRooms)
        {
            var room = ChatService.Instance.GetRoom(roomId);
            if (room == null)
            {
                Debug.Log("[PhoneSubwayFlowGate] Room not found: " + roomId);
                return false;
            }
            if (room.unreadCount > 0)
            {
                Debug.Log("[PhoneSubwayFlowGate] Room unread remains: " + roomId + " -> " + room.unreadCount);
                return false;
            }
        }

        return true;
    }

    private int GetCurrentDay()
    {
        if (FlowManager.Instance != null)
            return FlowManager.Instance.day;

        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            return gm.currentDay;

        return 1;
    }

    public static bool IsHealthChecked(int day)
    {
        return healthCheckedDays.Contains(day);
    }

    public static bool MarkHealthCheckedForDay(int day)
    {
        if (day <= 0) return false;
        return healthCheckedDays.Add(day);
    }

    private Button FindButtonByName(string name)
    {
        var tr = transform.Find(name);
        if (tr != null) return tr.GetComponent<Button>();

        var all = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name) return all[i];
        }

        var byGlobalName = GameObject.Find(name);
        if (byGlobalName != null)
            return byGlobalName.GetComponent<Button>();

        return null;
    }

    private void ShowMessage(string message)
    {
        EnsureRobotBubble();
        if (warningBubbleRoot == null || warningBodyText == null)
        {
            Debug.Log("[PhoneSubwayFlowGate] " + message);
            return;
        }

        if (warningNameText != null)
            warningNameText.text = "Robot";

        warningBodyText.text = message;
        warningBodyText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        if (warningNameText != null)
            warningNameText.color = new Color(0.05f, 0.05f, 0.05f, 1f);

        PositionBubbleAtRobot();
        warningBubbleRoot.gameObject.SetActive(true);
        warningBubbleRoot.SetAsLastSibling();

        if (messageRoutine != null)
            StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(CoHideMessageLater());
    }

    private IEnumerator CoHideMessageLater()
    {
        yield return new WaitForSecondsRealtime(1.6f);
        if (warningBubbleRoot != null)
            warningBubbleRoot.gameObject.SetActive(false);
        messageRoutine = null;
    }

    private IEnumerator CoCompleteAfterFeedback(int penalty)
    {
        yield return new WaitForSecondsRealtime(0.45f);

        if (FlowManager.Instance != null)
        {
            FlowManager.Instance.CompleteCurrentEvent(penalty);
        }
        else
        {
            var gm = FindAnyObjectByType<GameManager>();
            if (gm != null)
                gm.SubwayChatFinished();
        }

        isProcessingPower = false;
    }

    private void EnsureRobotBubble()
    {
        if (sceneRobotTransform == null || !sceneRobotTransform.gameObject.activeInHierarchy)
            sceneRobotTransform = FindSceneRobotTransform();

        if (warningBubbleRoot != null && warningBodyText != null)
            return;

        Transform bubbleParent = GetBubbleCanvasTransform();
        if (bubbleParent == null) return;

        SpeechBubbleUI template = TryGetDialogBoxTemplateFromDialogueManager();
        if (template != null)
        {
            var bubble = Instantiate(template, bubbleParent);
            warningBubbleRoot = bubble.transform as RectTransform;
            warningNameText = bubble.nameText;
            warningBodyText = bubble.bodyText;

            warningBubbleRoot.localScale = Vector3.one * 0.65f;
            warningBubbleRoot.gameObject.name = "SubwayRobotBubble";
            warningBubbleRoot.gameObject.SetActive(false);
            EnsureBubbleHasVisibleBackground(warningBubbleRoot);
            return;
        }

        var go = new GameObject("SubwayRobotBubble", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(bubbleParent, false);

        warningBubbleRoot = go.GetComponent<RectTransform>();
        warningBubbleRoot.anchorMin = new Vector2(0.5f, 0.5f);
        warningBubbleRoot.anchorMax = new Vector2(0.5f, 0.5f);
        warningBubbleRoot.pivot = new Vector2(0.5f, 0f);
        warningBubbleRoot.sizeDelta = new Vector2(360f, 140f);

        var bg = go.GetComponent<RawImage>();
        bg.color = new Color(1f, 1f, 1f, 0.95f);
        bg.raycastTarget = false;

        var txtGo = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(12f, 10f);
        txtRt.offsetMax = new Vector2(-12f, -10f);

        warningBodyText = txtGo.GetComponent<TextMeshProUGUI>();
        warningBodyText.alignment = TextAlignmentOptions.Center;
        warningBodyText.fontSize = 24f;
        warningBodyText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        warningBodyText.enableWordWrapping = true;

        warningBubbleRoot.gameObject.SetActive(false);
    }

    private void PositionBubbleAtRobot()
    {
        if (warningBubbleRoot == null || sceneRobotTransform == null) return;
        if (!(warningBubbleRoot.parent is RectTransform parentRect)) return;

        var canvas = parentRect.GetComponentInParent<Canvas>();
        Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        var worldCam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (worldCam == null) return;

        Vector3 world = sceneRobotTransform.position + new Vector3(0f, 1.2f, 0f);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(worldCam, world);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, uiCam, out var local))
            warningBubbleRoot.anchoredPosition = local + new Vector2(0f, 20f);
    }

    private Transform FindSceneRobotTransform()
    {
        var active = SceneManager.GetActiveScene();
        var roots = active.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var all = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < all.Length; j++)
            {
                if (all[j].name != "Robot") continue;
                if (all[j].GetComponentInParent<PhoneSubwayFlowGate>() != null) continue;
                return all[j];
            }
        }
        return null;
    }

    private Transform GetBubbleCanvasTransform()
    {
        var runtimeCanvasGo = GameObject.Find("__RuntimeDialogueCanvas");
        if (runtimeCanvasGo != null)
        {
            var runtimeCanvas = runtimeCanvasGo.GetComponent<Canvas>();
            if (runtimeCanvas != null) return runtimeCanvas.transform;
        }

        var active = SceneManager.GetActiveScene();
        var roots = active.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var canvases = roots[i].GetComponentsInChildren<Canvas>(true);
            for (int j = 0; j < canvases.Length; j++)
                return canvases[j].transform;
        }
        return null;
    }

    private SpeechBubbleUI TryGetDialogBoxTemplateFromDialogueManager()
    {
        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm == null) return null;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var fi = typeof(DialogueManager).GetField("speechBubblePrefab", flags);
        if (fi == null) return null;

        return fi.GetValue(dm) as SpeechBubbleUI;
    }

    private void EnsureBubbleHasVisibleBackground(RectTransform bubbleRoot)
    {
        if (bubbleRoot == null) return;

        var images = bubbleRoot.GetComponentsInChildren<Image>(true);
        bool hasRenderableImage = false;
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].sprite != null && images[i].color.a > 0.01f)
            {
                hasRenderableImage = true;
                break;
            }
        }

        if (!hasRenderableImage)
        {
            var bg = bubbleRoot.Find("__AutoBubbleBG");
            if (bg == null)
            {
                var bgGo = new GameObject("__AutoBubbleBG", typeof(RectTransform), typeof(RawImage));
                bgGo.transform.SetParent(bubbleRoot, false);
                bgGo.transform.SetAsFirstSibling();

                var bgRect = bgGo.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;

                var raw = bgGo.GetComponent<RawImage>();
                raw.color = new Color(1f, 1f, 1f, 0.95f);
                raw.raycastTarget = false;
            }
        }
    }
}
