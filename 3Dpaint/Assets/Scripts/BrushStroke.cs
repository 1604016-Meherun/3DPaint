using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrushStroke : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        gameObject.tag = "BrushStroke";
        Destroy(gameObject, 180); // optional: auto destroy after 3 min
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
