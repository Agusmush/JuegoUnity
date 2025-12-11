using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallPhysics : MonoBehaviour
{
    [Header("Gravedad Arcade")]
    // 1 = Gravedad normal. 3 = Cae como piedra (Estilo Rocket League).
    public float gravityMultiplier = 3.0f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // Aplicamos gravedad extra manualmente
        // Physics.gravity.y suele ser -9.81
        Vector3 extraGravity = Physics.gravity * (gravityMultiplier - 1f);
        rb.AddForce(extraGravity, ForceMode.Acceleration);
    }
}