using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Configuración Patada")]
    public float kickRange = 3.5f;
    public float kickForce = 10f;
    public float kickCooldown = 0.6f;
    public LayerMask interactLayers; // Default, Player (Bomba)

    [Header("Referencias")]
    public Transform cameraHolder;

    private float lastKickTime;

    // Esta función la llama el InputManager al presionar 'F'
    public void OnKickInput()
    {
        // Verificamos el Cooldown aquí
        if (Time.time >= lastKickTime + kickCooldown)
        {
            PerformKick();
        }
    }

    void PerformKick()
    {
        lastKickTime = Time.time;

        // Aquí puedes poner: animator.SetTrigger("Kick");

        RaycastHit hit;
        if (Physics.SphereCast(cameraHolder.position, 0.5f, cameraHolder.forward, out hit, kickRange, interactLayers))
        {
            // 1. Patear Bomba / Objetos Físicos
            Rigidbody targetRb = hit.collider.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                Vector3 forceDir = (hit.point - cameraHolder.position).normalized;
                forceDir += Vector3.up * 0.3f; // Levantar un poco
                forceDir.Normalize();

                targetRb.AddForce(forceDir * kickForce, ForceMode.Impulse);
            }

            // 2. Empujar Enemigos
            PlayerMotor enemyMotor = hit.collider.GetComponent<PlayerMotor>();
            if (enemyMotor != null)
            {
                Vector3 pushDir = cameraHolder.forward;
                pushDir.y = 0.2f;
                enemyMotor.AddExplosionForce(pushDir, 500f);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (cameraHolder != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(cameraHolder.position + cameraHolder.forward * kickRange, 0.5f);
        }
    }
}