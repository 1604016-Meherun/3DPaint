#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public class PrefabSaver : MonoBehaviour
{
    [Header("Save Settings")]
    public string saveFolder = "Assets/Prefabs"; // Your target folder

    public GameObject objectToSave;

    public Animator emptyNameErrorAnimator;
    public Animator duplicateNameErrorAnimator;

    public void SavePrefab()
    {
        string objectName = objectToSave.name;

        if(string.IsNullOrEmpty(objectName) || objectName == "Character")
        {
            if(emptyNameErrorAnimator != null)
            {
                Debug.LogWarning("Must rename preafab before saving.");
                OpenEmptyNameError();
                return;
            }
        }
        string fullPath = Path.Combine(saveFolder, objectName + ".prefab");

        // Normalize path for Unity
        fullPath = fullPath.Replace("\\", "/");

        // Check if prefab already exists
        if (AssetDatabase.LoadAssetAtPath<GameObject>(fullPath) != null)
        {
            Debug.LogWarning($"A prefab named '{objectName}.prefab' already exists in {saveFolder}. Save aborted.");
            if(duplicateNameErrorAnimator != null) OpenDuplicateNameError();
            return;
        }

        // Ensure directory exists
        if (!Directory.Exists(saveFolder))
        {
            Directory.CreateDirectory(saveFolder);
        }

        // Save the prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(objectToSave, fullPath);
        Debug.Log($"Prefab saved: {prefab.name} at {fullPath}");
    }

    public void OpenDuplicateNameError()
    {
        duplicateNameErrorAnimator.Play("Open", 0, 0f);
    }

    public void CloseDuplicateNameError()
    {
        duplicateNameErrorAnimator.Play("Close", 0, 0f);
    }

    public void OpenEmptyNameError()
    {
        emptyNameErrorAnimator.Play("Open", 0, 0f);
    }

    public void CloseEmptyNameError()
    {
        emptyNameErrorAnimator.Play("Close", 0, 0f);
    }
}
#endif
