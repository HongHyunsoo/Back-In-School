using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cinemachine;

/*
 * ===================================================================================
 * PlayerController (v2.0 - 점프 로직, 스태미나, 애니메이터 통합)
 * ===================================================================================
 * [v2.0 변경점]
 * - (문제 1 해결) 연속 점프 방지: isGrounded 체크를 FixedUpdate -> Update로 이동.
 * - (문제 2 해결) 점프 스태미나: 점프 시 staminaCostForJump 만큼 스태미나 소모.
 * - (문제 3 적용) 애니메이션: Animator와 연동. 'moveSpeed', 'isGrounded', 'yVelocity' 파라미터 전송.
 * ===================================================================================
 */
public class PlayerController : MonoBehaviour
{
    [Header("Player Stats")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float jumpForce = 12f;

    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float staminaDrainRate = 25f;  // 달릴 때 초당 소모
    public float staminaRegenRate = 15f;  // 쉴 때 초당 회복
    public float staminaCostForJump = 10f; // 점프 1회당 소모
    private float currentStamina;
    public Slider staminaSlider;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    private bool isGrounded;

    [Header("Animation")]
    private Animator anim; // 애니메이션 제어기

    [Header("Camera Control")]
    public CinemachineVirtualCamera virtualCamera; // 2. 인스펙터에서 Virtual Camera를 연결할 슬롯
    public float groundScreenY = 0.4f; // 바닥에 있을 때 Y 위치
    public float airScreenY = 0.5f;    // 공중에 있을 때 Y 위치
    public float cameraYBlendSpeed = 5f; // 카메라 Y 위치가 바뀌는 속도
    private CinemachineFramingTransposer framingTransposer; // 3. 카메라 설정값을 직접 제어할 변수

    // Private components and state
    private Rigidbody2D rb;
    private float moveInput;
    private bool isRunning = false;
    private bool isFacingRight = true;

    // --- UNITY METHODS ---

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>(); // Animator 컴포넌트 가져오기
        currentStamina = maxStamina;

        if (staminaSlider != null)
        {
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }

        currentStamina = maxStamina;

        if (staminaSlider != null)
        {
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }

        // 4. virtualCamera에서 FramingTransposer 컴포넌트 찾아오기
        if (virtualCamera != null)
        {
            framingTransposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        }
    }

    void Update()
    {
        // --- 1. 바닥 체크 (v2.1 로직: 방어막 1) ---
        // Y축 속도가 0.1f (아주 약간이라도) 보다 크면(상승 중이면) 무조건 '공중' 상태로 간주
        if (rb.velocity.y > 0.1f)
        {
            isGrounded = false;
        }
        else
        {
            // 가만히 있거나 하강 중일 때만 바닥 물리 체크
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        // --- 2. 입력 체크 ---
        moveInput = Input.GetAxisRaw("Horizontal");
        isRunning = Input.GetKey(KeyCode.LeftShift);

        // --- 3. 점프 (v2.6 수정: 방어막 2, 3) ---
        // 1. 점프 키 누름
        // 2. (방어막 1로 검증된) isGrounded가 true
        // 3. 스태미나 충분
        // 4. (방어막 2) Y속도의 '절대값'이 0.1f 미만 (거의 멈춰있음)
        if (Input.GetButtonDown("Jump") && isGrounded && currentStamina >= staminaCostForJump && Mathf.Abs(rb.velocity.y) < 0.1f)
        {
            // 점프 실행 (v2.2의 직관적인 속도 할당 방식 사용)
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            currentStamina -= staminaCostForJump; // 스태미나 소모

            // [방어막 3: 핵심] 점프하는 즉시 isGrounded를 수동으로 false로 설정
            isGrounded = false;
        }

        // --- 4. 스태미나 업데이트 ---
        HandleStamina();

        // --- 5. 스프라이트 방향 전환 ---
        FlipSprite();

        // --- 6. 애니메이션 업데이트 (매 프레임) ---
        UpdateAnimations();

        // 5. 카메라 위치 실시간 조작 (매 프레임)
        HandleCameraPosition();
    }

    void FixedUpdate()
    {
        // --- 7. 물리 이동 적용 ---
        float currentSpeed = walkSpeed;
        if (isRunning && moveInput != 0 && currentStamina > 0)
        {
            currentSpeed = runSpeed;
        }
        else
        {
            isRunning = false;
        }

        rb.velocity = new Vector2(moveInput * currentSpeed, rb.velocity.y);
    }

    // --- HELPER METHODS ---

    private void HandleStamina()
    {
        // 달리는 중일 때 스태미나 소모
        if (isRunning && moveInput != 0 && isGrounded) // 땅에서 달릴 때만 소모
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
            if (currentStamina < 0)
            {
                currentStamina = 0;
                isRunning = false;
            }
        }
        // 달리지 않을 때 스태미나 회복
        else if (currentStamina < maxStamina)
        {
            // (점프 직후 공중에서는 회복 안 되게 isGrounded 조건 추가 가능)
            currentStamina += staminaRegenRate * Time.deltaTime;
            if (currentStamina > maxStamina)
            {
                currentStamina = maxStamina;
            }
        }

        if (staminaSlider != null)
        {
            staminaSlider.value = currentStamina;
        }
    }

    private void FlipSprite()
    {
        if ((moveInput < 0 && isFacingRight) || (moveInput > 0 && !isFacingRight))
        {
            isFacingRight = !isFacingRight;
            Vector3 theScale = transform.localScale;
            theScale.x *= -1;
            transform.localScale = theScale;
        }
    }

    // (문제 3 해결) 애니메이터 파라미터 업데이트
    private void UpdateAnimations()
    {
        if (anim == null) return; // Animator가 없으면 실행 안 함

        // 현재 수평 속도의 '절대값'을 보냄 (0: 멈춤, 5: 걷기, 8: 달리기)
        // 걷기/달리기를 구분하기 위해 moveInput(0 또는 1) 대신 실제 속도(rb.velocity.x)를 사용
        float horizontalSpeed = Mathf.Abs(rb.velocity.x);

        anim.SetFloat("moveSpeed", horizontalSpeed); // (Idle, Walk, Run 구분용)
        anim.SetBool("isGrounded", isGrounded);     // (점프, 착지 구분용)
        anim.SetFloat("yVelocity", rb.velocity.y);  // (상승(Jump) / 하강(Fall) 구분용)
    }

    // 6. 동적 카메라 위치를 제어하는 새 함수
    private void HandleCameraPosition()
    {
        if (framingTransposer == null) return; // 카메라 설정이 없으면 종료

        // 1. 목표 Y 위치 설정: 땅에 있으면 groundScreenY, 공중이면 airScreenY
        float targetScreenY = isGrounded ? groundScreenY : airScreenY;

        // 2. 현재 카메라 Y 위치를 목표 Y 위치로 부드럽게 이동 (Lerp)
        framingTransposer.m_ScreenY = Mathf.Lerp(
            framingTransposer.m_ScreenY,
            targetScreenY,
            Time.deltaTime * cameraYBlendSpeed
        );
    }
}