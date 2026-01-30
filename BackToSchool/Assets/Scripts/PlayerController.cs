using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cinemachine;

/*
 * ===================================================================================
 * PlayerController (v2.0 - ���� ����, ���¹̳�, �ִϸ����� ����)
 * ===================================================================================
 * [v2.0 ������]
 * - (���� 1 �ذ�) ���� ���� ����: isGrounded üũ�� FixedUpdate -> Update�� �̵�.
 * - (���� 2 �ذ�) ���� ���¹̳�: ���� �� staminaCostForJump ��ŭ ���¹̳� �Ҹ�.
 * - (���� 3 ����) �ִϸ��̼�: Animator�� ����. 'moveSpeed', 'isGrounded', 'yVelocity' �Ķ���� ����.
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
    public float staminaDrainRate = 25f;  // �޸� �� �ʴ� �Ҹ�
    public float staminaRegenRate = 15f;  // �� �� �ʴ� ȸ��
    public float staminaCostForJump = 10f; // ���� 1ȸ�� �Ҹ�
    private float currentStamina;
    public Slider staminaSlider;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    private bool isGrounded;

    [Header("Animation")]
    private Animator anim; // �ִϸ��̼� �����

    [Header("Camera Control")]
    public CinemachineVirtualCamera virtualCamera; // 2. �ν����Ϳ��� Virtual Camera�� ������ ����
    public float groundScreenY = 0.4f; // �ٴڿ� ���� �� Y ��ġ
    public float airScreenY = 0.5f;    // ���߿� ���� �� Y ��ġ
    public float cameraYBlendSpeed = 5f; // ī�޶� Y ��ġ�� �ٲ�� �ӵ�
    private CinemachineFramingTransposer framingTransposer; // 3. ī�޶� �������� ���� ������ ����

    // Private components and state
    private Rigidbody2D rb;
    private float moveInput;
    private bool isRunning = false;
    private bool isFacingRight = true;

    // --- UNITY METHODS ---

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>(); // Animator ������Ʈ ��������
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

        // 4. virtualCamera���� FramingTransposer ������Ʈ ã�ƿ���
        if (virtualCamera != null)
        {
            framingTransposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        }
    }

    void Update()
    {
        // --- 1. �ٴ� üũ (v2.1 ����: �� 1) ---
        // groundCheck ? rb ? ??? ? ???? ?? ??? ??
        if (rb == null)
        {
            Debug.LogError("[PlayerController] Rigidbody2D ????? ?? ? ????.");
            return;
        }

        if (groundCheck == null)
        {
            Debug.LogError("[PlayerController] groundCheck ? ????? ???? ?????.");
            return;
        }

        // Y�� �ӵ��� 0.1f (���� �ణ�̶�) ���� ũ��(��� ���̸�) ������ '����' ���·� ����
        if (rb.velocity.y > 0.1f)
        {
            isGrounded = false;
        }
        else
        {
            // ������ �ְų� �ϰ� ���� ���� �ٴ� ���� üũ
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        // --- 2. �Է� üũ ---
        moveInput = Input.GetAxisRaw("Horizontal");
        isRunning = Input.GetKey(KeyCode.LeftShift);

        // --- 3. ���� (v2.6 ����: �� 2, 3) ---
        // 1. ���� Ű ����
        // 2. (�� 1�� ������) isGrounded�� true
        // 3. ���¹̳� ���
        // 4. (�� 2) Y�ӵ��� '���밪'�� 0.1f �̸� (���� ��������)
        if (Input.GetButtonDown("Jump") && isGrounded && currentStamina >= staminaCostForJump && Mathf.Abs(rb.velocity.y) < 0.1f)
        {
            // ���� ���� (v2.2�� �������� �ӵ� �Ҵ� ��� ���)
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            currentStamina -= staminaCostForJump; // ���¹̳� �Ҹ�

            // [�� 3: �ٽ�] �����ϴ� ��� isGrounded�� �������� false�� ����
            isGrounded = false;
        }

        // --- 4. ���¹̳� ������Ʈ ---
        HandleStamina();

        // --- 5. ��������Ʈ ���� ��ȯ ---
        FlipSprite();

        // --- 6. �ִϸ��̼� ������Ʈ (�� ������) ---
        UpdateAnimations();

        // 5. ī�޶� ��ġ �ǽð� ���� (�� ������)
        HandleCameraPosition();
    }

    void FixedUpdate()
    {
        // --- 7. ���� �̵� ���� ---
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
        // �޸��� ���� �� ���¹̳� �Ҹ�
        if (isRunning && moveInput != 0 && isGrounded) // ������ �޸� ���� �Ҹ�
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
            if (currentStamina < 0)
            {
                currentStamina = 0;
                isRunning = false;
            }
        }
        // �޸��� ���� �� ���¹̳� ȸ��
        else if (currentStamina < maxStamina)
        {
            // (���� ���� ���߿����� ȸ�� �� �ǰ� isGrounded ���� �߰� ����)
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

    // (���� 3 �ذ�) �ִϸ����� �Ķ���� ������Ʈ
    private void UpdateAnimations()
    {
        if (anim == null) return; // Animator�� ������ ���� �� ��

        // ���� ���� �ӵ��� '���밪'�� ���� (0: ����, 5: �ȱ�, 8: �޸���)
        // �ȱ�/�޸��⸦ �����ϱ� ���� moveInput(0 �Ǵ� 1) ��� ���� �ӵ�(rb.velocity.x)�� ���
        float horizontalSpeed = Mathf.Abs(rb.velocity.x);

        anim.SetFloat("moveSpeed", horizontalSpeed); // (Idle, Walk, Run ���п�)
        anim.SetBool("isGrounded", isGrounded);     // (����, ���� ���п�)
        anim.SetFloat("yVelocity", rb.velocity.y);  // (���(Jump) / �ϰ�(Fall) ���п�)
    }

    // 6. ���� ī�޶� ��ġ�� �����ϴ� �� �Լ�
    private void HandleCameraPosition()
    {
        if (framingTransposer == null) return; // ī�޶� ������ ������ ����

        // 1. ��ǥ Y ��ġ ����: ���� ������ groundScreenY, �����̸� airScreenY
        float targetScreenY = isGrounded ? groundScreenY : airScreenY;

        // 2. ���� ī�޶� Y ��ġ�� ��ǥ Y ��ġ�� �ε巴�� �̵� (Lerp)
        framingTransposer.m_ScreenY = Mathf.Lerp(
            framingTransposer.m_ScreenY,
            targetScreenY,
            Time.deltaTime * cameraYBlendSpeed
        );
    }
}