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
    public float cameraCollisionRadius = 0.2f; // NUEVO: Radio de la "esfera" de la cámara

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

        Vector3 momentum = Vector3.zero;
        if (controller != null) momentum = controller.velocity;

        if (playerLook != null)
        {
            playerLook.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (playerMotor != null) playerMotor.enabled = false;

        // IMPORTANTE: Al apagar esto, el PlayerAudio dejará de sonar (ver script de audio abajo)
        if (controller != null) controller.enabled = false;

        if (physicsCollider != null) physicsCollider.enabled = true;

        if (mainRb != null)
        {
            mainRb.isKinematic = false;
            mainRb.useGravity = true;
            mainRb.constraints = RigidbodyConstraints.None;
            mainRb.mass = 80f;
            mainRb.drag = 0.05f;
            mainRb.angularDrag = 0.05f;
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

        // 1. Dirección y Objetivo Base
        Vector3 dirFromKiller = (ragdollCenter - lastKillerPos).normalized;
        if (dirFromKiller == Vector3.zero) dirFromKiller = transform.forward;

        Vector3 targetPos = ragdollCenter + (dirFromKiller * cameraDistance) + (Vector3.up * cameraHeight);

        // --- NOTA: YA NO APLICAMOS EL SHAKE AQUÍ ---

        // 2. Suavizado de Movimiento (SmoothDamp)
        Vector3 smoothedPos = Vector3.SmoothDamp(
            Camera.main.transform.position,
            targetPos,
            ref currentVelRef,
            positionDamp
        );

        // 3. Colisión con Paredes (SphereCast)
        RaycastHit hit;
        Vector3 dirToCamera = smoothedPos - ragdollCenter;
        float distToCamera = dirToCamera.magnitude;

        // IMPORTANTE: Nos aseguramos de que el SphereCast NO choque con el ragdoll (capa Player o Default)
        // Usamos la máscara wallLayers que definiste. Asegúrate en el inspector que "Player" NO esté marcado en wallLayers.
        Vector3 finalPos = smoothedPos;

        if (Physics.SphereCast(ragdollCenter, cameraCollisionRadius, dirToCamera.normalized, out hit, distToCamera, wallLayers))
        {
            finalPos = hit.point + (hit.normal * wallOffset);
        }

        // 4. ELIMINAMOS EL SHAKE DE POSICIÓN
        // Vector3 finalPos = smoothedPos; <--- Ya no sumamos shake aquí
        Camera.main.transform.position = finalPos; // Posición limpia y segura contra paredes

        // 5. APLICAR SHAKE DE ROTACIÓN
        Quaternion targetLook = Quaternion.LookRotation(lastKillerPos - Camera.main.transform.position);

        // Calculamos la rotación base suave
        Quaternion smoothedRotation = Quaternion.Slerp(
            Camera.main.transform.rotation,
            targetLook,
            Time.deltaTime * rotationSpeed
        );

        // Le sumamos el terremoto
        if (CameraShake.Instance != null)
        {
            // Convertimos el Vector3 de shake a Quaternion y lo multiplicamos
            Quaternion shakeRot = Quaternion.Euler(CameraShake.Instance.CurrentShakeRotation);
            Camera.main.transform.rotation = smoothedRotation * shakeRot;
        }
        else
        {
            Camera.main.transform.rotation = smoothedRotation;
        }
    }

    public void DeactivateRagdoll()
    {
        isRagdollActive = false;

        if (physicsCollider != null) physicsCollider.enabled = false;

        if (mainRb != null)
        {
            mainRb.velocity = Vector3.zero;
            mainRb.angularVelocity = Vector3.zero;
            mainRb.Sleep();
            mainRb.isKinematic = true;
            mainRb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        Vector3 currentRot = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, currentRot.y, 0f);

        if (Camera.main != null)
        {
            Camera.main.transform.SetParent(transform);
            Camera.main.transform.localPosition = new Vector3(0, 0.8f, 0);
            Camera.main.transform.localRotation = Quaternion.identity;
        }
    }
}