using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using System.Collections.Generic;
using ImmersiDraw.Study;

public class LineDrawManager : MonoBehaviour
{
    public Transform drawSource;
    public GameObject linePrefab;
    public Transform lineParent;

    public Color brushColor;

    [Header("Custom Color UI (optional)")]
    public Image brushColorPreview;
    public TMP_InputField hexInputField;
    public GameObject colorPopupPanel;

    private LineDraw currentLine;
    private float strokeStartTime;
    private InputDevice rightController;

    void Start()
    {
        InitializeInputDevice();
        UpdateColorUI();
    }

    void InitializeInputDevice()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);

        if (devices.Count > 0)
        {
            rightController = devices[0];
            Debug.Log("Right controller found!");
        }
        else
        {
            Debug.LogWarning("No right controller found!");
        }
    }

    void Update()
    {
        if (!rightController.isValid)
        {
            InitializeInputDevice();
            return;
        }

        if (drawSource == null) return;

        rightController.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed);

        // During an IRB study session, drawing is only enabled in the drawing phase.
        if (!StudyEvents.DrawingAllowed) triggerPressed = false;

        if (triggerPressed)
        {
            if (currentLine == null)
            {
                GameObject newLine = Instantiate(linePrefab, lineParent);
                currentLine = newLine.GetComponent<LineDraw>();
                newLine.GetComponent<LineRenderer>().material.color = brushColor;
                strokeStartTime = Time.realtimeSinceStartup;
                StudyEvents.Record("stroke_start", "#" + ColorUtility.ToHtmlStringRGBA(brushColor));
            }
            currentLine.AddPoint(drawSource.position);
        }
        else
        {
            if (currentLine != null)
            {
                float secs = Time.realtimeSinceStartup - strokeStartTime;
                StudyEvents.Record("stroke_end", $"points={currentLine.PointCount};seconds={secs:0.00}");
            }
            currentLine = null;
        }
    }

    public void SwapColor(FavoriteColorTemplate color)
    {
        SetBrushColor(color.primaryColor);
        StudyEvents.Record("color_selection", "palette:#" + ColorUtility.ToHtmlStringRGBA(brushColor));
    }

    // --- Custom color control (type a color code and paint with it) ---

    // Shows/hides the color popup panel. Wired to the "Color" button.
    public void ToggleColorPopup()
    {
        StudyEvents.Record("ui_button", "color_popup");
        if (colorPopupPanel != null) colorPopupPanel.SetActive(!colorPopupPanel.activeSelf);
    }

    public void CloseColorPopup()
    {
        if (colorPopupPanel != null) colorPopupPanel.SetActive(false);
    }

    // Reads whatever is currently typed in hexInputField and applies it as the brush color.
    // Wired to the hex input field's On End Edit event, so hitting Enter applies it live
    // without closing the popup.
    public void ApplyHexInput()
    {
        if (hexInputField == null) return;
        if (SetColorFromHex(hexInputField.text))
            StudyEvents.Record("color_selection", "hex:#" + ColorUtility.ToHtmlStringRGBA(brushColor));
    }

    // Applies the typed color and closes the popup. Wired to the "Paint" button.
    public void ApplyHexInputAndClose()
    {
        ApplyHexInput();
        CloseColorPopup();
    }

    // Removes the most recently drawn stroke. Wired to the "Undo" button.
    public void Undo()
    {
        if (lineParent == null || lineParent.childCount == 0) return;
        StudyEvents.Record("undo", "");
        Transform last = lineParent.GetChild(lineParent.childCount - 1);
        Destroy(last.gameObject);
    }

    // Accepts hex codes with or without a leading '#', e.g. "FF8800" or "#FF8800AA".
    // An 8-digit code (RRGGBBAA) also sets opacity; a 6-digit code keeps the current opacity.
    public bool SetColorFromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return false;
        hex = hex.Trim();
        if (!hex.StartsWith("#")) hex = "#" + hex;

        if (ColorUtility.TryParseHtmlString(hex, out Color parsed))
        {
            // 6-digit codes don't specify alpha - keep whatever opacity is currently set
            if (hex.Length == 7) parsed.a = brushColor.a;
            SetBrushColor(parsed);
            return true;
        }

        Debug.LogWarning($"LineDrawManager: '{hex}' is not a valid color code.");
        return false;
    }

    private void SetBrushColor(Color c)
    {
        brushColor = c;
        UpdateColorUI();
    }

    private void UpdateColorUI()
    {
        if (brushColorPreview != null) brushColorPreview.color = brushColor;
        if (hexInputField != null) hexInputField.text = "#" + ColorUtility.ToHtmlStringRGBA(brushColor);
    }

    public void Erase()
    {
        StudyEvents.Record("erase_all", "strokes=" + lineParent.childCount);
        // Loop backwards to avoid issues when modifying the hierarchy while iterating
        for (int i = lineParent.childCount - 1; i >= 0; i--)
        {
            Transform child = lineParent.GetChild(i);
            Destroy(child.gameObject);
        }
    }

}
