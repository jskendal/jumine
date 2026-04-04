using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public int playerID;
    public int health = 100;
    public int maxHealth = 100;
    
    // Bonus/Malus
    public float speedMultiplier = 1f;
    public bool isInvincible = false;
    public bool hasArmor = false;
    public int armorTurns = 0;
    public bool isFrozen = false;
    private bool isPoisoned = false;
    public Color originalColor;
    
    public int poisonTurnsRemaining = 0;
    public int poisonDamagePerTurn = 0;

    // Timer d'effets
    private float effectTimer = 0f;
    //private BonusMalusType currentEffect = BonusMalusType.HealthPotion;
 
    public GridManager gridManager;

    [Header("Références")]
    public GameManager gameManager;

    void Start()
    {
        // CreateHealthBar();
        // UpdateHealthUI();
        gameManager = FindObjectOfType<GameManager>();
    }
    void Awake()
    {
        gridManager = FindObjectOfType<GridManager>();
    }
    
    void Update()
    {
        // Gestion des effets temporaires
        if (effectTimer > 0)
        {
            effectTimer -= Time.deltaTime;
            if (effectTimer <= 0)
            {
                //ResetEffect(currentEffect);
            }
        }
    }


    // void OnMouseDown()
    // {
    //     // Sécurité : clamp les coordonnées
    //     Vector2Int playerGridPos = gridManager.GetCellFromWorldPosition(transform.position);
    //     playerGridPos.x = Mathf.Clamp(playerGridPos.x, 0, gridManager.gridCells.GetLength(0) - 1);
    //     playerGridPos.y = Mathf.Clamp(playerGridPos.y, 0, gridManager.gridCells.GetLength(1) - 1);
        
    //     if (playerGridPos.x >= 0 && playerGridPos.y >= 0 &&
    //         gridManager.gridCells[playerGridPos.x, playerGridPos.y] != null)
    //     {
    //         Cell targetCell = gridManager.gridCells[playerGridPos.x, playerGridPos.y].GetComponent<Cell>();
    //         targetCell?.HandleClick();
    //     }
    // }

    // public bool HasActiveEffects()
    // {
    //     // Vérifie si le joueur a des effets actifs
    //     return isPoisoned || isFrozen || speedMultiplier != 1f || isInvincible;
    // }
}