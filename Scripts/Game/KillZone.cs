using UnityEngine;
using Unity.Netcode;

public class KillZone : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Lugar donde reaparecerá la bomba si cae aquí. Si lo dejas vacío, intentará usar el del GameManager.")]
    public Transform specificBombRespawnPoint;

    private void OnTriggerEnter(Collider other)
    {
        // Solo el Servidor tiene autoridad
        if (!NetworkManager.Singleton.IsServer) return;

        // --- CASO 1: JUGADOR ---
        PlayerHealth playerParams = other.GetComponentInParent<PlayerHealth>();

        if (playerParams != null)
        {
            if (!playerParams.isDead)
            {
                Debug.Log($"Servidor: Jugador {other.name} cayó al vacío.");

                // --- CORRECCIÓN: USAR EL NUEVO SISTEMA DE DAÑO ---
                // Creamos un daño de 9999, con fuerza 0 (porque solo cae)
                DamageInfo fallDamage = new DamageInfo(
                    9999f,                      // Daño mortal
                    other.transform.position,   // Punto
                    Vector3.zero,               // Dirección (irrelevante)
                    0f,                         // Fuerza 0 (para que el ragdoll no salga volando, solo caiga)
                    false,                      // No es explosión
                    0f,                         // Radio 0
                    9999                        // ID 9999 = Entorno / Vacío
                );

                playerParams.TakeDamage(fallDamage);
            }
            return;
        }

        // --- CASO 2: BOMBA ---
        BombController bomb = other.GetComponentInParent<BombController>();

        if (bomb != null)
        {
            Debug.Log("Servidor: ¡La bomba cayó al vacío! Respawneando...");
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
            targetPos = GameManager.Instance.spawnPointBomb.position;
        }
        else
        {
            targetPos = new Vector3(0, 5, 0);
        }

        // 2. Manipular Físicas
        Rigidbody rb = bomb.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 3. Teletransporte
        bomb.transform.position = targetPos;
    }
}