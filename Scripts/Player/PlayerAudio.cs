using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [Header("Configuración por Distancia")]
    [Tooltip("Distancia en metros para dar un paso. Auméntalo si suenan muy rápido.")]
    public float stepDistance = 1.8f;

    // Acumulador privado (no tocar en inspector)
    private float distanceTraveled;

    [Header("Clips de Audio")]
    public AudioClip[] stepClips;
    public AudioClip jumpClip;
    public AudioClip landClip;

    private AudioSource audioSource;
    private CharacterController cc;
    private bool wasGrounded;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        HandleFootsteps();
        HandleLanding();
    }

    void HandleFootsteps()
    {
        if (cc == null || !cc.isGrounded) return;

        // Calculamos la velocidad horizontal real
        Vector2 velocityHorizontal = new Vector2(cc.velocity.x, cc.velocity.z);
        float speed = velocityHorizontal.magnitude;

        // Si hay movimiento...
        if (speed > 0.1f)
        {
            // FÓRMULA MÁGICA: Distancia = Velocidad * Tiempo
            // Acumulamos cuántos metros avanzó en este frame
            distanceTraveled += speed * Time.deltaTime;

            // Si ya acumuló suficiente distancia para un paso...
            if (distanceTraveled >= stepDistance)
            {
                PlayRandomStep(speed); // Pasamos la velocidad para ajustar volumen
                distanceTraveled = 0f; // Reseteamos el contador de metros
            }
        }
        // NOTA: No reseteamos distanceTraveled a 0 si se detiene. 
        // Así, si diste medio paso y paraste, al volver a moverte completarás ese paso.
    }

    void PlayRandomStep(float currentSpeed)
    {
        if (stepClips.Length == 0) return;

        int index = Random.Range(0, stepClips.Length);
        AudioClip clip = stepClips[index];

        // Pitch aleatorio para variedad
        audioSource.pitch = Random.Range(0.9f, 1.1f);

        // VOLUMEN DINÁMICO:
        // Si vas lento (agachado), suena más pasito. Si corres, suena fuerte.
        // Asumiendo que tu velocidad de correr es aprox 6 o 7:
        float volumeFactor = Mathf.Clamp01(currentSpeed / 6f);
        // Nos aseguramos que nunca baje de 0.3 para que siempre se escuche algo
        float finalVolume = Mathf.Max(0.3f, volumeFactor);

        audioSource.PlayOneShot(clip, finalVolume);
    }

    void HandleLanding()
    {
        if (cc == null) return;

        if (!wasGrounded && cc.isGrounded)
        {
            // Resetear el ciclo de pasos al caer para que no suene doble paso
            distanceTraveled = 0;

            if (landClip != null)
            {
                audioSource.pitch = 1f;
                // Volumen del golpe depende de qué tan rápido caíste (opcional)
                audioSource.PlayOneShot(landClip, 0.8f);
            }
        }

        wasGrounded = cc.isGrounded;
    }

    public void PlayJumpSound()
    {
        if (jumpClip != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(jumpClip, 1.0f);
        }
    }
}