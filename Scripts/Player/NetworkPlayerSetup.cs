using UnityEngine;
using Unity.Netcode; // Necesario para multiplayer

public class NetworkPlayerSetup : NetworkBehaviour
{
    [Header("Componentes a controlar")]
    [Tooltip("La cámara dentro del prefab de ESTE jugador")]
    public GameObject playerCamera;
    
    [Tooltip("El AudioListener de ESTE jugador (suele estar en la cámara)")]
    public AudioListener playerAudioListener;

    [Tooltip("Scripts que solo deben funcionar si soy el dueño (ej: PlayerLook, InputManager)")]
    public MonoBehaviour[] scriptsToIgnoreOnRemote;

    [Header("UI del Jugador")]
    public GameObject playerUI; // Arrastra aquí el Canvas que está DENTRO de tu jugador

    // Esta función se ejecuta automáticamente cuando el objeto aparece en la red
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // --- ESTE SOY YO (JUGADOR LOCAL) ---
            
            // 1. Apagar la cámara del Lobby (Buscándola por Tag o Nombre)
            GameObject lobbyCam = GameObject.Find("LobbyCamera"); 
            // Ojo: Asegúrate de que tu cámara del lobby se llame "LobbyCamera" o tenga el tag "MainCamera" y lo busques así.
            if (lobbyCam != null) lobbyCam.SetActive(false);

            // 2. Encender MI cámara y MI oído
            if (playerCamera != null) playerCamera.SetActive(true);
            if (playerAudioListener != null) playerAudioListener.enabled = true;

            // 3. Ocultar el UI del Lobby (Opcional por ahora, para limpiar pantalla)
            // GameObject lobbyUI = GameObject.Find("NetworkUI"); 
            // if (lobbyUI != null) lobbyUI.SetActive(false); 

            // ACTIVAR mi UI
            if (playerUI != null) playerUI.SetActive(true);
        }
        else
        {
            // --- ESTE ES EL OTRO (JUGADOR REMOTO) ---
            
            // 1. Apagar SU cámara (para no ver lo que él ve)
            if (playerCamera != null) playerCamera.SetActive(false);
            
            // 2. Apagar SU oído (para no escuchar desde su posición)
            if (playerAudioListener != null) playerAudioListener.enabled = false;

            if (playerUI != null) playerUI.SetActive(false);

            // 3. Apagar sus controles para que mi teclado no lo mueva a él también
            // (Aunque dijiste que esto ya no pasaba, es bueno asegurarse)
            foreach (var script in scriptsToIgnoreOnRemote)
            {
                script.enabled = false;
            }
        }
    }
}