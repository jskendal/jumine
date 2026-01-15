using UnityEngine;
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
    
    private GameObject[,] gridCells;
    private GameObject[] futureRow;
    private bool isGridReady = false;
    
    private HashSet<Vector2Int> selectableCells = new HashSet<Vector2Int>();
    private bool hasDoneFirstTurn = false;

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
        rows = state.Rows;
        columns = state.Cols;
        gridCells = new GameObject[rows, columns];
        
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 position = new Vector3(c * cellSize, 0f, r * cellSize);
                GameObject cell = Instantiate(cellPrefab, position, Quaternion.identity, transform);
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

    public void GenerateFutureRow(CellEffect[] futureData)
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
            Color baseColor = cellScript.GetComponent<Renderer>().material.color;
            cellScript.SetVisual(baseColor, 0.5f); // <-- C'est ici qu'on la rend transparente      
            
            futureRow[c] = cell;
        }
    }

    // Permet de récupérer le script Cell d'une coordonnée précise
    public Cell GetCellScript(int r, int c)
    {
        if (gridCells != null && gridCells[r, c] != null)
            return gridCells[r, c].GetComponent<Cell>();
        return null;
    }

    public void CenterGrid()
    {
        float totalWidth = (columns - 1) * cellSize;
        float totalHeight = (rows - 1) * cellSize;
        transform.position = new Vector3(-totalWidth / 2f, 0f, -totalHeight / 2f);
    }
    
    public void InsertRow(CellEffect[] newRowData, CellEffect[] newFutureRowData)
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
                ;
                gridCells[r - 1, c] = gridCells[r, c];
                Vector3 newPos = new Vector3(c * cellSize, 0f, (r - 1) * cellSize);
                gridCells[r, c].transform.localPosition = newPos;
                gridCells[r, c].name = $"Cell_{r-1}_{c}";
             
                Cell cellScript = gridCells[r, c].GetComponent<Cell>();
                cellScript.row = r - 1;  // ← NOUVEAU row
                cellScript.col = c;

 
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

            // Rendre opaque
            Color baseColor = cellScript.GetComponent<Renderer>().material.color;
            cellScript.SetVisual(baseColor, 1.0f); 
        }
        
        // 4. Générer une nouvelle future ligne
        GenerateFutureRow(newFutureRowData);
        hasDoneFirstTurn = true;
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
        Material mat = new Material(Shader.Find("Standard"));
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


    // public enum CellType
    // {
    //     Neutral,
    //     HealthPotion,   // Vert (+ vie)
    //     DamageBomb      // Rouge (- vie)
    //     // On ajoutera plus tard : Missile, Nuclear, SpeedBoost, etc.
    // }

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
}