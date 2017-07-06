using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BezierCurve {

    private Vector3 p0;
    private Vector3 p1;
    private Vector3 p2;
    private Vector3 p3;

    private bool uninitialized;

    public BezierCurve() { uninitialized = true; }

    public BezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        this.p0 = p0;
        this.p1 = p1;
        this.p2 = p2;
        this.p3 = p3;
        uninitialized = false;
    }

    public Vector3 GetControlPoint(int i)
    {
        switch(i)
        {
            case 0:
                return p0;
            case 1:
                return p1;
            case 2:
                return p2;
            case 3:
                return p3;
        }

        throw new System.Exception("Control point index wasn't an int between 0 and 3");
    }

    public Vector3 Evaluate(float time)
    {
        float u = 1f - time;
        float tt = time * time;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * time;

        Vector3 point = uuu * p0;
        point += 3 * uu * time * p1;
        point += 3 * u * tt * p2;
        point += ttt * p3;

        return point;
    }

    public bool IsInitialized()
    {
        return !uninitialized;
    }
}
