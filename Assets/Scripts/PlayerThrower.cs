using System.Collections.Generic;
using UnityEngine;

public class PlayerThrower : MonoBehaviour
{
    [Header("Movement & Look")]
    public float walkSpeed = 3.5f;
    public float mouseSensitivity = 2f;
    public float gravity = -9.81f;
    public float minPitch = -35f;
    public float maxPitch = 45f;
    public Transform cameraTransform;

    [Header("Bin Aim (Optional)")]
    public Transform binTarget;
    public float binAimHeightOffset = 0.35f;
    public float aimSnapSpeed = 8f;

    [Header("Paper (P = Spawn, Space = Throw)")]
    public GameObject paperPrefab;
    public Transform throwPoint;
    public float paperScaleInHand = 5f;
    public float throwForce = 3f;
    public float upwardArc = 1.2f;

    [Header("Wind Resistance (Slows After Delay)")]
    public float slowdownDelay = 0.7f;
    public float baseLinearDamping = 0.15f;
    public float airDragRampSpeed = 2.0f;
    public float maxLinearDamping = 3.0f;

    [Header("Dotted Trajectory Guide")]
    public int dotCount = 16;
    public int fixedStepsPerDot = 4;
    public float dotSize = 0.14f;
    public Color dotColor = Color.yellow;

    private CharacterController controller;
    private float pitch;
    private float verticalVelocity;
    private Rigidbody heldPaperRb;
    private bool isBinAiming;

    private readonly List<Transform> guideDots = new List<Transform>();
    private readonly List<PaperFlightTracker> activeFlights = new List<PaperFlightTracker>();

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        BuildVisibleGuideDots();
        SetGuideDotsVisible(false);
    }

    private void OnEnable()
    {
        LockCursor(true);
    }

    private void OnDisable()
    {
        LockCursor(false);
    }

    private void Update()
    {
        HandleCursorLock();
        HandleLookAndMove();
        HandleAimToggle();

        if (Input.GetKeyDown(KeyCode.P) && heldPaperRb == null)
            SpawnPaperInHand();

        if (Input.GetKeyDown(KeyCode.Space) && heldPaperRb != null)
            ThrowHeldPaper();

        UpdateGuideDots();
        UpdateActiveFlights();
    }

    private void HandleCursorLock()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            LockCursor(false);

        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            LockCursor(true);
    }

    private void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void HandleLookAndMove()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * 100f * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * 100f * Time.deltaTime;

        transform.Rotate(Vector3.up * mouseX);
        pitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = (transform.right * h + transform.forward * v).normalized * walkSpeed;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }

    private void HandleAimToggle()
    {
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetMouseButtonDown(1))
            isBinAiming = !isBinAiming;
    }

    private void SpawnPaperInHand()
    {
        if (paperPrefab == null || throwPoint == null) return;

        GameObject paper = Instantiate(paperPrefab, throwPoint);
        paper.transform.localPosition = Vector3.zero;
        paper.transform.localRotation = Quaternion.identity;
        paper.transform.localScale = Vector3.one * paperScaleInHand;

        Collider col = paper.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        heldPaperRb = paper.GetComponent<Rigidbody>();
        if (heldPaperRb != null)
        {
            heldPaperRb.isKinematic = true;
            heldPaperRb.useGravity = false;
            heldPaperRb.linearDamping = baseLinearDamping;
        }
    }

    private void ThrowHeldPaper()
    {
        Vector3 impulse = GetThrowImpulse();

        heldPaperRb.transform.SetParent(null);
        heldPaperRb.isKinematic = false;
        heldPaperRb.useGravity = true;
        heldPaperRb.linearDamping = baseLinearDamping;

        Collider col = heldPaperRb.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        heldPaperRb.linearVelocity = Vector3.zero;
        heldPaperRb.angularVelocity = Vector3.zero;
        heldPaperRb.AddForce(impulse, ForceMode.Impulse);

        activeFlights.Add(new PaperFlightTracker
        {
            rb = heldPaperRb,
            airTime = 0f
        });

        heldPaperRb = null;
        SetGuideDotsVisible(false);
    }

    private Vector3 GetThrowImpulse()
    {
        Vector3 origin = throwPoint != null ? throwPoint.position : transform.position + Vector3.up;
        Vector3 dir = cameraTransform != null ? cameraTransform.forward : transform.forward;

        if (isBinAiming && binTarget != null)
        {
            Vector3 targetPos = binTarget.position + Vector3.up * binAimHeightOffset;
            Vector3 toBin = (targetPos - origin).normalized;
            dir = Vector3.Slerp(dir, toBin, Time.deltaTime * aimSnapSpeed).normalized;
        }

        return (dir.normalized * throwForce) + (Vector3.up * upwardArc);
    }

    private void BuildVisibleGuideDots()
    {
        Shader unlitShader = Shader.Find("Unlit/Color");
        if (unlitShader == null)
            unlitShader = Shader.Find("Universal Render Pipeline/Unlit");

        Material sharedDotMat = unlitShader != null ? new Material(unlitShader) : null;
        if (sharedDotMat != null && sharedDotMat.HasProperty("_Color"))
            sharedDotMat.SetColor("_Color", dotColor);
        else if (sharedDotMat != null && sharedDotMat.HasProperty("_BaseColor"))
            sharedDotMat.SetColor("_BaseColor", dotColor);

        for (int i = 0; i < dotCount; i++)
        {
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = $"TrajectoryDot_{i}";
            dot.transform.SetParent(null);
            dot.transform.localScale = Vector3.one * dotSize;

            Collider col = dot.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = dot.GetComponent<Renderer>();
            if (rend != null && sharedDotMat != null)
                rend.sharedMaterial = sharedDotMat;

            guideDots.Add(dot.transform);
        }
    }

    private void UpdateGuideDots()
    {
        bool hasPaperInHand = heldPaperRb != null && throwPoint != null;
        SetGuideDotsVisible(hasPaperInHand);
        if (!hasPaperInHand) return;

        float dt = Time.fixedDeltaTime;
        float mass = (heldPaperRb.mass > 0.0001f) ? heldPaperRb.mass : 1f;

        Vector3 simPos = throwPoint.position;
        Vector3 simVel = GetThrowImpulse() / mass;
        float simDamping = baseLinearDamping;
        float simTime = 0f;

        int stepsPerDot = Mathf.Max(1, fixedStepsPerDot);
        int totalSteps = dotCount * stepsPerDot;
        int dotIndex = 0;

        for (int step = 1; step <= totalSteps; step++)
        {
            simTime += dt;
            if (simTime > slowdownDelay)
            {
                simDamping = Mathf.MoveTowards(
                    simDamping,
                    maxLinearDamping,
                    airDragRampSpeed * dt
                );
            }

            simVel += Physics.gravity * dt;
            simVel *= Mathf.Clamp01(1f - simDamping * dt);
            simPos += simVel * dt;

            if (step % stepsPerDot == 0 && dotIndex < guideDots.Count)
            {
                guideDots[dotIndex].position = simPos;
                dotIndex++;
            }
        }
    }

    private void SetGuideDotsVisible(bool visible)
    {
        for (int i = 0; i < guideDots.Count; i++)
        {
            if (guideDots[i].gameObject.activeSelf != visible)
                guideDots[i].gameObject.SetActive(visible);
        }
    }

    private void UpdateActiveFlights()
    {
        for (int i = activeFlights.Count - 1; i >= 0; i--)
        {
            PaperFlightTracker flight = activeFlights[i];
            if (flight.rb == null)
            {
                activeFlights.RemoveAt(i);
                continue;
            }

            flight.airTime += Time.deltaTime;
            if (flight.airTime > slowdownDelay)
            {
                flight.rb.linearDamping = Mathf.MoveTowards(
                    flight.rb.linearDamping,
                    maxLinearDamping,
                    airDragRampSpeed * Time.deltaTime
                );
            }
        }
    }

    private class PaperFlightTracker
    {
        public Rigidbody rb;
        public float airTime;
    }
}