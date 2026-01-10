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
    private float poisonTimer = 0f;
    
    public int poisonTurnsRemaining = 0;
    public int poisonDamagePerTurn = 0;

    // Timer d'effets
    private float effectTimer = 0f;
    private BonusMalusType currentEffect = BonusMalusType.HealthPotion;
 
    private GameObject healthBarCanvas;
 
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
        
    // 🔥 FIX 3 : Commente ce tick automatique qui tue tout en temps réel
        // Le poison sera appliqué manuellement à la fin de chaque tour plus tard
        /*
        if (isPoisoned)
        {
            poisonTimer -= Time.deltaTime;
            if (poisonTimer <= 0)
            {
                isPoisoned = false;
            }
            else if (Time.frameCount % 30 == 0) // Dégât toutes les 0.5 sec environ
            {
                TakeDamage(10); // <-- ÇA C'EST LE PROBLÈME !
            }
        }
        */
    }
 

    public bool HasActiveEffects()
    {
        // Vérifie si le joueur a des effets actifs
        return isPoisoned || isFrozen || speedMultiplier != 1f || isInvincible;
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
            Debug.Log($"[TakeDamage] Tentative de dégâts sur PlayerID: {this.playerID}. GO: {this.gameObject.name}");
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
        // 🔥 Guérison du poison
        poisonTurnsRemaining = 0;
        gameManager.UpdatePlayerHealthBar(playerID, health);
        Debug.Log($"Player {playerID} healed {amount}. Health: {health}");
    }
    
    public void ApplyPoison(int turns, int damagePerTurn)
    {
        poisonTurnsRemaining = turns;
        poisonDamagePerTurn = damagePerTurn;
        Debug.Log($"Player {playerID} empoisonné pour {turns} tours ({damagePerTurn} dmg/tour)");
    }

// Appelé à la fin de CHAQUE tour par le GameManager
    public void ProcessTurnEndEffects()
    {
        if (poisonTurnsRemaining > 0)
        {
            TakeDamage(poisonDamagePerTurn);
            poisonTurnsRemaining--;
            Debug.Log($"Poison tick on Player {playerID}. Remaining turns: {poisonTurnsRemaining}");
        }
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