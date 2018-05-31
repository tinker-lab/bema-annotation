using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IntersectingPoint {

    private Vector3 point;
    private bool seen = false;

    public Vector3 Point
    {
        get { return point; }
    }

    public bool Seen
    {
        get { return seen; }
        set { seen = value; }
    }

    public IntersectingPoint(Vector3 point)
    {
        this.point = point;
    }

    public override bool Equals(object obj)
    {
        IntersectingPoint otherObj = (IntersectingPoint)obj;

        return PlaneCollision.ApproximatelyEquals(this.point, otherObj.Point);
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}
