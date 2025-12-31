using UnityEngine;
using Unity.Netcode;

public class ConnectionManager : NetworkBehaviour
{
    [Header("Referencias de Spawn")]
    public Transform spawnPointTeamA;
    public Transform spawnPointTeamB;
    public GameObject playerPrefab; // ¡Asegúrate de que este prefab esté en la lista del NetworkManager!

    [Header("Paneles UI")]
    public GameObject panelConexion; // Botones Host/Client
    public GameObject panelEquipos;  // Botones Azul/Rojo

    void Start()
    {
        // Al inicio, solo mostramos opción de conectar
        if (panelConexion != null) panelConexion.SetActive(true);
        if (panelEquipos != null) panelEquipos.SetActive(false);
    }

    // --- FASE 1: CONECTAR ---

    public void OnButtonHost()
    {
        NetworkManager.Singleton.StartHost();
        // El Host se conecta instantáneamente, así que podemos mostrar equipos ya
        // Pero es mejor esperar a OnNetworkSpawn para ser consistentes
    }

    public void OnButtonClient()
    {
        NetworkManager.Singleton.StartClient();
    }

    // Esta función mágica se ejecuta CUANDO YA ESTAMOS CONECTADOS Y SINCRONIZADOS
    public override void OnNetworkSpawn()
    {
        Debug.Log("¡Conexión establecida! Mostrando selección de equipos.");

        if (IsClient) // Tanto Host como Client son "IsClient"
        {
            if (panelConexion != null) panelConexion.SetActive(false);
            if (panelEquipos != null) panelEquipos.SetActive(true);
        }
    }

    // --- FASE 2: ELEGIR EQUIPO ---

    public void OnButtonJoinTeamA()
    {
        // Equipo 1 (Azul)
        SpawnPlayerServerRpc(NetworkManager.Singleton.LocalClientId, 1);
        if (panelEquipos != null) panelEquipos.SetActive(false);
    }

    public void OnButtonJoinTeamB()
    {
        // Equipo -1 (Rojo)
        SpawnPlayerServerRpc(NetworkManager.Singleton.LocalClientId, -1);
        if (panelEquipos != null) panelEquipos.SetActive(false);
    }

    // --- LÓGICA DEL SERVIDOR ---

    [ServerRpc(RequireOwnership = false)]
    private void SpawnPlayerServerRpc(ulong clientId, int teamID)
    {
        // Seguridad: Solo el servidor puede ejecutar esto
        if (!IsServer) return;

        Debug.Log($"Spawneando jugador {clientId} en equipo {teamID}");

        // 1. Elegir posición
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;

        if (teamID == 1 && spawnPointTeamA != null)
        {
            pos = spawnPointTeamA.position;
            rot = spawnPointTeamA.rotation;
        }
        else if (teamID == -1 && spawnPointTeamB != null)
        {
            pos = spawnPointTeamB.position;
            rot = spawnPointTeamB.rotation;
        }

        // 2. Crear Objeto
        GameObject newPlayer = Instantiate(playerPrefab, pos, rot);

        // 3. Configurar Datos (Vida, Equipo, etc)
        var health = newPlayer.GetComponent<PlayerHealth>();
        if (health != null) health.teamID = teamID;

        // 4. DARLE VIDA EN LA RED
        // IMPORTANTE: Esto fallará si el prefab no está en la lista del NetworkManager
        newPlayer.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
    }
}