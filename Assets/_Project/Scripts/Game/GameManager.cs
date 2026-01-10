using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static GridManager;
using UnityEngine.UI;
using System.Linq;

public class GameManager : MonoBehaviour
{
    [Header("Références")]
    public GridManager gridManager;
    public GameObject playerPrefab;
    
    [Header("Paramètres")]
    public float timeBetweenRows = 5f;
    public float selectionTime = 10f;
    
    [Header("Camera")]
    public Camera mainCamera;
    public float cameraHeight = 15f;
    public float cameraDistance = 12f;
    public float cameraAngle = 45f;
    
    [Header("Jump")]
    public float jumpHeight = 1.5f;
    public float jumpDuration = 0.5f;
    
    [Header("UI")]
    public UnityEngine.UI.Text timerText;
    public UnityEngine.UI.Slider timerSlider;
    
    // Variables
    private float rowTimer = 0f;
    private float selectionTimer = 0f;
    private bool isSelectionPhase = true;
    public GameObject[] players;
    private int controlledPlayerIndex = 0;
    private List<GameObject> highlightedBorders = new List<GameObject>();
    public static GameManager instance;

    [Header("Health UI")]
    public Slider[] playerHealthSliders; // Size = 4
    public TMPro.TextMeshProUGUI[] playerLabels; // Size = 4
    
    private bool hasDoneSpawnPlayers = false;

    private Vector2Int[] playerTargets = new Vector2Int[4];

    [Header("Contrôle Joueurs")]
    public int humanPlayerIndex = 1; 
    public ControlMode[] playerControlModes = new ControlMode[4] { ControlMode.AI, ControlMode.Human, ControlMode.AI, ControlMode.AI };

    void Start()
    {
        StartCoroutine(StartGameAfterGridReady());

        InitializeHealthUI();
    }
    void Awake() {
        instance = this;
        if (playerControlModes == null || playerControlModes.Length != 4)
            playerControlModes = new ControlMode[4];

        // Si tu veux FORCER une config par défaut à chaque lancement (pour debug)
        playerControlModes[0] = ControlMode.AI;
        playerControlModes[1] = ControlMode.Human;
        playerControlModes[2] = ControlMode.AI;
        playerControlModes[3] = ControlMode.AI;
    }

    void InitializeHealthUI()
    {
        if (playerHealthSliders == null || playerHealthSliders.Length < 4) 
        {
            Debug.LogWarning("Health sliders non assignés");
            return;
        }
        
        for (int i = 0; i < 4; i++)
        {
            if (playerHealthSliders[i] != null)
            {
                playerHealthSliders[i].maxValue = 100;
                playerHealthSliders[i].value = 100; // Mettre à 100% pour tous
                
                // Important : S'assurer que le fill est visible
                if(playerHealthSliders[i].fillRect == null)
                {
                    
                }
                else
                {
                    Image fillImage = playerHealthSliders[i].fillRect.GetComponent<Image>();
                    if (fillImage != null)
                    {
                        fillImage.enabled = true;
                        fillImage.color = GetPlayerColor(i); // Utiliser ta couleur de joueur
                    }
                }
            }
        }
    }

    Color GetPlayerColor(int index)
    {
        Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow };
        return index < colors.Length ? colors[index] : Color.white;
    }
    
    public void UpdatePlayerHealthBar(int playerIndex, int health)
    {
        if (playerIndex < 0 || playerIndex >= playerHealthSliders.Length) return; // Sécurité de l'index
        if (playerHealthSliders[playerIndex] == null) {
            Debug.LogWarning($"Slider UI for player {playerIndex + 1} is not assigned in the Inspector.");
            return;
        }

        Slider targetSlider = playerHealthSliders[playerIndex];

        // Mettre à jour la valeur
        targetSlider.value = health;
        
        // Récupérer l'Image du "fill"
        // 🔥 CORRECTION : On cherche l'Image sur le `fillRect` ou sur un de ses enfants
        Image fillImage = null;
        if (targetSlider.fillRect != null)
        {
            fillImage = targetSlider.fillRect.GetComponent<Image>();
            if (fillImage == null && targetSlider.fillRect.childCount > 0)
            {
                // Tente de trouver l'image sur le premier enfant du fillRect (ex: GameObject "Fill")
                fillImage = targetSlider.fillRect.GetChild(0).GetComponent<Image>();
            }
        }

        if (fillImage == null)
        {
            Debug.LogWarning($"No Image component found on Fill Rect or its first child for player {playerIndex + 1}. Cannot update health bar color/visibility.");
            return; // Ne peut pas continuer sans l'Image
        }

        if (health <= 0)
        {
            fillImage.enabled = false; // Cache la barre de vie
            // Tu peux aussi désactiver le Slider entier ou son GameObject pour un joueur KO
            // targetSlider.gameObject.SetActive(false); 
        }
        else
        {
            if (!fillImage.enabled) fillImage.enabled = true;
            fillImage.color = GetPlayerColor(playerIndex);
        }
    }

    IEnumerator StartGameAfterGridReady()
    {
        while (gridManager == null || !gridManager.IsGridReady()) yield return null;

            yield return null;

        ForceCameraPosition();

        SpawnPlayers();
        StartSelectionPhase();
    }
    
    void ForceCameraPosition()
    {
        if (mainCamera == null) return;
        mainCamera.transform.position = new Vector3(0f, cameraHeight, -cameraDistance);
        mainCamera.transform.rotation = Quaternion.Euler(cameraAngle, 0f, 0f);
    }
    
    void Update()
    {
            // FORCER la caméra à chaque frame pendant 0.5s
        float timer = 0f;
        timer += Time.deltaTime;
        
        if (timer < 0.5f && mainCamera != null)
        {
            mainCamera.transform.position = new Vector3(0f, cameraHeight, -cameraDistance);
            mainCamera.transform.rotation = Quaternion.Euler(cameraAngle, 0f, 0f);
        }
        if (isSelectionPhase)
        {
            UpdateSelectionTimer();
        }
        else
        {
            UpdateRowTimer();
        }
    }
    
    void SpawnPlayers()
    {
        players = new GameObject[4];
         int[] playerCols = new int[4];
        playerCols[0] = 2;           // 2 cellules du bord gauche
        playerCols[1] = playerCols[0] + 5;  // +4 cellules
        playerCols[2] = playerCols[1] + 5;  // +4 cellules
        playerCols[3] = playerCols[2] + 5;  // +4 cellules = 2 du bord droit (si columns=16)
        for (int i = 0; i < 4; i++)
        {
            Vector3 spawnPos = gridManager.GetCellWorldPosition(0, playerCols[i]);
            players[i] = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            players[i].name = $"Player_{i+1}";
            
            Player playerScript = players[i].GetComponent<Player>();
            if (playerScript == null)
                playerScript = players[i].AddComponent<Player>();
            
            // Assigner l’ID et la vie
            playerScript.playerID = i;
            playerScript.health = 100;
            
            Renderer rend = players[i].GetComponent<Renderer>();
            if (rend != null)
            {
                Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow };
                rend.material.color = colors[i];
            }
        }
        hasDoneSpawnPlayers = true;
    }
    
    void StartSelectionPhase()
    {
        isSelectionPhase = true;
        selectionTimer = selectionTime;

        // Initialiser les cibles par défaut (rester sur place)
        for (int i = 0; i < 4; i++)
        {
            if (players[i] != null && players[i].activeSelf)
            {
                Vector2Int current = GetPlayerCurrentCell(i);
                playerTargets[i] = current; // Par défaut: rester sur place
            }
        }
        if (playerControlModes[humanPlayerIndex] == ControlMode.Human && players[humanPlayerIndex] != null) {
            StartCoroutine(ShowHighlightsAfterDelay(0.1f));
        }
    }
    
    public void SetPlayerTarget(int playerIndex, int row, int col)
    {
        playerTargets[playerIndex] = new Vector2Int(row, col);
        Debug.Log($"Joueur {playerIndex + 1} a choisi la case ({row}, {col})");
    }

    IEnumerator ShowHighlightsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowSelectionHighlights();
    }
    
    void ShowSelectionHighlights()
    {
        ClearHighlights();
        
        int idx = humanPlayerIndex; // le joueur contrôlé à la souris
        
        if (idx >= 0 && idx < players.Length && players[idx] != null && players[idx].activeSelf)
        {
            ShowCellsAroundPlayer(players[idx]);
        }
    }
    
    void ShowCellsAroundPlayer(GameObject player)
    {
        Vector2Int playerCell = gridManager.GetCellFromWorldPosition(player.transform.position);
        Debug.Log($"ShowCellsAroundPlayer: joueur en ({playerCell.x},{playerCell.y})");
        List<Vector2Int> selectableCells = gridManager.GetCellsInRadius(playerCell, 2);
        
        foreach (var cell in selectableCells)
        {
            //Debug.Log($"  - Cellule sélectionnable: ({cell.x},{cell.y})");
            GameObject border = gridManager.ShowCellAsSelectable(cell.x, cell.y);
            highlightedBorders.Add(border);
        }
    }
    
    void ClearHighlights()
    {
        // Nettoie le tableau dans GridManager
        if (gridManager != null)
        {
            // Méthode à ajouter dans GridManager :
            gridManager.ClearSelectableCells();
        }
        
        foreach (var border in highlightedBorders)
        {
            if (border != null) Destroy(border);
        }
        highlightedBorders.Clear();
    }
    
    void UpdateSelectionTimer()
    {
        selectionTimer -= Time.deltaTime;
        if (timerText != null)
            timerText.text = $"CHOISISSEZ ! {Mathf.Max(0, selectionTimer):F1}s";
        
        if (timerSlider != null)
                timerSlider.value = selectionTimer / selectionTime;

        if (selectionTimer <= 0) EndSelectionPhase();
    }
    
    void UpdateRowTimer()
    {
        rowTimer += Time.deltaTime;
        if (timerText != null)
            timerText.text = $"Insertion dans: {timeBetweenRows - rowTimer:F1}s";
        
        // if (rowTimer >= timeBetweenRows)
        // {
        //     InsertNewRow();
        //     StartSelectionPhase();
        // }
    }
    
    void EndSelectionPhase()
    {
        isSelectionPhase = false;
        rowTimer = 0f;

        // Pour chaque joueur : si c'est une IA, on lui choisit une cible
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null || !players[i].activeSelf) continue;

            if (playerControlModes[i] == ControlMode.AI)
            {
                playerTargets[i] = DetermineAITarget(i);
                Debug.Log($"IA Player {i+1} a choisi la case ({playerTargets[i].x}, {playerTargets[i].y})");
            }
            // Si c'est un humain, playerTargets[i] est déjà initialisé à sa position actuelle
        }
        
            // ⚠️ TODO : GESTION DES COLLISIONS
        // Vérifier si plusieurs joueurs ont choisi la même case
        // Exemple de code à implémenter plus tard :
        /*
        for(int i=0; i<4; i++) {
        for(int j=i+1; j<4; j++) {
            if(playerTargets[i] == playerTargets[j] && players[i].activeSelf && players[j].activeSelf) {
            // Gérer la collision : repousser l'un des deux, ou infliger des dégâts aux deux
            Debug.Log($"⚠️ Collision prévue entre Joueur {i+1} et {j+1} !");
            }
        }
        }
        */
        
        // IMMÉDIATEMENT : Insertion et jump
        InsertNewRow();
        
        // NE PAS appeler StartSelectionPhase() ici
        // Ce sera appelé après le jump (dans InsertNewRow())
    }
    
    void InsertNewRow()
    {
        Debug.Log("=== INSERTION + JUMP ===");
        
        // 1. Nettoyer les highlights
        ClearHighlights();
        
        // 2. Insérer la ligne
        // 2.1. SAUVEGARDER les coordonnées AVANT insertion
        int savedRow = -1;
        int savedCol = -1;
        
        // if (Cell.selectedCell != null)
        // {
        //     savedRow = Cell.selectedCell.row;
        //     savedRow = playerTargets[0].x;
        //     savedCol = Cell.selectedCell.col;
        //     Debug.Log($"Cellule sélectionnée: ({savedRow},{savedCol}) - SAUVEGARDÉ");
        // }
        // else
        // {
        //     Vector2Int currentCell = GetPlayerCurrentCell(controlledPlayerIndex);
        //     savedRow = currentCell.x;
        //     savedCol = currentCell.y;
        //     // Debug.Log($"Aucune sélection, joueur reste sur: ({savedRow},{savedCol})");
        // }
        gridManager.InsertRow();

        // 3. Faire sauter les joueurs
        StartCoroutine(MoveAllPlayers(savedRow, savedCol));
        
        if(hasDoneSpawnPlayers)  
            StartCoroutine(ResolveTurnEffects());
    }

    IEnumerator ResolveTurnEffects()
    {
        // 1. Attendre la fin de l'anim de saut
        yield return new WaitForSeconds(jumpDuration + 0.2f);

        // 2. Liste des vivants
        List<int> activePlayers = new List<int>();
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].activeSelf) activePlayers.Add(i);
        }

        // 3. Mélanger l'ordre (Random)
        activePlayers = ShuffleList(activePlayers);

        Debug.Log("--- RÉSOLUTION DES EFFETS ---");

        foreach (int playerIndex in activePlayers)
        {
            // Où est ce joueur MAINTENANT ?
            Vector3 playerPos = players[playerIndex].transform.position;
            Vector2Int currentCell = gridManager.GetCellFromWorldPosition(playerPos);

            // Quel est l'effet sous ses pieds ?
            // (Note: gridManager.GetCellEffect doit exister, voir plus bas*)
            CellEffect effect = gridManager.GetCellEffect(currentCell.x, currentCell.y);

            if (effect.type != EffectType.Neutral)
            {
                Debug.Log($"Joueur {playerIndex + 1} active : {effect.type}");
                yield return StartCoroutine(ApplyEffectToPlayerCoroutine(playerIndex, effect, currentCell));
                yield return new WaitForSeconds(0.5f); // Pause dramatique
            }
        }

        // 2. Résolution des effets PERSISTANTS (poison, etc.) – une fois par tour
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null || !players[i].activeSelf) continue;
            Player p = players[i].GetComponent<Player>();
            if (p != null)
            {
                p.ProcessTurnEndEffects();
            }
        }

        // 4. Fin du tour
        CheckGameOver(); // (Ta méthode ou une simple vérification)
        StartSelectionPhase();
    }

    List<int> ShuffleList(List<int> list)
    {
        for (int i = 0; i < list.Count; i++) {
            int temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
        return list;
    }

    IEnumerator ApplyEffectToPlayerCoroutine(int playerIndex, CellEffect effect, Vector2Int cellCoord)
    {
         Player p = players[playerIndex].GetComponent<Player>();
        if (p == null) yield break;

        switch (effect.type)
        {
            case EffectType.HealthPotion:
                p.Heal(effect.value);
                // Afficher icône coeur
                break;

            case EffectType.DamageBomb:
                p.TakeDamage(effect.value);
                // Afficher icône explosion
                break;

            case EffectType.Poison:
                // Ici : pas de dégâts immédiats, on programme 3 tours de poison
                p.ApplyPoison((int)effect.duration, effect.value);
                break;

            case EffectType.Missile:
                // CAS SPÉCIAL : Le joueur qui marche sur le missile subit les dégâts ET déclenche l'arme
                //Non pas de //p.TakeDamage(effect.value); // Il se blesse en marchant dessus (ou pas, selon règles)
                //peut-etre plus tard un autre type de missile qui blesse aussi le déclencheur
                
                // On déclenche l'effet d'arme sur la grille
                ApplyWeaponEffect(effect, cellCoord, playerIndex);
                break;
        }
        
        yield return null;
    }

    void ApplyWeaponEffect(CellEffect effect, Vector2Int originCell, int triggeringPlayerIndex)
    {
        if (effect.type != EffectType.Missile) return;

        Debug.Log($"🚀 MISSILE déclenché en ligne {originCell.x} !");

        // On parcourt tous les joueurs pour voir s'ils sont sur la ligne de tir
        for (int i = 0; i < players.Length; i++)
        {
            if (i == triggeringPlayerIndex) continue;
            if (players[i] == null || !players[i].activeSelf) continue;

            // On récupère la position actuelle du joueur i
            Vector2Int playerPos = gridManager.GetCellFromWorldPosition(players[i].transform.position);

            // Règle du Missile : Touche tout le monde sur la même LIGNE (Row)
            if (playerPos.x == originCell.x)
            {
                Player pScript = players[i].GetComponent<Player>();
                if (pScript != null)
                {
                    pScript.TakeDamage(effect.value);
                    Debug.Log($"💥 Joueur {i + 1} touché par le missile !");
                    // TODO: Ajouter particules explosion ici
                }
            }
        }
    }
    void CheckGameOver()
    {
        int aliveCount = 0;
        int lastSurvivor = -1;
        
        foreach(var p in players)
        {
            if(p != null && p.activeSelf)
            {
                aliveCount++;
                lastSurvivor = System.Array.IndexOf(players, p);
            }
        }

        if(aliveCount <= 1)
        {
            Debug.Log($"🏆 GAME OVER ! Vainqueur : Joueur {lastSurvivor + 1}");
            isSelectionPhase = false; // Arrêter le jeu
            // Afficher UI Victoire
        }
    }

    
    IEnumerator MoveAllPlayers(int savedRow = -1, int savedCol = -1)
    {
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].activeSelf)
            {
                // On récupère la cible enregistrée pour ce joueur
                Vector2Int target = playerTargets[i];
                Vector3 worldPos = gridManager.GetCellWorldPosition(target.x, target.y);
                
                StartCoroutine(JumpToPosition(players[i], worldPos));
            }
        }
        
        yield return new WaitForSeconds(jumpDuration);
        StartCoroutine(ResolveTurnEffects());
    }
    
    IEnumerator JumpToPosition(GameObject player, Vector3 targetPosition)
    {
        Vector3 startPosition = player.transform.position;
        float timer = 0f;
        
            // Garder la même hauteur Y que le départ
    float baseY = startPosition.y;
    targetPosition.y = baseY; // S'assurer que la position cible a la même hauteur

        while (timer < jumpDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / jumpDuration;
            Vector3 horizontalPos = Vector3.Lerp(startPosition, targetPosition, progress);
            float height = Mathf.Sin(progress * Mathf.PI) * jumpHeight;
            player.transform.position = new Vector3(horizontalPos.x, startPosition.y + height, horizontalPos.z);
            yield return null;
        }
        player.transform.position = targetPosition;
    }

    Vector2Int GetPlayerCurrentCell(int playerIndex)
    {
        if (players == null)
        {
            Debug.LogWarning("⚠️ GetPlayerCurrentCell: players est null");
            return new Vector2Int(-1, -1);
        }
        if (playerIndex < 0 || playerIndex >= players.Length) 
        {
            Debug.LogWarning($"⚠️ PlayerIndex {playerIndex} hors limites");
            return new Vector2Int(-1, -1);
        }
        
        if (players[playerIndex] == null) 
        {
            Debug.LogWarning($"⚠️ Player {playerIndex} est null");
            return new Vector2Int(-1, -1);
        }
        
        if (gridManager == null)
        {
            Debug.LogWarning($"⚠️ GridManager est null");
            return new Vector2Int(-1, -1);
        }
        
        Vector3 playerPos = players[playerIndex].transform.position;
        Debug.Log($"🔍 GetPlayerCurrentCell: Player {playerIndex} position = {playerPos}");
        
        Vector2Int cell = gridManager.GetCellFromWorldPosition(playerPos);
        Debug.Log($"🔍 GetPlayerCurrentCell: résultat = ({cell.x},{cell.y})");
        
        return cell;
    }

    // Dans GameManager.cs
    Vector2Int DetermineAITarget(int playerIndex)
    {
        // 1. Sécurité : vérifier que le joueur est valide
        if (playerIndex < 0 || playerIndex >= players.Length || players[playerIndex] == null)
        {
            Debug.LogWarning($"⚠️ AI pour joueur {playerIndex} invalide");
            return new Vector2Int(-1, -1);
        }

        Player aiPlayer = players[playerIndex].GetComponent<Player>();
        Vector2Int currentCell = GetPlayerCurrentCell(playerIndex);

        // 2. Récupérer TOUTES les cases atteignables (même rayon 2 que l'humain)
        List<Vector2Int> reachableCells = gridManager.GetCellsInRadius(currentCell, 2);

        // 3. Si pas de case atteignable, rester sur place
        if (reachableCells.Count == 0)
        {
            Debug.Log($"🤖 AI Joueur {playerIndex+1} ne peut bouger, reste sur place");
            return currentCell;
        }

        // 4. 🎯 SCORER CHAQUE CASE (plus le score est élevé, mieux c'est)
        Dictionary<Vector2Int, float> cellScores = new Dictionary<Vector2Int, float>();

        foreach (Vector2Int cell in reachableCells)
        {
            float score = 0f;
            CellEffect effect = gridManager.GetCellEffect(cell.x, cell.y);

            // RÈGLES DE SCORING MODIFIABLES
            switch (effect.type)
            {
                case EffectType.DamageBomb:
                    score = -1000f; // 🚫 À ÉVITER ABSOLUMENT
                    break;

                case EffectType.Poison:
                    score = -200f; // 🟣 On évite
                    break;

                case EffectType.HealthPotion:
                    // 🟢 Si l'IA a peu de PV, la potion vaut beaucoup plus cher
                    if (aiPlayer.health < 50)
                        score = 800f; // Très prioritaire
                    else if (aiPlayer.health < 80 && aiPlayer.health > 50)
                        score = 150f;
                    else
                        score = 50f;
                    break;

                case EffectType.Neutral:
                    // 🟡 Petit bonus si on bouge vers l'avant pour ne pas rester coincé
                    if (cell.x > currentCell.x)
                        score = 50f;
                    else
                        score = 10f;
                    break;

                case EffectType.Missile:
                    score = 75f; // 🟡 On autorise l'IA à prendre le missile pour attaquer les autres
                    break;
            }

            cellScores.Add(cell, score);
        }

        // 5. Choisir la meilleure case (avec un peu de hasard pour ne pas être trop prévisible)
        Vector2Int bestCell = currentCell;
        float bestScore = -9999f;

        // 15% de chance de choisir une case aléatoire parmi les 3 meilleures pour éviter l'IA parfaite
        bool useRandom = Random.value < 0.15f;

        if (useRandom)
        {
            var topCells = cellScores.OrderByDescending(kvp => kvp.Value).Take(3).ToList();
            bestCell = topCells[Random.Range(0, topCells.Count)].Key;
            Debug.Log($"🤖 AI Joueur {playerIndex+1} choisit une case aléatoire pour varier !");
        }
        else
        {
            // Prendre la case avec le score le plus élevé
            foreach (var kvp in cellScores)
            {
                if (kvp.Value > bestScore)
                {
                    bestScore = kvp.Value;
                    bestCell = kvp.Key;
                }
            }
        }
        if (bestScore < 0) bestCell = currentCell;
        Debug.Log($"🤖 AI Joueur {playerIndex+1} choisit ({bestCell.x},{bestCell.y}) | Score: {bestScore}");
        return bestCell;
    }
}