using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class BombCarrier : NetworkBehaviour
{
    [Header("Configuración")]
    public Transform holdPoint;
    public float grabRange = 2.5f;
    public LayerMask bombLayer;

    [Header("Ajustes")]
    public Vector3 holdPositionOffset = new Vector3(0f, -0.2f, 0.5f);
    public Vector3 holdRotationOffset = Vector3.zero;
    public float throwForce = 8f;

    [Header("Estado")]
    public NetworkVariable<bool> isCarrying = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkObject currentBombNetObj;
    private CharacterController myCollider;

    private void Start()
    {
        myCollider = GetComponent<CharacterController>();
    }

    public void OnGrabInput()
    {
        if (!IsOwner) return;

        if (!isCarrying.Value)
        {
            TryGrabBombLocal();
        }
        else
        {
            // El cliente pide soltar con fuerza
            RequestDropServerRpc(throwForce);
        }
    }

    private void TryGrabBombLocal()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward, grabRange, bombLayer);
        foreach (var hit in hits)
        {
            NetworkObject bombNetObj = hit.GetComponent<NetworkObject>();
            if (bombNetObj != null)
            {
                RequestGrabServerRpc(bombNetObj.NetworkObjectId);
                return;
            }
        }
    }

    // --- LÓGICA DE SERVIDOR (GRAB) ---

    [ServerRpc]
    private void RequestGrabServerRpc(ulong bombId)
    {
        if (isCarrying.Value) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(bombId, out NetworkObject bomb))
        {
            if (bomb.TrySetParent(this.NetworkObject))
            {
                currentBombNetObj = bomb;
                isCarrying.Value = true;

                Rigidbody rb = bomb.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.detectCollisions = false;
                }

                bomb.transform.localPosition = holdPositionOffset;
                bomb.transform.localRotation = Quaternion.Euler(holdRotationOffset);

                IgnoreCollisionsClientRpc(bombId);
            }
        }
    }

    // --- LÓGICA DE SERVIDOR (DROP) ---

    // 1. EL RPC: Solo recibe la orden del cliente
    [ServerRpc]
    private void RequestDropServerRpc(float force)
    {
        DropBombLogic(force);
    }

    // 2. LA LÓGICA REAL: Se puede llamar desde el RPC o desde el Servidor directamente
    private void DropBombLogic(float force)
    {
        if (!isCarrying.Value || currentBombNetObj == null) return;

        // Quitar padre
        currentBombNetObj.TrySetParent((Transform)null);

        // Reactivar físicas
        Rigidbody rb = currentBombNetObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;

            Vector3 throwDirection = (transform.forward + Vector3.up).normalized;
            rb.AddForce(throwDirection * force, ForceMode.Impulse);
        }

        // Limpiar estado
        ulong droppedBombId = currentBombNetObj.NetworkObjectId;
        currentBombNetObj = null;
        isCarrying.Value = false;

        // Avisar a clientes
        ReEnableCollisionsClientRpc(droppedBombId);
    }

    // --- MÉTODOS PÚBLICOS ---

    public void ForceDropBomb()
    {
        // CASO 1: Somos el Servidor (ej. PlayerHealth nos mató)
        if (IsServer)
        {
            // Ejecutamos la lógica DIRECTAMENTE, sin usar RPC
            DropBombLogic(0f);
        }
        // CASO 2: Somos el Cliente (raro para muerte, pero por si acaso)
        else if (IsOwner)
        {
            RequestDropServerRpc(0f);
        }
    }

    // --- LÓGICA DE CLIENTE ---

    [ClientRpc]
    private void IgnoreCollisionsClientRpc(ulong bombId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(bombId, out NetworkObject bomb))
        {
            if (IsOwner)
            {
                Collider bombCol = bomb.GetComponent<Collider>();
                if (myCollider != null && bombCol != null)
                    Physics.IgnoreCollision(myCollider, bombCol, true);
            }
        }
    }

    [ClientRpc]
    private void ReEnableCollisionsClientRpc(ulong bombId)
    {
        if (IsOwner)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(bombId, out NetworkObject bomb))
            {
                Collider bombCol = bomb.GetComponent<Collider>();
                if (myCollider != null && bombCol != null)
                {
                    StartCoroutine(WaitForSeparation(myCollider, bombCol));
                }
            }
        }
    }

    IEnumerator WaitForSeparation(Collider player, Collider bomb)
    {
        Physics.IgnoreCollision(player, bomb, true);
        float timer = 0f;
        while (player != null && bomb != null && player.bounds.Intersects(bomb.bounds) && timer < 2f)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        if (player != null && bomb != null)
            Physics.IgnoreCollision(player, bomb, false);
    }
}