using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

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
    Freeze,
    Laser,
    CollisionDuel
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
    public bool startPoison;
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
    public List<CollisionDuel> CurrentDuels = new List<CollisionDuel>();
    public Dictionary<int, Vector2Int> PlayerFinalPositions = new Dictionary<int, Vector2Int>();
}

public struct CollisionDuel
{
    public int Row;
    public int Col;
    public List<int> PlayerIDs; // Les IDs des 2, 3 ou 4 joueurs sur cette case
}

public struct DuelResult
{
    public int WinnerId;
    public int LoserId;
    public Vector2Int LoserNewPos;
}

#endregion

#region MOTEUR DE JEU
public class GameEngine
{
    public delegate void OnSingleEffectApplied(int playerId, EffectType type, int row, int rank, int newHealth);
    public delegate void OnMultiEffectApplied(int launcherId, EffectType type, int row, int rank, EffectHitInfo[] hits);
    public delegate void OnSingleEffectRemoved(int playerId, EffectType type, int rank);
    public delegate void OnCollisionDetected(int launcherId, EffectType type, int nbPlayers, int rank, List<int> participants);

    public event OnSingleEffectApplied SingleEffectApplied;
    public event OnMultiEffectApplied MultiEffectApplied;
    public event OnSingleEffectRemoved SingleEffectRemoved;
    public event OnCollisionDetected CollisionDetected;

    private GameState _currentState;
    private readonly System.Random _rng = new System.Random(); // Générateur aléatoire PURE C#

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

        // 1.1 
        DetectCollisions();

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
        new EffectWeight { type = EffectType.Laser,       chance = 0.05f, value = 30, isWeapon = true },
        new EffectWeight { type = EffectType.Poison,        chance = 0.05f, value = 10, isWeapon = false, duration = 4f },
        new EffectWeight { type = EffectType.Armor,         chance = 0.04f, value = 10, isWeapon = false, duration = 3f },
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

    private void DetectCollisions()
    {
        _currentState.CurrentDuels.Clear();
        _currentState.PlayerFinalPositions.Clear();

        var groups = _currentState.Players
            .Where(p => p.IsAlive)
            .GroupBy(p => new { p.Row, p.Col })
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            // 1. Liste de tous les joueurs sur la case
            List<int> allPlayersOnCell = group.Select(p => p.ID).ToList();
            
            // 2. Sélectionner les 2 duellistes au hasard
            List<int> duelists = new List<int>();
            int idx1 = _rng.Next(0, allPlayersOnCell.Count);
            duelists.Add(allPlayersOnCell[idx1]);
            allPlayersOnCell.RemoveAt(idx1);
            
            int idx2 = _rng.Next(0, allPlayersOnCell.Count);
            duelists.Add(allPlayersOnCell[idx2]);
            allPlayersOnCell.RemoveAt(idx2);

            // 3. Enregistrer le vrai duel (avec seulement les 2 joueurs)
            var duel = new CollisionDuel
            {
                Row = group.Key.Row,
                Col = group.Key.Col,
                PlayerIDs = duelists // Seulement 2 IDs
            };
            _currentState.CurrentDuels.Add(duel);

            // 4. EXPULSER LES INTRUS (les joueurs 3 et 4 s'il y en a)
            // Ils ne participeront pas au duel.
            foreach (int intruderId in allPlayersOnCell)
            {
                // Trouver une case libre autour de la collision
                Vector2Int newPos = FindBumpingPosition(duel.Row, duel.Col);
                
                // Mettre à jour la logique du moteur TOUT DE SUITE
                int pIdx = _currentState.Players.FindIndex(p => p.ID == intruderId);
                var updatedIntruder = _currentState.Players[pIdx];
                updatedIntruder.Row = newPos.x;
                updatedIntruder.Col = newPos.y;
                _currentState.Players[pIdx] = updatedIntruder;
                
                // L'intrus atterrira directement sur sa nouvelle case d'expulsion après le saut
                //_currentState.PlayerFinalPositions[intruderId] = newPos;
            }

            // 5. CALCUL DES OFFSETS VISUELS POUR LES 2 DUELLISTES UNIQUEMENT
            for (int i = 0; i < duelists.Count; i++)
            {
                int pId = duelists[i];
                int originalCol = _currentState.Players.First(p => p.ID == pId).Col;
                
                int offsetCol = originalCol;
                if (i == 0) offsetCol -= 1;
                else if (i == 1) offsetCol += 1;

                if (offsetCol >= 0 && offsetCol < _currentState.Cols) {
                    _currentState.PlayerFinalPositions[pId] = new Vector2Int(duel.Row, offsetCol);
                } else {
                    _currentState.PlayerFinalPositions[pId] = new Vector2Int(duel.Row, originalCol);
                }
            }
        }
    }

    /// <summary>
    /// Résoudre les effets des cases où les joueurs ont atterri
    /// </summary>
    public void ResolveCellEffects(List<int> playerIdsToResolve = null)
    {
        Console.WriteLine("=== 🎲 TOUR {_currentState.CurrentTurn} : RÉSOLUTION DES EFFETS ===");
            // 1. Créer une liste d'indices et la mélanger
        List<int> playerIndices = Enumerable.Range(0, _currentState.Players.Count).ToList();
        for (int i = 0; i < playerIndices.Count; i++) {
            int r = _rng.Next(i, playerIndices.Count);
            int tmp = playerIndices[i]; playerIndices[i] = playerIndices[r]; playerIndices[r] = tmp;
        }
        if(playerIdsToResolve != null) {
            // Si une liste de joueurs spécifiques est fournie, filtrer les indices pour ne garder que ceux des joueurs concernés
            playerIndices = playerIndices.Where(i => playerIdsToResolve.Contains(_currentState.Players[i].ID)).ToList();
        }
        int executionRank = 1;
        HashSet<int> playersInDuels = new HashSet<int>();
        foreach (int i in playerIndices)
        {
            var player = _currentState.Players[i];
            if (!player.IsAlive || playersInDuels.Contains(player.ID)) continue;

            var duel = _currentState.CurrentDuels.FirstOrDefault(d => d.PlayerIDs.Contains(player.ID));

            if (duel.PlayerIDs != null  && duel.PlayerIDs.Any() && playerIdsToResolve == null) // Si le joueur est dans un duel ET qu'on n'est pas en train de résoudre une liste spécifique de joueurs (pour éviter les duels en cascade)
            {
                foreach(var pId in duel.PlayerIDs) playersInDuels.Add(pId);
                CollisionDetected?.Invoke(player.ID, EffectType.CollisionDuel, duel.PlayerIDs.Count, executionRank, duel.PlayerIDs);
                executionRank++;
            }
            else 
            {
                var cell = _currentState.Grid[player.Row, player.Col];
                if (cell.type == EffectType.Neutral) continue;

                switch (cell.type)
                {
                    case EffectType.HealthPotion:
                        player.Health = Math.Min(player.Health + cell.value, player.MaxHealth);
                        SingleEffectApplied?.Invoke(player.ID, EffectType.HealthPotion, player.Row, executionRank, player.Health);
                        executionRank++;
                        break;
                    case EffectType.DamageBomb:
                        if (player.ArmorTurnsRemaining == 0)
                        {
                            player.Health = Math.Max(player.Health - cell.value, 0);
                        }
                        SingleEffectApplied?.Invoke(player.ID, EffectType.DamageBomb, player.Row, executionRank, player.Health);
                        executionRank++;
                        break;
                    case EffectType.Poison:

                        if (player.ArmorTurnsRemaining == 0)
                        {
                            player.Health = Math.Max(player.Health - cell.value, 0);
                            player.PoisonTurnsRemaining = (int)cell.duration;//=3
                            player.startPoison = true;
                            SingleEffectApplied?.Invoke(player.ID, EffectType.Poison, player.Row, executionRank, player.Health);
                        }

                        executionRank++;
                        break;
                    case EffectType.Missile:
                        // Logique du missile : toucher joueur le plus proche sur meme ligne
                        ResolveMissileEffect(player.Row, player.ID, executionRank);
                        executionRank++;
                        break;
                    case EffectType.Laser:
                        // Logique du missile : toucher tous les joueurs sur la même ligne
                        ResolveLaserEffect(player.Row, player.ID, executionRank);
                        executionRank++;
                        break;
                    case EffectType.Armor:
                        player.ArmorTurnsRemaining = (int)cell.duration;//= 2;
                        SingleEffectApplied?.Invoke(player.ID, EffectType.Armor, player.Row, executionRank, player.Health);
                        executionRank++;
                        break;
                    case EffectType.Freeze:
                        player.FreezeTurnsRemaining = (int)cell.duration;//= 1;
                        player.isFrozen = 1;
                        SingleEffectApplied?.Invoke(player.ID, EffectType.Freeze, player.Row, executionRank, player.Health);
                        executionRank++;
                        break;
                }

                // Mettre à jour le joueur dans l'état
                _currentState.Players[i] = player;

            }
        }
    }

    /// <summary>
    /// Résoudre les effets persistants (poison, armor)
    /// </summary>
    private void ResolvePersistentEffects()
    {
        int executionRank = 1;
        for (int i = 0; i < _currentState.Players.Count; i++)
        {
            var player = _currentState.Players[i];
            if (!player.IsAlive) continue;

            // Poison : dégâts par tour
            if (player.PoisonTurnsRemaining > 0)
            {
                if (!player.startPoison)
                {
                    player.Health = Math.Max(player.Health - 10, 0);
                    SingleEffectApplied?.Invoke(player.ID, EffectType.Poison, player.Row, executionRank, player.Health);
                }
                player.startPoison = false;
                player.PoisonTurnsRemaining = player.PoisonTurnsRemaining - 1;
            }

            // Armor : diminue de 1 tour
            if (player.ArmorTurnsRemaining > 0)
            {
                player.ArmorTurnsRemaining = player.ArmorTurnsRemaining - 1;
                //if ArmorTurnsRemaining=0 OnSingleEffectRemoved.Invoke ?
                if(player.ArmorTurnsRemaining == 0)
                {
                    SingleEffectRemoved?.Invoke(player.ID, EffectType.Armor, executionRank);
                }
                executionRank++;
            }

            if (player.FreezeTurnsRemaining > 0)
            {
                player.FreezeTurnsRemaining = player.FreezeTurnsRemaining - 1;
                //if FreezeTurnsRemaining=0 OnSingleEffectRemoved.Invoke ?
            }
            // Vérifier si le joueur est mort
            if (player.Health <= 0)
            {
                player.IsAlive = false;
            }

            _currentState.Players[i] = player;
        }
    }


    private void ResolveLaserEffect(int targetRow, int launcherPlayerId, int executionRank)
    {
        List<EffectHitInfo> hits = new List<EffectHitInfo>();

        for (int i = 0; i < _currentState.Players.Count; i++)
        {
            var player = _currentState.Players[i];
            if (!player.IsAlive || player.Row != targetRow || player.ID == launcherPlayerId) continue;

            // Si le joueur a de l'armure, elle bloque les dégâts
            if (player.ArmorTurnsRemaining > 0)
            {
                //player.ArmorTurnsRemaining--; player.ArmorTurnsRemaining = 0;// not sure if we want to remove armor on missile hit
            }
            else
            {
                player.Health = Math.Max(player.Health - 30, 0);
                hits.Add(new EffectHitInfo {
                    PlayerId = player.ID,
                    NewHealth = player.Health
                });
            }

            _currentState.Players[i] = player;
        }
        if (MultiEffectApplied != null)
        {
            MultiEffectApplied.Invoke(
                launcherPlayerId,
                EffectType.Laser,
                targetRow,
                executionRank,
                hits.ToArray()
            );
        }
    }

    /// <summary>
    /// Logique du missile : toucher tous les joueurs sur la même ligne
    /// </summary>
    private void ResolveMissileEffect(int targetRow, int launcherPlayerId, int executionRank)
    {
        List<EffectHitInfo> hits = new List<EffectHitInfo>();
        var launcher = _currentState.Players.First(p => p.ID == launcherPlayerId);
        int launcherCol = launcher.Col;

        // Cherche le joueur le plus proche à GAUCHE
        PlayerState closestLeft = default;
        closestLeft.ID = -1;
        int minLeftDist = int.MaxValue;
        
        // Cherche le joueur le plus proche à DROITE
        PlayerState closestRight = default;
        closestRight.ID = -1;
        int minRightDist = int.MaxValue;

        foreach (var player in _currentState.Players)
        {
            if (!player.IsAlive || player.Row != targetRow || player.ID == launcherPlayerId) continue;

            int distance = Math.Abs(player.Col - launcherCol);
            if (player.Col < launcherCol && distance < minLeftDist)
            {
                minLeftDist = distance;
                closestLeft = player;
            }
            else if (player.Col > launcherCol && distance < minRightDist)
            {
                minRightDist = distance;
                closestRight = player;
            }
        }

        // Inflige des dégâts au plus proche à gauche
        if (closestLeft.ID != -1)
        {
            if (closestLeft.ArmorTurnsRemaining == 0){
                closestLeft.Health = Math.Max(closestLeft.Health - 30, 0);
                int index = _currentState.Players.FindIndex(p => p.ID == closestLeft.ID);
                _currentState.Players[index] = closestLeft;
                hits.Add(new EffectHitInfo {
                    PlayerId = closestLeft.ID,
                    NewHealth = closestLeft.Health
                });
             } else {
                closestLeft.ArmorTurnsRemaining--;
            }
        }

        // Inflige des dégâts au plus proche à droite
        if (closestRight.ID != -1)
        {
            if (closestRight.ArmorTurnsRemaining == 0) {
                closestRight.Health = Math.Max(closestRight.Health - 30, 0);
                int index = _currentState.Players.FindIndex(p => p.ID == closestRight.ID);
                _currentState.Players[index] = closestRight;
                hits.Add(new EffectHitInfo {
                    PlayerId = closestRight.ID,
                    NewHealth = closestRight.Health
                });
            } else {
                closestRight.ArmorTurnsRemaining--;
            }
                
        }
        if (MultiEffectApplied != null)
        {
            MultiEffectApplied.Invoke(
                launcherPlayerId,
                EffectType.Missile,
                targetRow,
                executionRank,
                hits.ToArray()
            );
        }
    }
    
    public DuelResult ResolveDuelLogic(CollisionDuel duel, Dictionary<int, int> playerChoices)
    {
        // 1. Le Moteur "lance la pièce" (0 = Or, 1 = Argent)
        int coinResult = _rng.Next(0, 2); 
        Console.WriteLine($"🪙 Résultat du Flip Coin : {(coinResult == 0 ? "Or" : "Argent")}");

        int winnerId = -1;
        int loserId = -1;

        // 2. Déterminer le gagnant en comparant avec le coinResult
        // S'il n'y a que 2 joueurs (cas actuel)
        if (duel.PlayerIDs.Count == 2)
        {
            int p1 = duel.PlayerIDs[0];
            int p2 = duel.PlayerIDs[1];

            // On vérifie le choix de P1. Si pas de choix (ex: IA), on lui assigne 0 par défaut et P2 aura 1.
            int p1Choice = playerChoices.ContainsKey(p1) ? playerChoices[p1] : 0;
            int p2Choice = playerChoices.ContainsKey(p2) ? playerChoices[p2] : (p1Choice == 0 ? 1 : 0);

            // Si P1 a deviné juste, il gagne. Sinon c'est P2.
            if (p1Choice == coinResult)
            {
                winnerId = p1;
                loserId = p2;
            }
            else
            {
                winnerId = p2;
                loserId = p1;
            }
        }
        else
        {
            // Fallback si + de 2 joueurs (à implémenter plus tard)
            int winnerIdx = _rng.Next(0, duel.PlayerIDs.Count);
            winnerId = duel.PlayerIDs[winnerIdx];
            loserId = duel.PlayerIDs[winnerIdx == 0 ? 1 : 0]; // (Simplifié pour l'instant)
        }

        Console.WriteLine($"🏆 Gagnant du duel : Joueur {winnerId + 1} !");

        // 3. Calculer la position d'expulsion du perdant (Ton code existant)
        Vector2Int expulsionPos = FindBumpingPosition(duel.Row, duel.Col);

        // 4. Mettre à jour la position du perdant dans le moteur (Ton code existant)
        int pIdx = _currentState.Players.FindIndex(p => p.ID == loserId);
        var updatedLoser = _currentState.Players[pIdx];
        updatedLoser.Row = expulsionPos.x;
        updatedLoser.Col = expulsionPos.y;
        _currentState.Players[pIdx] = updatedLoser;

        return new DuelResult { WinnerId = winnerId, LoserId = loserId, LoserNewPos = expulsionPos };
    }
    
    private Vector2Int FindBumpingPosition(int r, int c)
    {
        // Liste des directions prioritaires (Gauche, Droite, Haut, Bas)
        int[] dr = { 0, 0, 1, -1 };
        int[] dc = { -1, 1, 0, 0 };

        for (int i = 0; i < 4; i++)
        {
            int nr = r + dr[i];
            int nc = c + dc[i];

            // Vérifier si la case est dans la grille
            if (nr >= 0 && nr < _currentState.Rows && nc >= 0 && nc < _currentState.Cols)
            {
                // Vérifier si personne n'est sur cette case (simplifié)
                if (!_currentState.Players.Any(p => p.IsAlive && p.Row == nr && p.Col == nc))
                {
                    return new Vector2Int(nr, nc);
                }
            }
        }
        
        // Si tout est bouché, on renvoie une case par défaut (Haut)
        return new Vector2Int(r + 1, c);
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