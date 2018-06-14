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

    private Vector3 currentPos;
    private Vector3 lastPos;

    private GameObject leftPlane;   //
    private GameObject rightPlane;  // Used to detect collision with meshes in the model
    private GameObject centerCube;  //

    private int planeLayer;                         // Layer that cube and planes are on
    private static int outlineObjectCount = 0;      // Keeps saved outlines distinguishable from one another

    private List<GameObject> collidingMeshes;       // List of meshes currently being collided with
    private HashSet<GameObject> cubeColliders;      // All the objects the cube is colliding with
    CubeCollision centerComponent;                  // Script on cube that we get the list of colliders from

    private static Dictionary<string, Vector3[]> previousVertices;              // Key = name of obj with mesh, Value = all vertices of the mesh at the time of last click
    private static Dictionary<string, Vector2[]> previousUVs;                   // Key = name of obj with mesh, Value = all UVs of the mesh at the time of last click
    private Dictionary<string, int[]> previousUnselectedIndices;                // Key = name of object with mesh, Value = all indices that have not been selected (updated when user clicks)
    private static Dictionary<string, int> previousNumVertices;                 // Key = name of object with mesh, Value = original set of vertices (updated when user clicks and mesh is split)
    private static Dictionary<string, int[]> previousSelectedIndices;           // key = name of object with mesh, Value = original set of selected indices (updated when user clicks)
    private static HashSet<string> objWithSelections;                           // Collection of the the names of all the meshes that have had pieces selected from them.
    private static Dictionary<string, HashSet<GameObject>> savedOutlines;       // Key = name of object in model, Value = all the SAVED outline game objects attached to it
    private static Dictionary<string, GameObject> leftOutlines;                 // left hand outlines per model that are currently being manipulated (KEY = name of model object, VALUE = outline object)
    private static Dictionary<string, GameObject> rightOutlines;                // right hand outlines per model that are currently being manipulated (KEY = name of model object, VALUE = outline object)

    private List<Vector3> outlinePoints;    // Pairs of two connected points to be used in drawing an outline mesh

    List<int> selectedIndices;      // Reused for each mesh during ProcessMesh()
    List<int> unselectedIndices;    // ^^^^
    
    //Mesh leftOutlineMesh;       // Reused to draw highlights that move with left hand
    //Mesh rightOutlineMesh;      // Reused to draw highlights that move with right hand

    public static Dictionary<string, int[]> PreviousSelectedIndices
    {
        get { return previousSelectedIndices; }
    }

    public static Dictionary<string, Vector3[]> PreviousVertices
    {
        get { return previousVertices; }
    }

    public static Dictionary<string, Vector2[]> PreviousUVs
    {
        get { return previousUVs;  }
    }

    public static Dictionary<string, int> PreviousNumVertices
    {
        get { return previousNumVertices; }
    }
    public static HashSet<string> ObjectsWithSelections
    {
        get { return objWithSelections; }
    }
    public static Dictionary<string, HashSet<GameObject>> SavedOutlines
    {
        get { return savedOutlines; }
        set { savedOutlines = value; }
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

        currentPos = controller0.controller.transform.position;
        lastPos = currentPos;

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

        collidingMeshes = new List<GameObject>();      
        cubeColliders = new HashSet<GameObject>();
     
        //TODO: should these persist between states? Yes so only make one instance of the state. Should use the Singleton pattern here//TODO

        objWithSelections = new HashSet<string>();
        previousNumVertices = new Dictionary<string, int>();              // Keeps track of how many vertices a mesh should have
        previousUnselectedIndices = new Dictionary<string, int[]>();      // Keeps track of indices that were previously unselected
        previousSelectedIndices = new Dictionary<string, int[]>();
        previousVertices = new Dictionary<string, Vector3[]>();
        previousUVs = new Dictionary<string, Vector2[]>();
        savedOutlines = new Dictionary<string, HashSet<GameObject>>();
        selectedIndices = new List<int>();
        unselectedIndices = new List<int>();
        outlinePoints = new List<Vector3>();

        this.stateToReturnTo = stateToReturnTo;

        leftOutlines = new Dictionary<string, GameObject>();
        rightOutlines = new Dictionary<string, GameObject>();

        //leftOutlineMesh = new Mesh();
        //rightOutlineMesh = new Mesh();
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
        handPlane.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f); //Previously 0.03

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

        //the normals of both planes are always facing each other
        leftPlane.transform.up = (rightPlane.transform.position - leftPlane.transform.position).normalized;
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

        foreach (GameObject collidingObj in collidingMeshes)
        {
            Mesh mesh = collidingObj.GetComponent<MeshFilter>().mesh;
            mesh.subMeshCount = 2;
            indices = previousSelectedIndices[collidingObj.name]; // the indices of last selection
  
            if (objWithSelections.Contains(collidingObj.name))    // If it previously had a piece selected (CLICKED) - revert to that selection
            {
                // Generate a mesh to fill the entire selected part of the collider
                //Vector3[] verts = mesh.vertices;
                Vector3[] verts = previousVertices[collidingObj.name];

                        //Debug.Log( verts.Count().ToString() + ", " + previousNumVertices[collidingObj.name].ToString()  + "   deactivate");

                List<Vector2> uvs = new List<Vector2>();
                uvs = previousUVs[collidingObj.name].ToList();
                //mesh.GetUVs(0, uvs);

                mesh.Clear();
                mesh.vertices = verts;
                mesh.SetUVs(0, uvs);

                if (collidingObj.tag != "highlightmesh") // set unselected and selected regions back to what they were at the last click
                {
                    mesh.subMeshCount = 2;
                    mesh.SetTriangles(previousUnselectedIndices[collidingObj.name], 0);
                    mesh.SetTriangles(indices, 1);
                }
                else // for meshes that are outlines, use only one material (unselected will not be drawn)
                {
                    mesh.subMeshCount = 1;
                    mesh.SetTriangles(indices, 0);
                }

                mesh.RecalculateBounds();
                mesh.RecalculateNormals();

                // Go through each outline associated with the current mesh object and reset it
                foreach (GameObject outline in savedOutlines[collidingObj.name])
                {
                            //Debug.Log("Removing outlines for " + collidingObj.name);

                    Mesh outlineMesh = outline.GetComponent<MeshFilter>().mesh;
                    //Vector3[] outlineVerts = outlineMesh.vertices;
                    Vector3[] outlineVerts = previousVertices[outline.name];
                    List<Vector2> outlineUVs = new List<Vector2>();
                    outlineUVs = previousUVs[outline.name].ToList();
                    //outlineMesh.GetUVs(0, outlineUVs);

                    outlineMesh.Clear();
                    outlineMesh.vertices = outlineVerts;
                    outlineMesh.SetUVs(0, outlineUVs);

                    outlineMesh.subMeshCount = 1;
                    outlineMesh.SetTriangles(previousSelectedIndices[outline.name], 0);

                    outlineMesh.RecalculateBounds();
                    outlineMesh.RecalculateNormals();
                }
            }
            else // NOT CLICKED 
            {
                        //Debug.Log("deactivating and not clicked " + collidingObj.name);

                // reset object to original state (before interaction)
                if (collidingObj.tag != "highlightmesh")
                {
                    Material baseMaterial = collidingObj.GetComponent<Renderer>().materials[0];
                    baseMaterial.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                    collidingObj.GetComponent<Renderer>().materials[1] = baseMaterial;

                    leftOutlines[collidingObj.name].GetComponent<MeshFilter>().mesh.Clear();
                    rightOutlines[collidingObj.name].GetComponent<MeshFilter>().mesh.Clear();
                }
            }

            //stop rendering current outline whenever hands removed from collidingObj
            if (leftOutlines.ContainsKey(collidingObj.name) || rightOutlines.ContainsKey(collidingObj.name))
            {
                leftOutlines[collidingObj.name].GetComponent<MeshRenderer>().enabled = false;
                rightOutlines[collidingObj.name].GetComponent<MeshRenderer>().enabled = false;
            }

            
        }
    }

    public override void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        List<Vector2> UVList = new List<Vector2>();

        UpdatePlanes();

        currentPos = controller0.controller.transform.position;

        Debug.Log("Hand Motion " + Vector3.Distance(lastPos,currentPos).ToString());

        // Take input from cube about what it collides with
        cubeColliders = centerComponent.CollidedObjects;
        
        if (cubeColliders.Count > 0)
        {
            collidingMeshes.Clear();
            collidingMeshes = cubeColliders.ToList();
        }
        else // If not colliding with anything, change states
        { 
            GameObject.Find("UIController").GetComponent<UIController>().ChangeState(stateToReturnTo);
            return;    
        }

        foreach (GameObject currObjMesh in collidingMeshes)
        {
            if (!previousNumVertices.ContainsKey(currObjMesh.name)) // if the original vertices are not stored already, store them (first time seeing object)
            {
                previousNumVertices.Add(currObjMesh.name, currObjMesh.GetComponent<MeshFilter>().mesh.vertices.Length);
                currObjMesh.GetComponent<MeshFilter>().mesh.MarkDynamic();
                previousSelectedIndices.Add(currObjMesh.name, currObjMesh.GetComponent<MeshFilter>().mesh.GetIndices(0));
                previousVertices.Add(currObjMesh.name, currObjMesh.GetComponent<MeshFilter>().mesh.vertices);

                UVList = new List<Vector2>();
                currObjMesh.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                previousUVs.Add(currObjMesh.name, UVList.ToArray<Vector2>());

                currObjMesh.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            if (savedOutlines.ContainsKey(currObjMesh.name)) // if this object has outlines associated with it, process the outlines
            {
                foreach (GameObject outline in savedOutlines[currObjMesh.name])
                {
                    if (!previousNumVertices.ContainsKey(outline.name))
                    {
                        previousNumVertices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.vertices.Length);        // Maybe want to store vertices as array instead?
                        outline.GetComponent<MeshFilter>().mesh.MarkDynamic();

                        if (!previousSelectedIndices.ContainsKey(outline.name))
                        {
                            previousSelectedIndices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.GetIndices(0));
                            previousVertices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.vertices);

                            UVList = new List<Vector2>();
                            currObjMesh.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                            previousUVs.Add(currObjMesh.name, UVList.ToArray<Vector2>());
                        }
                    }

                    //test, following two if statements
                    //if (currObjMesh.name == "Polyline93")
                    //{
                    //    if(currObjMesh.GetComponent<MeshFilter>().mesh.vertices.Length - previousNumVertices[currObjMesh.name] < 0)
                    //    {
                    //        Debug.Log("Negative! outline");
                    //    }

                    //}

                    ProcessMesh(outline);
                }
            }
            else
            {
                savedOutlines.Add(currObjMesh.name, new HashSet<GameObject>());
            }

            if (currObjMesh.tag != "highlight")
            {
                if (!leftOutlines.ContainsKey(currObjMesh.name))                              //
                {                                                                             // Add a highlight for this mesh if there isn't one already
                    leftOutlines.Add(currObjMesh.name, MakeHandOutline(currObjMesh.name));    //
                }
                if (!rightOutlines.ContainsKey(currObjMesh.name))
                {
                    rightOutlines.Add(currObjMesh.name, MakeHandOutline(currObjMesh.name));
                }
            }

            ProcessMesh(currObjMesh);
        }

        if (controller0.device.GetHairTriggerDown() || controller1.device.GetHairTriggerDown()) // Clicked: a selection has been made
        {

            foreach (GameObject currObjMesh in collidingMeshes)
            {
                currObjMesh.GetComponent<MeshFilter>().mesh.UploadMeshData(false);
                GameObject savedLeftOutline = CopyObject(leftOutlines[currObjMesh.name]); // save the highlights at the point of selection
                GameObject savedRightOutline = CopyObject(rightOutlines[currObjMesh.name]);

                ////test, the immediate if statement
                //        bool divisBy3 = previousSelectedIndices[currObjMesh.name].Length % 3 == 0;
                //if (!divisBy3)
                //{
                //    Debug.Log("old: " + previousSelectedIndices[currObjMesh.name].Length.ToString() + ". for " + currObjMesh.name);
                //}

                previousNumVertices[currObjMesh.name] = currObjMesh.GetComponent<MeshFilter>().mesh.vertices.Length;
                previousVertices[currObjMesh.name] = currObjMesh.GetComponent<MeshFilter>().mesh.vertices;

                UVList = new List<Vector2>();
                currObjMesh.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                previousUVs[currObjMesh.name] = UVList.ToArray<Vector2>();

                //Debug.Log("old: " + oldVertNum.ToString() + ". New: " + previousNumVertices[currObjMesh.name].ToString() +". for " + currObjMesh.name);

                //The submesh to start
                int submeshNum = 0;
                Material[] origMaterials = currObjMesh.GetComponent<Renderer>().materials;
                for (int i = 0; i < origMaterials.Length; i++)
                {
                    if (origMaterials[i].name == "Selected (Instance)")
                    {
                        submeshNum = i;
                    }
                }

                previousSelectedIndices[currObjMesh.name] = currObjMesh.GetComponent<MeshFilter>().mesh.GetIndices(submeshNum);     // updates original indices to store the the most recently selected portion

                //test, following foreach statement
                //foreach (int index in previousSelectedIndices[currObjMesh.name])
                //{
                //    if (!(index >= 0 && index < currObjMesh.GetComponent<MeshFilter>().mesh.vertices.Count()))
                //    {
                //        Debug.Log("Selected index " + index.ToString() + " out of bounds for vertices at click update.");
                //    }
                //}

                ////test, immediate if statement
                //        Debug.Log(currObjMesh.GetComponent<MeshFilter>().mesh.vertices.Length.ToString() + " CLICK");

                //        divisBy3 = previousSelectedIndices[currObjMesh.name].Length % 3 == 0;
                //        if (!divisBy3)
                //        {
                //            Debug.Log("new: " + previousSelectedIndices[currObjMesh.name].Length.ToString() + ". for " + currObjMesh.name);
                //        }

                if (previousUnselectedIndices.ContainsKey(currObjMesh.name))
                {
                    previousUnselectedIndices[currObjMesh.name] = currObjMesh.GetComponent<MeshFilter>().mesh.GetIndices(0);
                }
                else
                {
                    previousUnselectedIndices.Add(currObjMesh.name, currObjMesh.GetComponent<MeshFilter>().mesh.GetIndices(0));
                }

                ////test, following foreach statement
                //        foreach (int index in previousUnselectedIndices[currObjMesh.name])
                //        {
                //            if (!(index >= 0 && index < currObjMesh.GetComponent<MeshFilter>().mesh.vertices.Count()))
                //            {
                //                Debug.Log("Unselected index " + index.ToString() + " out of bounds for vertices at click update.");
                //            }
                //        }


                objWithSelections.Add(currObjMesh.name);

                if (!savedOutlines.ContainsKey(currObjMesh.name))
                {
                    savedOutlines.Add(currObjMesh.name, new HashSet<GameObject>());
                }
                
                // process outlines and associate them with the original objects
                savedOutlines[currObjMesh.name].Add(savedLeftOutline);
                savedOutlines[currObjMesh.name].Add(savedRightOutline);

                foreach (GameObject outline in savedOutlines[currObjMesh.name]) 
                {
                    previousSelectedIndices[outline.name] = outline.GetComponent<MeshFilter>().mesh.GetIndices(0);
                    objWithSelections.Add(outline.name);
                    previousNumVertices[outline.name] = outline.GetComponent<MeshFilter>().mesh.vertices.Length;
                    previousVertices[outline.name] = outline.GetComponent<MeshFilter>().mesh.vertices;

                    UVList = new List<Vector2>();
                    outline.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                    previousUVs[outline.name] = UVList.ToArray<Vector2>();
                }

              
            }

        }
        lastPos = currentPos;
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
        copy.name = "highlight" + outlineObjectCount;
        outlineObjectCount++;

        return copy;
    }

    private bool OnNormalSideOfPlane(Vector3 pt, GameObject plane)
    {
        return Vector3.Dot(plane.transform.up, pt) >= Vector3.Dot(plane.transform.up, plane.transform.position);
    }

    private void ProcessMesh(GameObject item)
    {
        Mesh mesh = item.GetComponent<MeshFilter>().mesh;
        selectedIndices.Clear();

        if (!objWithSelections.Contains(item.name) || item.CompareTag("highlightmesh"))
        {
            unselectedIndices.Clear();
        }
        else
        {
            unselectedIndices =  previousUnselectedIndices[item.name].ToList<int>();
        }

        int[] indices = previousSelectedIndices[item.name];        // original indices is set to be JUST the selected part, that's why nothing else is drawn
        List<Vector3> vertices = previousVertices[item.name].ToList();

        List<Vector2> UVs = previousUVs[item.name].ToList();
        int numVertices = previousNumVertices[item.name];


        // vertices.RemoveRange(numVertices, vertices.Count - numVertices);
        //UVs.RemoveRange(numVertices, UVs.Count - numVertices);

        //test 06/05/18
        //if (vertices.Count - numVertices < 0)
        //{
        //    Debug.Log(item.GetComponent<MeshFilter>().mesh.vertices.Length.ToString() + ", " + previousNumVertices[item.name] + " " + item.name + " is negative!!!");
        //}

        List<Vector3> transformedVertices = new List<Vector3>(vertices.Count);

        for (int i = 0; i < vertices.Count; i++)
        {
            transformedVertices.Add(item.gameObject.transform.TransformPoint(vertices[i]));
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


            for (int i = 0; i < indices.Length / 3 ; i++)
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
                else if (side0 && side1 && side2)
                {
                    Debug.Log("             All three sides!!");
                }
                else if (PlaneCollision.ApproximatelyEquals(intersectPoint0, transformedVertices[triangleIndex0]) || PlaneCollision.ApproximatelyEquals(intersectPoint1, transformedVertices[triangleIndex1]) || PlaneCollision.ApproximatelyEquals(intersectPoint2, transformedVertices[triangleIndex2]))
                {
                    Debug.Log("             YOU HIT A VERTEX");
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

                        
                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint0));
                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint1));
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
                        
                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint0));
                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint2));
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
                        
                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint1));
                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint2));
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

            if (item.gameObject.tag != "highlightmesh")
            {
                if (planePass == 1)
                {
                    Mesh outlineMesh = CreateOutlineMesh(outlinePoints, currentPlane, rightOutlines[item.name].GetComponent<MeshFilter>().mesh);
                    //rightOutlines[item.name].GetComponent<MeshFilter>().mesh = outlineMesh;
                    rightOutlines[item.name].transform.position = item.transform.position;
                    rightOutlines[item.name].transform.localScale = item.transform.localScale;
                    rightOutlines[item.name].transform.rotation = item.transform.rotation;
                }
                else
                {
                    Mesh outlineMesh = CreateOutlineMesh(outlinePoints, currentPlane, leftOutlines[item.name].GetComponent<MeshFilter>().mesh);
                    //leftOutlines[item.name].GetComponent<MeshFilter>().mesh = outlineMesh;
                    leftOutlines[item.name].transform.position = item.transform.position;
                    leftOutlines[item.name].transform.localScale = item.transform.localScale;
                    leftOutlines[item.name].transform.rotation = item.transform.rotation;
                }

                outlinePoints.Clear();
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, UVs);

        if (item.gameObject.tag != "highlightmesh")
        {
            mesh.subMeshCount = 2;

            mesh.SetTriangles(unselectedIndices, 0);
            mesh.SetTriangles(selectedIndices, 1);

            Material[] materials = new Material[2];
            Material baseMaterial = item.GetComponent<Renderer>().materials[0];
            materials[0] = DetermineBaseMaterial(baseMaterial);         // Sets unselected as transparent
            materials[1] = Resources.Load("Selected") as Material;      // May need to specify which submesh we get this from? -> THIS SETS SELECTION AS ORANGE STUFF
            item.GetComponent<Renderer>().materials = materials;
        }

        else // set highlight meshes foreach (int index in selectedIndices)
        {
            mesh.subMeshCount = 1;
            mesh.SetTriangles(selectedIndices, 0);
        }

        //mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    /**
     * points contains a list of points where each successive pair of points gets a tube drawn between them, sets to mesh called selectorMesh
     * */
    private Mesh CreateOutlineMesh(List<Vector3> points, GameObject plane, Mesh outlineMesh)
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> faces = new List<int>();
        List<Vector2> uvCoordinates = new List<Vector2>();
        outlineMesh.Clear();

        float radius = .005f;
        int numSections = 6;

        Assert.IsTrue(points.Count % 2 == 0);
        int expectedNumVerts = (numSections + 1) * points.Count;

        if (expectedNumVerts > 65000)
        {
            points = OrderMesh(points);
        }

        if (points.Count >= 2) {
            for (int i = 0; i < points.Count-1; i += 2)
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
    private GameObject MakeHandOutline(string meshName)
    {
        GameObject newOutline = new GameObject();
        newOutline.name = meshName + " highlight";
        newOutline.AddComponent<MeshRenderer>();
        newOutline.AddComponent<MeshFilter>();
        newOutline.GetComponent<MeshFilter>().mesh = new Mesh();
        newOutline.GetComponent<MeshFilter>().mesh.MarkDynamic();
        newOutline.GetComponent<Renderer>().material = Resources.Load("TestMaterial") as Material;

        return newOutline;
    }
}