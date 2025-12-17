using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Configuración de Partida")]
    public float roundTime = 90f;
    public float postRoundTime = 8f;
    public int scoreToWin = 3;

    // Variables Normales
    public float currentTimer = 90f;
    public int scoreTeamA = 0;
    public int scoreTeamB = 0;
    public bool isRoundActive = false;

    [Header("Referencias")]
    public GameObject playerPrefab;
    public BombController bombPrefab;
    public Transform spawnPointBomb;
    public BombController currentBomb;

    // Si quieres spawnear al jugador automáticamente al iniciar
    public bool autoSpawnPlayer = true;
    private PlayerHealth localPlayer;

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
        // Bloque de autoconfiguración
        if (spawnPointBomb == null)
        {
            var spObj = GameObject.Find("SpawnPointBomb");
            if (spObj) spawnPointBomb = spObj.transform;
        }

        if (spawnVolumes == null || spawnVolumes.Length == 0)
        {
            spawnVolumes = FindObjectsOfType<SpawnVolume>();
        }

        // Spawnear Jugador Local
        if (autoSpawnPlayer)
        {
            SpawnLocalPlayer(0); // Team ID 0 por defecto
        }

        // Arrancar el juego
        StartCoroutine(GameLoop());
    }

    private void Update()
    {
        if (UIManager.Instance != null)
        {
            if (isRoundActive) UIManager.Instance.UpdateGameTimer(currentTimer);
            else UIManager.Instance.UpdatePrepTimer(currentTimer);
        }
    }

    // --- BUCLE PRINCIPAL ---
    IEnumerator GameLoop()
    {
        while (scoreTeamA < scoreToWin && scoreTeamB < scoreToWin)
        {
            // FASE 1: PREPARACIÓN
            isRoundActive = false;
            OnGameStateChange?.Invoke("PREPARACIÓN");

            CleanupScene();

            // Revivir al jugador si está muerto o resetearlo
            if (localPlayer != null) RespawnSinglePlayer(localPlayer);

            float timer = 5f;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                currentTimer = timer;
                yield return null;
            }

            // FASE 2: JUEGO
            SpawnBomb();
            isRoundActive = true;
            OnGameStateChange?.Invoke("¡LUCHEN!");

            timer = roundTime;
            while (timer > 0 && isRoundActive)
            {
                timer -= Time.deltaTime;
                currentTimer = timer;
                yield return null;
            }

            // FASE 3: FIN DE RONDA
            if (isRoundActive)
            {
                OnGameStateChange?.Invoke("TIEMPO AGOTADO");
                if (currentBomb != null) currentBomb.Explode();
                isRoundActive = false;
                yield return new WaitForSeconds(1f);
            }
            else
            {
                OnGameStateChange?.Invoke(endMessage);
            }

            // FASE 4: CAOS
            timer = postRoundTime;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                currentTimer = timer;
                yield return null;
            }
        }
    }

    // --- FUNCIONES ---

    public void SpawnLocalPlayer(int teamID)
    {
        Vector3 spawnPos = GetRandomSpawnPosition(teamID);
        GameObject newPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        localPlayer = newPlayer.GetComponent<PlayerHealth>();
        if (localPlayer != null)
        {
            localPlayer.teamID = teamID;
        }

        newPlayer.transform.LookAt(new Vector3(0, spawnPos.y, 0));
    }

    public void RegisterPoint(int losingTeamSide)
    {
        if (!isRoundActive) return;

        if (losingTeamSide == 1)
        {
            scoreTeamA++;
            endMessage = "PUNTO PARA EQUIPO A";
        }
        else
        {
            scoreTeamB++;
            endMessage = "PUNTO PARA EQUIPO B";
        }

        OnScoreUpdate?.Invoke(scoreTeamA, scoreTeamB);
        isRoundActive = false;
    }

    public void RespawnSinglePlayer(PlayerHealth player)
    {
        Vector3 targetPos = GetRandomSpawnPosition(player.teamID);
        player.GetComponent<CharacterController>().enabled = false;
        player.transform.position = targetPos;
        player.ResetPlayer();
        player.transform.LookAt(new Vector3(0, targetPos.y, 0));
    }

    private void SpawnBomb()
    {
        if (currentBomb != null) Destroy(currentBomb.gameObject);

        if (bombPrefab == null)
        {
            Debug.LogError("Error: Falta Bomb Prefab en GameManager");
            return;
        }

        Vector3 spawnPos = (spawnPointBomb != null) ? spawnPointBomb.position : new Vector3(0, 2, 0);
        currentBomb = Instantiate(bombPrefab, spawnPos, Quaternion.identity);
    }

    private void CleanupScene()
    {
        BombController[] bombs = FindObjectsOfType<BombController>();
        foreach (var b in bombs)
        {
            Destroy(b.gameObject);
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