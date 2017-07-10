using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EdgeInfo {
    private int index0, index1;
    private List<Triangle> parentTriangles;
    private Vector3 intersectionPoint;

    public int Index0
    {
        get { return index0; }
    }

    public int Index1
    {
        get { return index1; }
    }

    public Vector3 IntersectionPoint
    {
        get { return intersectionPoint; }
        set { intersectionPoint = value; }
    }

    public EdgeInfo(int p0, int p1, Triangle t)
    {
        index0 = p0;
        index1 = p1;
        parentTriangles = new List<Triangle>();
        parentTriangles.Add(t);
    }

    public void addParent(Triangle parent)
    {
        parentTriangles.Add(parent);
    }

    public override bool Equals(object obj)
    {
        EdgeInfo otherObj = (EdgeInfo)obj;

        return (otherObj.index0 == this.index0 && otherObj.index1 == this.index1) || (otherObj.index1 == this.index0 && otherObj.index0 == this.index1);
    }
}
