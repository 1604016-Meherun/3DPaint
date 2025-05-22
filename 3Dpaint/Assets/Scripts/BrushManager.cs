using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrushManager : MonoBehaviour
{
    public GameObject brushPrefab;
    public Transform controllerTip;
    public float spawnInterval = 0.02f;

    public Color currentColor = Color.white;
    public float brushScale = 0.02f;

    private float lastSpawnTime;

    void Update()
    {
        if (Input.GetAxis("Oculus_CrossPlatform_SecondaryIndexTrigger") > 0.1f)
        {
            if (Time.time - lastSpawnTime > spawnInterval)
            {
                GameObject brush = Instantiate(brushPrefab, controllerTip.position, Quaternion.identity);
                brush.transform.localScale = Vector3.one * brushScale;
                brush.GetComponent<MeshRenderer>().material.color = currentColor;

                lastSpawnTime = Time.time;
            }
        }
    }

    public void SetBrushColor(Color color)
    {
        currentColor = color;
    }

    public void SetBrushScale(float scale)
    {
        brushScale = scale;
    }

    public void ClearCanvas()
    {
        foreach (var stroke in GameObject.FindGameObjectsWithTag("BrushStroke"))
        {
            Destroy(stroke);
        }
    }
}
