using UnityEngine;
using System.Collections.Generic;

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
        GenerateGrid();
        CenterGrid();
        isGridReady = true;
        GenerateFutureRow();
    }
    
    public bool IsGridReady()
    {
        return isGridReady;
    }

    void GenerateGrid()
    {
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
                
                AssignRandomEffectToCell(cellScript, r, c);
            }
        }
    }
    
    void AssignRandomEffectToCell(Cell cellScript, int r, int c)
    {
                // Ligne de spawn → TOUJOURS neutre
        if (r == 0 || (r == 1 && !hasDoneFirstTurn))
        {
            cellScript.currentEffect = new CellEffect 
            { 
                type = EffectType.Neutral, 
                value = 0, 
                duration = 0, 
                isWeapon = false 
            };
            cellScript.SetVisual(Color.white, 1f);
            return;
        }

        float chance = Random.Range(0f, 1f);

        if (chance < 0.05f) // Missile
        {
            cellScript.currentEffect = new CellEffect 
            { 
                type = EffectType.Missile, 
                value = 30, 
                duration = 0, 
                isWeapon = true 
            };
            cellScript.SetVisual(Color.yellow, 0.8f);
        }
        else if (chance < 0.15f) // Poison
        {
            cellScript.currentEffect = new CellEffect 
            { 
                type = EffectType.Poison, 
                value = 10,   // dégâts par tour
                duration = 3, // 3 tours
                isWeapon = false 
            };
            cellScript.SetVisual(Color.magenta, 0.8f);
        }
        else if (chance < 0.25f) // Bombe
        {
            cellScript.currentEffect = new CellEffect 
            { 
                type = EffectType.DamageBomb, 
                value = 30, 
                duration = 0, 
                isWeapon = false 
            };
            cellScript.SetVisual(Color.red, 1f);
        }
        else if (chance < 0.35f) // Potion
        {
            cellScript.currentEffect = new CellEffect 
            { 
                type = EffectType.HealthPotion, 
                value = 30, 
                duration = 0, 
                isWeapon = false 
            };
            cellScript.SetVisual(Color.green, 1f);
        }
        else
        {
            cellScript.currentEffect = new CellEffect 
            { 
                type = EffectType.Neutral, 
                value = 0, 
                duration = 0, 
                isWeapon = false 
            };
            cellScript.SetVisual(Color.white, 1f);
        }
    }

    void GenerateFutureRow()
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

            AssignRandomEffectToCell(cellScript, -1, c); 
            Color baseColor = cellScript.GetComponent<Renderer>().material.color;
            cellScript.SetVisual(baseColor, 0.5f); // <-- C'est ici qu'on la rend transparente      
            
            futureRow[c] = cell;
        }
    }
    
    public void CenterGrid()
    {
        float totalWidth = (columns - 1) * cellSize;
        float totalHeight = (rows - 1) * cellSize;
        transform.position = new Vector3(-totalWidth / 2f, 0f, -totalHeight / 2f);
    }
    
    public void InsertRow()
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

            AssignRandomEffectToCell(cellScript, topRow, c);

            // Rendre opaque
            Color baseColor = cellScript.GetComponent<Renderer>().material.color;
            cellScript.SetVisual(baseColor, 1.0f); 
        }
        
        // 4. Générer une nouvelle future ligne
        GenerateFutureRow();
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


    public enum CellType
    {
        Neutral,
        HealthPotion,   // Vert (+ vie)
        DamageBomb      // Rouge (- vie)
        // On ajoutera plus tard : Missile, Nuclear, SpeedBoost, etc.
    }

    public CellType GetCellType(int row, int col)
    {
        if (row < 0 || row >= rows || col < 0 || col >= columns) 
            return CellType.Neutral;
        
        if (gridCells[row, col] == null) 
            return CellType.Neutral;
        
        Cell cellScript = gridCells[row, col].GetComponent<Cell>();
        if (cellScript == null) 
            return CellType.Neutral;
        
        // UTILISE TES BOOLS EXISTANTS
        if (cellScript.isBonus)
            return CellType.HealthPotion; // Vert = bonus santé
        else if (cellScript.isMalus)
            return CellType.DamageBomb;   // Rouge = dégâts
        else
            return CellType.Neutral;
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
}