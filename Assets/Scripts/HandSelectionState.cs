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

    private static int highlightObjectCount = 0;

    private List<Collider> collidedMeshes;
    private HashSet<Collider> cubeColliders;
    private HashSet<Collider> leftColliders;
    private HashSet<Collider> rightColliders;
    CubeCollision leftComponent;
    CubeCollision rightComponent;
    CubeCollision centerComponent;

    private Dictionary<string, int[]> originalUnselectedIndices;
    private static Dictionary<string, int> originalNumVertices;   // Key = name of object with mesh, Value = original set of vertices
    private static Dictionary<string, int[]> originalSelectedIndices; // key = name of object with mesh, value = original indices for the entire object
    private static HashSet<string> selectedMeshes; // Collection of the the names of all the meshes that have had pieces selected from them.

    //private Dictionary<Vector3, HashSet<Vector3>> pointGraph;   // Graph of selected points
    private List<Vector3> outlinePoints;    // Pairs of two connected points to be used in drawing an outline mesh

    List<int> selectedIndices;
    List<int> unselectedIndices;

    Vector3 corner0;
    Vector3 corner1;
    Vector3 corner2;

    GameObject leftOutlineMeshObject;
    GameObject rightOutlineMeshObject;
    Mesh leftOutlineMesh;
    Mesh rightOutlineMesh;

    private Stopwatch watch;

    public static Dictionary<string, int[]> OriginalIndices
    {
        get { return originalSelectedIndices; }
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
        originalSelectedIndices = new Dictionary<string, int[]>();
        selectedIndices = new List<int>();
        unselectedIndices = new List<int>();

        //pointGraph = new Dictionary<Vector3, HashSet<Vector3>>();   // graph of selected points
        outlinePoints = new List<Vector3>();

        this.stateToReturnTo = stateToReturnTo;

        corner0 = new Vector3();
        corner1 = new Vector3();
        corner2 = new Vector3();

        watch = new Stopwatch();

        leftOutlineMeshObject = new GameObject();
        leftOutlineMeshObject.name = "Left outline";
        leftOutlineMeshObject.AddComponent<MeshRenderer>();
        leftOutlineMeshObject.AddComponent<MeshFilter>();
        leftOutlineMeshObject.GetComponent<MeshFilter>().mesh.MarkDynamic();
        leftOutlineMeshObject.GetComponent<Renderer>().material = Resources.Load("TestMaterial") as Material;

        rightOutlineMeshObject = new GameObject();
        rightOutlineMeshObject.name = "Right outline";
        rightOutlineMeshObject.AddComponent<MeshRenderer>();
        rightOutlineMeshObject.AddComponent<MeshFilter>();
        rightOutlineMeshObject.GetComponent<MeshFilter>().mesh.MarkDynamic();
        rightOutlineMeshObject.GetComponent<Renderer>().material = Resources.Load("TestMaterial") as Material;

        leftOutlineMesh = new Mesh();
        rightOutlineMesh = new Mesh();
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

        leftOutlineMeshObject.GetComponent<MeshRenderer>().enabled = false;
        rightOutlineMeshObject.GetComponent<MeshRenderer>().enabled = false;

        UnityEngine.Debug.Log("Number meshes in collided meshes: " + collidedMeshes.Count);
        int[] indices;
        foreach (Collider c in collidedMeshes)
        {
            Mesh mesh = c.GetComponent<MeshFilter>().mesh;
            mesh.subMeshCount = 2;
            indices = originalSelectedIndices[c.name];
            UnityEngine.Debug.Log("original indices: " + originalSelectedIndices[c.name].Length);
           // UnityEngine.Debug.Log("original unselected indices: " + originalUnselectedIndices[c.name].Length); // only gets added to if clicked
            if (selectedMeshes.Contains(c.name))    // If it previously had a piece selected (CLICKED) - use originalUnselectedIndices for unselected part, everything else should be what was selected at last click
            {
                // Generate a mesh to fill the entire selected part of the collider
                UnityEngine.Debug.Log("selectedMeshes contains this mesh");
                Vector3[] verts = mesh.vertices;
                List<Vector2> uvs = new List<Vector2>();
                mesh.GetUVs(0, uvs);

                mesh.Clear();
                mesh.vertices = verts;
                mesh.SetUVs(0, uvs);

                if (c.tag != "highlightmesh")
                {
                    mesh.subMeshCount = 2;

                    mesh.SetTriangles(originalUnselectedIndices[c.name], 0);
                    mesh.SetTriangles(indices, 1);
                }
                else
                {
                    mesh.subMeshCount = 1;
                    mesh.SetTriangles(indices, 0);
                }

                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
            }
            else // NOT CLICKED - everything should be unselected using originalIndices
            {
                if (c.tag != "highlightmesh")
                {
                    Material baseMaterial = c.GetComponent<Renderer>().materials[0];
                    baseMaterial.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);// TODO: refactor this using other code (we set the mesh so. many. times.)
                    c.GetComponent<Renderer>().materials[1] = baseMaterial;
                }
            }
        }
    }

    public override void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        UpdatePlanes();

        // Take input from cube about what it collides with
        cubeColliders = centerComponent.CollidedObjects;
        
        if (cubeColliders.Count > 0)
        {
            collidedMeshes.Clear();
            collidedMeshes = cubeColliders.ToList();
        }
        else
        {
            //TODO: revert to last selection (leaving state without clicking)

            GameObject.Find("UIController").GetComponent<UIController>().ChangeState(stateToReturnTo);
            return;
            // If not colliding with anything, go back to navigationstate or wherever
        }

        foreach (Collider m in collidedMeshes)
        {
            if (!originalNumVertices.ContainsKey(m.name))
            {
                originalNumVertices.Add(m.name, m.GetComponent<MeshFilter>().mesh.vertices.Length);        // Maybe want to store vertices as array instead?

                m.GetComponent<MeshFilter>().mesh.MarkDynamic();
                originalSelectedIndices.Add(m.name, m.GetComponent<MeshFilter>().mesh.GetIndices(0));
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
                originalSelectedIndices[m.name] = m.GetComponent<MeshFilter>().mesh.GetIndices(submeshNum);     // updates original indices to store the the most recently selected portion
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
            GameObject savedLeftHighlight = copyObject(leftOutlineMeshObject);
            GameObject savedRightHighlight = copyObject(rightOutlineMeshObject);
            //collidedMeshes.Add(savedLeftHighlight.GetComponent<Collider>());
            //collidedMeshes.Add(savedRightHighlight.GetComponent<Collider>());
        }
    }

    private GameObject copyObject(GameObject original)
    {
        GameObject copy = new GameObject();
        copy.AddComponent<MeshRenderer>();
        copy.AddComponent<MeshFilter>();
        copy.transform.position = original.transform.position;
        copy.transform.rotation = original.transform.rotation;
        copy.transform.localScale = original.transform.localScale;
        copy.GetComponent<MeshRenderer>().material = original.GetComponent<MeshRenderer>().material;
        copy.GetComponent<MeshFilter>().mesh = original.GetComponent<MeshFilter>().mesh;
        copy.AddComponent<MeshCollider>();
        copy.tag = "highlightmesh";
        copy.name = "highlight" + highlightObjectCount;
        highlightObjectCount++;

        return copy;
    }

    private bool OnNormalSideOfPlane(Vector3 pt, GameObject plane)
    {
        return Vector3.Dot(plane.transform.up, pt) >= Vector3.Dot(plane.transform.up, plane.transform.position);
    }

    private void CenterCubeBetweenControllers()
    {
        // position cube at midpoint between controllers
        Vector3 leftPosition = leftPlane.transform.position;
        Vector3 rightPosition = rightPlane.transform.position;

        Vector3 halfWayBtwHands = Vector3.Lerp(leftPosition, rightPosition, 0.5f);
        centerCube.transform.position = halfWayBtwHands;

        // rotate cube w/ respect to both controllers
        RotatePlane(controller0, controller1, leftPosition, rightPosition, centerCube);

        // scale cube
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

        int[] indices = originalSelectedIndices[m.name];        // original indices is set to be JUST the selected part, that's why nothing else is drawn
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

        int intersectIndex0;
        int intersectIndex1;
        int intersectIndex2;

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
                        intersectIndex0 = numVertices++;
                        intersectIndex1 = numVertices++;

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

                            AddNewIndices(selectedIndices, intersectIndex1, intersectIndex0, triangleIndex1);
                            AddNewIndices(unselectedIndices, triangleIndex0, intersectIndex0, intersectIndex1);
                            AddNewIndices(unselectedIndices, triangleIndex2, triangleIndex0, intersectIndex1);

                        }
                        else
                        {
                            AddNewIndices(unselectedIndices, intersectIndex1, intersectIndex0, triangleIndex1);
                            AddNewIndices(selectedIndices, triangleIndex0, intersectIndex0, intersectIndex1);
                            AddNewIndices(selectedIndices, triangleIndex2, triangleIndex0, intersectIndex1);
                        }
                    }
                    else if (side0 && side2)
                    {
                        intersectIndex0 = numVertices++;
                        intersectIndex2 = numVertices++;

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
                            AddNewIndices(selectedIndices, intersectIndex2, triangleIndex0, intersectIndex0);
                            AddNewIndices(unselectedIndices, triangleIndex2, intersectIndex2, intersectIndex0);
                            AddNewIndices(unselectedIndices, triangleIndex1, triangleIndex2, intersectIndex0);
                        }
                        else
                        {
                            AddNewIndices(unselectedIndices, intersectIndex2, triangleIndex0, intersectIndex0);
                            AddNewIndices(selectedIndices, triangleIndex2, intersectIndex2, intersectIndex0);
                            AddNewIndices(selectedIndices, triangleIndex1, triangleIndex2, intersectIndex0);
                        }
                    }
                    else if (side1 && side2)
                    {
                        intersectIndex1 = numVertices++;
                        intersectIndex2 = numVertices++;

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
                            AddNewIndices(selectedIndices, intersectIndex1, triangleIndex2, intersectIndex2);
                            AddNewIndices(unselectedIndices, intersectIndex2, triangleIndex0, intersectIndex1);
                            AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, intersectIndex1);
                        }
                        else
                        {
                            AddNewIndices(unselectedIndices, intersectIndex1, triangleIndex2, intersectIndex2);
                            AddNewIndices(selectedIndices, intersectIndex2, triangleIndex0, intersectIndex1);
                            AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, intersectIndex1);
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.Log("hit else case: " + side0 + " " + side1 + " " + side2);
                    }
                }
            }

            if (m.gameObject.tag != "highlightmesh")
            {
                CreateHighlightMesh(outlinePoints, currentPlane);
            }
            outlinePoints.Clear();

            if (planePass == 1)
            {
                rightOutlineMeshObject.GetComponent<MeshFilter>().mesh = rightOutlineMesh;
                rightOutlineMeshObject.transform.position = m.transform.position;
                rightOutlineMeshObject.transform.localScale = m.transform.localScale;
                rightOutlineMeshObject.transform.rotation = m.transform.rotation;
            }
            else
            {
                leftOutlineMeshObject.GetComponent<MeshFilter>().mesh = leftOutlineMesh;
                leftOutlineMeshObject.transform.position = m.transform.position;
                leftOutlineMeshObject.transform.localScale = m.transform.localScale;
                leftOutlineMeshObject.transform.rotation = m.transform.rotation;
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, UVs);

        if (m.gameObject.tag != "highlightmesh")
        {
            mesh.subMeshCount = 2;

            mesh.SetTriangles(unselectedIndices, 0);
            mesh.SetTriangles(selectedIndices, 1);

            Material[] materials = new Material[2];
            Material baseMaterial = m.GetComponent<Renderer>().materials[0];
            materials[0] = DetermineBaseMaterial(baseMaterial);
            materials[1] = Resources.Load("Selected") as Material;      // May need to specify which submesh we get this from?
            m.GetComponent<Renderer>().materials = materials;
        }
        else
        {
            mesh.subMeshCount = 1;
            mesh.SetTriangles(selectedIndices, 0);
        }
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    /**
     * points contains a list of points where each successive pair of points gets a tube drawn between them, sets to mesh called selectorMesh
     * */
    private void CreateHighlightMesh(List<Vector3> points, GameObject plane)
    {
        //UnityEngine.Debug.Log("Num points in createhighlightmesh: " + points.Count);

        List<Vector3> verts = new List<Vector3>();
        List<int> faces = new List<int>();
        List<Vector2> uvCoordinates = new List<Vector2>();

        float radius = .005f;
        int numSections = 6;

        Assert.IsTrue(points.Count % 2 == 0);
        int expectedNumVerts = (numSections + 1) * points.Count;
        //UnityEngine.Debug.Log("EXPECTING " + expectedNumVerts + " VERTICES");
        if (expectedNumVerts > 65000)
        {
            //UnityEngine.Debug.Log("Size of points before processing: " + points.Count);
            points = OrderMesh(points);
            //UnityEngine.Debug.Log("Size of points after processing: " + points.Count);
        }

        for (int i = 0; i < points.Count; i+=2)
        {
            Vector3 centerStart = points[i];
            Vector3 centerEnd = points[i + 1];
            Vector3 direction = centerEnd - centerStart;
            direction = direction.normalized;
            Vector3 right = Vector3.Cross(plane.transform.up, direction);
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
                    faces.Add((slice*2 + 1) + ((numSections + 1) * i));
                    faces.Add((slice*2) + ((numSections + 1) * i ));
                    faces.Add((slice*2 -2) + ((numSections + 1) * i));

                    faces.Add(slice*2+1 + ((numSections + 1) * i));
                    faces.Add(slice*2-2 + ((numSections + 1) *i));
                    faces.Add(slice*2-1 + ((numSections + 1) * i));
                }
            }
        }
        //UnityEngine.Debug.Log("Num verts generated for outline mesh: " + verts.Count);
        //UnityEngine.Debug.Log("Expected num verts: " + expectedNumVerts);
        //Mesh selectorMesh = new Mesh();
        if (plane.Equals(leftPlane))
        {
            leftOutlineMesh.Clear();
            leftOutlineMesh.SetVertices(verts);        // verts too large
            leftOutlineMesh.SetUVs(0, uvCoordinates);
            leftOutlineMesh.SetTriangles(faces, 0);

            leftOutlineMesh.RecalculateBounds();
            leftOutlineMesh.RecalculateNormals();
        }
        else
        {
            rightOutlineMesh.Clear();
            rightOutlineMesh.SetVertices(verts);        // verts too large
            rightOutlineMesh.SetUVs(0, uvCoordinates);
            rightOutlineMesh.SetTriangles(faces, 0);

            rightOutlineMesh.RecalculateBounds();
            rightOutlineMesh.RecalculateNormals();
        }

        return; //selectorMesh;
    }

    /**
     * Make a graph of mesh vertices, order it, remove sequential duplicates and return new set of vertices
     */
    private List<Vector3> OrderMesh(List<Vector3> meshVertices)
    {
        Dictionary<Vector3, HashSet<Vector3>> vertexGraph = new Dictionary<Vector3, HashSet<Vector3>>();  // Each point should only be connected to two other points

        for (int i = 0; i < meshVertices.Count; i += 2)
        {
            AddToGraph(meshVertices[i], meshVertices[i + 1], ref vertexGraph);
        }

        meshVertices = DFSOrderPoints(vertexGraph);
        //UnityEngine.Debug.Log("size of meshVertices before removing duplicates: " + meshVertices.Count);
        meshVertices = RemoveSequentialDuplicates(meshVertices);
        //UnityEngine.Debug.Log("Size after removing duplicates: " + meshVertices.Count);

        return meshVertices;
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

    // Basic DFS, adds the intersection points of edges in the order it visits them
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
     //   int lastAllEqualIndex = -1; // Keeps track of earliest index when all points are equal, reset to -1 when connected up
        int i = 1;
        while (i < points.Count - 1)
        {
            bool firstTwoEqual = PlaneCollision.ApproximatelyEquals(points[i-1], points[i]);
            bool secondTwoEqual = PlaneCollision.ApproximatelyEquals(points[i], points[i + 1]);
            
            if (firstTwoEqual && secondTwoEqual)
            {
                //lastAllEqualIndex = i;
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
        return output;
        //for (int i = 0; i < points.Count-1; i += 3)
        //{
        //    bool firstTwoEqual = PlaneCollision.ApproximatelyEquals(points[i], points[i+1]);
        //    bool secondTwoEqual = PlaneCollision.ApproximatelyEquals(points[i+1], points[i + 2]);

        //    if (firstTwoEqual && secondTwoEqual)
        //    {
        //        lastAllEqualIndex = i;
        //        //output.Add(points[i]);
        //        //output.Add(points[i + 2]);  // Any better solution?
        //    }
        //    else if (firstTwoEqual && !secondTwoEqual)
        //    {
        //        output.Add(points[i]);      // Add one of the equal points
        //        output.Add(points[i + 1]);

        //    }
        //    else if (!firstTwoEqual && secondTwoEqual)
        //    {
        //        output.Add(points[i]);
        //        output.Add(points[i + 2]); // Add one of the equal points
        //    }
        //    else  // All are distsinct
        //    {
        //        output.Add(points[i]);      // Add first two
        //        output.Add(points[i + 1]);  //
        //        i--;
        //    }
        //}
        //List<Vector3>.Enumerator it = points.GetEnumerator();
        //it.MoveNext();
        //Vector3 prev = it.Current;
        //while (it.MoveNext())
        //{
        //    if (!PlaneCollision.ApproximatelyEquals(it.Current, prev))
        //    {
        //        output.Add(it.Current);
        //    }
        //    prev = it.Current;
        //}
        //return output;
    }

    Vector2 GetUVFromPoint(Vector3 point, GameObject plane)
    {
        Vector3 centerOfPlane = plane.transform.position;                                                                                   
        float sideLength = (10 * plane.transform.localScale.x) / 2;                                                                         
                                                                                                                                            
        corner0 = centerOfPlane - sideLength * plane.transform.right.normalized - sideLength * plane.transform.forward.normalized; 
        corner1 = centerOfPlane + sideLength * plane.transform.right.normalized - sideLength * plane.transform.forward.normalized;  
        corner2 = centerOfPlane - sideLength * plane.transform.right.normalized + sideLength * plane.transform.forward.normalized; 
        
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
}