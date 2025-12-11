using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SpawnVolume : MonoBehaviour
{
    public int teamID; // -1 = A, 1 = B
    public Color gizmoColor = Color.green;

    private BoxCollider box;

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true; // Importante para que no choque con jugadores
    }

    public Vector3 GetRandomSpawnPoint()
    {
        // Obtener los límites de la caja en coordenadas mundiales
        Bounds bounds = box.bounds;

        // Intentamos encontrar un punto libre (máximo 10 intentos)
        for (int i = 0; i < 10; i++)
        {
            float randX = Random.Range(bounds.min.x, bounds.max.x);
            float randZ = Random.Range(bounds.min.z, bounds.max.z);

            // Mantenemos la altura Y de la base de la caja para que no nazcan volando
            // O usamos bounds.min.y + un poquito
            float spawnY = transform.position.y;

            Vector3 candidatePos = new Vector3(randX, spawnY, randZ);

            // CHEQUEO DE SEGURIDAD: ¿Hay alguien ahí?
            // Usamos un radio de 1 metro (lo que mide el jugador de ancho aprox)
            if (!Physics.CheckSphere(candidatePos, 1f, LayerMask.GetMask("Player")))
            {
                return candidatePos;
            }
        }

        // Si fallamos 10 veces (está llenísimo), devolvemos el centro por defecto
        return transform.position;
    }

    // Dibujo en el editor para que veas la caja
    void OnDrawGizmos()
    {
        if (box == null) box = GetComponent<BoxCollider>();
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}