using UnityEngine;
using Unity.Netcode;

public class PlayerInteraction : NetworkBehaviour
{
    [Header("Configuración Patada")]
    public float kickRange = 3.5f;
    public float kickForce = 10f;
    public float kickCooldown = 0.6f;
    public LayerMask interactLayers;

    [Header("Referencias")]
    public Transform cameraHolder;

    private float lastKickTime;

    // Solo ejecutamos input si somos el dueño
    public void OnKickInput()
    {
        if (!IsOwner) return;

        if (Time.time >= lastKickTime + kickCooldown)
        {
            PerformKickLocal(); // Calculamos a quién pegar
        }
    }

    void PerformKickLocal()
    {
        lastKickTime = Time.time;

        RaycastHit hit;
        if (Physics.SphereCast(cameraHolder.position, 0.5f, cameraHolder.forward, out hit, kickRange, interactLayers))
        {
            // Buscamos si el objeto tiene identidad de red
            NetworkObject targetNetObj = hit.collider.GetComponentInParent<NetworkObject>();

            if (targetNetObj != null)
            {
                // Calculamos dirección desde nuestra cámara
                Vector3 forceDir = (hit.point - cameraHolder.position).normalized;
                forceDir += Vector3.up * 0.3f;
                forceDir.Normalize();

                // PEDIMOS AL SERVIDOR QUE APLIQUE LA FUERZA
                RequestKickServerRpc(targetNetObj.NetworkObjectId, forceDir * kickForce);
            }
        }
    }

    [ServerRpc]
    private void RequestKickServerRpc(ulong targetId, Vector3 force)
    {
        // El servidor busca el objeto por su ID
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject targetObj))
        {
            Rigidbody targetRb = targetObj.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                // El servidor aplica la fuerza -> NetworkTransform sincroniza el resultado
                targetRb.AddForce(force, ForceMode.Impulse);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (cameraHolder != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(cameraHolder.position + cameraHolder.forward * kickRange, 0.5f);
        }
    }
}