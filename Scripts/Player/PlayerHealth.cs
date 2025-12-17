using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Configuración de Salud")]
    public float maxHealth = 100f;

    // --- VARIABLES NORMALES (ANTES RED) ---
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

        // Lógica de Muerte Local
        OnDeath?.Invoke();

        if (TryGetComponent(out CharacterController cc)) cc.enabled = false;
        if (TryGetComponent(out PlayerMotor motor)) motor.enabled = false;
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

        if (GameManager.Instance != null && GameManager.Instance.isRoundActive)
        {
            GameManager.Instance.RespawnSinglePlayer(this);
        }
    }

    public void ResetPlayer()
    {
        isDead = false;
        currentHealth = maxHealth;

        // Lógica de Reseteo Local
        if (TryGetComponent(out CharacterController cc)) cc.enabled = false;
        if (TryGetComponent(out PlayerMotor motor)) motor.enabled = false;
        if (TryGetComponent(out RagdollManager ragdoll)) ragdoll.DeactivateRagdoll();

        // Reactivar Input
        if (TryGetComponent(out InputManager inputMgr)) inputMgr.enabled = true;

        if (TryGetComponent(out PlayerLook look))
        {
            look.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (UIManager.Instance != null) UIManager.Instance.ToggleGameplayHUD(true);
        UpdateVisuals();
        UpdateUI();
        StartCoroutine(ReviveSequence());
    }

    IEnumerator ReviveSequence()
    {
        yield return new WaitForFixedUpdate();
        if (TryGetComponent(out PlayerMotor motor)) { motor.enabled = true; motor.ResetMotion(); }
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
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