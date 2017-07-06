using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct DrawingSize  {

    private int width;
    private int height;
    private bool isEmpty;

    public int Width
    {
        get { return width; }
        set { width = value; }
    }
    public int Height
    {
        get { return height; }
        set { height = value; }
    }

    public bool IsEmpty
    {
        get { return isEmpty; }
        set { isEmpty = value; }
    }

    public DrawingSize(int width, int height)
    {
        this.width = width;
        this.height = height;

        if (width == 0 && height == 0)
        {
            isEmpty = true;
        }
        else
        {
            isEmpty = false;
        }
    }
}
