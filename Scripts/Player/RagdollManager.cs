using UnityEngine;

public class RagdollManager : MonoBehaviour
{
    [Header("Referencias")]
    public CharacterController controller;
    public Rigidbody mainRb;
    public PlayerLook playerLook;
    public CapsuleCollider physicsCollider;
    private PlayerMotor playerMotor;

    [Header("Configuración Killcam (Estilo TF2)")]
    public float cameraDistance = 15f;
    public float cameraHeight = 0.5f;
    public float positionDamp = 0.1f;
    public float rotationSpeed = 10f;

    [Header("Colisión de Cámara")]
    public LayerMask wallLayers;
    public float wallOffset = 0.3f;

    // Estado interno
    private bool isRagdollActive = false;
    private Transform currentKiller;
    private Vector3 lastKillerPos;
    private Vector3 currentVelRef;

    void Start()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        if (mainRb == null) mainRb = GetComponent<Rigidbody>();
        if (playerLook == null) playerLook = GetComponent<PlayerLook>();
        playerMotor = GetComponent<PlayerMotor>();

        if (physicsCollider != null) physicsCollider.enabled = false;
    }

    void LateUpdate()
    {
        if (!isRagdollActive) return;
        UpdateKillCam();
    }

    public void ActivateRagdoll(Transform killer)
    {
        currentKiller = killer;
        if (currentKiller != null) lastKillerPos = currentKiller.position;
        else lastKillerPos = transform.position + transform.forward * 5f;

        // 1. CAPTURAR VELOCIDAD
        Vector3 momentum = Vector3.zero;
        if (controller != null) momentum = controller.velocity;

        // 2. Desactivar controles
        if (playerLook != null)
        {
            playerLook.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (playerMotor != null) playerMotor.enabled = false;
        if (controller != null) controller.enabled = false;

        // 3. Activar Físicas
        if (physicsCollider != null) physicsCollider.enabled = true;

        if (mainRb != null)
        {
            mainRb.isKinematic = false;
            mainRb.useGravity = true;
            mainRb.constraints = RigidbodyConstraints.None;
            mainRb.mass = 80f;
            mainRb.drag = 0.05f;
            mainRb.angularDrag = 0.05f;

            // 4. APLICAR MOMENTUM
            mainRb.velocity = momentum;

            mainRb.AddTorque(Random.insideUnitSphere * 20f, ForceMode.Impulse);
            mainRb.AddForce(Vector3.up * 2f, ForceMode.Impulse);
        }

        if (Camera.main != null) Camera.main.transform.parent = null;

        isRagdollActive = true;
    }

    private void UpdateKillCam()
    {
        if (Camera.main == null) return;

        if (currentKiller != null) lastKillerPos = currentKiller.position;

        Vector3 ragdollCenter = transform.position + Vector3.up * 1.0f;

        Vector3 dirFromKiller = (ragdollCenter - lastKillerPos).normalized;
        if (dirFromKiller == Vector3.zero) dirFromKiller = transform.forward;

        Vector3 desiredPos = ragdollCenter + (dirFromKiller * cameraDistance) + (Vector3.up * cameraHeight);

        RaycastHit hit;
        Vector3 rayDir = desiredPos - ragdollCenter;
        float dist = rayDir.magnitude;

        if (Physics.Raycast(ragdollCenter, rayDir.normalized, out hit, dist, wallLayers))
        {
            desiredPos = hit.point - (rayDir.normalized * wallOffset);
        }

        if (CameraShake.Instance != null)
        {
            desiredPos += CameraShake.Instance.CurrentShakeOffset;
        }

        Camera.main.transform.position = Vector3.SmoothDamp(
            Camera.main.transform.position,
            desiredPos,
            ref currentVelRef,
            positionDamp
        );

        Quaternion targetRotation = Quaternion.LookRotation(lastKillerPos - Camera.main.transform.position);
        Camera.main.transform.rotation = Quaternion.Slerp(
            Camera.main.transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSpeed
        );
    }
    public void DeactivateRagdoll()
    {
        isRagdollActive = false;

        // 1. Apagar colisionador físico (cápsula del ragdoll)
        if (physicsCollider != null) physicsCollider.enabled = false;

        // 2. DOMAR AL RIGIDBODY
        if (mainRb != null)
        {
            mainRb.velocity = Vector3.zero;
            mainRb.angularVelocity = Vector3.zero;
            mainRb.Sleep();
            mainRb.isKinematic = true;

            // --- LA SOLUCIÓN NUCLEAR ---
            // Le prohibimos rotar. Si la física intenta inclinarlo, Unity lo ignorará.
            mainRb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        // 3. ENDEREZADO MANUAL
        // Reseteamos la rotación para que quede perfectamente vertical
        Vector3 currentRot = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, currentRot.y, 0f);

        // 4. RESETEO DE CÁMARA
        if (Camera.main != null)
        {
            Camera.main.transform.SetParent(transform);
            Camera.main.transform.localPosition = new Vector3(0, 0.8f, 0);
            Camera.main.transform.localRotation = Quaternion.identity;
        }
    }
}