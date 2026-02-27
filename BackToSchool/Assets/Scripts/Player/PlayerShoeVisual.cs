using UnityEngine;

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
                    SwapControllerPreservingState(next);
            }
        }
    }

    private void SwapControllerPreservingState(RuntimeAnimatorController next)
    {
        if (targetAnimator == null || next == null)
            return;

        var state = targetAnimator.GetCurrentAnimatorStateInfo(0);
        int stateHash = state.shortNameHash;
        float normalized = state.normalizedTime % 1f;

        targetAnimator.runtimeAnimatorController = next;
        targetAnimator.Update(0f);

        if (stateHash != 0)
            targetAnimator.Play(stateHash, 0, normalized);
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
}
