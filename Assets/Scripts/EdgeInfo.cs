using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Edge that is already part of a mesh
/// </summary>
public class EdgeInfo {
    private int index0, index1;
    //private List<Triangle> parentTriangles;
    private Vector3 intersectionPoint;
    private List<int> orderedIndices;

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

    public List<int> OrderedIndices
    {
        get { return orderedIndices; }
    }

    public EdgeInfo(int p0, int p1)
    {
        index0 = p0;
        index1 = p1;
        orderedIndices = OrderIndices(new List<int> { index0, index1 });
    }

    //public void addParent(Triangle parent)
    //{
    //    parentTriangles.Add(parent);
    //}

    public bool HasCommonIndexWith(EdgeInfo edge)
    {
        return this.index0 == edge.Index0 || this.index0 == edge.Index1 || this.index1 == edge.Index0 || this.index1 == edge.Index1;
    }

    // Orders index0 and index1 of edge, small to large
    public static List<int> OrderIndices(List<int> edge)
    {
        if (edge[1] < edge[0])
        {
            return new List<int> { edge[1], edge[0] };
        }
        else
        {
            return edge;
        }
    }

    public override bool Equals(object obj)
    {
        EdgeInfo otherObj = (EdgeInfo)obj;

        return (otherObj.index0 == this.index0 && otherObj.index1 == this.index1) || (otherObj.index1 == this.index0 && otherObj.index0 == this.index1);
    }
}
