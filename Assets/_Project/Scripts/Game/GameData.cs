// GameData.cs
using UnityEngine;

// public enum EffectType
// {
//     Neutral,
//     HealthPotion,    // +Vie
//     DamageBomb,      // -Vie
//     Poison,          // -Vie sur la durée
//     Missile,         // Arme : Dégâts de zone
//     Freeze,          // Status : Ne bouge pas
//     SpeedBoost       // Status : Bouge plus vite
// }

[System.Serializable]
public struct CellEffect
{
    public EffectType type;
    public int value;       // Dégâts / Soin / Portée
    public float duration;  // Durée en secondes (pour Poison/Freeze)
    public bool isWeapon;   // True si c'est un Missile (touche les autres)
}

public struct EffectWeight 
{ 
    public EffectType type; 
    public float chance; // Entre 0.0 et 1.0
    public int value; 
    public bool isWeapon;
    public float duration;
}

public enum ControlMode { Human, AI, Remote }