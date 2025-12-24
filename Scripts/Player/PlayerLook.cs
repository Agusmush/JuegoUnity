using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    [Header("Referencias")]
    public Camera cam; // <--- ERROR 1 SOLUCIONADO (PlayerHealth busca esto)

    [Header("Configuración")]
    public float xSensitivity = 30f;
    public float ySensitivity = 30f;

    private float xRotation = 0f;

    // Guardamos la posición original local de la cámara (normalmente 0, 0.8, 0)
    private Vector3 originalLocalPos;

    void Start()
    {
        // Configuración inicial del cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Guardamos la posición donde debe estar la cámara quieta
        if (cam != null)
        {
            originalLocalPos = cam.transform.localPosition;
        }
        else
        {
            Debug.LogError("PlayerLook: ¡Falta asignar la cámara en el inspector!");
        }
    }



    public void ProcessLook(Vector2 input)
    {
        if (cam == null) return;

        float mouseX = input.x * Time.deltaTime * xSensitivity;
        float mouseY = input.y * Time.deltaTime * ySensitivity;

        // 1. Lógica Normal
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -85f, 85f);

        // Rotamos el cuerpo (Yaw)
        transform.Rotate(Vector3.up * mouseX);

        // 2. APLICAR SHAKE (Rotacional)
        // Sumamos el shake a la rotación que calculamos con el mouse
        if (CameraShake.Instance != null)
        {
            Vector3 shake = CameraShake.Instance.CurrentShakeRotation;

            // X = Tu mirada + Vibración vertical
            // Y = Vibración horizontal
            // Z = Vibración de inclinación (Roll)
            cam.transform.localRotation = Quaternion.Euler(xRotation + shake.x, shake.y, shake.z);

            // Aseguramos que la posición esté fija en el cuello (soluciona tu bug)
            cam.transform.localPosition = originalLocalPos;
        }
        else
        {
            cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            cam.transform.localPosition = originalLocalPos;
        }
    }
}