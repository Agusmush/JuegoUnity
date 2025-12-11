using UnityEngine;
using Unity.Netcode;

public class PlayerLook : NetworkBehaviour
{
    public Camera cam;
    public float mouseSensitivity = 30f;
    private float xRotation = 0f;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // 1. Soy yo: Activo mi cámara
            if (cam != null)
            {
                cam.enabled = true;
                if (cam.GetComponent<AudioListener>()) cam.GetComponent<AudioListener>().enabled = true;
            }

            // 2. Apago la cámara del lobby (Buscándola por nombre)
            GameObject lobbyCam = GameObject.Find("LobbyCamera");
            if (lobbyCam != null) lobbyCam.SetActive(false);

            // 3. Bloqueo cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // 4. Ocultar Panel de Red si sigue activo
            GameObject netPanel = GameObject.Find("NetworkPanel");
            if (netPanel != null) netPanel.SetActive(false);
        }
        else
        {
            // 4. Es otro jugador: Apago su cámara y audio
            if (cam != null)
            {
                cam.enabled = false;
                if (cam.GetComponent<AudioListener>()) cam.GetComponent<AudioListener>().enabled = false;
            }
        }
    }

    // Esta función debe ser PÚBLICA para que InputManager la llame
    public void ProcessLook(Vector2 input)
    {
        if (!IsOwner) return;

        float mouseX = input.x * mouseSensitivity * Time.deltaTime;
        float mouseY = input.y * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (cam != null) cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }
}