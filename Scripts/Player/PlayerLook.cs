using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    public Camera cam;
    public float mouseSensitivity = 30f;
    private float xRotation = 0f;

    void Start()
    {
        // 1. Activar cámara
        if (cam != null)
        {
            cam.enabled = true;
            if (cam.GetComponent<AudioListener>()) cam.GetComponent<AudioListener>().enabled = true;
        }

        // 2. Apagar cámara de lobby si existe
        GameObject lobbyCam = GameObject.Find("LobbyCamera");
        if (lobbyCam != null) lobbyCam.SetActive(false);

        // 3. Bloquear cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // InputManager llama a esto
    public void ProcessLook(Vector2 input)
    {
        float mouseX = input.x * mouseSensitivity * Time.deltaTime;
        float mouseY = input.y * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (cam != null) cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }
}