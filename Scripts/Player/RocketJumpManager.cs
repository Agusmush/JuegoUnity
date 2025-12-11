using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocketJumpManager : MonoBehaviour
{
    private PlayerMotor motor;
    private Camera mainCamera;

    public float rocketForce = 30f;
    public float explosionLifetime = 0.5f;
    public Color explosionColor = new Color(1, 0.2f, 0.2f, 0.8f);
    public float explosionSize = 0.5f;

    void Start()
    {
        motor = GetComponent<PlayerMotor>();
        mainCamera = Camera.main;
    }

    public void PerformRocketJump()
    {
        // Rocket jump en la dirección OPUESTA a donde miras
        // Si miras al piso (down), te dispara hacia arriba
        Vector3 rocketDirection = -mainCamera.transform.forward;
        motor.AddExplosionForce(rocketDirection, rocketForce);

        CreateRocketExplosion(mainCamera.transform.position + rocketDirection * 2f);

        Debug.Log($"🚀 Rocket Jump!  Dirección: {rocketDirection}");
    }

    void CreateRocketExplosion(Vector3 explosionPos)
    {
        GameObject explosionObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        explosionObj.name = "RocketExplosion";
        explosionObj.transform.position = explosionPos;
        explosionObj.transform.localScale = Vector3.one * explosionSize;

        Material explosionMaterial = new Material(Shader.Find("Standard"));
        explosionMaterial.color = explosionColor;
        explosionObj.GetComponent<Renderer>().material = explosionMaterial;

        Collider collider = explosionObj.GetComponent<Collider>();
        if (collider != null)
            DestroyImmediate(collider);

        Destroy(explosionObj, explosionLifetime);
    }
}