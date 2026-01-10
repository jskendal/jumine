using UnityEngine;

[CreateAssetMenu(fileName = "NewCellEffectData", menuName = "Game/Cell Effect Data")]
public class CellEffectData : ScriptableObject
{
    public EffectType effectType;
    public Sprite iconSprite; // Le cœur, le crâne, le bouclier...
    public Color backgroundColor = Color.white; // La couleur de base de la cellule
    
    // Optionnel : pour la futureRow (transparence)
    [Range(0f, 1f)] public float futureRowAlpha = 0.5f;
}