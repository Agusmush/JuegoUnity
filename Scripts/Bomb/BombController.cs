using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombController : MonoBehaviour
{
    [Header("Configuración General")]
    public float explosionRadius = 1000f;
    public LayerMask explosionLayers;

    [Header("Mecánicas de Bando")]
    public float zeroTolerance = 0.1f;
    private int currentSide = 0;
    private int previousSide = 0;

    [Header("Timers de Presión")]
    public float maxTimeInEnemySide = 10f;
    private float explodeTimer;

    [Header("Muerte Súbita (Centro)")]
    public float gracePeriodDuration = 5f;
    public float maxNeutralTime = 10f;
    private float currentGraceTimer;
    private float neutralTimer;

    [Header("Audio Tensión (Explosión)")]
    public float anticipationTime = 5.0f;
    public float audioTailTime = 7.0f;
    private bool hasTriggeredAnticipation = false;
    private AudioSource bombExplosionSource;

    [Header("Audio Motor (Loop & Pitch)")]
    public AudioSource bombLoopSource;
    public float minPitch = 0.8f;
    public float maxPitch = 3.0f;

    [Header("Fluidez del Pitch")]
    public float pitchRiseSpeed = 0.5f;
    public float pitchDropSpeed = 2.0f;
    [Tooltip("A los cuantos segundos del Timer Global empieza a subir el pitch")]
    public float globalStressThreshold = 15.0f;

    [Header("Feedback de Cambio (Slots)")]
    public AudioClip switchClip;
    public AudioClip reliefSafe;
    public AudioClip reliefMedium;
    public AudioClip reliefCritical;

    [Header("Estado y Magnetismo")]
    public bool isArmed = true;
    public Transform magnetBasePositivo;
    public Transform magnetBaseNegativo;
    public float initialMagnetForce = 5f;
    public float magnetGrowthRate = 1f;
    private float currentMagnetForce;

    [Header("Visuales")]
    public TrailRenderer trail;
    public Renderer meshRenderer;
    public string outlineColorProperty = "_EmissionColor";
    private Color colorTeamA = Color.blue;
    private Color colorTeamB = Color.red;
    private Color colorNeutral = Color.white;

    private Rigidbody rb;
    private bool hasExploded = false;

    [Header("Fuerzas de Explosión")]
    public float minExplosionForce = 10000f;
    public float maxExplosionForce = 60000f;
    public float explosionUpwardModifier = 5.0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length >= 1) bombExplosionSource = sources[0];
        if (sources.Length >= 2) bombLoopSource = sources[1];

        if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();

        if (gracePeriodDuration <= 0) gracePeriodDuration = 5f;
        if (maxNeutralTime <= 0) maxNeutralTime = 10f;
        if (anticipationTime <= 0) anticipationTime = 5f;
        if (audioTailTime <= 0) audioTailTime = 7f;

        explodeTimer = maxTimeInEnemySide;
        currentGraceTimer = gracePeriodDuration;
        neutralTimer = maxNeutralTime;
        currentMagnetForce = initialMagnetForce;
    }

    void Update()
    {
        if (hasExploded) return;
        if (!isArmed) return;

        // 1. Determinar Lado
        int detectedSide = 0;
        if (transform.position.z < -zeroTolerance) detectedSide = -1;
        else if (transform.position.z > zeroTolerance) detectedSide = 1;
        else detectedSide = 0;

        currentSide = detectedSide;

        // 2. Detectar CAMBIO de lado
        if (currentSide != 0 && currentSide != previousSide)
        {
            HandleSwitchEvents();
        }

        if (currentSide != 0) previousSide = currentSide;

        CheckSideLogic();
        UpdateEnginePitch();
        ApplyMagnetForce();
        UpdateVisuals();
    }

    private float GetGlobalTimer()
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.currentTimer;
        }
        return 999f;
    }

    // --- FUNCIÓN CORREGIDA ---
    private void HandleSwitchEvents()
    {
        // 1. Calcular riesgo LOCAL para elegir el sonido de alivio
        float localTimeRemaining = 0f;
        if (previousSide != 0) localTimeRemaining = explodeTimer;
        else localTimeRemaining = neutralTimer;

        // 2. Calcular riesgo GLOBAL
        float globalTime = GetGlobalTimer();

        // 3. Obtener el tiempo MÁS CRÍTICO para el tipo de alivio
        float urgentTime = Mathf.Min(localTimeRemaining, globalTime);

        // 4. Seleccionar Audio de Alivio
        AudioClip reliefToPlay = null;
        if (urgentTime > 6.0f) reliefToPlay = reliefSafe;
        else if (urgentTime > 3.0f) reliefToPlay = reliefMedium;
        else reliefToPlay = reliefCritical;

        // --- CORRECCIÓN CLAVE AQUÍ ---
        // Solo detenemos el sonido de tensión (Riser) SI el tiempo global es seguro (> 5s).
        // Si el tiempo global es peligroso (<= 5s), el Riser DEBE SEGUIR SONANDO.

        bool isGlobalDanger = globalTime <= anticipationTime;

        if (hasTriggeredAnticipation)
        {
            // Si NO hay peligro global, significa que el peligro era local y lo acabamos de resetear.
            // Entonces sí: ¡Callate bomba!
            if (!isGlobalDanger)
            {
                if (bombExplosionSource != null) bombExplosionSource.Stop();
                hasTriggeredAnticipation = false;
            }
            // Si HAY peligro global (isGlobalDanger = true), NO hacemos Stop().
            // Dejamos hasTriggeredAnticipation en true.
            // El Riser sigue sonando de fondo mientras reproducimos el alivio encima.
        }

        // 5. Reproducir Sonidos de Pase (Mezclados sobre el Riser si corresponde)
        if (bombExplosionSource != null)
        {
            // Golpe Mecánico
            if (switchClip != null) bombExplosionSource.PlayOneShot(switchClip, 1.0f);

            // Alivio
            if (reliefToPlay != null)
            {
                float vol = (reliefToPlay == reliefCritical) ? 1.0f : 0.8f;
                bombExplosionSource.PlayOneShot(reliefToPlay, vol);
            }
        }

        // 6. Resetear Timers Locales
        explodeTimer = maxTimeInEnemySide;
        currentGraceTimer = gracePeriodDuration;
        neutralTimer = maxNeutralTime;

        if (AudioManager.Instance != null) AudioManager.Instance.Stop("Alarm");
        if (UIManager.Instance != null) UIManager.Instance.OnBombSideChanged(currentSide);
    }

    private void CheckSideLogic()
    {
        float timeUntilLocalBoom = 999f;

        if (currentSide != 0)
        {
            explodeTimer -= Time.deltaTime;
            timeUntilLocalBoom = explodeTimer;

            if (UIManager.Instance != null)
                UIManager.Instance.UpdateBombTimers(explodeTimer, currentSide, false);

            if (explodeTimer <= 0) ForceExplode();
        }
        else
        {
            if (currentGraceTimer > 0)
            {
                currentGraceTimer -= Time.deltaTime;
                timeUntilLocalBoom = 999f;
                if (UIManager.Instance != null)
                    UIManager.Instance.UpdateBombTimers(currentGraceTimer, 0, true);
            }
            else
            {
                neutralTimer -= Time.deltaTime;
                timeUntilLocalBoom = neutralTimer;
                if (UIManager.Instance != null)
                    UIManager.Instance.UpdateBombTimers(neutralTimer, 0, false);
                if (neutralTimer <= 0) ForceExplode();
            }
        }

        float globalTime = GetGlobalTimer();
        float mostUrgentTime = Mathf.Min(timeUntilLocalBoom, globalTime);

        // Lógica de activación del Riser (Sonido de explosión)
        if (mostUrgentTime <= anticipationTime && !hasTriggeredAnticipation && mostUrgentTime > 0)
        {
            if (bombExplosionSource != null) bombExplosionSource.Play();
            hasTriggeredAnticipation = true;
        }
    }

    private void UpdateEnginePitch()
    {
        if (bombLoopSource == null) return;

        float activeTimer = 0;
        float maxActiveTime = 1;
        bool isRelaxed = false;

        if (currentSide != 0)
        {
            activeTimer = explodeTimer;
            maxActiveTime = maxTimeInEnemySide;
        }
        else
        {
            if (currentGraceTimer > 0) isRelaxed = true;
            else
            {
                activeTimer = neutralTimer;
                maxActiveTime = maxNeutralTime;
            }
        }

        float localStress = 0f;
        if (!isRelaxed)
        {
            localStress = 1.0f - Mathf.Clamp01(activeTimer / maxActiveTime);
        }

        float globalTime = GetGlobalTimer();
        float globalStress = 0f;

        if (globalTime < globalStressThreshold)
        {
            globalStress = 1.0f - Mathf.Clamp01(globalTime / globalStressThreshold);
        }

        float finalStress = Mathf.Max(localStress, globalStress);
        float targetPitch = Mathf.Lerp(minPitch, maxPitch, finalStress * finalStress);

        float currentSpeed = (targetPitch > bombLoopSource.pitch) ? pitchRiseSpeed : pitchDropSpeed;
        bombLoopSource.pitch = Mathf.MoveTowards(bombLoopSource.pitch, targetPitch, Time.deltaTime * currentSpeed);
    }

    private void ApplyMagnetForce()
    {
        currentMagnetForce += magnetGrowthRate * Time.deltaTime;
        Transform targetMagnet = null;
        if (currentSide == 1 && magnetBaseNegativo != null) targetMagnet = magnetBaseNegativo;
        else if (currentSide == -1 && magnetBasePositivo != null) targetMagnet = magnetBasePositivo;
        if (targetMagnet != null)
        {
            Vector3 direction = (targetMagnet.position - transform.position).normalized;
            rb.AddForce(direction * currentMagnetForce * Time.deltaTime, ForceMode.Acceleration);
        }
    }

    public void ForceExplode() => Explode();

    public void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        StartCoroutine(HitStopRoutine());
        if (bombLoopSource != null) bombLoopSource.Stop();
        if (AudioManager.Instance != null) AudioManager.Instance.Stop("Alarm");
        if (!hasTriggeredAnticipation)
        {
            if (bombExplosionSource != null) bombExplosionSource.Play();
            else if (AudioManager.Instance != null) AudioManager.Instance.Play("Explosion");
        }
        int losingSide = 0;
        if (transform.position.z > zeroTolerance) losingSide = 1;
        else if (transform.position.z < -zeroTolerance) losingSide = -1;
        if (GameManager.Instance != null) GameManager.Instance.RegisterPoint(losingSide);
        if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(1.5f, 0.8f);
        ApplyExplosionPhysics(losingSide);
        StartCoroutine(DestroyRoutine());
    }

    IEnumerator DestroyRoutine()
    {
        if (meshRenderer != null) meshRenderer.enabled = false;
        if (trail != null) trail.enabled = false;
        foreach (var light in GetComponentsInChildren<Light>()) light.enabled = false;
        foreach (var particle in GetComponentsInChildren<ParticleSystem>()) particle.Stop();
        GetComponent<Collider>().enabled = false;
        rb.isKinematic = true;
        yield return new WaitForSeconds(audioTailTime);
        Destroy(gameObject);
    }

    private void ApplyExplosionPhysics(int losingSide)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject playerObj in players)
        {
            PlayerHealth health = playerObj.GetComponent<PlayerHealth>();
            PlayerMotor motor = playerObj.GetComponent<PlayerMotor>();
            float distance = Vector3.Distance(transform.position, playerObj.transform.position);
            float proximityFactor = 1f - Mathf.Clamp01(distance / (losingSide == 0 ? 5000f : 100f));
            int playerSide = (playerObj.transform.position.z >= 0) ? 1 : -1;
            bool isVictim = (losingSide == 0) || (playerSide == losingSide);

            if (isVictim)
            {
                float finalForce = Mathf.Lerp(minExplosionForce, maxExplosionForce, proximityFactor);
                if (health != null) health.TakeDamage(9999f, transform);
                Rigidbody playerRb = playerObj.GetComponent<Rigidbody>();
                if (playerRb != null && !playerRb.isKinematic)
                {
                    playerRb.AddExplosionForce(finalForce, transform.position, 0f, explosionUpwardModifier, ForceMode.Impulse);
                    playerRb.AddTorque(Random.insideUnitSphere * finalForce * 0.5f, ForceMode.Impulse);
                }
            }
            else
            {
                if (motor != null)
                {
                    Vector3 dir = (playerObj.transform.position - transform.position).normalized + Vector3.up;
                    motor.AddExplosionForce(dir.normalized, 300f);
                }
            }
        }
    }

    IEnumerator HitStopRoutine()
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(0.1f);
        Time.timeScale = 1f;
    }

    private void UpdateVisuals()
    {
        Color targetColor;
        if (currentSide == 1) targetColor = colorTeamB;
        else if (currentSide == -1) targetColor = colorTeamA;
        else
        {
            if (currentGraceTimer <= 0)
            {
                float blink = Mathf.PingPong(Time.time * 5, 1);
                targetColor = Color.Lerp(Color.yellow, Color.red, blink);
            }
            else targetColor = colorNeutral;
        }
        if (trail != null)
        {
            trail.startColor = targetColor;
            trail.endColor = new Color(targetColor.r, targetColor.g, targetColor.b, 0);
        }
        if (meshRenderer != null)
        {
            meshRenderer.material.SetColor(outlineColorProperty, targetColor * 2f);
        }
    }
}