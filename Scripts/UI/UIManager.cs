using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Marcador Principal")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI scoreAText;
    public TextMeshProUGUI scoreBText;
    public TextMeshProUGUI stateText;

    [Header("Barra de Tensión")]
    public Slider bombSlider;
    public Image bombHandleImage;  // El círculo que se mueve (Handle)
    public TextMeshProUGUI centerWarningText;

    [Header("Timers de Bomba (10s)")]
    public TextMeshProUGUI bombTimerAText; // Lado Izquierdo (Azul)
    public TextMeshProUGUI bombTimerBText; // Lado Derecho (Rojo)

    [Header("Jugador & HUD")]
    public GameObject gameplayHUDContainer; // Contenedor del HUD
    public Image healthBarFill;
    public TextMeshProUGUI healthNumText;
    public Gradient healthGradient;

    [Header("Colores")]
    public Color safeColor = new Color(0.8f, 0.8f, 0.8f, 0.5f); // Gris clarito (Standby)
    public Color teamAColor = Color.blue; // Equipo A (Izquierda)
    public Color teamBColor = Color.red;  // Equipo B (Derecha)

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

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        if (timerText != null) originalTimerScale = timerText.transform.localScale;
        if (bombTimerAText != null) originalPosBombA = bombTimerAText.transform.localPosition;
        if (bombTimerBText != null) originalPosBombB = bombTimerBText.transform.localPosition;

        if (centerWarningText) centerWarningText.gameObject.SetActive(false);
        if (stateText) stateText.gameObject.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);

        ToggleGameplayHUD(false); // Ocultar HUD al inicio
        ResetBombTimersVisuals(); // Poner timers en gris
    }

    private void Update()
    {
        UpdateSliderPosition();
    }

    // =========================================================
    //              LÓGICA DE BARRA Y SLIDER
    // =========================================================

    private void UpdateSliderPosition()
    {
        if (bombSlider == null) return;

        BombController bomb = FindObjectOfType<BombController>();
        if (bomb != null)
        {
            float mapLimit = 50f;
            float t = Mathf.InverseLerp(-mapLimit, mapLimit, bomb.transform.position.z);
            bombSlider.value = t;

            // Colorear el Círculo (Handle) según posición
            if (bombHandleImage != null)
            {
                if (t < 0.45f) bombHandleImage.color = teamAColor;      // Gana A (Azul)
                else if (t > 0.55f) bombHandleImage.color = teamBColor; // Gana B (Rojo)
                else bombHandleImage.color = Color.white;               // Centro
            }
        }
    }

    // =========================================================
    //              LÓGICA DE TIMERS DE BOMBA
    // =========================================================

    public void UpdateBombTimers(float timeRemaining, int activeSide, bool isGracePeriod)
    {
        float displayTime = Mathf.Max(0f, timeRemaining);
        string formattedTime = displayTime.ToString("F2");

        // 1. SI ES CENTRO (LADO 0) - Muerte Súbita o Espera
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
        // 2. SI EL EQUIPO A (AZUL) TIENE LA BOMBA (-1)
        else if (activeSide == -1)
        {
            if (centerWarningText) centerWarningText.gameObject.SetActive(false);

            ActivateTimer(bombTimerAText, formattedTime, teamAColor, timeRemaining);
            SetTimerStandby(bombTimerBText);
        }
        // 3. SI EL EQUIPO B (ROJO) TIENE LA BOMBA (1)
        else if (activeSide == 1)
        {
            if (centerWarningText) centerWarningText.gameObject.SetActive(false);

            SetTimerStandby(bombTimerAText);
            ActivateTimer(bombTimerBText, formattedTime, teamBColor, timeRemaining);
        }
    }

    // Helpers Visuales
    void ActivateTimer(TextMeshProUGUI txt, string timeStr, Color col, float rawTime)
    {
        if (txt == null) return;
        txt.gameObject.SetActive(true);
        txt.text = timeStr;
        txt.color = col;

        if (rawTime < 3.0f)
            txt.transform.localPosition = (txt == bombTimerAText ? originalPosBombA : originalPosBombB) + (Vector3)(Random.insideUnitCircle * 2f);
        else
            txt.transform.localPosition = (txt == bombTimerAText ? originalPosBombA : originalPosBombB);
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

    public void UpdateScore(int scoreA, int scoreB)
    {
        scoreAText.text = scoreA.ToString();
        scoreBText.text = scoreB.ToString();
    }

    public void UpdateState(string message)
    {
        stateText.text = message;
        stateText.gameObject.SetActive(true);
        StopCoroutine("HideStateDelay");
        if (!message.Contains("VICTORIA") && !message.Contains("GAME OVER"))
            StartCoroutine("HideStateDelay");
    }

    IEnumerator HideStateDelay()
    {
        yield return new WaitForSeconds(3f);
        stateText.gameObject.SetActive(false);
    }

    // =========================================================
    //              SALUD Y HUD (LO QUE FALTABA)
    // =========================================================

    public void ToggleGameplayHUD(bool isVisible)
    {
        if (gameplayHUDContainer != null)
            gameplayHUDContainer.SetActive(isVisible);
    }

    public void UpdateHealth(float current, float max)
    {
        float percentage = current / max;

        // Texto numérico
        if (healthNumText != null)
        {
            healthNumText.text = Mathf.CeilToInt(current).ToString();
            healthNumText.color = healthGradient.Evaluate(percentage);
        }

        // Barra de vida
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = percentage;
            healthBarFill.color = healthGradient.Evaluate(percentage);
        }
    }

    public void ShowVictoryScreen(string winnerName, Color winnerColor)
    {
        // 1. Apagar el texto de estado general (el amarillo que dice "VICTORIA: ...")
        // para que no se mezcle con el panel nuevo.
        if (stateText != null) stateText.gameObject.SetActive(false);

        // 2. Mostrar el Panel de Victoria
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            if (victoryText != null)
            {
                victoryText.text = "¡" + winnerName + " GANA!"; // Agregué signos de exclamación
                victoryText.color = winnerColor;
            }
        }

        // 3. Ocultar HUD de juego
        ToggleGameplayHUD(false);
    }
}