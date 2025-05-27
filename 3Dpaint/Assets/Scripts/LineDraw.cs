using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LineDraw : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private List<Vector3> points = new List<Vector3>();
    public float minDistance = 0.01f;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
    }

    public void AddPoint(Vector3 newPoint)
    {
        if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], newPoint) >= minDistance)
        {
            points.Add(newPoint);
            lineRenderer.positionCount = points.Count;
            lineRenderer.SetPosition(points.Count - 1, newPoint);
        }
    }
}
