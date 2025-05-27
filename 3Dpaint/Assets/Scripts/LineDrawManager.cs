using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine;
using UnityEngine.XR;

public class LineDrawManager : MonoBehaviour
{
    public Transform drawSource;
    public GameObject linePrefab;

    private LineDraw currentLine;
    public ControllerManager controllerManager;
    public Transform lineParent;

    //adding for the colour changing theme.
    [HideInInspector]
    public Color brushColor = Color.red;

    void Update()
    {
        if (!controllerManager.leftController.isValid || !controllerManager.rightController.isValid)
        {
            controllerManager.InitializeInputDevices();
        }

        if (drawSource == null) return;

        controllerManager.rightController.TryGetFeatureValue(CommonUsages.triggerButton, out bool buttonPressed);
        Debug.Log(buttonPressed);

        if (buttonPressed)
        {
            Debug.Log("Trigger pressed");
            if (currentLine == null)
            {
                GameObject newLine = Instantiate(linePrefab, lineParent);
                currentLine = newLine.GetComponent<LineDraw>();
                newLine.GetComponent<LineRenderer>().material.color = brushColor;
            }
            currentLine.AddPoint(drawSource.position);
        }
        else
        {
            currentLine = null;
        }
    }
}
