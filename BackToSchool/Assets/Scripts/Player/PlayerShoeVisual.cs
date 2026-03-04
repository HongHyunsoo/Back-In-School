using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerShoeVisual : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private bool swapRuntimeController;
    [SerializeField] private bool autoSwapIfControllersAssigned = true;
    [SerializeField] private RuntimeAnimatorController sneakersController;
    [SerializeField] private RuntimeAnimatorController slippersController;
    [SerializeField] private string slippersBoolParam = "isSlippers";

    bool lastAppliedIsSlippers = false;
    bool hasAppliedOnce = false;
    bool warnedInvalidControllerSetup = false;

    private void Awake()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponent<Animator>();

        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>(true);
    }

    private void OnEnable()
    {
        ForceRefresh();
    }

    private void Update()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>(true);

        bool isSlippers = FlowManager.Instance != null && FlowManager.Instance.IsWearingSlippers;
        if (!hasAppliedOnce || lastAppliedIsSlippers != isSlippers)
            ApplyVisual(isSlippers);
    }

    public void ForceRefresh()
    {
        hasAppliedOnce = false;
        bool isSlippers = FlowManager.Instance != null && FlowManager.Instance.IsWearingSlippers;
        ApplyVisual(isSlippers);
    }

    private void ApplyVisual(bool isSlippers)
    {
        hasAppliedOnce = true;
        lastAppliedIsSlippers = isSlippers;
        ValidateControllerSetup();

        if (targetAnimator != null)
        {
            if (HasBoolParameter(targetAnimator, slippersBoolParam))
                targetAnimator.SetBool(slippersBoolParam, isSlippers);

            bool shouldSwap = swapRuntimeController ||
                              (autoSwapIfControllersAssigned && sneakersController != null && slippersController != null);
            if (shouldSwap)
            {
                RuntimeAnimatorController next = isSlippers ? slippersController : sneakersController;
                if (next != null && targetAnimator.runtimeAnimatorController != next)
                    SwapControllerResetState(next, isSlippers);
            }
        }
    }

    private void ValidateControllerSetup()
    {
        if (warnedInvalidControllerSetup)
            return;

        bool shouldSwap = swapRuntimeController ||
                          (autoSwapIfControllersAssigned && sneakersController != null && slippersController != null);
        if (!shouldSwap)
            return;

        if (sneakersController == null || slippersController == null)
        {
            warnedInvalidControllerSetup = true;
            Debug.LogWarning("[PlayerShoeVisual] Sneakers/Slippers controller reference is missing.", this);
            return;
        }

        if (sneakersController == slippersController)
        {
            warnedInvalidControllerSetup = true;
            Debug.LogWarning(
                $"[PlayerShoeVisual] Invalid setup: both controllers are '{sneakersController.name}'. " +
                "Assign slippersController to PlayerAnimController_Shoes.",
                this);
        }
    }

    private void SwapControllerResetState(RuntimeAnimatorController next, bool isSlippers)
    {
        if (targetAnimator == null || next == null)
            return;

        targetAnimator.runtimeAnimatorController = next;
        targetAnimator.Rebind();
        targetAnimator.Update(0f);

        // Re-apply shoe flag after rebind.
        if (HasBoolParameter(targetAnimator, slippersBoolParam))
            targetAnimator.SetBool(slippersBoolParam, isSlippers);

        // Keep locomotion params in sane defaults right after controller swap.
        SetFloatIfHas(targetAnimator, "moveSpeed", 0f);
        SetFloatIfHas(targetAnimator, "yVelocity", 0f);
        SetBoolIfHas(targetAnimator, "isGrounded", true);

        Debug.Log($"[PlayerShoeVisual] Swapped animator controller -> {next.name} (isSlippers={isSlippers})", this);
    }

    private static bool HasBoolParameter(Animator animator, string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName))
            return false;

        var parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Bool &&
                parameters[i].name == paramName)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasFloatParameter(Animator animator, string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName))
            return false;

        var parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Float &&
                parameters[i].name == paramName)
            {
                return true;
            }
        }
        return false;
    }

    private static void SetFloatIfHas(Animator animator, string paramName, float value)
    {
        if (HasFloatParameter(animator, paramName))
            animator.SetFloat(paramName, value);
    }

    private static void SetBoolIfHas(Animator animator, string paramName, bool value)
    {
        if (HasBoolParameter(animator, paramName))
            animator.SetBool(paramName, value);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignControllersByName();
    }

    [ContextMenu("Auto Assign Shoe Controllers")]
    private void AutoAssignControllersByName()
    {
        if (targetAnimator == null)
        {
            targetAnimator = GetComponent<Animator>();
            if (targetAnimator == null)
                targetAnimator = GetComponentInChildren<Animator>(true);
        }

        RuntimeAnimatorController defaultCtrl = FindControllerByExactName("PlayerAnimController");
        RuntimeAnimatorController shoesCtrl = FindControllerByExactName("PlayerAnimController_Shoes");

        if (defaultCtrl != null)
            sneakersController = defaultCtrl;
        if (shoesCtrl != null)
            slippersController = shoesCtrl;
    }

    private static RuntimeAnimatorController FindControllerByExactName(string name)
    {
        string[] guids = AssetDatabase.FindAssets($"t:RuntimeAnimatorController {name}");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
            if (ctrl != null && ctrl.name == name)
                return ctrl;
        }
        return null;
    }
#endif
}
