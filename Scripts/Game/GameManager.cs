using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement; // <--- NECESARIO PARA REINICIAR
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Configuración de Tiempos")]
    public float prepTime = 5f;
    public float roundTime = 60f;
    public float postRoundTime = 5f;

    [Header("Puntuación")]
    public int scoreToWin = 3;

    [Header("Debug")]
    public bool autoStartGame = false; // Si es TRUE, arranca solo (para testear rápido)

    // Variables de Estado
    public float currentTimer = 0f;
    public int scoreTeamA = 0;
    public int scoreTeamB = 0;
    public bool isRoundActive = false;
    public bool isChaosPhase = false;
    public bool isGameRunning = false; // <--- NUEVO: Para saber si la partida está en curso

    [Header("Referencias")]
    public GameObject playerPrefab;
    public BombController bombPrefab;
    public Transform spawnPointBomb;
    public BombController currentBomb;
    public Camera lobbyCamera;

    public bool autoSpawnPlayer = false;
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
        // Buscar referencias iniciales
        if (spawnPointBomb == null)
        {
            var spObj = GameObject.Find("SpawnPointBomb");
            if (spObj) spawnPointBomb = spObj.transform;
        }

        if (spawnVolumes == null || spawnVolumes.Length == 0)
        {
            spawnVolumes = FindObjectsOfType<SpawnVolume>();
        }

        // Si estamos testeando y queremos saltarnos el lobby:
        if (autoStartGame)
        {
            if (autoSpawnPlayer) SpawnLocalPlayer(0); // Spawnear dummy si hace falta
            StartMatch();
        }
        else
        {
            // ESTADO DE ESPERA / LOBBY
            OnGameStateChange?.Invoke("ESPERANDO JUGADORES...");
            // Aquí el juego se queda quieto hasta que alguien llame a StartMatch()
        }
    }

    private void Update()
    {
        // Solo actualizamos UI si la partida está corriendo
        if (isGameRunning && UIManager.Instance != null)
        {
            if (isRoundActive) UIManager.Instance.UpdateGameTimer(currentTimer);
            else UIManager.Instance.UpdatePrepTimer(currentTimer);
        }
    }

    // --- ESTA FUNCIÓN SE LLAMA DESDE UN BOTÓN DE UI O CUANDO ESTÉN LISTOS ---
    public void StartMatch()
    {
        if (isGameRunning) return; // Ya está corriendo
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("MapAmbience");

        scoreTeamA = 0;
        scoreTeamB = 0;
        OnScoreUpdate?.Invoke(scoreTeamA, scoreTeamB); // Reset UI

        StartCoroutine(GameLoop());
    }

    // --- BUCLE PRINCIPAL ---
    IEnumerator GameLoop()
    {
        isGameRunning = true;

        // BUCLE DE RONDAS (Mientras nadie gane)
        while (scoreTeamA < scoreToWin && scoreTeamB < scoreToWin)
        {
            // FASE 1: PREPARACIÓN
            isRoundActive = false;
            isChaosPhase = false;
            OnGameStateChange?.Invoke("PREPARADOS...");

            // Ocultar bomba anterior si quedó
            CleanupScene();

            // Revivir jugadores
            if (localPlayer != null) RespawnSinglePlayer(localPlayer);

            // Reset timers visuales
            if (UIManager.Instance != null) UIManager.Instance.ResetBombTimersVisuals();

            float timer = prepTime;
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

            // FASE 4: POST-RONDA (CAOS)
            isChaosPhase = true;
            timer = postRoundTime;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                currentTimer = timer;
                yield return null;
            }
        }

        // --- FIN DE LA PARTIDA (SALIMOS DEL WHILE) ---
        EndMatch();
    }

    private void EndMatch()
    {
        isGameRunning = false;
        string winner = (scoreTeamA >= scoreToWin) ? "EQUIPO AZUL" : "EQUIPO ROJO";
        Color winnerColor = (scoreTeamA >= scoreToWin) ? Color.blue : Color.red;

        Debug.Log("PARTIDA TERMINADA. GANADOR: " + winner);
        OnGameStateChange?.Invoke("VICTORIA: " + winner);

        // Llamar a la UI de Victoria
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowVictoryScreen(winner, winnerColor);
        }

        // Limpiar la escena de bombas
        CleanupScene();
    }

    // Función para llamar desde un botón de "Reiniciar" en la UI de Victoria
    public void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void SpawnLocalPlayer(int teamID)
    {
        Vector3 spawnPos = GetRandomSpawnPosition(teamID);
        GameObject newPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        localPlayer = newPlayer.GetComponent<PlayerHealth>();
        if (localPlayer != null) localPlayer.teamID = teamID;

        var visuals = newPlayer.GetComponent<PlayerTeamVisuals>();
        if (visuals != null) visuals.UpdateVisuals();

        newPlayer.transform.LookAt(new Vector3(0, spawnPos.y, 0));

        if (lobbyCamera != null)
        {
            lobbyCamera.gameObject.SetActive(false); // Al apagar el objeto, se apaga el Listener
            // O si prefieres no apagar el objeto visual:
            // lobbyCamera.GetComponent<AudioListener>().enabled = false;
        }
    }

    // MODIFICADO: Ahora acepta losingTeamSide = 0 (Empate)
    public void RegisterPoint(int losingTeamSide)
    {
        if (!isRoundActive) return;

        if (losingTeamSide == 1) // Perdió B (Estaba en lado positivo)
        {
            scoreTeamA++;
            endMessage = "PUNTO: EQUIPO A";
        }
        else if (losingTeamSide == -1) // Perdió A (Estaba en lado negativo)
        {
            scoreTeamB++;
            endMessage = "PUNTO: EQUIPO B";
        }
        else // losingTeamSide == 0 (EMPATE / NADIE GANA)
        {
            // No sumamos puntos a nadie
            endMessage = "¡DESTRUCCIÓN MUTUA!";
        }

        OnScoreUpdate?.Invoke(scoreTeamA, scoreTeamB);
        isRoundActive = false;
    }

    public void RespawnSinglePlayer(PlayerHealth player)
    {
        Vector3 targetPos = GetRandomSpawnPosition(player.teamID);
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.position = targetPos;
        player.transform.rotation = Quaternion.identity;
        player.transform.LookAt(new Vector3(0, targetPos.y, 0));

        player.ResetPlayer();
    }

    private void SpawnBomb()
    {
        if (currentBomb != null) Destroy(currentBomb.gameObject);
        if (bombPrefab == null) return;

        Vector3 spawnPos = (spawnPointBomb != null) ? spawnPointBomb.position : new Vector3(0, 2, 0);
        currentBomb = Instantiate(bombPrefab, spawnPos, Quaternion.identity);
    }

    private void CleanupScene()
    {
        BombController[] bombs = FindObjectsOfType<BombController>();
        foreach (var b in bombs) Destroy(b.gameObject);
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