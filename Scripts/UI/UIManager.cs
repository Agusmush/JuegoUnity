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

    [Header("Timers de Bomba (5s)")]
    public TextMeshProUGUI bombTimerAText;
    public TextMeshProUGUI bombTimerBText;

    [Header("Colores")]
    public Color safeColor = new Color(1, 1, 1, 0.2f);
    public Color dangerColor = Color.red;
    public Color snapshotColor = Color.cyan;
    public Color normalTimeColor = Color.white;
    public Color criticalTimeColor = Color.red;

    [Header("Jugador")]
    public Image healthBarFill;
    public TextMeshProUGUI healthNumText;
    public GameObject gameplayHUDContainer; // Arrastra el objeto "GameplayHUD" aquí

    [Header("Colores de Salud")]
    public Gradient healthGradient;
    // Variables internas para recordar posiciones originales (Fix del texto flotante)
    private Vector3 originalTimerScale;
    private Vector3 originalPosBombA;
    private Vector3 originalPosBombB;

    private Coroutine resetCoroutineA;
    private Coroutine resetCoroutineB;



    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (timerText != null) originalTimerScale = timerText.transform.localScale;

        // GUARDAMOS LAS POSICIONES ORIGINALES PARA QUE NO SE VAYAN CAMINANDO
        if (bombTimerAText != null) originalPosBombA = bombTimerAText.transform.localPosition;
        if (bombTimerBText != null) originalPosBombB = bombTimerBText.transform.localPosition;

        if (stateText != null) stateText.gameObject.SetActive(false);
        ToggleGameplayHUD(false);

        // Ocultar timers de bomba al inicio del juego
        HideBombTimers();
    }

    // --- MÉTODOS DE VISIBILIDAD BOMBA ---

    public void HideBombTimers()
    {
        if (bombTimerAText) bombTimerAText.gameObject.SetActive(false);
        if (bombTimerBText) bombTimerBText.gameObject.SetActive(false);
    }

    public void ShowBombTimers()
    {
        // Los mostramos en estado "Neutro/Listo"
        if (bombTimerAText)
        {
            bombTimerAText.text = "5.00";
            bombTimerAText.color = safeColor;
            bombTimerAText.transform.localPosition = originalPosBombA; // Reset posición
            bombTimerAText.gameObject.SetActive(true);
        }
        if (bombTimerBText)
        {
            bombTimerBText.text = "5.00";
            bombTimerBText.color = safeColor;
            bombTimerBText.transform.localPosition = originalPosBombB; // Reset posición
            bombTimerBText.gameObject.SetActive(true);
        }
    }

    // --- TIMERS PRINCIPALES (Dos modos distintos) ---

    // Modo 1: Preparación (Blanco, Enteros, Sin Pánico)
    public void UpdatePrepTimer(float currentTime)
    {
        timerText.text = Mathf.CeilToInt(currentTime).ToString("0");
        timerText.color = normalTimeColor;
        timerText.transform.localScale = originalTimerScale;
    }

    // Modo 2: Gameplay (Rojo, Decimales, Pánico)
    public void UpdateGameTimer(float currentTime)
    {
        if (currentTime > 9f)
        {
            // TAMAÑO NORMAL
            timerText.text = Mathf.CeilToInt(currentTime).ToString("0");
            timerText.color = normalTimeColor;
            timerText.transform.localScale = originalTimerScale;
        }
        else
        {
            // TAMAÑO REDUCIDO + LATIDO
            timerText.text = Mathf.Max(0, currentTime).ToString("F2");
            timerText.color = criticalTimeColor;

            // Calculamos el latido (PingPong)
            float heartbeat = 1f + Mathf.PingPong(Time.time * 5f, 0.2f);

            // FACTOR DE REDUCCIÓN: 0.75f (75% del tamaño original)
            // Ajusta este número si quieres que sea más chico (0.6f) o más grande (0.8f)
            float shrinkFactor = 0.75f;

            // Aplicamos: Escala Original * Reducción * Latido
            timerText.transform.localScale = originalTimerScale * shrinkFactor * heartbeat;
        }
    }

    // --- TIMERS DE LA BOMBA (LÓGICA BLINDADA) ---

    public void UpdateBombTimers(float timeRemaining, int activeSide)
    {
        // FIX 1: Clampear a 0. Nunca permitimos que baje de 0.00 visualmente
        float displayTime = Mathf.Max(0f, timeRemaining);
        string formattedTime = displayTime.ToString("F2");

        // SI EL EQUIPO A ESTÁ EN PELIGRO (-1)
        if (activeSide == -1)
        {
            if (resetCoroutineA != null) StopCoroutine(resetCoroutineA);

            // FIX 2: ¡Resurrección! Forzamos que sea visible por si el parpadeo lo dejó apagado
            bombTimerAText.enabled = true;

            bombTimerAText.text = formattedTime;
            bombTimerAText.color = dangerColor;
            bombTimerAText.gameObject.SetActive(true);

            // Fix del temblor
            if (timeRemaining < 2.0f)
                bombTimerAText.transform.localPosition = originalPosBombA + (Vector3)(Random.insideUnitCircle * 2f);
            else
                bombTimerAText.transform.localPosition = originalPosBombA;
        }
        // SI EL EQUIPO B ESTÁ EN PELIGRO (1)
        else if (activeSide == 1)
        {
            if (resetCoroutineB != null) StopCoroutine(resetCoroutineB);

            // FIX 2: ¡Resurrección!
            bombTimerBText.enabled = true;

            bombTimerBText.text = formattedTime;
            bombTimerBText.color = dangerColor;
            bombTimerBText.gameObject.SetActive(true);

            // Fix del temblor
            if (timeRemaining < 2.0f)
                bombTimerBText.transform.localPosition = originalPosBombB + (Vector3)(Random.insideUnitCircle * 2f);
            else
                bombTimerBText.transform.localPosition = originalPosBombB;
        }
        else
        {
            // Zona Neutral: Asegurar posiciones
            bombTimerAText.transform.localPosition = originalPosBombA;
            bombTimerBText.transform.localPosition = originalPosBombB;

            // Asegurar visibilidad si queremos que se vean grises
            bombTimerAText.enabled = true;
            bombTimerBText.enabled = true;
        }
    }

    public void OnBombSideChanged(int newActiveSide)
    {
        if (newActiveSide == 1) // Pasó a B -> A se salvó
        {
            if (resetCoroutineA != null) StopCoroutine(resetCoroutineA);
            resetCoroutineA = StartCoroutine(SnapshotSequence(bombTimerAText, originalPosBombA));
        }
        else if (newActiveSide == -1) // Pasó a A -> B se salvó
        {
            if (resetCoroutineB != null) StopCoroutine(resetCoroutineB);
            resetCoroutineB = StartCoroutine(SnapshotSequence(bombTimerBText, originalPosBombB));
        }
    }

    IEnumerator SnapshotSequence(TextMeshProUGUI timerText, Vector3 originalPos)
    {
        timerText.transform.localPosition = originalPos;
        timerText.color = snapshotColor;

        // Parpadeo rápido
        for (int i = 0; i < 3; i++)
        {
            timerText.enabled = false;
            yield return new WaitForSeconds(0.1f);

            // Verificación de seguridad: Si la corrutina fue interrumpida externamente, morimos aquí.
            // Pero si sigue viva, encendemos.
            timerText.enabled = true;
            yield return new WaitForSeconds(0.1f);
        }

        yield return new WaitForSeconds(1.0f);

        // Estado final seguro
        timerText.enabled = true; // Seguridad extra
        timerText.text = "5.00";
        timerText.color = safeColor;
    }

    // ... Resto de métodos (Score, Health, State) IGUAL QUE ANTES ...
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

    public void ToggleGameplayHUD(bool isVisible)
    {
        if (gameplayHUDContainer != null)
            gameplayHUDContainer.SetActive(isVisible);
    }

    public void UpdateHealth(float current, float max)
    {
        // 1. Actualizar texto y color del texto
        float percentage = current / max;

        if (healthNumText != null)
        {
            healthNumText.text = Mathf.CeilToInt(current).ToString();
            // El texto también se pone rojo si quieres
            healthNumText.color = healthGradient.Evaluate(percentage);
        }

        // 2. Actualizar Barra y su Color
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = percentage;
            // MAGIA: El gradiente elige el color exacto basado en el %
            healthBarFill.color = healthGradient.Evaluate(percentage);
        }
    }
}