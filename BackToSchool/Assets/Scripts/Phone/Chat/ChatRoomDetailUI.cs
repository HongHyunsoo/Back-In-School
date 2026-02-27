using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatRoomDetailUI : MonoBehaviour
{
  

    [Header("Screens")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private GameObject screenRoomList;
    [SerializeField] private GameObject screenRoomDetail;

    [Header("Top")]
    [SerializeField] private Button btnBack;
    [SerializeField] private TMP_Text roomTitleText;

    [Header("Messages")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private ChatMessageItem msgOtherPrefab;
    [SerializeField] private ChatMessageItem msgMePrefab;
    [SerializeField] private float bottomSafeScrollOffset = 22f;
    [SerializeField] private int messageSpacing = 6;
    [SerializeField] private int contentHorizontalPadding = 0;
    [SerializeField] private int contentBottomPadding = 96;

    [Header("Send")]
    [SerializeField] private Button btnSendNext;
    [SerializeField] private TMP_Text btnSendNextText;

    private bool pendingStart;
    private bool waitingTap = false;
    private bool queuedTap = false;

    private DialogueLine pendingLine = null;




    // === CSV 기반 매핑 ===
    // roomId -> conversationID (Conversations.csv??Conversation_ID)
    // ?�단 Inspector?�서 ?�기 ?�게 간단 배열�?구현
    [System.Serializable]
    public class RoomConversationMap
    {
        public string roomId;
        public string conversationId;
    }
    [Header("CSV Conversation Mapping")]
    [SerializeField] private RoomConversationMap[] roomConversationMaps;

    private string currentRoomId;
    private string currentConversationId;

    private List<DialogueLine> currentLines;
    private int lineIndex;
    private Coroutine routine;
    private Coroutine scrollRoutine;

    // ChatLineMeta.csv??Order가 0-based(0..N-1)?��? 1-based(1..N)?��? ?�동 감�?
    // (?�재 ?�로?�트??ChatLineMeta.csv??DAY1_CHAT_M??0..21�??�성?�어 ?�어 0-based가 기본)
    private bool metaOrderIsZeroBased = true;

    private void Awake()
    {
        EnsureScrollRectStableSettings();
        EnsureContentRootBinding();
        SanitizeChatContentRect();
        NormalizeContentRectForScroll();
        EnsureViewportIsValid();
        if (btnBack) btnBack.onClick.AddListener(OnBack);
        if (btnSendNext) btnSendNext.onClick.AddListener(OnSendNext);
        //ShowList();
    }

    private void OnDisable()
    {
        if (scrollRoutine != null)
        {
            StopCoroutine(scrollRoutine);
            scrollRoutine = null;
        }
    }

    private void RequestScrollToBottom()
    {
        if (scrollRect == null) return;
        if (scrollRoutine != null) StopCoroutine(scrollRoutine);
        scrollRoutine = StartCoroutine(CoScrollToBottom());
    }

    private IEnumerator CoScrollToBottom()
    {
        yield return null;
        yield return null;
        ForceScrollToLatestMessage();
        scrollRoutine = null;
    }

    private void Start()
    {
        ShowList();
    }


    public void OpenRoom(string roomId, string title)
    {
        EnsureScrollRectStableSettings();
        EnsureContentRootBinding();
        SanitizeChatContentRect();
        NormalizeContentRectForScroll();
        EnsureViewportIsValid();

        if (LocalizationManager.Instance == null)
        {
            Debug.LogError("[ChatRoomDetailUI] LocalizationManager.Instance 가 ?�습?�다. 채팅 ?�?��? 불러?????�습?�다. (roomId=" + roomId + ")");
            return;
        }

        if (contentRoot == null)
        {
            Debug.LogError("[ChatRoomDetailUI] contentRoot 가 ?�스?�터???�결?��? ?�았?�니??");
            return;
        }

        if (msgOtherPrefab == null || msgMePrefab == null)
        {
            Debug.LogError("[ChatRoomDetailUI] msgOtherPrefab ?�는 msgMePrefab ???�스?�터???�결?��? ?�았?�니??");
            return;
        }

        Debug.Log($"[ChatUI] this={gameObject.name} id={gameObject.GetInstanceID()} root={transform.root.name}");
        Debug.Log($"[ChatUI] detailRef={(screenRoomDetail ? screenRoomDetail.name : "null")} id={(screenRoomDetail ? screenRoomDetail.GetInstanceID() : -1)}");

        currentRoomId = roomId;

        // ?�음 처리
        if (ChatService.Instance != null)
            ChatService.Instance.MarkRoomRead(roomId);

        // ?�면 ?�단 �??�름
        if (roomTitleText) roomTitleText.text = title;

        // roomId -> conversationID 매핑
        currentConversationId = ResolveConversationId(roomId);
        if (string.IsNullOrEmpty(currentConversationId))
        {
            Debug.LogWarning($"[ChatRoomDetailUI] roomId '{roomId}'??매핑??conversationID가 ?�음");
            if (btnSendNext) btnSendNext.gameObject.SetActive(false);
            return;
        }

        // CSV?�서 ?�??불러?�기
        currentLines = LocalizationManager.Instance.GetConversation(currentConversationId);
        if (currentLines == null || currentLines.Count == 0)
        {
            Debug.LogWarning($"[ChatRoomDetailUI] conversation '{currentConversationId}' ?�?��? 비어?�음");
            if (btnSendNext) btnSendNext.gameObject.SetActive(false);
            return;
        }

        // ??ChatLineMeta Order base ?�동 감�?
        // - meta?? Order=0??존재?�면 0-based�?간주
        // - ?�으�?1-based�?간주
        if (LocalizationManager.Instance != null)
        {
            ChatLineMetaDef _tmp;
            metaOrderIsZeroBased = LocalizationManager.Instance.TryGetChatLineMeta(currentConversationId, 0, out _tmp);
        }

        // ChatService ?�션 ?�태?�서 진행 ?�덱???�료 ?��? 복원
        int restoredIndex = 0;
        bool isCompleted = false;
        ChatSessionState st = null;
        if (ChatService.Instance != null)
        {
           ChatService.Instance.EnsureSession(currentConversationId, currentRoomId);


            st = ChatService.Instance.GetSession(currentConversationId);
            if (st != null)
            {
                restoredIndex = Mathf.Clamp(st.progressIndex, 0, currentLines.Count);
                isCompleted = st.completed;
            }
        }


        // Activate detail first so message layout is calculated in active hierarchy.
        if (screenRoomList) screenRoomList.SetActive(false);
        if (screenRoomDetail) screenRoomDetail.SetActive(true);

        ClearMessages();
        for (int i = 0; i < restoredIndex && i < currentLines.Count; i++)
        {
            var pastLine = currentLines[i];
            bool isMePast = IsPlayerSpeaker(pastLine.speakerID);
            SpawnMessage(pastLine, isMePast);
        }

        lineIndex = restoredIndex;

        // ????방에??"?�직 진행???�용???�아?�는지" 먼�? 계산
        bool shouldContinue = (!isCompleted && lineIndex < currentLines.Count);

        var appMgr = FindAnyObjectByType<PhoneAppManager>();
        if (appMgr) appMgr.SetLocked(shouldContinue);

        // 버튼 기본 ?�태
        if (btnSendNext) btnSendNext.gameObject.SetActive(shouldContinue);
        if (btnSendNext) btnSendNext.interactable = true;

        RequestScrollToBottom();

        // ???�료(?�는 ?��? ?�까지 �??�면 ?�기???? ?�금?� ?��? shouldContinue=false�?꺼짐.
        if (!shouldContinue)
        {
            // ?�시 progressIndex가 ?�까지 갔는??completed ?�래그�? ??찍힌 경우???�리
            if (ChatService.Instance != null && !isCompleted)
                ChatService.Instance.CompleteSession(currentConversationId);

            return;
        }


        // ?�션 ?�작 (?��? 존재?�는 ?�션?�면 StartSession ?��??�서 무시)
        if (ChatService.Instance != null)
            ChatService.Instance.StartSession(currentConversationId);

        // ???�기???��? 부분�????�생 ?�작
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PlayFromIndex());

        Debug.Log($"[ChatUI] list activeSelf={screenRoomList.activeSelf} activeInHierarchy={screenRoomList.activeInHierarchy}");
        Debug.Log($"[ChatUI] detail activeSelf={screenRoomDetail.activeSelf} activeInHierarchy={screenRoomDetail.activeInHierarchy}");
        Debug.Log($"[ChatUI] detail parent activeInHierarchy={screenRoomDetail.transform.parent.gameObject.activeInHierarchy}");

    }

    private int GetOrder(DialogueLine line)
    {
        // lineID: DAY1_CHAT_M_22 같�? ?�태?�서 ???�자 ?�싱
        if (!string.IsNullOrEmpty(line.lineID))
        {
            int us = line.lineID.LastIndexOf('_');
            if (us >= 0 && us + 1 < line.lineID.Length)
            {
                if (int.TryParse(line.lineID.Substring(us + 1), out int parsed))
                    return parsed;
            }
        }
        // fallback: ?�덱??기반(1-based)
        return lineIndex + 1;
    }

    private string ResolveConversationId(string roomId)
    {
        if (roomConversationMaps == null) return null;
        for (int i = 0; i < roomConversationMaps.Length; i++)
        {
            if (roomConversationMaps[i] != null && roomConversationMaps[i].roomId == roomId)
                return roomConversationMaps[i].conversationId;
        }
        return null;
    }

    IEnumerator PlayFromIndex()
    {
        waitingTap = false;
        pendingLine = null;

        while (currentLines != null && lineIndex < currentLines.Count)
        {
            var line = currentLines[lineIndex];
            bool isMe = IsPlayerSpeaker(line.speakerID);

            // ??ChatLineMeta Order 기�?(0-based/1-based)??맞춰 조회
            int order = metaOrderIsZeroBased ? lineIndex : (lineIndex + 1);

            ChatLineMetaDef meta = null;
            bool hasMeta = (LocalizationManager.Instance != null &&
                            LocalizationManager.Instance.TryGetChatLineMeta(currentConversationId, order, out meta));

            bool waitTap = hasMeta && meta != null && meta.waitTap;
            float delay = (hasMeta && meta != null && meta.delay > 0f) ? meta.delay : 0.6f;

            // ??WaitTap?�면 "??줄을 보내�?버튼?�로 출력"?�도�?멈춤
            if (waitTap)
            {
                if (queuedTap)
                {
                    queuedTap = false;
                    // ?�약???�릭???�으�?즉시 보내버리�?                    // (pendingLine ?�팅 ??바로 Spawn)
                    waitingTap = false;

                    SpawnMessage(line, isMe);
                    if (ChatService.Instance != null)
                        ChatService.Instance.AdvanceSession(currentConversationId);
                    lineIndex++;

                    continue; // ?�음 ?�인 계속
                }

                waitingTap = true;
                pendingLine = line;

                if (btnSendNext) btnSendNext.interactable = true;
                if (btnSendNextText) btnSendNextText.text = isMe ? "Send" : "Next";

                yield break;
            }

            // ?�동 진행 ?�인
            // ???�동 진행 중에??버튼???��?????"?�음 �?�?밀리�? ?�게,
            //    ?�재 ?�레?��? ?�킵(=즉시 출력)?�도�?처리?�다.
            if (btnSendNext) btnSendNext.interactable = true;
            if (btnSendNextText) btnSendNextText.text = "...";

            // delay ?�안 ?�기하?? ?��?가 버튼???�르�?queuedTap???�비?�고 즉시 진행
            float t = 0f;
            while (t < delay)
            {
                if (queuedTap)
                {
                    queuedTap = false;
                    break;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            SpawnMessage(line, isMe);

            if (ChatService.Instance != null)
                ChatService.Instance.AdvanceSession(currentConversationId);

            lineIndex++;
        }

        EndSession();
    }



    private IEnumerator DeferredStart()
    {
        // ?�면???�성?�된 ?�태�????�레???�어�????�작
        yield return null;

        // 그래???�시 비활?�이?�면 ??�???방어
        if (!gameObject.activeInHierarchy)
            yield break;

        yield return PlayFromIndex();
    }


    private bool IsPlayerSpeaker(string speakerId)
    {
        return speakerId == "PLAYER" || speakerId == "NAME_PLAYER";
    }

    private void OnSendNext()
    {


        if (!waitingTap)
        {
            queuedTap = true;
            return;
        }
        if (pendingLine == null) return;

        waitingTap = false;

        bool isMe = IsPlayerSpeaker(pendingLine.speakerID);

        SpawnMessage(pendingLine, isMe);

        if (ChatService.Instance != null)
            ChatService.Instance.AdvanceSession(currentConversationId);

        lineIndex++;
        pendingLine = null;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PlayFromIndex()); // ?????�레???��? 말고 즉시 ?�음 ?�단

    }



    private void SpawnMessage(DialogueLine line, bool isMe)
    {
        EnsureContentRootBinding();
        NormalizeContentRectForScroll();

        if (contentRoot == null) { Debug.LogError("[ChatUI] contentRoot null"); return; }

        var prefab = isMe ? msgMePrefab : msgOtherPrefab;
        if (prefab == null) { Debug.LogError("[ChatUI] msg prefab null"); return; }

        string displayName = LocalizationManager.Instance.GetName(line.speakerID);
        string body = LocalizationManager.Instance.GetLine(line.lineID);
        var avatar = SpeakerAvatarProvider.GetAvatar(line.speakerID);

        var item = Instantiate(prefab, contentRoot);
        item.transform.SetParent(contentRoot, false); // ??좌표 꼬임 방�?
        item.Set(displayName, avatar, body, true);
        ForceLeftAlignTexts(item.transform);
        EnsureMessageItemVisibleHeight(item);
        Debug.Log($"[ChatUI] Spawned msg under={contentRoot.name} childCount={contentRoot.childCount} line={line.lineID}");


        Canvas.ForceUpdateCanvases();
        var itemRect = item.transform as RectTransform;
        if (itemRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(itemRect);
        var contentRect = contentRoot as RectTransform;
        if (contentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        // Force layout before scrolling to bottom.
        RequestScrollToBottom();
    }





    private void EndSession()
    {
        if (ChatService.Instance != null && !string.IsNullOrEmpty(currentConversationId))
            ChatService.Instance.CompleteSession(currentConversationId);

        currentConversationId = null;
        currentLines = null;

        var appMgr = FindAnyObjectByType<PhoneAppManager>();
        if (appMgr) appMgr.SetLocked(false);

        if (btnSendNext) btnSendNext.gameObject.SetActive(false);
    }

    private void OnBack()
    {
        // ?�션 중이�?�??�감
        if (ChatService.Instance != null && ChatService.Instance.HasActiveSession) return;
        ShowList();
    }

    private void ShowList()
    {
        if (screenRoomList) screenRoomList.SetActive(true);
        if (screenRoomDetail) screenRoomDetail.SetActive(false);

        // 메시지/진행 ?�태???��? (같�? 방으�??�아?�을 ??그�?�?보이?�록)
        if (routine != null) StopCoroutine(routine);
        routine = null;

        if (btnSendNext) btnSendNext.gameObject.SetActive(false);
    }

    private void ClearMessages()
    {
        if (contentRoot == null) return;
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        if (scrollRect != null && scrollRect.content != null)
            scrollRect.content.anchoredPosition = Vector2.zero;
    }

    private void EnsureViewportIsValid()
    {
        if (scrollRect == null)
            return;

        if (scrollRect.viewport != null)
            return;

        if (contentRoot == null)
            return;

        Transform t = contentRoot;
        while (t != null)
        {
            var rt = t as RectTransform;
            if (rt != null && (t.GetComponent<RectMask2D>() != null || t.GetComponent<Mask>() != null))
            {
                scrollRect.viewport = rt;
                return;
            }

            t = t.parent;
        }
    }
    private void ForceScrollToLatestMessage()
    {
        if (scrollRect == null || contentRoot == null)
            return;

        RectTransform content = contentRoot as RectTransform;
        if (content == null)
            return;

        // Always pin to assigned message content to avoid runtime mismatch.
        if (scrollRect.content != content)
            scrollRect.content = content;

        SanitizeChatContentRect();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();

        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 0f;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
        scrollRect.velocity = Vector2.zero;
    }

    private void EnsureContentRootBinding()
    {
        if (contentRoot == null)
        {
            if (scrollRect != null && scrollRect.content != null)
                contentRoot = scrollRect.content.transform;
            return;
        }

        if (scrollRect != null && contentRoot is RectTransform contentRt && scrollRect.content != contentRt)
            scrollRect.content = contentRt;
    }

    private void SanitizeChatContentRect()
    {
        if (!(contentRoot is RectTransform content))
            return;

        if (scrollRect != null && scrollRect.viewport != null)
        {
            RectTransform vp = scrollRect.viewport;
            // Broken prefab case: viewport is zero-sized at bottom-left.
            if (vp.rect.width < 10f || vp.rect.height < 10f ||
                vp.anchorMin != Vector2.zero || vp.anchorMax != Vector2.one)
            {
                vp.anchorMin = Vector2.zero;
                vp.anchorMax = Vector2.one;
                vp.pivot = new Vector2(0.5f, 0.5f);
                vp.anchoredPosition = Vector2.zero;
                vp.sizeDelta = Vector2.zero;
                vp.localScale = Vector3.one;
            }
        }

        // Chat content should be top-aligned stretch on X.
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);

        // Recover from corrupted prefab/content offsets that push all messages out of viewport.
        Vector2 ap = content.anchoredPosition;
        if (Mathf.Abs(ap.y) > 10f || Mathf.Abs(ap.x) > 2f)
            content.anchoredPosition = new Vector2(0f, 0f);
        else
            content.anchoredPosition = new Vector2(0f, 0f);

        if (Mathf.Abs(content.sizeDelta.x) > 2f)
            content.sizeDelta = new Vector2(0f, content.sizeDelta.y);

        if (content.localScale != Vector3.one)
            content.localScale = Vector3.one;

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.spacing = messageSpacing;
            var p = vlg.padding ?? new RectOffset();
            p.left = 0;
            p.right = 0;
            p.bottom = contentBottomPadding;
            vlg.padding = p;
        }
    }

    private void NormalizeContentRectForScroll()
    {
        // Keep prefab-authored anchors/pivot and layout untouched.
    }

    private void EnsureScrollRectStableSettings()
    {
        if (scrollRect == null)
            return;

        scrollRect.horizontal = false;
        if (scrollRect.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport)
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private void EnsureMessageItemVisibleHeight(ChatMessageItem item)
    {
        if (item == null)
            return;

        RectTransform rt = item.transform as RectTransform;
        if (rt == null)
            return;

        var layoutElement = item.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = item.gameObject.AddComponent<LayoutElement>();

        // Msg prefab root height can be 0 in this project; enforce a visible height for VLG.
        float preferred = 72f;
        var texts = item.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            var t = texts[i];
            if (t == null || string.IsNullOrEmpty(t.text))
                continue;

            float width = 320f;
            RectTransform textRt = t.transform as RectTransform;
            if (textRt != null && textRt.rect.width > 20f)
                width = textRt.rect.width;

            preferred += t.GetPreferredValues(t.text, width, 0f).y;
        }

        preferred = Mathf.Clamp(preferred, 72f, 320f);
        layoutElement.minHeight = preferred;
        layoutElement.preferredHeight = preferred;
        layoutElement.flexibleHeight = 0f;

        if (rt.sizeDelta.y < 1f)
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, preferred);
    }

    private void ForceLeftAlignTexts(Transform root)
    {
        if (root == null)
            return;

        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null)
                continue;

            if (texts[i].name.IndexOf("Name", System.StringComparison.OrdinalIgnoreCase) >= 0)
                texts[i].alignment = TextAlignmentOptions.Left;
            else
                texts[i].alignment = TextAlignmentOptions.TopLeft;
        }
    }

}


