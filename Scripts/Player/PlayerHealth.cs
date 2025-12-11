using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using Unity.Netcode;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Configuración de Salud")]
    public float maxHealth = 100f;

    // --- VARIABLES DE RED ---
    public NetworkVariable<float> netHealth = new NetworkVariable<float>(100f);
    public NetworkVariable<int> netTeamID = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int teamID { get { return netTeamID.Value; } }
    public float currentHealth { get { return netHealth.Value; } }

    public bool isDead = false;
    private Transform lastAttacker;

    [Header("Eventos")]
    public UnityEvent OnDeath;
    public UnityEvent OnRevive;

    // DEBUG para InputManager
    public Transform testKiller;

    public override void OnNetworkSpawn()
    {
        UpdateVisuals();
        UpdateUI();
        netHealth.OnValueChanged += (oldVal, newVal) => UpdateUI();

        if (IsOwner && UIManager.Instance != null)
            UIManager.Instance.ToggleGameplayHUD(true);
    }

    public void OnDebugKill()
    {
        if (IsServer) TakeDamage(maxHealth * 10f);
        else RequestSuicideServerRpc();
    }

    [ServerRpc]
    private void RequestSuicideServerRpc()
    {
        TakeDamage(maxHealth * 10f);
    }

    public void TakeDamage(float amount, Transform attacker = null)
    {
        if (!IsServer) return;

        if (attacker != null) lastAttacker = attacker;

        netHealth.Value -= amount;

        if (netHealth.Value <= 0 && !isDead)
        {
            netHealth.Value = 0;
            Die();
        }
    }
    public void TakeDamage(float amount) { TakeDamage(amount, null); }

    private void Die()
    {
        isDead = true;
        DieClientRpc();

        if (GameManager.Instance != null) StartCoroutine(WaitAndRespawn());
    }

    [ClientRpc]
    private void DieClientRpc()
    {
        OnDeath?.Invoke();

        if (TryGetComponent(out CharacterController cc)) cc.enabled = false;
        if (TryGetComponent(out PlayerMotor motor)) motor.enabled = false;
        if (TryGetComponent(out RagdollManager ragdoll)) ragdoll.ActivateRagdoll(lastAttacker);
        if (TryGetComponent(out BombCarrier carrier) && carrier.isCarrying) carrier.DropBomb();

        // Desactivar Input y Cámara
        if (TryGetComponent(out InputManager inputMgr)) inputMgr.enabled = false;
        if (TryGetComponent(out PlayerLook look)) look.enabled = false;

        if (UIManager.Instance != null && IsOwner) UIManager.Instance.ToggleGameplayHUD(false);
    }

    IEnumerator WaitAndRespawn()
    {
        yield return new WaitForSeconds(4.0f);

        // --- CORRECCIÓN: SOLO RESPAWNEAR SI LA RONDA SIGUE ACTIVA ---
        if (GameManager.Instance != null && GameManager.Instance.isRoundActive.Value)
        {
            GameManager.Instance.RespawnSinglePlayer(this);
        }
        // Si la ronda terminó, no hacemos nada. El GameManager nos revivirá al iniciar la siguiente.
    }

    public void ResetPlayer()
    {
        isDead = false;
        if (IsServer) netHealth.Value = maxHealth;
        ResetPlayerClientRpc();
    }

    [ClientRpc]
    private void ResetPlayerClientRpc()
    {
        if (TryGetComponent(out CharacterController cc)) cc.enabled = false;
        if (TryGetComponent(out PlayerMotor motor)) motor.enabled = false;
        if (TryGetComponent(out RagdollManager ragdoll)) ragdoll.DeactivateRagdoll();

        // Reactivar Input
        if (TryGetComponent(out InputManager inputMgr)) inputMgr.enabled = true;

        if (TryGetComponent(out PlayerLook look) && IsOwner)
        {
            look.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (UIManager.Instance != null && IsOwner) UIManager.Instance.ToggleGameplayHUD(true);
        UpdateVisuals();
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
        if (IsOwner && UIManager.Instance != null) UIManager.Instance.UpdateHealth(netHealth.Value, maxHealth);
    }
    public void Heal(float amount) { if (IsServer) netHealth.Value += amount; }
}