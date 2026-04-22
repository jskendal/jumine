using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


public class GridManager : MonoBehaviour
{
    [Header("Paramètres")]
    public int rows = 6;
    public int columns = 20;
    public float cellSize = 1.2f;
    
    [Header("Prefabs")]
    public GameObject cellPrefab;
    
    public GameObject[,] gridCells;
    private GameObject[] futureRow;
    private bool isGridReady = false;
    
    private HashSet<Vector2Int> selectableCells = new HashSet<Vector2Int>();

    // Variable pour stocker l'état des effets
    private bool _areCellEffectsHidden = false;


    void Start()
    {
        foreach (Transform child in transform)
        {
            if(child.name.StartsWith("Cell_") || child.name.StartsWith("FutureCell_"))
                Destroy(child.gameObject);
        }
 
        CenterGrid();
        isGridReady = true;
    }
    
    public bool IsGridReady()
    {
        return isGridReady;
    }

    public void GenerateGrid(GameState state)
    {
        Debug.Log("=== GRID GENERATING ===");
        rows = state.Rows;
        columns = state.Cols;
        gridCells = new GameObject[rows, columns];
        
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 position = new Vector3(c * cellSize, 0f, r * cellSize);
                GameObject cell = Instantiate(cellPrefab, position, Quaternion.identity, transform);
                cell.transform.localPosition = new Vector3(c * cellSize, 0f, r * cellSize);
                Cell cellScript = cell.GetComponent<Cell>();
                cellScript.row = r;  // r = row (0 à rows-1)
                cellScript.col = c;  // c = col (0 à columns-1)
                cell.name = $"Cell_{r}_{c}";
                gridCells[r, c] = cell;
                
                ApplyEffectToCell(cellScript, state.Grid[r, c]);
            }
        }
    }
    
    public CellEffectData[] effectVisuals;

    // 1. On crée une fonction qui traduit la donnée "Moteur" en visuel "Unity"
    public void ApplyEffectToCell(Cell cellScript, CellEffect data)
    {
        if (cellScript == null) return;

        // 1. Met à jour la logique interne de la cellule
        cellScript.currentEffect = new CellEffect {
            type = data.type,
            value = data.value,
            isWeapon = data.isWeapon
        };
        
        // 2. Trouve la configuration visuelle correspondante à cet effet
        CellEffectData visualConfig = effectVisuals.FirstOrDefault(x => x.effectType == data.type);
        if (visualConfig == null)
        {
            Debug.LogWarning($"Visual data for effect {data.type} not found. Using Neutral fallback.");
            visualConfig = effectVisuals.FirstOrDefault(x => x.effectType == EffectType.Neutral);
            if (visualConfig == null)
            {
                Debug.LogError("FATAL ERROR: Neutral visual data not found.");
                return;
            }
        }

        // 3. Applique la couleur ET l'alpha DEPUIS LE SCRIPTABLEOBJECT
        float alpha = (cellScript.row == -1) ? visualConfig.futureRowAlpha : 1.0f;
        cellScript.SetVisual(visualConfig.backgroundColor, alpha);

        // 4. Applique l'icône DEPUIS LE SCRIPTABLEOBJECT
        cellScript.SetIcon(visualConfig);
    }

    public void ForceCellVisual(int row, int col, EffectType newType)
    {
        // Récupérer la cellule (tu dois avoir un tableau gridCells ou GetCell() existant)
        if (row < 0 || row >= rows || col < 0 || col >= columns) return;
        
        GameObject cellObj = gridCells[row, col];
        if (cellObj == null) return;

        Cell cellScript = cellObj.GetComponent<Cell>();
        if (cellScript == null) return;

        // Trouver la config visuelle dans ton tableau effectVisuals
        CellEffectData visualConfig = effectVisuals.FirstOrDefault(x => x.effectType == newType);
        if (visualConfig == null) return;

        // Appliquer le visuel via le script Cell
        cellScript.SetIcon(visualConfig);
        cellScript.SetVisual(visualConfig.backgroundColor, 1f);
        
        // Optionnel : Mettre à jour le type stocké dans la cellule
        cellScript.currentEffect.type = newType;
    }

    public void GenerateFutureRow(CellEffect[] futureData, Dictionary<int, bool> playerSightDisabled = null)
    {
        futureRow = new GameObject[columns];
        
        for (int c = 0; c < columns; c++)
        {
            GameObject cell = Instantiate(cellPrefab, Vector3.zero, Quaternion.identity, transform);
            cell.name = $"FutureCell_{c}";
            Cell cellScript = cell.GetComponent<Cell>();

            cellScript.row = -1;
            cellScript.col = c;
            cell.transform.localPosition = new Vector3(
                c * cellSize,
                0f,
                rows * cellSize + 0.5f
            );

            ApplyEffectToCell(cellScript, futureData[c]);

            //ApplySightDisabledEffect(cellScript, playerSightDisabled);

            Color baseColor = cellScript.GetComponent<Renderer>().material.color;
            cellScript.SetVisual(baseColor, 0.5f); // <-- C'est ici qu'on la rend transparente      
            
            futureRow[c] = cell;
        }
    }

    private Dictionary<Cell, (bool iconActive, Color iconColor)> originalIconStates = new Dictionary<Cell, (bool, Color)>();
    private Dictionary<Cell, Color> originalBackgroundColors = new Dictionary<Cell, Color>();

    public void CenterGrid()
    {
        float totalWidth = (columns - 1) * cellSize;
        float totalHeight = (rows - 1) * cellSize;
        transform.position = new Vector3(-totalWidth / 2f, 0f, -totalHeight / 2f);
    }
    
    public void InsertRow(CellEffect[] newRowData, CellEffect[] newFutureRowData, Dictionary<int, bool> playerSightDisabled = null)
    {
        // 1. Détruire la ligne du bas
        for (int c = 0; c < columns; c++)
        {
            if(gridCells != null && gridCells[0, c] != null)
                Destroy(gridCells[0, c]);
        }
        
        // 2. Décaler toutes les lignes vers le bas
        for (int r = 1; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                if(gridCells == null)
                {
                    Debug.LogError("gridCells is null!");
                    return;
                }
            
                    gridCells[r - 1, c] = gridCells[r, c];
                    Vector3 newPos = new Vector3(c * cellSize, 0f, (r - 1) * cellSize);
                    gridCells[r, c].transform.localPosition = newPos;
                    gridCells[r, c].name = $"Cell_{r-1}_{c}";
                
                    Cell cellScript = gridCells[r, c].GetComponent<Cell>();
                    cellScript.row = r - 1;  // ← NOUVEAU row
                    cellScript.col = c;
                    int localPlayerId = FindObjectOfType<GameManager>().localPlayerID;
                    // if (playerSightDisabled != null && playerSightDisabled.ContainsKey(localPlayerId) && playerSightDisabled[localPlayerId])
                    // {
                    //     ApplySightDisabledEffect(cellScript, playerSightDisabled);
                    // }
            }
        }
        
        // 3. Insérer la future ligne en haut
        int topRow = rows - 1;
        for (int c = 0; c < columns; c++)
        {
            gridCells[topRow, c] = futureRow[c];
            Vector3 newPos = new Vector3(c * cellSize, 0f, topRow * cellSize);
            futureRow[c].transform.localPosition = newPos;
            futureRow[c].name = $"Cell_{topRow}_{c}";
            
            Cell cellScript = futureRow[c].GetComponent<Cell>();
            cellScript.row = topRow;  // ← row = 5
            cellScript.col = c;       // col = c

            ApplyEffectToCell(cellScript, newRowData[c]);

            //ApplySightDisabledEffect(cellScript, playerSightDisabled);

            // Rendre opaque
            Color baseColor = cellScript.GetComponent<Renderer>().material.color;
            cellScript.SetVisual(baseColor, 1.0f); 
        }
        
        // 4. Générer une nouvelle future ligne
        GenerateFutureRow(newFutureRowData, playerSightDisabled);

        //should I enable or disable with allCells ? or use directly FlipCellsAnimation fct ?
        //FlipCellsAnimation(false);
        // List<Cell> allCells = new List<Cell>();
        // for (int r = 0; r < rows; r++)
        // {
        //     for (int c = 0; c < columns; c++)
        //     {
        //         if (gridCells[r, c] != null)
        //             allCells.Add(gridCells[r, c].GetComponent<Cell>());
        //     }
        // }
        // foreach (Transform child in transform)
        // {
        //     Cell cellScript = child.GetComponent<Cell>();
        //     if (cellScript != null && cellScript.row == -1)
        //         allCells.Add(cellScript);
        // }

        //FlipCellsAnimation(true);
    }
    
    public Vector3 GetCellWorldPosition(int row, int col)
    {
        if (row >= 0 && row < rows && col >= 0 && col < columns)
        {
            // Position LOCALE dans le grid
            Vector3 localPos = new Vector3(col * cellSize, 0.5f, row * cellSize);
            
            // Convertir en position MONDE
            Vector3 worldPos = transform.TransformPoint(localPos);
            
            //Debug.Log($"GetCellWorldPosition({row},{col}): local={localPos}, world={worldPos}, gridPos={transform.position}");
            
            return worldPos;
        }
        
        return Vector3.zero;
    }
 
    public Vector2Int GetCellFromWorldPosition(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int col = Mathf.RoundToInt(localPos.x / cellSize);
        int row = Mathf.RoundToInt(localPos.z / cellSize);
        col = Mathf.Clamp(col, 0, columns - 1);
        row = Mathf.Clamp(row, 0, rows - 1);
        return new Vector2Int(row, col);
    }
    
    public List<Vector2Int> GetCellsInRadius(Vector2Int centerCell, int radius)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        
        for (int r = -radius; r <= radius; r++)
        {
            for (int c = -radius; c <= radius; c++)
            {
                if (Mathf.Abs(r) + Mathf.Abs(c) <= radius)
                {
                    Vector2Int cell = new Vector2Int(centerCell.x + r, centerCell.y + c);
                    if (cell.x >= 0 && cell.x < rows && cell.y >= 0 && cell.y < columns)
                    {
                        cells.Add(cell);
                    }
                }
            }
        }
        return cells;
    }
    
    public GameObject ShowCellAsSelectable(int row, int col)
    {
        Vector3 cellPos = GetCellWorldPosition(row, col);
        cellPos.y = 0.05f; // Très peu au-dessus
        
        // Créer un CUBE PLEIN cyan transparent
        GameObject border = GameObject.CreatePrimitive(PrimitiveType.Cube);
        
        border.transform.position = cellPos;
        border.transform.localScale = new Vector3(cellSize * 0.95f, 0.02f, cellSize * 0.95f);
        
        // Material semi-transparent CYAN
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(0, 1, 1, 0.3f); // Cyan transparent
        
        // Configurer pour la transparence
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        
        border.GetComponent<Renderer>().material = mat;
        Destroy(border.GetComponent<Collider>());

        border.name = $"Border_Selectable_{row}_{col}";
        selectableCells.Add(new Vector2Int(row, col));
        return border;
    }

    public bool IsCellSelectable(int row, int col)
    {
        return selectableCells.Contains(new Vector2Int(row, col));
    }
    
    // Quand tu nettoies les bordures
    public void ClearSelectableCells()
    {
        selectableCells.Clear();
    }
 
    public CellEffect GetCellEffect(int row, int col)
    {
        if (row < 0 || row >= rows || col < 0 || col >= columns) 
            return new CellEffect { type = EffectType.Neutral };
        
        if (gridCells[row, col] != null)
        {
            Cell c = gridCells[row, col].GetComponent<Cell>();
            if (c != null) return c.currentEffect;
        }
        return new CellEffect { type = EffectType.Neutral };
    }

    // Fonction pour masquer les effets (avec animation)
    public void HideCellEffects(bool rotation = true)
    {
        if (_areCellEffectsHidden) return; // Déjà masqué

        _areCellEffectsHidden = true;
        StartCoroutine(FlipCellsAnimation(true, rotation)); // true = masquer
    }

    // Fonction pour afficher les effets (avec animation)
    public void ShowCellEffects(bool rotation = true)
    {
        if (!_areCellEffectsHidden) return; // Déjà visible

        _areCellEffectsHidden = false;
        StartCoroutine(FlipCellsAnimation(false, rotation)); // false = afficher
        originalBackgroundColors.Clear();
        originalIconStates.Clear();
    }

    
    private void ApplySightDisabledEffect(Cell cellScript, Dictionary<int, bool> playerSightDisabled)
    {
        if (playerSightDisabled == null || playerSightDisabled.Count(x=>x.Value == true) == 0) return;

        int localPlayerId = FindObjectOfType<GameManager>().localPlayerID;
        bool shouldHide = playerSightDisabled.ContainsKey(localPlayerId) && playerSightDisabled[localPlayerId];

        if (cellScript.iconRenderer == null) return;

        if (shouldHide)
        {
            // 1. Stocker l'état original de l'icône (si ce n'est pas déjà fait)
            if (!originalIconStates.ContainsKey(cellScript))
            {
                originalIconStates[cellScript] = (
                    cellScript.iconRenderer.gameObject.activeSelf,
                    cellScript.iconRenderer.color
                );
            }
            cellScript.SetVisual(new Color(0.3f, 0.3f, 0.3f, 0.5f), 1f);
            // 2. Masquer l'icône (désactiver + transparence)
            cellScript.iconRenderer.gameObject.SetActive(false);
        }
        else
        {
            // 3. Restaurer l'état original de l'icône
            if (originalIconStates.TryGetValue(cellScript, out var originalState))
            {
                cellScript.iconRenderer.gameObject.SetActive(originalState.iconActive);
                cellScript.iconRenderer.color = originalState.iconColor;
                originalIconStates.Remove(cellScript); // Nettoyer
            }
        }
    }

    public void MarkCellAsConsumed(int row, int col)
    {
        Cell cellScript = gridCells[row, col].GetComponent<Cell>();

        // ✅ Appliquer un gris semi-transparent par-dessus la couleur et l'icône
        // (Tu peux ajuster l'alpha pour plus ou moins de transparence)
        Color grayOverlay = new Color(0.5f, 0.5f, 0.5f, 0.6f); // Gris semi-transparent
        cellScript.SetVisual(cellScript.backgroundRenderer.material.color * grayOverlay, 1f); // Fond

        // ✅ Désactiver l'icône de l'effet (optionnel, selon ton design)
        if (cellScript.iconRenderer != null)
        {
            cellScript.iconRenderer.color = cellScript.iconRenderer.color * grayOverlay;
        }
    }

    // Animation de retournement des cases
    public IEnumerator FlipCellsAnimation(bool hide, bool rotation = true)
    {
        float duration = 0.3f;
        float timer = 0f;

        // Stocker les couleurs initiales des icônes pour les restaurer après
        List<GameObject> futureRowCells = new List<GameObject>();

        // 1. Trouver les futures rows
        foreach (Transform child in transform)
        {
            Cell cellScript = child.GetComponent<Cell>();
            if (cellScript != null && cellScript.row == -1)
            {
                futureRowCells.Add(child.gameObject);
            }
        }

        List<Cell> allCells = new List<Cell>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                if (gridCells[r, c] != null)
                    allCells.Add(gridCells[r, c].GetComponent<Cell>());
            }
        }
        foreach (Transform child in transform)
        {
            Cell cellScript = child.GetComponent<Cell>();
            if (cellScript != null && cellScript.row == -1)
                allCells.Add(cellScript);
        }

        while (timer < duration)
        {
            // 2. Traiter les cellules normales
            foreach (Cell cellScript in allCells)
            {
                if (cellScript == null) continue;

                // Stocker la couleur originale
                if (hide && !originalBackgroundColors.ContainsKey(cellScript))
                {
                    originalBackgroundColors[cellScript] = cellScript.backgroundRenderer.material.color;
                }

                // Masquer avec un noir
                if (hide)
                {
                    cellScript.SetVisual(new Color(0.1f, 0.1f, 0.1f, 1f), 1f);
                }
                else if (originalBackgroundColors.ContainsKey(cellScript))
                {
                    cellScript.SetVisual(originalBackgroundColors[cellScript], 1f);
                }

                // Rotation et icônes (comme avant)
                if(rotation)
                {
                    cellScript.transform.localRotation = Quaternion.Slerp(
                        cellScript.transform.localRotation,
                        Quaternion.Euler(0f, hide ? 180f : 0f, 0f),
                        timer / duration
                    );
                }

                if (hide)
                {
                    cellScript.iconRenderer.gameObject.SetActive(false);
                }
                else
                {
                    cellScript.iconRenderer.gameObject.SetActive(true);
                }

                // Restaurer le fond
                if (originalBackgroundColors.TryGetValue(cellScript, out Color bgColor))
                {
                    cellScript.SetVisual(bgColor, 1f);
                    originalBackgroundColors.Remove(cellScript); // Nettoyer
                }

                // Restaurer les icônes (ANNULE SetActive(false))
                if (originalIconStates.TryGetValue(cellScript, out var originalState))
                {
                    cellScript.iconRenderer.gameObject.SetActive(true); // Force à true
                    cellScript.iconRenderer.color = originalState.iconColor;
                    originalIconStates.Remove(cellScript); // Nettoyer
                }

                // 3. Traiter les futures rows
                if (hide)
                {
                    if (!originalBackgroundColors.ContainsKey(cellScript))
                    {
                        originalBackgroundColors[cellScript] = cellScript.backgroundRenderer.material.color;
                    }
                    cellScript.SetVisual(new Color(0.3f, 0.3f, 0.3f, 0.5f), 1f);
                }
                else if (originalBackgroundColors.ContainsKey(cellScript))
                {
                    cellScript.SetVisual(originalBackgroundColors[cellScript], 1f);
                }

                if(rotation)
                {
                    cellScript.transform.localRotation = Quaternion.Slerp(
                        cellScript.transform.localRotation,
                        Quaternion.Euler(0f, hide ? 180f : 0f, 0f),
                        timer / duration
                    );
                }
                //
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // Forcer les valeurs finales après l'animation
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Cell cellScript = gridCells[r, c].GetComponent<Cell>();
                if (cellScript == null) continue;

                // Rotation finale
                cellScript.transform.localRotation = Quaternion.Euler(0f, hide ? 180f : 0f, 0f);

                // Masquage final de l'icône
                if (originalIconStates.TryGetValue(cellScript, out var originalState))
                {
                    cellScript.iconRenderer.gameObject.SetActive(originalState.iconActive);
                    cellScript.iconRenderer.color = originalState.iconColor;
                    originalIconStates.Remove(cellScript); // Nettoyer
                }

                // Opacité finale du fond
                if (cellScript.backgroundRenderer != null)
                {
                    Color finalBgColor = cellScript.backgroundRenderer.material.color;
                    finalBgColor.a = hide ? 0.5f : 1f;
                    cellScript.backgroundRenderer.material.color = finalBgColor;
                }
            }
        }
    }

}