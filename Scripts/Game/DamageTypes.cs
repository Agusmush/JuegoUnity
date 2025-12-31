using UnityEngine;
using Unity.Netcode;

[System.Serializable]
public struct DamageInfo : INetworkSerializable
{
    public float DamageAmount;
    public Vector3 PointOfImpact;
    public Vector3 ForceDirection;
    public float ForceMagnitude;
    public bool IsExplosion;
    public float ExplosionRadius;
    public ulong AttackerID;

    // Constructor compatible con tus scripts
    public DamageInfo(float damageAmount, Vector3 pointOfImpact, Vector3 forceDirection, float forceMagnitude, bool isExplosion, float explosionRadius, ulong attackerID)
    {
        DamageAmount = damageAmount;
        PointOfImpact = pointOfImpact;
        ForceDirection = forceDirection;
        ForceMagnitude = forceMagnitude;
        IsExplosion = isExplosion;
        ExplosionRadius = explosionRadius;
        AttackerID = attackerID;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref DamageAmount);
        serializer.SerializeValue(ref PointOfImpact);
        serializer.SerializeValue(ref ForceDirection);
        serializer.SerializeValue(ref ForceMagnitude);
        serializer.SerializeValue(ref IsExplosion);
        serializer.SerializeValue(ref ExplosionRadius);
        serializer.SerializeValue(ref AttackerID);
    }
}