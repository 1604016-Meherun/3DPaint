using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using System.Collections.Generic;

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

        if (triggerPressed)
        {
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

    public void SwapColor(FavoriteColorTemplate color)
    {
        SetBrushColor(color.primaryColor);
    }

    // --- Custom color control (type a color code and paint with it) ---

    // Shows/hides the color popup panel. Wired to the "Color" button.
    public void ToggleColorPopup()
    {
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
        SetColorFromHex(hexInputField.text);
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
        // Loop backwards to avoid issues when modifying the hierarchy while iterating
        for (int i = lineParent.childCount - 1; i >= 0; i--)
        {
            Transform child = lineParent.GetChild(i);
            Destroy(child.gameObject);
        }
    }

}
