using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("CONTENEDOR GLOBAL (Match HUD)")]
    // Arrastra aquí un objeto vacío que contenga: Slider, Timers, Scores, Ronda.
    public GameObject matchHUDContainer;

    [Header("Marcador Principal")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI scoreAText;
    public TextMeshProUGUI scoreBText;
    public TextMeshProUGUI roundInfoText; // El texto de "RONDA X"

    [Header("Avisos Centrales")]
    public TextMeshProUGUI stateText; // El texto de "¡LUCHEN!"

    [Header("Barra de Tensión")]
    public Slider bombSlider;
    public Image bombHandleImage;
    public TextMeshProUGUI centerWarningText;

    [Header("Timers de Bomba (10s)")]
    public TextMeshProUGUI bombTimerAText; // Lado Izquierdo (Azul)
    public TextMeshProUGUI bombTimerBText; // Lado Derecho (Rojo)

    [Header("Jugador & HUD (Player HUD)")]
    public GameObject gameplayHUDContainer; // Solo la vida y munición
    public Image healthBarFill;
    public TextMeshProUGUI healthNumText;
    public Gradient healthGradient;

    [Header("Colores")]
    public Color safeColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
    public Color teamAColor = Color.blue;
    public Color teamBColor = Color.red;

    public Color suddenDeathColor = Color.yellow;
    public Color snapshotColor = Color.cyan;
    public Color normalTimeColor = Color.white;
    public Color criticalTimeColor = Color.red;

    [Header("Pantalla Final")]
    public GameObject victoryPanel;
    public TextMeshProUGUI victoryText;

    // Variables Internas
    private Vector3 originalPosBombA;
    private Vector3 originalPosBombB;
    private Vector3 originalTimerScale;
    private Coroutine resetCoroutineA;
    private Coroutine resetCoroutineB;
    private Coroutine stateTextCoroutine;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        if (timerText != null) originalTimerScale = timerText.transform.localScale;
        if (bombTimerAText != null) originalPosBombA = bombTimerAText.transform.localPosition;
        if (bombTimerBText != null) originalPosBombB = bombTimerBText.transform.localPosition;

        if (centerWarningText) centerWarningText.gameObject.SetActive(false);
        if (stateText) stateText.gameObject.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);

        // ESTADO INICIAL: Todo apagado (estamos en el Lobby)
        ToggleGameplayHUD(false); // Apagamos vida
        ToggleMatchHUD(false);    // Apagamos Slider y marcadores

        ResetBombTimersVisuals();
    }

    private void Update()
    {
        UpdateSliderPosition();
    }

    // =========================================================
    //              CONTROL DE VISIBILIDAD (NUEVO)
    // =========================================================

    // Se llama desde GameManager al iniciar la partida
    public void ToggleMatchHUD(bool isVisible)
    {
        // Opción A: Usar el contenedor padre (RECOMENDADO)
        if (matchHUDContainer != null)
        {
            matchHUDContainer.SetActive(isVisible);
        }
        // Opción B: Apagar cosas sueltas si no usaste el contenedor
        else
        {
            if (bombSlider != null) bombSlider.gameObject.SetActive(isVisible);
            if (timerText != null) timerText.gameObject.SetActive(isVisible);
            if (scoreAText != null) scoreAText.gameObject.SetActive(isVisible);
            if (scoreBText != null) scoreBText.gameObject.SetActive(isVisible);
            if (roundInfoText != null) roundInfoText.gameObject.SetActive(isVisible);
            if (bombTimerAText != null && isVisible == false) bombTimerAText.gameObject.SetActive(false);
            if (bombTimerBText != null && isVisible == false) bombTimerBText.gameObject.SetActive(false);
        }
    }

    // Se llama desde PlayerHealth al spawnear/morir
    public void ToggleGameplayHUD(bool isVisible)
    {
        if (gameplayHUDContainer != null)
            gameplayHUDContainer.SetActive(isVisible);
    }

    // =========================================================
    //              LÓGICA DE BARRA Y SLIDER
    // =========================================================

    private void UpdateSliderPosition()
    {
        if (bombSlider == null) return;

        // 1. Buscamos la bomba
        BombController bomb = FindObjectOfType<BombController>();

        // 2. Lógica de Visibilidad INTELIGENTE
        if (bomb != null)
        {
            // SI HAY BOMBA: Aseguramos que el slider se vea y actualizamos valor
            if (!bombSlider.gameObject.activeSelf) bombSlider.gameObject.SetActive(true);

            float mapLimit = 50f;
            float t = Mathf.InverseLerp(-mapLimit, mapLimit, bomb.transform.position.z);
            bombSlider.value = t;

            if (bombHandleImage != null)
            {
                if (t < 0.45f) bombHandleImage.color = teamAColor;
                else if (t > 0.55f) bombHandleImage.color = teamBColor;
                else bombHandleImage.color = Color.white;
            }
        }
        else
        {
            // SI NO HAY BOMBA (Lobby o Fase de Preparación): Ocultamos SOLO el slider
            // El Timer y el Score seguirán visibles si el MatchHUD está activo.
            if (bombSlider.gameObject.activeSelf) bombSlider.gameObject.SetActive(false);
        }
    }

    // =========================================================
    //              TIMERS DE BOMBA
    // =========================================================

    public void UpdateBombTimers(float timeRemaining, int activeSide, bool isGracePeriod)
    {
        float displayTime = Mathf.Max(0f, timeRemaining);
        string formattedTime = displayTime.ToString("F2");

        // 1. SI ES CENTRO (LADO 0)
        if (activeSide == 0)
        {
            SetTimerStandby(bombTimerAText);
            SetTimerStandby(bombTimerBText);

            if (centerWarningText)
            {
                if (isGracePeriod)
                {
                    centerWarningText.gameObject.SetActive(false);
                }
                else
                {
                    centerWarningText.gameObject.SetActive(true);
                    centerWarningText.text = "¡MUERTE SÚBITA!\n" + formattedTime;
                    centerWarningText.color = Color.Lerp(suddenDeathColor, Color.red, Mathf.PingPong(Time.time * 10, 1));
                }
            }
        }
        // 2. SI ES UN BANDO (LADO 1 o -1)
        else
        {
            if (centerWarningText) centerWarningText.gameObject.SetActive(false);

            if (activeSide == 1) // Lado B (Rojo/Derecha)
            {
                ActivateTimer(bombTimerBText, formattedTime, criticalTimeColor, timeRemaining);
                SetTimerStandby(bombTimerAText);
            }
            else if (activeSide == -1) // Lado A (Azul/Izquierda)
            {
                ActivateTimer(bombTimerAText, formattedTime, criticalTimeColor, timeRemaining);
                SetTimerStandby(bombTimerBText);
            }
        }
    }

    void ActivateTimer(TextMeshProUGUI txt, string timeStr, Color col, float rawTime)
    {
        if (txt == null) return;
        txt.gameObject.SetActive(true);
        txt.text = timeStr;
        txt.color = col;

        if (rawTime < 3.0f)
        {
            Vector3 basePos = (txt == bombTimerAText ? originalPosBombA : originalPosBombB);
            txt.transform.localPosition = basePos + (Vector3)(Random.insideUnitCircle * 2f);
        }
        else
        {
            txt.transform.localPosition = (txt == bombTimerAText ? originalPosBombA : originalPosBombB);
        }
    }

    void SetTimerStandby(TextMeshProUGUI txt)
    {
        if (txt == null) return;
        txt.gameObject.SetActive(true);
        txt.text = "10.00";
        txt.color = safeColor;
        txt.transform.localPosition = (txt == bombTimerAText ? originalPosBombA : originalPosBombB);
    }

    public void ResetBombTimersVisuals()
    {
        SetTimerStandby(bombTimerAText);
        SetTimerStandby(bombTimerBText);
        if (centerWarningText) centerWarningText.gameObject.SetActive(false);
    }

    public void OnBombSideChanged(int newActiveSide)
    {
        if (newActiveSide == 1 && resetCoroutineA != null) StopCoroutine(resetCoroutineA);
        if (newActiveSide == -1 && resetCoroutineB != null) StopCoroutine(resetCoroutineB);

        if (newActiveSide == 1) resetCoroutineA = StartCoroutine(SnapshotSequence(bombTimerAText, originalPosBombA));
        else if (newActiveSide == -1) resetCoroutineB = StartCoroutine(SnapshotSequence(bombTimerBText, originalPosBombB));
    }

    IEnumerator SnapshotSequence(TextMeshProUGUI timerText, Vector3 originalPos)
    {
        timerText.transform.localPosition = originalPos;
        timerText.color = snapshotColor;
        yield return new WaitForSeconds(0.1f);
        timerText.color = safeColor;
        timerText.text = "10.00";
    }

    // =========================================================
    //              TIMERS PRINCIPALES & SCORE
    // =========================================================

    public void UpdatePrepTimer(float currentTime)
    {
        timerText.text = Mathf.CeilToInt(currentTime).ToString("0");
        timerText.color = normalTimeColor;

        if (originalTimerScale != Vector3.zero)
            timerText.transform.localScale = originalTimerScale;
    }

    public void UpdateGameTimer(float currentTime)
    {
        if (currentTime > 9f)
        {
            timerText.text = Mathf.CeilToInt(currentTime).ToString("0");
            timerText.color = normalTimeColor;
            timerText.transform.localScale = originalTimerScale;
        }
        else
        {
            timerText.text = Mathf.Max(0, currentTime).ToString("F2");
            timerText.color = criticalTimeColor;
            float heartbeat = 1f + Mathf.PingPong(Time.time * 5f, 0.2f);
            timerText.transform.localScale = originalTimerScale * 0.75f * heartbeat;
        }
    }

    public void UpdateRoundDisplay(int currentRound)
    {
        if (roundInfoText != null)
        {
            roundInfoText.gameObject.SetActive(true);
            roundInfoText.text = "RONDA " + currentRound;
        }
    }

    public void UpdateScore(int scoreA, int scoreB)
    {
        scoreAText.text = scoreA.ToString();
        scoreBText.text = scoreB.ToString();
    }

    public void ShowCenterMessage(string message, float duration = 0f)
    {
        if (stateText == null) return;

        if (stateTextCoroutine != null) StopCoroutine(stateTextCoroutine);

        stateText.text = message;

        if (string.IsNullOrEmpty(message))
        {
            stateText.gameObject.SetActive(false);
        }
        else
        {
            stateText.gameObject.SetActive(true);
            if (duration > 0f)
            {
                stateTextCoroutine = StartCoroutine(HideStateTextAfterDelay(duration));
            }
        }
    }

    // =========================================================
    //              SALUD
    // =========================================================

    public void UpdateHealth(float current, float max)
    {
        float percentage = current / max;

        if (healthNumText != null)
        {
            healthNumText.text = Mathf.CeilToInt(current).ToString();
            healthNumText.color = healthGradient.Evaluate(percentage);
        }

        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = percentage;
            healthBarFill.color = healthGradient.Evaluate(percentage);
        }
    }

    // =========================================================
    //              VICTORIA
    // =========================================================

    public void ShowVictoryScreen(string winnerName, Color winnerColor)
    {
        if (stateText != null) stateText.gameObject.SetActive(false);

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            if (victoryText != null)
            {
                victoryText.text = "¡" + winnerName + " GANA!";
                victoryText.color = winnerColor;
            }
        }
        // Apagamos todo lo demás para dejar limpia la pantalla de victoria
        ToggleGameplayHUD(false);
        ToggleMatchHUD(false);
    }

    private IEnumerator HideStateTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        stateText.gameObject.SetActive(false);
        stateTextCoroutine = null;
    }
}