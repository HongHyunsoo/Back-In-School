using UnityEngine;

[DisallowMultipleComponent]
public class BackgroundNpcWalker : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 1.3f;
    [SerializeField] private bool flipSpriteWithDirection = true;
    [SerializeField] private bool spriteFacesRight = false;
    [SerializeField] private float despawnPadding = 0.4f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string moveSpeedParameter = "moveSpeed";
    [SerializeField] private string groundedParameter = "isGrounded";
    [SerializeField] private string walkStateName = "NPC_C_walk";
    [SerializeField] private float animationBlendSeconds = 0.08f;

    private SpriteRenderer spriteRenderer;
    private float direction = 1f;
    private float despawnWorldX = 12f;
    private int moveSpeedHash;
    private int groundedHash;
    private int walkStateHash;

    private void Awake()
    {
        ResolveReferences();
        CacheAnimationHashes();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheAnimationHashes();
        ApplyFacing();
        ApplyWalkAnimation();
    }

    private void Update()
    {
        float speed = Mathf.Max(0f, moveSpeed);
        transform.position += Vector3.right * (direction * speed * Time.deltaTime);
        ApplyAnimatorParameters(speed);

        if ((direction > 0f && transform.position.x > despawnWorldX + despawnPadding) ||
            (direction < 0f && transform.position.x < despawnWorldX - despawnPadding))
        {
            Destroy(gameObject);
        }
    }

    public void Initialize(float walkDirection, float speed, float despawnX)
    {
        direction = walkDirection >= 0f ? 1f : -1f;
        moveSpeed = Mathf.Max(0f, speed);
        despawnWorldX = despawnX;
        ResolveReferences();
        ApplyFacing();
        ApplyWalkAnimation();
    }

    private void ResolveReferences()
    {
        if (animator == null)
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>(true);
    }

    private void CacheAnimationHashes()
    {
        moveSpeedHash = string.IsNullOrEmpty(moveSpeedParameter) ? 0 : Animator.StringToHash(moveSpeedParameter);
        groundedHash = string.IsNullOrEmpty(groundedParameter) ? 0 : Animator.StringToHash(groundedParameter);
        walkStateHash = string.IsNullOrEmpty(walkStateName) ? 0 : Animator.StringToHash(walkStateName);
    }

    private void ApplyFacing()
    {
        if (!flipSpriteWithDirection || spriteRenderer == null)
            return;

        spriteRenderer.flipX = spriteFacesRight ? direction < 0f : direction > 0f;
    }

    private void ApplyWalkAnimation()
    {
        if (animator == null)
            return;

        int targetStateHash = ResolveWalkStateHash();
        if (targetStateHash != 0 && animator.HasState(0, targetStateHash))
        {
            animator.enabled = true;
            animator.speed = 1f;
            animator.Play(targetStateHash, 0, 0f);
            if (animationBlendSeconds > 0f)
                animator.CrossFadeInFixedTime(targetStateHash, Mathf.Max(0f, animationBlendSeconds), 0, 0f);
        }

        ApplyAnimatorParameters(Mathf.Max(0f, moveSpeed));
    }

    private int ResolveWalkStateHash()
    {
        if (walkStateHash != 0 && animator.HasState(0, walkStateHash))
            return walkStateHash;

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null || controller.animationClips == null)
            return 0;

        AnimationClip[] clips = controller.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null || string.IsNullOrEmpty(clip.name))
                continue;

            int clipStateHash = Animator.StringToHash(clip.name);
            if (animator.HasState(0, clipStateHash))
                return clipStateHash;
        }

        return 0;
    }

    private void ApplyAnimatorParameters(float speed)
    {
        if (animator == null)
            return;

        SetFloatIfExists(moveSpeedHash, speed);
        SetBoolIfExists(groundedHash, true);
    }

    private void SetFloatIfExists(int hash, float value)
    {
        if (hash == 0)
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == hash && parameters[i].type == AnimatorControllerParameterType.Float)
            {
                animator.SetFloat(hash, value);
                return;
            }
        }
    }

    private void SetBoolIfExists(int hash, bool value)
    {
        if (hash == 0)
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == hash && parameters[i].type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(hash, value);
                return;
            }
        }
    }
}
