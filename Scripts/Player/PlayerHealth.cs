using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Referencias UI")]
    public GameObject gameplayHUD;
    private NetworkTransform netTransform;
    private NetworkRigidbody netRigidbody;

    [Header("Configuración de Salud")]
    public float maxHealth = 100f;
    public NetworkVariable<float> netHealth = new NetworkVariable<float>(100f);
    public int teamID = 0;
    public NetworkVariable<bool> netIsDead = new NetworkVariable<bool>(false);
    public bool isDead => netIsDead.Value;

    // Aquí guardamos el golpe fatal
    private DamageInfo fatalDamage;

    [Header("Eventos")]
    public UnityEvent OnDeath;

    private void Awake()
    {
        netTransform = GetComponent<NetworkTransform>();
        netRigidbody = GetComponent<NetworkRigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        netHealth.OnValueChanged += OnHealthChanged;
        if (IsOwner && gameplayHUD != null)
        {
            gameplayHUD.SetActive(true);
            UpdateLocalHUD(netHealth.Value);
        }
    }

    public override void OnNetworkDespawn() => netHealth.OnValueChanged -= OnHealthChanged;
    private void OnHealthChanged(float old, float val) { if (IsOwner) UpdateLocalHUD(val); }
    private void UpdateLocalHUD(float val) { if (UIManager.Instance != null) UIManager.Instance.UpdateHealth(val, maxHealth); }

    public void OnDebugKill()
    {
        if (!IsServer) return;
        DamageInfo debugDmg = new DamageInfo(1000f, transform.position + Vector3.up, Vector3.down, 10000f, false, 0, 999);
        TakeDamage(debugDmg);
    }

    // --- SISTEMA DE DAÑO ---
    public void TakeDamage(DamageInfo info)
    {
        if (!IsServer || netIsDead.Value) return;

        netHealth.Value -= info.DamageAmount;

        if (netHealth.Value <= 0)
        {
            netHealth.Value = 0;
            fatalDamage = info;
            DieServerLogic();
        }
    }

    private void DieServerLogic()
    {
        netIsDead.Value = true;

        if (GameManager.Instance != null)
        {
            // Registrar punto
            int losingSide = (teamID == 0) ? -1 : 1;
            GameManager.Instance.RegisterPoint(losingSide);
        }

        try
        {
            if (TryGetComponent(out BombCarrier carrier) && carrier.isCarrying.Value) carrier.ForceDropBomb();
        }
        catch { }

        if (netTransform != null) netTransform.enabled = false;
        if (netRigidbody != null) netRigidbody.enabled = false;

        DieClientRpc(fatalDamage);
        StartCoroutine(WaitAndRespawnServer());
    }

    [ClientRpc]
    private void DieClientRpc(DamageInfo info)
    {
        if (netTransform != null) netTransform.enabled = false;
        if (netRigidbody != null) netRigidbody.enabled = false;

        if (IsOwner)
        {
            if (gameplayHUD != null) gameplayHUD.SetActive(false);
            OnDeath?.Invoke();
            if (TryGetComponent(out InputManager inputMgr)) inputMgr.enabled = false;
            if (TryGetComponent(out PlayerLook look)) look.enabled = false;
        }
        if (TryGetComponent(out CharacterController cc)) cc.enabled = false;
        if (TryGetComponent(out PlayerMotor motor)) motor.enabled = false;

        // ACTIVAR RAGDOLL
        if (TryGetComponent(out RagdollManager ragdoll))
        {
            ragdoll.ActivateRagdoll(info, IsOwner);
        }
    }

    IEnumerator WaitAndRespawnServer()
    {
        yield return new WaitForSeconds(4.0f);
        if (IsServer && GameManager.Instance != null && !GameManager.Instance.isChaosPhase)
        {
            Vector3 spawnPos = GameManager.Instance.GetRandomSpawnPosition(teamID);
            netIsDead.Value = false;
            netHealth.Value = maxHealth;
            transform.position = spawnPos;

            if (netTransform != null) netTransform.enabled = true;
            if (netRigidbody != null) netRigidbody.enabled = true;

            ForceRespawnClientRpc(spawnPos, Quaternion.identity);
        }
    }

    [ClientRpc]
    public void ForceRespawnClientRpc(Vector3 pos, Quaternion rot) { StartCoroutine(RespawnSequence(pos, rot)); }

    private IEnumerator RespawnSequence(Vector3 pos, Quaternion rot)
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        transform.position = pos; transform.rotation = rot; Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();

        if (netTransform != null) netTransform.enabled = true;
        if (netRigidbody != null) netRigidbody.enabled = true;

        ResetPlayer();

        if (cc != null) cc.enabled = true;
        if (TryGetComponent(out PlayerMotor motor)) { motor.enabled = true; motor.ResetMotion(); }
    }

    public void ResetPlayer()
    {
        if (TryGetComponent(out RagdollManager ragdoll)) ragdoll.DeactivateRagdoll(IsOwner);
        if (IsOwner)
        {
            if (gameplayHUD != null) gameplayHUD.SetActive(true);
            if (TryGetComponent(out InputManager inputMgr)) inputMgr.enabled = true;
            if (TryGetComponent(out PlayerLook look)) { look.enabled = true; look.cam.transform.localRotation = Quaternion.identity; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (UIManager.Instance != null) { UIManager.Instance.UpdateHealth(maxHealth, maxHealth); UIManager.Instance.ToggleGameplayHUD(true); }
        }
        if (TryGetComponent(out PlayerTeamVisuals visuals)) visuals.UpdateVisuals();
    }
}