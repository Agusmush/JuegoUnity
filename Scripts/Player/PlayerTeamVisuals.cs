using UnityEngine;

public class PlayerTeamVisuals : MonoBehaviour
{
    // Hacemos esta variable privada para que no salga en el inspector.
    // El script la buscará solo.
    private PlayerHealth playerHealth;

    [Header("Configuración Visual")]
    public Renderer targetRenderer; // AQUÍ arrastras el cuerpo 3D del personaje

    [Header("Materiales de Equipo")]
    public Material materialTeamA; // Material Azul/Cian
    public Material materialTeamB; // Material Rojo/Naranja

    void Awake()
    {
        // AUTOMATIZACIÓN: Buscamos el script de salud en este mismo objeto
        playerHealth = GetComponent<PlayerHealth>();
    }

    public void UpdateVisuals()
    {
        // Seguridad: Si no encontró el script de salud o el renderer, no hace nada
        if (playerHealth == null || targetRenderer == null) return;

        if (playerHealth.teamID == 0) // Equipo A
        {
            targetRenderer.material = materialTeamA;
        }
        else if (playerHealth.teamID == 1) // Equipo B
        {
            targetRenderer.material = materialTeamB;
        }
    }
}