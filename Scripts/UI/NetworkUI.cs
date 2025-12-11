using UnityEngine;
using Unity.Netcode;

public class NetworkUI : MonoBehaviour
{
    public void ClickHost()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void ClickClient()
    {
        NetworkManager.Singleton.StartClient();
    }

    private void Awake()
    {
        // ESTO ES VITAL PARA TESTEAR EN LA MISMA PC
        Application.runInBackground = true;
    }
}