using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    private float shakeDuration = 0f;
    private float shakeMagnitude = 0f;
    private float dampingSpeed = 1.0f;

    // Ahora esto representa ROTACIÓN (X=Pitch, Y=Yaw, Z=Roll)
    public Vector3 CurrentShakeRotation { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (shakeDuration > 0)
        {
            // Generamos ruido Perlin o Random para la rotación
            // X (Arriba/Abajo), Y (Izquierda/Derecha), Z (Inclinación - Muy importante para explosiones)
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;
            float z = Random.Range(-1f, 1f) * shakeMagnitude * 1.5f; // El Roll suele ser más fuerte

            CurrentShakeRotation = new Vector3(x, y, z);

            shakeDuration -= Time.deltaTime * dampingSpeed;
        }
        else
        {
            CurrentShakeRotation = Vector3.zero;
        }
    }

    public void TriggerShake(float duration, float magnitude)
    {
        shakeDuration = duration;
        // La magnitud para rotación suele necesitar ser mayor que para posición.
        // Si antes usabas 0.5, ahora prueba con 5.0 o 10.0 grados.
        shakeMagnitude = magnitude;
        dampingSpeed = 2.0f; // Un poco más rápido para que vibre
    }
}