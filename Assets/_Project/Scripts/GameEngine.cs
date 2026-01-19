using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

// 🔴 PAS DE USING UNITYENGINE ICI ! C'est IMPORTANT.

#region ENUMS ET STRUCTURES DE DONNÉES
public enum EffectType 
{ 
    Neutral, 
    HealthPotion, 
    DamageBomb, 
    Poison, 
    Missile, 
    Armor, 
    Freeze 
}

/// <summary>
/// Action choisie par un joueur pour un tour : sa cible
/// </summary>
public struct PlayerAction
{
    public int PlayerID;
    public int TargetRow;
    public int TargetCol;
}

/// <summary>
/// Données d'une cellule (pas de visuel, juste la logique)
/// </summary>
// public struct CellData
// {
//     public EffectType Type;
//     public int Value;       // Dégâts / Soin / Durée
//     public bool IsWeapon;   // True pour le missile
// }

/// <summary>
/// État d'un joueur (pas de GameObject, juste les valeurs utiles)
/// </summary>
public struct PlayerState
{
    public int ID;
    public int Health;
    public int MaxHealth;
    public int Row;
    public int Col;
    public bool IsAlive;
    public int PoisonTurnsRemaining;
    public int ArmorTurnsRemaining;
    public int FreezeTurnsRemaining;
    public int isFrozen;
}

/// <summary>
/// État complet de la partie (tout ce qu'il faut pour reprendre la partie à tout moment)
/// </summary>
public class GameState
{
    public int CurrentTurn;
    public CellEffect[,] Grid;
    public List<PlayerState> Players;
    public int Rows;
    public int Cols;
    public bool HasDoneFirstTurn;
    public CellEffect[] FutureRow;
}

#endregion

#region MOTEUR DE JEU
public class GameEngine
{
    public delegate void OnEffectApplied(int playerId, EffectType effectType, int value, int executionRank);
    public event OnEffectApplied EffectApplied;

    private GameState _currentState;
    private readonly Random _rng = new Random(); // Générateur aléatoire PURE C#

    /// <summary>
    /// Initialise une nouvelle partie
    /// </summary>
    public GameEngine(int rows, int cols)
    {
        // Initialiser l'état de base
        _currentState = new GameState
        {
            Rows = rows,
            Cols = cols,
            CurrentTurn = 0,
            HasDoneFirstTurn = false,
            Grid = new CellEffect[rows, cols],
            Players = new List<PlayerState>()
        };

        // Générer la grille initiale
        GenerateInitialGrid();
        
        _currentState.FutureRow = new CellEffect[cols];
        for(int c=0; c<cols; c++) _currentState.FutureRow[c] = GenerateRandomCell(-1, c);
    }

    /// <summary>
    /// Exécute un tour complet : mouvements, insertion de ligne, résolution des effets
    /// </summary>
    public void ProcessTurn(List<PlayerAction> playerActions)
    {
        // 1. Appliquer les mouvements des joueurs
        ApplyPlayerMoves(playerActions);

        // 2. Faire descendre la grille (logique InsertRow)
        InsertNewRow();

        // 3. Résoudre les effets des cases où les joueurs ont atterri
        ResolveCellEffects();

        // 4. Résoudre les effets persistants (poison, armor)
        ResolvePersistentEffects();

        // 5. Incrémenter le tour
        _currentState.CurrentTurn++;
    }

    #region MÉTHODES DE LOGIQUE
    private void GenerateInitialGrid()
    {
        for (int r = 0; r < _currentState.Rows; r++)
        {
            for (int c = 0; c < _currentState.Cols; c++)
            {
                _currentState.Grid[r, c] = GenerateRandomCell(r, c);
            }
        }
    }

    private List<EffectWeight> possibleEffects = new List<EffectWeight>
    {
        new EffectWeight { type = EffectType.Missile,       chance = 0.05f, value = 30, isWeapon = true },
        new EffectWeight { type = EffectType.Poison,        chance = 0.05f, value = 10, isWeapon = false, duration = 4f },
        new EffectWeight { type = EffectType.Armor,         chance = 0.04f, value = 10, isWeapon = false, duration = 3f }, // Protection
        new EffectWeight { type = EffectType.DamageBomb,    chance = 0.06f, value = 30, isWeapon = false },
        new EffectWeight { type = EffectType.HealthPotion,  chance = 0.06f, value = 30, isWeapon = false },
        new EffectWeight { type = EffectType.Freeze,        chance = 0.05f, value = 10, isWeapon = false, duration = 2f },
        // 👇 LE NOUVEAU 👇
        //new EffectWeight { type = EffectType.Freeze,    chance = 0.05f, value = 0,  isWeapon = false } // 5% de chance
        // Total actuel : 0.05+0.10+0.10+0.10+0.10+0.05 = 0.50 (50%)
        // -> Il reste 50% de chances d'avoir une case Neutre. C'est parfait !
    };

    private CellEffect GenerateRandomCell(int row, int col)
    {
        if (row == 0 || (row == 1 ))//&& !_currentState.HasDoneFirstTurn
        {
            return new CellEffect { type = EffectType.Neutral, value = 0, isWeapon = false };
        }

        float chance = (float)_rng.NextDouble();
        float cumulative = 0f;

        foreach (var effect in possibleEffects)
        {
            cumulative += effect.chance;
            if (chance < cumulative)
            {
                return new CellEffect 
                { 
                    type = effect.type, 
                    value = effect.value, 
                    isWeapon = effect.isWeapon,
                    duration = effect.duration
                };
            }
        }

        // Si on a dépassé toutes les chances (ou si la liste est vide), c'est Neutre
        return new CellEffect { type = EffectType.Neutral, value = 0, isWeapon = false };
    }
 
    /// <summary>
    /// Logique de décalage de la grille (remplace GridManager.InsertRow)
    /// </summary>
    private void InsertNewRow()
    {
        int rows = _currentState.Rows;
        int cols = _currentState.Cols;

        // 1. Décaler toutes les lignes vers le bas (logique identique à GridManager.InsertRow)
        for (int r = 1; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                _currentState.Grid[r - 1, c] = _currentState.Grid[r, c];
            }
        }

        // 2. La future row ENTRE dans la grille comme nouvelle ligne du haut
        int topRow = rows - 1;
        for (int c = 0; c < cols; c++)
        {
            _currentState.Grid[topRow, c] = _currentState.FutureRow[c];
        }

        // 3. Générer une NOUVELLE future row pour le tour suivant
        for (int c = 0; c < cols; c++)
        {
            _currentState.FutureRow[c] = GenerateRandomCell(-1, c);
        }

        // 4. Optionnel : marquer que le premier tour est passé
        // if (!_currentState.HasDoneFirstTurn)
        //     _currentState.HasDoneFirstTurn = true;
    }

    /// <summary>
    /// Appliquer les mouvements des joueurs selon leurs actions
    /// </summary>
    private void ApplyPlayerMoves(List<PlayerAction> playerActions)
    {
        foreach (var action in playerActions)
        {
            var player = _currentState.Players.FirstOrDefault(p => p.ID == action.PlayerID);
            if (!player.IsAlive) continue;

            // Vérifier que la cible est valide (dans les limites de la grille)
            if (action.TargetRow >=0 && action.TargetRow < _currentState.Rows && action.TargetCol >=0 && action.TargetCol < _currentState.Cols)
            {
                // Mettre à jour la position du joueur
                player.Row = action.TargetRow;
                player.Col = action.TargetCol;
                _currentState.Players[_currentState.Players.FindIndex(p => p.ID == action.PlayerID)] = player;
            }
        }
    }

    /// <summary>
    /// Résoudre les effets des cases où les joueurs ont atterri
    /// </summary>
    private void ResolveCellEffects()
    {
        Console.WriteLine("=== 🎲 TOUR {_currentState.CurrentTurn} : RÉSOLUTION DES EFFETS ===");
            // 1. Créer une liste d'indices et la mélanger
        List<int> playerIndices = Enumerable.Range(0, _currentState.Players.Count).ToList();
        for (int i = 0; i < playerIndices.Count; i++) {
            int r = _rng.Next(i, playerIndices.Count);
            int tmp = playerIndices[i]; playerIndices[i] = playerIndices[r]; playerIndices[r] = tmp;
        }

        int executionRank = 0;
        foreach (int i in playerIndices)
        {
            var player = _currentState.Players[i];
            if (!player.IsAlive) continue;

            var cell = _currentState.Grid[player.Row, player.Col];
            if (cell.type == EffectType.Neutral) continue;

            switch (cell.type)
            {
                case EffectType.HealthPotion:
                    player.Health = Math.Min(player.Health + cell.value, player.MaxHealth);
                    EffectApplied?.Invoke(player.ID, EffectType.HealthPotion, cell.value, executionRank);
                    executionRank++;
                    break;
                case EffectType.DamageBomb:
                    if (player.ArmorTurnsRemaining == 0)
                    {
                        player.Health = Math.Max(player.Health - cell.value, 0);
                    }
                    EffectApplied?.Invoke(player.ID, EffectType.DamageBomb, cell.value, executionRank);
                    executionRank++;
                    break;
                case EffectType.Poison:
                    player.PoisonTurnsRemaining = (int)cell.duration;//=3
                    EffectApplied?.Invoke(player.ID, EffectType.Poison, cell.value, executionRank);
                    executionRank++;
                    break;
                case EffectType.Missile:
                    // Logique du missile : toucher tous les joueurs sur la même ligne
                    ResolveMissileEffect(player.Row);
                    EffectApplied?.Invoke(player.ID, EffectType.Missile, cell.value, executionRank);
                    executionRank++;
                    break;
                case EffectType.Armor:
                    player.ArmorTurnsRemaining = (int)cell.duration;//= 2;
                    executionRank++;
                    break;
                case EffectType.Freeze:
                    player.FreezeTurnsRemaining = (int)cell.duration;//= 1;
                    player.isFrozen = 1;
                    EffectApplied?.Invoke(player.ID, EffectType.Freeze, cell.value, executionRank);
                    executionRank++;
                    break;
            }

            // Mettre à jour le joueur dans l'état
            _currentState.Players[i] = player;
        }
    }

    /// <summary>
    /// Résoudre les effets persistants (poison, armor)
    /// </summary>
    private void ResolvePersistentEffects()
    {
        for (int i = 0; i < _currentState.Players.Count; i++)
        {
            var player = _currentState.Players[i];
            if (!player.IsAlive) continue;

            // Poison : dégâts par tour
            if (player.PoisonTurnsRemaining > 0)
            {
                player.Health = Math.Max(player.Health - 10, 0);
                player.PoisonTurnsRemaining = player.PoisonTurnsRemaining - 1;
            }

            // Armor : diminue de 1 tour
            if (player.ArmorTurnsRemaining > 0)
            {
                player.ArmorTurnsRemaining = player.ArmorTurnsRemaining - 1;
            }

            if (player.FreezeTurnsRemaining > 0)
            {
                player.FreezeTurnsRemaining = player.FreezeTurnsRemaining - 1;
            }
            // Vérifier si le joueur est mort
            if (player.Health <= 0)
            {
                player.IsAlive = false;
            }

            _currentState.Players[i] = player;
        }
    }

    /// <summary>
    /// Logique du missile : toucher tous les joueurs sur la même ligne
    /// </summary>
    private void ResolveMissileEffect(int targetRow)
    {
        for (int i = 0; i < _currentState.Players.Count; i++)
        {
            var player = _currentState.Players[i];
            if (!player.IsAlive || player.Row != targetRow) continue;

            // Si le joueur a de l'armure, elle bloque les dégâts
            if (player.ArmorTurnsRemaining > 0)
            {
                //player.ArmorTurnsRemaining = 0;// not sure if we want to remove armor on missile hit
            }
            else
            {
                player.Health = Math.Max(player.Health - 30, 0);
            }

            _currentState.Players[i] = player;
        }
    }
    #endregion

    #region ACCESSEURS
    public GameState GetCurrentState() => _currentState;

    /// <summary>
    /// Ajouter un joueur à la partie
    /// </summary>
    public void AddPlayer(PlayerState player)
    {
        _currentState.Players.Add(player);
    }

    internal void ClearFreezeEffect(int iD)
    {
        int playerIndex = _currentState.Players.FindIndex(p => p.ID == iD);
        if (playerIndex >= 0)
        {
            var player = _currentState.Players[playerIndex];
            player.isFrozen = 0;
            _currentState.Players[playerIndex] = player;
        }
    }


    #endregion
}
#endregion