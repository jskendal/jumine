using UnityEngine;

public enum BonusMalusType
{
    HealthPotion,    // +30 PV
    SpeedBoost,      // x1.5 vitesse
    Invincibility,   // Invincible 3s
    Poison,          // -10 PV/s pendant 3s
    Shrink,          // Taille 50%
    Freeze           // Immobile 2s
}

public class BonusMalus : MonoBehaviour
{
    public BonusMalusType type;
    public int value;          // Ex: +30 pour HealthPotion
    public float duration;     // Ex: 5 secondes
    
    public Color color;        // Pour le rendu visuel
    
    void Start()
    {
        // Donner une couleur selon le type
        switch (type)
        {
            case BonusMalusType.HealthPotion: color = Color.green; break;
            case BonusMalusType.SpeedBoost: color = Color.cyan; break;
            case BonusMalusType.Invincibility: color = Color.white; break;
            case BonusMalusType.Poison: color = Color.magenta; break;
            case BonusMalusType.Shrink: color = Color.yellow; break;
            case BonusMalusType.Freeze: color = Color.blue; break;
        }
        
        GetComponent<Renderer>().material.color = color;
    }
    
    // void OnTriggerEnter(Collider other)
    // {
    //     if (other.CompareTag("Player"))
    //     {
    //         Player player = other.GetComponent<Player>();
    //         if (player != null)
    //         {
    //             player.ApplyEffect(type, value, duration);
    //             Destroy(gameObject);
    //         }
    //     }
    // }
}