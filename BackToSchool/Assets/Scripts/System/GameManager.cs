using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
 * ===================================================================================
 * GameManager (v1.4 - '占쎈본 ID' 占쏙옙占쏙옙 占쏙옙占?
 * ===================================================================================
 * - [v1.4 占쏙옙占쏙옙占쏙옙]
 * - 1. (v1.3) CSV 占싸듸옙 占쏙옙占쏙옙 占쏙옙占?占쏙옙占쏙옙 (LocalizationManager占쏙옙 Awake占쏙옙占쏙옙 占싯아쇽옙 占쏙옙)
 * - 2. (占신깍옙) ChangeState()占쏙옙 '占쏙옙占썰리 占쏙옙' 占쏙옙占승곤옙 占실몌옙,
 * DialogueManager.StartDialogue()占쏙옙 '占쎈본 ID'占쏙옙 占쏙옙占쏙옙 호占쏙옙
 * ===================================================================================
 */
public class GameManager : MonoBehaviour
{
    // ... (v1.3占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙) ...
    public int currentDay = 1;
    public GameState currentState;
    public PlayerController playerController;
    public DialogueManager dialogueManager;



    private IEnumerator Start()
    {
        // 1) inspector 誘명븷?뱀씠硫??먮룞?쇰줈 李얘린 (吏?섏쿋 ?ъ뿉?쒕뒗 ?놁쓣 ???덉쓬 - ?뺤긽)
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        if (dialogueManager == null)
            dialogueManager = FindAnyObjectByType<DialogueManager>();

        // 2) LocalizationManager 以鍮꾨맆 ?뚭퉴吏 ?湲?(理쒕? 2珥??뺣룄)
        float t = 0f;
        while (LocalizationManager.Instance == null && t < 2f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (LocalizationManager.Instance == null)
        {
            Debug.LogError("[GameManager] LocalizationManager.Instance媛 以鍮꾨릺吏 ?딆븯?듬땲?? ?곹깭 ?꾪솚??嫄대꼫?곷땲??");
            yield break;
        }

        // 3) ?곹깭 吏꾩엯
        ForceStateByScene(SceneManager.GetActiveScene().name);
        ChangeState(currentState);

    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ??諛붾??뚮쭏???곹깭 媛뺤젣 ?명똿
        ForceStateByScene(scene.name);
        ChangeState(currentState);

        // (以묒슂) ??諛붾뚮㈃ DialogueManager媛 ???ㅻ툕?앺듃 ?ㅼ떆 ?≪븘????
        if (dialogueManager == null) dialogueManager = FindAnyObjectByType<DialogueManager>();
        if (dialogueManager != null)
        {
            dialogueManager.RebindForScene(); // ?꾨옒 2踰덉뿉??DialogueManager??異붽????⑥닔
        }
    }

    private void ForceStateByScene(string sceneName)
    {
        switch (sceneName)
        {
            case "CHAT":
                currentState = GameState.Subway;
                break;

            case "FREEROAM":
                currentState = ResolveFreeRoamStateByFlowContext();
                break;

            case "STORY":
                // 스토리 씬은 별도 이벤트에서 상태를 명시적으로 전환한다.
                break;

            case "MINIGAME":
                // 미니게임 씬은 개별 컨트롤러가 상태를 사용한다.
                break;

            // Legacy scene aliases
            case "SubwayScene":
                currentState = GameState.Subway;
                break;

            case "SchoolFreeTime":
                currentState = ResolveFreeRoamStateByFlowContext();
                break;
        }
    }

    private GameState ResolveFreeRoamStateByFlowContext()
    {
        string flowId = PlayerPrefs.GetString("FLOW_ID", "");
        if (!string.IsNullOrEmpty(flowId))
        {
            string idUpper = flowId.ToUpperInvariant();
            if (idUpper.Contains("AFTERSCHOOL"))
                return GameState.AfterSchool;
            if (idUpper.Contains("BEFORE_ASSEMBLY"))
                return GameState.Morning_Slippers;
            if (idUpper.Contains("LUNCH"))
                return GameState.Lunch_FreeTime;
            if (idUpper.Contains("DAY5") && idUpper.Contains("FREEROAM"))
                return GameState.Day5_FreeTime;
        }

        if (currentState == GameState.Morning_Slippers ||
            currentState == GameState.Lunch_FreeTime ||
            currentState == GameState.AfterSchool ||
            currentState == GameState.Day5_FreeTime)
        {
            return currentState;
        }

        return GameState.Lunch_FreeTime;
    }



    // =============================================================
    // (v1.4) 占쏙옙占쏙옙 占쏙옙占승몌옙 占쏙옙占쏙옙占싹댐옙 占쌕쏙옙 占쌉쇽옙 (占쏙옙占쏙옙占쏙옙)
    // =============================================================
    public void ChangeState(GameState newState)
    {
        currentState = newState;

        // PlayerController? DialogueManager??留??ъ뿉?쒕쭔 ?꾩슂 (吏?섏쿋 ?ъ뿉?쒕뒗 ?좏깮?ы빆)
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        if (dialogueManager == null) dialogueManager = FindAnyObjectByType<DialogueManager>();

        // Subway ?곹깭媛 ?꾨땺 ?뚮쭔 寃쎄퀬 (吏?섏쿋 ?ъ뿉?쒕뒗 ?뺤긽?곸쑝濡??놁쓣 ???덉쓬)
        if (newState != GameState.Subway)
        {
            if (playerController == null)
            {
                Debug.LogWarning("[GameManager] PlayerController瑜?李얠쓣 ???놁뒿?덈떎. (?곹깭: " + newState.ToString() + ")");
            }

            if (dialogueManager == null)
            {
                Debug.LogWarning("[GameManager] DialogueManager瑜?李얠쓣 ???놁뒿?덈떎. (?곹깭: " + newState.ToString() + ")");
            }
        }

        UnityEngine.Debug.Log("?덈줈???곹깭濡??꾪솚: " + newState.ToString());

        switch (currentState)
        {
            // --- 1~4?쇱감 ?꾩묠 ---
            // 吏?섏쿋 ?? ????梨꾪똿留??ъ슜, DialogueManager 留먰뭾???ъ슜 ????
            case GameState.Subway:
                // PlayerController媛 ?덉쑝硫?鍮꾪솢?깊솕 (吏?섏쿋?먯꽌???대룞 遺덇?)
                if (playerController != null) 
                {
                    playerController.enabled = false;
                }
                // DialogueManager??吏?섏쿋 ?ъ뿉???ъ슜 ????(梨꾪똿? ???깆쑝濡쒕쭔)
                // ChatService.ActivateSegmentsFor()媛 ?먮룞?쇰줈 梨꾪똿 ?멸렇癒쇳듃瑜??쒖꽦?뷀븿
                if (ChatService.Instance != null)
                {
                    ChatService.Instance.ActivateSegmentsFor(currentDay, GameState.Subway);
                }
                
                break;


            case GameState.Morning_Slippers:
                if (playerController != null)
                    playerController.enabled = true; // 占실놂옙화 占쏙옙占싣쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙
                else
                    Debug.LogWarning("[GameManager] playerController媛 null (Morning_Slippers)");
                // (占쏙옙占쌩울옙) '占실놂옙화' 트占쏙옙占신울옙 占쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙占승뤄옙
                break;

            case GameState.Morning_Assembly:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Morning_Assembly)");
                // (占쏙옙占썰리 占쏙옙) 占쏙옙짜占쏙옙 占승댐옙 占쏙옙占쏙옙 占쎈본 ID 占쏙옙占쏙옙 占쏙옙占?
                if (dialogueManager != null)
                    dialogueManager.StartDialogue("ASSEMBLY_DAY" + currentDay, null); // 占쏙옙: "ASSEMBLY_DAY1"
                else
                    Debug.LogWarning("[GameManager] dialogueManager媛 null (Morning_Assembly StartDialogue 紐삵븿)");
                break;

            // --- 1~4占쏙옙占쏙옙 占쏙옙틴 ---
            case GameState.Class_Intro_1:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Class_Intro_1)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLASS1_INTRO_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager媛 null (Class_Intro_1 StartDialogue 紐삵븿)");
                break;
            case GameState.Class_Minigame_1:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Class_Minigame_1)");
                // (占쏙옙占쌩울옙) '占쏙옙占쏙옙 占싱니곤옙占쏙옙 1' 占신댐옙占쏙옙 활占쏙옙화
                break;
            case GameState.Class_Outro_1:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Class_Outro_1)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLASS1_OUTRO_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager媛 null (Class_Outro_1 StartDialogue 紐삵븿)");
                break;

            case GameState.Lunch_Run:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Lunch_Run)");
                // (占쏙옙占쌩울옙) '占쌨식쏙옙 占쌨몌옙占쏙옙' 占싱니곤옙占쏙옙 활占쏙옙화
                break;
            case GameState.Lunch_Tetris:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Lunch_Tetris)");
                // (占쏙옙占쌩울옙) '占쏙옙占쏙옙 占쏙옙트占쏙옙占쏙옙' 占싱니곤옙占쏙옙 활占쏙옙화
                break;
            case GameState.Lunch_FreeTime:
                if (playerController != null)
                    playerController.enabled = true; // 占쏙옙占쏙옙 占시곤옙
                else
                    Debug.LogWarning("[GameManager] playerController媛 null (Lunch_FreeTime)");
                break;

            case GameState.Class_Intro_2:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Class_Intro_2)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLASS2_INTRO_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager媛 null (Class_Intro_2 StartDialogue 紐삵븿)");
                break;
            case GameState.Class_Minigame_2:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Class_Minigame_2)");
                // (占쏙옙占쌩울옙) '占쏙옙占쏙옙 占싱니곤옙占쏙옙 2' 占신댐옙占쏙옙 활占쏙옙화
                break;
            case GameState.Class_Outro_2:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Class_Outro_2)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLASS2_OUTRO_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager媛 null (Class_Outro_2 StartDialogue 紐삵븿)");
                break;

            case GameState.Closing_Assembly:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Closing_Assembly)");

                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLOSING_DAY" + currentDay, null);
                else
                    Debug.LogWarning("[GameManager] dialogueManager媛 null (Closing_Assembly StartDialogue 紐삵븿)");
                break;
            case GameState.AfterSchool:
                if (playerController != null)
                    playerController.enabled = true; // 占쏙옙占쏙옙 占시곤옙
                else
                    Debug.LogWarning("[GameManager] playerController媛 null (AfterSchool)");
                break;
            case GameState.GoHome:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (GoHome)");
                currentDay++; // 占쏙옙짜 +1
                ChangeState(GameState.Subway); // (占쌈쏙옙)
                // (占쏙옙占쌩울옙) SceneManager.LoadScene("SubwayScene");
                break;

            // --- 5?쇱감 (?밸퀎) ?섏뾽 ---
            case GameState.Day5_BigCleaning:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Day5_BigCleaning)");
                // (異뷀썑) '?泥?냼' 誘몃땲寃뚯엫 ?쒖꽦??
                break;
            case GameState.Day5_LockerCleaning:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Day5_LockerCleaning)");
                // (異뷀썑) '?щЪ???뺣━' 誘몃땲寃뚯엫/???쒖꽦??
                break;
            case GameState.Day5_BagPacking:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Day5_BagPacking)");
                // (異뷀썑) '媛諛??멸린' 誘몃땲寃뚯엫 ?쒖꽦??
                break;
            case GameState.Day5_FreeTime:
                if (playerController != null)
                    playerController.enabled = true; // 5?쇱감 諛⑷낵???쒓컙
                else
                    Debug.LogWarning("[GameManager] playerController媛 null (Day5_FreeTime)");
                break;
            case GameState.Day5_ClosingAssembly:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Day5_ClosingAssembly)");
                
                if (dialogueManager != null)
                    dialogueManager.StartDialogue("CLOSING_DAY5", null); // 5?쇱감 醫낅?
                else
                    Debug.LogWarning("[GameManager] dialogueManager媛 null (Day5_ClosingAssembly StartDialogue 紐삵븿)");
                break;
            case GameState.Day5_LunchChoice:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Day5_LunchChoice)");
                
                if (dialogueManager != null)
                    dialogueManager.StartDialogue("LUNCH_CHOICE_DAY5", null); // (異뷀썑 '?좏깮吏' 遺꾧린 ?꾩슂)
                else
                    Debug.LogWarning("[GameManager] dialogueManager媛 null (Day5_LunchChoice StartDialogue 紐삵븿)");
                break;
            case GameState.Day5_EndingCredits:
                if (playerController != null) playerController.enabled = false;
                else Debug.LogWarning("[GameManager] playerController媛 null (Day5_EndingCredits)");
                // (異뷀썑) ?붾뵫 ?щ젅????濡쒕뱶
                break;
        }
    }

    // (DialogueFinished, MinigameFinished 占쌉쇽옙占쏙옙 v1.3占쏙옙 占쏙옙占쏙옙)
    // ???醫낅즺 ???ㅼ쓬 ?곹깭濡??꾪솚
    // 二쇱쓽: Subway ?곹깭?먯꽌??DialogueManager媛 ?놁쑝誘濡????⑥닔媛 ?몄텧?섏? ?딆쓬
    //       吏?섏쿋 ?ъ뿉?쒕뒗 梨꾪똿 ?꾨즺 ??蹂꾨룄濡??ㅼ쓬 ?곹깭濡??꾪솚?댁빞 ??
    public void DialogueFinished()
    {
        UnityEngine.Debug.Log("??붽? 醫낅즺?섏뿀?듬땲?? ?꾩옱 ?곹깭: " + currentState.ToString());

        switch (currentState)
        {
            // --- 1~4?쇱감 ?꾩묠 ---
            // 二쇱쓽: Subway ?곹깭?먯꽌??DialogueManager媛 ?놁쑝誘濡???耳?댁뒪???ㅽ뻾?섏? ?딆쓬
            //       梨꾪똿 ?꾨즺 ??ChatService??ChatRoomDetailUI?먯꽌 吏곸젒 ?몄텧 ?꾩슂
            case GameState.Subway:
                ChangeState(GameState.Morning_Slippers);
                break;

            case GameState.Morning_Assembly:
                ChangeState(GameState.Class_Intro_1);
                break;

            // --- 1~4?쇱감 ?섏뾽 ---
            case GameState.Class_Intro_1:
                ChangeState(GameState.Class_Minigame_1);
                break;

            case GameState.Class_Outro_1:
                ChangeState(GameState.Lunch_Run); // ?먮뒗 Lunch_Tetris, Lunch_FreeTime (?좏깮吏濡?遺꾧린 媛??
                break;

            case GameState.Class_Intro_2:
                ChangeState(GameState.Class_Minigame_2);
                break;

            case GameState.Class_Outro_2:
                ChangeState(GameState.Closing_Assembly);
                break;

            case GameState.Closing_Assembly:
                ChangeState(GameState.AfterSchool);
                break;

            // --- 5?쇱감 (?밸퀎) ?섏뾽 ---
            case GameState.Day5_ClosingAssembly:
                ChangeState(GameState.Day5_LunchChoice);
                break;

            case GameState.Day5_LunchChoice:
                // ?좏깮吏?먯꽌 遺꾧린 泥섎━??
                break;

            // ??붽? ?녿뒗 ?곹깭??洹몃?濡??좎?
            default:
                break;
        }
    }

    public void MinigameFinished(bool success)
    {
        UnityEngine.Debug.Log("誘몃땲寃뚯엫 醫낅즺: " + (success ? "?깃났" : "?ㅽ뙣"));

        switch (currentState)
        {
            case GameState.Class_Minigame_1:
                ChangeState(GameState.Class_Outro_1);
                break;

            case GameState.Class_Minigame_2:
                ChangeState(GameState.Class_Outro_2);
                break;

            case GameState.Lunch_Run:
            case GameState.Lunch_Tetris:
                ChangeState(GameState.Lunch_FreeTime);
                break;

            case GameState.Day5_BigCleaning:
                ChangeState(GameState.Day5_LockerCleaning);
                break;

            case GameState.Day5_LockerCleaning:
                ChangeState(GameState.Day5_BagPacking);
                break;

            case GameState.Day5_BagPacking:
                ChangeState(GameState.Day5_FreeTime);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// 吏?섏쿋 ?ъ뿉??梨꾪똿 ?꾨즺 ???몄텧?섎뒗 ?⑥닔
    /// ChatService??ChatRoomDetailUI?먯꽌 ?몄텧 媛??
    /// </summary>
    public void SubwayChatFinished()
    {
        if (currentState == GameState.Subway)
        {
            UnityEngine.Debug.Log("[GameManager] 吏?섏쿋 梨꾪똿 ?꾨즺, ?ㅼ쓬 ?곹깭濡??꾪솚");
            ChangeState(GameState.Morning_Slippers);
        }
    }

    // (v1.3占쏙옙 LoadDialogueFileForState 占쌉쇽옙占쏙옙 占쏙옙占쏙옙占쏙옙)
}

