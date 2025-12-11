using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    private float shakeDuration = 0f;
    private float shakeMagnitude = 0f;
    private float dampingSpeed = 1.0f;

    // Esta es la variable que leerán los otros scripts
    public Vector3 CurrentShakeOffset { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (shakeDuration > 0)
        {
            // Generamos el desplazamiento aleatorio
            CurrentShakeOffset = Random.insideUnitSphere * shakeMagnitude;

            // Reducir tiempo
            shakeDuration -= Time.deltaTime * dampingSpeed;
        }
        else
        {
            // Si no hay shake, el offset es cero
            CurrentShakeOffset = Vector3.zero;
        }
    }

    public void TriggerShake(float duration, float magnitude)
    {
        shakeDuration = duration;
        shakeMagnitude = magnitude;
        dampingSpeed = 1.0f;
    }
}