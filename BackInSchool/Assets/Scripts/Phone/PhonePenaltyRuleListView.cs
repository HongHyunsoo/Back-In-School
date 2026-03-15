using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhonePenaltyRuleListView : MonoBehaviour
{
    [Header("Legacy Single Text (optional)")]
    [SerializeField] private TextMeshProUGUI targetText;

    [Header("Stack View (chat-like)")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private bool newestAtBottom = true;
    [SerializeField] private bool autoScrollToBottom = true;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Text")]
    [SerializeField] private string emptyKo = "\uC544\uC9C1 \uBC8C\uC810 \uAE30\uB85D\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
    [SerializeField] private string emptyEn = "No penalty records yet.";

    private readonly List<GameObject> spawnedItems = new List<GameObject>();

    private void OnEnable()
    {
        PenaltyReasonLog.OnChanged += Refresh;
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
        Refresh();
    }

    private void OnDisable()
    {
        PenaltyReasonLog.OnChanged -= Refresh;
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    public void Refresh()
    {
        var items = PenaltyReasonLog.GetAll();
        if (items == null || items.Count == 0)
        {
            ClearSpawnedItems();
            SetLegacyText(L(emptyKo, emptyEn));
            return;
        }

        bool canSpawn = (contentRoot != null && itemPrefab != null);
        if (!canSpawn)
        {
            string all = string.Empty;
            for (int i = 0; i < items.Count; i++)
            {
                all += BuildEntryText(items[i]);
                if (i < items.Count - 1)
                    all += "\n";
            }

            SetLegacyText(all);
            return;
        }

        SetLegacyText(string.Empty);
        BuildStackedItems(items);
        if (autoScrollToBottom)
            ScrollToEnd();
    }

    private void OnLanguageChanged(Language _)
    {
        Refresh();
    }

    private void BuildStackedItems(IReadOnlyList<PenaltyReasonEntry> items)
    {
        ClearSpawnedItems();

        if (newestAtBottom)
        {
            for (int i = 0; i < items.Count; i++)
                SpawnItem(items[i]);
        }
        else
        {
            for (int i = items.Count - 1; i >= 0; i--)
                SpawnItem(items[i]);
        }
    }

    private void SpawnItem(PenaltyReasonEntry entry)
    {
        var go = Instantiate(itemPrefab, contentRoot);
        string scoreText = BuildScoreText(entry);
        string reasonText = BuildReasonText(entry);

        if (!TryApplyRuleBreakerFields(go, scoreText, reasonText))
        {
            var textComp = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (textComp != null)
                textComp.text = BuildEntryText(entry);
        }
        spawnedItems.Add(go);
    }

    private void ClearSpawnedItems()
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            if (spawnedItems[i] != null)
                Destroy(spawnedItems[i]);
        }

        spawnedItems.Clear();
    }

    private string BuildEntryText(PenaltyReasonEntry it)
    {
        string text = GetReasonLabel(it.reasonId) + " (+" + it.amount + ")";
        if (it.day > 0)
            text += "  D" + it.day;
        return text;
    }

    private string BuildScoreText(PenaltyReasonEntry it)
    {
        return "+" + it.amount.ToString();
    }

    private string BuildReasonText(PenaltyReasonEntry it)
    {
        string reason = GetReasonLabel(it.reasonId);
        if (it.day > 0)
            reason += "  D" + it.day;
        return reason;
    }

    private bool TryApplyRuleBreakerFields(GameObject go, string score, string reason)
    {
        if (go == null)
            return false;

        TMP_Text scoreText = FindNamedText(go.transform, "Score");
        TMP_Text reasonText = FindNamedText(go.transform, "Reason");
        if (scoreText == null || reasonText == null)
            return false;

        scoreText.text = score;
        reasonText.text = reason;
        return true;
    }

    private static TMP_Text FindNamedText(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        Transform t = root.Find(childName);
        if (t == null)
            return null;

        return t.GetComponent<TMP_Text>();
    }

    private void SetLegacyText(string text)
    {
        if (targetText == null)
            targetText = GetComponentInChildren<TextMeshProUGUI>(true);
        if (targetText != null)
            targetText.text = text;
    }

    private void ScrollToEnd()
    {
        if (scrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private string GetReasonLabel(string reasonId)
    {
        switch (reasonId)
        {
            case PenaltyReasonLog.ReasonHealthSurveyMissing:
                return L("\uC790\uAC00\uC9C4\uB2E8 \uBBF8\uC2E4\uC2DC", "Health survey not completed");
            case PenaltyReasonLog.ReasonNoSlippers:
                return L("\uC2E4\uB0B4\uD654 \uBBF8\uCC29\uC6A9", "Indoor slippers not worn");
            case PenaltyReasonLog.ReasonRunningAtLunch:
                return L("\uC810\uC2EC \uC790\uC720\uC2DC\uAC04 \uB6F0\uAE30", "Running during lunch free time");
            default:
                return L("\uAE30\uD0C0 \uBC8C\uC810", "Penalty");
        }
    }

    private string L(string ko, string en)
    {
        if (LocalizationManager.Instance == null)
            return ko;

        return LocalizationManager.Instance.GetCurrentLanguage() == Language.Korean ? ko : en;
    }
}
