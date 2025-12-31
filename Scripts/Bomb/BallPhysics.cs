using UnityEngine;
using Unity.Netcode; // Necesario

[RequireComponent(typeof(Rigidbody))]
public class BallPhysics : NetworkBehaviour // Cambiamos a NetworkBehaviour
{
    [Header("Gravedad Arcade")]
    public float gravityMultiplier = 3.0f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // REGLA DE ORO: Solo el servidor toca las físicas de un objeto de red
        if (!IsServer) return;

        if (rb != null)
        {
            Vector3 extraGravity = Physics.gravity * (gravityMultiplier - 1f);
            rb.AddForce(extraGravity, ForceMode.Acceleration);
        }
    }
}