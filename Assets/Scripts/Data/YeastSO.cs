using UnityEngine;

[CreateAssetMenu(fileName = "Yeast", menuName = "WineSim/Yeast")]
public class YeastSO : ScriptableObject
{
    public string yeastName;
    public float speed = 1f;          // 0.5..1.5 influences ferment days
    public float aroma = 0f;          // flat bonus to craft quality
    public float toleranceABV = 15f;  // stalls above
}