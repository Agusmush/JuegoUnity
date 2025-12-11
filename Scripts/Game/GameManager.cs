using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Configuración de Partida")]
    public float roundTime = 90f;
    public float postRoundTime = 8f;
    public int scoreToWin = 3;

    // Variables de Red
    public NetworkVariable<float> netTimer = new NetworkVariable<float>(90f);
    public NetworkVariable<int> scoreTeamA = new NetworkVariable<int>(0);
    public NetworkVariable<int> scoreTeamB = new NetworkVariable<int>(0);
    public NetworkVariable<bool> isRoundActive = new NetworkVariable<bool>(false);

    [Header("Referencias")]
    public GameObject playerPrefab;
    public Camera lobbyCamera;
    public BombController bombPrefab;
    public Transform spawnPointBomb;
    public BombController currentBomb;

    [Header("Spawns")]
    public SpawnVolume[] spawnVolumes;

    [System.Serializable] public class ScoreEvent : UnityEvent<int, int> { }
    public ScoreEvent OnScoreUpdate;
    [System.Serializable] public class GameStateEvent : UnityEvent<string> { }
    public GameStateEvent OnGameStateChange;

    private string endMessage = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(true);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) StartCoroutine(GameLoop());
    }

    private void Update()
    {
        if (UIManager.Instance != null)
        {
            if (isRoundActive.Value) UIManager.Instance.UpdateGameTimer(netTimer.Value);
            else UIManager.Instance.UpdatePrepTimer(netTimer.Value);
        }
    }

    // --- BUCLE PRINCIPAL ---
    IEnumerator GameLoop()
    {
        while (scoreTeamA.Value < scoreToWin && scoreTeamB.Value < scoreToWin)
        {
            // FASE 1: PREPARACIÓN
            isRoundActive.Value = false;
            UpdateGameStateClientRpc("PREPARACIÓN");

            CleanupScene();

            // --- NUEVO: REVIVIR A TODOS AL INICIO DE RONDA ---
            RespawnAllPlayers();
            // -------------------------------------------------

            float timer = 5f;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                netTimer.Value = timer;
                yield return null;
            }

            // FASE 2: JUEGO
            SpawnBomb();
            isRoundActive.Value = true;
            UpdateGameStateClientRpc("¡LUCHEN!");

            timer = roundTime;
            while (timer > 0 && isRoundActive.Value)
            {
                timer -= Time.deltaTime;
                netTimer.Value = timer;
                yield return null;
            }

            // FASE 3: FIN DE RONDA
            if (isRoundActive.Value)
            {
                UpdateGameStateClientRpc("TIEMPO AGOTADO");
                if (currentBomb != null) currentBomb.Explode();
                isRoundActive.Value = false;
                yield return new WaitForSeconds(1f);
            }
            else
            {
                UpdateGameStateClientRpc(endMessage);
            }

            // FASE 4: CAOS
            timer = postRoundTime;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                netTimer.Value = timer;
                yield return null;
            }
        }
    }

    // --- FUNCIONES ---
    public void RequestJoinGame(int chosenTeamID)
    {
        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost)
            RequestSpawnServerRpc(chosenTeamID, NetworkManager.Singleton.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnServerRpc(int teamID, ulong clientId)
    {
        // 1. Calcular posición
        Vector3 spawnPos = GetRandomSpawnPosition(teamID);

        // 2. Instanciar el Prefab (En memoria local del server)
        GameObject newPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        // --- CAMBIO DE ORDEN AQUÍ ---

        // 3. PRIMERO: SPAWN EN RED (Darle vida y dueño)
        // Al hacer esto, el objeto ya es válido para recibir datos de red.
        var netObj = newPlayer.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId);

        // 4. SEGUNDO: ASIGNAR EL EQUIPO
        // Ahora que está spawneado, la NetworkVariable detecta el cambio y lo sincroniza.
        var health = newPlayer.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.netTeamID.Value = teamID;
        }

        // 5. Orientar (Esto es transform local, da igual el orden, pero mejor al final)
        newPlayer.transform.LookAt(new Vector3(0, spawnPos.y, 0));
    }

    public void RegisterPoint(int losingTeamSide)
    {
        if (!IsServer) return;
        if (!isRoundActive.Value) return;

        if (losingTeamSide == 1)
        {
            scoreTeamA.Value++;
            endMessage = "PUNTO PARA EQUIPO A";
        }
        else
        {
            scoreTeamB.Value++;
            endMessage = "PUNTO PARA EQUIPO B";
        }

        UpdateScoreClientRpc(scoreTeamA.Value, scoreTeamB.Value);
        isRoundActive.Value = false;
    }

    [ClientRpc]
    private void UpdateScoreClientRpc(int scoreA, int scoreB)
    {
        OnScoreUpdate?.Invoke(scoreA, scoreB);
    }

    [ClientRpc]
    private void UpdateGameStateClientRpc(string msg)
    {
        OnGameStateChange?.Invoke(msg);
    }

    // Revivir a uno solo (durante la partida)
    public void RespawnSinglePlayer(PlayerHealth player)
    {
        if (!IsServer) return;
        Vector3 targetPos = GetRandomSpawnPosition(player.teamID);
        player.GetComponent<CharacterController>().enabled = false;
        player.transform.position = targetPos;
        player.ResetPlayer();
        player.transform.LookAt(new Vector3(0, targetPos.y, 0));
    }

    // --- NUEVA FUNCIÓN: REVIVIR A TODOS ---
    private void RespawnAllPlayers()
    {
        // Buscamos todos los jugadores conectados
        foreach (var clientID in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientID, out NetworkClient client))
            {
                if (client.PlayerObject != null)
                {
                    PlayerHealth player = client.PlayerObject.GetComponent<PlayerHealth>();
                    if (player != null)
                    {
                        // Usamos la misma lógica de respawn individual para cada uno
                        RespawnSinglePlayer(player);
                    }
                }
            }
        }
    }

    private void SpawnBomb()
    {
        // 1. Limpiar bomba anterior
        if (currentBomb != null)
        {
            if (currentBomb.GetComponent<NetworkObject>() != null && currentBomb.GetComponent<NetworkObject>().IsSpawned)
                currentBomb.GetComponent<NetworkObject>().Despawn();
            else
                Destroy(currentBomb.gameObject);
        }

        // 2. DIAGNÓSTICO DE ERRORES (Aquí sabrás qué pasa)
        if (bombPrefab == null)
        {
            Debug.LogError("⛔ ERROR FATAL: La casilla 'Bomb Prefab' está vacía en el GameManager.");
            return; // Abortamos para que no se congele el juego
        }

        if (spawnPointBomb == null)
        {
            Debug.LogWarning("⚠️ AVISO: 'Spawn Point Bomb' está vacío. Usando posición (0, 2, 0) por defecto.");
        }

        // 3. Determinar posición segura
        Vector3 spawnPos = (spawnPointBomb != null) ? spawnPointBomb.position : new Vector3(0, 2, 0);

        // 4. Instanciar
        currentBomb = Instantiate(bombPrefab, spawnPos, Quaternion.identity);

        // 5. Spawn en Red (Vital para Multiplayer)
        var netObj = currentBomb.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }
        else
        {
            Debug.LogError("⛔ ERROR: Tu Prefab de la Bomba NO tiene el componente 'NetworkObject'. Agrégaselo.");
        }
    }

    private void CleanupScene()
    {
        BombController[] bombs = FindObjectsOfType<BombController>();
        foreach (var b in bombs)
        {
            if (b.GetComponent<NetworkObject>().IsSpawned) b.GetComponent<NetworkObject>().Despawn();
            else Destroy(b.gameObject);
        }
        currentBomb = null;
    }

    private Vector3 GetRandomSpawnPosition(int teamID)
    {
        List<SpawnVolume> validVolumes = new List<SpawnVolume>();
        foreach (var vol in spawnVolumes)
        {
            if (vol != null && vol.teamID == teamID) validVolumes.Add(vol);
        }
        if (validVolumes.Count == 0) return spawnPointBomb != null ? spawnPointBomb.position : new Vector3(0, 5, 0);
        return validVolumes[Random.Range(0, validVolumes.Count)].GetRandomSpawnPoint();
    }
}