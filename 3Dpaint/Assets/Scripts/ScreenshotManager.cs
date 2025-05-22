using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenshotManager : MonoBehaviour
{
    public string fileName = "3DPaintScreenshot.png";

    public void TakeScreenshot()
    {
        StartCoroutine(Capture());
    }

    IEnumerator Capture()
    {
        yield return new WaitForEndOfFrame();
        string path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log("Screenshot saved to: " + path);
    }
}
