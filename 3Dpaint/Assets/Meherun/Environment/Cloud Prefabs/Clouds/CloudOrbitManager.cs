using UnityEngine;
using System.Collections.Generic;

public class CloudOrbitManager : MonoBehaviour
{
    [Header("Cloud Setup")]
    public GameObject[] cloudPrefabs; // Assign your 5 prefabs here
    public int numberOfClouds = 10;

    [Header("Orbit Settings")]
    public float orbitRadius = 20f;
    public float orbitSpeed = 10f;
    public float depthRandomness = 2f; // Random offset to radius
    public float speedVariance = 2f;

    [Header("Height Settings")]
    public Vector2 heightRange = new Vector2(-5f, 5f); // Min and max Y height

    [Header("Scale Settings")]
    public Vector2 cloudScaleRange = new Vector2(0.8f, 1.2f); // Min and max scale

    private List<CloudData> clouds = new List<CloudData>();

    private class CloudData
    {
        public GameObject cloudObject;
        public float angle;   // Current angle in degrees
        public float speed;   // Speed in degrees per second
        public float radius;  // Distance from center
        public float height;  // Y position
        public Vector3 scale;
    }

    void Start()
    {
        float heightStep = 0f;
        if (numberOfClouds > 1)
            heightStep = (heightRange.y - heightRange.x) / (numberOfClouds - 1);

        for (int i = 0; i < numberOfClouds; i++)
        {
            GameObject prefab = cloudPrefabs[Random.Range(0, cloudPrefabs.Length)];
            GameObject cloud = Instantiate(prefab);

        float angle = Random.Range(0f, 360f);
        float speed = orbitSpeed + Random.Range(-speedVariance, speedVariance);
        float radius = orbitRadius + Random.Range(-depthRandomness, depthRandomness);
        float height = heightRange.x + (heightStep * i);

        // scale X and Y only
        float scaleValue = Random.Range(cloudScaleRange.x, cloudScaleRange.y);
        Vector3 originalScale = cloud.transform.localScale;
        cloud.transform.localScale = new Vector3(scaleValue, scaleValue, originalScale.z);

        CloudData data = new CloudData()
        {
            cloudObject = cloud,
            angle = angle,
            speed = speed,
            radius = radius,
            height = height,
            scale = cloud.transform.localScale
        };

            clouds.Add(data);
        }
    }


    void Update()
    {
        foreach (CloudData cloud in clouds)
        {
            cloud.angle += cloud.speed * Time.deltaTime;
            float rad = cloud.angle * Mathf.Deg2Rad;

            Vector3 position = new Vector3(
                Mathf.Cos(rad) * cloud.radius,
                cloud.height,
                Mathf.Sin(rad) * cloud.radius
            );

            cloud.cloudObject.transform.position = position;

            // Face the center
            cloud.cloudObject.transform.LookAt(new Vector3(0f, cloud.height, 0f), Vector3.up);
        }
    }
}
