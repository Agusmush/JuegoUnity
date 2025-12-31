using UnityEngine;
using Unity.Netcode;

public class TeamSelectionUI : NetworkBehaviour
{
    [Header("Referencias")]
    public GameObject panelEquipos;
    public GameObject playerPrefab; // Usamos tu prefab único
    public Transform spawnAzul;
    public Transform spawnRojo;

    [Header("Solo Host")]
    public GameObject botonIniciarPartida;

    public override void OnNetworkSpawn()
    {
        if (panelEquipos != null) panelEquipos.SetActive(true);

        if (botonIniciarPartida != null)
        {
            botonIniciarPartida.SetActive(IsServer);
        }
    }

    public void ClickIniciarPartida()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartMatch();
        }
    }

    public void ElegirAzul()
    {
        SpawnPlayerServerRpc(0, NetworkManager.Singleton.LocalClientId);
        if (panelEquipos != null) panelEquipos.SetActive(false);
    }

    public void ElegirRojo()
    {
        SpawnPlayerServerRpc(1, NetworkManager.Singleton.LocalClientId);
        if (panelEquipos != null) panelEquipos.SetActive(false);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnPlayerServerRpc(int teamID, ulong clientId)
    {
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;

        // Lógica de posición
        if (teamID == 0 && spawnAzul != null)
        {
            pos = spawnAzul.position;
            rot = spawnAzul.rotation;
        }
        else if (teamID == 1 && spawnRojo != null)
        {
            pos = spawnRojo.position;
            rot = spawnRojo.rotation;
        }

        // 1. Instanciar
        GameObject nuevoJugador = Instantiate(playerPrefab, pos, rot);

        // 2. Obtener NetworkObject
        var netObj = nuevoJugador.GetComponent<NetworkObject>();

        // 3. SPAWNEAR PRIMERO (Esto arregla el error "doesn't know its NetworkBehaviour")
        netObj.SpawnAsPlayerObject(clientId, true);

        // 4. ASIGNAR VARIABLE DESPUÉS DE SPAWNEAR
        // Como ya está spawneado, la NetworkVariable interna se sincroniza bien.
        PlayerTeamVisuals visualScript = nuevoJugador.GetComponent<PlayerTeamVisuals>();
        if (visualScript != null)
        {
            visualScript.SetTeam(teamID);
        }
    }
}