using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static GridManager;
using UnityEngine.UI;

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
    private GameObject[] players;
    private int controlledPlayerIndex = 0;
    private List<GameObject> highlightedBorders = new List<GameObject>();
    

    [Header("Health UI")]
    public Slider[] playerHealthSliders; // Size = 4
    public TMPro.TextMeshProUGUI[] playerLabels; // Size = 4
    
    void Start()
    {
        //if (mainCamera == null) mainCamera = Camera.main;
        // if (mainCamera == null)
        // {
        //     mainCamera = Camera.main;
        //     if (mainCamera == null)
        //     {
        //         // Option 2 : Cherche n'importe quelle caméra
        //         Camera[] cameras = FindObjectsOfType<Camera>();
        //         if (cameras.Length > 0)
        //             mainCamera = cameras[0];
        //     }
        // }
        StartCoroutine(StartGameAfterGridReady());

        InitializeHealthUI();
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
                Image fillImage = playerHealthSliders[i].fillRect.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.enabled = true;
                    fillImage.color = GetPlayerColor(i); // Utiliser ta couleur de joueur
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
        if (playerIndex < 0 || playerIndex >= 4) return;
        if (playerHealthSliders[playerIndex] == null) return;
        
        // Mettre à jour la valeur
        playerHealthSliders[playerIndex].value = health;
        
        // Récupérer l'image du fill
        Image fillImage = playerHealthSliders[playerIndex].fillRect.GetComponent<Image>();
        
        if (health <= 0)
        {
            // Désactiver le fill quand PV = 0
            fillImage.enabled = false;
        }
        else
        {
            // Réactiver le fill si besoin
            if (!fillImage.enabled)
                fillImage.enabled = true;
            
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
            
            if (players[i].GetComponent<Player>() == null)
                players[i].AddComponent<Player>();
            
            Renderer rend = players[i].GetComponent<Renderer>();
            if (rend != null)
            {
                Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow };
                rend.material.color = colors[i];
            }
        }
    }
    
    void StartSelectionPhase()
    {
        isSelectionPhase = true;
        selectionTimer = selectionTime;
        StartCoroutine(ShowHighlightsAfterDelay(0.1f));
    }
    
    IEnumerator ShowHighlightsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowSelectionHighlights();
    }
    
    void ShowSelectionHighlights()
    {
        ClearHighlights();
        
        if (players[controlledPlayerIndex] != null && players[controlledPlayerIndex].activeSelf)
        {
            ShowCellsAroundPlayer(players[controlledPlayerIndex]);
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
        
        if (Cell.selectedCell != null)
        {
            savedRow = Cell.selectedCell.row;
            savedCol = Cell.selectedCell.col;
            Debug.Log($"Cellule sélectionnée: ({savedRow},{savedCol}) - SAUVEGARDÉ");
        }
        else
        {
            Vector2Int currentCell = GetPlayerCurrentCell(controlledPlayerIndex);
            savedRow = currentCell.x;
            savedCol = currentCell.y;
            // Debug.Log($"Aucune sélection, joueur reste sur: ({savedRow},{savedCol})");
        }
        gridManager.InsertRow();

        // 3. Faire sauter les joueurs
        StartCoroutine(MoveAllPlayers(savedRow, savedCol));
        
        StartCoroutine(CheckBonusMalusAfterJump(savedRow, savedCol));
    }

    IEnumerator CheckBonusMalusAfterJump(int savedRow, int savedCol)
    {
        // 1. Attendre la fin du saut
        yield return new WaitForSeconds(jumpDuration + 0.1f);
        
        // 2. Vérifier le type de case (si cellule valide)
        if (savedRow >= 0 && savedCol >= 0)
        {
            CellType cellType = gridManager.GetCellType(savedRow, savedCol);
            Debug.Log($"🎯 Joueur a atterri sur: {cellType} à ({savedRow},{savedCol})");
            
            // 3. Si c'est un bonus/malus, attendre et appliquer
            if (cellType != CellType.Neutral)
            {
                Debug.Log("⏳ Attente de 2 secondes avant effet...");
                yield return new WaitForSeconds(2f);
                
                // 4. Appliquer effet au joueur concerné
                ApplyEffectToPlayer(controlledPlayerIndex, cellType);
            }
            else
            {
                Debug.Log("⚪ Case neutre, attente courte");
                yield return new WaitForSeconds(0.5f);
            }
        }
        else
        {
            // Aucune cellule sélectionnée
            Debug.Log("❌ Aucune cellule sélectionnée");
            yield return new WaitForSeconds(0.5f);
        }
        
        // 5. Repartir le TimerSlider
        StartSelectionPhase();
    }
    
    void ApplyEffectToPlayer(int playerIndex, CellType cellType)
    {
        if (playerIndex < 0 || playerIndex >= players.Length) return;
        if (players[playerIndex] == null) return;
        
        Player playerScript = players[playerIndex].GetComponent<Player>();
        if (playerScript == null) return;
        
        switch (cellType)
        {
            case CellType.HealthPotion:
                playerScript.Heal(30);
                Debug.Log($"💚 Joueur {playerIndex+1} gagne 30 PV");
                break;
                
            case CellType.DamageBomb:
                playerScript.TakeDamage(30);
                Debug.Log($"💥 Joueur {playerIndex+1} perd 30 PV");
                break;
                
            default:
                Debug.Log($"❓ Effet non implémenté: {cellType}");
                break;
        }
    }

    
    IEnumerator MoveAllPlayers(int savedRow = -1, int savedCol = -1)
    {
        List<Coroutine> jumps = new List<Coroutine>();
        if(players == null) yield break;
        
        Cell selectedCell = Cell.selectedCell;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                Vector3 targetPosition;
                
                if (i == controlledPlayerIndex && savedRow != -1 && savedCol != -1)
                {
                    // JOUEUR CONTRÔLÉ : saute sur la position SAUVEGARDÉE
                    targetPosition = gridManager.GetCellWorldPosition(savedRow, savedCol);
                    Debug.Log($"Joueur {i+1} saute sur ({savedRow},{savedCol})");
                }
                else
                {
                    // AUTRES JOUEURS : sautent sur place
                    targetPosition = players[i].transform.position;
                }
                
                Coroutine jump = StartCoroutine(JumpToPosition(players[i], targetPosition));
                jumps.Add(jump);
            }
        }
        
        foreach (var jump in jumps) yield return jump;
        
        // Après le saut, DÉSÉLECTIONNER la cellule
        if (selectedCell != null)
        {
            selectedCell.Deselect();
        }
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
}