using UnityEngine;
using Unity.Netcode; // <--- Importante

public class NetworkUIManager : MonoBehaviour
{
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Host (Servidor + Jugador)"))
                NetworkManager.Singleton.StartHost();

            if (GUILayout.Button("Client (Unirse)"))
                NetworkManager.Singleton.StartClient();

            if (GUILayout.Button("Server (Solo Servidor)"))
                NetworkManager.Singleton.StartServer();
        }
        else
        {
            GUILayout.Label("Estado: " + (NetworkManager.Singleton.IsHost ? "Host" : NetworkManager.Singleton.IsServer ? "Server" : "Client"));
        }

        GUILayout.EndArea();
    }
}