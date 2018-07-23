using System;
using System.Collections.Generic;
using UnityEngine;

public class MakeCircle : MonoBehaviour
{
    public int numSections;
    public float radius;
    public Vector3 centerPoint;

    public MakeCircle()
    {
        numSections = 36;
        radius = 0.75f;
        centerPoint = new Vector3(0, 0, 0);
    }

    public void GeneratePlaneCircle()
    {
        Vector3 right = new Vector3(1, 0, 0);
        Vector3 up = new Vector3(0, 1, 0);
        Vector3 point;
        Vector3 normal;

        up = up.normalized * radius;
        right = right.normalized * radius;

        for (int i = 1; i < numSections; i++)
        {
            if ((numSections % 2) == 0 && i == (numSections / 2))
            {
                continue;
            }

            float theta = (float)i / (float)numSections * 2.0f * Mathf.PI;
            point = centerPoint + right * Mathf.Sin(theta) + up * Mathf.Cos(theta);
            normal = (-(point - centerPoint)).normalized;
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.transform.parent = GameObject.Find("planeParent").transform;
            plane.transform.position = point;
            plane.transform.rotation = Quaternion.AngleAxis(90, Vector3.forward) * Quaternion.LookRotation(normal, Vector3.up);
            if(i > numSections / 2)
            {
                plane.transform.Rotate(Vector3.right, 180f);
            }
        }
    }
}
