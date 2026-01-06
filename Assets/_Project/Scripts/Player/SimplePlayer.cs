using UnityEngine;
using UnityEngine.UI;


public class PlayerEffect
{
    public GameObject player;
    public BonusMalusType type;
    
    public PlayerEffect(GameObject player, BonusMalusType type)
    {
        this.player = player;
        this.type = type;
    }
}

public class Player : MonoBehaviour
{
    public int playerID;
    public int health = 100;
    public int maxHealth = 100;
    
    // Bonus/Malus
    public float speedMultiplier = 1f;
    public bool isInvincible = false;
    private bool isFrozen = false;
    private bool isPoisoned = false;
    private float poisonTimer = 0f;
    
    // Timer d'effets
    private float effectTimer = 0f;
    private BonusMalusType currentEffect = BonusMalusType.HealthPotion;
 
    private GameObject healthBarCanvas;
    private GameObject healthBarBackground;
    private GameObject healthBarFill;

    [Header("Références")]
    public GameManager gameManager;

    void Start()
    {
        // CreateHealthBar();
        // UpdateHealthUI();
        gameManager = FindObjectOfType<GameManager>();
    }
    
    void Update()
    {
        // Gestion des effets temporaires
        if (effectTimer > 0)
        {
            effectTimer -= Time.deltaTime;
            if (effectTimer <= 0)
            {
                ResetEffect(currentEffect);
            }
        }
        
        // Poison : dégâts continus
        if (isPoisoned)
        {
            poisonTimer -= Time.deltaTime;
            if (poisonTimer <= 0)
            {
                isPoisoned = false;
            }
            else if (Time.frameCount % 30 == 0) // Dégât toutes les 0.5 sec environ
            {
                TakeDamage(10);
            }
        }
    }
 

    public bool HasActiveEffects()
    {
        // Vérifie si le joueur a des effets actifs
        return isPoisoned || isFrozen || speedMultiplier != 1f || isInvincible;
    }
    
    public void ApplyEffect(BonusMalusType type, int value, float duration)
    {
        Debug.Log($"Player {playerID} got {type} (value:{value}, dur:{duration}s)");
        
        currentEffect = type;
        effectTimer = duration;
        
        switch (type)
        {
            case BonusMalusType.HealthPotion:
                Heal(value);
                break;
                
            case BonusMalusType.SpeedBoost:
                speedMultiplier = 1.5f;
                break;
                
            case BonusMalusType.Invincibility:
                isInvincible = true;
                break;
                
            case BonusMalusType.Poison:
                isPoisoned = true;
                poisonTimer = duration;
                break;
                
            case BonusMalusType.Shrink:
                transform.localScale = Vector3.one * 0.5f;
                break;
                
            case BonusMalusType.Freeze:
                isFrozen = true;
                break;
        }
    }

    void ResetEffect(BonusMalusType type)
    {
        switch (type)
        {
            case BonusMalusType.SpeedBoost:
                speedMultiplier = 1f;
                break;
                
            case BonusMalusType.Invincibility:
                isInvincible = false;
                break;
                
            case BonusMalusType.Shrink:
                transform.localScale = Vector3.one;
                break;
                
            case BonusMalusType.Freeze:
                isFrozen = false;
                break;
        }
    }

    public void TakeDamage(int damage)
    {
        if (isInvincible) return;
        
        health -= damage;
        health = Mathf.Max(health, 0);
        //UpdateHealthUI();

        Debug.Log($"Player {playerID} took {damage} damage. Health: {health}");
        
        gameManager.UpdatePlayerHealthBar(playerID, health);
        if (health <= 0)
        {
            Die();
        }
    }
    
    public void Heal(int amount)
    {
        health += amount;
        health = Mathf.Min(health, maxHealth);
        gameManager.UpdatePlayerHealthBar(playerID, health);
        Debug.Log($"Player {playerID} healed {amount}. Health: {health}");
    }
    
    public void ApplySpeedBoost(float multiplier, float duration)
    {
        speedMultiplier = multiplier;
        Invoke(nameof(ResetSpeed), duration);
    }
    
    private void ResetSpeed()
    {
        speedMultiplier = 1f;
    }
    
    void Die()
    {
        Debug.Log($"Player {playerID} died!");
        if (healthBarCanvas != null)
            healthBarCanvas.SetActive(false);
        gameObject.SetActive(false);
    }
    
    void OnDestroy()
    {
        if (healthBarCanvas != null)
            Destroy(healthBarCanvas);
    }
}
    public class Billboard : MonoBehaviour
    {
        void Update()
        {
            if (Camera.main != null)
            {
                // Toujours face à la caméra
                transform.rotation = Camera.main.transform.rotation;
            }
        }
    }