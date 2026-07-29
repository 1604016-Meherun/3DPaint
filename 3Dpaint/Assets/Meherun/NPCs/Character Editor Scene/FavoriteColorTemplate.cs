using UnityEngine;

[CreateAssetMenu(fileName = "New Favorite Color", menuName = "Favorite Color")]
public class FavoriteColorTemplate : ScriptableObject
{
    public Color primaryColor;
    public Color textColor;
}
