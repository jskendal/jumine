using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    
    // Variables
    private float rowTimer = 0f;
    private float selectionTimer = 0f;
    private bool isSelectionPhase = true;
    private GameObject[] players;
    private int[] playerColumns = { 2, 5, 10, 13 };
    private int controlledPlayerIndex = 0;
    private List<GameObject> highlightedBorders = new List<GameObject>();
    
    void Start()
    {
        //if (mainCamera == null) mainCamera = Camera.main;

        StartCoroutine(StartGameAfterGridReady());
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
            
            if (players[i].GetComponent<SimplePlayer>() == null)
                players[i].AddComponent<SimplePlayer>();
            
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
        gridManager.InsertRow();

        // 3. Faire sauter les joueurs
        StartCoroutine(MoveAllPlayers(savedRow, savedCol));
        
        // 4. APRÈS le jump, démarrer nouvelle phase
        StartCoroutine(StartNewPhaseAfterJump());
    }

    IEnumerator StartNewPhaseAfterJump()
    {
        // Attendre la fin du jump
        yield return new WaitForSeconds(jumpDuration + 0.1f);
        
        // Démarrer nouvelle phase de sélection
        StartSelectionPhase();
    }
    
    IEnumerator MoveAllPlayers(int savedRow = -1, int savedCol = -1)
    {
        List<Coroutine> jumps = new List<Coroutine>();
        if(players == null) yield break;
        
        Cell selectedCell = Cell.selectedCell;

        // foreach (var player in players)
        // {
        //     if (player != null)
        //     {
        //         Coroutine jump = StartCoroutine(JumpToPosition(player, player.transform.position));
        //         jumps.Add(jump);
        //     }
        // }
        // foreach (var jump in jumps) yield return jump;

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
}