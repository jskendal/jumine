using System;
using System.Collections.Generic;
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
public struct CellData
{
    public EffectType Type;
    public int Value;       // Dégâts / Soin / Durée
    public bool IsWeapon;   // True pour le missile
}

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
}

/// <summary>
/// État complet de la partie (tout ce qu'il faut pour reprendre la partie à tout moment)
/// </summary>
public class GameState
{
    public int CurrentTurn;
    public CellData[,] Grid;
    public List<PlayerState> Players;
    public int Rows;
    public int Cols;
    public bool HasDoneFirstTurn;
    public CellData[] FutureRow;
}
#endregion

#region MOTEUR DE JEU
public class GameEngine
{
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
            Grid = new CellData[rows, cols],
            Players = new List<PlayerState>()
        };

        // Générer la grille initiale
        GenerateInitialGrid();
        
        _currentState.FutureRow = new CellData[cols];
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

    /// <summary>
    /// Génère une cellule aléatoire, avec règles de sécurité pour la ligne de spawn
    /// </summary>
    private CellData GenerateRandomCell(int row, int col)
    {
        // 🔒 Ligne de spawn et première ligne au-dessus : TOUJOURS neutre pour le premier tour
        if (row == 0 || (row == 1 && !_currentState.HasDoneFirstTurn))
        {
            return new CellData { Type = EffectType.Neutral, Value = 0, IsWeapon = false };
        }

        float chance = (float)_rng.NextDouble();

        if (chance < 0.05f) // 5% Missile
        {
            return new CellData { Type = EffectType.Missile, Value = 30, IsWeapon = true };
        }
        else if (chance < 0.15f) // 10% Poison
        {
            return new CellData { Type = EffectType.Poison, Value = 10, IsWeapon = false };
        }
        else if (chance < 0.25f) // 10% Bombe
        {
            return new CellData { Type = EffectType.DamageBomb, Value = 30, IsWeapon = false };
        }
        else if (chance < 0.35f) // 10% Potion
        {
            return new CellData { Type = EffectType.HealthPotion, Value = 30, IsWeapon = false };
        }
        else // 65% Neutre
        {
            return new CellData { Type = EffectType.Neutral, Value = 0, IsWeapon = false };
        }
    }

    /// <summary>
    /// Logique de décalage de la grille (remplace GridManager.InsertRow)
    /// </summary>
    private void InsertNewRow()
    {
        // 1. Détruire la ligne du bas (logiquement : on oublie ses données)
        // 2. Décaler toutes les lignes vers le bas
        for (int r = 1; r < _currentState.Rows; r++)
        {
            for (int c = 0; c < _currentState.Cols; c++)
            {
                _currentState.Grid[r - 1, c] = _currentState.Grid[r, c];
            }
        }

        // 3. Générer la nouvelle ligne du haut
        int topRow = _currentState.Rows - 1;
        for (int c = 0; c < _currentState.Cols; c++)
        {
            _currentState.Grid[topRow, c] = GenerateRandomCell(topRow, c);
        }

        // 4. Marquer que le premier tour est passé
        if (!_currentState.HasDoneFirstTurn)
        {
            _currentState.HasDoneFirstTurn = true;
        }
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
        for (int i = 0; i < _currentState.Players.Count; i++)
        {
            var player = _currentState.Players[i];
            if (!player.IsAlive) continue;

            var cell = _currentState.Grid[player.Row, player.Col];

            switch (cell.Type)
            {
                case EffectType.HealthPotion:
                    player.Health = Math.Min(player.Health + cell.Value, player.MaxHealth);
                    break;
                case EffectType.DamageBomb:
                    player.Health = Math.Max(player.Health - cell.Value, 0);
                    break;
                case EffectType.Poison:
                    player.PoisonTurnsRemaining = 3;
                    break;
                case EffectType.Missile:
                    // Logique du missile : toucher tous les joueurs sur la même ligne
                    ResolveMissileEffect(player.Row);
                    break;
                case EffectType.Armor:
                    player.ArmorTurnsRemaining = 2;
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
                player.ArmorTurnsRemaining = 0;
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

    
    #endregion
}
#endregion