using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Assertions;

public class HandSelectionState : InteractionState
{

    InteractionState stateToReturnTo;
    private ControllerInfo controller0;
    private ControllerInfo controller1;

    private GameObject leftPlane;
    private GameObject rightPlane;
    private GameObject centerCube;

    private int planeLayer;

    private List<Collider> collidedMeshes;
    private HashSet<Collider> cubeColliders;
    private HashSet<Collider> leftColliders;
    private HashSet<Collider> rightColliders;
    CubeCollision leftComponent;
    CubeCollision rightComponent;
    CubeCollision centerComponent;

    private Dictionary<string, int[]> originalUnselectedIndices;
    private static Dictionary<string, int> originalNumVertices;   // Key = name of object with mesh, Value = original set of vertices
    private static Dictionary<string, int[]> originalIndices; // key = name of object with mesh, value = original indices for the entire object
    private static HashSet<string> selectedMeshes; // Collection of the the names of all the meshes that have had pieces selected from them.

    //private Dictionary<Vector3, HashSet<Vector3>> pointGraph;   // Graph of selected points
    private List<Vector3> outlinePoints;    // Pairs of two connected points to be used in drawing an outline mesh

    List<int> selectedIndices;
    List<int> unselectedIndices;

    Vector3 corner0;
    Vector3 corner1;
    Vector3 corner2;
    
    List<Vector2> orderedUVs;
    List<Vector3> triangulatedMeshPoints;

    GameObject leftOutlineMesh;
    GameObject rightOutlineMesh;

    private Stopwatch watch;

    public static Dictionary<string, int[]> OriginalIndices
    {
        get { return originalIndices; }
    }
    public static Dictionary<string, int> SeenMeshes
    {
        get { return originalNumVertices; }
    }
    public static HashSet<string> SelectedMeshes
    {
        get { return selectedMeshes; }
    }

    public HandSelectionState(ControllerInfo controller0Info, ControllerInfo controller1Info, InteractionState stateToReturnTo)
    {
        desc = "HandSelectionState";
        controller0 = controller0Info;
        controller1 = controller1Info;


        planeLayer = LayerMask.NameToLayer("PlaneLayer");

        leftPlane = CreateHandPlane(controller0, "handSelectionLeftPlane");
        rightPlane = CreateHandPlane(controller1, "handSelectionRightPlane");

        centerCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        centerCube.name = "handSelectionCenterCube";
        centerCube.GetComponent<Renderer>().material = Resources.Load("Plane Material") as Material;
        centerCube.AddComponent<MeshCollider>();
        centerCube.GetComponent<MeshCollider>().convex = true;
        centerCube.GetComponent<MeshCollider>().isTrigger = true;
        centerCube.AddComponent<Rigidbody>();
        centerCube.GetComponent<Rigidbody>().isKinematic = true;
        centerComponent = centerCube.AddComponent<CubeCollision>();
        centerCube.transform.localScale = new Vector3(0.3f, 0.00000001f, 0.3f);
        centerCube.layer = planeLayer;
        centerCube.GetComponent<MeshRenderer>().enabled = false;

        collidedMeshes = new List<Collider>();      // TODO: figure out how to fill this up properly
        cubeColliders = new HashSet<Collider>();
        leftColliders = new HashSet<Collider>();
        rightColliders = new HashSet<Collider>();

        leftComponent = leftPlane.GetComponent<CubeCollision>();
        rightComponent = rightPlane.GetComponent<CubeCollision>();

        //TODO: should these persist between states? Yes so only make one instance of the state. Should use the Singleton pattern here//TODO

        selectedMeshes = new HashSet<string>();
        originalNumVertices = new Dictionary<string, int>();        // Keeps track of how many vertices a mesh should have
        originalUnselectedIndices = new Dictionary<string, int[]>();      // Keeps track of indices that were previously unselected
        originalIndices = new Dictionary<string, int[]>();
        selectedIndices = new List<int>();
        unselectedIndices = new List<int>();

        //pointGraph = new Dictionary<Vector3, HashSet<Vector3>>();   // graph of selected points
        outlinePoints = new List<Vector3>();

        this.stateToReturnTo = stateToReturnTo;

        corner0 = new Vector3();
        corner1 = new Vector3();
        corner2 = new Vector3();
        
        orderedUVs = new List<Vector2>();
        triangulatedMeshPoints = new List<Vector3>();

        watch = new Stopwatch();

        leftOutlineMesh = new GameObject();
        leftOutlineMesh.AddComponent<MeshRenderer>();
        leftOutlineMesh.AddComponent<MeshFilter>();

        rightOutlineMesh = new GameObject();
        rightOutlineMesh.AddComponent<MeshRenderer>();
        rightOutlineMesh.AddComponent<MeshFilter>();
    }

    public GameObject CreateHandPlane(ControllerInfo c, String name)
    {
        GameObject handPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        handPlane.name = name;
        handPlane.GetComponent<Renderer>().material = Resources.Load("Plane Material") as Material;
        handPlane.AddComponent<MeshCollider>();
        handPlane.GetComponent<MeshCollider>().convex = true;
        handPlane.GetComponent<MeshCollider>().isTrigger = true;
        handPlane.AddComponent<Rigidbody>();
        handPlane.GetComponent<Rigidbody>().isKinematic = true;
        //handPlane.AddComponent<CubeCollision>();

        handPlane.transform.position = c.controller.transform.position;
        handPlane.transform.rotation = c.controller.transform.rotation;
        //  handPlane.transform.localScale = new Vector3(0.3f, 0.00000001f, 0.3f);
        handPlane.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);

        handPlane.layer = planeLayer;
        handPlane.GetComponent<MeshRenderer>().enabled = false;
        return handPlane;
    }

    public void UpdatePlanes()
    {
        leftPlane.transform.position = controller0.controller.transform.position;
        rightPlane.transform.position = controller1.controller.transform.position;

        leftPlane.transform.up = (rightPlane.transform.position - leftPlane.transform.position).normalized;
        rightPlane.transform.up = (leftPlane.transform.position - rightPlane.transform.position).normalized;

        CenterCubeBetweenControllers();
    }

    public override void deactivate()
    {

        controller0.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;    // Disable hand rendering
        controller1.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;    //

        controller0.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true); // Enable rendering of controllers
        controller1.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true); //
    }

    public override void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        UpdatePlanes();

        // Take input from cube about what it collides with
        cubeColliders = centerComponent.CollidedObjects;
        
        if (cubeColliders.Count > 0)
        {
            collidedMeshes = cubeColliders.ToList();
        }
        else
        {
            GameObject.Find("UIController").GetComponent<UIController>().ChangeState(stateToReturnTo);  // If not colliding with anything, go back to navigationstate or wherever
        }


        foreach (Collider m in collidedMeshes)
        {
            if (!originalNumVertices.ContainsKey(m.name))
            {
                originalNumVertices.Add(m.name, m.GetComponent<MeshFilter>().mesh.vertices.Length);        // Maybe want to store vertices as array instead?

                m.GetComponent<MeshFilter>().mesh.MarkDynamic();
                originalIndices.Add(m.name, m.GetComponent<MeshFilter>().mesh.GetIndices(0));
            }
            ProcessMesh(m);
        }



        if (controller0.device.GetHairTriggerUp() || controller1.device.GetHairTriggerUp())     // Looks as though all that happens is it only displays the unselected submesh
        {
            foreach (Collider m in collidedMeshes)
            {
                originalNumVertices[m.name] = m.GetComponent<MeshFilter>().mesh.vertices.Length;


                //The submesh to start
                int submeshNum = 0;
                Material[] origMaterials = m.GetComponent<Renderer>().materials;
                for (int i = 0; i < origMaterials.Length; i++)
                {
                    if (origMaterials[i].name == "Selected (Instance)")
                    {
                        submeshNum = i;
                    }
                }
                originalIndices[m.name] = m.GetComponent<MeshFilter>().mesh.GetIndices(submeshNum);
                if (originalUnselectedIndices.ContainsKey(m.name))
                {
                    originalUnselectedIndices[m.name] = m.GetComponent<MeshFilter>().mesh.GetIndices(0);
                }
                else
                {
                    originalUnselectedIndices.Add(m.name, m.GetComponent<MeshFilter>().mesh.GetIndices(0));
                }
                selectedMeshes.Add(m.name);
            }
        }
        collidedMeshes.Clear(); // TODO: check that this list is still clear when returning to the state
    }



    private bool OnNormalSideOfPlane(Vector3 pt, GameObject plane)
    {
        return Vector3.Dot(plane.transform.up, pt) >= Vector3.Dot(plane.transform.up, plane.transform.position);
    }

    private void CenterCubeBetweenControllers()
    {
        // position plane at midpoint between controllers

        Vector3 leftPosition = leftPlane.transform.position;
        Vector3 rightPosition = rightPlane.transform.position;

        Vector3 halfWayBtwHands = Vector3.Lerp(leftPosition, rightPosition, 0.5f);
        centerCube.transform.position = halfWayBtwHands;

        // rotate plane w/ respect to both controllers
        RotatePlane(controller0, controller1, leftPosition, rightPosition, centerCube);

        // scale plane
        float distance = Vector3.Distance(rightPosition, leftPosition);

        centerCube.transform.localScale = new Vector3(1f, 0, 0) * distance + new Vector3(0, 0.3f, 0.3f);


    }

    private void RotatePlane(ControllerInfo controller0Info, ControllerInfo controller1Info, Vector3 leftPos, Vector3 rightPos, GameObject nPlane)
    {
        Vector3 xAxis = (rightPos - leftPos).normalized;

        Vector3 zAxis = controller0Info.isLeft ? controller1Info.trackedObj.transform.forward : controller0Info.trackedObj.transform.forward;
        zAxis = (zAxis - (Vector3.Dot(zAxis, xAxis) * xAxis)).normalized;
        Vector3 yAxis = Vector3.Cross(zAxis, xAxis).normalized;

        Vector3 groundY = new Vector3(0, 1);

        float controllerToGroundY = Vector3.Angle(yAxis, groundY);
        nPlane.transform.rotation = Quaternion.LookRotation(zAxis, yAxis);

    }

    private void ProcessMesh(Collider m)
    {
        Mesh mesh = m.GetComponent<MeshFilter>().mesh;
        selectedIndices.Clear();

        if (!selectedMeshes.Contains(m.name))
        {
            unselectedIndices.Clear();
            // unselectedIndices = mesh.GetTriangles(0).ToList();      // If this mesh has already been split, make sure unselected triangles are drawn too
        }
        else
        {
            unselectedIndices =  originalUnselectedIndices[m.name].ToList<int>();
        }

        int[] indices = originalIndices[m.name];        // original indices is set to be JUST the selected part, that's why nothing else is drawn
        List<Vector3> vertices = new List<Vector3>();
        mesh.GetVertices(vertices);

        List<Vector2> UVs = new List<Vector2>();
        mesh.GetUVs(0, UVs); //TODO: are there multiple channels?

        int numVertices = originalNumVertices[m.name];
        vertices.RemoveRange(numVertices, vertices.Count - numVertices);
        UVs.RemoveRange(numVertices, UVs.Count - numVertices);

        List<Vector3> transformedVertices = new List<Vector3>(vertices.Count);

        for (int i = 0; i < vertices.Count; i++)
        {
            transformedVertices.Add(m.gameObject.transform.TransformPoint(vertices[i]));
        }

        Vector3 intersectPoint0 = new Vector3();
        Vector3 intersectPoint1 = new Vector3();
        Vector3 intersectPoint2 = new Vector3();

        Vector2 intersectUV0 = new Vector2();
        Vector2 intersectUV1 = new Vector2();
        Vector2 intersectUV2 = new Vector2();

        int triangleIndex0;
        int triangleIndex1;
        int triangleIndex2;

        int intersect0Index;
        int intersect1Index;
        int intersect2Index;

        for (int planePass = 0; planePass < 2; planePass++)
        {
            GameObject currentPlane = leftPlane;
            if (planePass == 1)
            {
                currentPlane = rightPlane;
                indices = selectedIndices.ToArray();
                selectedIndices.Clear();
            }


            for (int i = 0; i < (int)(indices.Length / 3); i++)
            {
                triangleIndex0 = indices[3 * i];
                triangleIndex1 = indices[3 * i + 1];
                triangleIndex2 = indices[3 * i + 2];

                bool side0 = intersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex1], ref intersectPoint0, ref intersectUV0, UVs[triangleIndex0], UVs[triangleIndex1], vertices[triangleIndex0], vertices[triangleIndex1], currentPlane);
                bool side1 = intersectsWithPlane(transformedVertices[triangleIndex1], transformedVertices[triangleIndex2], ref intersectPoint1, ref intersectUV1, UVs[triangleIndex1], UVs[triangleIndex2], vertices[triangleIndex1], vertices[triangleIndex2], currentPlane);
                bool side2 = intersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex2], ref intersectPoint2, ref intersectUV2, UVs[triangleIndex0], UVs[triangleIndex2], vertices[triangleIndex0], vertices[triangleIndex2], currentPlane);


                if (!side0 && !side1 && !side2) // 0 intersections
                {
                    if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], currentPlane))
                    {
                        AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                    }
                    else
                    {
                        AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                    }
                }
                else
                {  // intersections have occurred
                   // determine which side of triangle has 1 vertex
                   // add vertex and indices to appropriate mesh
                   // for side with 2, add vertices, add 2 triangles
                    if (side0 && side1) // 2 intersections
                    {
                        intersect0Index = numVertices++;
                        intersect1Index = numVertices++;

                        vertices.Add(intersectPoint0);
                        vertices.Add(intersectPoint1);
                        
                        transformedVertices.Add(m.gameObject.transform.TransformPoint(intersectPoint0));
                        transformedVertices.Add(m.gameObject.transform.TransformPoint(intersectPoint1));
                        UVs.Add(intersectUV0);
                        UVs.Add(intersectUV1);

                        //AddToGraph(intersectPoint0, intersectPoint1, ref pointGraph);
                        outlinePoints.Add(intersectPoint0);
                        outlinePoints.Add(intersectPoint1);

                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex1], currentPlane))
                        {

                            // Add the indices for various triangles to selected and unselected

                            AddNewIndices(selectedIndices, intersect1Index, intersect0Index, triangleIndex1);
                            AddNewIndices(unselectedIndices, triangleIndex0, intersect0Index, intersect1Index);
                            AddNewIndices(unselectedIndices, triangleIndex2, triangleIndex0, intersect1Index);

                        }
                        else
                        {
                            AddNewIndices(unselectedIndices, intersect1Index, intersect0Index, triangleIndex1);
                            AddNewIndices(selectedIndices, triangleIndex0, intersect0Index, intersect1Index);
                            AddNewIndices(selectedIndices, triangleIndex2, triangleIndex0, intersect1Index);
                        }
                    }
                    else if (side0 && side2)
                    {
                        intersect0Index = numVertices++;
                        intersect2Index = numVertices++;

                        vertices.Add(intersectPoint0);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                        vertices.Add(intersectPoint2);
                        
                        transformedVertices.Add(m.gameObject.transform.TransformPoint(intersectPoint0));
                        transformedVertices.Add(m.gameObject.transform.TransformPoint(intersectPoint2));
                        UVs.Add(intersectUV0);
                        UVs.Add(intersectUV2);

                        outlinePoints.Add(intersectPoint0);
                        outlinePoints.Add(intersectPoint2);

                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], currentPlane))
                        {
                            AddNewIndices(selectedIndices, intersect2Index, triangleIndex0, intersect0Index);
                            AddNewIndices(unselectedIndices, triangleIndex2, intersect2Index, intersect0Index);
                            AddNewIndices(unselectedIndices, triangleIndex1, triangleIndex2, intersect0Index);
                        }
                        else
                        {
                            AddNewIndices(unselectedIndices, intersect2Index, triangleIndex0, intersect0Index);
                            AddNewIndices(selectedIndices, triangleIndex2, intersect2Index, intersect0Index);
                            AddNewIndices(selectedIndices, triangleIndex1, triangleIndex2, intersect0Index);
                        }
                    }
                    else if (side1 && side2)
                    {
                        intersect1Index = numVertices++;
                        intersect2Index = numVertices++;

                        vertices.Add(intersectPoint1);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                        vertices.Add(intersectPoint2);
                        
                        transformedVertices.Add(m.gameObject.transform.TransformPoint(intersectPoint1));
                        transformedVertices.Add(m.gameObject.transform.TransformPoint(intersectPoint2));
                        UVs.Add(intersectUV1);
                        UVs.Add(intersectUV2);

                        //AddToGraph(intersectPoint1, intersectPoint2, ref pointGraph);
                        outlinePoints.Add(intersectPoint1);
                        outlinePoints.Add(intersectPoint2);

                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex2], currentPlane))
                        {
                            AddNewIndices(selectedIndices, intersect1Index, triangleIndex2, intersect2Index);
                            AddNewIndices(unselectedIndices, intersect2Index, triangleIndex0, intersect1Index);
                            AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, intersect1Index);
                        }
                        else
                        {
                            AddNewIndices(unselectedIndices, intersect1Index, triangleIndex2, intersect2Index);
                            AddNewIndices(selectedIndices, intersect2Index, triangleIndex0, intersect1Index);
                            AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, intersect1Index);
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.Log("hit else case: " + side0 + " " + side1 + " " + side2);
                    }
                }
            }

            orderedUVs.Clear();
            triangulatedMeshPoints.Clear();
            
            Mesh outlineMesh = CreateHighlightMesh(outlinePoints, currentPlane);
            outlinePoints.Clear();

            if (planePass == 1)
            {
                rightOutlineMesh.GetComponent<MeshFilter>().mesh = outlineMesh;
                rightOutlineMesh.transform.position = m.transform.position;
                rightOutlineMesh.transform.localScale = m.transform.localScale;
                rightOutlineMesh.transform.rotation = m.transform.rotation;
            }
            else
            {
                leftOutlineMesh.GetComponent<MeshFilter>().mesh = outlineMesh;
                leftOutlineMesh.transform.position = m.transform.position;
                leftOutlineMesh.transform.localScale = m.transform.localScale;
                leftOutlineMesh.transform.rotation = m.transform.rotation;
            }
        }

        mesh.Clear();
        mesh.subMeshCount = 2;

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, UVs);
        
        mesh.SetTriangles(unselectedIndices, 0);
        mesh.SetTriangles(selectedIndices, 1);

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        Material[] materials = new Material[2];
        Material baseMaterial = m.GetComponent<Renderer>().materials[0];

        if (baseMaterial.name == "TransparentUnselected")
        {
            materials[0] = baseMaterial;
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
            materials[0] = transparentBase;
        }
        // May need to specify which submesh we get this from?
        materials[1] = Resources.Load("Selected") as Material;
        // materials[2] = Resources.Load("Unselected") as Material;
        m.GetComponent<Renderer>().materials = materials;


      //  Debug.Log("Submesh count: " + mesh.subMeshCount);
    }

    /**
     * points contains a list of points where each successive pair of points gets a tube drawn between them
     * */
    private Mesh CreateHighlightMesh(List<Vector3> points, GameObject plane)
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> faces = new List<int>();
        List<Vector2> uvCoordinates = new List<Vector2>();

        float radius = .005f;

        Assert.IsTrue(points.Count % 2 == 0);

        for (int i = 0; i < points.Count; i+=2)
        {
            Vector3 centerStart = points[i];
            Vector3 centerEnd = points[i + 1];
            Vector3 direction = centerEnd - centerStart;
            Vector3 right = Vector3.Cross(plane.transform.up, direction);
            Vector3 up = Vector3.Cross(direction, right);
            up = up.normalized * radius;
            right = right.normalized * radius;

            int numSections = 10;

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
                    faces.Add((slice*2 + 1) + ((numSections + 1) * i));
                    faces.Add((slice*2) + ((numSections + 1) * i ));
                    faces.Add((slice*2 -2) + ((numSections + 1) * i));

                    faces.Add(slice*2+1 + ((numSections + 1) * i));
                    faces.Add(slice*2-2 + ((numSections + 1) *i));
                    faces.Add(slice*2-1 + ((numSections + 1) * i));
                }
            }
        }
        Mesh selectorMesh = new Mesh();
        selectorMesh.vertices = verts.ToArray();
        selectorMesh.uv = uvCoordinates.ToArray();
        selectorMesh.triangles = faces.ToArray();

        selectorMesh.RecalculateBounds();
        selectorMesh.RecalculateNormals();

        return selectorMesh;
    }

    // returns value of latest index added and adds to list
    private void AddNewIndices(List<int> indices, int numToAdd)
    {
        for (int i = 0; i < numToAdd; i++)
        {
            int latestIndex = indices.Count;
            indices.Add(latestIndex);
        }
    }

    // Adds a triangle with predefined indices into a list of indices
    private void AddNewIndices(List<int> indices, int index0, int index1, int index2)
    {
        indices.Add(index0);
        indices.Add(index1);
        indices.Add(index2);
    }

    private bool intersectsWithPlane(Vector3 lineVertexWorld0, Vector3 lineVertexWorld1, ref Vector3 intersectPoint, ref Vector2 intersectUV, Vector2 vertex0UV, Vector2 vertex1UV, Vector3 lineVertexLocal0, Vector3 lineVertexLocal1, GameObject plane) // checks if a particular edge intersects with the plane, if true, returns point of intersection along edge
    {
        Vector3 lineSegmentLocal = lineVertexLocal1 - lineVertexLocal0;
        float dot = Vector3.Dot(plane.transform.up, lineVertexWorld1 - lineVertexWorld0);
        Vector3 w = plane.transform.position - lineVertexWorld0;


        float epsilon = 0.001f;
        if (Mathf.Abs(dot) > epsilon)
        {
            float factor = Vector3.Dot(plane.transform.up, w) / dot;
            if (factor >= 0f && factor <= 1f)
            {
                lineSegmentLocal = factor * lineSegmentLocal;
                intersectPoint = lineVertexLocal0 + lineSegmentLocal;
                intersectUV = vertex0UV + factor * (vertex1UV - vertex0UV);

                return true;

            }
        }
        return false;
    }

    // Orders the points of one mesh. NOTE: currently just uses alreadyVisited HashSet, nothing else;
    List<Vector3> DFSOrderPoints(Dictionary<Vector3, HashSet<Vector3>> pointConnections)
    {
        HashSet<Vector3> alreadyVisited = new HashSet<Vector3>();
        List<Vector3> orderedPoints = new List<Vector3>();

        foreach (Vector3 pt in pointConnections.Keys)
        {
            if (!alreadyVisited.Contains(pt))
            {
                //TODO: make a new list for ordered points here to pass in
                DFSVisit(pt, pointConnections, ref alreadyVisited, ref orderedPoints);
            }
        }

        return orderedPoints;
    }

    // Basic BFS, adds the intersection points of edges in the order it visits them
    void DFSVisit(Vector3 pt, Dictionary<Vector3, HashSet<Vector3>> connectedEdges, ref HashSet<Vector3> alreadyVisited, ref List<Vector3> orderedPoints)
    {
        alreadyVisited.Add(pt);
        orderedPoints.Add(pt);
        
        foreach (Vector3 otherIndex in connectedEdges[pt])
        {
            if (!alreadyVisited.Contains(otherIndex))
            {
                
                DFSVisit(otherIndex, connectedEdges, ref alreadyVisited, ref orderedPoints);
            }
        }
        
    }

    // Takes two connected points and adds or updates entries in the list of actual points and the graph of their connections
    private void AddToGraph(Vector3 point0, Vector3 point1, ref Dictionary<Vector3, HashSet<Vector3>> pointConnections)
    {

        if (!pointConnections.ContainsKey(point0))
        {
            HashSet<Vector3> connections = new HashSet<Vector3>();
            connections.Add(point1);
            pointConnections.Add(point0, connections);
        }
        else
        {
            pointConnections[point0].Add(point1);
        }

        if (!pointConnections.ContainsKey(point1))
        {
            HashSet<Vector3> connections = new HashSet<Vector3>();
            connections.Add(point0);
            pointConnections.Add(point1, connections);
        }
        else
        {
            pointConnections[point1].Add(point0);
        }

    }

    private List<Vector3> RemoveSequentialDuplicates(List<Vector3> points)
    {
        List<Vector3> output = new List<Vector3>(points.Count);
        List<Vector3>.Enumerator it = points.GetEnumerator();
        it.MoveNext();
        Vector3 prev = it.Current;
        while (it.MoveNext())
        {
            if (!PlaneCollision.ApproximatelyEquals(it.Current, prev))
            {
                output.Add(it.Current);
            }
            prev = it.Current;
        }
        return output;
    }

    Vector2 GetUVFromPoint(Vector3 point, GameObject plane)
    {
        Vector3 centerOfPlane = plane.transform.position;                                                                                   //
        float sideLength = (10 * plane.transform.localScale.x) / 2;                                                                         //
                                                                                                                                            //
        corner0 = centerOfPlane - sideLength * plane.transform.right.normalized - sideLength * plane.transform.forward.normalized;  // Definitely need to assign vars here, but might 
        corner1 = centerOfPlane + sideLength * plane.transform.right.normalized - sideLength * plane.transform.forward.normalized;  // want to declare them in Start()
        corner2 = centerOfPlane - sideLength * plane.transform.right.normalized + sideLength * plane.transform.forward.normalized;  //
        
        Vector2 uv = new Vector2();

        Vector3 uVec = corner1 - corner0;
        Vector3 vVec = corner0 - corner2;

        float uLength = uVec.magnitude;
        float vLength = vVec.magnitude;
        uVec = Vector3.Normalize(uVec);
        vVec = Vector3.Normalize(vVec);

        uv.x = Vector3.Dot(point - corner0, uVec);
        uv.y = Vector3.Dot(point - corner2, vVec);
        
        return uv;
    }

    Vector3 GetPointFromUV(Vector2 uv, GameObject plane)
    {
        Vector3 centerOfPlane = plane.transform.position;                                                                                   //
        float sideLength = (10 * plane.transform.localScale.x) / 2;                                                                         //
                                                                                                                                            //
        Vector3 corner0 = centerOfPlane - sideLength * plane.transform.right.normalized - sideLength * plane.transform.forward.normalized;  // Definitely need to assign vars here, but might 
        Vector3 corner1 = centerOfPlane + sideLength * plane.transform.right.normalized - sideLength * plane.transform.forward.normalized;  // want to declare them in Start()
        Vector3 corner2 = centerOfPlane - sideLength * plane.transform.right.normalized + sideLength * plane.transform.forward.normalized;  //

        Vector3 uVec = corner1 - corner0;
        Vector3 vVec = corner0 - corner2;

        Vector3 multiplied_uVec = new Vector3(uv.x * uVec.x, uv.x * uVec.y, uv.x * uVec.z);
        Vector3 multiplied_vVec = new Vector3(uv.y * vVec.x, uv.y * vVec.y, uv.y * vVec.z);

        return (corner2 + multiplied_uVec + multiplied_vVec);
    }
}
