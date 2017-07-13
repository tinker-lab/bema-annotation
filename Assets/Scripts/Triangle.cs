using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Triangle {
    private EdgeInfo edge0;
    private EdgeInfo edge1;
    private EdgeInfo edge2;
    private HashSet<EdgeInfo> orderedEdges;

    public EdgeInfo Edge0
    {
        get { return edge0; }
    }

    public EdgeInfo Edge1
    {
        get { return edge1; }
    }

    public EdgeInfo Edge2
    {
        get { return edge2; }
    }

    public HashSet<EdgeInfo> OrderedEdges
    {
        get { return orderedEdges; }
    }
    
    public Triangle(int index0, int index1, int index2)
    {
        //edge0 = new EdgeInfo(index0, index1, this);
        //edge1 = new EdgeInfo( index0, index2, this) ;
        //edge2 = new EdgeInfo (index1, index2, this);
        //orderedEdges.Add(orderedTuple(edge0));
        //orderedEdges.Add(orderedTuple(edge1));
        //orderedEdges.Add(orderedTuple(edge2));
    }

    //public static List<int> orderedTuple(List<int> edge)
    //{
    //    if (edge[1] < edge[0])
    //    {
    //        return new List<int> { edge[1], edge[0] };
    //    }
    //    else
    //    {
    //        return edge;
    //    }
    //}

    //public bool isAdjacent(Triangle t)
    //{
    //    foreach (List<int> edge in orderedEdges)
    //    {
    //        if (t.OrderedEdges.Contains(edge))
    //        {
    //            return true;
    //        }
    //    }
    //    return false;
    //}




}
