using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public class SliceNSwipeSelectionState : InteractionState
{
    public GameObject camera;// = new GameObject();
    private GameObject laser;
    private GameObject reticle;

    private const bool debug = false;
    private const float motionThreshold = 0.04f;

    private static int sliceStatus = 0;    //0 if you haven't just made a slice, 1 if you have and you need to select.
    //private ControllerInfo controller0;
    //private ControllerInfo controller1;
    private ControllerInfo mainController;
    private ControllerInfo altController;
   // private GameObject handTrail;
    private GameObject swordLine;

    private Vector3 lastPos;
    private Vector3 lastOrientation;
    private GameObject lastSeenObj;

    private GameObject slicePlane;   //

    private int planeLayer;                         // Layer that cube and planes are on
    private static int outlineObjectCount = 0;      // Keeps saved outlines distinguishable from one another

    //private List<GameObject> collidingMeshes;       // List of meshes currently being collided with
    private GameObject collidingMesh;                   // The currently colliding object.

    private static Dictionary<string, Vector3[]> previousVertices;              // Key = name of obj with mesh, Value = all vertices of the mesh at the time of last click
    private static Dictionary<string, Vector2[]> previousUVs;                   // Key = name of obj with mesh, Value = all UVs of the mesh at the time of last click
    private Dictionary<string, List<int>> previousUnselectedIndices;                // Key = name of object with mesh, Value = all indices that have not been selected (updated when user clicks)
    private static Dictionary<string, int> previousNumVertices;                 // Key = name of object with mesh, Value = original set of vertices (updated when user clicks and mesh is split)
    private static Dictionary<string, List<int>> previousSelectedIndices;           // key = name of object with mesh, Value = original set of selected indices (updated when user clicks)
    private static HashSet<string> objWithSelections;                           // Collection of the the names of all the meshes that have had pieces selected from them.
    private static Dictionary<string, HashSet<GameObject>> savedOutlines;       // Key = name of object in model, Value = all the SAVED outline game objects attached to it
    private static Dictionary<string, GameObject> sliceOutlines;                 // left hand outlines per model that are currently being manipulated (KEY = name of model object, VALUE = outline object)
    private Dictionary<string, Material> originalMaterial;

    private static Dictionary<string, int[]> selection0Indices;
    private static Dictionary<string, int[]> selection1Indices;

    private List<Vector3> outlinePoints;    // Pairs of two connected points to be used in drawing an outline mesh

    List<int> selected0Indices;      // Reused for each mesh during ProcessMesh()
    List<int> selected1Indices;    // ^^^^

    public static Dictionary<string, List<int>> PreviousSelectedIndices
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
    public static Dictionary<string, GameObject> IntersectOutlines
    {
        get { return sliceOutlines; }
    }
    public static int SliceStatus
    {
        get { return sliceStatus;  }
    }

    /// <summary>
    /// State that activates whenever there's a mesh between the user's controllers. Allows user to select surfaces and progressively refine their selection.
    /// Currently only works when selecting a single object.
    /// </summary>
    /// <param name="controller0Info"></param>
    /// <param name="controller1Info"></param>
    public SliceNSwipeSelectionState(ControllerInfo controller0Info, ControllerInfo controller1Info) // InteractionState stateToReturnTo) 
    {

        Debug.Log("Constructing Slice State");
        // NOTE: Selecting more than one mesh will result in highlights appearing in the wrong place
        desc = "SliceNSwipeSelectionState";
        ControllerInfo controller0 = controller0Info;
        ControllerInfo controller1 = controller1Info;

        camera = GameObject.Find("Camera (eye)"); //.transform.GetChild(0).gameObject;
        laser = GameObject.Find("LaserParent").transform.GetChild(0).gameObject;
        reticle = GameObject.Find("ReticleParent").transform.GetChild(0).gameObject;
      //  handTrail = GameObject.Find("HandTrail");
        swordLine = GameObject.Find("SwordLine");

        DetermineDominantController(controller0, controller1);

        planeLayer = LayerMask.NameToLayer("PlaneLayer");

        slicePlane = CreateHandPlane(mainController, "SliceNSwipeHandPlane");

        collidingMesh = null; new GameObject();      
        //cubeColliders = new HashSet<GameObject>();
     
        //TODO: should these persist between states? Yes so only make one instance of the state. Should use the Singleton pattern here//TODO

        objWithSelections = new HashSet<string>();
        previousNumVertices = new Dictionary<string, int>();              // Keeps track of how many vertices a mesh should have
        previousUnselectedIndices = new Dictionary<string, List<int>>();      // Keeps track of indices that were previously unselected
        previousSelectedIndices = new Dictionary<string, List<int>>();
        previousVertices = new Dictionary<string, Vector3[]>();
        previousUVs = new Dictionary<string, Vector2[]>();
        savedOutlines = new Dictionary<string, HashSet<GameObject>>();
        selected0Indices = new List<int>();
        selected1Indices = new List<int>();
        outlinePoints = new List<Vector3>();
        originalMaterial = new Dictionary<string, Material>();
        lastSeenObj = new GameObject();

        selection0Indices = new Dictionary<string, int[]>();
        selection1Indices = new Dictionary<string, int[]>();

        sliceOutlines = new Dictionary<string, GameObject>();
    }

    private void DetermineDominantController(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        controller0Info.controller.gameObject.transform.GetChild(0).gameObject.SetActive(false); //deactivate controller rendering
        controller0Info.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = true; //enable hand rendering
        Debug.Log("Set Dominant Hand");
        mainController = controller0Info;
        altController = controller1Info;
        GameObject hand = GameObject.Find("Hand");

        //handTrail.transform.parent = mainController.controller.transform;
        //handTrail.transform.localPosition = new Vector3 (0f, 0f, 0f);
        //Debug.Log(handTrail.transform.position.ToString());

        swordLine.transform.parent = mainController.controller.transform;
        swordLine.transform.localPosition = new Vector3(0f, -0.01f, 0.4f);
        swordLine.transform.parent.rotation = Quaternion.Inverse(mainController.controller.transform.rotation);
        swordLine.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = true;

        //  altController.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false; //disable hand rendering
        //  altController.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true); //enable rendering of controllers
        Debug.Log("Remember to Implement DetermineDominantController()");
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
        handPlane.layer = LayerMask.NameToLayer("Ignore Raycast"); //ignore raycast
        //Debug.Log("plane layer - " + handPlane.layer.ToString());
        handPlane.GetComponent<Renderer>().material = Resources.Load("Plane Material") as Material;
        handPlane.AddComponent<MeshCollider>();
        handPlane.GetComponent<MeshCollider>().convex = true;
        handPlane.GetComponent<MeshCollider>().isTrigger = true;
        handPlane.AddComponent<Rigidbody>();
        handPlane.GetComponent<Rigidbody>().isKinematic = true;

        handPlane.transform.position = c.controller.transform.position;
        handPlane.transform.rotation = c.controller.transform.rotation;
        handPlane.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f); //Previously 0.03

        //handPlane.layer = planeLayer;
        if (!debug)
        {
            handPlane.GetComponent<MeshRenderer>().enabled = false;
        }

        return handPlane;
    }

    /// <summary>
    /// Adjusts position of planes and cube.
    /// </summary>
    public void UpdatePlane(Vector3 movement)
    {
        slicePlane.transform.position = mainController.controller.transform.position;

        Vector3 mxf = Vector3.Cross(movement, mainController.controller.transform.forward).normalized;

        slicePlane.transform.up = mxf;

        //swordLine.transform.rotation = mainController.controller.transform.rotation;

    }

    public override void Deactivate()
    {
        swordLine.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;    // Disable line rendering

        List<int> indices;

        Mesh mesh = collidingMesh.GetComponent<MeshFilter>().mesh;
        mesh.subMeshCount = 2;
        indices = previousSelectedIndices[collidingMesh.name]; // the indices of last selection

        if (objWithSelections.Contains(collidingMesh.name))    // If it previously had a piece selected (CLICKED) - revert to that selection
        {
            // Generate a mesh to fill the entire selected part of the collider
            Vector3[] verts = previousVertices[collidingMesh.name];
                                           
            List<Vector2> uvs = new List<Vector2>();
            uvs = previousUVs[collidingMesh.name].ToList();

            mesh.Clear();
            mesh.vertices = verts;
            mesh.SetUVs(0, uvs);

            if (collidingMesh.tag != "highlightmesh") // set unselected and selected regions back to what they were at the last click
            {
                mesh.subMeshCount = 2;
                mesh.SetTriangles(previousUnselectedIndices[collidingMesh.name], 0);
            mesh.SetTriangles(indices, 1);
        }
        else // for meshes that are outlines, use only one material (unselected will not be drawn)
        {
            mesh.subMeshCount = 1;
            mesh.SetTriangles(indices, 0);
        }

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        //// Go through each outline associated with the current mesh object and reset it
        //foreach (GameObject outline in savedOutlines[collidingMesh.name])
        //{
        //    //Debug.Log("Removing outlines for " + collidingMesh.name);

        //    Mesh outlineMesh = outline.GetComponent<MeshFilter>().mesh;
        //    Vector3[] outlineVerts = previousVertices[outline.name];
        //    List<Vector2> outlineUVs = new List<Vector2>();
        //    outlineUVs = previousUVs[outline.name].ToList();

        //    outlineMesh.Clear();
        //    outlineMesh.vertices = outlineVerts;
        //    outlineMesh.SetUVs(0, outlineUVs);

        //    outlineMesh.subMeshCount = 1;
        //    outlineMesh.SetTriangles(previousSelectedIndices[outline.name], 0);

        //    outlineMesh.RecalculateBounds();
        //    outlineMesh.RecalculateNormals();
        //}
}
        else // NOT CLICKED
        {
            //Debug.Log("deactivating and not clicked " + collidingMesh.name);

            // reset object to original state (before interaction)
            if (collidingMesh.tag != "highlightmesh")
            {
                Material baseMaterial = collidingMesh.GetComponent<Renderer>().materials[0];
                baseMaterial.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                collidingMesh.GetComponent<Renderer>().materials[1] = baseMaterial;

                selection0Indices[collidingMesh.name].Remove(collidingMesh.name);
                selection1Indices[collidingMesh.name].Remove(collidingMesh.name);
                ColorMesh(collidingMesh, "slice");

                sliceOutlines[collidingMesh.name].GetComponent<MeshFilter>().mesh.Clear();
            }
        }
    }

    public override void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        Vector3 currentPos = mainController.trackedObj.transform.position;
        Vector3 currentOrientation = mainController.trackedObj.transform.forward;


        if (lastPos == new Vector3(0, 0, 0))
        {
            lastPos = currentPos;
            lastOrientation = currentOrientation;
        }


        List<Vector2> UVList = new List<Vector2>();

        GazeSelection();

        if (collidingMesh != null)
        { 
            if (sliceStatus == 0 && Vector3.Distance(lastPos, currentPos) <= motionThreshold) //small movement and you haven't made a slice
            {
                UpdatePlane(lastPos - currentPos);

                if (!previousNumVertices.ContainsKey(collidingMesh.name)) // if the original vertices are not stored already, store them (first time seeing object)
                {
                    FirstContactProcess(collidingMesh, UVList);
                }

                // Debug.Log("not slice: " + dist.ToString());
            }
            else if (sliceStatus == 0 && !mainController.device.GetHairTrigger() && Vector3.Distance(lastPos, currentPos) > motionThreshold ) // you just made a big slicing movement
            {

                string debugString = "";
                UpdatePlane(lastPos - currentPos);

                sliceStatus = 1;
                /* to make a cut plane you need to get the transform.forward and also the difference between last and current positions*/

                if (!previousNumVertices.ContainsKey(collidingMesh.name))
                {
                    FirstContactProcess(collidingMesh, UVList);
                    debugString += collidingMesh.name + "  ";
                }
                else
                {
                    SplitMesh(collidingMesh);
                    ColorMesh(collidingMesh, "slice");
                    debugString += collidingMesh.name + "  ";
                    collidingMesh.GetComponent<MeshFilter>().mesh.UploadMeshData(false);

                    // process outlines and associate them with the original objects
                    /*
                    if (savedOutlines.ContainsKey(currObjMesh.name))
                    {
                        foreach (GameObject outline in savedOutlines[currObjMesh.name])
                        {
                            if (!previousNumVertices.ContainsKey(outline.name))
                            {
                                previousNumVertices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.vertices.Length);        // Maybe want to store vertices as array instead?
                                outline.GetComponent<MeshFilter>().mesh.MarkDynamic();

                                if (!previousSelectedIndices.ContainsKey(outline.name))
                                {
                                    previousSelectedIndices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.GetIndices(0).ToList());
                                    previousVertices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.vertices);

                                    UVList = new List<Vector2>();
                                    outline.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                                    previousUVs.Add(outline.name, UVList.ToArray<Vector2>());
                                }
                            }
                            if ( outline.name == "highlight0")
                            {
                                Debug.Log(" At slice " + previousSelectedIndices[outline.name].Count.ToString() + " selected Indices");
                            }
                            SplitMesh(outline);
                            debugString += outline.name + "  ";
                            //previousSelectedIndices[outline.name] = selection0Indices[outline.name].Concat(selection1Indices[outline.name]).ToList();
                            //previousNumVertices[outline.name] = outline.GetComponent<MeshFilter>().mesh.vertices.Length;
                            //previousVertices[outline.name] = outline.GetComponent<MeshFilter>().mesh.vertices;

                            //UVList = new List<Vector2>();
                            //outline.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                            //previousUVs[outline.name] = UVList.ToArray<Vector2>();
                            ColorMesh(outline, "slice");
                        }
                    }
                    else
                    {
                        savedOutlines.Add(currObjMesh.name, new HashSet<GameObject>());
                    }
                    savedOutlines[currObjMesh.name].Add(savedSliceOutline);
                    */

                }
                Debug.Log("SLICE: " + debugString);
            }
            else if (sliceStatus == 1)
            {
                if (altController.device.GetHairTrigger())          //remove last swipe
                {
                    sliceStatus = 0;

                    selection0Indices.Remove(collidingMesh.name);
                    selection1Indices.Remove(collidingMesh.name);

                    sliceOutlines[collidingMesh.name].GetComponent<MeshFilter>().mesh = new Mesh();

                    ColorMesh(collidingMesh, "slice");
                    /*
                    string outlineCollection = "";

                    if(savedOutlines[currObjMesh.name].Remove(lastSliceOutline[currObjMesh.name]))
                    {
                        Debug.Log("~~~~~ removed: " + lastSliceOutline[currObjMesh.name].name);
                    }
                    */

                    /*
                    foreach (GameObject outline in savedOutlines[currObjMesh.name])
                    {
                        outlineCollection += outline.name + ", ";
                        ColorMesh(outline, "slice");

                    }
                    Debug.Log("outlines for removed SLICE: " + outlineCollection);
                    */

                }
                else if (mainController.device.GetHairTrigger() && Vector3.Distance(lastPos, currentPos) > motionThreshold - 0.02f) //Swipe with main trigger helf to select
                {
                    //Debug.Log("Swipe!! " + Vector3.Distance(lastPos, currentPos).ToString());
                    /* if movement is big & towards normal side of plane, discard the indeces on that side.
                     * else if away from normal side of plane, discard indeces on that side.
                     * make discarded indeces transparent and delete slicing plane.
                     */

                    Vector3 heading = (lastPos - currentPos).normalized;

                    if (previousNumVertices.ContainsKey(collidingMesh.name) && collidingMesh.gameObject.tag != "highlightmesh")
                    {
                        if (NormalSwipe(heading, slicePlane))
                        {
                            //Debug.Log("swipe " + collidingMesh.name);
                            previousUnselectedIndices[collidingMesh.name] = previousUnselectedIndices[collidingMesh.name].Concat(selection0Indices[collidingMesh.name]).ToList();
                            previousSelectedIndices[collidingMesh.name] = selection1Indices[collidingMesh.name].ToList();
                        }
                        else if (!NormalSwipe(heading, slicePlane))
                        {

                            //Debug.Log("swipe " + collidingMesh.name);
                            previousSelectedIndices[collidingMesh.name] = selection0Indices[collidingMesh.name].ToList();
                            previousUnselectedIndices[collidingMesh.name] = previousUnselectedIndices[collidingMesh.name].Concat(selection1Indices[collidingMesh.name]).ToList();
                        }
                        else
                        {
                            Debug.Log("Swipe not to a normal or !normal side of plane???");
                        }
                    }
                    previousNumVertices[collidingMesh.name] = collidingMesh.GetComponent<MeshFilter>().mesh.vertices.Length;
                    previousVertices[collidingMesh.name] = collidingMesh.GetComponent<MeshFilter>().mesh.vertices;

                    UVList = new List<Vector2>();
                    collidingMesh.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                    previousUVs[collidingMesh.name] = UVList.ToArray<Vector2>();

                    objWithSelections.Add(collidingMesh.name);
                    ColorMesh(collidingMesh, "swipe");

                    string debugStr = "swipe: " + collidingMesh.name + " ";

                    if (!savedOutlines.ContainsKey(collidingMesh.name))
                    {
                        savedOutlines.Add(collidingMesh.name, new HashSet<GameObject>());
                    }

                    foreach (GameObject outline in savedOutlines[collidingMesh.name])
                    {

                        if (!previousNumVertices.ContainsKey(outline.name))
                        {
                            previousNumVertices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.vertices.Length);        // Maybe want to store vertices as array instead?
                            outline.GetComponent<MeshFilter>().mesh.MarkDynamic();

                            if (!previousSelectedIndices.ContainsKey(outline.name))
                            {
                                previousSelectedIndices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.GetIndices(0).ToList());
                                previousVertices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.vertices);

                                UVList = new List<Vector2>();
                                outline.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                                previousUVs.Add(outline.name, UVList.ToArray<Vector2>());
                            }
                        }

                        SplitMesh(outline);
                        if (NormalSwipe(heading, slicePlane))
                        {
                            previousSelectedIndices[outline.name] = selection1Indices[outline.name].ToList();
                        }
                        else
                        {
                            PreviousSelectedIndices[outline.name] = selection0Indices[outline.name].ToList();
                        }

                        if (outline.name == "highlight0")
                        {
                            Debug.Log(" At swipe " + previousSelectedIndices[outline.name].Count.ToString() + " selected Indices");
                        }

                        debugStr += outline.name + " ";
                        //previousSelectedIndices[outline.name] = outline.GetComponent<MeshFilter>().mesh.GetIndices(0).ToList();
                        //TODO: check whether objWithSelections is needed for outlines.
                        objWithSelections.Add(outline.name);
                        previousNumVertices[outline.name] = outline.GetComponent<MeshFilter>().mesh.vertices.Length;
                        previousVertices[outline.name] = outline.GetComponent<MeshFilter>().mesh.vertices;

                        UVList = new List<Vector2>();
                        outline.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                        previousUVs[outline.name] = UVList.ToArray<Vector2>();
                        ColorMesh(outline, "swipe");
                        
                    }

                    GameObject savedSliceOutline = CopyObject(sliceOutlines[collidingMesh.name]); // save the highlights at the point of selection
                    savedOutlines[collidingMesh.name].Add(savedSliceOutline);

                    Debug.Log(debugStr);

                    sliceStatus = 0;
                }
            }
        }

        lastPos = currentPos;
        lastOrientation = currentOrientation;
    }

    private void GazeSelection()
    {
        RaycastHit hit;

        if (Physics.Raycast(camera.transform.position, camera.transform.forward, out hit, 1000))
        {
            if (sliceStatus == 0)
            {

                Vector3 hitPoint = hit.point;
                int hitLayer = hit.collider.gameObject.layer;
                //ShowLaser(hit, laser, camera.transform.position, hitPoint);
                //ShowReticle(hit);
                if (hit.collider.name != "floor" && hit.collider.name != "SliceNSwipeHandPlane" && hit.collider.tag != "highlightmesh")
                {
                    collidingMesh = hit.collider.gameObject;
                    if (!originalMaterial.ContainsKey(collidingMesh.name))
                    {
                        originalMaterial.Add(collidingMesh.name, collidingMesh.GetComponent<Renderer>().material);
                    }
                    if (collidingMesh.name != lastSeenObj.name)
                    {
                        collidingMesh.GetComponent<Renderer>().material = GazeSelectedMaterial(collidingMesh.GetComponent<Renderer>().material);
                        if (originalMaterial.ContainsKey(lastSeenObj.name))
                        {
                            lastSeenObj.GetComponent<Renderer>().material = originalMaterial[lastSeenObj.name];
                        }
                        lastSeenObj = collidingMesh;
                    }

                }
                else
                {
                    if (originalMaterial.ContainsKey(lastSeenObj.name))
                    {
                        lastSeenObj.GetComponent<Renderer>().material = originalMaterial[lastSeenObj.name];
                    }
                    if (hit.collider.name == "floor")
                    {
                        lastSeenObj = hit.collider.gameObject;
                    }
                    collidingMesh = null;

                }
            }
            //Debug.Log("collide: " + hit.collider.name + ", " + collidingMeshes.Count + ", state " + sliceStatus.ToString());
        }
        else if (originalMaterial.ContainsKey(lastSeenObj.name))
        {
            lastSeenObj.GetComponent<Renderer>().material = originalMaterial[lastSeenObj.name];
        }
    }

    private void FirstContactProcess(GameObject gObject, List<Vector2> UVList)
    {
        previousNumVertices.Add(gObject.name, gObject.GetComponent<MeshFilter>().mesh.vertices.Length);
        gObject.GetComponent<MeshFilter>().mesh.MarkDynamic();
        previousSelectedIndices.Add(gObject.name, gObject.GetComponent<MeshFilter>().mesh.GetIndices(0).ToList<int>());
        previousUnselectedIndices.Add(gObject.name, new List<int>());
        previousVertices.Add(gObject.name, gObject.GetComponent<MeshFilter>().mesh.vertices);

        UVList = new List<Vector2>();
        gObject.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
        previousUVs.Add(gObject.name, UVList.ToArray<Vector2>());

        gObject.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (gObject.tag != "highlightmesh")
        {
            if (!sliceOutlines.ContainsKey(gObject.name))                              //
            {                                                                             // Add an Outline for this mesh if there isn't one already
                sliceOutlines.Add(gObject.name, MakeHandOutline(gObject));    //
            }
           // originalMaterial.Add(gObject.name, gObject.GetComponent<Renderer>().materials[0]);
        }
    }

    private void ShowLaser(RaycastHit hit, GameObject laser, Vector3 laserStartPos, Vector3 hitPoint)
    {
        laser.SetActive(true);
        Transform laserTransform = laser.transform;

        laserTransform.position = Vector3.Lerp(laserStartPos, hitPoint, .5f);
        laserTransform.LookAt(hitPoint);
        laserTransform.localScale = new Vector3(laserTransform.localScale.x, laserTransform.localScale.y, hit.distance);
    }
    /* Adapted from the Interaction in VR unity tutorials here:
     * https://unity3d.com/learn/tutorials/topics/virtual-reality/interaction-vr
     */
    public void ShowReticle(RaycastHit hit)
    {
        reticle.SetActive(true);

        reticle.transform.position = hit.point;
        reticle.transform.localScale = hit.distance * new Vector3(0.1f, 0.1f, 0.1f);

        // If the reticle should use the normal of what has been hit...
        //if (hit.normal)
            // ... set it's rotation based on it's forward vector facing along the normal.
            reticle.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
        //else
            //// However if it isn't using the normal then it's local rotation should be as it was originally.
            //m_ReticleTransform.localRotation = m_OriginalRotation;
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

    private bool NormalSwipe(Vector3 swipeDirection, GameObject slicePlane)
    {
        return Vector3.Dot(swipeDirection, slicePlane.transform.up) <= 0;
    }

    private void SplitMesh(GameObject item)
    {
        Mesh mesh = item.GetComponent<MeshFilter>().mesh;
        selected0Indices.Clear();
        selected1Indices.Clear();

        int[] indices = previousSelectedIndices[item.name].ToArray();        // original indices is set to be JUST the selected part, that's why nothing else is drawn
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


        for (int i = 0; i < indices.Length / 3; i++)
        {
            triangleIndex0 = indices[3 * i];
            triangleIndex1 = indices[3 * i + 1];
            triangleIndex2 = indices[3 * i + 2];

            bool side0 = IntersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex1], ref intersectPoint0, ref intersectUV0, UVs[triangleIndex0], UVs[triangleIndex1], vertices[triangleIndex0], vertices[triangleIndex1], slicePlane);
            bool side1 = IntersectsWithPlane(transformedVertices[triangleIndex1], transformedVertices[triangleIndex2], ref intersectPoint1, ref intersectUV1, UVs[triangleIndex1], UVs[triangleIndex2], vertices[triangleIndex1], vertices[triangleIndex2], slicePlane);
            bool side2 = IntersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex2], ref intersectPoint2, ref intersectUV2, UVs[triangleIndex0], UVs[triangleIndex2], vertices[triangleIndex0], vertices[triangleIndex2], slicePlane);


            if (!side0 && !side1 && !side2) // 0 intersections
            {
                if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], slicePlane))
                {
                    AddNewIndices(selected0Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                }
                else
                {
                    AddNewIndices(selected1Indices, triangleIndex0, triangleIndex1, triangleIndex2);
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

                    if (OnNormalSideOfPlane(transformedVertices[triangleIndex1], slicePlane))
                    {

                        // Add the indices for various triangles to selected and unselected

                        AddNewIndices(selected0Indices, intersectIndex1, intersectIndex0, triangleIndex1);
                        AddNewIndices(selected1Indices, triangleIndex0, intersectIndex0, intersectIndex1);
                        AddNewIndices(selected1Indices, triangleIndex2, triangleIndex0, intersectIndex1);

                    }
                    else
                    {
                        AddNewIndices(selected1Indices, intersectIndex1, intersectIndex0, triangleIndex1);
                        AddNewIndices(selected0Indices, triangleIndex0, intersectIndex0, intersectIndex1);
                        AddNewIndices(selected0Indices, triangleIndex2, triangleIndex0, intersectIndex1);
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

                    if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], slicePlane))
                    {
                        AddNewIndices(selected0Indices, intersectIndex2, triangleIndex0, intersectIndex0);
                        AddNewIndices(selected1Indices, triangleIndex2, intersectIndex2, intersectIndex0);
                        AddNewIndices(selected1Indices, triangleIndex1, triangleIndex2, intersectIndex0);
                    }
                    else
                    {
                        AddNewIndices(selected1Indices, intersectIndex2, triangleIndex0, intersectIndex0);
                        AddNewIndices(selected0Indices, triangleIndex2, intersectIndex2, intersectIndex0);
                        AddNewIndices(selected0Indices, triangleIndex1, triangleIndex2, intersectIndex0);
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

                    if (OnNormalSideOfPlane(transformedVertices[triangleIndex2], slicePlane))
                    {
                        AddNewIndices(selected0Indices, intersectIndex1, triangleIndex2, intersectIndex2);
                        AddNewIndices(selected1Indices, intersectIndex2, triangleIndex0, intersectIndex1);
                        AddNewIndices(selected1Indices, triangleIndex0, triangleIndex1, intersectIndex1);
                    }
                    else
                    {
                        AddNewIndices(selected1Indices, intersectIndex1, triangleIndex2, intersectIndex2);
                        AddNewIndices(selected0Indices, intersectIndex2, triangleIndex0, intersectIndex1);
                        AddNewIndices(selected0Indices, triangleIndex0, triangleIndex1, intersectIndex1);
                    }
                }
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, UVs);


        //selected0 and selected1 are the locally used lists of indices. selection 0 & 1 are global dictionaries storing arrays of indices for every object
        //i apologize for this
        selection0Indices[item.name] = selected0Indices.ToArray();
        selection1Indices[item.name] = selected1Indices.ToArray();

        if (item.name == "highlight0")
        {
            Debug.Log(" At SPLIT " + previousSelectedIndices[item.name].Count.ToString() + " selected Indices. 0: " + selection0Indices[item.name].Count().ToString() + ", 1: " + selection1Indices.Count().ToString());
        }

        if (item.gameObject.tag != "highlightmesh")
        {
            sliceOutlines[item.name].GetComponent<MeshFilter>().mesh = new Mesh();
            CreateOutlineMesh(outlinePoints, slicePlane, sliceOutlines[item.name].GetComponent<MeshFilter>().sharedMesh);
        }

        outlinePoints.Clear();

    }

    private void ColorMesh(GameObject item,  string mode)
    {
        Mesh mesh = item.GetComponent<MeshFilter>().mesh;
        

        if (item.gameObject.tag != "highlightmesh")
        {
            Material[] materials = new Material[3];
            if (mode == "slice")
            {
                // Debug.Log(item.name + " 0: " + selection0Indices[item.name].Length.ToString() + " 1: " + selection1Indices[item.name].Length.ToString());
                if (selection0Indices.ContainsKey(item.name))
                {
                    mesh.subMeshCount = 3;

                    mesh.SetTriangles(selection0Indices[item.name], 0);
                    mesh.SetTriangles(selection1Indices[item.name], 1);
                    mesh.SetTriangles(previousUnselectedIndices[item.name], 2);

                    // materials[2] = item.GetComponent<Renderer>().materials[0];
                    materials[0] = Resources.Load("Blue Material") as Material;
                    materials[1] = Resources.Load("Green Material") as Material;

                    Material baseMaterial = originalMaterial[item.name];
                    materials[2] = DetermineBaseMaterial(baseMaterial);
                } else
                {
                    // This case handles backing out of a slice. In that case it is indicated by clearing selection0Indices for the object
                    if (objWithSelections.Contains(item.name))
                    {
                        mesh.subMeshCount = 2;
                        Debug.Log("Remove slice with selections");
                        mesh.SetTriangles(previousSelectedIndices[item.name], 1);
                        mesh.SetTriangles(previousUnselectedIndices[item.name], 0);

                        Material baseMaterial = originalMaterial[item.name];
                        materials[0] = DetermineBaseMaterial(baseMaterial);         // Sets unselected as transparent
                        materials[1] = Resources.Load("Selected") as Material;      // May need to specify which submesh we get this from? -> THIS SETS SELECTION AS ORANGE STUFF
                        //Debug.Log("num submeshes: " + mesh.subMeshCount.ToString() + " mats len: " + materials.Length.ToString() + " first mat: " + materials[0].name + ", " + materials[1].name);
                    }
                    else
                    {
                        Debug.Log("Remove first slice");
                        mesh.subMeshCount = 1;
                        mesh.SetTriangles(previousSelectedIndices[item.name], 0);

                        materials[0] = originalMaterial[item.name];
                    }
                }
            }
            else if (mode == "swipe")
            {
                mesh.subMeshCount = 2;
                Debug.Log(item.name + " s: " + previousSelectedIndices[item.name].Count.ToString() + " u: " + previousUnselectedIndices[item.name].Count.ToString());
                mesh.SetTriangles(previousSelectedIndices[item.name], 1);
                mesh.SetTriangles(previousUnselectedIndices[item.name], 0);

                Material baseMaterial = originalMaterial[item.name];
                materials[0] = DetermineBaseMaterial(baseMaterial);         // Sets unselected as transparent
                //materials[0] = baseMaterial;
                materials[1] = Resources.Load("Selected") as Material;      // May need to specify which submesh we get this from? -> THIS SETS SELECTION AS ORANGE STUFF
            }
            item.GetComponent<Renderer>().materials = materials;
            //Debug.Log(item.name + " M0: " + materials[0].name + " M1: " + materials[1].name);
        }
        else if (item.gameObject.tag == "highlightmesh" ) //&& mode == "slice")
        {
            if (item.name == "highlight0")
            {
                Debug.Log(" At Color " + previousSelectedIndices[item.name].Count.ToString() + " selected Indices");
            }

            mesh.subMeshCount = 1;
            mesh.SetTriangles(previousSelectedIndices[item.name], 0);
            Debug.Log("                  coloring: " + item.name);

        }
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
            Debug.Log("Outline mesh was ordered: " + outlineMesh.name);
        }

       // points.Add(points.ElementAt(0)); //Add the first point again at the end to make a loop.

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

        meshVertices.Add(meshVertices[0]);
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

    Material GazeSelectedMaterial(Material baseMaterial)
    {
        if (baseMaterial.name == "TransparenSighted")
        {
            return baseMaterial;
        }
        else
        {
            Material transparentBase = new Material(baseMaterial);
            transparentBase.name = "TransparentSighted";
            transparentBase.color = new Color(1.0f, 0f, 1.0f, 0.5f);
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
    /// <param name="item"></param>
    /// <returns></returns>
    private GameObject MakeHandOutline(GameObject item)
    {
        string meshName = item.name;
        GameObject newOutline = new GameObject();
        newOutline.name = meshName + " highlight";
        newOutline.AddComponent<MeshRenderer>();
        newOutline.AddComponent<MeshFilter>();
        newOutline.tag = "highlightmesh";
        newOutline.GetComponent<MeshFilter>().mesh = new Mesh();
        newOutline.GetComponent<MeshFilter>().mesh.MarkDynamic();
        newOutline.GetComponent<Renderer>().material = Resources.Load("TestMaterial") as Material;


        newOutline.transform.position = item.transform.position;
        newOutline.transform.localScale = item.transform.localScale;
        newOutline.transform.rotation = item.transform.rotation;

        return newOutline;
    }
}