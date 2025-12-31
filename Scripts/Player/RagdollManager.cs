using UnityEngine;
using Unity.Netcode.Components;

public class RagdollManager : MonoBehaviour
{
    [Header("Referencias")]
    public CharacterController controller;
    public Rigidbody mainRb;
    public MonoBehaviour playerLook;
    public CapsuleCollider physicsCollider;
    public MonoBehaviour playerMotor;

    [Header("Configuración Killcam")]
    public float cameraDistance = 15f;
    public float cameraHeight = 0.5f;
    public float positionDamp = 0.1f;
    public float rotationSpeed = 10f;
    public LayerMask wallLayers;

    private bool isRagdollActive = false;
    private bool isMineLocal = false;
    private Vector3 currentVelRef;
    private Vector3 killCamFocus;

    private NetworkTransform netTransform;
    private NetworkRigidbody netRigidbody;
    private Animator anim;

    void Awake()
    {
        netTransform = GetComponent<NetworkTransform>();
        netRigidbody = GetComponent<NetworkRigidbody>();
        anim = GetComponent<Animator>();
        if (controller == null) controller = GetComponent<CharacterController>();
        if (mainRb == null) mainRb = GetComponent<Rigidbody>();
        if (playerLook == null) playerLook = GetComponent("PlayerLook") as MonoBehaviour;
        if (playerMotor == null) playerMotor = GetComponent("PlayerMotor") as MonoBehaviour;
        if (physicsCollider != null) physicsCollider.enabled = false;
    }

    void LateUpdate()
    {
        if (!isRagdollActive || !isMineLocal) return;
        UpdateKillCam();
    }

    public void ActivateRagdoll(DamageInfo info, bool isMine)
    {
        isMineLocal = isMine;
        isRagdollActive = true;

        if (anim != null) anim.enabled = false;
        if (netTransform != null) netTransform.enabled = false;
        if (netRigidbody != null) netRigidbody.enabled = false;
        if (playerMotor != null) playerMotor.enabled = false;
        if (controller != null) controller.enabled = false;

        // CORRECCIÓN 1: Usar PointOfImpact en lugar de Point
        killCamFocus = info.IsExplosion ? info.PointOfImpact : (transform.position - info.ForceDirection * 5f);

        if (isMineLocal)
        {
            if (playerLook != null) playerLook.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (Camera.main != null) Camera.main.transform.parent = null;
        }

        if (physicsCollider != null) physicsCollider.enabled = true;

        if (mainRb != null)
        {
            mainRb.isKinematic = false;
            mainRb.useGravity = true;
            mainRb.constraints = RigidbodyConstraints.None;
            mainRb.mass = 80f;
            mainRb.drag = 0.05f;
            mainRb.velocity = Vector3.zero;

            if (info.IsExplosion)
            {
                // CORRECCIÓN 2: Usar ForceMagnitude y PointOfImpact
                mainRb.AddExplosionForce(info.ForceMagnitude, info.PointOfImpact, info.ExplosionRadius, 1f, ForceMode.Impulse);
                mainRb.AddTorque(Random.insideUnitSphere * (info.ForceMagnitude * 0.05f), ForceMode.Impulse);
            }
            else
            {
                // CORRECCIÓN 3: Usar ForceDirection, ForceMagnitude y PointOfImpact
                mainRb.AddForceAtPosition(info.ForceDirection.normalized * info.ForceMagnitude, info.PointOfImpact, ForceMode.Impulse);
            }
        }
    }

    private void UpdateKillCam()
    {
        if (Camera.main == null) return;
        Vector3 center = transform.position + Vector3.up;

        Vector3 dirFromFocus = (center - killCamFocus).normalized;
        if (dirFromFocus == Vector3.zero) dirFromFocus = transform.forward;

        Vector3 targetPos = center + (dirFromFocus * cameraDistance) + (Vector3.up * cameraHeight);
        Vector3 smoothedPos = Vector3.SmoothDamp(Camera.main.transform.position, targetPos, ref currentVelRef, positionDamp);

        RaycastHit hit;
        if (Physics.SphereCast(center, 0.2f, (smoothedPos - center).normalized, out hit, Vector3.Distance(center, smoothedPos), wallLayers))
        {
            smoothedPos = hit.point + (hit.normal * 0.3f);
        }

        Camera.main.transform.position = smoothedPos;
        Quaternion targetLook = Quaternion.LookRotation(killCamFocus - Camera.main.transform.position);
        Camera.main.transform.rotation = Quaternion.Slerp(Camera.main.transform.rotation, targetLook, Time.deltaTime * rotationSpeed);
    }

    public void DeactivateRagdoll(bool isMine)
    {
        isRagdollActive = false;
        isMineLocal = false;

        if (anim != null) anim.enabled = true;
        if (netTransform != null) netTransform.enabled = true;
        if (netRigidbody != null) netRigidbody.enabled = true;
        if (physicsCollider != null) physicsCollider.enabled = false;

        if (mainRb != null)
        {
            mainRb.velocity = Vector3.zero;
            mainRb.isKinematic = true;
            mainRb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);

        if (isMine && Camera.main != null)
        {
            Camera.main.transform.SetParent(transform);
            Camera.main.transform.localPosition = new Vector3(0, 0.6f, 0);
            Camera.main.transform.localRotation = Quaternion.identity;
        }
    }
}