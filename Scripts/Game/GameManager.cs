using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Configuración de Tiempos")]
    public float prepTime = 5f;
    public float roundTime = 60f;
    public float postRoundTime = 5f;

    [Header("Puntuación")]
    public int scoreToWin = 3;

    // --- VARIABLES DE RED ---
    public NetworkVariable<float> netTimer = new NetworkVariable<float>(0f);
    public NetworkVariable<int> netScoreA = new NetworkVariable<int>(0);
    public NetworkVariable<int> netScoreB = new NetworkVariable<int>(0);
    public NetworkVariable<int> netRoundNumber = new NetworkVariable<int>(1);

    // NUEVO: Variable que controla si estamos EN PARTIDA o EN LOBBY
    // Esto asegura que si entras tarde, veas el HUD correctamente
    public NetworkVariable<bool> netIsMatchActive = new NetworkVariable<bool>(false);

    // Mensaje de estado sincronizado
    public NetworkVariable<Unity.Collections.FixedString64Bytes> netGameStateMessage = new NetworkVariable<Unity.Collections.FixedString64Bytes>("");

    // Estado interno
    private bool isGameRunning = false;
    private bool isRoundActive = false;
    public bool isChaosPhase = false;

    public float currentTimer => netTimer.Value;

    [Header("Referencias")]
    public GameObject bombPrefab;
    public Transform spawnPointBomb;
    public BombController currentBomb;

    [Header("Spawns")]
    public SpawnVolume[] spawnVolumes;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        // Suscripciones existentes
        netTimer.OnValueChanged += (oldVal, newVal) => UpdateUITimer(newVal);
        netScoreA.OnValueChanged += (oldVal, newVal) => UpdateUIScore();
        netScoreB.OnValueChanged += (oldVal, newVal) => UpdateUIScore();
        netGameStateMessage.OnValueChanged += (oldVal, newVal) => UpdateUIMessage(newVal.ToString());
        netRoundNumber.OnValueChanged += (oldVal, newVal) => UpdateUIRound(newVal);

        // NUEVO: Suscripción para encender/apagar el HUD automáticamente
        netIsMatchActive.OnValueChanged += (oldVal, newVal) => ToggleMatchHUDLocal(newVal);

        if (IsServer)
        {
            if (spawnVolumes == null || spawnVolumes.Length == 0)
                spawnVolumes = FindObjectsOfType<SpawnVolume>();

            netGameStateMessage.Value = "ESPERANDO HOST...";
            netRoundNumber.Value = 1;
            netIsMatchActive.Value = false; // Empezamos apagados (Lobby)
        }
        else
        {
            // Actualización inicial para clientes
            UpdateUITimer(netTimer.Value);
            UpdateUIScore();
            UpdateUIMessage(netGameStateMessage.Value.ToString());
            UpdateUIRound(netRoundNumber.Value);

            // NUEVO: Forzar estado del HUD al entrar
            ToggleMatchHUDLocal(netIsMatchActive.Value);
        }
    }

    // --- HELPER LOCAL ---
    private void ToggleMatchHUDLocal(bool isActive)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ToggleMatchHUD(isActive);
        }
    }

    // --- UI UPDATES (Igual que antes) ---

    private void UpdateUITimer(float time)
    {
        if (UIManager.Instance == null) return;

        // Solo actualizamos si la partida está activa visualmente
        if (netIsMatchActive.Value && isRoundActive)
        {
            UIManager.Instance.UpdateGameTimer(time);
        }
        else
        {
            UIManager.Instance.UpdatePrepTimer(time);
        }
    }

    private void UpdateUIScore()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateScore(netScoreA.Value, netScoreB.Value);
    }

    private void UpdateUIRound(int round)
    {
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateRoundDisplay(round);
    }

    private void UpdateUIMessage(string msg)
    {
        if (UIManager.Instance == null) return;

        float duration = 0f;
        if (msg.Contains("LUCHEN") || msg.Contains("PUNTO") || msg.Contains("EMPATE"))
        {
            duration = 3f;
        }
        UIManager.Instance.ShowCenterMessage(msg, duration);
    }

    // --- GAME LOOP ---

    public void StartMatch()
    {
        if (!IsServer) return;
        if (isGameRunning) return;

        StartCoroutine(ServerGameLoop());
    }

    IEnumerator ServerGameLoop()
    {
        isGameRunning = true;

        // 1. ACTIVAMOS LA PARTIDA -> Esto dispara el OnValueChanged en todos los clientes
        // y hace aparecer el HUD automáticamente.
        netIsMatchActive.Value = true;

        netScoreA.Value = 0;
        netScoreB.Value = 0;
        netRoundNumber.Value = 1;

        while (netScoreA.Value < scoreToWin && netScoreB.Value < scoreToWin)
        {
            // FASE 1: PREPARACIÓN
            isRoundActive = false;
            isChaosPhase = false;
            netRoundNumber.Value = (netScoreA.Value + netScoreB.Value + 1);
            netGameStateMessage.Value = "";

            CleanupScene();
            RespawnAllPlayers();

            float timer = prepTime;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                netTimer.Value = timer;
                yield return null;
            }

            // FASE 2: JUEGO
            SpawnBomb();
            isRoundActive = true;
            netGameStateMessage.Value = "¡LUCHEN!";

            timer = roundTime;
            while (timer > 0 && isRoundActive)
            {
                timer -= Time.deltaTime;
                netTimer.Value = timer;
                yield return null;
            }

            // FASE 3: FIN DE RONDA
            if (isRoundActive)
            {
                netGameStateMessage.Value = "TIEMPO AGOTADO";
                if (currentBomb != null) currentBomb.ForceExplode();
                yield return new WaitForSeconds(2f);
            }

            isRoundActive = false;
            isChaosPhase = true;

            timer = postRoundTime;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                netTimer.Value = timer;
                yield return null;
            }
        }

        EndMatch();
    }

    private void EndMatch()
    {
        isGameRunning = false;
        bool teamAWins = netScoreA.Value >= scoreToWin;

        string winnerMsg = teamAWins ? "VICTORIA: AZUL" : "VICTORIA: ROJO";
        netGameStateMessage.Value = winnerMsg;

        Color winColor = teamAWins ? Color.blue : Color.red;
        ShowVictoryClientRpc(winnerMsg, winColor);

        CleanupScene();

        // Opcional: Si quieres que al terminar vuelva al Lobby y se borre el HUD:
        // netIsMatchActive.Value = false; 
    }

    [ClientRpc]
    private void ShowVictoryClientRpc(string winnerName, Color color)
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ShowVictoryScreen(winnerName, color);
    }

    // --- PUNTOS Y BOMBA (Igual que antes) ---

    public void RegisterPoint(int losingTeamSide)
    {
        if (!IsServer || !isRoundActive) return;

        if (losingTeamSide == 1)
        {
            netScoreA.Value++;
            netGameStateMessage.Value = "PUNTO PARA AZUL";
        }
        else if (losingTeamSide == -1)
        {
            netScoreB.Value++;
            netGameStateMessage.Value = "PUNTO PARA ROJO";
        }
        else
        {
            netGameStateMessage.Value = "EMPATE TÉCNICO";
        }

        isRoundActive = false;
    }

    private void SpawnBomb()
    {
        if (bombPrefab == null) return;
        if (currentBomb != null && currentBomb.GetComponent<NetworkObject>().IsSpawned)
            currentBomb.GetComponent<NetworkObject>().Despawn();

        Vector3 spawnPos = (spawnPointBomb != null) ? spawnPointBomb.position : new Vector3(0, 5, 0);
        GameObject bombInst = Instantiate(bombPrefab, spawnPos, Quaternion.identity);
        bombInst.GetComponent<NetworkObject>().Spawn();
        currentBomb = bombInst.GetComponent<BombController>();
    }

    private void CleanupScene()
    {
        if (currentBomb != null && currentBomb.NetworkObject != null && currentBomb.NetworkObject.IsSpawned)
            currentBomb.NetworkObject.Despawn();
        currentBomb = null;
    }

    private void RespawnAllPlayers()
    {
        foreach (var player in FindObjectsOfType<PlayerHealth>())
        {
            Vector3 targetPos = GetRandomSpawnPosition(player.teamID);
            if (IsServer)
            {
                player.netHealth.Value = player.maxHealth;
                player.netIsDead.Value = false;
            }
            player.ForceRespawnClientRpc(targetPos, Quaternion.identity);
        }
    }

    public Vector3 GetRandomSpawnPosition(int teamID)
    {
        List<SpawnVolume> validVolumes = new List<SpawnVolume>();
        if (spawnVolumes != null)
        {
            foreach (var vol in spawnVolumes)
                if (vol != null && vol.teamID == teamID) validVolumes.Add(vol);
        }

        if (validVolumes.Count > 0)
            return validVolumes[Random.Range(0, validVolumes.Count)].GetRandomSpawnPoint();

        TeamSelectionUI teamUI = FindObjectOfType<TeamSelectionUI>();
        if (teamUI != null)
        {
            if (teamID == 0 && teamUI.spawnAzul != null) return teamUI.spawnAzul.position;
            if (teamID == 1 && teamUI.spawnRojo != null) return teamUI.spawnRojo.position;
        }
        return new Vector3(0, 5, 0);
    }

    public void RestartMatch()
    {
        if (!IsServer) return;
        StartMatch();
    }
}