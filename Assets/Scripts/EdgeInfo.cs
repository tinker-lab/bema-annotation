using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Edge that is already part of a mesh
/// </summary>
public class EdgeInfo {
    private int index0, index1;

    public EdgeInfo(int p0, int p1)
    {
        // Index0 is always the lower index.
        if (p0 < p1)
        {
            index0 = p0;
            index1 = p1;
        }
        else
        {
            index0 = p1;
            index1 = p0;
        }
    }

    public int Index0
    {
        get { return index0; }
    }

    public int Index1
    {
        get { return index1; }
    }

    public bool HasCommonIndexWith(EdgeInfo edge)
    {
        return this.index0 == edge.Index0 || this.index0 == edge.Index1 || this.index1 == edge.Index0 || this.index1 == edge.Index1;
    }

    public override bool Equals(object obj)
    {
        EdgeInfo otherObj = (EdgeInfo)obj;

        return (otherObj.index0 == this.index0 && otherObj.index1 == this.index1) || (otherObj.index1 == this.index0 && otherObj.index0 == this.index1);
    }

    public override int GetHashCode()
    {
        return index0.GetHashCode() + index1.GetHashCode();
    }
}
