using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public class RayCastSelectionState : InteractionState
{
    private ControllerInfo controller0;
    private ControllerInfo controller1;

    private GameObject collidingObject;
    private GameObject tubeMesh;

    
    //turn outlinePoints into a local variable within ProcessMesh
    private List<Vector3> outlinePoints;    //Pairs of two connected points to be used in drawing an outline mesh
    private List<Vector3> rayDirection;

    //laser
    private GameObject laser;
    private int hitLayer;
    bool buttonPressed;
    Mesh tubeMeshA;
    Mesh tubeMeshB;
    bool renderingA;
    bool endPtCloseToStartPt;

    MeshData curMeshData;
    List<int> outlineTriangles;


    /// <summary>
    /// State that allows a user to draw a selection outline using raycasting.
    /// Currently only works when selecting a single object.
    /// </summary>
    /// <param name="controller0Info"></param>
    /// <param name="controller1Info"></param>
    /// <param name="stateToReturnTo"></param>
    public RayCastSelectionState(ControllerInfo controller0Info, ControllerInfo controller1Info, SelectionData sharedData)
    {
        desc = "RayCastSelectionState";
        controller0 = controller0Info;
        controller1 = controller1Info;

        collidingObject = null;

        outlinePoints = new List<Vector3>();
        outlineTriangles = new List<int>();
        tubeMesh = MakeTubeMesh();

        //setup laser
        laser = GameObject.Find("LaserParent").transform.GetChild(0).gameObject;
        rayDirection = new List<Vector3>();
        buttonPressed = false;

    }

    bool DoRayCast(ControllerInfo controllerInfo, GameObject laser, ref RaycastHit hit)
    {
        Vector3 laserStartPos = controllerInfo.trackedObj.transform.position;

        if (Physics.Raycast(laserStartPos, controllerInfo.trackedObj.transform.forward, out hit, 4f)) 
        {
            //Debug.Log("raycast ");// + hit.collider.gameObject.name);
            ShowLaser(hit.point, laser, laserStartPos);
            return true;
        }
        else
        {
            //laser.SetActive(false);
            ShowLaser(laserStartPos + 4f * controllerInfo.trackedObj.transform.forward, laser, laserStartPos);
            return false;
        }
    }

    private void ShowLaser(Vector3 hitPoint, GameObject laser, Vector3 laserStartPos)
    {
        laser.SetActive(true);
        Transform laserTransform = laser.transform;

        laserTransform.position = Vector3.Lerp(laserStartPos, hitPoint, .5f);
        laserTransform.LookAt(hitPoint);
        laserTransform.localScale = new Vector3(laserTransform.localScale.x, laserTransform.localScale.y, Vector3.Distance(hitPoint, laserStartPos));
    }

    public override void Deactivate()
    {
        laser.SetActive(false);
    }

    public override string HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        controller0 = controller0Info;
        controller1 = controller1Info;

        RaycastHit hit = new RaycastHit();
        bool collided = DoRayCast(controller0Info, laser, ref hit);

        if (collided && collidingObject == null)
        {
            collidingObject = hit.collider.gameObject;
        }
        else if (collided && collidingObject != null)
        {
            if (collidingObject != hit.collider.gameObject)
            {
                //TODO: We need to reset things here and restart the selection
                Debug.Log("TODO");
                Debug.Break();
            }
        }
        else if (!collided)
        {
            collidingObject = null;
        }
        
        //button has been clicked, begin selection
        if(controller0.device.GetHairTriggerDown() && collided && !buttonPressed)
        {
            //Debug.Log("Trigger down!");
            outlinePoints.Clear();
            rayDirection.Clear();
            outlineTriangles.Clear();
            endPtCloseToStartPt = false;

            Mesh m = collidingObject.GetComponent<MeshFilter>().mesh;
            if (m.subMeshCount == 1)
            {
                curMeshData = new MeshData(m.vertices, m.uv, m.triangles);
            }
            else if (m.subMeshCount == 2)
            {
                // There is already a previous selection
                //TODO: We need to figure out how to handle this. The outline must stay within the previous, or else we need to use the previous selection edges as part of the cut
                Debug.Log("TODO");
                Debug.Break();
                //curMeshData = new MeshData(m.vertices, m.GetIndices(0));
            }
            else
            {
                //We shouldn't get here!
                Debug.Log("Error");
                Debug.Break();
            }

            //TODO:
            // set invalid color for outline here

            AddNextPoint(hit);

            buttonPressed = true;
        }
        else if (!controller0.device.GetHairTriggerUp() && collided && buttonPressed) // button is held down, continue selection
        {
            //Only add new points if they have moved far enough from the previous
            float movementThreshold = 0.008f;
            if ((hit.point-outlinePoints[outlinePoints.Count-1]).magnitude > movementThreshold)
            {
                AddNextPoint(hit);

                UpdateOutline(outlinePoints);

                bool prevVal = endPtCloseToStartPt;
                endPtCloseToStartPt = IsEndPtCloseToStartPt();
                if (endPtCloseToStartPt)
                {
                    //TODO: update tube color if close enough
                }
                else if (prevVal)
                {
                    //TODO: set back to invalid color 
                }
            }
        }
        else if (controller0.device.GetHairTriggerUp() && buttonPressed) //button has been released, end selection
        {
            buttonPressed = false;

            endPtCloseToStartPt = IsEndPtCloseToStartPt();
            //TODO: do we need to worry about recoloring here? I don't think so?
            if (endPtCloseToStartPt)
            {
                AddNextPoint(hit);

                //TODO: We need to add the first resampled point back to the end to complete the loop

                UpdateOutline(outlinePoints);

                ProcessMesh();
            }
        }
        return "";
    }

    private void AddNextPoint(RaycastHit hit)
    {
        outlinePoints.Add(hit.point);
        outlineTriangles.Add(hit.triangleIndex);
        rayDirection.Add(controller0.trackedObj.transform.forward);
    }

    private void UpdateOutline(List<Vector3> points)
    {
        Mesh bufferMesh = renderingA ? tubeMeshA : tubeMeshB;
        renderingA = !renderingA;
        tubeMesh.GetComponent<MeshFilter>().sharedMesh = CreateOutlineMesh(points, rayDirection[rayDirection.Count - 1], bufferMesh);
    }

    private bool IsEndPtCloseToStartPt()
    {
        float distanceThreshold = 0.03f;
        if (outlinePoints.Count < 2)
        {
            return false;
        }
        else
        {
            return ((outlinePoints[outlinePoints.Count - 1] - outlinePoints[0]).magnitude <= distanceThreshold);
        }
    }

    //private List<Vector3> Resample(List<Vector3> pts, float sampleDistance)
    //{
    //    CatmullRomSpline spline = new CatmullRomSpline();
    //    float time = 0f;
    //    float totalDistance = 0f;
    //    for (int i = 0; i < pts.Count(); i++)
    //    {
    //        if (i > 0)
    //        {
    //            float distance = (pts[i] - pts[i - 1]).magnitude;
    //            totalDistance += distance;
    //            time += Mathf.Sqrt(distance);
    //        }
    //        spline.Append(time, pts[i]);
    //    }

    //    List<Vector3> resampled = new List<Vector3>();

    //    int numSampleIntervals = (int) (totalDistance / sampleDistance);
    //    float interval = spline.TotalTime() / numSampleIntervals;
    //    if (numSampleIntervals != 0 && time != 0)
    //    {
    //        for (int i = 0; i <= numSampleIntervals; i++)
    //        {
    //            Vector3 pos = spline.Evaluate(i * interval);
    //            resampled.Add(pos);
    //            if (i > 0)
    //            {
    //                resampled.Add(pos);
    //            }
    //        }
    //    }
    //    else
    //    {
    //        resampled = pts;
    //    }

    //    return resampled;

    //}


    private void ProcessMesh()
    {
        //OutlineTriangles is continually updated as we subdivide and split faces.
        // but we still need to know when you cross an original triangle edge, so we make a copy here.
        List<int> origOutlineTriangles = new List<int>();
        foreach (int triId in outlineTriangles)
        {
            origOutlineTriangles.Add(triId);
        }

        int curTriId = outlineTriangles[0];

        // subdivide the first face
        Dictionary<int, List<int>> children = curMeshData.SubDivideFace(outlineTriangles[0], outlinePoints[0], -1);
        UpdateTriangleIndices(children);

        for (int i = 1; i < outlinePoints.Count; i++)
        {
            //Are we in the same triangle as the previous point or did we cross an edge?
            if (origOutlineTriangles[i] == curTriId)
            {
                // subdivide face where the point is (likely could be in a new triangle)
                children = curMeshData.SubDivideFace(outlineTriangles[i], outlinePoints[i], curMeshData.VertexIndex(outlineTriangles[i], outlinePoints[i - 1]));
                UpdateTriangleIndices(children);
                if (i == outlinePoints.Count - 1)
                {
                    MarkEndToStartConnectionAsCut(children, outlinePoints[i]);
                }
            }
            else
            {
                //new triangle.
                curTriId = origOutlineTriangles[i];

                // find edge intersection

                List<Vector3> prevTriPoints = curMeshData.Points(outlineTriangles[i - 1]);
                Vector3 prevNormal = Vector3.Cross(prevTriPoints[0] - prevTriPoints[1], prevTriPoints[2] - prevTriPoints[1]).normalized;
                Vector3 projectionOfCurrentIntoPrevPlane = outlinePoints[i] + (prevNormal * (-(Vector3.Dot(prevNormal, outlinePoints[i]) - Vector3.Dot(prevNormal, outlinePoints[i - 1]))));
                List<Vector3> sharedEdgeVerts = curMeshData.SharedEdge(outlineTriangles[i - 1], outlineTriangles[i]);
                if (sharedEdgeVerts.Count != 2)
                {
                    //TODO! There is an assumption here that there are no other triangles in between the previous point and the current!
                    Debug.Log("Broken assumption. Triangles in cut do not share an edge.");
                    Debug.Break();
                }

                Vector3 intersectPoint = FindIntersection(outlinePoints[i - 1], projectionOfCurrentIntoPrevPlane, sharedEdgeVerts[0], sharedEdgeVerts[1]);

                // split face on previous tri
                MeshTriangle prev = curMeshData.Face(outlineTriangles[i - 1]);
                MeshTriangle cur = curMeshData.Face(outlineTriangles[i]);
                int faceIndex = curMeshData.FaceIndex(cur, prev);
                children = curMeshData.SplitFace(outlineTriangles[i - 1], curMeshData.Vertex(prev, faceIndex).index, intersectPoint);
                UpdateTriangleIndices(children);
                if (i == outlinePoints.Count - 1)
                {
                    MarkEndToStartConnectionAsCut(children, intersectPoint);
                }

                // subdivide current tri
                children = curMeshData.SubDivideFace(outlineTriangles[i], outlinePoints[i], -1);
                UpdateTriangleIndices(children);
                if (i == outlinePoints.Count - 1)
                {
                    MarkEndToStartConnectionAsCut(children, outlinePoints[i]);
                }

                // split face on appropriate tri
                int neighborId = curMeshData.NeighborId(prev, faceIndex);
                MeshTriangle newNeighbor = curMeshData.Neighbor(prev, faceIndex);
                int newFaceIndex = curMeshData.FaceIndex(prev, newNeighbor);
                children = curMeshData.SplitFace(neighborId, curMeshData.Vertex(newNeighbor, newFaceIndex).index, intersectPoint);
                UpdateTriangleIndices(children);
                if (i == outlinePoints.Count - 1)
                {
                    MarkEndToStartConnectionAsCut(children, intersectPoint);
                }
            }
        }


        Queue<int> queue = new Queue<int>();

        // Pick a good starting triangle that we can assume is inside the selection
        //TODO: we can probably pick one we are more sure of using heuristics
        int id = outlineTriangles[0];
        curMeshData.Face(id).selected = true;
        queue.Enqueue(id);

        while (queue.Count > 0)
        {
            id = queue.Dequeue();
            MeshTriangle tri = curMeshData.Face(id);

            // Get all adjacent faces of the dequeued
            // tri. If an adjacent has not been visited and does not share a cut edge, 
            // then mark it visited and enqueue it
            MeshTriangle neighbor0 = curMeshData.Neighbor(tri, 0);
            if (!tri.edge0IsCut && !neighbor0.selected)
            {
                neighbor0.selected = true;
                queue.Enqueue(tri.adjTriId0);
            }
            MeshTriangle neighbor1 = curMeshData.Neighbor(tri, 1);
            if (!tri.edge1IsCut && !neighbor1.selected)
            {
                neighbor1.selected = true;
                queue.Enqueue(tri.adjTriId1);
            }
            MeshTriangle neighbor2 = curMeshData.Neighbor(tri, 2);
            if (!tri.edge2IsCut && !neighbor2.selected)
            {
                neighbor2.selected = true;
                queue.Enqueue(tri.adjTriId2);
            }
        }

        curMeshData.UpdateMesh(collidingObject.GetComponent<MeshFilter>().mesh);

        if (collidingObject.GetComponent<MeshFilter>().mesh.subMeshCount == 2)
        {
            Material[] materials = new Material[2];
            Material baseMaterial = collidingObject.GetComponent<Renderer>().materials[0];
            materials[0] = DetermineBaseMaterial(baseMaterial);         // Sets unselected as transparent
            materials[1] = Resources.Load("Selected") as Material;      // May need to specify which submesh we get this from? -> THIS SETS SELECTION AS ORANGE STUFF
            collidingObject.GetComponent<Renderer>().materials = materials;
        }
    }


    private void MarkEndToStartConnectionAsCut(Dictionary<int, List<int>> newTris, Vector3 secondToLastPoint)
    {
        foreach(KeyValuePair<int, List<int>> entry in newTris)
        {
            List<int> children = entry.Value;
            foreach(int id  in children)
            {
                // Does this triangle we just created have the outline start point as a vertex? Is so, one of it's edges should be cut.
                int endIndex = curMeshData.VertexIndex(id, outlinePoints[0]);
                if (endIndex != -1)
                {
                    int startIndex = curMeshData.VertexIndex(id, secondToLastPoint);
                    if (startIndex != -1)
                    {
                        MeshTriangle tri = curMeshData.Face(id);
                        int edgeIndex = curMeshData.EdgeIndex(startIndex, endIndex);
                        switch (edgeIndex)
                        {
                            case 0:
                                tri.edge0IsCut = true;
                                break;
                            case 1:
                                tri.edge1IsCut = true;
                                break;
                            case 2:
                                tri.edge2IsCut = true;
                                break;
                        }
                    }
                    else
                    {
                        Debug.Log("Error: can't find secondToLastPoint");
                    }
                }

            }
        }
    }
    
    /// <summary>
    /// Line segment intersection test between the lines v0v1 and v2v3 that are in the same plane
    /// </summary>
    /// <param name="v0"></param>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="v3"></param>
    /// <returns></returns>
    private Vector3 FindIntersection(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        if (PlaneCollision.ApproximatelyEquals(v0, v2) ||
        (PlaneCollision.ApproximatelyEquals(v0, v3)))
        {
            return v0;
        }

        if (PlaneCollision.ApproximatelyEquals(v1, v2))
        {
            return v2;
        }

        //L1 = v0 + a d0
        //L2 = v2 + b d1

        Vector3 d0 = v1 - v0;
        Vector3 d1 = v3 - v2;

        // Derivation:
        //v0 + a d0 = v2 + b d1
        //a d0 = (v2 - v0) + b d1
        //a (d0 X d1) = (v2 - v0) X d1, solve for a

        Vector3 d0CrossD1 = Vector3.Cross(d0, d1);
        Vector3 rightSide = Vector3.Cross((v2 - v0), d1);

        if (d0CrossD1.magnitude == 0)
        {
            Debug.Log("lines are not in the same plane!");
            Debug.Break();
        }
        float a = rightSide.magnitude / d0CrossD1.magnitude;

        if (a < 0.0 || a > 1.0)
        {
            Debug.Log("lines intersect outside of the segment");
            Debug.Break();
        }

        return v0 + a * d0;
    }

    private void UpdateTriangleIndices(Dictionary<int, List<int>> parentToChildren)
    {
        for(int i=0; i < outlineTriangles.Count; i++)
        {
            if (parentToChildren.ContainsKey(outlineTriangles[i]))
            {
                // Find which child now contains the associated point
                Vector3 point = outlinePoints[i];
                foreach(int child in parentToChildren[outlineTriangles[i]])
                {
                    List<Vector3> points = curMeshData.Points(child);
                    if (PointInTriangle(point, points[0], points[1], points[2]))
                    {
                        outlineTriangles[i] = child;
                        break;
                    }
                }
            }
        }
    }

    // Tests whether
    // point p is in triangle (a, b, c)
    private bool PointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 v0 = b - a, v1 = c - a, v2 = p - a;
        float d00 = Vector3.Dot(v0, v0);
        float d01 = Vector3.Dot(v0, v1);
        float d11 = Vector3.Dot(v1, v1);
        float d20 = Vector3.Dot(v2, v0);
        float d21 = Vector3.Dot(v2, v1);
        float invDenom = 1.0f / (d00 * d11 - d01 * d01);
        float v = (d11 * d20 - d01 * d21) * invDenom;
        float w = (d00 * d21 - d01 * d20) * invDenom;
        //u = 1.0f - v - w;

        return (v >= 0) && (w >= 0) && (v + w < 1);
    }


    /**
     * points contains a list of points where each successive pair of points gets a tube drawn between them, sets to mesh called selectorMesh
     * */
    private Mesh CreateOutlineMesh(List<Vector3> points, Vector3 rayDirection, Mesh outlineMesh)
    {
        List<Vector3> verts;
        List<int> faces;
        List<Vector2> uvCoordinates;

        const float radius = .005f;
        const int numSections = 4;

        int expectedNumVertices = (numSections + 1) * points.Count;
        //int expectedNumVertices = points.Count * numSections;
        verts = new List<Vector3>(expectedNumVertices);
        faces = new List<int>(expectedNumVertices*2 - (numSections*2));
        uvCoordinates = new List<Vector2>(expectedNumVertices);
    
        outlineMesh.Clear();

      
        // points.Add(points.ElementAt(0)); //Add the first point again at the end to make a loop.

        if (points.Count >= 2)
        {
            List<Vector3> duplicatedPoints = new List<Vector3>();
            duplicatedPoints.Add(points[0]);
            for (int i = 1; i < points.Count; i++)
            {
                duplicatedPoints.Add(points[i]);
                duplicatedPoints.Add(points[i]);
            }

            // Assumes that points contains the first point of the line and then every other point in points is duplicated!!!!!!!!!!!!!!!!!!! Point duplication happens in resample method
            for (int i = 0; i < duplicatedPoints.Count - 1; i+=2)
            {
                Vector3 centerStart = duplicatedPoints[i];
                Vector3 centerEnd = duplicatedPoints[i + 1];
                Vector3 direction = centerEnd - centerStart;
                direction = direction.normalized;
                Vector3 right = Vector3.Cross(rayDirection, direction);
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

            outlineMesh.RecalculateNormals();
        }

        return outlineMesh;
    }

    private List<Vector3> RemoveSequentialDuplicates(List<Vector3> points)
    {
        List<Vector3> output = new List<Vector3>(points.Count);
        int i = 0;
        output.Add(points[i]);
        while (i < points.Count - 1)
        {
            int j = i+1;
            while(j < points.Count && PlaneCollision.ApproximatelyEquals(points[i], points[j])){
                j++;
            }
            if (j < points.Count)
            {
                output.Add(points[j]);
            }
            i = j;
        }

        return output;
    }
    
    /// <summary>
    /// Given a material, returns a transparent version if it's not already transparent
    /// </summary>
    /// <param name="baseMaterial"></param>
    /// <returns></returns>
    Material DetermineBaseMaterial(Material baseMaterial)
    {
        if (baseMaterial.name == "TransparentUnselected")
        {
           return baseMaterial;
        }
        else
        {
            Material transparentBase = new Material(baseMaterial);
            transparentBase.name = "TransparentUnselected";
            transparentBase.color = new Color(1.0f, 1.0f, 1.0f, 0.5f);
            transparentBase.SetFloat("_Mode", 3f);
            transparentBase.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            transparentBase.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            transparentBase.SetInt("_ZWrite", 0);
            transparentBase.DisableKeyword("_ALPHATEST_ON");
            transparentBase.DisableKeyword("_ALPHABLEND_ON");
            transparentBase.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            transparentBase.renderQueue = 3000;
            return transparentBase;
        }
    }

    /// <summary>
    /// Make a Gameobject that will follow the user's hands
    /// </summary>
    /// <param name="meshName"></param>
    /// <returns></returns>
    private GameObject MakeTubeMesh()
    {
        GameObject newOutline = new GameObject();
        newOutline.name = "outline";
        newOutline.AddComponent<MeshRenderer>();
        newOutline.AddComponent<MeshFilter>();
        tubeMeshA = new Mesh();
        tubeMeshB = new Mesh();
        tubeMeshA.MarkDynamic();
        tubeMeshB.MarkDynamic();
        newOutline.GetComponent<MeshFilter>().mesh = tubeMeshA;
        renderingA = true;
       // newOutline.GetComponent<MeshFilter>().mesh.MarkDynamic();
        newOutline.GetComponent<Renderer>().material = Resources.Load("TestMaterial") as Material;

        return newOutline;
    }
}

/// <summary>
/// Class to hold a triangle mesh following the conventions in CGAL
/// </summary>
public class MeshData
{
    protected List<MeshTriangle> meshTriangles;
    protected List<MeshVertex> meshVertices;

    public MeshData(Vector3[] verts, Vector2[] uvs, int[] indices)
    {
        meshTriangles = new List<MeshTriangle>();
        meshVertices = new List<MeshVertex>();
        BuildDS(verts, uvs, indices);
    }

    public void UpdateMesh(Mesh mesh)
    {
        mesh.Clear();

        Vector3[] vertices = new Vector3[meshVertices.Count];
        Vector2[] uvs = new Vector2[meshVertices.Count];
        for(int i=0; i < meshVertices.Count; i++)
        {
            vertices[i] = meshVertices[i].point;
            uvs[i] = meshVertices[i].uv;
        }
        mesh.vertices = vertices;
        mesh.uv = uvs;

        List<int> selectedIndices = new List<int>();
        List<int> unselectedIndices = new List<int>();

        for(int i=0; i < meshTriangles.Count; i++)
        {
            MeshTriangle tri = meshTriangles[i];
            if (tri.unused)
            {
                continue;
            }
            if (tri.selected)
            {
                selectedIndices.Add(tri.vId0);
                selectedIndices.Add(tri.vId1);
                selectedIndices.Add(tri.vId2);
            }
            else
            {
                unselectedIndices.Add(tri.vId0);
                unselectedIndices.Add(tri.vId1);
                unselectedIndices.Add(tri.vId2);
            }
        }

        if (selectedIndices.Count > 0)
        {
            mesh.subMeshCount = 2;

            mesh.SetTriangles(unselectedIndices, 0);
            mesh.SetTriangles(selectedIndices, 1);
        }
        else
        {
            mesh.subMeshCount = 1;
            mesh.SetTriangles(unselectedIndices, 0);
        }

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    private void BuildDS(Vector3[] verts, Vector2[] uvs, int[] indices)
    {
        List<MeshVertex> vertsToSort = new List<MeshVertex>();

        for(int i=0; i < verts.Length; i++)
        {
            MeshVertex v = new MeshVertex(i, verts[i], uvs[i]);
            meshVertices.Add(v);

            vertsToSort.Add(v);
        }

        for(int i=0; i < indices.Length; i+=3)
        {
            int vId0 = indices[i];
            int vId1 = indices[i + 1];
            int vId2 = indices[i + 2];
            MeshTriangle tri = new MeshTriangle(vId0, vId1, vId2);
            meshTriangles.Add(tri);
            int triIndex = meshTriangles.Count - 1;

            meshVertices[vId0].triangleId = triIndex;
            meshVertices[vId1].triangleId = triIndex;
            meshVertices[vId2].triangleId = triIndex;
        }

        FindSamePositions(ref vertsToSort);

        for(int i=0; i < meshTriangles.Count; i++)
        {
            MeshTriangle curTri = meshTriangles[i];
            //tri 0 is verts 1,2
            curTri.adjTriId0 = FindAdjTri(Vertex(curTri.vId1), Vertex(curTri.vId2));

            // tri 1 is verts 0,2
            curTri.adjTriId1 = FindAdjTri(Vertex(curTri.vId0), Vertex(curTri.vId2));

            // tri 2 is verts 0, 1
            curTri.adjTriId2 = FindAdjTri(Vertex(curTri.vId0), Vertex(curTri.vId1));

            //TODO: if needed, this could be sped up by also setting corresponding triangle adjacencies.

        }
    }

    // Compute barycentric coordinates (u, v, w) for
    // point p with respect to triangle (a, b, c)
    private Vector3 Barycentric(MeshTriangle tri, Vector3 p)
    {
        Vector3 a = meshVertices[tri.vId0].point;
        Vector3 b = meshVertices[tri.vId1].point;
        Vector3 c = meshVertices[tri.vId2].point;
        Vector3 v0 = b - a, v1 = c - a, v2 = p - a;
        float d00 = Vector3.Dot(v0, v0);
        float d01 = Vector3.Dot(v0, v1);
        float d11 = Vector3.Dot(v1, v1);
        float d20 = Vector3.Dot(v2, v0);
        float d21 = Vector3.Dot(v2, v1);
        float invDenom = 1.0f / (d00 * d11 - d01 * d01);
        float v = (d11 * d20 - d01 * d21) * invDenom;
        float w = (d00 * d21 - d01 * d20) * invDenom;
        float u = 1.0f - v - w;
        return new Vector3(u, v, w);
    }


    /// <summary>
    /// Splits the triangle indicated by triId into three separate triangles around curPoint.
    /// CurPoint must be contained within the original triangle (this is not checked!)
    /// If cutVertexIndex is not -1 then the new edge from it to the curPoint will be marked as cut.
    /// </summary>
    /// <param name="triId"></param>
    /// <param name="curPoint"></param>
    /// <returns>A list of the new triangle ids</returns>
    public Dictionary<int, List<int>> SubDivideFace(int triId, Vector3 curPoint, int cutVertexIndex)
    {
        MeshTriangle orig = meshTriangles[triId];
        Vector3 baryCentric = Barycentric(orig, curPoint);
        Vector2 uv = meshVertices[orig.vId0].uv * baryCentric.x + meshVertices[orig.vId1].uv * baryCentric.y + meshVertices[orig.vId2].uv * baryCentric.z;

        // make a new vertex in the center of the three new triangles
        int id = meshVertices.Count;
        MeshVertex center0 = new MeshVertex(id, curPoint, uv);
        MeshVertex center1 = new MeshVertex(id + 1, curPoint, uv);
        MeshVertex center2 = new MeshVertex(id + 2, curPoint, uv);

        List<int> samePosition = new List<int>() {id, id + 1, id + 2 };
        center0.samePositions = samePosition;
        center1.samePositions = samePosition;
        center2.samePositions = samePosition;

        meshVertices.Add(center0);
        meshVertices.Add(center1);
        meshVertices.Add(center2);

        

        // Duplicate the necessary edge vertices from the original triangle.
        MeshVertex origV0 = new MeshVertex(id + 3, meshVertices[orig.vId0].point, meshVertices[orig.vId0].uv); 
        MeshVertex origV1 = new MeshVertex(id + 4, meshVertices[orig.vId1].point, meshVertices[orig.vId1].uv);
        MeshVertex origV2 = new MeshVertex(id + 5, meshVertices[orig.vId2].point, meshVertices[orig.vId2].uv);

        meshVertices.Add(origV0);
        meshVertices.Add(origV1);
        meshVertices.Add(origV2);


        // Make the new triangles
        MeshTriangle tri0 = new MeshTriangle(center0.index, orig.vId1, origV2.index);
        MeshTriangle tri1 = new MeshTriangle(center1.index, orig.vId2, origV0.index);
        MeshTriangle tri2 = new MeshTriangle(center2.index, orig.vId0, origV1.index);


        int tri0Id = meshTriangles.Count;
        int tri1Id = tri0Id + 1;
        int tri2Id = tri1Id + 1;

        tri0.adjTriId0 = orig.adjTriId0;
        tri0.adjTriId1 = tri1Id;
        tri0.adjTriId2 = tri2Id;

        tri1.adjTriId0 = orig.adjTriId1;
        tri1.adjTriId1 = tri2Id;
        tri1.adjTriId2 = tri0Id;

        tri2.adjTriId0 = orig.adjTriId2;
        tri2.adjTriId1 = tri0Id;
        tri2.adjTriId2 = tri1Id;

        // Mark cuts
        tri0.edge0IsCut = orig.edge0IsCut;
        tri1.edge0IsCut = orig.edge1IsCut;
        tri2.edge0IsCut = orig.edge2IsCut;
        switch (cutVertexIndex)
        {
            case 0:
                tri1.edge1IsCut = true;
                tri2.edge2IsCut = true;
                break;
            case 1:
                tri0.edge2IsCut = true;
                tri2.edge1IsCut = true;
                break;
            case 2:
                tri0.edge1IsCut = true;
                tri1.edge2IsCut = true;
                break;
        }

        if (orig.adjTriId0 == -1 || orig.adjTriId1 == -1 || orig.adjTriId2 == -1)
        {
            Debug.Log("Triangle does not have an adjacent");
        }

        //Update neighboring triangles.
        meshTriangles[orig.adjTriId0].UpdateAdjTri(triId, tri0Id);
        meshTriangles[orig.adjTriId1].UpdateAdjTri(triId, tri1Id);
        meshTriangles[orig.adjTriId2].UpdateAdjTri(triId, tri2Id);

        // Set new tri ids
        center0.triangleId = tri0Id;
        center1.triangleId = tri1Id;
        center2.triangleId = tri2Id;
        origV0.triangleId = tri1Id;
        origV1.triangleId = tri2Id;
        origV2.triangleId = tri0Id;

        // Update old tri ids for vertices we are reusing
        meshVertices[orig.vId0].triangleId = tri2Id;
        meshVertices[orig.vId1].triangleId = tri0Id;
        meshVertices[orig.vId2].triangleId = tri1Id;

        foreach(int vId in meshVertices[orig.vId0].samePositions)
        {
            if (vId != orig.vId0)
            {
                meshVertices[vId].samePositions.Add(origV0.index);
            }
        }
        foreach (int vId in meshVertices[orig.vId1].samePositions)
        {
            if (vId != orig.vId1)
            {
                meshVertices[vId].samePositions.Add(origV1.index);
            }
        }
        foreach (int vId in meshVertices[orig.vId2].samePositions)
        {
            if (vId != orig.vId2)
            {
                meshVertices[vId].samePositions.Add(origV2.index);
            }
        }

        meshVertices[orig.vId0].samePositions.Add(origV0.index);
        meshVertices[orig.vId1].samePositions.Add(origV1.index);
        meshVertices[orig.vId2].samePositions.Add(origV2.index);

        origV0.samePositions = meshVertices[orig.vId0].samePositions;
        origV1.samePositions = meshVertices[orig.vId1].samePositions;
        origV2.samePositions = meshVertices[orig.vId2].samePositions;

        meshTriangles.Add(tri0);
        meshTriangles.Add(tri1);
        meshTriangles.Add(tri2);

        List<int> children = new List<int>() { tri0Id, tri1Id, tri2Id };
        Dictionary<int, List<int>> parentToChildren = new Dictionary<int, List<int>>();
        parentToChildren.Add(triId, children);

        orig.unused = true;

        return parentToChildren;
    }

    /// <summary>
    /// Returns a list of the two vertices along a shared edge between the triangles.
    /// The list will be empty if they don't share an edge.
    /// </summary>
    /// <param name="triId0"></param>
    /// <param name="triId1"></param>
    /// <returns></returns>
    public List<Vector3> SharedEdge(int triId0, int triId1)
    {
        MeshTriangle tri0 = meshTriangles[triId0];
        MeshTriangle tri1 = meshTriangles[triId1];
        int faceIndex = FaceIndex(tri1, tri0);
        List<Vector3> verts = new List<Vector3>();
        if (faceIndex != -1)
        {
            verts.Add(Vertex(tri0, CW(faceIndex)).point);
            verts.Add(Vertex(tri0, CCW(faceIndex)).point);
        }
        return verts;
    }

    /// <summary>
    /// Splits an existing triangle from the vertex specified by vertexID to the split point
    /// NOTE: This also splits the adjoining triangle to maintain proper connections!
    /// Marks the split edge as cut for triId
    /// </summary>
    /// <param name="triId"></param>
    /// <param name="vertexId"></param>
    /// <param name="splitPoint"></param>
    /// <returns>A dictionary of the child triangles created. </returns>
    public Dictionary<int, List<int>> SplitFace(int triId, int vertexId, Vector3 splitPoint)
    {
        MeshTriangle splitTri = meshTriangles[triId];

        int splitVertIndexInTri = VertexIndex(vertexId, splitTri);
        Debug.Assert(splitVertIndexInTri != -1);

        Vector3 splitEdgeDir = (Vertex(splitTri, CW(splitVertIndexInTri)).point - Vertex(splitTri, CCW(splitVertIndexInTri)).point);
        float alpha = (splitPoint - Vertex(splitTri, CCW(splitVertIndexInTri)).point).magnitude / splitEdgeDir.magnitude;
        Vector2 uv = alpha * Vertex(splitTri, CCW(splitVertIndexInTri)).uv + (1.0f - alpha) * Vertex(splitTri, CW(splitVertIndexInTri)).uv; 

        int id = meshVertices.Count;
        // New vertices on the edge that is being split, both for the current tri and neighboring tri
        MeshVertex split0 = new MeshVertex(id, splitPoint, uv);
        MeshVertex split1 = new MeshVertex(id+1, splitPoint, uv);
        MeshVertex split2 = new MeshVertex(id+2, splitPoint, uv);
        MeshVertex split3 = new MeshVertex(id+3, splitPoint, uv);

        List<int> samePosition = new List<int>() { id, id + 1, id + 2, id + 3 };
        split0.samePositions = samePosition;
        split1.samePositions = samePosition;
        split2.samePositions = samePosition;
        split3.samePositions = samePosition;

        // Vertex on opposite side of split edge
        MeshVertex splitVertex = meshVertices[vertexId];
        


        MeshTriangle neighbor = Neighbor(splitTri, splitVertIndexInTri);
        int neighborSplitIndex = FaceIndex(splitTri, neighbor);
        MeshVertex neighborSplitVertex = Vertex(neighbor, neighborSplitIndex);
        int neighborId = NeighborId(splitTri, splitVertIndexInTri);


        //Make new split vertex and neighbor tri vertex
        MeshVertex newSplitVertex = new MeshVertex(id+4, splitVertex.point, splitVertex.uv);
        MeshVertex newNeighborSplitVertex = new MeshVertex(id + 5, neighborSplitVertex.point, neighborSplitVertex.uv);

        // Make the new triangles
        MeshTriangle tri0 = new MeshTriangle(splitVertex.index, Vertex(splitTri, CCW(splitVertIndexInTri)).index, split0.index);
        MeshTriangle tri1 = new MeshTriangle(newSplitVertex.index, split1.index, Vertex(splitTri, CCW(CCW(splitVertIndexInTri))).index);
        // Neighboring new triangles
        MeshTriangle tri2 = new MeshTriangle(newNeighborSplitVertex.index, split2.index, Vertex(neighbor, CW(neighborSplitIndex)).index);
        MeshTriangle tri3 = new MeshTriangle(neighborSplitVertex.index, Vertex(neighbor, CCW(neighborSplitIndex)).index, split3.index);

        // set trianglesIndices for verts
        int tri0Id = meshTriangles.Count;
        int tri1Id = tri0Id + 1;
        int tri2Id = tri1Id + 1;
        int tri3Id = tri2Id + 1;

        split0.triangleId = tri0Id;
        split1.triangleId = tri1Id;
        split2.triangleId = tri2Id;
        split3.triangleId = tri3Id;

        Vertex(splitTri, splitVertIndexInTri).triangleId = tri0Id;
        Vertex(splitTri, CCW(splitVertIndexInTri)).triangleId = tri0Id;
        Vertex(splitTri, CCW(CCW(splitVertIndexInTri))).triangleId = tri1Id;

        Vertex(neighbor, neighborSplitIndex).triangleId = tri3Id;
        Vertex(neighbor, CCW(neighborSplitIndex)).triangleId = tri3Id;
        Vertex(neighbor, CCW(CCW(neighborSplitIndex))).triangleId = tri2Id;

        // update split vertices same points
        foreach (int vId in splitVertex.samePositions)
        {
            if (vId != vertexId)
            {
                meshVertices[vId].samePositions.Add(newSplitVertex.index);
            }
        }
        foreach (int vId in neighborSplitVertex.samePositions)
        {
            if (vId != neighborId)
            {
                meshVertices[vId].samePositions.Add(newNeighborSplitVertex.index);
            }
        }

        splitVertex.samePositions.Add(newSplitVertex.index);
        neighborSplitVertex.samePositions.Add(newNeighborSplitVertex.index);

        newSplitVertex.samePositions = splitVertex.samePositions;
        newNeighborSplitVertex.samePositions = neighborSplitVertex.samePositions;

        // set adj tris
        tri0.adjTriId0 = tri2Id;
        tri0.adjTriId1 = tri1Id;
        tri0.adjTriId2 = NeighborId(splitTri, CW(splitVertIndexInTri));
        tri1.adjTriId0 = tri3Id;
        tri1.adjTriId1 = NeighborId(splitTri, CCW(splitVertIndexInTri));
        tri1.adjTriId2 = tri0Id;
        tri2.adjTriId0 = tri0Id;
        tri2.adjTriId1 = NeighborId(neighbor, CCW(neighborSplitIndex));
        tri2.adjTriId2 = tri3Id;
        tri3.adjTriId0 = tri1Id;
        tri3.adjTriId1 = tri2Id;
        tri3.adjTriId2 = NeighborId(splitTri, CW(neighborSplitIndex));

        // update cut edges based on original
        bool splitEdgeCut = neighbor.IsSharedEdgeCut(triId);
        if (splitEdgeCut)
        {
            Debug.Log("The user has drawn a self intersecting cut");
        }
        tri0.edge0IsCut = splitEdgeCut;
        tri0.edge1IsCut = true;
        tri0.edge2IsCut = meshTriangles[NeighborId(splitTri, CW(splitVertIndexInTri))].IsSharedEdgeCut(triId);
        tri1.edge0IsCut = splitEdgeCut;
        tri1.edge1IsCut = meshTriangles[NeighborId(splitTri, CCW(splitVertIndexInTri))].IsSharedEdgeCut(triId);
        tri1.edge2IsCut = true;
        tri2.edge0IsCut = splitEdgeCut;
        tri2.edge1IsCut = meshTriangles[NeighborId(neighbor, CCW(neighborSplitIndex))].IsSharedEdgeCut(neighborId);
        tri2.edge2IsCut = false;
        tri3.edge0IsCut = splitEdgeCut;
        tri3.edge1IsCut = false;
        tri3.edge2IsCut = meshTriangles[NeighborId(neighbor, CW(neighborSplitIndex))].IsSharedEdgeCut(neighborId);

        // update neighbor tris

        meshTriangles[NeighborId(splitTri, CW(splitVertIndexInTri))].UpdateAdjTri(triId, tri0Id);
        meshTriangles[NeighborId(splitTri, CCW(splitVertIndexInTri))].UpdateAdjTri(triId, tri1Id);
        meshTriangles[NeighborId(neighbor, CW(neighborSplitIndex))].UpdateAdjTri(neighborId, tri3Id);
        meshTriangles[NeighborId(neighbor, CCW(neighborSplitIndex))].UpdateAdjTri(neighborId, tri2Id);

        meshTriangles.Add(tri0);
        meshTriangles.Add(tri1);
        meshTriangles.Add(tri2);
        meshTriangles.Add(tri3);

        List<int> childrenOfSplitTri = new List<int>() { tri0Id, tri1Id };
        List<int> childrenOfNeighborTri = new List<int>() { tri2Id, tri3Id };

        Dictionary<int, List<int>> parentToChildTriangles = new Dictionary<int, List<int>>();
        parentToChildTriangles.Add(triId, childrenOfSplitTri);
        parentToChildTriangles.Add(NeighborId(splitTri, splitVertIndexInTri), childrenOfNeighborTri);

        splitTri.unused = true;
        neighbor.unused = true;

        return parentToChildTriangles;

    }

    private int FindAdjTri(MeshVertex v0, MeshVertex v1)
    {
        List<int> samePosV0 = v0.samePositions;
        List<int> samePosV1 = v1.samePositions;

        for(int i=0; i < samePosV0.Count; i++)
        {
            if (samePosV0[i] == v0.index)
            {
                continue;
            }
            for(int j = 0; j < samePosV1.Count; j++)
            {
                if (samePosV1[j] == v1.index)
                {
                    continue;
                }
                if (meshVertices[samePosV0[i]].triangleId == meshVertices[samePosV1[j]].triangleId)
                {
                    return meshVertices[samePosV0[i]].triangleId;
                }
            }
        }
        return -1;
    }

    // Side-effect, verts is sorted after calling!
    private void FindSamePositions(ref List<MeshVertex> verts)
    {
        verts.Sort(delegate (MeshVertex pt0, MeshVertex pt1)
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

        // set cur val to first point
        //loop over list
        // if (cur point equal to cur val)
        // add to same list
        // else
        // set values for all in the list
        // set new cur
        // clear same list

        Vector3 curPoint = verts[0].point;
        List<int> samePoints = new List<int>();        
        for( int i=0; i < verts.Count; i++)
        {
            if (!PlaneCollision.ApproximatelyEquals(verts[i].point, curPoint))
            {
                foreach(int j in samePoints)
                {
                    verts[j].samePositions = samePoints;
                }
                samePoints = new List<int>();
                curPoint = verts[i].point;
            }
            samePoints.Add(verts[i].index);
        }
    }



    /// <summary>
    /// Returns the triangle corresponding to a particular vertex
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    public MeshTriangle Face(MeshVertex v)
    {
        return meshTriangles[v.triangleId];
    }

    public MeshTriangle Face(int triId)
    {
        return meshTriangles[triId];
    }

    public List<Vector3> Points(int triId)
    {
        MeshTriangle tri = meshTriangles[triId];
        return new List<Vector3>() { meshVertices[tri.vId0].point, meshVertices[tri.vId1].point, meshVertices[tri.vId2].point };
    }

    public MeshVertex Vertex(int vertexId)
    {
        return meshVertices[vertexId];
    }

    public MeshVertex Vertex(MeshTriangle tri, int i)
    {
        switch (i)
        {
            case 0:
                return meshVertices[tri.vId0];
            case 1:
                return meshVertices[tri.vId1];
            case 2:
                return meshVertices[tri.vId2];
            default:
                Debug.Log("ERROR in Vertex call. Tried to access with i not within range [0-2]");
                return null;
        }
    }

    public int VertexIndex(MeshVertex v, MeshTriangle tri)
    {
        if (v.Equals(meshVertices[tri.vId0]))
        {
            return 0;
        }
        else if (v.Equals(meshVertices[tri.vId1]))
        {
            return 1;
        }
        else if (v.Equals(meshVertices[tri.vId2]))
        {
            return 2;
        }
        else
        {
            return -1;
        }
    }

    public int VertexIndex(int vertexId, MeshTriangle tri)
    {
        if (vertexId == tri.vId0)
        {
            return 0;
        }
        else if (vertexId == tri.vId1)
        {
            return 1;
        }
        else if (vertexId == tri.vId2)
        {
            return 2;
        }
        else
        {
            return -1;
        }
    }

    public int VertexIndex(int triId, Vector3 point)
    {
        MeshTriangle tri = meshTriangles[triId];
        if (PlaneCollision.ApproximatelyEquals(meshVertices[tri.vId0].point, point))
        {
            return 0;
        }
        else if (PlaneCollision.ApproximatelyEquals(meshVertices[tri.vId1].point, point))
        {
            return 1;
        }
        else if (PlaneCollision.ApproximatelyEquals(meshVertices[tri.vId2].point, point))
        {
            return 2;
        }
        else
        {
            Debug.Log("Cannot find a vertex with that point in this tri");
            //Debug.Break();
            return -1;
        }
    }

    public MeshTriangle Neighbor(MeshTriangle tri, int i)
    {
        switch (i)
        {
            case 0:
                return meshTriangles[tri.adjTriId0];
            case 1:
                return meshTriangles[tri.adjTriId1];
            case 2:
                return meshTriangles[tri.adjTriId2];
            default:
                Debug.Log("ERROR in Neighbor call. Tried to access with i not within range [0-2]");
                return null;
        }
    }

    public int NeighborId(MeshTriangle tri, int i)
    {
        switch (i)
        {
            case 0:
                return tri.adjTriId0;
            case 1:
                return tri.adjTriId1;
            case 2:
                return tri.adjTriId2;
            default:
                Debug.Log("ERROR in NeighborIndex call. Tried to access with i not within range [0-2]");
                return -1;
        }
    }

    public int FaceIndex(int testId, int triId)
    {
        return FaceIndex(meshTriangles[testId], meshTriangles[triId]);
    }

    public int FaceIndex(MeshTriangle test, MeshTriangle tri)
    {
        if (test.Equals(meshTriangles[tri.adjTriId0]))
        {
            return 0;
        }
        else if (test.Equals(meshTriangles[tri.adjTriId1]))
        {
            return 1;
        }
        else if (test.Equals(meshTriangles[tri.adjTriId2]))
        {
            return 2;
        }
        else
        {
            return -1;
        }
    }

    public int CW(int i)
    {
        return (i + 2) % 3;
    }

    public int CCW(int i)
    {
        return (i + 1) % 3;
    }

    public int EdgeIndex(int v0Index, int v1Index)
    {
        if (CCW(v0Index) == v1Index)
        {
            return CW(v0Index);
        }
        else if (CW(v0Index) == v1Index)
        {
            return CCW(v0Index);
        }
        else
        {
            Debug.Log("ERROR: Edge index given incorrect indices");
            Debug.Break();
            return -1;
        }
    }
}

public class MeshTriangle
{
    public int adjTriId0
    {
        get; set;
    }
    public int adjTriId1
    {
        get; set;
    }
    public int adjTriId2
    {
        get; set;
    }

    public int vId0
    {
        get; set;
    }
    public int vId1
    {
        get; set;
    }
    public int vId2
    {
        get; set;
    }
    public bool unused
    {
        get; set;
    }

    public bool selected
    {
        get; set;
    }

    public bool edge0IsCut { get; set; }
    public bool edge1IsCut { get; set; }
    public bool edge2IsCut { get; set; }

    public MeshTriangle(int vId0, int vId1, int vId2)
    {
        this.vId0 = vId0;
        this.vId1 = vId1;
        this.vId2 = vId2;
        this.adjTriId0 = -1;
        this.adjTriId1 = -1;
        this.adjTriId2 = -1;
        unused = false;
        edge0IsCut = false;
        edge1IsCut = false;
        edge2IsCut = false;

        selected = false;
    }

    public override bool Equals(object obj)
    {
        MeshTriangle other = (MeshTriangle)obj;
        return adjTriId0 == other.adjTriId0 && adjTriId1 == other.adjTriId1 && adjTriId2 == other.adjTriId2 && vId0 == other.vId0 && vId1 == other.vId1 && vId2 == other.vId2;
    }

    /// <summary>
    /// replaces the value for an adj tri
    /// </summary>
    /// <param name="previousIndex"></param>
    /// <param name="newIndex"></param>
    public void UpdateAdjTri(int previousId, int newId)
    {
        if (adjTriId0 == previousId)
        {
            adjTriId0 = newId;
        }
        else if (adjTriId1 == previousId)
        {
            adjTriId1 = newId;
        }
        else if (adjTriId2 == previousId)
        {
            adjTriId2 = newId;
        }
        else
        {
            Debug.Log("method called with a previousID that isn't currently assigned.");
            Debug.Break();
        }
    }

    public bool IsSharedEdgeCut(int neighborId)
    {
        if (adjTriId0 == neighborId)
        {
            return edge0IsCut;
        }
        else if (adjTriId1 == neighborId)
        {
            return edge1IsCut;
        }
        else if (adjTriId2 == neighborId)
        {
            return edge2IsCut;
        }
        else
        {
            // method called with a previousID that isn't currently assigned.
            Debug.Log("Error neighbor id is not a neighbor of this triangle");
            Debug.Break();
            return false;
        }
    }

}

public class MeshVertex
{
    public int triangleId;
    public Vector3 point;
    public Vector2 uv;
    public int index;
   

    // Holds the indices for other vertices at the same position
    public List<int> samePositions
    {
        get; set;
    }

    public MeshVertex(int index, Vector3 point, Vector2 uv)
    {
        this.index = index;
        this.triangleId = -1;
        this.point = point;
        this.uv = uv;
        samePositions = new List<int>();
    }

    public override bool Equals(object obj)
    {
        MeshVertex other = (MeshVertex)obj;
        return triangleId == other.triangleId && PlaneCollision.ApproximatelyEquals(this.point, other.point);
    }

}