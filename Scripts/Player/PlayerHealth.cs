using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Configuración de Salud")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public int teamID = 0;

    public bool isDead = false;
    private Transform lastAttacker;

    [Header("Eventos")]
    public UnityEvent OnDeath;
    public UnityEvent OnRevive;

    // DEBUG para InputManager
    public Transform testKiller;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateVisuals();
        UpdateUI();

        if (UIManager.Instance != null)
            UIManager.Instance.ToggleGameplayHUD(true);
    }

    public void OnDebugKill()
    {
        TakeDamage(maxHealth * 10f);
    }

    public void TakeDamage(float amount, Transform attacker = null)
    {
        if (isDead) return;

        if (attacker != null) lastAttacker = attacker;

        currentHealth -= amount;
        UpdateUI();

        if (currentHealth <= 0 && !isDead)
        {
            currentHealth = 0;
            Die();
        }
    }
    public void TakeDamage(float amount) { TakeDamage(amount, null); }

    private void Die()
    {
        isDead = true;
        OnDeath?.Invoke();

        if (TryGetComponent(out CharacterController cc)) cc.enabled = false;
        if (TryGetComponent(out PlayerMotor motor)) motor.enabled = false;

        // Activar Ragdoll si tienes el script
        if (TryGetComponent(out RagdollManager ragdoll)) ragdoll.ActivateRagdoll(lastAttacker);

        if (TryGetComponent(out BombCarrier carrier) && carrier.isCarrying) carrier.DropBomb();

        // Desactivar Input y Cámara
        if (TryGetComponent(out InputManager inputMgr)) inputMgr.enabled = false;
        if (TryGetComponent(out PlayerLook look)) look.enabled = false;

        if (UIManager.Instance != null) UIManager.Instance.ToggleGameplayHUD(false);

        if (GameManager.Instance != null) StartCoroutine(WaitAndRespawn());
    }

    IEnumerator WaitAndRespawn()
    {
        yield return new WaitForSeconds(4.0f);

        // --- LÓGICA DE RESPAWN CONDICIONAL ---
        if (GameManager.Instance != null)
        {
            // Solo revivimos si NO estamos en la fase de caos
            if (!GameManager.Instance.isChaosPhase)
            {
                GameManager.Instance.RespawnSinglePlayer(this);
            }
            // Si estamos en caos, no hacemos nada. 
            // El jugador se queda muerto hasta que empiece la siguiente ronda 
            // y el GameManager llame a 'RespawnSinglePlayer' manualmente en FASE 1.
        }
    }

    public void ResetPlayer()
    {
        isDead = false;
        currentHealth = maxHealth;

        // 1. Apagar todo lo que mueva al personaje
        if (TryGetComponent(out CharacterController cc)) cc.enabled = false;
        if (TryGetComponent(out PlayerMotor motor)) motor.enabled = false;

        // 2. Apagar Ragdoll y limpiar físicas
        if (TryGetComponent(out RagdollManager ragdoll)) ragdoll.DeactivateRagdoll();
        if (TryGetComponent(out Rigidbody rb))
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 3. PRIMER FORZADO DE ROTACIÓN
        transform.rotation = Quaternion.identity;

        // Reactivar lógica
        if (TryGetComponent(out InputManager inputMgr)) inputMgr.enabled = true;

        if (TryGetComponent(out PlayerLook look))
        {
            look.enabled = true;
            // Reiniciar rotación de la cámara (cabeza)
            look.cam.transform.localRotation = Quaternion.identity;
            look.transform.localRotation = Quaternion.identity; // Reiniciar cuerpo visual si rota separado

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (UIManager.Instance != null) UIManager.Instance.ToggleGameplayHUD(true);
        UpdateVisuals();
        UpdateUI();

        // Iniciamos la secuencia de "Enderezado"
        StartCoroutine(ReviveSequence());
    }

    IEnumerator ReviveSequence()
    {
        // --- CORRECCIÓN 2: LA SOLUCIÓN NUCLEAR ANTI-INCLINACIÓN ---

        // Esperamos un tiempo real, no solo un frame.
        // Esto da tiempo a que el Ragdoll termine de desaparecer por completo.
        yield return new WaitForSeconds(0.1f);

        // SEGUNDO FORZADO DE ROTACIÓN (El de seguridad)
        transform.rotation = Quaternion.identity;
        Physics.SyncTransforms(); // Obliga a Unity a aplicar esto YA

        yield return new WaitForFixedUpdate();

        // Ahora sí, encendemos el motor y el controller
        if (TryGetComponent(out PlayerMotor motor))
        {
            motor.enabled = true;
            motor.ResetMotion();
        }

        if (TryGetComponent(out CharacterController cc)) cc.enabled = true;
    }

    private void UpdateVisuals()
    {
        if (TryGetComponent(out PlayerTeamVisuals visuals)) visuals.UpdateVisuals();
    }
    private void UpdateUI()
    {
        if (UIManager.Instance != null) UIManager.Instance.UpdateHealth(currentHealth, maxHealth);
    }
    public void Heal(float amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        UpdateUI();
    }
}