using UnityEngine;
using UnityEngine.UI;
using Cinemachine;

public class PlayerController : MonoBehaviour
{
    [Header("Player Stats")]
    public float walkSpeed = 5f;
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

    private Rigidbody2D rb;
    private Animator anim;
    private CinemachineFramingTransposer framingTransposer;

    private float currentStamina;
    private float moveInput;
    private bool isRunning;
    private bool isFacingRight = true;
    private bool isGrounded;
    private bool animGrounded;
    private float groundedStableTimer;
    private static readonly int HashMoveSpeed = Animator.StringToHash("moveSpeed");
    private static readonly int HashIsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int HashYVelocity = Animator.StringToHash("yVelocity");

    public bool IsRunningHeld => isRunning;
    public bool IsActivelyRunning => isRunning && moveInput != 0f && isGrounded && currentStamina > 0f;
    public float HorizontalInput => moveInput;
    public bool IsGrounded => isGrounded;
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

        bool rawGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
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

            if (Input.GetKeyDown(jumpKey) && isGrounded && currentStamina >= staminaCostForJump && Mathf.Abs(rb.velocity.y) < 0.1f)
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

        HandleStamina();
        FlipSprite();
        UpdateAnimations();
        HandleCameraPosition();
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        float currentSpeed = (isRunning && moveInput != 0f && currentStamina > 0f) ? runSpeed : walkSpeed;
        rb.velocity = new Vector2(moveInput * currentSpeed, rb.velocity.y);
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
