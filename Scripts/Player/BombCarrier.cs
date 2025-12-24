using UnityEngine;
using System.Collections;

public class BombCarrier : MonoBehaviour
{
    [Header("Configuración")]
    public Transform holdPoint;
    public float grabRange = 2.5f;
    public LayerMask bombLayer;

    [Header("Ajuste Visual de Agarre")]
    // X: Lados, Y: Altura, Z: Profundidad (Alejar del cuerpo)
    // Prueba con (0, -0.2, 0.5) para alejarla medio metro y bajarla un poco
    public Vector3 holdPositionOffset = new Vector3(0f, -0.2f, 0.5f);
    public Vector3 holdRotationOffset = Vector3.zero; // Por si quieres inclinarla

    [Header("Lanzamiento")]
    public float throwForce = 8f;

    [Header("Penalizaciones al cargar")]
    [Range(0.1f, 1f)] public float carrySpeedPenalty = 0.8f;
    [Range(0.1f, 1f)] public float carryJumpPenalty = 0.5f;

    [Header("Estado")]
    public bool isCarrying = false;
    private BombController currentBomb;
    private PlayerMotor motor;
    private CharacterController myCollider;

    private void Start()
    {
        motor = GetComponent<PlayerMotor>();
        myCollider = GetComponent<CharacterController>();
    }

    public void OnGrabInput()
    {
        if (!isCarrying)
        {
            TryGrabBomb();
        }
        else
        {
            DropBomb(throwForce);
        }
    }

    private void TryGrabBomb()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward, grabRange, bombLayer);

        foreach (var hit in hits)
        {
            BombController bomb = hit.GetComponent<BombController>();
            if (bomb != null)
            {
                GrabBomb(bomb);
                return;
            }
        }
    }

    private void GrabBomb(BombController bomb)
    {
        currentBomb = bomb;
        isCarrying = true;

        Rigidbody rb = bomb.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // Parentesco
        bomb.transform.SetParent(holdPoint);

        // --- APLICAMOS EL OFFSET AQUÍ ---
        // En lugar de Vector3.zero, usamos tu configuración personalizada
        bomb.transform.localPosition = holdPositionOffset;
        bomb.transform.localRotation = Quaternion.Euler(holdRotationOffset);

        if (motor != null)
        {
            motor.speedFactor = carrySpeedPenalty;
            motor.jumpFactor = carryJumpPenalty;
        }
    }

    public void DropBomb(float force = 0f)
    {

        if (!isCarrying || currentBomb == null) return;

        BombController bombToDrop = currentBomb;

        bombToDrop.transform.SetParent(null);

        Rigidbody rb = bombToDrop.GetComponent<Rigidbody>();
        Collider bombCol = bombToDrop.GetComponent<Collider>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;

            // --- AQUÍ ESTÁ EL CAMBIO ---
            if (myCollider != null && bombCol != null)
            {
                // Iniciamos la espera inteligente
                StartCoroutine(WaitForSeparation(myCollider, bombCol));
            }

            if (force > 0)
            {
                rb.AddForce(holdPoint.forward * force, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
            }
        }

        currentBomb = null;
        isCarrying = false;

        if (motor != null) 
        {
            motor.speedFactor = 1f;
            motor.jumpFactor = 1f;
        }
    }

    IEnumerator ReEnableCollisionDelay(Collider player, Collider bomb)
    {
        yield return new WaitForSeconds(0.6f);
        if (player != null && bomb != null)
        {
            Physics.IgnoreCollision(player, bomb, false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (holdPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + transform.forward, grabRange);
        }
    }

    IEnumerator WaitForSeparation(Collider player, Collider bomb)
    {
        // 1. Ignoramos colisión al inicio
        Physics.IgnoreCollision(player, bomb, true);

        // 2. Esperamos HASTA que los colliders dejen de tocarse
        // Usamos bounds.Intersects para saber si siguen superpuestos
        float safetyTimer = 0f;
        while (player != null && bomb != null && player.bounds.Intersects(bomb.bounds))
        {
            // Si pasan más de 3 segundos y siguen pegados (bug raro), cortamos para evitar bucle infinito
            safetyTimer += Time.deltaTime;
            if (safetyTimer > 3f) break;

            yield return null; // Esperamos al siguiente frame
        }

        // 3. Ya no se tocan (o pasó el tiempo límite): Reactivamos colisión
        if (player != null && bomb != null)
        {
            Physics.IgnoreCollision(player, bomb, false);
        }
    }
}