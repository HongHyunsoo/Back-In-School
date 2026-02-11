using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Health app flow:
/// QuestionPage -> SurmitPage -> Exit(to Home)
/// Submit passes only when:
/// - Q1: "네"
/// - Q2: "검사하지 않음"
/// - Q3: "네"
/// On invalid answers, show Robot warning using the same DialogBox prefab used by DialogueManager.
/// </summary>
public class PhoneHealthSurveyController : MonoBehaviour
{
    private GameObject healthPanel;
    private GameObject questionPage;
    private GameObject surmitPage;

    private readonly List<Button> submitButtons = new List<Button>();
    private Button exitButton;

    private PhoneAppManager appManager;
    private bool previousHealthPanelActive;

    private RectTransform robotRoot;
    private Transform sceneRobotTransform;
    private RectTransform warningBubbleRoot;
    private TextMeshProUGUI warningNameText;
    private TextMeshProUGUI warningBodyText;
    private Coroutine warningRoutine;

    private const string WarningMessage = "\uC624\uB298 \uC544\uCE68\uBD80\uD130 \uBCF4\uAC74\uC30C\uC774\uB791 \uBA74\uB2F4\uD558\uACE0 \uC2F6\uC740 \uAC8C \uC544\uB2C8\uB77C\uBA74 \uB2E4\uC2DC \uCCB4\uD06C\uD574\uC57C \uD560 \uAC70\uC57C";

    private void Start()
    {
        appManager = FindAnyObjectByType<PhoneAppManager>();
        ResolveUiRefs();

        RebindSubmitButtons();

        exitButton = FindExitButtonInSurmitPage();
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(OnExitClicked);
        }

        previousHealthPanelActive = healthPanel != null && healthPanel.activeInHierarchy;
        if (previousHealthPanelActive)
            RefreshPageByState();

        EnsureRobotAndWarningBubble();
    }

    private void Update()
    {
        if (healthPanel == null || questionPage == null || surmitPage == null)
        {
            ResolveUiRefs();
            if (healthPanel == null) return;
        }

        bool now = healthPanel.activeInHierarchy;
        if (!previousHealthPanelActive && now)
        {
            RebindSubmitButtons();
            RefreshPageByState();
            EnsureRobotAndWarningBubble();
        }

        if (now && !IsHealthAllowedInCurrentFlow())
        {
            if (appManager == null)
                appManager = FindAnyObjectByType<PhoneAppManager>();
            if (appManager != null)
                appManager.BackToHome();
            now = false;
        }

        if (now && warningBubbleRoot != null && warningBubbleRoot.gameObject.activeSelf)
            PositionWarningBubbleNearRobot();

        previousHealthPanelActive = now;
    }

    private void OnSubmitClicked()
    {
        if (!HasRequiredAnswers())
        {
            Debug.Log("[HealthSurvey] Invalid answers. Showing robot warning.");
            ShowRobotWarning(WarningMessage);
            return;
        }

        Debug.Log("[HealthSurvey] Required answers passed.");
        int day = GetCurrentDay();
        PhoneSubwayFlowGate.MarkHealthCheckedForDay(day);

        if (questionPage != null) questionPage.SetActive(false);
        if (surmitPage != null) surmitPage.SetActive(true);
    }

    private void RebindSubmitButtons()
    {
        submitButtons.Clear();
        var all = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < all.Length; i++)
        {
            bool byName = all[i].name == "Submit" || all[i].name.Contains("Submit");
            bool byText = false;
            var txt = all[i].GetComponentInChildren<TextMeshProUGUI>(true);
            if (txt != null)
            {
                string t = txt.text.Replace(" ", "").Trim();
                byText = t.Contains("제출") || t.Contains("Submit");
            }

            // 질문 페이지 내부 버튼만 대상으로 제한
            bool inQuestionPage = questionPage != null && all[i].transform.IsChildOf(questionPage.transform);
            if (!(inQuestionPage && (byName || byText))) continue;

            submitButtons.Add(all[i]);
            all[i].onClick.RemoveAllListeners();
            all[i].onClick.AddListener(OnSubmitClicked);
        }

        // 최후 폴백: 질문 페이지 버튼이 하나뿐이면 그 버튼을 submit으로 사용
        if (submitButtons.Count == 0 && questionPage != null)
        {
            var qBtns = questionPage.GetComponentsInChildren<Button>(true);
            if (qBtns.Length == 1)
            {
                submitButtons.Add(qBtns[0]);
                qBtns[0].onClick.RemoveAllListeners();
                qBtns[0].onClick.AddListener(OnSubmitClicked);
            }
        }

        Debug.Log("[HealthSurvey] Submit binding count: " + submitButtons.Count);
    }

    private void OnExitClicked()
    {
        if (appManager == null)
            appManager = FindAnyObjectByType<PhoneAppManager>();

        if (appManager != null)
            appManager.BackToHome();
    }

    private void RefreshPageByState()
    {
        int day = GetCurrentDay();
        bool checkedToday = PhoneSubwayFlowGate.IsHealthChecked(day);

        if (questionPage != null) questionPage.SetActive(!checkedToday);
        if (surmitPage != null) surmitPage.SetActive(checkedToday);
    }

    private bool HasRequiredAnswers()
    {
        bool q1 = IsExpectedAnswer("Question_1", "\uB124");
        bool q2 = IsExpectedAnswer("Question_2", "\uAC80\uC0AC\uD558\uC9C0\uC54A\uC74C");
        bool q3 = IsExpectedAnswer("Question_3", "\uB124");
        return q1 && q2 && q3;
    }

    private bool IsExpectedAnswer(string questionName, string expected)
    {
        var question = FindByName(questionName);
        if (question == null) return false;

        string expectedNorm = NormalizeAnswer(expected);
        var toggles = question.GetComponentsInChildren<Toggle>(true);

        for (int i = 0; i < toggles.Length; i++)
        {
            if (!toggles[i].isOn) continue;

            var txt = toggles[i].GetComponentInChildren<TextMeshProUGUI>(true);
            string label = txt != null ? txt.text : "";
            if (NormalizeAnswer(label) == expectedNorm)
                return true;
        }

        return false;
    }

    private static string NormalizeAnswer(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return raw.Replace(" ", "").Replace("\t", "").Replace("\n", "").Replace("\r", "").Trim();
    }

    private void EnsureRobotAndWarningBubble()
    {
        if (healthPanel == null) return;

        var robotObj = FindByName("Robot");
        if (robotObj == null)
        {
            robotObj = new GameObject("Robot", typeof(RectTransform), typeof(Image));
            robotObj.transform.SetParent(healthPanel.transform, false);

            robotRoot = robotObj.GetComponent<RectTransform>();
            robotRoot.anchorMin = new Vector2(1f, 0f);
            robotRoot.anchorMax = new Vector2(1f, 0f);
            robotRoot.pivot = new Vector2(1f, 0f);
            robotRoot.anchoredPosition = new Vector2(-24f, 24f);
            robotRoot.sizeDelta = new Vector2(120f, 120f);

            var img = robotObj.GetComponent<Image>();
            img.color = new Color(0.2f, 0.55f, 0.95f, 1f);
        }
        else
        {
            robotRoot = robotObj.GetComponent<RectTransform>();
        }

        if (sceneRobotTransform == null)
            sceneRobotTransform = FindSceneRobotTransform();

        if (warningBubbleRoot != null && warningBodyText != null)
            return;

        var bubbleParent = GetBubbleCanvasTransform();
        if (bubbleParent == null)
            return;

        // Prefer the same DialogBox prefab used by DialogueManager.
        SpeechBubbleUI template = TryGetDialogBoxTemplateFromDialogueManager();
        if (template != null)
        {
            var bubble = Instantiate(template, bubbleParent);
            warningBubbleRoot = bubble.transform as RectTransform;
            warningNameText = bubble.nameText;
            warningBodyText = bubble.bodyText;

            warningBubbleRoot.anchorMin = new Vector2(0.5f, 0.5f);
            warningBubbleRoot.anchorMax = new Vector2(0.5f, 0.5f);
            warningBubbleRoot.pivot = new Vector2(0.5f, 0f);
            warningBubbleRoot.anchoredPosition = new Vector2(0f, 0f);
            warningBubbleRoot.localScale = Vector3.one * 0.65f;

            EnsureBubbleHasVisibleBackground(warningBubbleRoot);

            warningBubbleRoot.gameObject.name = "RobotWarningBubble";
            warningBubbleRoot.gameObject.SetActive(false);
            return;
        }

        // Hard fallback if template could not be resolved.
        var bubbleObj = new GameObject("RobotWarningBubble", typeof(RectTransform), typeof(RawImage));
        bubbleObj.transform.SetParent(bubbleParent, false);

        warningBubbleRoot = bubbleObj.GetComponent<RectTransform>();
        warningBubbleRoot.anchorMin = new Vector2(0.5f, 0.5f);
        warningBubbleRoot.anchorMax = new Vector2(0.5f, 0.5f);
        warningBubbleRoot.pivot = new Vector2(0.5f, 0f);
        warningBubbleRoot.anchoredPosition = new Vector2(0f, 0f);
        warningBubbleRoot.sizeDelta = new Vector2(330f, 130f);

        var bubbleImg = bubbleObj.GetComponent<RawImage>();
        bubbleImg.color = new Color(1f, 1f, 1f, 0.96f);

        var textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(bubbleObj.transform, false);
        var tr = textObj.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(12f, 10f);
        tr.offsetMax = new Vector2(-12f, -10f);

        warningBodyText = textObj.GetComponent<TextMeshProUGUI>();
        warningBodyText.alignment = TextAlignmentOptions.TopLeft;
        warningBodyText.fontSize = 20f;
        warningBodyText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        warningBodyText.enableWordWrapping = true;

        warningBubbleRoot.gameObject.SetActive(false);
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

    private SpeechBubbleUI TryGetDialogBoxTemplateFromDialogueManager()
    {
        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm == null) return null;

        // Try to read private serialized field: speechBubblePrefab
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var fi = typeof(DialogueManager).GetField("speechBubblePrefab", flags);
        if (fi == null) return null;

        return fi.GetValue(dm) as SpeechBubbleUI;
    }

    private void ShowRobotWarning(string message)
    {
        EnsureRobotAndWarningBubble();
        if (warningBubbleRoot == null || warningBodyText == null) return;

        if (warningNameText != null)
            warningNameText.text = "Robot";

        warningBodyText.text = message;
        if (warningBodyText != null) warningBodyText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        if (warningNameText != null) warningNameText.color = new Color(0.05f, 0.05f, 0.05f, 1f);
        PositionWarningBubbleNearRobot();
        warningBubbleRoot.gameObject.SetActive(true);
        warningBubbleRoot.SetAsLastSibling();

        if (warningRoutine != null)
            StopCoroutine(warningRoutine);
        warningRoutine = StartCoroutine(CoHideWarningLater());
    }

    private IEnumerator CoHideWarningLater()
    {
        yield return new WaitForSecondsRealtime(3.2f);
        if (warningBubbleRoot != null)
            warningBubbleRoot.gameObject.SetActive(false);
        warningRoutine = null;
    }

    private void PositionWarningBubbleNearRobot()
    {
        if (warningBubbleRoot == null) return;
        if (sceneRobotTransform == null || !sceneRobotTransform.gameObject.activeInHierarchy)
            sceneRobotTransform = FindSceneRobotTransform();

        if (sceneRobotTransform == null) return;
        if (!(warningBubbleRoot.parent is RectTransform parentRect)) return;

        var canvas = parentRect.GetComponentInParent<Canvas>();
        Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        var world = sceneRobotTransform.position + new Vector3(0f, 1.2f, 0f);
        var worldCam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (worldCam == null) return;
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
                if (all[j].GetComponentInParent<PhoneHealthSurveyController>() != null) continue;
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

    private int GetCurrentDay()
    {
        if (FlowManager.Instance != null)
            return FlowManager.Instance.day;

        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            return gm.currentDay;

        return 1;
    }

    private Button FindExitButtonInSurmitPage()
    {
        if (surmitPage == null) return null;

        var buttons = surmitPage.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            if (b.name.Contains("Back")) return b;
            if (b.name.Contains("Exit")) return b;

            var text = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null && text.text.Contains("\uB098\uAC00\uAE30")) return b;
        }

        return null;
    }

    private GameObject FindByName(string name)
    {
        var tr = transform.Find(name);
        if (tr != null) return tr.gameObject;

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.name == name) return child.gameObject;

            var nested = child.GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < nested.Length; j++)
                if (nested[j].name == name) return nested[j].gameObject;
        }

        return null;
    }

    private void ResolveUiRefs()
    {
        healthPanel = FindByName("App_Health");
        questionPage = FindByName("QuestionPage");
        surmitPage = FindByName("SurmitPage");
    }

    private static bool IsHealthAllowedInCurrentFlow()
    {
        string flowType = PlayerPrefs.GetString("FLOW_TYPE", "");
        string flowId = PlayerPrefs.GetString("FLOW_ID", "");

        if (flowType == "CHAT")
            return true;

        if (flowType == "FREEROAM")
            return string.IsNullOrEmpty(flowId) || flowId.Contains("BEFORE_ASSEMBLY");

        return false;
    }
}

