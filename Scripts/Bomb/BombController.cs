using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class BombController : NetworkBehaviour
{
    [Header("Configuración General")]
    public float explosionRadius = 1000f;
    public LayerMask explosionLayers;

    [Header("Red")]
    public NetworkVariable<int> netCurrentSide = new NetworkVariable<int>(0);
    public NetworkVariable<float> netVisualTimer = new NetworkVariable<float>(10f);
    public NetworkVariable<bool> netIsArmed = new NetworkVariable<bool>(true);
    public NetworkVariable<bool> netIsGracePeriod = new NetworkVariable<bool>(true);

    [Header("Mecánicas de Bando")]
    public float zeroTolerance = 0.1f;
    public float enterThreshold = 1.0f;
    public float exitThreshold = 0.5f;

    private int currentSideState = 0;
    private float lastSwitchTime = 0f;

    // --- FIX SATURACIÓN ---
    private float lastFeedbackTime = 0f;
    private float minTimeBetweenSounds = 0.15f;

    [Header("Timers")]
    public float maxTimeInEnemySide = 10f;
    private float explodeTimer;
    public float gracePeriodDuration = 5f;
    public float maxNeutralTime = 10f;
    private float currentGraceTimer;
    private float neutralTimer;

    [Header("Audio Tensión")]
    public float anticipationTime = 5.0f;
    public float audioTailTime = 4.0f;
    private bool hasTriggeredAnticipation = false;

    [Header("REFERENCIAS (Arrastrar los 3 AudioSources)")]
    public AudioSource bombLoopSource;
    public AudioSource bombExplosionSource;
    public AudioSource bombFeedbackSource;

    [Header("Clips")]
    public AudioClip switchClip;
    public AudioClip reliefSafe;
    public AudioClip reliefMedium;
    public AudioClip reliefCritical;
    public AudioClip alarmClip;
    public AudioClip explosionClip;

    [Header("Audio Motor")]
    public float minPitch = 0.8f;
    public float maxPitch = 3.0f;
    public float pitchRiseSpeed = 0.5f;
    public float pitchDropSpeed = 2.0f;
    public float globalStressThreshold = 5.0f;

    [Header("Físicas")]
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

    public float minExplosionForce = 10000f;
    public float maxExplosionForce = 60000f; // Fuerza alta para mover ragdolls
    public float explosionUpwardModifier = 5.0f;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();

        SetupAudio(bombLoopSource, true, 1f, 50f, 200f);
        SetupAudio(bombExplosionSource, false, 1f, 100f, 500f);
        SetupAudio(bombFeedbackSource, false, 1f, 50f, 200f);

        if (bombLoopSource != null) bombLoopSource.Play();
        if (IsServer) ResetLogic();
    }

    private void SetupAudio(AudioSource source, bool loop, float spatialBlend, float minDistance = 1f, float maxDistance = 500f)
    {
        if (source != null)
        {
            source.dopplerLevel = 0;
            source.spatialBlend = spatialBlend;
            source.loop = loop;
            source.playOnAwake = false;
            if (spatialBlend > 0f)
            {
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = minDistance;
                source.maxDistance = maxDistance;
            }
        }
    }

    private void ResetLogic()
    {
        explodeTimer = maxTimeInEnemySide;
        currentGraceTimer = gracePeriodDuration;
        neutralTimer = maxNeutralTime;
        currentMagnetForce = initialMagnetForce;
        netIsArmed.Value = true;
        netIsGracePeriod.Value = true;
        currentSideState = 0;
        netCurrentSide.Value = 0;
    }

    void Update()
    {
        if (hasExploded) return;
        UpdateAudioAndVisuals();
        if (IsServer) ServerLogic();
    }

    private void ServerLogic()
    {
        if (!netIsArmed.Value) return;
        float z = transform.position.z;
        int nextState = currentSideState;

        if (currentSideState == 0)
        {
            if (z > enterThreshold) nextState = 1;
            else if (z < -enterThreshold) nextState = -1;
        }
        else if (currentSideState == 1)
        {
            if (z < exitThreshold) nextState = 0;
            else if (z < -enterThreshold) nextState = -1;
        }
        else if (currentSideState == -1)
        {
            if (z > -exitThreshold) nextState = 0;
            else if (z > enterThreshold) nextState = 1;
        }

        if (nextState != currentSideState)
        {
            float timeLeft;
            if (currentSideState == 0 && nextState != 0) timeLeft = maxTimeInEnemySide;
            else if (currentSideState != 0 && nextState == 0) timeLeft = explodeTimer;
            else if (currentSideState != 0 && nextState != 0) timeLeft = explodeTimer;
            else timeLeft = maxTimeInEnemySide;

            if (GameManager.Instance != null)
            {
                float globalTime = GameManager.Instance.currentTimer;
                if (globalTime < globalStressThreshold) timeLeft = Mathf.Min(timeLeft, globalTime);
            }

            PlaySwitchSoundClientRpc(currentSideState, nextState, timeLeft);

            if (nextState != 0) explodeTimer = maxTimeInEnemySide;
            if (nextState == 0) { currentGraceTimer = gracePeriodDuration; neutralTimer = maxNeutralTime; }

            currentSideState = nextState;
            netCurrentSide.Value = currentSideState;
        }

        float displayTime = 0f;
        if (currentSideState != 0)
        {
            netIsGracePeriod.Value = false;
            explodeTimer -= Time.deltaTime;
            displayTime = explodeTimer;
            if (explodeTimer <= 0) ForceExplode();
        }
        else
        {
            if (currentGraceTimer > 0)
            {
                currentGraceTimer -= Time.deltaTime;
                displayTime = currentGraceTimer;
                netIsGracePeriod.Value = true;
            }
            else
            {
                neutralTimer -= Time.deltaTime;
                displayTime = neutralTimer;
                netIsGracePeriod.Value = false;
                if (neutralTimer <= 0) ForceExplode();
            }
        }

        netVisualTimer.Value = displayTime;
        ApplyMagnetForce();
    }

    private void UpdateAudioAndVisuals()
    {
        int side = netCurrentSide.Value;
        float timer = netVisualTimer.Value;
        bool isGrace = netIsGracePeriod.Value;

        if (netIsArmed.Value && bombLoopSource != null && !bombLoopSource.isPlaying) bombLoopSource.Play();

        bool globalOverride = (GameManager.Instance != null && GameManager.Instance.currentTimer < globalStressThreshold);
        bool justSwitched = (Time.time - lastSwitchTime < 0.5f);
        bool shouldAlarm = ((timer <= anticipationTime || globalOverride) && !isGrace && netIsArmed.Value && !justSwitched);

        if (shouldAlarm)
        {
            if (!hasTriggeredAnticipation)
            {
                if (bombExplosionSource != null)
                {
                    bombExplosionSource.Stop();
                    if (alarmClip != null)
                    {
                        bombExplosionSource.clip = alarmClip;
                        bombExplosionSource.loop = false;
                        bombExplosionSource.Play();
                    }
                    else bombExplosionSource.Play();
                }
                hasTriggeredAnticipation = true;
            }
        }
        else
        {
            if (hasTriggeredAnticipation && !justSwitched)
            {
                if (bombExplosionSource != null) bombExplosionSource.Stop();
                hasTriggeredAnticipation = false;
            }
        }

        if (bombLoopSource != null)
        {
            float stress = 0f;
            if (!(side == 0 && isGrace))
            {
                float maxActiveTime = (side == 0) ? maxNeutralTime : maxTimeInEnemySide;
                stress = 1.0f - Mathf.Clamp01(timer / maxActiveTime);
            }

            if (GameManager.Instance != null)
            {
                float globalTime = GameManager.Instance.currentTimer;
                if (globalTime < globalStressThreshold)
                {
                    float globalStressFactor = 1.0f - Mathf.Clamp01(globalTime / globalStressThreshold);
                    stress = Mathf.Max(stress, globalStressFactor);
                }
            }

            float targetPitch = Mathf.Lerp(minPitch, maxPitch, stress * stress);
            bombLoopSource.pitch = Mathf.MoveTowards(bombLoopSource.pitch, targetPitch, Time.deltaTime);
        }

        UpdateVisuals(side, timer);
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateBombTimers(timer, side, isGrace);
            UIManager.Instance.OnBombSideChanged(side);
        }
    }

    [ClientRpc]
    private void PlaySwitchSoundClientRpc(int oldSide, int newSide, float timeLeft)
    {
        lastSwitchTime = Time.time;
        if (Time.time - lastFeedbackTime < minTimeBetweenSounds) return;
        lastFeedbackTime = Time.time;

        if (bombExplosionSource != null)
        {
            bombExplosionSource.Stop();
            hasTriggeredAnticipation = false;
        }

        if (bombFeedbackSource != null)
        {
            if (switchClip != null) bombFeedbackSource.PlayOneShot(switchClip);
            AudioClip relief = reliefSafe;
            if (timeLeft <= 3.0f) relief = reliefCritical;
            else if (timeLeft <= 6.0f) relief = reliefMedium;
            if (relief != null) bombFeedbackSource.PlayOneShot(relief);
        }
    }

    public void ForceExplode() { if (IsServer) ExplodeSequence(); }

    private void ExplodeSequence()
    {
        if (hasExploded) return;
        hasExploded = true;
        netIsArmed.Value = false;

        int losingSide = 0;
        if (transform.position.z > zeroTolerance) losingSide = 1;
        else if (transform.position.z < -zeroTolerance) losingSide = -1;

        // APLICAR FÍSICAS + DAÑO
        ApplyExplosionPhysics(losingSide);

        if (GameManager.Instance != null) GameManager.Instance.RegisterPoint(losingSide);

        ExplodeClientRpc(losingSide);
        StartCoroutine(DestroyServerRoutine());
    }

    [ClientRpc]
    private void ExplodeClientRpc(int losingSide)
    {
        if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(1.5f, 0.8f);

        if (bombLoopSource != null) bombLoopSource.Stop();
        if (bombExplosionSource != null) bombExplosionSource.Stop();
        if (bombFeedbackSource != null) bombFeedbackSource.Stop();

        if (explosionClip != null && bombExplosionSource != null) bombExplosionSource.PlayOneShot(explosionClip);
        else if (AudioManager.Instance != null) AudioManager.Instance.Play("Explosion");

        if (meshRenderer != null) meshRenderer.enabled = false;
        if (trail != null) trail.enabled = false;
        foreach (var p in GetComponentsInChildren<ParticleSystem>()) p.Stop();

        StartCoroutine(HitStopRoutine());
    }

    // --- AQUÍ ESTÁ EL TRUCO PARA QUE FUNCIONE TODO ---
    private void ApplyExplosionPhysics(int losingSide)
    {
        // 1. DAÑO (Matar a los vivos)
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject playerObj in players)
        {
            PlayerHealth health = playerObj.GetComponent<PlayerHealth>();

            float distance = Vector3.Distance(transform.position, playerObj.transform.position);
            float range = (losingSide == 0) ? 5000f : 100f;

            float proximityFactor = 1f - Mathf.Clamp01(distance / range);
            bool isVictim = (losingSide == 0) || (playerObj.transform.position.z * losingSide > 0);

            // SOLO SI ESTÁ VIVO
            if (isVictim && health != null && !health.netIsDead.Value)
            {
                float finalForce = Mathf.Lerp(minExplosionForce, maxExplosionForce, proximityFactor);

                DamageInfo info = new DamageInfo(
                    9999f,
                    transform.position,
                    Vector3.up,
                    finalForce,
                    true,
                    range,
                    999
                );
                // Esto mata al jugador y activa su Ragdoll
                health.TakeDamage(info);
            }
        }

        // 2. FÍSICA A TODO (Incluyendo los muertos)
        // Este bucle mueve a los Ragdolls que TakeDamage ignoró (porque ya estaban muertos)
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius, explosionLayers);

        foreach (var hit in colliders)
        {
            Rigidbody hitRb = hit.GetComponent<Rigidbody>();
            if (hitRb != null)
            {
                hitRb.AddExplosionForce(maxExplosionForce, transform.position, explosionRadius, explosionUpwardModifier, ForceMode.Impulse);
            }
        }
    }

    // --- UTILS ---
    private void ApplyMagnetForce()
    {
        if (!IsServer) return;
        currentMagnetForce += magnetGrowthRate * Time.deltaTime;
        Transform target = null;
        if (netCurrentSide.Value == 1) target = magnetBaseNegativo;
        else if (netCurrentSide.Value == -1) target = magnetBasePositivo;
        if (target != null)
        {
            Vector3 dir = (target.position - transform.position).normalized;
            rb.AddForce(dir * currentMagnetForce * Time.deltaTime, ForceMode.Acceleration);
        }
    }

    IEnumerator HitStopRoutine()
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(0.1f);
        Time.timeScale = 1f;
    }

    IEnumerator DestroyServerRoutine()
    {
        yield return new WaitForSeconds(audioTailTime);
        if (NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn();
    }

    private void UpdateVisuals(int side, float timer)
    {
        Color col = colorNeutral;
        if (side == 1) col = colorTeamB;
        else if (side == -1) col = colorTeamA;
        else
        {
            if (timer < 5f && side == 0) col = Color.Lerp(Color.yellow, Color.red, Mathf.PingPong(Time.time * 5, 1));
        }
        if (trail != null) { trail.startColor = col; trail.endColor = new Color(col.r, col.g, col.b, 0); }
        if (meshRenderer != null) meshRenderer.material.SetColor(outlineColorProperty, col * 2f);
    }
}