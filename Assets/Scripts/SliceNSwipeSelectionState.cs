using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public class SliceNSwipeSelectionState : InteractionState
{
    private int selectionCount = 1;

    public GameObject camera;// = new GameObject();
    private GameObject laser;
    private GameObject reticle;

    private const bool debug = false;
    private const float motionThreshold = 0.03f;
    bool isExperiment;
    bool swapControllers;
    //bool canTransition;

    private enum SliceStatusState
    {
        ReadyToSlice = 0,
        SliceActiveReadyToSwipe
    }

    private static SliceStatusState sliceStatus = SliceStatusState.ReadyToSlice;    //0 if you haven't just made a slice, 1 if you have and you need to select.
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

    SelectionData selectionData;
    UndoManager undoManager;

    //private static Dictionary<string, Vector3[]> previousVertices;              // Key = name of obj with mesh, Value = all vertices of the mesh at the time of last click
    //private static Dictionary<string, Vector2[]> previousUVs;                   // Key = name of obj with mesh, Value = all UVs of the mesh at the time of last click
    //private Dictionary<string, List<int>> previousUnselectedIndices;                // Key = name of object with mesh, Value = all indices that have not been selected (updated when user clicks)
    //private static Dictionary<string, int> previousNumVertices;                 // Key = name of object with mesh, Value = original set of vertices (updated when user clicks and mesh is split)
    //private static Dictionary<string, List<int>> previousSelectedIndices;           // key = name of object with mesh, Value = original set of selected indices (updated when user clicks)
    //private static HashSet<string> objWithSelections;                           // Collection of the the names of all the meshes that have had pieces selected from them.
    //private static Dictionary<string, HashSet<GameObject>> savedOutlines;       // Key = name of object in model, Value = all the SAVED outline game objects attached to it
    //private static Dictionary<string, GameObject> sliceOutlines;                 // left hand outlines per model that are currently being manipulated (KEY = name of model object, VALUE = outline object)
    private Dictionary<string, Material> originalMaterial;

    private static Dictionary<string, int[]> sliced0Indices;
    private static Dictionary<string, int[]> sliced1Indices;

    // We use these to keep track of the two possible alternatives when you swipe. After a slice, we reset the approprate (depending on swipe direction) one to SelectionData.TriangleStates.
    public Dictionary<Triangle, SelectionData.TriangleSelectionState> triangleStates0;
    public Dictionary<Triangle, SelectionData.TriangleSelectionState> triangleStates1;

    private List<OutlinePoint> unsortedOutlinePoints;    // Pairs of two connected points to be used in drawing an outline mesh

    List<int> selected0Indices;      // Reused for each mesh during ProcessMesh()
    List<int> selected1Indices;    // ^^^^

    //public static Dictionary<string, List<int>> PreviousSelectedIndices
    //{
    //    get { return previousSelectedIndices; }
    //}

    //public static Dictionary<string, Vector3[]> PreviousVertices
    //{
    //    get { return previousVertices; }
    //}

    //public static Dictionary<string, Vector2[]> PreviousUVs
    //{
    //    get { return previousUVs;  }
    //}

    //public static Dictionary<string, int> PreviousNumVertices
    //{
    //    get { return previousNumVertices; }
    //}
    //public static HashSet<string> ObjectsWithSelections
    //{
    //    get { return objWithSelections; }
    //}
    //public static Dictionary<string, HashSet<GameObject>> SavedOutlines
    //{
    //    get { return savedOutlines; }
    //    set { savedOutlines = value; }
    //}
    //public static Dictionary<string, GameObject> IntersectOutlines
    //{
    //    get { return sliceOutlines; }
    //}
    //public static int SliceStatus
    //{
    //    get { return sliceStatus; }
    //}

    /// <summary>
    /// State that activates whenever there's a mesh between the user's controllers. Allows user to select surfaces and progressively refine their selection.
    /// Currently only works when selecting a single object.
    /// </summary>
    /// <param name="controller0Info"></param>
    /// <param name="controller1Info"></param>
    public SliceNSwipeSelectionState(ControllerInfo controller0Info, ControllerInfo controller1Info, SelectionData sharedData, UndoManager undoMgr, bool experiment = false, bool zeroDominant = true)
    {

        //Debug.Log("Constructing Slice State");
        // NOTE: Selecting more than one mesh will result in highlights appearing in the wrong place
        desc = "SliceNSwipeSelectionState";
        ControllerInfo controller0 = controller0Info;
        ControllerInfo controller1 = controller1Info;

        isExperiment = experiment;
        swapControllers = !zeroDominant;

        camera = GameObject.Find("Camera (eye)"); //.transform.GetChild(0).gameObject;
        laser = GameObject.Find("LaserParent").transform.GetChild(0).gameObject;
        reticle = GameObject.Find("ReticleParent").transform.GetChild(0).gameObject;
      //  handTrail = GameObject.Find("HandTrail");
        swordLine = GameObject.Find("SwordLine");

        DetermineDominantController(controller0, controller1);

        planeLayer = LayerMask.NameToLayer("PlaneLayer");

        slicePlane = CreateHandPlane(mainController, "SliceNSwipeHandPlane");

        //cubeColliders = new HashSet<GameObject>();

        //TODO: should these persist between states? Yes so only make one instance of the state. Should use the Singleton pattern here//TODO

        selectionData = sharedData;
        undoManager = undoMgr;

        //objWithSelections = new HashSet<string>();
        //previousNumVertices = new Dictionary<string, int>();              // Keeps track of how many vertices a mesh should have
        //previousUnselectedIndices = new Dictionary<string, List<int>>();      // Keeps track of indices that were previously unselected
        //previousSelectedIndices = new Dictionary<string, List<int>>();
        //previousVertices = new Dictionary<string, Vector3[]>();
        //previousUVs = new Dictionary<string, Vector2[]>();
        //savedOutlines = new Dictionary<string, HashSet<GameObject>>();
        selected0Indices = new List<int>();
        selected1Indices = new List<int>();
        unsortedOutlinePoints = new List<OutlinePoint>();
        originalMaterial = new Dictionary<string, Material>();
        lastSeenObj = new GameObject();

        sliced0Indices = new Dictionary<string, int[]>();
        sliced1Indices = new Dictionary<string, int[]>();

        //sliceOutlines = new Dictionary<string, GameObject>();

        if (isExperiment)
        {
            collidingMesh = GameObject.Find("TestObj").transform.GetChild(1).gameObject;
            originalMaterial.Add(collidingMesh.name, collidingMesh.GetComponent<Renderer>().material);
        }
        else
        {
            collidingMesh = null;
        }
    }

    public override bool CanTransition()
    {
        bool allowed;
        if(sliceStatus == SliceStatusState.SliceActiveReadyToSwipe)
        {
            allowed = false;
        }
        else
        {
            allowed = true;
        }
        return allowed;
    }

    private void DetermineDominantController(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        //Debug.Log("Set Dominant Hand");
        if (!swapControllers)
        {
            mainController = controller0Info;
            controller0Info.controller.gameObject.transform.GetChild(0).gameObject.SetActive(false);
            controller0Info.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = true;    //hand rendering
            altController = controller1Info;
            controller1Info.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true);                     //controller rendering
            controller1Info.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;
        }
        else
        {
            mainController = controller1Info;
            controller1Info.controller.gameObject.transform.GetChild(0).gameObject.SetActive(false);
            controller1Info.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = true;    //hand rendering
            altController = controller0Info;
            controller0Info.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true);
            controller0Info.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;   //controller rendering
        }
        //GameObject hand = GameObject.Find("Hand");

        //handTrail.transform.parent = mainController.controller.transform;
        //handTrail.transform.localPosition = new Vector3 (0f, 0f, 0f);
        //Debug.Log(handTrail.transform.position.ToString());

        //swordLine.transform.parent = mainController.controller.transform;
        //swordLine.transform.localPosition = new Vector3(0f, -0.01f, 0.4f);
        //swordLine.transform.Rotate(new Vector3(90, 0, 0));


        //  altController.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false; //disable hand rendering
        //  altController.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true); //enable rendering of controllers
        //Debug.Log("Remember to Implement DetermineDominantController()");
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
        //UnityEngine.Object.DontDestroyOnLoad(handPlane);
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
        slicePlane.transform.position = mainController.controller.transform.position + -0.01f * mainController.trackedObj.transform.up.normalized;

        Vector3 mxf = Vector3.Cross(movement, mainController.controller.transform.forward).normalized;

        slicePlane.transform.up = mxf;

        swordLine.gameObject.transform.GetComponent<MeshRenderer>().enabled = true;
    }

    public override void Deactivate()
    {
        swordLine.gameObject.transform.GetComponent<MeshRenderer>().enabled = false;    // Disable line rendering

        List<int> indices;
        if (collidingMesh != null)
        {
            Mesh mesh = collidingMesh.GetComponent<MeshFilter>().mesh;
            mesh.subMeshCount = 2;
            indices = SelectionData.PreviousSelectedIndices[collidingMesh.name].ToList(); // the indices of last selection

            if (sliceStatus == SliceStatusState.SliceActiveReadyToSwipe)
            {
                sliced0Indices.Remove(collidingMesh.name);
                sliced1Indices.Remove(collidingMesh.name);
                ColorMesh(collidingMesh, "slice");

                if (!isExperiment)
                {
                    if (OutlineManager.preSelectionOutlines.ContainsKey(collidingMesh.name)) //|| rightOutlines.ContainsKey(collidingObj.name))
                    {
                        for (int i = OutlineManager.preSelectionOutlines[collidingMesh.name].Count - 1; i >= 0; i--)
                        {
                            UnityEngine.Object.Destroy(OutlineManager.preSelectionOutlines[collidingMesh.name][i]);
                            OutlineManager.preSelectionOutlines[collidingMesh.name].RemoveAt(i);
                        }
                    }
                }
            }
            else
            {
                if (!SelectionData.ObjectsWithSelections.Contains(collidingMesh.name))
                {
                    mesh.subMeshCount = 1;
                    mesh.SetTriangles(SelectionData.PreviousSelectedIndices[collidingMesh.name], 0);

                    collidingMesh.GetComponent<MeshRenderer>().material = originalMaterial[collidingMesh.name];
                }
                else
                {
                    ColorMesh(collidingMesh, "swipe");
                }
            }
        }
        //sliceStatus = 0;
    }

    public override string HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        string eventString = "";
        Vector3 currentPos = mainController.trackedObj.transform.position;
        Vector3 currentOrientation = mainController.trackedObj.transform.forward;

        swordLine.transform.position = currentPos + 0.4f * currentOrientation.normalized + -0.01f * mainController.trackedObj.transform.up.normalized;//new Vector3(0f, -0.01f, 0.4f); ;
        swordLine.transform.rotation = mainController.trackedObj.transform.rotation;
        swordLine.transform.Rotate(new Vector3(90, 0, 0));

        if (lastPos == new Vector3(0, 0, 0))
        {
            lastPos = currentPos;
            lastOrientation = currentOrientation;
        }


        List<Vector2> UVList = new List<Vector2>();

        if (sliceStatus == SliceStatusState.ReadyToSlice)
        {
            if (mainController.device.GetPressDown(SteamVR_Controller.ButtonMask.Grip) || altController.device.GetPressDown(SteamVR_Controller.ButtonMask.Grip))// || Input.GetButtonDown("ViveGrip"))
            {
                Debug.Log("undo button pressed");
                undoManager.Undo();

            }

            if (!isExperiment)
            {
                GazeSelection();
            }
        }
        if (collidingMesh != null)
        { 
            if (sliceStatus == SliceStatusState.ReadyToSlice && Vector3.Distance(lastPos, currentPos) <= motionThreshold) //small movement and you haven't made a slice
            {
                UpdatePlane(lastPos - currentPos);

                if (!SelectionData.PreviousNumVertices.ContainsKey(collidingMesh.name)) // if the original vertices are not stored already, store them (first time seeing object)
                {
                    eventString = "first collision with " + collidingMesh.name;
                    FirstContactProcess(collidingMesh, UVList);
                }

                // Debug.Log("not slice: " + dist.ToString());
            }
            else if (sliceStatus == SliceStatusState.ReadyToSlice && !mainController.device.GetHairTrigger() && Vector3.Distance(lastPos, currentPos) > motionThreshold ) // you just made a big slicing movement
            {
                eventString = "slice for selection " + selectionCount.ToString();
                //string debugString = "";
                UpdatePlane(lastPos - currentPos);

                /* to make a cut plane you need to get the transform.forward and also the difference between last and current positions*/

                if (!SelectionData.PreviousNumVertices.ContainsKey(collidingMesh.name))
                {
                    eventString = "first collision with " + collidingMesh.name;
                    FirstContactProcess(collidingMesh, UVList);
                    //debugString += collidingMesh.name + "  ";
                } else if (!SelectionData.PreviousUnselectedIndices.ContainsKey(collidingMesh.name))
                {
                    SelectionData.PreviousUnselectedIndices.Add(collidingMesh.name, new int[0]);
                }
                else
                {
                    //if (!sliceOutlines.ContainsKey(collidingMesh.name))                              //
                    //{                                                                             // Add an Outline for this mesh if there isn't one already
                    //    sliceOutlines.Add(collidingMesh.name, OutlineManager.MakeHandOutline(collidingMesh));    //
                    //}
                    SplitMesh(collidingMesh);
                    ColorMesh(collidingMesh, "slice");
                    //debugString += collidingMesh.name + "  ";
                    collidingMesh.GetComponent<MeshFilter>().mesh.UploadMeshData(false);
                }
                //Debug.Log("SLICE: " + debugString);

                sliceStatus = SliceStatusState.SliceActiveReadyToSwipe;
            }
            else if (sliceStatus == SliceStatusState.SliceActiveReadyToSwipe)
            {
                if (altController.device.GetHairTrigger())          //remove last swipe
                {
                    eventString = "swipe selection " + selectionCount.ToString();
                    selectionCount++;
                    sliceStatus = SliceStatusState.ReadyToSlice;

                    sliced0Indices.Remove(collidingMesh.name);
                    sliced1Indices.Remove(collidingMesh.name);


                    if (!isExperiment)
                    {
                        foreach (GameObject outline in OutlineManager.preSelectionOutlines[collidingMesh.name])
                        {
                            UnityEngine.Object.Destroy(outline);
                        }
                        OutlineManager.preSelectionOutlines[collidingMesh.name].Clear();
                    }

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
                else if (mainController.device.GetHairTrigger() && Vector3.Distance(lastPos, currentPos) > motionThreshold - 0.02f) //Swipe with main trigger held to select
                {
                    //Debug.Log("Swipe!! " + Vector3.Distance(lastPos, currentPos).ToString());
                    /* if movement is big & towards normal side of plane, discard the indeces on that side.
                     * else if away from normal side of plane, discard indeces on that side.
                     * make discarded indeces transparent and delete slicing plane.
                     */

                    Vector3 heading = (currentPos - lastPos).normalized;

                    SelectionData.RecentlySelectedObj.Clear();
                    SelectionData.RecentlySelectedObjNames.Clear();

                    if (SelectionData.NumberOfSelections.ContainsKey(collidingMesh.name))
                    {
                        SelectionData.NumberOfSelections[collidingMesh.name] = SelectionData.NumberOfSelections[collidingMesh.name] + 1;
                    }
                    else
                    {
                        SelectionData.NumberOfSelections.Add(collidingMesh.name, 1);
                    }

                    if (SelectionData.NumberOfSelections[collidingMesh.name] == 1)
                    {
                        SelectionData.RecentUVs.Add(collidingMesh.name, SelectionData.PreviousUVs[collidingMesh.name]);
                        SelectionData.RecentVertices.Add(collidingMesh.name, SelectionData.PreviousVertices[collidingMesh.name]);
                        SelectionData.RecentNumVertices.Add(collidingMesh.name, SelectionData.PreviousNumVertices[collidingMesh.name]);
                        SelectionData.RecentSelectedIndices.Add(collidingMesh.name, SelectionData.PreviousSelectedIndices[collidingMesh.name]);
                    }
                    else
                    {
                        //Moving previous info to recent before updating, for undoing - NEW
                        SelectionData.RecentUVs[collidingMesh.name] = SelectionData.PreviousUVs[collidingMesh.name];
                        SelectionData.RecentVertices[collidingMesh.name] = SelectionData.PreviousVertices[collidingMesh.name];
                        SelectionData.RecentNumVertices[collidingMesh.name] = SelectionData.PreviousNumVertices[collidingMesh.name];
                        if (SelectionData.RecentUnselectedIndices.ContainsKey(collidingMesh.name))
                        {
                            SelectionData.RecentUnselectedIndices[collidingMesh.name] = SelectionData.PreviousUnselectedIndices[collidingMesh.name];
                        }
                        else
                        {
                            //On the first selection everything is stored as selected.
                            SelectionData.RecentUnselectedIndices.Add(collidingMesh.name, SelectionData.PreviousUnselectedIndices[collidingMesh.name]);
                        }
                        SelectionData.RecentSelectedIndices[collidingMesh.name] = SelectionData.PreviousSelectedIndices[collidingMesh.name];
                    }
                    SelectionData.RecentlySelectedObjNames.Add(collidingMesh.name);
                    SelectionData.RecentlySelectedObj.Add(collidingMesh);

                    if (SelectionData.PreviousNumVertices.ContainsKey(collidingMesh.name) && collidingMesh.gameObject.tag != "highlightmesh")
                    {
                        if (isSwipeTowardsNormalSideOfPlane(heading, slicePlane))
                        {
                            //Debug.Log("swipe " + collidingMesh.name);
                            SelectionData.PreviousUnselectedIndices[collidingMesh.name] = SelectionData.PreviousUnselectedIndices[collidingMesh.name].Concat(sliced0Indices[collidingMesh.name]).ToArray();
                            SelectionData.PreviousSelectedIndices[collidingMesh.name] = sliced1Indices[collidingMesh.name];
                            SelectionData.RecentTriangleStates = SelectionData.TriangleStates;
                            SelectionData.TriangleStates = triangleStates1;
                        }
                        else
                        {

                            //Debug.Log("swipe " + collidingMesh.name);
                            SelectionData.PreviousSelectedIndices[collidingMesh.name] = sliced0Indices[collidingMesh.name];
                            SelectionData.PreviousUnselectedIndices[collidingMesh.name] = SelectionData.PreviousUnselectedIndices[collidingMesh.name].Concat(sliced1Indices[collidingMesh.name]).ToArray();
                            SelectionData.RecentTriangleStates = SelectionData.TriangleStates;
                            SelectionData.TriangleStates = triangleStates0;
                        }
                    }
                    SelectionData.PreviousNumVertices[collidingMesh.name] = collidingMesh.GetComponent<MeshFilter>().mesh.vertices.Length;
                    SelectionData.PreviousVertices[collidingMesh.name] = collidingMesh.GetComponent<MeshFilter>().mesh.vertices;

                    UVList = new List<Vector2>();
                    collidingMesh.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                    SelectionData.PreviousUVs[collidingMesh.name] = UVList.ToArray<Vector2>();

                    SelectionData.ObjectsWithSelections.Add(collidingMesh.name);
                    ColorMesh(collidingMesh, "swipe");

                    //string debugStr = "swipe: " + collidingMesh.name + " ";

                    if (!isExperiment)
                    {
                        if (!SelectionData.SavedOutlines.ContainsKey(collidingMesh.name))
                        {
                            SelectionData.SavedOutlines.Add(collidingMesh.name, new HashSet<GameObject>());
                        }

                        foreach (GameObject outline in SelectionData.SavedOutlines[collidingMesh.name])
                        {

                            if (!SelectionData.PreviousNumVertices.ContainsKey(outline.name))
                            {
                                SelectionData.PreviousNumVertices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.vertices.Length);        // Maybe want to store vertices as array instead?
                                outline.GetComponent<MeshFilter>().mesh.MarkDynamic();

                                if (!SelectionData.PreviousSelectedIndices.ContainsKey(outline.name))
                                {
                                    SelectionData.PreviousSelectedIndices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.GetIndices(0));
                                    SelectionData.PreviousVertices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.vertices);

                                    UVList = new List<Vector2>();
                                    outline.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                                    SelectionData.PreviousUVs.Add(outline.name, UVList.ToArray<Vector2>());
                                }
                            }

                            SplitMesh(outline);
                            if (isSwipeTowardsNormalSideOfPlane(heading, slicePlane))
                            {
                                SelectionData.PreviousSelectedIndices[outline.name] = sliced1Indices[outline.name];
                            }
                            else
                            {
                                SelectionData.PreviousSelectedIndices[outline.name] = sliced0Indices[outline.name];
                            }

                            //if (outline.name == "highlight0")
                            //{
                            //    Debug.Log(" At swipe " + SelectionData.PreviousSelectedIndices[outline.name].Count().ToString() + " selected Indices");
                            //}

                            //debugStr += outline.name + " ";
                            //previousSelectedIndices[outline.name] = outline.GetComponent<MeshFilter>().mesh.GetIndices(0).ToList();
                            //TODO: check whether objWithSelections is needed for outlines.
                            SelectionData.ObjectsWithSelections.Add(outline.name);
                            SelectionData.PreviousNumVertices[outline.name] = outline.GetComponent<MeshFilter>().mesh.vertices.Length;
                            SelectionData.PreviousVertices[outline.name] = outline.GetComponent<MeshFilter>().mesh.vertices;

                            UVList = new List<Vector2>();
                            outline.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                            SelectionData.PreviousUVs[outline.name] = UVList.ToArray<Vector2>();
                            ColorMesh(outline, "swipe");

                        }

                        foreach (GameObject outline in OutlineManager.preSelectionOutlines[collidingMesh.name])
                        {
                            GameObject savedOutline = OutlineManager.CopyObject(outline); // save the highlights at the point of selection

                            //GameObject savedOutline = OutlineManager.MakeNewOutline(outline);
                            SelectionData.SavedOutlines[collidingMesh.name].Add(savedOutline);

                            UnityEngine.Object.Destroy(outline);
                        }
                        OutlineManager.preSelectionOutlines[collidingMesh.name].Clear();
                    }

                    //Debug.Log("Slice Selection: " + collidingMesh.name);

                    //Debug.Log(debugStr);

                    sliceStatus = SliceStatusState.ReadyToSlice;
                }
            }
        }

        lastPos = currentPos;
        lastOrientation = currentOrientation;

        return eventString;
    }

    private void GazeSelection()
    {
        RaycastHit hit;

        if (Physics.Raycast(camera.transform.position, camera.transform.forward, out hit, 1.7f))
        {
                //Vector3 hitPoint = hit.point;
                //int hitLayer = hit.collider.gameObject.layer;
                //ShowLaser(hit, laser, camera.transform.position, hitPoint);
                //ShowReticle(hit);
                //if (hit.collider.name != "floor")
                //{
                collidingMesh = hit.collider.gameObject;
                if (!originalMaterial.ContainsKey(collidingMesh.name))
                {
                    originalMaterial.Add(collidingMesh.name, collidingMesh.GetComponent<Renderer>().material);
                }

            if (lastSeenObj == null || collidingMesh.name != lastSeenObj.name)
                {
                    collidingMesh.GetComponent<Renderer>().material = GazeSelectedMaterial(collidingMesh.GetComponent<Renderer>().material);
                    if (lastSeenObj != null && originalMaterial.ContainsKey(lastSeenObj.name))
                    {
                        lastSeenObj.GetComponent<Renderer>().material = originalMaterial[lastSeenObj.name];
                    }
                    lastSeenObj = collidingMesh;
                    //Debug.Log("lastSeenObj is reassigned: " + collidingMesh.name);
                }

                //}
                //else
                //{
                //    if (originalMaterial.ContainsKey(lastSeenObj.name))
                //    {
                //        lastSeenObj.GetComponent<Renderer>().material = originalMaterial[lastSeenObj.name];
                //    }
                //    if (hit.collider.name == "floor")
                //    {
                //        lastSeenObj = hit.collider.gameObject;
                //        //Debug.Log("lastSeenObj is reassigned (hit collider): " + hit.collider.name);
                //    }
                //    collidingMesh = null;

                //}
            
            //Debug.Log("collide: " + hit.collider.name + ", " + collidingMeshes.Count + ", state " + sliceStatus.ToString());
        }
        else
        {
            if (lastSeenObj != null && originalMaterial.ContainsKey(lastSeenObj.name))
            {
                lastSeenObj.GetComponent<Renderer>().material = originalMaterial[lastSeenObj.name];
                lastSeenObj = null;
            }
            collidingMesh = null;
        }
    }

    private void FirstContactProcess(GameObject gObject, List<Vector2> UVList)
    {
        SelectionData.PreviousNumVertices.Add(gObject.name, gObject.GetComponent<MeshFilter>().mesh.vertices.Length);
        gObject.GetComponent<MeshFilter>().mesh.MarkDynamic();
        SelectionData.PreviousSelectedIndices.Add(gObject.name, gObject.GetComponent<MeshFilter>().mesh.GetIndices(0));
        SelectionData.PreviousUnselectedIndices.Add(gObject.name, new int[gObject.GetComponent<MeshFilter>().mesh.GetIndices(0).Length]);
        SelectionData.PreviousVertices.Add(gObject.name, gObject.GetComponent<MeshFilter>().mesh.vertices);

        UVList = new List<Vector2>();
        gObject.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
        SelectionData.PreviousUVs.Add(gObject.name, UVList.ToArray<Vector2>());

        gObject.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        //if (gObject.tag != "highlightmesh")
        //{
        //    //if (!sliceOutlines.ContainsKey(gObject.name))                              //
        //    //{                                                                             // Add an Outline for this mesh if there isn't one already
        //    //    sliceOutlines.Add(gObject.name, MakeHandOutline(gObject));    //
        //    //}
        //   // originalMaterial.Add(gObject.name, gObject.GetComponent<Renderer>().materials[0]);
        //}
    }

    //private void ShowLaser(RaycastHit hit, GameObject laser, Vector3 laserStartPos, Vector3 hitPoint)
    //{
    //    laser.SetActive(true);
    //    Transform laserTransform = laser.transform;

    //    laserTransform.position = Vector3.Lerp(laserStartPos, hitPoint, .5f);
    //    laserTransform.LookAt(hitPoint);
    //    laserTransform.localScale = new Vector3(laserTransform.localScale.x, laserTransform.localScale.y, hit.distance);
    //}
    ///* Adapted from the Interaction in VR unity tutorials here:
    // * https://unity3d.com/learn/tutorials/topics/virtual-reality/interaction-vr
    // */
    //public void ShowReticle(RaycastHit hit)
    //{
    //    reticle.SetActive(true);

    //    reticle.transform.position = hit.point;
    //    reticle.transform.localScale = hit.distance * new Vector3(0.1f, 0.1f, 0.1f);

    //    // If the reticle should use the normal of what has been hit...
    //    //if (hit.normal)
    //        // ... set it's rotation based on it's forward vector facing along the normal.
    //        reticle.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
    //    //else
    //        //// However if it isn't using the normal then it's local rotation should be as it was originally.
    //        //m_ReticleTransform.localRotation = m_OriginalRotation;
    //}

    ///// <summary>
    ///// Creates a new game object with the same position, rotation, scale, material, and mesh as the original.
    ///// </summary>
    ///// <param name="original"></param>
    ///// <returns></returns>
    //private GameObject CopyObject(GameObject original)
    //{
    //    GameObject copy = new GameObject();
    //    copy.AddComponent<MeshRenderer>();
    //    copy.AddComponent<MeshFilter>();
    //    copy.transform.position = original.transform.position;
    //    copy.transform.rotation = original.transform.rotation;
    //    copy.transform.localScale = original.transform.localScale;
    //    copy.GetComponent<MeshRenderer>().material = original.GetComponent<MeshRenderer>().material;
    //    copy.GetComponent<MeshFilter>().mesh = original.GetComponent<MeshFilter>().mesh;
    //    copy.tag = "highlightmesh"; // tag this object as a highlight
    //    copy.name = "Slice highlight" + outlineObjectCount;
    //    outlineObjectCount++;

    //    return copy;
    //}

    private bool OnNormalSideOfPlane(Vector3 pt, GameObject plane)
    {
        return Vector3.Dot(plane.transform.up, pt) >= Vector3.Dot(plane.transform.up, plane.transform.position);
    }

    private bool isSwipeTowardsNormalSideOfPlane(Vector3 swipeDirection, GameObject slicePlane)
    {
        return Vector3.Dot(swipeDirection, slicePlane.transform.up) >= 0;
    }

    private void SplitMesh(GameObject item)
    {
        Mesh mesh = item.GetComponent<MeshFilter>().mesh;
        selected0Indices.Clear();
        selected1Indices.Clear();

        int[] indices = SelectionData.PreviousSelectedIndices[item.name].ToArray();        // original indices is set to be JUST the selected part, that's why nothing else is drawn
        List<Vector3> vertices = SelectionData.PreviousVertices[item.name].ToList();

        List<Vector2> UVs = SelectionData.PreviousUVs[item.name].ToList();
        int numVertices = SelectionData.PreviousNumVertices[item.name];

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

        triangleStates0 = new Dictionary<Triangle, SelectionData.TriangleSelectionState>(SelectionData.TriangleStates);
        triangleStates1 = new Dictionary<Triangle, SelectionData.TriangleSelectionState>(SelectionData.TriangleStates);


        for (int i = 0; i < indices.Length / 3; i++)
        {
            triangleIndex0 = indices[3 * i];
            triangleIndex1 = indices[3 * i + 1];
            triangleIndex2 = indices[3 * i + 2];

            SelectionData.TriangleSelectionState currentTriangleState = SelectionData.TriangleSelectionState.UnselectedOrigUnselectedNow;
            if (isExperiment && item.gameObject.tag != "highlightmesh")
            {
                Triangle tri = new Triangle(triangleIndex0, triangleIndex1, triangleIndex2);
                try
                {
                    currentTriangleState = SelectionData.TriangleStates[tri];
                }
                catch (KeyNotFoundException)
                {
                    Debug.Log("Error triangle does not exist in dictionary");
                    Debug.Break();
                }
            }

            bool side0 = false;
            bool side1 = false;
            bool side2 = false;

            if (BoundingCircleIntersectsWithPlane(slicePlane, transformedVertices[triangleIndex0], transformedVertices[triangleIndex1], transformedVertices[triangleIndex2]))
            {
                side0 = IntersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex1], ref intersectPoint0, ref intersectUV0, UVs[triangleIndex0], UVs[triangleIndex1], vertices[triangleIndex0], vertices[triangleIndex1], slicePlane);
                side1 = IntersectsWithPlane(transformedVertices[triangleIndex1], transformedVertices[triangleIndex2], ref intersectPoint1, ref intersectUV1, UVs[triangleIndex1], UVs[triangleIndex2], vertices[triangleIndex1], vertices[triangleIndex2], slicePlane);
                side2 = IntersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex2], ref intersectPoint2, ref intersectUV2, UVs[triangleIndex0], UVs[triangleIndex2], vertices[triangleIndex0], vertices[triangleIndex2], slicePlane);
            }

            if (!side0 && !side1 && !side2) // 0 intersections
            {
                if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], slicePlane))
                {
                    AddNewIndices(selected0Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                    if (isExperiment && item.gameObject.tag != "highlightmesh")
                    {
                        UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                        UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                    }
                }
                else
                {
                    AddNewIndices(selected1Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                    if (isExperiment && item.gameObject.tag != "highlightmesh")
                    {
                        UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                        UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                    }
                }
            }
            //else if (side0 && side1 && side2)
            //{
            //    Debug.Log("             All three sides!!");
            //}
            //else if (PlaneCollision.ApproximatelyEquals(intersectPoint0, transformedVertices[triangleIndex0]) || PlaneCollision.ApproximatelyEquals(intersectPoint1, transformedVertices[triangleIndex1]) || PlaneCollision.ApproximatelyEquals(intersectPoint2, transformedVertices[triangleIndex2]))
            //{
            //    Debug.Log("             YOU HIT A VERTEX");
            //}
            else
            {  // intersections have occurred
               // determine which side of triangle has 1 vertex
               // add vertex and indices to appropriate mesh
               // for side with 2, add vertices, add 2 triangles
                if (side0 && side1) // 2 intersections
                {

                    if (PlaneCollision.ApproximatelyEquals(intersectPoint0, intersectPoint1))
                    {
                        // plane intersects a triangle vertex
                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], slicePlane))
                        {
                            AddNewIndices(selected0Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                            }
                        }
                        else
                        {
                            AddNewIndices(selected1Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                            }
                        }
                    }
                    else
                    {
                        intersectIndex0 = numVertices++;
                        intersectIndex1 = numVertices++;

                        vertices.Add(intersectPoint0);
                        vertices.Add(intersectPoint1);


                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint0));
                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint1));
                        UVs.Add(intersectUV0);
                        UVs.Add(intersectUV1);


                        unsortedOutlinePoints.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint0), unsortedOutlinePoints.Count, unsortedOutlinePoints.Count + 1));
                        unsortedOutlinePoints.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint1), unsortedOutlinePoints.Count, unsortedOutlinePoints.Count - 1));

                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex1], slicePlane))
                        {

                            // Add the indices for various triangles to selected and unselected

                            AddNewIndices(selected0Indices, intersectIndex1, intersectIndex0, triangleIndex1);
                            AddNewIndices(selected1Indices, triangleIndex0, intersectIndex0, intersectIndex1);
                            AddNewIndices(selected1Indices, triangleIndex2, triangleIndex0, intersectIndex1);

                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, intersectIndex1, intersectIndex0, triangleIndex1, true);
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, intersectIndex0, intersectIndex1, false);
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex2, triangleIndex0, intersectIndex1, false);

                                UpdateTriangleState(triangleStates1, currentTriangleState, intersectIndex1, intersectIndex0, triangleIndex1, false);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, intersectIndex0, intersectIndex1, true);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex2, triangleIndex0, intersectIndex1, true);
                            }

                        }
                        else
                        {
                            AddNewIndices(selected1Indices, intersectIndex1, intersectIndex0, triangleIndex1);
                            AddNewIndices(selected0Indices, triangleIndex0, intersectIndex0, intersectIndex1);
                            AddNewIndices(selected0Indices, triangleIndex2, triangleIndex0, intersectIndex1);

                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, intersectIndex1, intersectIndex0, triangleIndex1, false);
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, intersectIndex0, intersectIndex1, true);
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex2, triangleIndex0, intersectIndex1, true);

                                UpdateTriangleState(triangleStates1, currentTriangleState, intersectIndex1, intersectIndex0, triangleIndex1, true);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, intersectIndex0, intersectIndex1, false);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex2, triangleIndex0, intersectIndex1, false);
                            }
                        }
                    }
                }
                else if (side0 && side2)
                {
                    if (PlaneCollision.ApproximatelyEquals(intersectPoint0, intersectPoint2))
                    {
                        // plane intersects a triangle vertex
                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex1], slicePlane))
                        {
                            AddNewIndices(selected0Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                            }
                        }
                        else
                        {
                            AddNewIndices(selected1Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                            }
                        }
                    }
                    else
                    {

                        intersectIndex0 = numVertices++;
                        intersectIndex2 = numVertices++;

                        vertices.Add(intersectPoint0);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                        vertices.Add(intersectPoint2);

                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint0));
                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint2));
                        UVs.Add(intersectUV0);
                        UVs.Add(intersectUV2);

                        unsortedOutlinePoints.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint0), unsortedOutlinePoints.Count, unsortedOutlinePoints.Count + 1));
                        unsortedOutlinePoints.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint2), unsortedOutlinePoints.Count, unsortedOutlinePoints.Count - 1));

                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], slicePlane))
                        {
                            AddNewIndices(selected0Indices, intersectIndex2, triangleIndex0, intersectIndex0);
                            AddNewIndices(selected1Indices, triangleIndex2, intersectIndex2, intersectIndex0);
                            AddNewIndices(selected1Indices, triangleIndex1, triangleIndex2, intersectIndex0);

                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, intersectIndex2, triangleIndex0, intersectIndex0, true);
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex2, intersectIndex2, intersectIndex0, false);
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex1, triangleIndex2, intersectIndex0, false);

                                UpdateTriangleState(triangleStates1, currentTriangleState, intersectIndex2, triangleIndex0, intersectIndex0, false);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex2, intersectIndex2, intersectIndex0, true);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex1, triangleIndex2, intersectIndex0, true);
                            }
                        }
                        else
                        {
                            AddNewIndices(selected1Indices, intersectIndex2, triangleIndex0, intersectIndex0);
                            AddNewIndices(selected0Indices, triangleIndex2, intersectIndex2, intersectIndex0);
                            AddNewIndices(selected0Indices, triangleIndex1, triangleIndex2, intersectIndex0);

                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, intersectIndex2, triangleIndex0, intersectIndex0, false);
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex2, intersectIndex2, intersectIndex0, true);
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex1, triangleIndex2, intersectIndex0, true);

                                UpdateTriangleState(triangleStates1, currentTriangleState, intersectIndex2, triangleIndex0, intersectIndex0, true);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex2, intersectIndex2, intersectIndex0, false);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex1, triangleIndex2, intersectIndex0, false);
                            }
                        }
                    }
                }
                else if (side1 && side2)
                {

                    if (PlaneCollision.ApproximatelyEquals(intersectPoint1, intersectPoint2))
                    {
                        // plane intersects a triangle vertex
                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex1], slicePlane))
                        {
                            AddNewIndices(selected0Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                            }
                        }
                        else
                        {
                            AddNewIndices(selected1Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                            }
                        }
                    }
                    else
                    {
                        intersectIndex1 = numVertices++;
                        intersectIndex2 = numVertices++;

                        vertices.Add(intersectPoint1);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                        vertices.Add(intersectPoint2);

                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint1));
                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint2));
                        UVs.Add(intersectUV1);
                        UVs.Add(intersectUV2);

                        unsortedOutlinePoints.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint1), unsortedOutlinePoints.Count, unsortedOutlinePoints.Count + 1));
                        unsortedOutlinePoints.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint2), unsortedOutlinePoints.Count, unsortedOutlinePoints.Count - 1));

                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex2], slicePlane))
                        {
                            AddNewIndices(selected0Indices, intersectIndex1, triangleIndex2, intersectIndex2);
                            AddNewIndices(selected1Indices, intersectIndex2, triangleIndex0, intersectIndex1);
                            AddNewIndices(selected1Indices, triangleIndex0, triangleIndex1, intersectIndex1);

                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, intersectIndex1, triangleIndex2, intersectIndex2, true);
                                UpdateTriangleState(triangleStates0, currentTriangleState, intersectIndex2, triangleIndex0, intersectIndex1, false);
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, intersectIndex1, false);

                                UpdateTriangleState(triangleStates1, currentTriangleState, intersectIndex1, triangleIndex2, intersectIndex2, false);
                                UpdateTriangleState(triangleStates1, currentTriangleState, intersectIndex2, triangleIndex0, intersectIndex1, true);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, intersectIndex1, true);
                            }
                        }
                        else
                        {
                            AddNewIndices(selected1Indices, intersectIndex1, triangleIndex2, intersectIndex2);
                            AddNewIndices(selected0Indices, intersectIndex2, triangleIndex0, intersectIndex1);
                            AddNewIndices(selected0Indices, triangleIndex0, triangleIndex1, intersectIndex1);

                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, intersectIndex1, triangleIndex2, intersectIndex2, false);
                                UpdateTriangleState(triangleStates0, currentTriangleState, intersectIndex2, triangleIndex0, intersectIndex1, true);
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, intersectIndex1, true);

                                UpdateTriangleState(triangleStates1, currentTriangleState, intersectIndex1, triangleIndex2, intersectIndex2, true);
                                UpdateTriangleState(triangleStates1, currentTriangleState, intersectIndex2, triangleIndex0, intersectIndex1, false);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, intersectIndex1, false);
                            }
                        }
                    }
                }
                else
                {
                    // We really shouldn't get here, but do seem to occasionally
                    if (side0)
                    {
                        // plane intersects a triangle vertex
                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex2], slicePlane))
                        {
                            AddNewIndices(selected0Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                            }
                        }
                        else
                        {
                            AddNewIndices(selected1Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                            }
                        }
                    }
                    else if (side1)
                    {
                        // plane intersects a triangle vertex
                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], slicePlane))
                        {
                            AddNewIndices(selected0Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                            }
                        }
                        else
                        {
                            AddNewIndices(selected1Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                            }
                        }
                    }
                    else if (side2)
                    {
                        // plane intersects a triangle vertex
                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex1], slicePlane))
                        {
                            AddNewIndices(selected0Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                            }
                        }
                        else
                        {
                            AddNewIndices(selected1Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                            if (isExperiment && item.gameObject.tag != "highlightmesh")
                            {
                                UpdateTriangleState(triangleStates0, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                                UpdateTriangleState(triangleStates1, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                            }
                        }
                    }
                }
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, UVs);

        sliced0Indices[item.name] = selected0Indices.ToArray();
        sliced1Indices[item.name] = selected1Indices.ToArray();

        if (item.gameObject.tag != "highlightmesh" && !isExperiment)
        {
            OutlineManager.ResizePreselectedPoints(item, unsortedOutlinePoints, 0, slicePlane);
           // sliceOutlines[item.name].GetComponent<MeshFilter>().sharedMesh = new Mesh();
          //  OutlineManager.CreateOutlineMesh(outlinePoints, slicePlane.transform.up, sliceOutlines[item.name]);
        }

        unsortedOutlinePoints.Clear();

    }

    private void UpdateTriangleState(Dictionary<Triangle, SelectionData.TriangleSelectionState> dictionary, SelectionData.TriangleSelectionState currentState, int index0, int index1, int index2, bool isSelectedNow)
    {
        SelectionData.TriangleSelectionState newState = SelectionData.TriangleSelectionState.UnselectedOrigUnselectedNow;
        if (currentState == SelectionData.TriangleSelectionState.SelectedOrigSelectedNow && isSelectedNow)
        {
            newState = SelectionData.TriangleSelectionState.SelectedOrigSelectedNow;
        }
        else if (currentState == SelectionData.TriangleSelectionState.SelectedOrigSelectedNow && !isSelectedNow)
        {
            newState = SelectionData.TriangleSelectionState.SelectedOrigUnselectedNow;
        }
        else if (currentState == SelectionData.TriangleSelectionState.SelectedOrigUnselectedNow && isSelectedNow)
        {
            newState = SelectionData.TriangleSelectionState.SelectedOrigSelectedNow;
        }
        else if (currentState == SelectionData.TriangleSelectionState.SelectedOrigUnselectedNow && !isSelectedNow)
        {
            newState = SelectionData.TriangleSelectionState.SelectedOrigUnselectedNow;
        }
        else if (currentState == SelectionData.TriangleSelectionState.UnselectedOrigSelectedNow && isSelectedNow)
        {
            newState = SelectionData.TriangleSelectionState.UnselectedOrigSelectedNow;
        }
        else if (currentState == SelectionData.TriangleSelectionState.UnselectedOrigSelectedNow && !isSelectedNow)
        {
            newState = SelectionData.TriangleSelectionState.UnselectedOrigUnselectedNow;
        }
        else if (currentState == SelectionData.TriangleSelectionState.UnselectedOrigUnselectedNow && isSelectedNow)
        {
            newState = SelectionData.TriangleSelectionState.UnselectedOrigSelectedNow;
        }
        else if (currentState == SelectionData.TriangleSelectionState.UnselectedOrigUnselectedNow && !isSelectedNow)
        {
            newState = SelectionData.TriangleSelectionState.UnselectedOrigUnselectedNow;
        }


        Triangle tri = new Triangle(index0, index1, index2);
        if (dictionary.ContainsKey(tri))
        {
           dictionary[tri] = newState;
        }
        else
        {
            dictionary.Add(tri, newState);
        }
    }

    private bool BoundingCircleIntersectsWithPlane(GameObject plane, Vector3 a, Vector3 b, Vector3 c)
    {
        float dotABAB = Vector3.Dot(b - a, b - a);
        float dotABAC = Vector3.Dot(b - a, c - a);
        float dotACAC = Vector3.Dot(c - a, c - a);
        float d = 2.0f * (dotABAB * dotACAC - dotABAC * dotABAC);
        Vector3 referencePt = a;
        Vector3 center = new Vector3(0, 0, 0);

        float s = (dotABAB * dotACAC - dotACAC * dotABAC) / d;
        float t = (dotACAC * dotABAB - dotABAB * dotABAC) / d;
        // s controls height over AC, t over AB, (1-s-t) over BC
        if (s <= 0.0f)
        {
            center = 0.5f * (a + c);
        }
        else if (t <= 0.0f)
        {
            center = 0.5f * (a + b);
        }
        else if (s + t >= 1.0f)
        {
            center = 0.5f * (b + c);
            referencePt = b;
        }
        else center = a + s * (b - a) + t * (c - a);

        float sqrRadius = Vector3.Dot(center - referencePt, center - referencePt);

        Vector3 closestPointInPlaneToCenter = center + (plane.transform.up * (-(Vector3.Dot(plane.transform.up, center) - Vector3.Dot(plane.transform.up, plane.transform.position))));

        return (center - closestPointInPlaneToCenter).sqrMagnitude < sqrRadius;

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
                if (sliced0Indices.ContainsKey(item.name))
                {
                    mesh.subMeshCount = 3;
                    //materials = new Material[3];
                    mesh.SetTriangles(sliced0Indices[item.name], 0);
                    mesh.SetTriangles(sliced1Indices[item.name], 1);
                    mesh.SetTriangles(SelectionData.PreviousUnselectedIndices[item.name], 2);

                    // materials[2] = item.GetComponent<Renderer>().materials[0];
                    materials[0] = Resources.Load("Blue Material") as Material;
                    materials[1] = Resources.Load("Green Material") as Material;

                    //materials[0] = DetermineBaseMaterial(materials[0]);
                    //materials[1] = DetermineBaseMaterial(materials[1]);                     //added to make things transparent in experiment!

                    Material baseMaterial = originalMaterial[item.name];
                    //if (lastSeenObj != null && lastSeenObj.name == item.name)
                    //{
                    if (!isExperiment)
                    {
                        materials[2] = GazeSelectedMaterial(baseMaterial);
                    } 
                    else
                    {
                        materials[2] = baseMaterial;
                    }
                    //Debug.Log("after a slice has been made, should still be purple");
                    //}
                    //else
                    //{
                    //    materials[2] = DetermineBaseMaterial(baseMaterial);         // Sets unselected as transparent
                    //    Debug.Log("after a slice has been made, turns transparent material");
                    //}
                } else
                {
                    // This case handles backing out of a slice. In that case it is indicated by clearing selection0Indices for the object
                    if (SelectionData.ObjectsWithSelections.Contains(item.name))
                    {
                        mesh.subMeshCount = 2;
                        materials = new Material[2];
                       // Debug.Log("Remove slice with selections");
                        mesh.SetTriangles(SelectionData.PreviousSelectedIndices[item.name], 1);
                        mesh.SetTriangles(SelectionData.PreviousUnselectedIndices[item.name], 0);

                        Material baseMaterial = originalMaterial[item.name];
                        if (!isExperiment && lastSeenObj != null && lastSeenObj.name == item.name)
                        {
                            materials[0] = GazeSelectedMaterial(baseMaterial);
                            //Debug.Log("removed a slice, should be purple");
                        }
                        else
                        {
                            materials[0] = baseMaterial;    //DetermineBaseMaterial(baseMaterial);         // Sets unselected as transparent
                            //Debug.Log("removed a slice, should be transparent");
                        }
                        materials[1] = Resources.Load("Selected Transparent") as Material;      // May need to specify which submesh we get this from? -> THIS SETS SELECTION AS ORANGE STUFF
                        //Debug.Log("num submeshes: " + mesh.subMeshCount.ToString() + " mats len: " + materials.Length.ToString() + " first mat: " + materials[0].name + ", " + materials[1].name);
                    }
                    else
                    {
                        //Debug.Log("Remove first slice");
                        mesh.subMeshCount = 1;
                        materials = new Material[1];
                        mesh.SetTriangles(SelectionData.PreviousSelectedIndices[item.name], 0);

                        Material baseMaterial = originalMaterial[item.name];
                        if(!isExperiment &&  lastSeenObj != null && lastSeenObj.name == item.name)
                        {
                            materials[0] = GazeSelectedMaterial(baseMaterial);
                            //Debug.Log("removed first slice, should be purple");
                        }
                        else
                        {
                            materials[0] = baseMaterial;         // Sets unselected as transparent
                            //Debug.Log("removed first slice, should be transparent");
                        }
                        //Debug.Log("materials assigned length " + materials.Length);
                    }
                }
            }
            else if (mode == "swipe")
            {
                mesh.subMeshCount = 2;
                materials = new Material[2];
                //Debug.Log(item.name + " s: " + SelectionData.PreviousSelectedIndices[item.name].Count().ToString() + " u: " + SelectionData.PreviousUnselectedIndices[item.name].Count().ToString());
                mesh.SetTriangles(SelectionData.PreviousSelectedIndices[item.name], 1);
                mesh.SetTriangles(SelectionData.PreviousUnselectedIndices[item.name], 0);

                Material baseMaterial = originalMaterial[item.name];
               

                if (!isExperiment && lastSeenObj != null && item.name == lastSeenObj.name)
                {
                    baseMaterial = GazeSelectedMaterial(baseMaterial);
                    //Debug.Log("have swiped, should be purple");
                }
                //else
                //{
                //    baseMaterial = DetermineBaseMaterial(baseMaterial);         // Sets unselected as transparent                                     Don't change baseMaterial during experiments -> already transparent!!
                //    //Debug.Log("have swiped, transparent");
                //}
                
                materials[0] = baseMaterial;
                materials[1] = Resources.Load("Selected Transparent") as Material;      // May need to specify which submesh we get this from? -> THIS SETS SELECTION AS ORANGE STUFF
               // materials[1] = DetermineBaseMaterial(materials[1]);                             //added for experiment transparency
            }

           // item.GetComponent<Renderer>().material; --can we clear this out first? is that the problem??

            //if (mesh.subMeshCount > 1)                see if this works in experiment w resized arrays. esp for removing first slice.
            //{
                //Debug.Log("materials used length " + materials.Length);
                item.GetComponent<Renderer>().materials = materials;
            //}
            //else
            //{
            //    Debug.Log("materials used length " + materials.Length + " " + materials[0].name);
            //    item.GetComponent<Renderer>().material = materials[0];
            //}
            //Debug.Log(item.name + " M0: " + materials[0].name + " M1: " + materials[1].name);
        }
        else if (item.gameObject.tag == "highlightmesh" ) //&& mode == "slice")
        {
            //if (item.name == "highlight0")
            //{
            //    //Debug.Log(" At Color " + SelectionData.PreviousSelectedIndices[item.name].Count().ToString() + " selected Indices");
            //}

            mesh.subMeshCount = 1;
            mesh.SetTriangles(SelectionData.PreviousSelectedIndices[item.name], 0);
            //Debug.Log("                  coloring: " + item.name);

        }
        mesh.RecalculateNormals();
    }

    ///**
    // * points contains a list of points where each successive pair of points gets a tube drawn between them, sets to mesh called selectorMesh
    // * */
    //private Mesh CreateOutlineMesh(List<Vector3> points, GameObject plane, Mesh outlineMesh)
    //{
    //    List<Vector3> verts = new List<Vector3>();
    //    List<int> faces = new List<int>();
    //    List<Vector2> uvCoordinates = new List<Vector2>();
    //    outlineMesh.Clear();

    //    float radius = .005f;
    //    int numSections = 6;

    //    Assert.IsTrue(points.Count % 2 == 0);
    //    int expectedNumVerts = (numSections + 1) * points.Count;

    //    //if (expectedNumVerts > 65000)
    //    //{
    //    points = OrderMesh(points);
    //    //    Debug.Log("Outline mesh was ordered: " + outlineMesh.name);
    //    //}

    //   // points.Add(points.ElementAt(0)); //Add the first point again at the end to make a loop.

    //    if (points.Count >= 2) {

    //        List<Vector3> duplicatedPoints = new List<Vector3>();
    //        duplicatedPoints.Add(points[0]);
    //        for (int i = 1; i < points.Count; i++)
    //        {
    //            duplicatedPoints.Add(points[i]);
    //            duplicatedPoints.Add(points[i]);
    //        }

    //        for (int i = 0; i < duplicatedPoints.Count-1; i += 2)
    //        {
    //            Vector3 centerStart = duplicatedPoints[i];
    //            Vector3 centerEnd = duplicatedPoints[i + 1];
    //            Vector3 direction = centerEnd - centerStart;
    //            direction = direction.normalized;
    //            Vector3 right = Vector3.Cross(plane.transform.up, direction);
    //            Vector3 up = Vector3.Cross(direction, right);
    //            up = up.normalized * radius;
    //            right = right.normalized * radius;

    //            for (int slice = 0; slice <= numSections; slice++)
    //            {
    //                float theta = (float)slice / (float)numSections * 2.0f * Mathf.PI;
    //                Vector3 p0 = centerStart + right * Mathf.Sin(theta) + up * Mathf.Cos(theta);
    //                Vector3 p1 = centerEnd + right * Mathf.Sin(theta) + up * Mathf.Cos(theta);

    //                verts.Add(p0);
    //                verts.Add(p1);
    //                uvCoordinates.Add(new Vector2((float)slice / (float)numSections, 0));
    //                uvCoordinates.Add(new Vector2((float)slice / (float)numSections, 1));

    //                if (slice > 0)
    //                {
    //                    faces.Add((slice * 2 + 1) + ((numSections + 1) * i));
    //                    faces.Add((slice * 2) + ((numSections + 1) * i));
    //                    faces.Add((slice * 2 - 2) + ((numSections + 1) * i));

    //                    faces.Add(slice * 2 + 1 + ((numSections + 1) * i));
    //                    faces.Add(slice * 2 - 2 + ((numSections + 1) * i));
    //                    faces.Add(slice * 2 - 1 + ((numSections + 1) * i));
    //                }
    //            }
    //        }

    //        outlineMesh.SetVertices(verts);
    //        outlineMesh.SetUVs(0, uvCoordinates);
    //        outlineMesh.SetTriangles(faces, 0);

    //        outlineMesh.RecalculateBounds();
    //        outlineMesh.RecalculateNormals();
    //    }

    //    return outlineMesh;
    //}

    ///**
    // * Make a graph of mesh vertices, order it, remove sequential duplicates and return new set of vertices
    // */
    //private List<Vector3> OrderMesh(List<Vector3> meshVertices)
    //{
    //    Dictionary<Vector3, HashSet<Vector3>> vertexGraph = new Dictionary<Vector3, HashSet<Vector3>>();  // Each point should only be connected to two other points

    //    for (int i = 0; i < meshVertices.Count; i += 2)
    //    {
    //        AddToGraph(meshVertices[i], meshVertices[i + 1], ref vertexGraph);
    //    }

    //    meshVertices = DFSOrderPoints(vertexGraph);

    //    meshVertices.Add(meshVertices[0]);
    //    meshVertices = RemoveSequentialDuplicates(meshVertices);

    //    return meshVertices;
    //}

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

        float epsilon = 0.00001f;
        if (Mathf.Abs(dot) > epsilon)
        {
            float factor = Vector3.Dot(plane.transform.up, w) / dot;
            if ((factor > 0f && factor < 1f) || Math.Abs(factor) <= epsilon || Math.Abs(factor - 1f) <= epsilon)
            {
                lineSegmentLocal = factor * lineSegmentLocal;
                intersectPoint = lineVertexLocal0 + lineSegmentLocal;
                intersectUV = vertex0UV + factor * (vertex1UV - vertex0UV);

                return true;
            }
        }
        return false;
    }

    //// Orders the points of one mesh. NOTE: currently just uses alreadyVisited HashSet, nothing else;
    //List<Vector3> DFSOrderPoints(Dictionary<Vector3, HashSet<Vector3>> pointConnections)
    //{
    //    HashSet<Vector3> alreadyVisited = new HashSet<Vector3>();
    //    List<Vector3> orderedPoints = new List<Vector3>();

    //    foreach (Vector3 pt in pointConnections.Keys)
    //    {
    //        if (!alreadyVisited.Contains(pt))
    //        {
    //            //TODO: make a new list for ordered points here to pass in
    //            DFSVisit(pt, pointConnections, ref alreadyVisited, ref orderedPoints);
    //        }
    //    }
    //    return orderedPoints;
    //}

    //// Basic DFS, adds the intersection points of edges in the order it visits them
    //void DFSVisit(Vector3 pt, Dictionary<Vector3, HashSet<Vector3>> connectedEdges, ref HashSet<Vector3> alreadyVisited, ref List<Vector3> orderedPoints)
    //{
    //    alreadyVisited.Add(pt);
    //    orderedPoints.Add(pt);
        
    //    foreach (Vector3 otherIndex in connectedEdges[pt])
    //    {
    //        if (!alreadyVisited.Contains(otherIndex))
    //        {               
    //            DFSVisit(otherIndex, connectedEdges, ref alreadyVisited, ref orderedPoints);
    //        }
    //    }
        
    //}

    //// Takes two connected points and adds or updates entries in the list of actual points and the graph of their connections
    //private void AddToGraph(Vector3 point0, Vector3 point1, ref Dictionary<Vector3, HashSet<Vector3>> pointConnections)
    //{
    //    if (!pointConnections.ContainsKey(point0))
    //    {
    //        HashSet<Vector3> connections = new HashSet<Vector3>();
    //        connections.Add(point1);
    //        pointConnections.Add(point0, connections);
    //    }
    //    else
    //    {
    //        pointConnections[point0].Add(point1);
    //    }

    //    if (!pointConnections.ContainsKey(point1))
    //    {
    //        HashSet<Vector3> connections = new HashSet<Vector3>();
    //        connections.Add(point0);
    //        pointConnections.Add(point1, connections);
    //    }
    //    else
    //    {
    //        pointConnections[point1].Add(point0);
    //    }
    //}

    //private bool ContainsV3Key(Dictionary<Vector3, HashSet<Vector3>> dict, Vector3 key)
    //{
    //    List<Vector3> keys = dict.Keys.ToList();


    //}

    //private List<Vector3> RemoveSequentialDuplicates(List<Vector3> points)
    //{
    //    List<Vector3> output = new List<Vector3>(points.Count);
    //    int i = 0;
    //    output.Add(points[i]);
    //    while (i < points.Count - 1)
    //    {
    //        int j = i+1;
    //        while(j < points.Count && PlaneCollision.ApproximatelyEquals(points[i], points[j])){
    //            j++;
    //        }
    //        if (j < points.Count)
    //        {
    //            output.Add(points[j]);
    //        }
    //        i = j;
    //    }
    //        /*
    //    {
    //        bool firstTwoEqual = PlaneCollision.ApproximatelyEquals(points[i-1], points[i]);
    //        bool secondTwoEqual = PlaneCollision.ApproximatelyEquals(points[i], points[i + 1]);
            
    //        if (firstTwoEqual && secondTwoEqual)
    //        {
    //            output.Add(points[i - 1]);
    //            output.Add(points[i + 2]);
    //            i += 3;  
    //        }
    //        else if ((firstTwoEqual && !secondTwoEqual) || (!firstTwoEqual && secondTwoEqual))  // If only two are the same
    //        {
    //            output.Add(points[i-1]);      // Add one of the equal points
    //            output.Add(points[i + 1]);
    //            i += 3;
    //        }
            
    //        else  // All are distinct
    //        {
    //            output.Add(points[i - 1]);      // Add first two
    //            output.Add(points[i]);  
    //            i += 2;
    //        }
    //    }
    //    */
    //    return output;
    //}
    
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
            transparentBase.name = "TransparentUnselected " + baseMaterial.name;        //added a space and material name for experiment
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
        if (baseMaterial.name == "TransparentSighted")
        {
            return baseMaterial;
        }
        else
        {
            Material transparentBase = new Material(baseMaterial);
            transparentBase.name = "TransparentSighted";
            transparentBase.color = new Color(1.0f, 0f, 1.0f, 0.2f);        //alpha val was 0.5f. changed to be more transparent for the experiment
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

    ///// <summary>
    ///// Make a Gameobject that will follow the user's hands
    ///// </summary>
    ///// <param name="item"></param>
    ///// <returns></returns>
    //private GameObject MakeHandOutline(GameObject item)
    //{
    //    string meshName = item.name;
    //    GameObject newOutline = new GameObject();
    //    newOutline.name = meshName + " highlight";
    //    newOutline.AddComponent<MeshRenderer>();
    //    newOutline.AddComponent<MeshFilter>();
    //    newOutline.tag = "highlightmesh";
    //    newOutline.GetComponent<MeshFilter>().mesh = new Mesh();
    //    newOutline.GetComponent<MeshFilter>().mesh.MarkDynamic();
    //    newOutline.GetComponent<Renderer>().material = Resources.Load("TestMaterial") as Material;
    //    newOutline.layer = LayerMask.NameToLayer("Ignore Raycast");

    //    newOutline.transform.position = item.transform.position;
    //    newOutline.transform.localScale = item.transform.localScale;
    //    newOutline.transform.rotation = item.transform.rotation;

    //    return newOutline;
    //}
}