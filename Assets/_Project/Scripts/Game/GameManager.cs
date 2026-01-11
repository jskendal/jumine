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
 
    private List<GameObject> highlightedBorders = new List<GameObject>();
    public static GameManager instance;

    [Header("Health UI")]
    public Slider[] playerHealthSliders; // Size = 4
    public TMPro.TextMeshProUGUI[] playerLabels; // Size = 4
    
    private bool hasDoneSpawnPlayers = false;

    private Vector2Int[] playerTargets = new Vector2Int[4];

    [Header("Contrôle Joueurs")]
    public int localPlayerID = 1; 
    public ControlMode[] playerControlModes = new ControlMode[4] { ControlMode.AI, ControlMode.Human, ControlMode.AI, ControlMode.AI };
    
    private GameEngine engine; // Le moteur de jeu
    private List<PlayerAction> currentTurnActions = new List<PlayerAction>();
    private int[] playerCols = new int[] { 2, 7, 12, 17 }; 

    void Start()
    {
        // Initialiser le moteur de jeu
        engine = new GameEngine(gridManager.rows, gridManager.columns);

        // Ajouter les 4 joueurs au moteur
        for(int i=0; i<4; i++)
        {
            engine.AddPlayer(new PlayerState
            {
                ID = i,
                Health = 100,
                MaxHealth = 100,
                Row = 0,
                Col = playerCols[i],
                IsAlive = true
            });
        }

        StartCoroutine(StartGameAfterGridReady());

        InitializeHealthUI();
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
        // 1. On attend que les références soient là
        while (gridManager == null) yield return null;

        // 2. On récupère l'état initial du moteur
        GameState initialState = engine.GetCurrentState();

        // 3. On demande à GridManager de créer la grille à partir de cet état
        gridManager.GenerateGrid(initialState);
        gridManager.CenterGrid();
        gridManager.GenerateFutureRow(initialState.FutureRow);

        yield return null;

        ForceCameraPosition();
        SpawnPlayers();
        selectionTimer = selectionTime;
        if (timerText != null) timerText.text = $"CHOISISSEZ ! {selectionTimer:F1}s";
        if (timerSlider != null) { timerSlider.maxValue = 1f; timerSlider.value = 1f; }
        StartSelectionPhase();
    }
    
    void ForceCameraPosition()
    {
        if (mainCamera == null) return;
        mainCamera.transform.position = new Vector3(0f, cameraHeight, -cameraDistance);
        mainCamera.transform.rotation = Quaternion.Euler(cameraAngle, 0f, 0f);
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
        if (playerControlModes[localPlayerID] == ControlMode.Human && players[localPlayerID] != null) {
            StartCoroutine(ShowHighlightsAfterDelay(0.1f));
        }
    }
    
    public void SetPlayerTarget(int playerIndex, int row, int col)
    {
        // On met à jour l'affichage visuel (ton tableau playerTargets existant)
        playerTargets[playerIndex] = new Vector2Int(row, col);

        // On prépare l'action pour le moteur (on remplace si déjà existante pour ce joueur)
        currentTurnActions.RemoveAll(a => a.PlayerID == playerIndex);
        currentTurnActions.Add(new PlayerAction { 
            PlayerID = playerIndex, 
            TargetRow = row, 
            TargetCol = col 
        });

        Debug.Log($"Cible enregistrée pour Moteur : Joueur {playerIndex + 1} -> ({row},{col})");
    }

    IEnumerator ShowHighlightsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowSelectionHighlights();
    }
    
    void ShowSelectionHighlights()
    {
        ClearHighlights();
        
        int idx = localPlayerID; // le joueur contrôlé à la souris
        
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
        selectionTimer = Mathf.Max(0f, selectionTimer - Time.deltaTime);

        if (timerText != null)
            timerText.text = $"CHOISISSEZ ! {Mathf.Max(0, selectionTimer):F1}s";
        
        if (timerSlider != null)
                timerSlider.value = selectionTimer / selectionTime;

        //if (selectionTimer <= 0) EndSelectionPhase();
         if (selectionTimer <= 0f)
        {
            // Sécurité supplémentaire: vérifier que la grille est prête
            if (!gridManager.IsGridReady()) return;

            EndSelectionPhase();
        }
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

        // 1. IA : Demander aux IA de remplir leurs PlayerActions
        for (int i = 0; i < players.Length; i++)
        {
            if (playerControlModes[i] == ControlMode.AI && players[i].activeSelf)
            {
                // On utilise ta logique IA actuelle pour obtenir une cible
                Vector2Int aiTarget = DetermineAITarget(i);
                SetPlayerTarget(i, aiTarget.x, aiTarget.y);
            }
        }

         // 2. EXÉCUTER LA LOGIQUE DANS LE MOTEUR
        // C'est ici que le "cerveau" travaille
        engine.ProcessTurn(currentTurnActions);
        currentTurnActions.Clear(); // On vide pour le prochain tour
        

        // 3. RÉCUPÉRER LE RÉSULTAT
        GameState state = engine.GetCurrentState();

        // 4. DEMANDER À UNITY D'ANIMER LE RÉSULTAT
        // On utilise les données du moteur pour dire à Unity quoi faire
        StartCoroutine(SyncUnityWithEngine(state));

        // IMMÉDIATEMENT : Insertion et jump
        //InsertNewRow();
        
        // NE PAS appeler StartSelectionPhase() ici
        // Ce sera appelé après le jump (dans InsertNewRow())
    }
    
    IEnumerator SyncUnityWithEngine(GameState state)
    {
            // 1. Désactiver les choix visuels (cyan/jaune)
        ClearHighlights();

        // 1. Récupérer la ligne du haut depuis le moteur
            CellData[] topRowData = new CellData[state.Cols];
            for(int c=0; c < state.Cols; c++) {
                topRowData[c] = state.Grid[state.Rows - 1, c];
            }

            CellData[] newFutureRowData = state.FutureRow;
            if (newFutureRowData == null || newFutureRowData.Length != state.Cols)
            {
                Debug.LogWarning("Le moteur n'a pas fourni de FutureRow valide. Utilisation d'un tableau vide.");
                newFutureRowData = new CellData[state.Cols]; // Tableau vide par défaut
            }
            // 2. Passer cette ligne à Unity
            gridManager.InsertRow(topRowData, newFutureRowData);


        // B. Faire sauter les joueurs vers leurs nouvelles positions calculées par le moteur
        foreach (var pState in state.Players)
        {
            // 1. Vérifier que l'ID est valide
            if (pState.ID < 0 || pState.ID >= players.Length)
            {
                Debug.LogError($"ID Joueur invalide : {pState.ID}");
                continue; // Passe au joueur suivant
            }

            // 2. Récupérer l'objet visuel correspondant
            GameObject playerObj = players[pState.ID];

            // 3. Vérifier que l'objet existe (il a pu être détruit si le joueur est mort)
            if (playerObj != null && playerObj.activeSelf)
            {
                // Le joueur est vivant ET son objet existe -> On le bouge
                Vector3 targetPos = gridManager.GetCellWorldPosition(pState.Row, pState.Col);
                StartCoroutine(JumpToPosition(playerObj, targetPos));
            }
            else if (pState.IsAlive)
            {
                // Cas rare : Le moteur dit qu'il est vivant, mais l'objet n'existe pas.
                Debug.LogWarning($"Le Joueur {pState.ID} est marqué vivant dans le moteur mais son GameObject est manquant !");
            }
        }

        yield return new WaitForSeconds(jumpDuration + 0.1f);

        // C. Mettre à jour les barres de vie et les effets visuels
        foreach (var pState in state.Players)
        {
            UpdatePlayerHealthBar(pState.ID, pState.Health);
            
            if (!pState.IsAlive && players[pState.ID].activeSelf)
            {
                players[pState.ID].SetActive(false);
            }
        }

            // 5. Vérifier la fin de partie
        int survivors = state.Players.Count(p => p.IsAlive);
        if (survivors <= 1)
        {
            var winner = state.Players.FirstOrDefault(p => p.IsAlive);
            timerText.text = $"FIN ! Vainqueur: Joueur {winner.ID + 1}";
            yield break; // On arrête la boucle du jeu
        }

        // D. Relancer le tour suivant
        StartSelectionPhase();
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
            int futureRow = cell.x + 1;
            CellEffect effect;
       
            effect = gridManager.GetCellEffect(futureRow, cell.y);

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