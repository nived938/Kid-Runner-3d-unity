using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement (forward & lanes)")]
    public float forwardSpeed = 6f;            // starting forward speed
    public float speedIncreaseRate = 0.03f;    // units/sec added each second
    public float maxSpeed = 18f;
    public float laneDistance = 2.9f;          // distance between lanes (center-left/right)
    public float laneChangeSpeed = 8f;         // how quickly we interpolate horizontally

    [Header("Jump")]
    public float jumpHeight = 1.8f;            // meters
    public float gravity = -20f;               // negative value

    [Header("Slide")]
    [Tooltip("How long slide lasts (seconds)")]
    public float slideDuration = 0.8f;
    [Range(0.25f, 1f)]
    public float slideHeightMultiplier = 0.5f; // multiplies original CharacterController height

    [Header("Input / Touch")]
    public float swipeThreshold = 50f;         // pixels, min distance to count as swipe

    // internal
    CharacterController controller;
    Animator animator;

    int currentLane = 1; // 0 = left, 1 = center, 2 = right
    float verticalVelocity = 0f;
    float initialForwardSpeed;

    // store original controller dims so we can restore after slide
    float originalControllerHeight;
    Vector3 originalControllerCenter;
    bool isSliding = false;

    // touch detection
    Vector2 touchStartPos;
    bool touchStarted = false;

    // Add these fields near top of PlayerController
    private bool isInvulnerable = false;
    private bool isStunned = false;


    [Header("Magnet Powerup")]
    public float magnetRadius = 8f;     // how far it attracts coins
    public float magnetSpeed = 10f;     // how fast coins move toward player
    private bool isMagnetActive = false;


    [Header("Power-ups")]
    public GameObject shieldVisual; // assign a shield/glow object around player
    private bool shieldActive = false;
    private float shieldTimer = 0f;

    [HideInInspector]
    public bool isFlying = false;


    [Header("Skateboard Settings")]
    private bool isSkating = false;
    private float skateTimer = 0f;

    public Transform boyMesh; // assign in Inspector


    [Header("Skateboard Settings")]
    public GameObject skateboardObject;
    public Transform skateboardAttachPoint; // empty child at player feet
    public float skateboardSpinSpeed = 360f;
    public float skateboardReturnDelay = 0.8f;
    public float skateboardReturnSpeed = 5f;

    private bool skateboardDetached = false;
    private Quaternion skateOriginalRot;
    private Vector3 skateOriginalLocalPos;


    public ChaserFollow chaser;

    private float totalDistance = 0f;
    public float Distance => totalDistance; // optional getter


    // Expose check for Obstacle
    public bool IsInvulnerable() => isInvulnerable;

    // Call this to give temporary invulnerability (non-blocking)
    public void MakeInvulnerable(float duration)
    {
        StartCoroutine(InvulnerableRoutine(duration));
    }

    private IEnumerator InvulnerableRoutine(float duration)
    {
        isInvulnerable = true;
        // optional: flash material / UI indicator here
        yield return new WaitForSeconds(duration);
        isInvulnerable = false;
    }

    // Call this to "stun" the player (stop inputs/movement) then resume and give invulnerability
    // stunDuration = how long to pause movement (e.g. match death anim length)
    // invulDuration = how long to be invulnerable after resuming
    public void StunAndRecover(float stunDuration, float invulDuration)
    {
        StartCoroutine(StunAndRecoverRoutine(stunDuration, invulDuration));
    }


    public void ActivateMagnet(float duration)
    {
        if (isMagnetActive) StopCoroutine("MagnetRoutine");
        StartCoroutine(MagnetRoutine(duration));
    }


    public void ActivateSkateboard(float duration)
    {
        if (isSkating) return;

        isSkating = true;
        skateTimer = duration;

        if (skateboardObject != null)
            skateboardObject.SetActive(true);

        if (boyMesh != null)
        {
            Vector3 localPos = boyMesh.localPosition;
            localPos.y += 0.25f; // lift slightly above board (adjust as needed)
            boyMesh.localPosition = localPos;
        }

        animator.SetBool("isSkating", true);
        MakeInvulnerable(duration);

    }




    private IEnumerator MagnetRoutine(float duration)
    {
        isMagnetActive = true;
        yield return new WaitForSeconds(duration);
        isMagnetActive = false;
    }

    private IEnumerator StunAndRecoverRoutine(float stunDuration, float invulDuration)
    {
        isStunned = true;

        // Save speed before stun
        float savedSpeed = Mathf.Max(forwardSpeed, initialForwardSpeed);

        // Stop movement
        forwardSpeed = 0f;
        verticalVelocity = 0f;

        // Play Hurt animation
        animator.ResetTrigger("Die");
        animator.SetTrigger("Hurt");

        // Wait the stun duration
        yield return new WaitForSeconds(stunDuration);

        // ? Wait until Hurt anim fully transitions back to Run
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Running"))
            yield return null;

        // Restore normal speed (at least initialForwardSpeed so not stuck slow)
        forwardSpeed = Mathf.Max(savedSpeed, initialForwardSpeed);

        isStunned = false;

        // Force animator to running state
        animator.ResetTrigger("Hurt");
        animator.SetFloat("ForwardSpeed", forwardSpeed);

        if (invulDuration > 0f)
            MakeInvulnerable(invulDuration);
    }





    void Start()
    {
        if (skateboardObject != null)
        {
            skateOriginalRot = skateboardObject.transform.localRotation;
            skateOriginalLocalPos = skateboardObject.transform.localPosition;
        }


        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        originalControllerHeight = controller.height;
        originalControllerCenter = controller.center;
        initialForwardSpeed = forwardSpeed;

        // mobile: try to cap to 60 fps for smoothness (optional)
        Application.targetFrameRate = 60;
    }

    void Update()
    {

        if (isSkating)
        {
            skateTimer -= Time.deltaTime;
            if (skateTimer <= 0f)
            {
                isSkating = false;

                if (skateboardObject != null)
                    skateboardObject.SetActive(false);

                if (boyMesh != null)
                {
                    Vector3 localPos = boyMesh.localPosition;
                    localPos.y -= 0.25f; // return to normal
                    boyMesh.localPosition = localPos;
                }

                animator.SetBool("isSkating", false);
            }
        }





        if (isFlying)
        {
            // Still allow lane change input while flying
            HandleInput();

            // Calculate lane target position
            float targetX = (currentLane - 1) * laneDistance;
            float deltaX = targetX - transform.position.x;
            float lateral = deltaX * laneChangeSpeed;

            // Move forward + horizontal (no gravity)
            Vector3 flyMove = Vector3.forward * forwardSpeed
                            + Vector3.right * lateral;

            controller.Move(flyMove * Time.deltaTime);

            // Still update animations if needed
            UpdateAnimator();
            RampSpeed();

            return;
        }





        // Stop updating if game is over
        if (isStunned || (GameManager.instance != null && GameManager.instance.IsGameOver())
       || (GameManager.instance != null && !GameManager.instance.IsGameStarted()))
            return;


        // Tick shield timer
        if (shieldActive)
        {
            shieldTimer -= Time.deltaTime;
            if (shieldTimer <= 0f)
            {
                DeactivateShield();
            }
        }



        if (isMagnetActive)
        {
            AttractCoins();
        }


        HandleInput();
        ApplyMovement();
        UpdateAnimator();
        RampSpeed();
    }

   
    public void ActivateShield(float duration)
    {
        shieldActive = true;
        shieldTimer = duration;
        MakeInvulnerable(duration); // reuse your existing invulnerability system

        if (shieldVisual != null)
            shieldVisual.SetActive(true);
    }

    private void DeactivateShield()
    {
        shieldActive = false;
        if (shieldVisual != null)
            shieldVisual.SetActive(false);
    }



    void AttractCoins()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, magnetRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Coin"))
            {
                // smoothly move coin toward player
                hit.transform.position = Vector3.MoveTowards(
                    hit.transform.position,
                    transform.position,
                    magnetSpeed * Time.deltaTime
                );
            }
        }
    }



    // ---------------------------
    // INPUT (keyboard + swipe)
    // ---------------------------
    void HandleInput()
    {
        // keyboard (editor/testing)
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            ChangeLane(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            ChangeLane(1);
        if (Input.GetKeyDown(KeyCode.Space))
            TryJump();
        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.S))
            TrySlide();

        // touch input (mobile)
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                touchStartPos = t.position;
                touchStarted = true;
            }
            else if (t.phase == TouchPhase.Ended && touchStarted)
            {
                Vector2 end = t.position;
                DetectSwipe(touchStartPos, end);
                touchStarted = false;
            }
        }
        else // also support mouse swipes in editor
        {
            if (Input.GetMouseButtonDown(0))
            {
                touchStartPos = Input.mousePosition;
                touchStarted = true;
            }
            else if (Input.GetMouseButtonUp(0) && touchStarted)
            {
                Vector2 end = (Vector2)Input.mousePosition;
                DetectSwipe(touchStartPos, end);
                touchStarted = false;
            }
        }
    }

    void DetectSwipe(Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        if (delta.magnitude < swipeThreshold) return;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            // horizontal swipe
            if (delta.x > 0) ChangeLane(1); else ChangeLane(-1);
        }
        else
        {
            // vertical swipe
            if (delta.y > 0) TryJump(); else TrySlide();
        }
    }

    void ChangeLane(int dir)
    {
        currentLane = Mathf.Clamp(currentLane + dir, 0, 2);
    }

    // ---------------------------
    // MOVEMENT
    // ---------------------------
    void ApplyMovement()
    {
        if (isStunned)
        {
            // Force stop in ALL directions
            verticalVelocity = 0f;
            controller.Move(Vector3.zero);
            return;
        }

        // target x-position based on lane
        float targetX = (currentLane - 1) * laneDistance;
        float deltaX = targetX - transform.position.x;
        float lateral = deltaX * laneChangeSpeed;

        // vertical: grounded handling
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -1f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 move = Vector3.forward * forwardSpeed
                     + Vector3.right * lateral
                     + Vector3.up * verticalVelocity;

        controller.Move(move * Time.deltaTime);

        // Add this inside Update() or ApplyMovement()
        if (GameManager.instance != null && GameManager.instance.IsGameStarted() && !GameManager.instance.IsGameOver())
        {
            totalDistance += forwardSpeed * Time.deltaTime;
            GameManager.instance.UpdateDistance(totalDistance);
        }

    }


    void TryJump()
    {
        if (controller.isGrounded && !isSliding)
        {
            // v = sqrt(2 * g * h) with gravity negative -> multiply with -1
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger("Jump");

            if (isSkating && skateboardObject != null && !skateboardDetached)
            {
                StartCoroutine(DetachAndSpinSkateboard());
            }

        }
    }

    private IEnumerator DetachAndSpinSkateboard()
    {
        skateboardDetached = true;

        // Detach skateboard so it can spin freely
        skateboardObject.transform.parent = null;

        float elapsed = 0f;
        float duration = skateboardReturnDelay;

        // Spin skateboard while player is in air
        // Cache initial forward speed so the board keeps moving with player
        Vector3 skateVelocity = transform.forward * (forwardSpeed * 0.8f);

        // Spin + move forward while detached
        while (elapsed < duration)
        {
            // keep moving forward roughly with player
            skateboardObject.transform.position += skateVelocity * Time.deltaTime;

            // spin
            skateboardObject.transform.Rotate(Vector3.forward * skateboardSpinSpeed * Time.deltaTime, Space.Self);

            elapsed += Time.deltaTime;
            yield return null;
        }


        // Wait until player lands
        while (!controller.isGrounded)
            yield return null;

        // Smoothly reattach skateboard to player feet
        Vector3 startPos = skateboardObject.transform.position;
        Quaternion startRot = skateboardObject.transform.rotation;

        Vector3 targetPos = skateboardAttachPoint.position;
        Quaternion targetRot = skateOriginalRot;

        elapsed = 0f;
        while (elapsed < (1f / skateboardReturnSpeed))
        {
            skateboardObject.transform.position = Vector3.Lerp(startPos, targetPos, elapsed * skateboardReturnSpeed);
            skateboardObject.transform.rotation = Quaternion.Slerp(startRot, targetRot, elapsed * skateboardReturnSpeed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Final snap + re-parent
        skateboardObject.transform.SetParent(skateboardAttachPoint);
        skateboardObject.transform.localPosition = skateOriginalLocalPos;
        skateboardObject.transform.localRotation = skateOriginalRot;

        skateboardDetached = false;
    }


    void TrySlide()
    {
        if (controller.isGrounded && !isSliding)
            StartCoroutine(DoSlide());
    }

    IEnumerator DoSlide()
    {
        isSliding = true;
        if (animator != null) animator.SetTrigger("Slide");

        // reduce height & shift center downward so feet remain on ground
        float newHeight = originalControllerHeight * slideHeightMultiplier;
        float centerY = originalControllerCenter.y - (originalControllerHeight - newHeight) / 2f;

        controller.height = newHeight;
        controller.center = new Vector3(originalControllerCenter.x, centerY, originalControllerCenter.z);

        // optional: slightly increase forward speed during slide
        float savedSpeed = forwardSpeed;
        // forwardSpeed += 1f; // uncomment if you want slide speed burst

        yield return new WaitForSeconds(slideDuration);

        // restore
        controller.height = originalControllerHeight;
        controller.center = originalControllerCenter;
        forwardSpeed = savedSpeed;
        isSliding = false;
    }

    // ---------------------------
    // ANIMATOR + SPEED RAMP
    // ---------------------------
    void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat("ForwardSpeed", forwardSpeed);
        animator.SetBool("isGrounded", controller.isGrounded);
        animator.SetBool("isSliding", isSliding);
    }

    void RampSpeed()
    {
        // increase forward speed slowly over time, clamped to maxSpeed
        forwardSpeed = Mathf.Clamp(forwardSpeed + speedIncreaseRate * Time.deltaTime, initialForwardSpeed, maxSpeed);
    }
}
