using UnityEngine;
using Unity.Netcode;

public class BombCarrier : NetworkBehaviour
{
    [Header("Configuración")]
    public Transform holdPoint;   // ARRASTRA TU OBJETO "HoldPoint" (Mano) AQUÍ
    public float grabRange = 2.5f;
    public LayerMask bombLayer;
    public float throwForce = 10f;

    [Header("Estado")]
    public bool isCarrying = false;
    private BombController currentBomb;
    private PlayerMotor motor;

    private void Start()
    {
        motor = GetComponent<PlayerMotor>();
    }

    private void Update()
    {
        // SISTEMA DE "TELEQUINESIS RÍGIDA" (Evita el error InvalidParent)
        if (isCarrying && currentBomb != null && IsOwner)
        {
            // Forzamos la posición de la bomba a la mano en cada frame
            // Al ser dueños, el NetworkTransform replicará esto a los demás
            currentBomb.transform.position = holdPoint.position;
            currentBomb.transform.rotation = holdPoint.rotation;
        }
    }

    // Llamado por InputManager (Tecla E)
    public void OnGrabInput()
    {
        if (!IsOwner) return;

        if (!isCarrying)
        {
            TryGrabBomb();
        }
        else
        {
            DropBomb(throwForce); // Lanzar
        }
    }

    private void TryGrabBomb()
    {
        // Buscamos bombas cercanas con una esfera invisible
        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward, grabRange, bombLayer);

        foreach (var hit in hits)
        {
            BombController bomb = hit.GetComponent<BombController>();
            if (bomb != null)
            {
                // Pedimos al servidor permiso para agarrar ESTA bomba
                RequestGrabServerRpc(bomb.GetComponent<NetworkObject>().NetworkObjectId);
                return; // Solo agarramos una
            }
        }
    }

    // --- LÓGICA DEL SERVIDOR ---

    [ServerRpc]
    private void RequestGrabServerRpc(ulong bombNetworkId)
    {
        // Buscamos la bomba por su ID de red
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(bombNetworkId, out NetworkObject bombNet))
        {
            // 1. CAMBIAR DUEÑO (Vital: Ahora el cliente manda sobre la posición)
            bombNet.ChangeOwnership(OwnerClientId);

            // 2. AVISAR A TODOS (ClientRpc)
            GrabClientRpc(bombNetworkId);
        }
    }

    [ClientRpc]
    private void GrabClientRpc(ulong bombNetworkId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(bombNetworkId, out NetworkObject bombNet))
        {
            BombController bomb = bombNet.GetComponent<BombController>();

            // Configuración Local
            currentBomb = bomb;
            isCarrying = true;

            // Apagar físicas para que no se caiga de la mano
            Rigidbody rb = bomb.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false; // Evitar que la bomba choque con el jugador
            }

            // Aplicar peso al jugador (usa el script PlayerMotor)
            if (motor != null) motor.speedFactor = 0.8f; // 20% más lento
        }
    }

    // --- SOLTAR / LANZAR ---

    public void DropBomb(float force = 0f)
    {
        if (!isCarrying || currentBomb == null) return;

        // Pedimos al servidor soltarla
        RequestDropServerRpc(force);
    }

    [ServerRpc]
    private void RequestDropServerRpc(float force)
    {
        // 1. Quitamos la propiedad (Opcional: Devolver al Server o dejarla en el cliente)
        // Por seguridad, devolvemos propiedad al Server para que nadie haga trampas con el lag
        if (currentBomb != null)
            currentBomb.GetComponent<NetworkObject>().RemoveOwnership();

        // 2. Avisar a todos
        DropClientRpc(force);
    }

    [ClientRpc]
    private void DropClientRpc(float force)
    {
        if (currentBomb != null)
        {
            Rigidbody rb = currentBomb.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;

                // Aplicar fuerza de lanzamiento (si somos el que lanza o el server)
                if (force > 0)
                {
                    // Lanzamos hacia donde mira la cámara (o el holdpoint)
                    rb.AddForce(holdPoint.forward * force, ForceMode.Impulse);
                }
            }
            currentBomb = null;
        }

        isCarrying = false;

        // Restaurar velocidad
        if (motor != null) motor.speedFactor = 1f;
    }

    // Dibujar el rango en el editor para que veas cuánto alcanzas
    private void OnDrawGizmosSelected()
    {
        if (holdPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + transform.forward, grabRange);
        }
    }
}