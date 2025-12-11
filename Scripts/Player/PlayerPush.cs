using UnityEngine;

public class PlayerPush : MonoBehaviour
{
    [Header("Configuración de Empuje")]
    public float pushPower = 2.0f; // Fuerza del empujón corporal
    public float weight = 6.0f;    // Peso aparente del jugador al chocar

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        // 1. Validaciones: Que tenga RB y no sea cinemático
        if (body == null || body.isKinematic) return;

        // 2. No empujar hacia abajo (pisar la bomba)
        if (hit.moveDirection.y < -0.3f) return;

        // 3. Calcular dirección de empuje
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

        // 4. Aplicar fuerza
        // Usamos la velocidad del jugador para determinar qué tan fuerte pega
        body.velocity = pushDir * pushPower;

        // Alternativa si quieres algo más suave:
        // body.AddForce(pushDir * pushPower * 10f, ForceMode.Force);
    }
}