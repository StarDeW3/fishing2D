using UnityEngine;

[CreateAssetMenu(menuName = "Fishing/Fish Type", fileName = "FishType")]
public class FishTypeData : ScriptableObject
{
    [Header("Görünüm")]
    public string fishName = "Fish";
    public Sprite sprite;

    [Header("Davranış")]
    [Min(0f)] public float speed = 2f;
    [Min(0f)] public float turnDelay = 5f;
    [Range(1f, 5f)] public float difficulty = 1f;

    [Header("Ödül")]
    public int scoreValue = 10;

    [Header("Spawn")]
    [Min(0)] public int spawnWeight = 10;
}
