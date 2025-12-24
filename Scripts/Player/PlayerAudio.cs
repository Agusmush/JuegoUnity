using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [Header("Configuración por Distancia")]
    [Tooltip("Distancia en metros para dar un paso. Auméntalo si suenan muy rápido.")]
    public float stepDistance = 1.8f;

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
        // CORRECCIÓN AQUÍ:
        // Añadimos "!cc.enabled". Si el RagdollManager apagó el controller, salimos.
        if (cc == null || !cc.enabled || !cc.isGrounded) return;

        Vector2 velocityHorizontal = new Vector2(cc.velocity.x, cc.velocity.z);
        float speed = velocityHorizontal.magnitude;

        if (speed > 0.1f)
        {
            distanceTraveled += speed * Time.deltaTime;

            if (distanceTraveled >= stepDistance)
            {
                PlayRandomStep(speed);
                distanceTraveled = 0f;
            }
        }
    }

    void PlayRandomStep(float currentSpeed)
    {
        if (stepClips.Length == 0) return;

        int index = Random.Range(0, stepClips.Length);
        AudioClip clip = stepClips[index];

        audioSource.pitch = Random.Range(0.9f, 1.1f);

        float volumeFactor = Mathf.Clamp01(currentSpeed / 6f);
        float finalVolume = Mathf.Max(0.3f, volumeFactor);

        audioSource.PlayOneShot(clip, finalVolume);
    }

    void HandleLanding()
    {
        // CORRECCIÓN AQUÍ TAMBIÉN:
        if (cc == null || !cc.enabled) return;

        if (!wasGrounded && cc.isGrounded)
        {
            distanceTraveled = 0;

            if (landClip != null)
            {
                audioSource.pitch = 1f;
                audioSource.PlayOneShot(landClip, 0.8f);
            }
        }

        wasGrounded = cc.isGrounded;
    }

    public void PlayJumpSound()
    {
        // Opcional: Evitar saltar si estamos muertos (aunque el input ya debería estar bloqueado)
        if (cc != null && !cc.enabled) return;

        if (jumpClip != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(jumpClip, 1.0f);
        }
    }
}