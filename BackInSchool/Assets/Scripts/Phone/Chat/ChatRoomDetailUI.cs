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
    [SerializeField] private int messageSpacing = 6;
    [SerializeField] private int contentBottomPadding = 96;

    [Header("Send")]
    [SerializeField] private Button btnSendNext;
    [SerializeField] private TMP_Text btnSendNextText;

    private bool pendingStart;
    private bool waitingTap = false;
    private bool queuedTap = false;

    private DialogueLine pendingLine = null;




    // === CSV 기반 매핑 ===
    // roomId -> conversationID (Conversations.csv의 Conversation_ID)
    // Inspector에서 연결하기 쉬운 간단 배열로 구현
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

    // ChatLineMeta.csv의 Order가 0-based인지 1-based인지 자동 감지
    // 현재 프로젝트의 DAY1_CHAT_M은 0..21로 구성되어 있어 0-based가 기본
    private bool metaOrderIsZeroBased = true;

    private void Awake()
    {
        EnsureScrollRectStableSettings();
        EnsureContentRootBinding();
        SanitizeChatContentRect();
        EnsureViewportIsValid();
        if (btnBack) btnBack.onClick.AddListener(OnBack);
        if (btnSendNext) btnSendNext.onClick.AddListener(OnSendNext);
        //ShowList();
    }

    private void OnDisable()
    {
        ResetViewDepth();

        if (scrollRoutine != null)
        {
            StopCoroutine(scrollRoutine);
            scrollRoutine = null;
        }
    }

    public void ResetViewDepth()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        waitingTap = false;
        queuedTap = false;
        pendingLine = null;

        var appMgr = FindAnyObjectByType<PhoneAppManager>();
        if (appMgr != null)
            appMgr.SetLocked(false);

        if (btnSendNext != null)
            btnSendNext.gameObject.SetActive(false);

        if (screenRoomList != null)
            screenRoomList.SetActive(true);
        if (screenRoomDetail != null)
            screenRoomDetail.SetActive(false);
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
        EnsureViewportIsValid();

        if (LocalizationManager.Instance == null)
        {
            Debug.LogError("[ChatRoomDetailUI] LocalizationManager.Instance가 없습니다. 채팅 내용을 불러올 수 없습니다. (roomId=" + roomId + ")");
            return;
        }

        if (contentRoot == null)
        {
            Debug.LogError("[ChatRoomDetailUI] contentRoot가 Inspector에 연결되지 않았습니다.");
            return;
        }

        if (msgOtherPrefab == null || msgMePrefab == null)
        {
            Debug.LogError("[ChatRoomDetailUI] msgOtherPrefab 또는 msgMePrefab이 Inspector에 연결되지 않았습니다.");
            return;
        }

        Debug.Log($"[ChatUI] this={gameObject.name} id={gameObject.GetInstanceID()} root={transform.root.name}");
        Debug.Log($"[ChatUI] detailRef={(screenRoomDetail ? screenRoomDetail.name : "null")} id={(screenRoomDetail ? screenRoomDetail.GetInstanceID() : -1)}");

        currentRoomId = roomId;

        // 읽음 처리
        if (ChatService.Instance != null)
            ChatService.Instance.MarkRoomRead(roomId);

        // 화면 상단 방 이름
        if (roomTitleText) roomTitleText.text = title;

        // roomId -> conversationID 매핑
        currentConversationId = ResolveConversationId(roomId);
        if (string.IsNullOrEmpty(currentConversationId))
        {
            Debug.LogWarning($"[ChatRoomDetailUI] roomId '{roomId}'에 매핑된 conversationID가 없습니다.");
            if (btnSendNext) btnSendNext.gameObject.SetActive(false);
            return;
        }

        // CSV에서 대화 불러오기
        currentLines = LocalizationManager.Instance.GetConversation(currentConversationId);
        if (currentLines == null || currentLines.Count == 0)
        {
            Debug.LogWarning($"[ChatRoomDetailUI] conversation '{currentConversationId}'의 내용이 비어 있습니다.");
            if (btnSendNext) btnSendNext.gameObject.SetActive(false);
            return;
        }

        // ChatLineMeta Order 기준 자동 감지
        // - Order=0이 존재하면 0-based로 간주
        // - 없으면 1-based로 간주
        if (LocalizationManager.Instance != null)
        {
            ChatLineMetaDef _tmp;
            metaOrderIsZeroBased = LocalizationManager.Instance.TryGetChatLineMeta(currentConversationId, 0, out _tmp);
        }

        // ChatService 세션 상태에서 진행 인덱스와 완료 여부 복원
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
            SpawnMessage(pastLine, isMePast, playSfx: false);
        }

        lineIndex = restoredIndex;

        // 이 방에 아직 진행할 내용이 남아 있는지 먼저 계산
        bool shouldContinue = (!isCompleted && lineIndex < currentLines.Count);

        var appMgr = FindAnyObjectByType<PhoneAppManager>();
        if (appMgr) appMgr.SetLocked(shouldContinue);

        // 버튼 기본 상태
        if (btnSendNext) btnSendNext.gameObject.SetActive(shouldContinue);
        if (btnSendNext) btnSendNext.interactable = true;

        RequestScrollToBottom();

        // 이미 완료했거나 끝까지 읽었다면 잠금을 풀고 버튼을 숨긴다.
        if (!shouldContinue)
        {
            // 끝까지 읽었지만 completed 플래그가 남지 않은 경우를 정리한다.
            if (ChatService.Instance != null && !isCompleted)
                ChatService.Instance.CompleteSession(currentConversationId);

            return;
        }


        // 세션 시작. 이미 존재하는 세션이면 ChatService 내부에서 무시한다.
        if (ChatService.Instance != null)
            ChatService.Instance.StartSession(currentConversationId);

        // 이어 읽어야 하는 부분부터 재생 시작
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PlayFromIndex());

        Debug.Log($"[ChatUI] list activeSelf={screenRoomList.activeSelf} activeInHierarchy={screenRoomList.activeInHierarchy}");
        Debug.Log($"[ChatUI] detail activeSelf={screenRoomDetail.activeSelf} activeInHierarchy={screenRoomDetail.activeInHierarchy}");
        Debug.Log($"[ChatUI] detail parent activeInHierarchy={screenRoomDetail.transform.parent.gameObject.activeInHierarchy}");

    }

    private int GetOrder(DialogueLine line)
    {
        // lineID: DAY1_CHAT_M_22 같은 형태에서 끝 숫자를 파싱
        if (!string.IsNullOrEmpty(line.lineID))
        {
            int us = line.lineID.LastIndexOf('_');
            if (us >= 0 && us + 1 < line.lineID.Length)
            {
                if (int.TryParse(line.lineID.Substring(us + 1), out int parsed))
                    return parsed;
            }
        }
        // fallback: 인덱스 기반(1-based)
        return lineIndex + 1;
    }

    private string ResolveConversationId(string roomId)
    {
        if (ChatService.Instance != null)
        {
            string nextSessionId = ChatService.Instance.GetNextSessionIdForRoom(roomId);
            if (!string.IsNullOrEmpty(nextSessionId))
                return nextSessionId;
        }

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

            // ChatLineMeta Order 기준(0-based/1-based)에 맞춰 조회
            int order = metaOrderIsZeroBased ? lineIndex : (lineIndex + 1);

            ChatLineMetaDef meta = null;
            bool hasMeta = (LocalizationManager.Instance != null &&
                            LocalizationManager.Instance.TryGetChatLineMeta(currentConversationId, order, out meta));

            bool waitTap = hasMeta && meta != null && meta.waitTap;
            float delay = (hasMeta && meta != null && meta.delay > 0f) ? meta.delay : 0.6f;

            // WaitTap이면 해당 줄을 보내기 버튼으로 출력하도록 멈춘다.
            if (waitTap)
            {
                if (queuedTap)
                {
                    queuedTap = false;
                    // 미리 클릭했다면 pendingLine 설정 없이 즉시 출력한다.
                    waitingTap = false;

                    SpawnMessage(line, isMe);
                    if (ChatService.Instance != null)
                        ChatService.Instance.AdvanceSession(currentConversationId);
                    lineIndex++;

                    continue; // 다음 라인 계속
                }

                waitingTap = true;
                pendingLine = line;

                if (btnSendNext) btnSendNext.interactable = true;
                if (btnSendNextText) btnSendNextText.text = isMe ? "Send" : "Next";

                yield break;
            }

            // 자동 진행 라인
            // 자동 진행 중 버튼을 눌러도 다음 탭으로 밀리지 않도록
            // 현재 딜레이만 스킵해서 즉시 출력한다.
            if (btnSendNext) btnSendNext.interactable = true;
            if (btnSendNextText) btnSendNextText.text = "...";

            // delay 동안 기다리되 버튼을 누르면 즉시 진행
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
        // 화면이 활성화된 다음 프레임부터 시작
        yield return null;

        // 여전히 비활성 상태라면 중단
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
        routine = StartCoroutine(PlayFromIndex()); // 다음 라인을 즉시 판단

    }



    private void SpawnMessage(DialogueLine line, bool isMe, bool playSfx = true)
    {
        EnsureContentRootBinding();

        if (contentRoot == null) { Debug.LogError("[ChatUI] contentRoot null"); return; }

        var prefab = isMe ? msgMePrefab : msgOtherPrefab;
        if (prefab == null) { Debug.LogError("[ChatUI] msg prefab null"); return; }

        string displayName = LocalizationManager.Instance.GetName(line.speakerID);
        string body = LocalizationManager.Instance.GetLine(line.lineID);
        var avatar = SpeakerAvatarProvider.GetAvatar(line.speakerID);

        var item = Instantiate(prefab, contentRoot);
        item.transform.SetParent(contentRoot, false); // 좌표 꼬임 방지
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

        if (playSfx)
            PhoneSystem.Instance?.PlayPhoneBlipSfx();
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
        // 세션 중에는 뒤로 가기 잠금
        if (ChatService.Instance != null && ChatService.Instance.HasActiveSession) return;
        ShowList();
    }

    private void ShowList()
    {
        if (screenRoomList) screenRoomList.SetActive(true);
        if (screenRoomDetail) screenRoomDetail.SetActive(false);

        // 메시지와 진행 상태는 유지한다. 같은 방으로 돌아오면 그대로 보인다.
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


