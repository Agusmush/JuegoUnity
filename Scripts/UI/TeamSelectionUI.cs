using UnityEngine;

public class TeamSelectionUI : MonoBehaviour
{
    // Opcional: Referencia al panel para ocultarlo al elegir
    public GameObject teamSelectionPanel;

    public void OnClickTeamA()
    {
        Debug.Log("Click en Equipo A");
        if (GameManager.Instance != null)
        {
            // CAMBIO: Usamos SpawnLocalPlayer en lugar de RequestJoinGame
            // Asegúrate de que tus "SpawnVolumes" en la escena tengan ID 0 (o el que uses para el equipo A)
            GameManager.Instance.SpawnLocalPlayer(0);

            // Ocultamos el menú
            if (teamSelectionPanel != null) teamSelectionPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("¡No encuentro el GameManager!");
        }
    }

    public void OnClickTeamB()
    {
        Debug.Log("Click en Equipo B");
        if (GameManager.Instance != null)
        {
            // CAMBIO: Usamos SpawnLocalPlayer
            // Asegúrate de que tus "SpawnVolumes" del equipo B tengan ID 1
            GameManager.Instance.SpawnLocalPlayer(1);

            if (teamSelectionPanel != null) teamSelectionPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("¡No encuentro el GameManager!");
        }
    }
}