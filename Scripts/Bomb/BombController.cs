using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombController : MonoBehaviour
{
    [Header("Configuración General")]
    public float explosionRadius = 1000f;
    public LayerMask explosionLayers;

    [Header("Mecánicas de Bando (Z-Axis)")]
    public float zeroTolerance = 0.5f; // Aumentado un poco para que el centro sea zona segura clara
    private int currentSide = 0; // 1 = Positivo (B), -1 = Negativo (A)
    private int previousSide = 0; // Para detectar cambios

    [Header("Regla de los 5 Segundos")]
    public float maxTimeInEnemySide = 5f;
    private float explodeTimer; // <--- ESTA ERA LA VARIABLE QUE FALTABA
    public bool isArmed = true;

    [Header("Imanes")]
    public Transform magnetBasePositivo;
    public Transform magnetBaseNegativo;
    public float initialMagnetForce = 5f;
    public float magnetGrowthRate = 1f;
    private float currentMagnetForce;

    [Header("Visuales")]
    public TrailRenderer trail;
    public Renderer meshRenderer;
    public string outlineColorProperty = "_EmissionColor";

    [Header("Estado")]
    private Rigidbody rb;
    private bool hasExploded = false;

    [Header("Explosión Nuclear")]
    public float minExplosionForce = 10000f;
    public float maxExplosionForce = 60000f;
    public float explosionUpwardModifier = 5.0f;

    // Referencia para scoring
    private GameObject lastPlayerTouched;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        explodeTimer = maxTimeInEnemySide; // Iniciamos con 5 segundos
        currentMagnetForce = initialMagnetForce;
    }

    void Update()
    {
        if (hasExploded) return;
        if (!isArmed) return;

        // 1. Determinar Lado (-1, 0, 1)
        int detectedSide = 0;
        if (transform.position.z < -zeroTolerance) detectedSide = -1; // Lado A
        else if (transform.position.z > zeroTolerance) detectedSide = 1;  // Lado B

        currentSide = detectedSide;

        // 2. Detectar CRUCE DE LÍNEA (Snapshot Logic)
        // Si cambiamos de lado (y no es que fuimos al centro)
        if (currentSide != 0 && currentSide != previousSide)
        {
            // Resetear timer
            explodeTimer = maxTimeInEnemySide;

            // Avisar a UI para efecto "Snapshot"
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OnBombSideChanged(currentSide);
            }
        }

        // Actualizar previo
        if (currentSide != 0) previousSide = currentSide;

        // 3. Lógica de Tiempo y Imanes
        CheckSideLogic();
        ApplyMagnetForce();
        UpdateVisuals();
    }

    private void CheckSideLogic()
    {
        // Si estamos en un lado peligroso (no en el centro)
        if (currentSide != 0)
        {
            // Restar tiempo
            explodeTimer -= Time.deltaTime;

            // Actualizar UI
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateBombTimers(explodeTimer, currentSide);
            }

            // BOOM
            if (explodeTimer <= 0)
            {
                ForceExplode();
            }
        }
        else
        {
            // Zona Neutral: Limpiamos los textos o mostramos 5.00
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateBombTimers(5.0f, 0);
            }
        }
    }

    private void ApplyMagnetForce()
    {
        currentMagnetForce += magnetGrowthRate * Time.deltaTime;
        Transform targetMagnet = null;

        // Si la bomba está en lado B (1), el imán de A (-1/Negativo) tira de ella?
        // O el imán de su propio lado la empuja? 
        // Asumo que el imán "enemigo" la atrae para dificultar. Ajusta según tu lógica.

        // Lógica: Si está en Positivo (1), atrae el Negativo (para que cruce)
        if (currentSide == 1 && magnetBaseNegativo != null) targetMagnet = magnetBaseNegativo;
        else if (currentSide == -1 && magnetBasePositivo != null) targetMagnet = magnetBasePositivo;

        if (targetMagnet != null)
        {
            Vector3 direction = (targetMagnet.position - transform.position).normalized;
            rb.AddForce(direction * currentMagnetForce * Time.deltaTime, ForceMode.Acceleration);
        }
    }

    public void ForceExplode() // Renombrado a ForceExplode para coincidir con GameManager
    {
        Explode();
    }

    public void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        StartCoroutine(HitStopRoutine());

        // Calcular quién perdió (Donde explotó)
        int losingSide = (transform.position.z >= 0) ? 1 : -1;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPoint(losingSide);
        }

        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.TriggerShake(1.0f, 0.5f);
        }

        ApplyExplosionPhysics(losingSide);

        Destroy(gameObject, 0.2f);
    }

    private void ApplyExplosionPhysics(int losingSide)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject playerObj in players)
        {
            PlayerHealth health = playerObj.GetComponent<PlayerHealth>();
            PlayerMotor motor = playerObj.GetComponent<PlayerMotor>();

            float distance = Vector3.Distance(transform.position, playerObj.transform.position);
            float proximityFactor = 1f - Mathf.Clamp01(distance / 100f);

            // Determinar bando del jugador
            float playerZ = playerObj.transform.position.z;
            int playerSide = (playerZ >= 0) ? 1 : -1;

            if (playerSide == losingSide)
            {
                // PERDEDORES: Mueren y vuelan
                float finalForce = Mathf.Lerp(minExplosionForce, maxExplosionForce, proximityFactor);
                if (health != null) health.TakeDamage(9999f, transform);

                Rigidbody playerRb = playerObj.GetComponent<Rigidbody>();
                if (playerRb != null && !playerRb.isKinematic)
                {
                    playerRb.AddExplosionForce(finalForce, transform.position, 0f, explosionUpwardModifier, ForceMode.Impulse);
                    playerRb.AddTorque(Random.insideUnitSphere * finalForce * 0.2f, ForceMode.Impulse);
                }
            }
            else
            {
                // GANADORES: Empujón leve
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
        Time.timeScale = 0.1f;
        yield return new WaitForSecondsRealtime(0.05f);
        Time.timeScale = 1f;
    }

    private void UpdateVisuals()
    {
        // Azul para Positivo (1), Rojo para Negativo (-1)
        Color targetColor = (currentSide == 1) ? Color.blue : Color.red;
        if (currentSide == 0) targetColor = Color.white;

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

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            lastPlayerTouched = collision.gameObject;
    }
}