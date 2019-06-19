using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Triangle {
    
    private int Index0 { get; set; }
    private int Index1 { get; set; }
    private int Index2 { get; set; }

    public Triangle(int index0, int index1, int index2)
    {
        Index0 = index0;
        Index1 = index1;
        Index2 = index2;
    }

    public override bool Equals(object obj)
    {
        Triangle otherObj = (Triangle)obj;

        return (otherObj.Index0 == this.Index0 && otherObj.Index1 == this.Index1 && otherObj.Index2 == this.Index2);
    }

    public override int GetHashCode()
    {
        return Index0.GetHashCode() + Index1.GetHashCode()+ Index2.GetHashCode();
    }



}
