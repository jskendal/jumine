using UnityEngine;

public class Cell : MonoBehaviour
{
    [Header("Type de case")]
    public bool isBonus = false;
    public bool isMalus = false;
    
    [Header("Matériaux")]
    public Material normalMaterial;
    public Material bonusMaterial;
    public Material malusMaterial;
    
    public static Cell selectedCell = null;

    public int row, col;
    
    private GridManager gridManager;

    void Start()
    {
        //Debug.Log($"Cell {gameObject.name} - row={row}, col={col}, position={transform.position}");
        gridManager = FindObjectOfType<GridManager>();
        // Assigner une couleur aléatoire pour tester
        SetupRandomType();
    }
    
    void SetupRandomType()
    {
        float chance = Random.Range(0f, 1f);
        
        Renderer rend = GetComponent<Renderer>();
        
        if (rend != null)
        {
            if (chance < 0.1f) // 10% bonus
            {
                rend.material = bonusMaterial != null ? bonusMaterial : CreateColorMaterial(Color.green);
                isBonus = true;
            }
            else if (chance < 0.2f) // 10% malus
            {
                rend.material = malusMaterial != null ? malusMaterial : CreateColorMaterial(Color.red);
                isMalus = true;
            }
            else // 80% normal
            {
                rend.material = normalMaterial != null ? normalMaterial : CreateColorMaterial(Color.white);
            }
        }
    }
    
    Material CreateColorMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        return mat;
    }
    
    void OnMouseDown()
    {
        
        // Pour tester : faire clignoter la case quand cliquée
        // StartCoroutine(FlashCell());
        
        // Debug.Log($"Case {name} cliquée!");
        if (gridManager == null) return;
        
        // Vérifie DIRECTEMENT dans GridManager si cette cellule est sélectionnable
        if (gridManager.IsCellSelectable(row, col))
        {
            Debug.Log($"Cellule ({row},{col}) cliquée - SÉLECTIONNABLE!");
            
            // Désélectionne l'ancienne
            if (selectedCell != null && selectedCell != this)
            {
                selectedCell.Deselect();
            }
            
            // Sélectionne celle-ci
            Select();
        }
        else
        {
            Debug.Log($"Cellule ({row},{col}) NON sélectionnable - ignoré");
            // RIEN
        }
    }
    
    public void Select()
    {
        selectedCell = this;
        Debug.Log($"SELECT: {name} (row={row}, col={col}) sélectionnée");
        // Trouve la bordure par son nom
        GameObject border = GameObject.Find($"Border_Selectable_{row}_{col}");
        if (border != null)
        {
            border.name = $"Border_Selected_{row}_{col}";
            Renderer renderer = border.GetComponent<Renderer>();
            renderer.material.color = new Color(1, 1, 0, 0.5f); // Jaune
        }
    }

    public void Deselect()
    {
        GameObject border = GameObject.Find($"Border_Selected_{row}_{col}");
        if (border != null)
        {
            border.name = $"Border_Selectable_{row}_{col}";
            Renderer renderer = border.GetComponent<Renderer>();
            renderer.material.color = new Color(0, 1, 1, 0.3f); // Cyan
        }
        
        if (selectedCell == this)
        {
            selectedCell = null;
        }
    }

    System.Collections.IEnumerator FlashCell()
    {
        Renderer rend = GetComponent<Renderer>();
        Color originalColor = rend.material.color;
        
        rend.material.color = Color.yellow;
        yield return new WaitForSeconds(0.2f);
        rend.material.color = originalColor;
    }
}