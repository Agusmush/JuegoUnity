using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    public Camera cam;

    // IMPORTANTE: Al quitar Time.deltaTime, este valor debe ser mucho más bajo.
    // Prueba empezando con 0.1 o 0.5 en el Inspector.
    public float mouseSensitivity = 0.01f;

    private float xRotation = 0f;

    void Start()
    {
        if (cam != null)
        {
            cam.enabled = true;
            if (cam.GetComponent<AudioListener>()) cam.GetComponent<AudioListener>().enabled = true;
        }

        GameObject lobbyCam = GameObject.Find("LobbyCamera");
        if (lobbyCam != null) lobbyCam.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ProcessLook(Vector2 input)
    {
        // CORRECCIÓN: Quitamos Time.deltaTime. 
        // El input del mouse ya es un delta de posición, no una velocidad.
        float mouseX = input.x * mouseSensitivity;
        float mouseY = input.y * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (cam != null) cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }
}