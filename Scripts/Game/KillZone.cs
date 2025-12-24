using UnityEngine;

public class KillZone : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Lugar donde reaparecerá la bomba si cae aquí. Si lo dejas vacío, intentará usar el del GameManager.")]
    public Transform specificBombRespawnPoint;

    private void OnTriggerEnter(Collider other)
    {
        // --- CASO 1: JUGADOR ---
        // Buscamos si el objeto es un jugador o parte de él
        PlayerHealth playerParams = other.GetComponentInParent<PlayerHealth>();

        if (playerParams != null)
        {
            // Solo matamos si está vivo para evitar errores con ragdolls muertos cayendo
            if (playerParams.currentHealth > 0)
            {
                Debug.Log($"Jugador {other.name} cayó al vacío.");
                playerParams.TakeDamage(9999f, null); // Muerte instantánea
            }
            return; // Salimos, ya procesamos esto
        }

        // --- CASO 2: BOMBA ---
        // Buscamos si es la bomba
        BombController bomb = other.GetComponentInParent<BombController>();

        if (bomb != null)
        {
            Debug.Log("¡La bomba cayó al vacío! Respawneando...");
            ResetBombPosition(bomb);
        }
    }

    private void ResetBombPosition(BombController bomb)
    {
        // 1. Determinar posición de destino
        Vector3 targetPos = Vector3.zero;

        if (specificBombRespawnPoint != null)
        {
            targetPos = specificBombRespawnPoint.position;
        }
        else if (GameManager.Instance != null && GameManager.Instance.spawnPointBomb != null)
        {
            // Usamos el spawn global definido en el GameManager
            targetPos = GameManager.Instance.spawnPointBomb.position;
        }
        else
        {
            targetPos = new Vector3(0, 5, 0); // Fallback por si todo falla
        }

        // 2. Manipular Físicas (IMPORTANTE)
        // Hay que frenarla en seco, si no conservará la velocidad de caída y saldrá disparada hacia abajo al respawnear.
        Rigidbody rb = bomb.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 3. Teletransporte
        bomb.transform.position = targetPos;

        // Opcional: ¿Quieres resetear el timer de la bomba si cae? 
        // Por ahora solo reseteamos posición para que siga la tensión.
    }
}