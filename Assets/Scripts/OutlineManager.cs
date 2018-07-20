using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OutlinePoint
{
    public Vector3 point;
    public int index;
    public int sameTriangle;
    public int samePosition;

    public int chainID;
    public int indexInChain;

    public OutlinePoint(Vector3 pt, int ind, int triangle)
    {
        point = pt;
        index = ind;
        sameTriangle = triangle;
        samePosition = -1;
        chainID = -1;
        indexInChain = -1;
    }
}


public class OutlineManager {

    private static int outlineObjectCount;
    public static Dictionary<string, List<GameObject>> preSelectionOutlines;

    public OutlineManager()
    {
        outlineObjectCount = 0;
        preSelectionOutlines = new Dictionary<string, List<GameObject>>();
    }

    public static void ResizePreselectedPoints(GameObject item, List<OutlinePoint> unsortedPoints, int planePass, GameObject currentPlane)
    {
        //Debug.Log("Order points for " + item.name);
        List<List<OutlinePoint>> sortedPoints = OutlineManager.OrderPoints(unsortedPoints);
        //    Debug.Log("Just called order points");

        if (!preSelectionOutlines.ContainsKey(item.name))                              //
        {                                                                             // Add a highlight for this mesh if there isn't one already
            preSelectionOutlines.Add(item.name, new List<GameObject>());    //
        }

        if (planePass == 0 && preSelectionOutlines[item.name].Count > sortedPoints.Count)
        {
            for (int i = preSelectionOutlines[item.name].Count - 1; i >= sortedPoints.Count; i--)
            {
                UnityEngine.Object.Destroy(preSelectionOutlines[item.name][i]);
                preSelectionOutlines[item.name].RemoveAt(i);
            }
        }

        int startingIndex = (planePass == 0 ? 0 : preSelectionOutlines[item.name].Count);

        if (preSelectionOutlines[item.name].Count < (planePass == 0 ? sortedPoints.Count : preSelectionOutlines[item.name].Count + sortedPoints.Count))
        {
            int n = (planePass == 0 ? sortedPoints.Count : preSelectionOutlines[item.name].Count + sortedPoints.Count) - preSelectionOutlines[item.name].Count;
            for (int i = 0; i < n; i++)
            {
                preSelectionOutlines[item.name].Add(OutlineManager.MakeNewOutline(item)); //MakeHandOutline(item.name));
            }
        }


        for (int chainIndex = 0; chainIndex < sortedPoints.Count; chainIndex++)
        {
            int meshId = startingIndex + chainIndex;

            if (meshId >= preSelectionOutlines[item.name].Count)
            {
                Debug.Log("meshID oob");
            }
            if (chainIndex < 0 || chainIndex >= sortedPoints.Count)
            {
                Debug.Log("chainIndex oob");
            }

            Mesh outlineMesh = OutlineManager.CreateOutlineMesh(sortedPoints[chainIndex], currentPlane.transform.up, preSelectionOutlines[item.name][meshId]);
            preSelectionOutlines[item.name][meshId].GetComponent<MeshFilter>().sharedMesh = outlineMesh;
            //preSelectionOutlines[item.name][meshId].transform.position = Vector3.zero;//item.transform.position;
            //preSelectionOutlines[item.name][meshId].transform.localScale = Vector3.one;//item.transform.localScale;
            //preSelectionOutlines[item.name][meshId].transform.rotation = Quaternion.identity;//item.transform.rotation;

            //}
        }
    }

    public static List<List<OutlinePoint>> OrderPoints(List<OutlinePoint> outlinePts)
    {

        //     Debug.Log("In order points");
        FindSamePositions(ref outlinePts);
        List<List<OutlinePoint>> chains = new List<List<OutlinePoint>>();
        int currChainID = 0;

        for (int i = 0; i < outlinePts.Count; i++)
        {
            if (outlinePts[i].chainID != -1)
            {
                continue;
            }

            List<OutlinePoint> currChain = new List<OutlinePoint>();
            int currIndex = i;
            int connectingChainID = -1;
            int sign = -1;
            int indexOfConnection = -1;

            while (true)
            {
                if (currIndex == -1)
                {
                    // happens when you get to the end of a chain.
                    break;
                }

                if (outlinePts[currIndex].chainID == -1)
                {
                    if (sign > 0 || currChain.Count == 0)
                    {
                        currChain.Add(outlinePts[currIndex]);               //add ever other point to the chain (the skipped ones will be in the same position to their adjacent point)
                        outlinePts[currIndex].chainID = currChainID;
                        outlinePts[currIndex].indexInChain = currChain.Count - 1;
                    }
                    else
                    {
                        outlinePts[currIndex].chainID = currChainID;
                        outlinePts[currIndex].indexInChain = -1;
                    }

                    if (sign < 0)                               //advance to a point that shares a triangle or a position with the current point
                    {
                        currIndex = outlinePts[currIndex].sameTriangle;
                    }
                    else
                    {
                        currIndex = outlinePts[currIndex].samePosition;
                    }
                    sign *= -1;

                }
                else
                {
                    if (outlinePts[currIndex].chainID != currChainID)
                    {                                                       //set up how this chain will merge with others
                        connectingChainID = outlinePts[currIndex].chainID;
                        indexOfConnection = outlinePts[currIndex].indexInChain;
                    }
                    else
                    {
                        // got back to the starting one. 

                        if (sign < 0)
                        {
                            currChain.Add(outlinePts[currIndex]);                                   //connected the beginning and end of the first outline, but subsequent selections are still broken.
                            //outlinePts[currIndex].chainID = currChainID;
                            //outlinePts[currIndex].indexInChain = currChain.Count - 1;
                        }
                    }
                    break;
                }
            }

            if (connectingChainID != -1)
            {
                // Debug.Log("index of connection " + indexOfConnection.ToString());
                if (indexOfConnection == 0)
                {
                    // Debug.Log("indexOfConnection == 0!!!");
                    foreach (OutlinePoint p in currChain)           //reset the saved chaining info for every outlinePoint
                    {
                        p.chainID = connectingChainID;
                    }
                    foreach (OutlinePoint p in chains[connectingChainID])
                    {
                        p.indexInChain += currChain.Count;
                    }
                    currChain.AddRange(chains[connectingChainID]);
                    chains[connectingChainID] = currChain;
                }
                else
                {
                    //        Debug.Log("Hand: While ordering outline points, adding to the middle of a chain. Hope to never see this in a log!!");                            

                    Debug.Log("indexOfConnections: " + indexOfConnection.ToString());

                }
            }
            else
            {
                chains.Add(currChain);          //save this chain and get ready for the next
                currChainID++;
            }

        }
        //if (chains.Count() > 0)
        //{
        //    Debug.Log("Found " + chains.Count().ToString() + " chains");
        //}

        List<int> deleteAfterMerge = new List<int>();
        for (int i = 0; i < chains.Count; i++)
        {
            if (deleteAfterMerge.Contains(i))
            {
                continue;
            }
            for (int j = i + 1; j < chains.Count; j++)
            {
                // if (PlaneCollision.ApproximatelyEquals(chains[i][0].point, chains[j][0].point))
                if (chains[i][0].samePosition == chains[j][0].index)
                {
                    //if (chains[i][0].samePosition != chains[j][0].index)
                    if (!PlaneCollision.ApproximatelyEquals(chains[i][0].point, chains[j][0].point))
                    {
                        Debug.Log("While ordering outline points, merged points don't have same position.");
                    }
                    chains[i].Reverse();
                    chains[i].AddRange(chains[j]);
                    //  Debug.Log("Joined two chains");
                    deleteAfterMerge.Add(j);
                }
            }
        }
        if (deleteAfterMerge.Count > 0)
        {
            //  Debug.Log("Remove " + deleteAfterMerge.Count().ToString() + " from the chain list");
        }
        deleteAfterMerge.Sort();
        deleteAfterMerge.Reverse();
        // Debug.Log("Indices to delete: " + deleteAfterMerge.ToString() + " FROM " + chains.Count());
        foreach (int index in deleteAfterMerge)
        {
            if (chains.Count < index)
            {
                Debug.Log("trying to remove an out of bounds list " + index.ToString() + " from " + chains.Count);
            }

            chains.RemoveAt(index);
        }


        return chains;
    }

    //Takes two connected points and adds or updates entries in the list of actual points and the graph of their connections
    private static void FindSamePositions(ref List<OutlinePoint> pointConnections)
    {
        List<OutlinePoint> sorted = new List<OutlinePoint>(pointConnections);
        sorted.Sort(delegate (OutlinePoint pt0, OutlinePoint pt1)
        {
            int xComp = pt0.point.x.CompareTo(pt1.point.x);
            if (xComp != 0)
            {
                return xComp;
            }
            else
            {
                int yComp = pt0.point.y.CompareTo(pt1.point.y);
                if (yComp != 0)
                {
                    return yComp;
                }
                else
                {
                    return pt0.point.z.CompareTo(pt1.point.z);
                }
            }
        });

        for(int i=0; i < sorted.Count-1; i++)
        {
            OutlinePoint vertex = sorted[i];
            if (vertex.samePosition == -1)
            {
                OutlinePoint otherVertex = sorted[i+1];
                if (PlaneCollision.ApproximatelyEquals(vertex.point, otherVertex.point))
                {
                    otherVertex.samePosition = vertex.index;
                    vertex.samePosition = otherVertex.index;
                }
            }
        }


        /*
        for (int i = 0; i < pointConnections.Count; i++)
        {
            OutlinePoint vertex = pointConnections[i];
            if (vertex.samePosition == -1)
            {

                for (int j = i + 1; j < pointConnections.Count; j++)
                {
                    OutlinePoint otherVertex = pointConnections[j];
                    if (PlaneCollision.ApproximatelyEquals(vertex.point, otherVertex.point))
                    {
                        //   Debug.Log("Found a SameTriangle at " + vertex.point.ToString());
                        otherVertex.samePosition = vertex.index;
                        vertex.samePosition = otherVertex.index;
                        break;
                    }
                }
            }
        }
        */
    }

    private List<Vector3> RemoveSequentialDuplicates(List<Vector3> points)
    {
        List<Vector3> output = new List<Vector3>(points.Count);
        int i = 0;
        output.Add(points[i]);
        while (i < points.Count - 1)
        {
            int j = i + 1;
            while (j < points.Count && PlaneCollision.ApproximatelyEquals(points[i], points[j]))
            {
                j++;
            }
            if (j < points.Count)
            {
                output.Add(points[j]);
            }
            i = j;
        }
        /*
    {
        bool firstTwoEqual = PlaneCollision.ApproximatelyEquals(points[i-1], points[i]);
        bool secondTwoEqual = PlaneCollision.ApproximatelyEquals(points[i], points[i + 1]);

        if (firstTwoEqual && secondTwoEqual)
        {
            output.Add(points[i - 1]);
            output.Add(points[i + 2]);
            i += 3;  
        }
        else if ((firstTwoEqual && !secondTwoEqual) || (!firstTwoEqual && secondTwoEqual))  // If only two are the same
        {
            output.Add(points[i-1]);      // Add one of the equal points
            output.Add(points[i + 1]);
            i += 3;
        }

        else  // All are distinct
        {
            output.Add(points[i - 1]);      // Add first two
            output.Add(points[i]);  
            i += 2;
        }
    }
    */
        return output;
    }

    /**
 * points contains a list of points where each successive pair of points gets a tube drawn between them, sets to mesh called selectorMesh
 * */
    public static Mesh CreateOutlineMesh(List<OutlinePoint> points, Vector3 plane, GameObject outline)
    {
        Mesh outlineMesh = outline.GetComponent<MeshFilter>().sharedMesh;
        List<Vector3> verts = new List<Vector3>();
        List<int> faces = new List<int>();
        List<Vector2> uvCoordinates = new List<Vector2>();
        outlineMesh.Clear();

        float radius = .005f;
        int numSections = 6;

        //Assert.IsTrue(points.Count % 2 == 0);
        int expectedNumVerts = (numSections + 1) * points.Count;


        //TODO: should we remove duplicates here?

        if (points.Count >= 2)
        {

            List<Vector3> duplicatedPoints = new List<Vector3>();
            duplicatedPoints.Add(points[0].point);
            for (int i = 1; i < points.Count; i++)
            {
                duplicatedPoints.Add(points[i].point);
                duplicatedPoints.Add(points[i].point);
            }


            for (int i = 0; i < duplicatedPoints.Count - 1; i += 2)
            {
                Vector3 centerStart = duplicatedPoints[i];
                Vector3 centerEnd = duplicatedPoints[i + 1];
                Vector3 direction = centerEnd - centerStart;
                direction = direction.normalized;
                Vector3 right = Vector3.Cross(plane, direction);
                Vector3 up = Vector3.Cross(direction, right);
                up = up.normalized * radius;
                right = right.normalized * radius;

                for (int slice = 0; slice <= numSections; slice++)
                {
                    float theta = (float)slice / (float)numSections * 2.0f * Mathf.PI;
                    Vector3 p0 = centerStart + right * Mathf.Sin(theta) + up * Mathf.Cos(theta);
                    Vector3 p1 = centerEnd + right * Mathf.Sin(theta) + up * Mathf.Cos(theta);

                    verts.Add(p0);
                    verts.Add(p1);
                    uvCoordinates.Add(new Vector2((float)slice / (float)numSections, 0));
                    uvCoordinates.Add(new Vector2((float)slice / (float)numSections, 1));

                    if (slice > 0)
                    {
                        faces.Add((slice * 2 + 1) + ((numSections + 1) * i));
                        faces.Add((slice * 2) + ((numSections + 1) * i));
                        faces.Add((slice * 2 - 2) + ((numSections + 1) * i));

                        faces.Add(slice * 2 + 1 + ((numSections + 1) * i));
                        faces.Add(slice * 2 - 2 + ((numSections + 1) * i));
                        faces.Add(slice * 2 - 1 + ((numSections + 1) * i));
                    }
                }
            }

            outlineMesh.SetVertices(verts);
            outlineMesh.SetUVs(0, uvCoordinates);
            outlineMesh.SetTriangles(faces, 0);

            outlineMesh.RecalculateBounds();
            outlineMesh.RecalculateNormals();
        }

        return outlineMesh;
    }

    /// <summary>
    /// Make a Gameobject that will follow the user's hands
    /// </summary>
    /// <param name="meshName"></param>
    /// <returns></returns>
    public static GameObject MakeNewOutline(GameObject item)
    {
        GameObject newOutline = new GameObject();
        newOutline.name = "highlight " + outlineObjectCount;
        newOutline.AddComponent<MeshRenderer>();
        newOutline.AddComponent<MeshFilter>();
        newOutline.GetComponent<MeshFilter>().mesh = new Mesh();
        newOutline.GetComponent<MeshFilter>().mesh.MarkDynamic();
        newOutline.GetComponent<Renderer>().material = Resources.Load("TestMaterial") as Material;
        Color transparency = new Color();
        transparency = newOutline.GetComponent<Renderer>().material.color + new Color(0f, 0f, 0f, 0.5f);
        newOutline.GetComponent<Renderer>().material.color = transparency;             //added this line to make outlines transparent for the experiment
        newOutline.tag = "highlightmesh";
        newOutline.layer = LayerMask.NameToLayer("Ignore Raycast");

        outlineObjectCount++;

        newOutline.transform.position = Vector3.zero; //item.transform.position;
        newOutline.transform.localScale = Vector3.one; //item.transform.localScale;
        newOutline.transform.rotation = Quaternion.identity; //item.transform.rotation;

        return newOutline;
    }

    /// <summary>
    /// Creates a new game object with the same position, rotation, scale, material, and mesh as the original.
    /// </summary>
    /// <param name="original"></param>
    /// <returns></returns>
    public static GameObject CopyObject(GameObject original)
    {
        GameObject copy = new GameObject();
        copy.AddComponent<MeshRenderer>();
        copy.AddComponent<MeshFilter>();
        copy.transform.position = original.transform.position;
        copy.transform.rotation = original.transform.rotation;
        copy.transform.localScale = original.transform.localScale;
        copy.GetComponent<MeshRenderer>().material = original.GetComponent<MeshRenderer>().material;
        Mesh mesh = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<int> ind = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        original.GetComponent<MeshFilter>().mesh.GetVertices(verts);
        original.GetComponent<MeshFilter>().mesh.GetTriangles(ind, 0);
        original.GetComponent<MeshFilter>().mesh.GetUVs(0, uvs);

        //mesh.SetTriangles(ind, 0);    //this one fails to set triangles.
        mesh.SetVertices(verts);
        mesh.SetTriangles(ind, 0);
        mesh.SetUVs(0, uvs);
        copy.GetComponent<MeshFilter>().mesh = mesh;
        copy.tag = "highlightmesh"; // tag this object as a highlight
        //copy.name = "highlight" + outlineObjectCount;
        copy.name = original.name;
        copy.layer = LayerMask.NameToLayer("Ignore Raycast");
        //outlineObjectCount++;

        return copy;
    }

}
