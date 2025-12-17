using UnityEngine;

public class BombCarrier : MonoBehaviour
{
    [Header("Configuración")]
    public Transform holdPoint;
    public float grabRange = 2.5f;
    public LayerMask bombLayer;
    public float throwForce = 10f;

    [Header("Estado")]
    public bool isCarrying = false;
    private BombController currentBomb;
    private PlayerMotor motor;

    private void Start()
    {
        motor = GetComponent<PlayerMotor>();
    }

    // Update ya no es necesario para "forzar" posición, 
    // Unity maneja la jerarquía padre-hijo automáticamente.

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

        // Físicas: Desactivar para que no pese ni choque
        Rigidbody rb = bomb.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // Parentesco: La bomba se vuelve hija de la mano
        bomb.transform.SetParent(holdPoint);
        bomb.transform.localPosition = Vector3.zero;
        bomb.transform.localRotation = Quaternion.identity;

        // Peso en jugador
        if (motor != null) motor.speedFactor = 0.8f;
    }

    public void DropBomb(float force = 0f)
    {
        if (!isCarrying || currentBomb == null) return;

        // Romper parentesco
        currentBomb.transform.SetParent(null);

        // Reactivar físicas
        Rigidbody rb = currentBomb.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;

            if (force > 0)
            {
                rb.AddForce(holdPoint.forward * force, ForceMode.Impulse);
            }
        }

        currentBomb = null;
        isCarrying = false;

        // Restaurar velocidad
        if (motor != null) motor.speedFactor = 1f;
    }

    private void OnDrawGizmosSelected()
    {
        if (holdPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + transform.forward, grabRange);
        }
    }
}