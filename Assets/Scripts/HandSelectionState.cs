using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public class HandSelectionState : InteractionState
{
    private const bool debug = false;

    InteractionState stateToReturnTo;
    private ControllerInfo controller0;
    private ControllerInfo controller1;

    private GameObject leftPlane;   //
    private GameObject rightPlane;  // Used to detect collision with meshes in the model
    private GameObject centerCube;  //

    private int planeLayer;                         // Layer that cube and planes are on
    private static int highlightObjectCount = 0;    // Keeps saved outlines distinguishable from one another

    private List<GameObject> collidedMeshes;        // List of meshes currently being collided with
    private HashSet<GameObject> cubeColliders;      // All the objects the cube is colliding with
    CubeCollision centerComponent;                  // Script on cube that we get the list of colliders from

    private Dictionary<string, int[]> originalUnselectedIndices;                // Key = name of object with mesh, Value = all indices that have not been selected (updated when user clicks)
    private static Dictionary<string, int> originalNumVertices;                 // Key = name of object with mesh, Value = original set of vertices (updated when user clicks and mesh is split)
    private static Dictionary<string, int[]> originalSelectedIndices;           // key = name of object with mesh, value = original set of selected indices (updated when user clicks)
    private static HashSet<string> selectedMeshes;                              // Collection of the the names of all the meshes that have had pieces selected from them.
    private static Dictionary<string, HashSet<GameObject>> modelHighlights;     // Key = name of object in model, Value = all the SAVED outline game objects attached to it
    private static Dictionary<string, GameObject> leftOutlines;                 // left hand outlines per model that are currently being manipulated (KEY = name of model object, VALUE = outline object)
    private static Dictionary<string, GameObject> rightOutlines;                // right hand outlines per model that are currently being manipulated (KEY = name of model object, VALUE = outline object)

    private List<Vector3> outlinePoints;    // Pairs of two connected points to be used in drawing an outline mesh

    List<int> selectedIndices;      // Reused for each mesh during ProcessMesh()
    List<int> unselectedIndices;    // ^^^^
    
    Mesh leftOutlineMesh;       // Reused to draw highlights that move with left hand
    Mesh rightOutlineMesh;      // Reused to draw highlights that move with right hand

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
    public static Dictionary<string, HashSet<GameObject>> ModelHighlights
    {
        get { return modelHighlights; }
        set { modelHighlights = value; }
    }
    public static Dictionary<string, GameObject> LeftOutlines
    {
        get { return leftOutlines; }
    }
    public static Dictionary<string, GameObject> RightOutlines
    {
        get { return rightOutlines; }
    }

    /// <summary>
    /// State that activates whenever there's a mesh between the user's controllers. Allows user to select surfaces and progressively refine their selection.
    /// Currently only works when selecting a single object.
    /// </summary>
    /// <param name="controller0Info"></param>
    /// <param name="controller1Info"></param>
    /// <param name="stateToReturnTo"></param>
    public HandSelectionState(ControllerInfo controller0Info, ControllerInfo controller1Info, InteractionState stateToReturnTo) 
    {
        // NOTE: Selecting more than one mesh will result in highlights appearing in the wrong place
        desc = "HandSelectionState";
        controller0 = controller0Info;
        controller1 = controller1Info;

        planeLayer = LayerMask.NameToLayer("PlaneLayer");

        leftPlane = CreateHandPlane(controller0, "handSelectionLeftPlane");
        rightPlane = CreateHandPlane(controller1, "handSelectionRightPlane");

        //The center cube is anchored between controllers and detects collisions with other objects
        centerCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        centerCube.name = "handSelectionCenterCube";
        centerCube.GetComponent<Renderer>().material = Resources.Load("Cube Material") as Material;
        centerCube.AddComponent<MeshCollider>();
        centerCube.GetComponent<MeshCollider>().convex = true;
        centerCube.GetComponent<MeshCollider>().isTrigger = true;
        centerCube.AddComponent<Rigidbody>();
        centerCube.GetComponent<Rigidbody>().isKinematic = true;
        centerComponent = centerCube.AddComponent<CubeCollision>();
        centerCube.layer = planeLayer;
        if (!debug)
        {
            centerCube.GetComponent<MeshRenderer>().enabled = false;
        }

        collidedMeshes = new List<GameObject>();      
        cubeColliders = new HashSet<GameObject>();
     
        //TODO: should these persist between states? Yes so only make one instance of the state. Should use the Singleton pattern here//TODO

        selectedMeshes = new HashSet<string>();
        originalNumVertices = new Dictionary<string, int>();        // Keeps track of how many vertices a mesh should have
        originalUnselectedIndices = new Dictionary<string, int[]>();      // Keeps track of indices that were previously unselected
        originalSelectedIndices = new Dictionary<string, int[]>();
        modelHighlights = new Dictionary<string, HashSet<GameObject>>();
        selectedIndices = new List<int>();
        unselectedIndices = new List<int>();
        outlinePoints = new List<Vector3>();

        this.stateToReturnTo = stateToReturnTo;

        leftOutlines = new Dictionary<string, GameObject>();
        rightOutlines = new Dictionary<string, GameObject>();

        leftOutlineMesh = new Mesh();
        rightOutlineMesh = new Mesh();
    }

    /// <summary>
    /// Sets up the planes that follow each hand/controller
    /// </summary>
    /// <param name="c"></param>
    /// <param name="name"></param>
    /// <returns></returns>
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

        handPlane.transform.position = c.controller.transform.position;
        handPlane.transform.rotation = c.controller.transform.rotation;
        handPlane.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);//Previously 0.03

        handPlane.layer = planeLayer;
        if (!debug)
        {
            handPlane.GetComponent<MeshRenderer>().enabled = false;
        }
        return handPlane;
    }
    /// <summary>
    /// Adjusts position of planes and cube.
    /// </summary>
    public void UpdatePlanes()
    {
        leftPlane.transform.position = controller0.controller.transform.position;
        rightPlane.transform.position = controller1.controller.transform.position;
        /*
        Vector3 up = (rightPlane.transform.position - leftPlane.transform.position).normalized;
        Vector3 right = Vector3.Cross(up, Vector3.up).normalized;
        Vector3 forward = Vector3.Cross(up, right).normalized;

        leftPlane.transform.up = up;
        leftPlane.transform.right = right;
        leftPlane.transform.forward = forward;

        rightPlane.transform.up = -up;
        rightPlane.transform.right = right;
        rightPlane.transform.forward = -forward;
        */

        leftPlane.transform.up = (rightPlane.transform.position - leftPlane.transform.position).normalized; // the normals of both planes are always facing each other
        rightPlane.transform.up = (leftPlane.transform.position - rightPlane.transform.position).normalized;


        CenterCubeBetweenControllers();
    }

    private void CenterCubeBetweenControllers()
    {
        // position cube at midpoint between controllers
        Vector3 leftPosition = leftPlane.transform.position;
        Vector3 rightPosition = rightPlane.transform.position;

        Vector3 halfWayBtwHands = Vector3.Lerp(leftPosition, rightPosition, 0.5f);
        centerCube.transform.position = halfWayBtwHands;

        // rotate cube w/ respect to both controllers
        RotateCube(controller0, controller1, leftPosition, rightPosition, centerCube);

        // scale cube
        float distance = Vector3.Distance(rightPosition, leftPosition);
        centerCube.transform.localScale = new Vector3(1f, 0, 0) * distance + new Vector3(0, 0.3f, 0.3f);
    }

    private void RotateCube(ControllerInfo controller0Info, ControllerInfo controller1Info, Vector3 leftPos, Vector3 rightPos, GameObject cube)
    {
        Vector3 xAxis = (rightPos - leftPos).normalized;

        Vector3 zAxis = controller0Info.isLeft ? controller1Info.trackedObj.transform.forward : controller0Info.trackedObj.transform.forward;
        zAxis = (zAxis - (Vector3.Dot(zAxis, xAxis) * xAxis)).normalized;
        Vector3 yAxis = Vector3.Cross(zAxis, xAxis).normalized;

        Vector3 groundY = new Vector3(0, 1);

        //float controllerToGroundY = Vector3.Angle(yAxis, groundY);
        cube.transform.rotation = Quaternion.LookRotation(zAxis, yAxis);
    }

    public override void Deactivate()
    {
        
        controller0.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;    // Disable hand rendering
        controller1.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;    //

        controller0.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true); // Enable rendering of controllers
        controller1.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true); //
        

        int[] indices;
        foreach (GameObject c in collidedMeshes)
        {
            Mesh mesh = c.GetComponent<MeshFilter>().mesh;
            mesh.subMeshCount = 2;
            indices = originalSelectedIndices[c.name]; // the indices of last selection
  
            if (selectedMeshes.Contains(c.name))    // If it previously had a piece selected (CLICKED) - revert to that selection
            {
                // Generate a mesh to fill the entire selected part of the collider
                Vector3[] verts = mesh.vertices;
                List<Vector2> uvs = new List<Vector2>();
                mesh.GetUVs(0, uvs);

                mesh.Clear();
                mesh.vertices = verts;
                mesh.SetUVs(0, uvs);

                if (c.tag != "highlightmesh") // set unselected and selected regions back to what they were at the last click
                {
                    mesh.subMeshCount = 2;
                    mesh.SetTriangles(originalUnselectedIndices[c.name], 0);
                    mesh.SetTriangles(indices, 1);
                }
                else // for meshes that are highlights, use only one material (unselected will not be drawn)
                {
                    mesh.subMeshCount = 1;
                    mesh.SetTriangles(indices, 0);
                }

                mesh.RecalculateBounds();
                mesh.RecalculateNormals();

                // Go through each highlight associated with the current mesh object and reset it
                foreach (GameObject highlight in modelHighlights[c.name])
                {
                    Mesh highlightMesh = highlight.GetComponent<MeshFilter>().mesh;
                    Vector3[] highlightVerts = highlightMesh.vertices;
                    List<Vector2> highlightUVs = new List<Vector2>();
                    highlightMesh.GetUVs(0, highlightUVs);

                    highlightMesh.Clear();
                    highlightMesh.vertices = highlightVerts;
                    highlightMesh.SetUVs(0, highlightUVs);
                    
                    highlightMesh.subMeshCount = 1;
                    highlightMesh.SetTriangles(originalSelectedIndices[highlight.name], 0);
                    
                    highlightMesh.RecalculateBounds();
                    highlightMesh.RecalculateNormals();
                }
            }
            else // NOT CLICKED 
            {
                // reset object to original state (before interaction)
                if (c.tag != "highlightmesh")
                {
                    Material baseMaterial = c.GetComponent<Renderer>().materials[0];
                    baseMaterial.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                    c.GetComponent<Renderer>().materials[1] = baseMaterial;
                }
            }
            //stop rendering current outline
            if (leftOutlines.ContainsKey(c.name) || rightOutlines.ContainsKey(c.name))
            {
                leftOutlines[c.name].GetComponent<MeshRenderer>().enabled = false;
                rightOutlines[c.name].GetComponent<MeshRenderer>().enabled = false;
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
        else // If not colliding with anything, change states
        { 
            GameObject.Find("UIController").GetComponent<UIController>().ChangeState(stateToReturnTo);
            return;    
        }

        foreach (GameObject m in collidedMeshes)
        {
            if (!originalNumVertices.ContainsKey(m.name)) // if the original vertices are not stored already, store them (first time seeing object)
            {
                originalNumVertices.Add(m.name, m.GetComponent<MeshFilter>().mesh.vertices.Length);
                m.GetComponent<MeshFilter>().mesh.MarkDynamic();
                originalSelectedIndices.Add(m.name, m.GetComponent<MeshFilter>().mesh.GetIndices(0));
            }

            if (modelHighlights.ContainsKey(m.name)) // if this object has highlights associated with it, process the highlights
            {
                foreach (GameObject highlight in modelHighlights[m.name])
                {
                    if (!originalNumVertices.ContainsKey(highlight.name))
                    {
                        originalNumVertices.Add(highlight.name, highlight.GetComponent<MeshFilter>().mesh.vertices.Length);        // Maybe want to store vertices as array instead?
                        highlight.GetComponent<MeshFilter>().mesh.MarkDynamic();

                        if (!originalSelectedIndices.ContainsKey(highlight.name))
                        {
                            originalSelectedIndices.Add(highlight.name, highlight.GetComponent<MeshFilter>().mesh.GetIndices(0));
                        }
                    }
                    ProcessMesh(highlight);
                }
            }
            else
            {
                modelHighlights.Add(m.name, new HashSet<GameObject>());
            }

            if (m.tag != "highlight")
            {
                if (!leftOutlines.ContainsKey(m.name))                      //
                {                                                           // Add a highlight for this mesh if there isn't one already
                    leftOutlines.Add(m.name, MakeHandHighlight(m.name));    //
                }
                if (!rightOutlines.ContainsKey(m.name))
                {
                    rightOutlines.Add(m.name, MakeHandHighlight(m.name));
                }
            }
            ProcessMesh(m);
        }

        if (controller0.device.GetHairTriggerUp() || controller1.device.GetHairTriggerUp()) // Clicked: a selection has been made
        {
            foreach (GameObject m in collidedMeshes)
            {
                GameObject savedLeftHighlight = CopyObject(leftOutlines[m.name]); // save the highlights at the point of selection
                GameObject savedRightHighlight = CopyObject(rightOutlines[m.name]);

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
            
                if (!modelHighlights.ContainsKey(m.name))
                {
                    modelHighlights.Add(m.name, new HashSet<GameObject>());
                }
                
                // process highlights and associate them with the original objects
                modelHighlights[m.name].Add(savedLeftHighlight);
                modelHighlights[m.name].Add(savedRightHighlight);

                foreach (GameObject highlight in modelHighlights[m.name]) 
                {
                    originalSelectedIndices[highlight.name] = highlight.GetComponent<MeshFilter>().mesh.GetIndices(0);
                    selectedMeshes.Add(highlight.name);
                    originalNumVertices[highlight.name] = highlight.GetComponent<MeshFilter>().mesh.vertices.Length;
                }
            }

        }
    }
    
    /// <summary>
    /// Creates a new game object with the same position, rotation, scale, material, and mesh as the original.
    /// </summary>
    /// <param name="original"></param>
    /// <returns></returns>
    private GameObject CopyObject(GameObject original)
    {
        GameObject copy = new GameObject();
        copy.AddComponent<MeshRenderer>();
        copy.AddComponent<MeshFilter>();
        copy.transform.position = original.transform.position;
        copy.transform.rotation = original.transform.rotation;
        copy.transform.localScale = original.transform.localScale;
        copy.GetComponent<MeshRenderer>().material = original.GetComponent<MeshRenderer>().material;
        copy.GetComponent<MeshFilter>().mesh = original.GetComponent<MeshFilter>().mesh;
        copy.tag = "highlightmesh"; // tag this object as a highlight
        copy.name = "highlight" + highlightObjectCount;
        highlightObjectCount++;

        return copy;
    }

    private bool OnNormalSideOfPlane(Vector3 pt, GameObject plane)
    {
        return Vector3.Dot(plane.transform.up, pt) >= Vector3.Dot(plane.transform.up, plane.transform.position);
    }

    private void ProcessMesh(GameObject m)
    {
        Mesh mesh = m.GetComponent<MeshFilter>().mesh;
        selectedIndices.Clear();

        if (!selectedMeshes.Contains(m.name) || m.CompareTag("highlightmesh"))
        {
            unselectedIndices.Clear();
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

                bool side0 = IntersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex1], ref intersectPoint0, ref intersectUV0, UVs[triangleIndex0], UVs[triangleIndex1], vertices[triangleIndex0], vertices[triangleIndex1], currentPlane);
                bool side1 = IntersectsWithPlane(transformedVertices[triangleIndex1], transformedVertices[triangleIndex2], ref intersectPoint1, ref intersectUV1, UVs[triangleIndex1], UVs[triangleIndex2], vertices[triangleIndex1], vertices[triangleIndex2], currentPlane);
                bool side2 = IntersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex2], ref intersectPoint2, ref intersectUV2, UVs[triangleIndex0], UVs[triangleIndex2], vertices[triangleIndex0], vertices[triangleIndex2], currentPlane);


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
                }
            }

            if (m.gameObject.tag != "highlightmesh")
            {
                CreateHighlightMesh(outlinePoints, currentPlane);
            }
            outlinePoints.Clear();

            if (m.gameObject.tag != "highlightmesh")
            {
                if (planePass == 1)
                {
                    rightOutlines[m.name].GetComponent<MeshFilter>().mesh = rightOutlineMesh;
                    rightOutlines[m.name].transform.position = m.transform.position;
                    rightOutlines[m.name].transform.localScale = m.transform.localScale;
                    rightOutlines[m.name].transform.rotation = m.transform.rotation;
                }
                else
                {
                    leftOutlines[m.name].GetComponent<MeshFilter>().mesh = leftOutlineMesh;
                    leftOutlines[m.name].transform.position = m.transform.position;
                    leftOutlines[m.name].transform.localScale = m.transform.localScale;
                    leftOutlines[m.name].transform.rotation = m.transform.rotation;
                }
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
        else // set highlight meshes
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
        List<Vector3> verts = new List<Vector3>();
        List<int> faces = new List<int>();
        List<Vector2> uvCoordinates = new List<Vector2>();

        float radius = .005f;
        int numSections = 6;

        Assert.IsTrue(points.Count % 2 == 0);
        int expectedNumVerts = (numSections + 1) * points.Count;

        if (expectedNumVerts > 65000)
        {
            points = OrderMesh(points);
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

        if (plane.Equals(leftPlane))
        {
            leftOutlineMesh.Clear();
            leftOutlineMesh.SetVertices(verts);        
            leftOutlineMesh.SetUVs(0, uvCoordinates);
            leftOutlineMesh.SetTriangles(faces, 0);

            leftOutlineMesh.RecalculateBounds();
            leftOutlineMesh.RecalculateNormals();
        }
        else
        {
            rightOutlineMesh.Clear();
            rightOutlineMesh.SetVertices(verts);       
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
        meshVertices = RemoveSequentialDuplicates(meshVertices);

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

    private bool IntersectsWithPlane(Vector3 lineVertexWorld0, Vector3 lineVertexWorld1, ref Vector3 intersectPoint, ref Vector2 intersectUV, Vector2 vertex0UV, Vector2 vertex1UV, Vector3 lineVertexLocal0, Vector3 lineVertexLocal1, GameObject plane) // checks if a particular edge intersects with the plane, if true, returns point of intersection along edge
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
        int i = 1;
        while (i < points.Count - 1)
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
    private GameObject MakeHandHighlight(string meshName)
    {
        GameObject newHighlight = new GameObject();
        newHighlight.name = meshName + " highlight";
        newHighlight.AddComponent<MeshRenderer>();
        newHighlight.AddComponent<MeshFilter>();
        newHighlight.GetComponent<MeshFilter>().mesh.MarkDynamic();
        newHighlight.GetComponent<Renderer>().material = Resources.Load("TestMaterial") as Material;

        return newHighlight;
    }
}