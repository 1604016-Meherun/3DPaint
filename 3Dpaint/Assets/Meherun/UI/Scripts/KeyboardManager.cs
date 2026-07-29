using TMPro;
using UnityEngine;

public class VRKeyboardManager : MonoBehaviour
{
    public static VRKeyboardManager Instance;

    [Header("Live Display (while typing)")]
    public TMP_Text liveDisplayText;

    [Header("Saved Output Text")]
    public TMP_Text savedOutputText;

    [Header("Keyboard UI")]
    public GameObject keyboardUI; // Root of the keyboard
    public Animator keyboardAnimator; // Animator that plays "open" and "close"

    private string originalText = "";
    private string currentText = "";

    private bool isOpen = false;

    public GameObject placeholderText;

    public GameObject objToRename;

    void Awake()
    {
        Instance = this;
    }

    public void OpenKeyboard()
    {
        if(savedOutputText != null) originalText = savedOutputText.text;
        if (objToRename != null) originalText = objToRename.name;
        currentText = ""; // Start blank
        liveDisplayText.text = currentText;

        if (keyboardUI != null)
            keyboardUI.SetActive(true);

        if (keyboardAnimator != null)
            keyboardAnimator.Play("Open", 0, 0f);

        isOpen = true;
    }

    public void AddCharacter(string character)
    {
        currentText += character;
        liveDisplayText.text = currentText;
    }

    public void Backspace()
    {
        if (currentText.Length > 0)
        {
            currentText = currentText.Substring(0, currentText.Length - 1);
            liveDisplayText.text = currentText;
        }
    }

    public void SaveAndExit()
    {
        if(savedOutputText != null) savedOutputText.text = currentText;
        StartCoroutine(CloseWithAnimation());
        if(placeholderText != null) placeholderText.SetActive(false);
        isOpen = false;
    }

    public void ExitWithoutSaving()
    {
        liveDisplayText.text = originalText;
        StartCoroutine(CloseWithAnimation());
        isOpen = false;
    }

    public void ExitWithoutSavingIfOpen()
    {
        if (isOpen == true) ExitWithoutSaving();
    }

    public void SetObjectName()
    {
        objToRename.name = currentText;
    }

    private System.Collections.IEnumerator CloseWithAnimation()
    {
        if (keyboardAnimator != null)
            keyboardAnimator.Play("Close", 0, 0f);

        // Wait for animation to finish (you can hardcode or get length)
        yield return new WaitForSeconds(0.5f); // adjust to your "close" animation length

        //keyboardUI.SetActive(false);
        currentText = "";
    }
}
