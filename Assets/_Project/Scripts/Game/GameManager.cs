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

        engine.EffectApplied += (playerId, effectType, value, executionRank) => StartCoroutine(OnEffectApplied(playerId, effectType, value, executionRank));
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

    IEnumerator OnEffectApplied(int playerId, EffectType effectType, int value, int rank)
    {
         // 🔥 Le secret : chaque animation attend son tour
        yield return new WaitForSeconds(rank * 1.5f); 
        // Tu récupères le joueur et tu joues l'animation
        GameObject playerObj = players[playerId];
        switch(effectType)
        {
            case EffectType.HealthPotion:
                Debug.Log($"Anim Heal Joueur {playerId+1}");
            // Spawn 5 petites sphères vertes aléatoirement autour du joueur
                for (int i = 0; i < 5; i++)
                {
                    GameObject healParticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    healParticle.transform.position = playerObj.transform.position + Random.insideUnitSphere * 0.5f;
                    healParticle.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                    healParticle.GetComponent<Renderer>().material.color = Color.green;

                    // Faire monter la particule puis la détruire
                    StartCoroutine(MoveUpAndDestroy(healParticle));
                }
                yield return new WaitForSeconds(0.5f);
                break;

            case EffectType.DamageBomb:
                Debug.Log($"[Anim] Bomb Joueur {playerId+1}");
                // Anim : secousse + flash rouge
                Vector3 originalPos = playerObj.transform.position;
                for (int i = 0; i < 3; i++)
                {
                    playerObj.transform.position += Random.insideUnitSphere * 0.1f;
                    yield return new WaitForEndOfFrame();
                }
                playerObj.transform.position = originalPos;
                break;

            case EffectType.Missile:
                 int targetRow = value;

                // 1. Créer le missile ET L'ORIENTER HORIZONTALEMENT
                GameObject missile = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                missile.transform.position = playerObj.transform.position + Vector3.up * 0.5f;
                missile.transform.localScale = new Vector3(0.8f, 0.2f, 0.2f); // Plus long et plus fin
                missile.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // Rotation de 90° pour qu'il soit horizontal
                missile.GetComponent<Renderer>().material.color = new Color(1f, 0.8f, 0f); // Jaune/orange pour le contraste

                // 2. Le reste du code de déplacement reste identique
                float speed = 12f;
                float endX = gridManager.GetCellWorldPosition(targetRow, gridManager.columns - 1).x;
                float startX = gridManager.GetCellWorldPosition(targetRow, 0).x;

                while (Vector3.Distance(missile.transform.position, new Vector3(endX, missile.transform.position.y, missile.transform.position.z)) > 0.1f)
                {
                    missile.transform.Translate(Vector3.right * speed * Time.deltaTime);
                    yield return null;
                }

                Destroy(missile);
                break;

            case EffectType.Freeze:
                Debug.Log($"[Anim] Freeze Joueur {playerId+1}");
                Renderer playerRend = playerObj.GetComponent<Renderer>();
                Color originalColor = playerRend.material.color;

                // 1. Créer le cube de glace AU DESSUS du joueur, pas dedans
                GameObject iceCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                iceCube.name = "IceCube"; // Pour le retrouver plus tard dans OnEffectRemoved
                iceCube.transform.parent = playerObj.transform; // Le rattacher au joueur pour qu'il suive
                iceCube.transform.localPosition = Vector3.zero; // Position relative au joueur
                iceCube.transform.localScale = playerObj.transform.localScale * 1.2f; // 20% plus grand pour être visible autour
                iceCube.transform.localRotation = Quaternion.identity;

                // 2. Configurer le matériau de glace correctement
                Material iceMat = new Material(Shader.Find("Standard"));
                iceMat.color = new Color(0.3f, 0.7f, 1f, 0.4f); // Bleu glace plus visible
                iceMat.SetFloat("_Mode", 3);
                iceMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                iceMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                iceMat.SetInt("_ZWrite", 0); // Désactiver l'écriture en profondeur pour que le joueur soit visible à travers la glace
                iceCube.GetComponent<Renderer>().material = iceMat;

                // 3. Tinter le joueur en bleu glace
                playerRend.material.color = new Color(0.7f, 0.9f, 1f);
                break;

            case EffectType.Poison:
                for (int i = 0; i < 5; i++)
                {
                    GameObject poisonParticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    poisonParticle.transform.position = playerObj.transform.position + Random.insideUnitSphere * 0.5f;
                    poisonParticle.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                    poisonParticle.GetComponent<Renderer>().material.color = new Color(0.8f, 0.2f, 0.8f); // Violet foncé

                    // Plus lent que le soin pour donner une impression de lenteur toxique
                    StartCoroutine(MoveUpAndDestroy(poisonParticle, true));
                }
                yield return new WaitForSeconds(0.5f);
                break;

            case EffectType.Armor:
                Debug.Log($"[Anim] Armor Joueur {playerId+1}");
                // Anim : bouclier lumineux
                break;
        }
    }

    IEnumerator OnEffectRemoved(int playerId, EffectType effectType, int value, int rank)
    {
        yield return new WaitForSeconds(rank * 1.5f);
        GameObject playerObj = players[playerId];
        switch(effectType)
        {
            case EffectType.Freeze:
                Debug.Log($"[Anim] Freeze Removed Joueur {playerId+1}");
  Transform cube = playerObj.transform.Find("IceCube");
    if (cube != null) Destroy(cube.gameObject);

    // Remettre la VRAIE couleur du joueur (pas forcément blanc)
    Renderer pRend = playerObj.GetComponent<Renderer>();
    pRend.material.color = GetPlayerColor(playerId); // Utilise ta fonction existante
                break;
        }
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

    void StartSelectionPhase(GameState state = null)
    {
        isSelectionPhase = true;
        selectionTimer = selectionTime;
 
        foreach (var pState in state.Players)
        {
            if (players[pState.ID] != null && players[pState.ID].activeSelf)
            {
                Vector2Int current = GetPlayerCurrentCell(pState.ID);
                playerTargets[pState.ID] = current; // Par défaut: rester sur place
            }
            //todo fct remove Freezed effect
            if (pState.isFrozen == 1 && pState.FreezeTurnsRemaining == 0)
            {
                engine.ClearFreezeEffect(pState.ID);
                StartCoroutine(OnEffectRemoved(pState.ID, EffectType.Freeze, 0, 0));
            }
        }
        if (playerControlModes[localPlayerID] == ControlMode.Human 
        && players[localPlayerID] != null
        && state.Players.Find(p => p.ID == localPlayerID).FreezeTurnsRemaining == 0) {
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
    
    IEnumerator MoveUpAndDestroy(GameObject obj, bool slow = false)
    {
        float speed = slow ? 1f : 2f;
        float lifetime = slow ? 1.5f : 1f;
        float timer = 0f;

        while (timer < lifetime)
        {
            obj.transform.Translate(Vector3.up * speed * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }
        Destroy(obj);
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
            CellEffect[] topRowData = new CellEffect[state.Cols];
            for(int c=0; c < state.Cols; c++) {
                topRowData[c] = state.Grid[state.Rows - 1, c];
            }

            CellEffect[] newFutureRowData = state.FutureRow;
            if (newFutureRowData == null || newFutureRowData.Length != state.Cols)
            {
                Debug.LogWarning("Le moteur n'a pas fourni de FutureRow valide. Utilisation d'un tableau vide.");
                newFutureRowData = new CellEffect[state.Cols]; // Tableau vide par défaut
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
        StartSelectionPhase(state);
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

                case EffectType.Freeze:
                    score = -100f; // 🟣 On évite
                    break;

                case EffectType.Armor:
                    if (aiPlayer.health < 50)
                        score = 800f; // Très prioritaire
                    else if (aiPlayer.health < 80 && aiPlayer.health > 50)
                        score = 150f;
                    else
                        score = 50f;
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