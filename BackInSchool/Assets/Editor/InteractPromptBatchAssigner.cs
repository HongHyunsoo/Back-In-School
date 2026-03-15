using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

public class InteractPromptBatchAssigner : EditorWindow
{
    private GameObject promptPrefab;
    private Vector3 localOffset = Vector3.zero;
    private bool overwriteExisting = false;
    private string interactTextToken = "interact";
    private string stairUpTextToken = "up";
    private string stairDownTextToken = "down";
    private bool autoCreateMissingTexts = true;
    private Vector3 stairUpLocalOffset = new Vector3(0f, 0.18f, 0f);
    private Vector3 stairDownLocalOffset = new Vector3(0f, -0.18f, 0f);

    [MenuItem("Tools/Back in School/Assign Interact Prompts")]
    public static void Open()
    {
        GetWindow<InteractPromptBatchAssigner>("Assign Interact Prompts");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Batch Assign interactPrompt", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        promptPrefab = (GameObject)EditorGUILayout.ObjectField("Prompt Prefab", promptPrefab, typeof(GameObject), false);
        localOffset = EditorGUILayout.Vector3Field("Local Offset", localOffset);
        overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", overwriteExisting);
        interactTextToken = EditorGUILayout.TextField("Interact Text Token", interactTextToken);
        stairUpTextToken = EditorGUILayout.TextField("Stair Up Text Token", stairUpTextToken);
        stairDownTextToken = EditorGUILayout.TextField("Stair Down Text Token", stairDownTextToken);
        autoCreateMissingTexts = EditorGUILayout.Toggle("Auto Create Missing Texts", autoCreateMissingTexts);
        stairUpLocalOffset = EditorGUILayout.Vector3Field("Stair Up Offset", stairUpLocalOffset);
        stairDownLocalOffset = EditorGUILayout.Vector3Field("Stair Down Offset", stairDownLocalOffset);

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(promptPrefab == null))
        {
            if (GUILayout.Button("Apply To Current Scene"))
                ApplyToCurrentScene();
        }

        EditorGUILayout.HelpBox(
            "Targets: SchoolLockerInteraction, DialogueTrigger, MapTransitionPortal\n" +
            "A prefab instance is created as a child of each target and assigned to interactPrompt.",
            MessageType.Info);
    }

    private void ApplyToCurrentScene()
    {
        if (promptPrefab == null)
            return;

        int assignedPrompts = 0;
        int assignedTexts = 0;
        AssignForType<SchoolLockerInteraction>(ref assignedPrompts, ref assignedTexts);
        AssignForType<DialogueTrigger>(ref assignedPrompts, ref assignedTexts);
        AssignForType<MapTransitionPortal>(ref assignedPrompts, ref assignedTexts);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[InteractPromptBatchAssigner] Assigned prompts: {assignedPrompts}, linked text fields: {assignedTexts}");
    }

    private void AssignForType<T>(ref int promptCount, ref int textCount) where T : Component
    {
        var targets = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            if (target == null)
                continue;

            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty("interactPrompt");
            if (prop == null)
                continue;

            GameObject promptInstance = prop.objectReferenceValue as GameObject;

            bool shouldCreatePrompt = promptInstance == null || overwriteExisting;
            if (shouldCreatePrompt)
            {
                Undo.RecordObject(target, "Assign interactPrompt");

                if (promptInstance != null && overwriteExisting)
                    Undo.DestroyObjectImmediate(promptInstance);

                promptInstance = (GameObject)PrefabUtility.InstantiatePrefab(promptPrefab, target.transform);
                promptInstance.name = promptPrefab.name;
                promptInstance.transform.localPosition = localOffset;
                promptInstance.transform.localRotation = Quaternion.identity;
                promptInstance.SetActive(false);
                Undo.RegisterCreatedObjectUndo(promptInstance, "Create interactPrompt");

                prop.objectReferenceValue = promptInstance;
                promptCount++;
            }

            if (promptInstance != null)
                textCount += TryAssignPromptTextRefs(so, promptInstance);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private int TryAssignPromptTextRefs(SerializedObject so, GameObject promptRoot)
    {
        int linked = 0;
        linked += AssignTextRef(so, promptRoot, "interactKeyText", interactTextToken, Vector3.zero);
        linked += AssignTextRef(so, promptRoot, "stairUpKeyText", stairUpTextToken, stairUpLocalOffset);
        linked += AssignTextRef(so, promptRoot, "stairDownKeyText", stairDownTextToken, stairDownLocalOffset);
        return linked;
    }

    private int AssignTextRef(SerializedObject so, GameObject promptRoot, string fieldName, string token, Vector3 localOffset)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
            return 0;

        if (!overwriteExisting && prop.objectReferenceValue != null)
            return 0;

        TMP_Text candidate = FindTextByToken(promptRoot, token);
        if (candidate == null)
            candidate = promptRoot.GetComponentInChildren<TMP_Text>(true);
        if (candidate == null && autoCreateMissingTexts)
            candidate = CreatePromptText(promptRoot, fieldName, localOffset);

        if (candidate == null)
            return 0;

        prop.objectReferenceValue = candidate;
        return 1;
    }

    private static TMP_Text FindTextByToken(GameObject root, string token)
    {
        if (root == null || string.IsNullOrWhiteSpace(token))
            return null;

        var all = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;

            string name = all[i].name;
            if (name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return all[i];
        }

        return null;
    }

    private static TMP_Text CreatePromptText(GameObject root, string fieldName, Vector3 localOffset)
    {
        if (root == null)
            return null;

        var go = new GameObject(fieldName);
        Undo.RegisterCreatedObjectUndo(go, "Create prompt text");
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = localOffset;

        var text = go.AddComponent<TextMeshPro>();
        text.text = "[KEY]";
        text.fontSize = 6f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        text.sortingOrder = 20;
        return text;
    }
}
