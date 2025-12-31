using UnityEngine;
using Unity.Netcode;

public class PlayerTeamVisuals : NetworkBehaviour
{
    private PlayerHealth playerHealth;

    [Header("Configuración Visual")]
    public Renderer targetRenderer;

    [Header("Materiales de Equipo")]
    public Material materialTeamA; // Pon aquí el material AZUL
    public Material materialTeamB; // Pon aquí el material ROJO

    // Por defecto nace siendo 0 (Azul)
    public NetworkVariable<int> netTeamID = new NetworkVariable<int>(0);

    void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        netTeamID.OnValueChanged += OnTeamChanged;
        // Al nacer, aplicamos el color que tenga la variable (0 o 1)
        UpdateVisuals(netTeamID.Value);
    }

    private void OnTeamChanged(int oldTeam, int newTeam)
    {
        UpdateVisuals(newTeam);
    }

    // SOBRECARGA: Si PlayerHealth llama a esto sin argumentos, usamos el valor actual
    public void UpdateVisuals()
    {
        UpdateVisuals(netTeamID.Value);
    }

    public void UpdateVisuals(int teamID)
    {
        if (targetRenderer == null) return;

        // Sincronizamos también el script de vida
        if (playerHealth != null)
        {
            playerHealth.teamID = teamID;
        }

        // --- LÓGICA CORREGIDA 0 vs 1 ---
        if (teamID == 0)
        {
            // 0 = AZUL
            targetRenderer.material = materialTeamA;
        }
        else if (teamID == 1)
        {
            // 1 = ROJO
            targetRenderer.material = materialTeamB;
        }
    }

    public void SetTeam(int newTeamID)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            netTeamID.Value = newTeamID;
        }
    }
}