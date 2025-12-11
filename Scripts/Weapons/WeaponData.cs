using UnityEngine;

public enum WeaponType { Hitscan, Projectile, Melee }

[CreateAssetMenu(fileName = "New Weapon", menuName = "Weapon System/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Básicos")]
    public string weaponName;
    public WeaponType type;
    public GameObject modelPrefab;

    [Header("Combate (Matar)")]
    public float damage;
    public float fireRate;
    public bool isAutomatic;
    public float enemyKnockback; // Empujar jugadores

    [Header("Física (La Bomba)")]
    public float bombPushForce;   // TODOS las armas tienen esto
    public float explosionRadius; // Si es > 0, empuja en área

    [Header("Tool / Self")]
    public float selfKnockback;   // Para Rocket Jumping (si disparas al suelo)
    public float recoil;          // Mueve la cámara
}