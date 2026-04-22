
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


public enum EffectType 
{ 
    Neutral, 
    HealthPotion, 
    DamageBomb, 
    Poison, 
    Missile,
    MissileV,
    Armor, 
    Freeze,
    Laser,
    LaserV,
    Spray,
    Lightning,
    Random,
    RandomWeapon,
    Portal,
    PortalWeapon,
    MegaJump,
    Invisibility,
    DoubleDamage,
    SightDisabled,
    InstantDeath,
    CollisionDuel
}

[System.Serializable]
public struct CellEffect
{
    public EffectType type;
    public int value;       // Dégâts / Soin / Portée
    public float duration;  // Durée en secondes (pour Poison/Freeze)
    public bool isWeapon;   // True si c'est un Missile (touche les autres)
    public bool isConsumed;
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

public enum Direction { Up, Down, UpAndDown, Left, Right, LeftAndRight, All, None } 

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

public struct PlayerAction
{
    public int PlayerID;
    public int TargetRow;
    public int TargetCol;
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

public struct CollisionDuel
{
    public int DuelId;
    public int Row;
    public int Col;
    public List<int> PlayerIDs; // Les IDs des 2, 3 ou 4 joueurs sur cette case
}

public struct PlayerState
{
    public int ID;
    public int Health;
    public int MaxHealth;
    public int Row;
    public int Col;
    public int DestRow;
    public int DestCol;
    public bool IsAlive;
    public bool IsAI;
    public int PoisonTurnsRemaining;
    public int ArmorTurnsRemaining;
    public int FreezeTurnsRemaining;
    public int MegaJumpTurnsRemaining;
    public int InvisibilityRemaining;
    public int SightDisabledRemaining;
    public string Name;
}

public class GameState
{
    public int CurrentTurn;
    public CellEffect[,] Grid;
    public List<PlayerState> Players;
    public int Rows;
    public int Cols;
    public bool CurrentSightDisabled;
    public CellEffect[] FutureRow;
    public List<CollisionDuel> CurrentDuels = new List<CollisionDuel>();
    public Dictionary<int, Position> PlayerFinalPositions = new Dictionary<int, Position>();
    public int Seed;
}

