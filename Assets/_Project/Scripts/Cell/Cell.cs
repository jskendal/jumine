using UnityEngine;

public class Cell : MonoBehaviour
{ 
    [Header("Données de la Case")]
    public CellEffect currentEffect; 
    
    
    public Renderer backgroundRenderer; // Le renderer de la case de fond
    public SpriteRenderer iconRenderer;       // Le renderer de l'icône (le Quad)
    //public static Cell selectedCell = null;

    public int row, col;
    
    private GridManager gridManager;
    private Renderer myRenderer;

    void Awake()
    {
        myRenderer = GetComponent<Renderer>();
        //Debug.Log($"Cell {gameObject.name} - row={row}, col={col}, position={transform.position}");
        gridManager = FindObjectOfType<GridManager>();
        // Assigner une couleur aléatoire pour tester
        //SetupRandomType();

        if (backgroundRenderer == null) backgroundRenderer = GetComponent<Renderer>();
        if (iconRenderer == null) iconRenderer = transform.Find("Icon")?.GetComponent<SpriteRenderer>();

        if (iconRenderer == null) {
            Debug.LogWarning("Aucun Renderer enfant nommé 'Icon' trouvé sur la cellule.");
        }
    }
    
        // 🔥 C'est ici que tu vas ajouter une nouvelle fonction temporaire SetIcon() 🔥
    public void SetIcon(Sprite spriteToDisplay)
    {
        if (iconRenderer != null)
        {
            iconRenderer.sprite = spriteToDisplay;
            iconRenderer.gameObject.SetActive(spriteToDisplay != null); // Cache l'icône si pas de sprite
        } else {
            Debug.LogWarning($"Cell ({name}): iconRenderer est NULL. L'icône ne peut pas être affichée.");
        }
    }
    
    public void SetVisual(Color color, float alpha = 1f)
    {
        if (myRenderer == null) myRenderer = GetComponent<Renderer>();
        if (myRenderer == null) return;

        Material mat = new Material(myRenderer.material);
        mat.color = new Color(color.r, color.g, color.b, alpha);
        
        // Gérer le mode du shader (Opaque vs Transparent)
        if (alpha < 1.0f)
        {
            // Mode TRANSPARENT
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        else
        {
            // Mode OPAQUE (très important de le remettre)
            mat.SetFloat("_Mode", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = -1; // Default
        }

        // Appliquer le material configuré
        myRenderer.material = mat;
    }

    void OnMouseDown()
    {
        if (gridManager == null) return;
        
        // Vérifie DIRECTEMENT dans GridManager si cette cellule est sélectionnable
        if (gridManager.IsCellSelectable(row, col))
        {
                   // Désélectionner l'ancienne cellule visuellement
            GameObject oldSelectedBorder = GameObject.FindWithTag("SelectedBorder");
            if (oldSelectedBorder != null)
            {
                // On enlève le tag pour ne plus la considérer comme "sélectionnée"
                oldSelectedBorder.tag = "Untagged";
                
                // On la remet en cyan
                oldSelectedBorder.GetComponent<Renderer>().material.color = new Color(0, 1, 1, 0.3f);
        
            }

            // Enregistrer la cible dans GameManager
            GameManager gm = FindObjectOfType<GameManager>();
            gm.SetPlayerTarget(gm.localPlayerID, row, col); // Joueur 0 = humain

            // Sélectionner visuellement la nouvelle cellule
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

        GameObject border = GameObject.Find($"Border_Selectable_{row}_{col}");
        if (border != null)
        {
            border.name = $"Border_Selected_{row}_{col}";
            border.tag = "SelectedBorder"; // Optionnel pour le nettoyage
            border.GetComponent<Renderer>().material.color = new Color(1, 1, 0, 0.5f);
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
    }
}