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

    [Header("Send")]
    [SerializeField] private Button btnSendNext;
    [SerializeField] private TMP_Text btnSendNextText;

    private bool pendingStart;
    private bool waitingTap = false;
    private bool queuedTap = false;

    private DialogueLine pendingLine = null;




    // === CSV 기반 매핑 ===
    // roomId -> conversationID (Conversations.csv의 Conversation_ID)
    // 일단 Inspector에서 넣기 쉽게 간단 배열로 구현
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

    // ChatLineMeta.csv의 Order가 0-based(0..N-1)인지 1-based(1..N)인지 자동 감지
    // (현재 프로젝트의 ChatLineMeta.csv는 DAY1_CHAT_M이 0..21로 작성되어 있어 0-based가 기본)
    private bool metaOrderIsZeroBased = true;

    private void Awake()
    {
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
        // TMP/레이아웃이 LateUpdate에서 크기를 다시 계산하는 경우가 있어
        // 한두 프레임 뒤에 스크롤을 "확정"해야 마지막 메시지가 안 가려진다.
        yield return null;
        Canvas.ForceUpdateCanvases();

        var rt = (scrollRect.content != null) ? scrollRect.content : (RectTransform)contentRoot;
        if (rt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        yield return null;
        Canvas.ForceUpdateCanvases();

        scrollRect.velocity = Vector2.zero;
        scrollRect.verticalNormalizedPosition = 0f;
        scrollRoutine = null;
    }

    private void Start()
    {
        ShowList(); // ✅ 한 번만 초기화
    }


    public void OpenRoom(string roomId, string title)
    {
        if (LocalizationManager.Instance == null)
        {
            Debug.LogError("[ChatRoomDetailUI] LocalizationManager.Instance 가 없습니다. 채팅 대화를 불러올 수 없습니다. (roomId=" + roomId + ")");
            return;
        }

        if (contentRoot == null)
        {
            Debug.LogError("[ChatRoomDetailUI] contentRoot 가 인스펙터에 연결되지 않았습니다.");
            return;
        }

        if (msgOtherPrefab == null || msgMePrefab == null)
        {
            Debug.LogError("[ChatRoomDetailUI] msgOtherPrefab 또는 msgMePrefab 이 인스펙터에 연결되지 않았습니다.");
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
            Debug.LogWarning($"[ChatRoomDetailUI] roomId '{roomId}'에 매핑된 conversationID가 없음");
            if (btnSendNext) btnSendNext.gameObject.SetActive(false);
            return;
        }

        // CSV에서 대화 불러오기
        currentLines = LocalizationManager.Instance.GetConversation(currentConversationId);
        if (currentLines == null || currentLines.Count == 0)
        {
            Debug.LogWarning($"[ChatRoomDetailUI] conversation '{currentConversationId}' 대사가 비어있음");
            if (btnSendNext) btnSendNext.gameObject.SetActive(false);
            return;
        }

        // ✅ ChatLineMeta Order base 자동 감지
        // - meta에: Order=0이 존재하면 0-based로 간주
        // - 없으면 1-based로 간주
        if (LocalizationManager.Instance != null)
        {
            ChatLineMetaDef _tmp;
            metaOrderIsZeroBased = LocalizationManager.Instance.TryGetChatLineMeta(currentConversationId, 0, out _tmp);
        }

        // ChatService 세션 상태에서 진행 인덱스/완료 여부 복원
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


        // 기존 메시지 영역 초기화 후, 이미 진행된 부분까지 다시 렌더링
        ClearMessages();
        for (int i = 0; i < restoredIndex && i < currentLines.Count; i++)
        {
            var pastLine = currentLines[i];
            bool isMePast = IsPlayerSpeaker(pastLine.speakerID);
            SpawnMessage(pastLine, isMePast);
        }

        lineIndex = restoredIndex;

        // ✅ 이 방에서 "아직 진행할 내용이 남아있는지" 먼저 계산
        bool shouldContinue = (!isCompleted && lineIndex < currentLines.Count);

        var appMgr = FindAnyObjectByType<PhoneAppManager>();
        if (appMgr) appMgr.SetLocked(shouldContinue);

        // 버튼 기본 상태
        if (btnSendNext) btnSendNext.gameObject.SetActive(shouldContinue);
        if (btnSendNext) btnSendNext.interactable = true;

        // ✅ 화면 전환은 맨 마지막에
        if (screenRoomList) screenRoomList.SetActive(false);
        if (screenRoomDetail) screenRoomDetail.SetActive(true);

        // ✅ 방을 열 때/기존 메시지 리렌더 후 항상 맨 아래로
        RequestScrollToBottom();

        // ✅ 완료(또는 이미 끝까지 봄)라면 여기서 끝. 잠금은 이미 shouldContinue=false로 꺼짐.
        if (!shouldContinue)
        {
            // 혹시 progressIndex가 끝까지 갔는데 completed 플래그가 안 찍힌 경우도 정리
            if (ChatService.Instance != null && !isCompleted)
                ChatService.Instance.CompleteSession(currentConversationId);

            return;
        }


        // 세션 시작 (이미 존재하는 세션이면 StartSession 내부에서 무시)
        if (ChatService.Instance != null)
            ChatService.Instance.StartSession(currentConversationId);

        // ✅ 여기서 남은 부분부터 재생 시작
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PlayFromIndex());

        Debug.Log($"[ChatUI] list activeSelf={screenRoomList.activeSelf} activeInHierarchy={screenRoomList.activeInHierarchy}");
        Debug.Log($"[ChatUI] detail activeSelf={screenRoomDetail.activeSelf} activeInHierarchy={screenRoomDetail.activeInHierarchy}");
        Debug.Log($"[ChatUI] detail parent activeInHierarchy={screenRoomDetail.transform.parent.gameObject.activeInHierarchy}");

    }

    private int GetOrder(DialogueLine line)
    {
        // lineID: DAY1_CHAT_M_22 같은 형태에서 끝 숫자 파싱
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

            // ✅ ChatLineMeta Order 기준(0-based/1-based)에 맞춰 조회
            int order = metaOrderIsZeroBased ? lineIndex : (lineIndex + 1);

            ChatLineMetaDef meta = null;
            bool hasMeta = (LocalizationManager.Instance != null &&
                            LocalizationManager.Instance.TryGetChatLineMeta(currentConversationId, order, out meta));

            bool waitTap = hasMeta && meta != null && meta.waitTap;
            float delay = (hasMeta && meta != null && meta.delay > 0f) ? meta.delay : 0.6f;

            // ✅ WaitTap이면 "이 줄을 보내기 버튼으로 출력"하도록 멈춤
            if (waitTap)
            {
                if (queuedTap)
                {
                    queuedTap = false;
                    // 예약된 클릭이 있으면 즉시 보내버리기
                    // (pendingLine 세팅 후 바로 Spawn)
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
                if (btnSendNextText) btnSendNextText.text = isMe ? "보내기" : "다음";

                yield break;
            }

            // 자동 진행 라인
            // ✅ 자동 진행 중에도 버튼을 눌렀을 때 "다음 줄"로 밀리지 않게,
            //    현재 딜레이를 스킵(=즉시 출력)하도록 처리한다.
            if (btnSendNext) btnSendNext.interactable = true;
            if (btnSendNextText) btnSendNextText.text = "…";

            // delay 동안 대기하되, 유저가 버튼을 누르면 queuedTap을 소비하고 즉시 진행
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
        // 화면이 활성화된 상태로 한 프레임 넘어간 뒤 시작
        yield return null;

        // 그래도 혹시 비활성이라면 한 번 더 방어
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
        routine = StartCoroutine(PlayFromIndex()); // ✅ 한 프레임 쉬지 말고 즉시 다음 판단

    }



    private void SpawnMessage(DialogueLine line, bool isMe)
    {
        if (contentRoot == null) { Debug.LogError("[ChatUI] contentRoot null"); return; }

        var prefab = isMe ? msgMePrefab : msgOtherPrefab;
        if (prefab == null) { Debug.LogError("[ChatUI] msg prefab null"); return; }

        string displayName = LocalizationManager.Instance.GetName(line.speakerID);
        string body = LocalizationManager.Instance.GetLine(line.lineID);
        var avatar = SpeakerAvatarProvider.GetAvatar(line.speakerID);

        var item = Instantiate(prefab, contentRoot);
        item.transform.SetParent(contentRoot, false); // ✅ 좌표 꼬임 방지
        item.Set(displayName, avatar, body, true);

        // ✅ 레이아웃 갱신 + 맨 아래로 스크롤
        // (TMP 높이 계산이 LateUpdate 이후 확정되는 경우가 있어서 한 프레임 뒤에 스크롤 확정)
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
        // 세션 중이면 못 나감
        if (ChatService.Instance != null && ChatService.Instance.HasActiveSession) return;
        ShowList();
    }

    private void ShowList()
    {
        if (screenRoomList) screenRoomList.SetActive(true);
        if (screenRoomDetail) screenRoomDetail.SetActive(false);

        // 메시지/진행 상태는 유지 (같은 방으로 돌아왔을 때 그대로 보이도록)
        if (routine != null) StopCoroutine(routine);
        routine = null;

        if (btnSendNext) btnSendNext.gameObject.SetActive(false);
    }

    private void ClearMessages()
    {
        if (contentRoot == null) return;
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);
    }
}
