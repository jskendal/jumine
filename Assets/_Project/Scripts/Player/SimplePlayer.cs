using UnityEngine;

public class SimplePlayer : MonoBehaviour
{
    private GameManager gameManager;
    private bool isSelected = false;
    
    void Start()
    {
        // Trouver le GameManager
        gameManager = FindObjectOfType<GameManager>();
    }
    
    void OnMouseDown()
    {
        if (gameManager != null)
        {
            Debug.Log($"Joueur {name} sélectionné !");
            
            // Pour tester : changer la couleur
            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
            {
                if (!isSelected)
                {
                    rend.material.color = Color.white; // Sélectionné
                    isSelected = true;
                }
                else
                {
                    rend.material.color = Color.gray; // Désélectionné
                    isSelected = false;
                }
            }
        }
    }
}