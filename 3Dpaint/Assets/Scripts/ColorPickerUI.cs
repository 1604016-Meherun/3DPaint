using UnityEngine;

public class ColorPickerUI : MonoBehaviour
{
    public LineDrawManager drawManager;
    public Color myColor;

    public GameObject lineParent; // Assign LineParent from the inspector


    public void ClearAllLines()
    {
        foreach (Transform child in lineParent.transform)
        {
            Destroy(child.gameObject);
        }
    }

    public void SetColorRed()
    {
        drawManager.brushColor = Color.red;
    }

    public void SetColorGreen()
    {
        drawManager.brushColor = Color.green;
        Debug.Log("Color set to green");
    }
    public void SetColorBlue() { drawManager.brushColor = Color.blue; }
    public void SetColorYellow() { drawManager.brushColor = Color.yellow; }
    public void SetColorWhite() { drawManager.brushColor = Color.white; }
    public void SetColorBlack(){ drawManager.brushColor = Color.black; }
    //public void SetColorPink() => drawManager.brushColor = Color.pink;


    public void SetColorCustom(Color c) => drawManager.brushColor = c;
}
