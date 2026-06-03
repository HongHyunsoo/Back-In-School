using UnityEngine;
using UnityEngine.UI;
using Cinemachine;
using System;
using System.Linq;

public class PlayerController : MonoBehaviour
{
    [Header("Player Stats")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float jumpForce = 12f;

    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float staminaDrainRate = 25f;
    public float staminaRegenRate = 15f;
    public float staminaCostForJump = 10f;
    public Slider staminaSlider;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Camera Control")]
    public CinemachineVirtualCamera virtualCamera;
    public float groundScreenY = 0.4f;
    public float airScreenY = 0.5f;
    public float cameraYBlendSpeed = 5f;
    [Range(0f, 1f)] public float cameraScreenX = 0.5f;
    [Range(0f, 1f)] public float cameraDeadZoneWidth = 0f;
    [Range(0f, 1f)] public float cameraDeadZoneHeight = 0f;
    [Range(0f, 1f)] public float cameraSoftZoneWidth = 0.24f;
    [Range(0f, 1f)] public float cameraSoftZoneHeight = 0.55f;
    public float cameraXDamping = 0.35f;
    public float cameraYDamping = 0.85f;

    [Header("Locomotion Clips")]
    [SerializeField] private AnimationClip defaultWalkAnimationClip;
    [SerializeField] private AnimationClip defaultSprintAnimationClip;
    [SerializeField] private AnimationClip shoesWalkAnimationClip;
    [SerializeField] private AnimationClip shoesSprintAnimationClip;

    private Rigidbody2D rb;
    private Animator anim;
    private CinemachineFramingTransposer framingTransposer;
    private RuntimeAnimatorController locomotionBaseController;
    private AnimatorOverrideController locomotionOverrideController;
    private AnimationClip locomotionWalkClip;
    private AnimationClip locomotionSprintClip;
    private bool? currentAppliedSprintMode;

    private float currentStamina;
    private float moveInput;
    private bool isRunning;
    private bool isFacingRight = true;
    private bool isGrounded;
    private bool animGrounded;
    private bool isPressingIntoWall;
    private bool touchingWallLeft;
    private bool touchingWallRight;
    private float groundedStableTimer;
    private readonly RaycastHit2D[] horizontalMoveHits = new RaycastHit2D[4];
    private readonly ContactPoint2D[] wallContacts = new ContactPoint2D[8];
    private readonly ContactPoint2D[] floorContacts = new ContactPoint2D[8];
    private static readonly int HashMoveSpeed = Animator.StringToHash("moveSpeed");
    private static readonly int HashIsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int HashYVelocity = Animator.StringToHash("yVelocity");

    public bool IsRunningHeld => isRunning;
    public bool IsActivelyRunning => isRunning && moveInput != 0f && isGrounded && currentStamina > 0f;
    public float HorizontalInput => moveInput;
    public bool IsGrounded => isGrounded;
    public bool IsGroundedStable => animGrounded;
    public bool IsTouchingFloor => HasFloorContact();
    public float VerticalVelocity => rb != null ? rb.velocity.y : 0f;
    public bool ExternalInputLocked { get; set; }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        currentStamina = maxStamina;
        if (staminaSlider != null)
        {
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }

        if (virtualCamera != null)
            framingTransposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();

        ApplyCameraFramingSettings();
    }

    private void Update()
    {
        if (rb == null || groundCheck == null)
            return;

        bool rawGrounded = CheckGroundedOnFloor();
        isGrounded = rawGrounded;

        // Debounce animator grounding so Fall does not end from one-frame overlap flicker.
        if (rawGrounded && rb.velocity.y <= 0.05f)
            groundedStableTimer += Time.deltaTime;
        else
            groundedStableTimer = 0f;

        animGrounded = groundedStableTimer >= 0.06f;

        KeyCode leftKey = KeyBindingConfig.Get(KeyBindingConfig.LeftKey, KeyCode.A);
        KeyCode rightKey = KeyBindingConfig.Get(KeyBindingConfig.RightKey, KeyCode.D);
        KeyCode jumpKey = KeyBindingConfig.Get(KeyBindingConfig.JumpKey, KeyCode.Space);
        KeyCode sprintKey = KeyBindingConfig.Get(KeyBindingConfig.SprintKey, KeyCode.LeftShift);

        float horizontal = 0f;
        if (!ExternalInputLocked)
        {
            if (Input.GetKey(leftKey)) horizontal -= 1f;
            if (Input.GetKey(rightKey)) horizontal += 1f;
            isRunning = Input.GetKey(sprintKey);

            RefreshWallContacts();
            bool pressingIntoWallNow = IsMovingIntoTouchedWall(horizontal) || IsHorizontalMoveBlocked(horizontal * walkSpeed);
            if (Input.GetKeyDown(jumpKey) && isGrounded && !pressingIntoWallNow && currentStamina >= staminaCostForJump && Mathf.Abs(rb.velocity.y) < 0.1f)
            {
                rb.velocity = new Vector2(rb.velocity.x, jumpForce);
                currentStamina -= staminaCostForJump;
                isGrounded = false;
                animGrounded = false;
                groundedStableTimer = 0f;
            }
        }
        else
        {
            isRunning = false;
        }

        moveInput = Mathf.Clamp(horizontal, -1f, 1f);
        isPressingIntoWall = IsMovingIntoTouchedWall(moveInput) || IsHorizontalMoveBlocked(moveInput * walkSpeed);

        HandleStamina();
        FlipSprite();
        ApplyLocomotionClipOverride();
        UpdateAnimations();
        HandleCameraPosition();
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        RefreshWallContacts();
        float currentSpeed = (isRunning && moveInput != 0f && currentStamina > 0f) ? runSpeed : walkSpeed;
        float targetVelocityX = moveInput * currentSpeed;
        bool movingIntoWall = IsMovingIntoTouchedWall(moveInput) || isPressingIntoWall || IsHorizontalMoveBlocked(targetVelocityX);
        if (movingIntoWall)
            targetVelocityX = 0f;

        rb.velocity = new Vector2(targetVelocityX, rb.velocity.y);
    }

    private bool CheckGroundedOnFloor()
    {
        Vector2 origin = groundCheck.position;
        float radius = Mathf.Max(0.01f, groundCheckRadius);
        float distance = Mathf.Max(0.02f, radius * 0.5f);
        RaycastHit2D hit = Physics2D.CircleCast(origin + (Vector2.up * 0.03f), radius, Vector2.down, distance, groundLayer);
        return hit.collider != null && hit.normal.y >= 0.55f;
    }

    private bool HasFloorContact()
    {
        if (rb == null)
            return false;

        int count = rb.GetContacts(floorContacts);
        for (int i = 0; i < count; i++)
        {
            if (floorContacts[i].normal.y >= 0.55f)
                return true;
        }

        return false;
    }

    private void RefreshWallContacts()
    {
        touchingWallLeft = false;
        touchingWallRight = false;

        if (rb == null)
            return;

        int count = rb.GetContacts(wallContacts);
        for (int i = 0; i < count; i++)
        {
            Vector2 normal = wallContacts[i].normal;
            if (normal.y >= 0.55f)
                continue;

            if (normal.x > 0.45f)
                touchingWallLeft = true;
            else if (normal.x < -0.45f)
                touchingWallRight = true;
        }
    }

    private bool IsMovingIntoTouchedWall(float horizontal)
    {
        if (horizontal > 0f)
            return touchingWallRight;

        if (horizontal < 0f)
            return touchingWallLeft;

        return false;
    }

    private bool IsHorizontalMoveBlocked(float targetVelocityX)
    {
        if (rb == null || Mathf.Approximately(targetVelocityX, 0f))
            return false;

        float direction = Mathf.Sign(targetVelocityX);
        var filter = new ContactFilter2D();
        filter.SetLayerMask(groundLayer);
        filter.useTriggers = false;

        float castDistance = Mathf.Abs(targetVelocityX) * Time.fixedDeltaTime + 0.04f;
        int count = rb.Cast(new Vector2(direction, 0f), filter, horizontalMoveHits, castDistance);
        for (int i = 0; i < count; i++)
        {
            var hit = horizontalMoveHits[i];
            if (hit.collider == null)
                continue;

            if (Mathf.Abs(hit.normal.x) >= 0.45f && hit.normal.y < 0.55f)
                return true;
        }

        return false;
    }

    private void HandleStamina()
    {
        if (isRunning && moveInput != 0f && isGrounded)
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
            if (currentStamina < 0f)
            {
                currentStamina = 0f;
                isRunning = false;
            }
        }
        else if (currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            if (currentStamina > maxStamina)
                currentStamina = maxStamina;
        }

        if (staminaSlider != null)
            staminaSlider.value = currentStamina;
    }

    private void FlipSprite()
    {
        if ((moveInput < 0f && isFacingRight) || (moveInput > 0f && !isFacingRight))
        {
            isFacingRight = !isFacingRight;
            Vector3 s = transform.localScale;
            s.x *= -1f;
            transform.localScale = s;
        }
    }

    private void UpdateAnimations()
    {
        if (anim == null || rb == null)
            return;

        float horizontalSpeed = Mathf.Abs(rb.velocity.x);
        if (HasAnimatorParam(HashMoveSpeed, AnimatorControllerParameterType.Float))
            anim.SetFloat(HashMoveSpeed, horizontalSpeed);
        if (HasAnimatorParam(HashIsGrounded, AnimatorControllerParameterType.Bool))
            anim.SetBool(HashIsGrounded, animGrounded);
        if (HasAnimatorParam(HashYVelocity, AnimatorControllerParameterType.Float))
            anim.SetFloat(HashYVelocity, rb.velocity.y);
    }

    private void ApplyLocomotionClipOverride()
    {
        if (anim == null || anim.runtimeAnimatorController == null)
            return;

        RuntimeAnimatorController activeController = anim.runtimeAnimatorController;
        RuntimeAnimatorController baseController =
            activeController is AnimatorOverrideController overrideController
                ? overrideController.runtimeAnimatorController
                : activeController;

        if (baseController == null)
            return;

        if (locomotionBaseController != baseController || locomotionOverrideController == null)
        {
            locomotionBaseController = baseController;
            locomotionOverrideController = new AnimatorOverrideController(baseController);
            locomotionWalkClip = baseController.animationClips.FirstOrDefault(clip =>
                clip != null &&
                (clip.name == "Player_Walk" || clip.name == "Player_Walk_Shoes"));

            bool useShoesSet =
                baseController.name.IndexOf("Sneakers", StringComparison.OrdinalIgnoreCase) >= 0 ||
                baseController.name.IndexOf("Shoes", StringComparison.OrdinalIgnoreCase) >= 0;

            locomotionSprintClip = useShoesSet ? shoesSprintAnimationClip : defaultSprintAnimationClip;

            if (locomotionWalkClip == null)
                locomotionWalkClip = useShoesSet ? shoesWalkAnimationClip : defaultWalkAnimationClip;

            if (locomotionWalkClip == null || locomotionSprintClip == null)
            {
                locomotionOverrideController = null;
                locomotionBaseController = null;
                locomotionWalkClip = null;
                locomotionSprintClip = null;
                currentAppliedSprintMode = null;
                return;
            }

            anim.runtimeAnimatorController = locomotionOverrideController;
            currentAppliedSprintMode = null;
        }

        if (locomotionOverrideController == null || locomotionWalkClip == null || locomotionSprintClip == null)
            return;

        bool useSprint = IsActivelyRunning;
        if (currentAppliedSprintMode.HasValue && currentAppliedSprintMode.Value == useSprint)
            return;

        locomotionOverrideController[locomotionWalkClip] = useSprint ? locomotionSprintClip : locomotionWalkClip;
        currentAppliedSprintMode = useSprint;
    }

    private bool HasAnimatorParam(int hash, AnimatorControllerParameterType type)
    {
        if (anim == null)
            return false;

        var parameters = anim.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == hash && parameters[i].type == type)
                return true;
        }
        return false;
    }

    private void HandleCameraPosition()
    {
        if (framingTransposer == null)
            return;

        ApplyCameraFramingSettings();

        float targetScreenY = isGrounded ? groundScreenY : airScreenY;
        framingTransposer.m_ScreenY = Mathf.Lerp(
            framingTransposer.m_ScreenY,
            targetScreenY,
            Time.deltaTime * cameraYBlendSpeed
        );
    }

    private void ApplyCameraFramingSettings()
    {
        if (virtualCamera != null && framingTransposer == null)
            framingTransposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();

        if (framingTransposer == null)
            return;

        framingTransposer.m_ScreenX = cameraScreenX;
        framingTransposer.m_DeadZoneWidth = cameraDeadZoneWidth;
        framingTransposer.m_DeadZoneHeight = cameraDeadZoneHeight;
        framingTransposer.m_SoftZoneWidth = cameraSoftZoneWidth;
        framingTransposer.m_SoftZoneHeight = cameraSoftZoneHeight;
        framingTransposer.m_XDamping = cameraXDamping;
        framingTransposer.m_YDamping = cameraYDamping;
    }
}
