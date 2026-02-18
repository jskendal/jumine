
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

using System.Collections.Generic;

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

public struct EffectHitInfo
{
    public int PlayerId;   // victime
    public int NewHealth;  // HP après l’impact
}

public enum ControlMode { Human, AI, Remote }

public struct Position
{
    public int Row;
    public int Col;

    public Position(int r, int c)
    {
        Row = r;
        Col = c;
    }
}

public struct GameEventData
{
    public EffectType Type;           // Ton enum direct
    public int Rank;                  // Ordre d'animation
    public int PlayerId;              // Qui subit l'effet
    public int LauncherId;            // Qui lance (pour armes/duels)
    public int Row;                   // Où ça se passe
    public int NewHealth;             // PV après effet
    public Direction WeaponDirection; // Ta enum direct
    public EffectHitInfo[]? Hits;     // Cibles touchées (peut être null)
    public List<int>? Participants;   // Joueurs dans un duel
}