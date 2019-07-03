using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Collections;

public class HandSelectionState : InteractionState
{
    private const bool debug = false;
    private bool notExperiment;
    private int selectionCount;
    //private bool canTransition = true;

    InteractionState stateToReturnTo;
    private ControllerInfo controller0;
    private ControllerInfo controller1;

    private GameObject leftPlane;   //
    private GameObject rightPlane;  // Used to detect collision with meshes in the model
    private GameObject centerCube;  //

    //private int planeLayer;                         // Layer that cube and planes are on

    private List<GameObject> collidingMeshes;       // List of meshes currently being collided with
    private HashSet<GameObject> cubeColliders;      // All the objects the cube is colliding with
    CubeCollision centerComponent;                  // Script on cube that we get the list of colliders from

    SelectionData selectionData;
    UndoManager undoManager;

    public Dictionary<Triangle, SelectionData.TriangleSelectionState> triangleStatesBeforeSelection;

    private Dictionary<string, Vector3[]> currentTriangleBoundsCenters;
    private Dictionary<string, float[]> currentTriangleBoundsSqrRadius;

    //private static Dictionary<string, Vector3[]> previousVertices;              // Key = name of obj with mesh, Value = all vertices of the mesh at the time of last click
    //private static Dictionary<string, Vector2[]> previousUVs;                   // Key = name of obj with mesh, Value = all UVs of the mesh at the time of last click
    //private Dictionary<string, int[]> previousUnselectedIndices;                // Key = name of object with mesh, Value = all indices that have not been selected (updated when user clicks)
    //private static Dictionary<string, int> previousNumVertices;                 // Key = name of object with mesh, Value = original set of vertices (updated when user clicks and mesh is split)
    //private static Dictionary<string, int[]> previousSelectedIndices;           // key = name of object with mesh, Value = original set of selected indices (updated when user clicks)
    //private static HashSet<string> objWithSelections;                           // Collection of the the names of all the meshes that have had pieces selected from them.
    //private static Dictionary<string, HashSet<GameObject>> savedOutlines;       // Key = name of object in model, Value = all the SAVED outline game objects attached to it
    private static Dictionary<string, List<GameObject>> preSelectionOutlines;                 // left hand outlines per model that are currently being manipulated (KEY = name of model object, VALUE = outline object)
    //private static Dictionary<string, List<GameObject>> rightOutlines;                // right hand outlines per model that are currently being manipulated (KEY = name of model object, VALUE = outline object)

    private List<OutlinePoint> unsortedOutlinePts;    // Pairs of two connected points to be used in drawing an outline mesh

    List<int> selectedIndices;      // Reused for each mesh during ProcessMesh()
    List<int> unselectedIndices;    // ^^^^

    //Mesh leftOutlineMesh;       // Reused to draw highlights that move with left hand
    //Mesh rightOutlineMesh;      // Reused to draw highlights that move with right hand

    public static Dictionary<string, int[]> PreviousSelectedIndices
    {
        get { return SelectionData.PreviousSelectedIndices; }
    }

    public static Dictionary<string, Vector3[]> PreviousVertices
    {
        get { return SelectionData.PreviousVertices; }
    }

    public static Dictionary<string, Vector2[]> PreviousUVs
    {
        get { return SelectionData.PreviousUVs; }
    }

    public static Dictionary<string, int> PreviousNumVertices
    {
        get { return SelectionData.PreviousNumVertices; }
    }
    public static HashSet<string> ObjectsWithSelections
    {
        get { return SelectionData.ObjectsWithSelections; }
    }
    public static Dictionary<string, HashSet<GameObject>> SavedOutlines
    {
        get { return SelectionData.SavedOutlines; }
        set { SelectionData.SavedOutlines = value; }
    }


    //public static Dictionary<string, List<GameObject>> PreSelectionOutlines
    //{
    //    get { return preSelectionOutlines; }
    //}

    //public static Dictionary<string, GameObject> LeftOutlines
    //{
    //    get { return leftOutlines; }
    //}
    //public static Dictionary<string, GameObject> RightOutlines
    //{
    //    get { return rightOutlines; }
    //}

    /// <summary>
    /// State that activates whenever there's a mesh between the user's controllers. Allows user to select surfaces and progressively refine their selection.
    /// Currently only works when selecting a single object.
    /// </summary>
    /// <param name="controller0Info"></param>
    /// <param name="controller1Info"></param>
    /// <param name="stateToReturnTo"></param>
    public HandSelectionState(ControllerInfo controller0Info, ControllerInfo controller1Info, InteractionState stateToReturnTo, SelectionData sharedData, UndoManager undoMgr, bool experiment)
    {
        // NOTE: Selecting more than one mesh will result in highlights appearing in the wrong place
        desc = "HandSelectionState";
        controller0 = controller0Info;
        controller1 = controller1Info;

        //planeLayer = LayerMask.NameToLayer("PlaneLayer");

        leftPlane = CreateHandPlane(controller0, "handSelectionLeftPlane");
        rightPlane = CreateHandPlane(controller1, "handSelectionRightPlane");
        

        //The center cube is anchored between controllers and detects collisions with other objects
        centerCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        //UnityEngine.Object.DontDestroyOnLoad(centerCube);
        centerCube.name = "handSelectionCenterCube";
        centerCube.GetComponent<Renderer>().material = Resources.Load("Cube Material") as Material;
        centerCube.AddComponent<MeshCollider>();
        centerCube.GetComponent<MeshCollider>().convex = true;
        centerCube.GetComponent<MeshCollider>().isTrigger = true;
        centerCube.AddComponent<Rigidbody>();
        centerCube.GetComponent<Rigidbody>().isKinematic = true;
        centerComponent = centerCube.AddComponent<CubeCollision>();
        //centerCube.layer = planeLayer;
        centerCube.layer = LayerMask.NameToLayer("Ignore Raycast");

        if (!debug)
        {
            centerCube.GetComponent<MeshRenderer>().enabled = false;
        }

        collidingMeshes = new List<GameObject>();
        cubeColliders = new HashSet<GameObject>();

        //TODO: should these persist between states? Yes so only make one instance of the state. Should use the Singleton pattern here//TODO

        selectionData = sharedData;
        undoManager = undoMgr;

        //objWithSelections = new HashSet<string>();
        //previousNumVertices = new Dictionary<string, int>();              // Keeps track of how many vertices a mesh should have
        //previousUnselectedIndices = new Dictionary<string, int[]>();      // Keeps track of indices that were previously unselected
        //previousSelectedIndices = new Dictionary<string, int[]>();
        //previousVertices = new Dictionary<string, Vector3[]>();
        //previousUVs = new Dictionary<string, Vector2[]>();
        //savedOutlines = new Dictionary<string, HashSet<GameObject>>();
        selectedIndices = new List<int>();
        unselectedIndices = new List<int>();
        unsortedOutlinePts = new List<OutlinePoint>();
        this.stateToReturnTo = stateToReturnTo;
        notExperiment = !experiment;
        selectionCount = 0;
        if (notExperiment)
        {
            preSelectionOutlines = OutlineManager.preSelectionOutlines;
        }
        //rightOutlines = new Dictionary<string, List<GameObject>>();

        //leftOutlineMesh = new Mesh();
        //rightOutlineMesh = new Mesh();
    }

    public override bool CanTransition()
    {
        bool allowed;
        if(collidingMeshes.Count > 0)
        {
            allowed = false;
        } else
        {
            allowed = true;
        }
        return allowed;
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
        handPlane.layer = LayerMask.NameToLayer("Ignore Raycast");
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

        //Vector3 groundY = new Vector3(0, 1);

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
            indices = SelectionData.PreviousSelectedIndices[collidingObj.name]; // the indices of last selection
            if (notExperiment)
            {
                preSelectionOutlines = OutlineManager.preSelectionOutlines;
            }

            if (SelectionData.ObjectsWithSelections.Contains(collidingObj.name))    // If it previously had a piece selected (CLICKED) - revert to that selection
            {
                // Generate a mesh to fill the entire selected part of the collider
                //Vector3[] verts = mesh.vertices;
                Vector3[] verts = SelectionData.PreviousVertices[collidingObj.name];

                //Debug.Log( verts.Count().ToString() + ", " + previousNumVertices[collidingObj.name].ToString()  + "   deactivate");

                List<Vector2> uvs = new List<Vector2>();
                uvs = SelectionData.PreviousUVs[collidingObj.name].ToList();
                //mesh.GetUVs(0, uvs);

                mesh.Clear();
                mesh.vertices = verts;
                mesh.SetUVs(0, uvs);

                if (collidingObj.tag != "highlightmesh") // set unselected and selected regions back to what they were at the last click
                {
                    mesh.subMeshCount = 2;
                    mesh.SetTriangles(SelectionData.PreviousUnselectedIndices[collidingObj.name], 0);
                    mesh.SetTriangles(indices, 1);
                }
                else // for meshes that are outlines, use only one material (unselected will not be drawn)
                {
                    mesh.subMeshCount = 1;
                    mesh.SetTriangles(indices, 0);
                }

                mesh.RecalculateBounds();
                mesh.RecalculateNormals();

                if (notExperiment)
                {
                    // Go through each outline associated with the current mesh object and reset it
                    foreach (GameObject outline in SelectionData.SavedOutlines[collidingObj.name])
                    {
                        //Debug.Log("Removing outlines for " + collidingObj.name);

                        Mesh outlineMesh = outline.GetComponent<MeshFilter>().mesh;
                        //Vector3[] outlineVerts = outlineMesh.vertices;
                        Vector3[] outlineVerts = SelectionData.PreviousVertices[outline.name];
                        List<Vector2> outlineUVs = new List<Vector2>();
                        outlineUVs = SelectionData.PreviousUVs[outline.name].ToList();
                        //outlineMesh.GetUVs(0, outlineUVs);

                        outlineMesh.Clear();
                        outlineMesh.vertices = outlineVerts;
                        outlineMesh.SetUVs(0, outlineUVs);

                        outlineMesh.subMeshCount = 1;
                        outlineMesh.SetTriangles(SelectionData.PreviousSelectedIndices[outline.name], 0);

                        outlineMesh.RecalculateBounds();
                        outlineMesh.RecalculateNormals();
                    }
                }
            }
            else // NOT CLICKED 
            {
                //Debug.Log("deactivating and not clicked " + collidingObj.name);

                // reset object to original state (before interaction)
                if (collidingObj.tag != "highlightmesh")
                {
                    Material baseMaterial = collidingObj.GetComponent<Renderer>().materials[0];
                    //baseMaterial.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);                          // this is making the material opaque in experiment. it should Not
                    collidingObj.GetComponent<Renderer>().materials[1] = baseMaterial;

                    if (notExperiment)
                    {
                        foreach (GameObject outlineMesh in preSelectionOutlines[collidingObj.name])
                        {
                            outlineMesh.GetComponent<MeshFilter>().mesh.Clear();
                        }
                        //leftOutlines[collidingObj.name].GetComponent<MeshFilter>().mesh.Clear();
                        //rightOutlines[collidingObj.name].GetComponent<MeshFilter>().mesh.Clear();
                    }
                }
            }

            if (notExperiment)
            {
                //stop rendering current outline whenever hands removed from collidingObj
                if (preSelectionOutlines.ContainsKey(collidingObj.name)) //|| rightOutlines.ContainsKey(collidingObj.name))
                {
                    for (int i = preSelectionOutlines[collidingObj.name].Count - 1; i >= 0; i--)
                    {
                        UnityEngine.Object.Destroy(preSelectionOutlines[collidingObj.name][i]);
                        preSelectionOutlines[collidingObj.name].RemoveAt(i);
                    }
                }
            }
        }
    }

    public override string HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        string eventString = "";

        List<Vector2> UVList = new List<Vector2>();

        if (controller0Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad) || controller1Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad))
        {
            Debug.Break();
        }

            UpdatePlanes();

        //Debug.Log("Hand Motion " + Vector3.Distance(lastPos,currentPos).ToString());

        // Take input from cube about what it collides with
        cubeColliders = centerComponent.CollidedObjects;

        if (cubeColliders.Count > 0)
        {
            collidingMeshes.Clear();
            collidingMeshes = cubeColliders.ToList();
        }
        else // If not colliding with anything, change states
        {
            controller0.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true); // enable rendering of controllers
            controller1.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true); //

            controller0.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;    // deactivate hand rendering
            controller1.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;    //

            if (notExperiment)
            {
                GameObject.Find("UIController").GetComponent<UIController>().ChangeState(stateToReturnTo);
                return "";
            }
            else
            {
                GameObject.Find("ExperimentController").GetComponent<RunExperiment>().ChangeState(stateToReturnTo);
                return "";
            }
        }

        if (controller0.device.GetPressDown(SteamVR_Controller.ButtonMask.Grip) || controller1.device.GetPressDown(SteamVR_Controller.ButtonMask.Grip))// || Input.GetButtonDown("ViveGrip"))
        {
            Debug.Log("undo button pressed");
            undoManager.Undo();

        }

        foreach (GameObject currObjMesh in collidingMeshes)
        {
            if (!SelectionData.PreviousNumVertices.ContainsKey(currObjMesh.name)) // if the original vertices are not stored already, store them (first time seeing object)
            {
                eventString = "first collision with " + currObjMesh.name;
                SelectionData.PreviousNumVertices.Add(currObjMesh.name, currObjMesh.GetComponent<MeshFilter>().mesh.vertices.Length);
                currObjMesh.GetComponent<MeshFilter>().mesh.MarkDynamic();
                SelectionData.PreviousSelectedIndices.Add(currObjMesh.name, currObjMesh.GetComponent<MeshFilter>().mesh.GetIndices(0));
                SelectionData.PreviousVertices.Add(currObjMesh.name, currObjMesh.GetComponent<MeshFilter>().mesh.vertices);

                UVList = new List<Vector2>();
                currObjMesh.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                SelectionData.PreviousUVs.Add(currObjMesh.name, UVList.ToArray<Vector2>());

                currObjMesh.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            if (SelectionData.SavedOutlines.ContainsKey(currObjMesh.name)) // if this object has outlines associated with it, process the outlines
            {
                foreach (GameObject outline in SelectionData.SavedOutlines[currObjMesh.name])
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
                SelectionData.SavedOutlines.Add(currObjMesh.name, new HashSet<GameObject>());
            }

            //if (currObjMesh.tag != "highlight")
            //{
            //    if (!preSelectionOutlines.ContainsKey(currObjMesh.name))                              //
            //    {                                                                             // Add a highlight for this mesh if there isn't one already
            //        preSelectionOutlines.Add(currObjMesh.name, MakeHandOutline(currObjMesh.name));    //
            //    }
            //    if (!rightOutlines.ContainsKey(currObjMesh.name))
            //    {
            //        rightOutlines.Add(currObjMesh.name, MakeHandOutline(currObjMesh.name));
            //    }
            //}

            ProcessMesh(currObjMesh);
        }

        if (controller0.device.GetHairTriggerDown() || controller1.device.GetHairTriggerDown()) // Clicked: a selection has been made
        {
            // Debug.Log("Hand. OnTriggerDown " + collidingMeshes.Count().ToString());
            SelectionData.RecentlySelectedObj.Clear();
            SelectionData.RecentlySelectedObjNames.Clear();

            selectionCount++;
            eventString = "selection " + selectionCount.ToString();
            foreach (GameObject currObjMesh in collidingMeshes)
            {
              //  Debug.Log("Hand Selection: " + currObjMesh.name);

                currObjMesh.GetComponent<MeshFilter>().mesh.UploadMeshData(false);

                if (notExperiment)
                {
                    if (!SelectionData.SavedOutlines.ContainsKey(currObjMesh.name))
                    {
                        SelectionData.SavedOutlines.Add(currObjMesh.name, new HashSet<GameObject>());
                    }

                    foreach (GameObject outline in OutlineManager.preSelectionOutlines[currObjMesh.name])
                    {
                        GameObject savedOutline = OutlineManager.CopyObject(outline); // save the highlights at the point of selection

                        //GameObject savedOutline = OutlineManager.MakeNewOutline(outline);
                        SelectionData.SavedOutlines[currObjMesh.name].Add(savedOutline);
                    }
                    //GameObject savedRightOutline = CopyObject(rightOutlines[currObjMesh.name]);
                }


                ////test, the immediate if statement
                //        bool divisBy3 = previousSelectedIndices[currObjMesh.name].Length % 3 == 0;
                //if (!divisBy3)
                //{
                //    Debug.Log("old: " + previousSelectedIndices[currObjMesh.name].Length.ToString() + ". for " + currObjMesh.name);
                //}

                if (SelectionData.NumberOfSelections.ContainsKey(currObjMesh.name))
                {
                    SelectionData.NumberOfSelections[currObjMesh.name] = SelectionData.NumberOfSelections[currObjMesh.name] + 1;
                }
                else
                {
                    SelectionData.NumberOfSelections.Add(currObjMesh.name, 1);
                }

                if (SelectionData.NumberOfSelections[currObjMesh.name] == 1)
                {
                    SelectionData.RecentUVs.Add(currObjMesh.name, SelectionData.PreviousUVs[currObjMesh.name]);
                    SelectionData.RecentVertices.Add(currObjMesh.name, SelectionData.PreviousVertices[currObjMesh.name]);
                    SelectionData.RecentNumVertices.Add(currObjMesh.name, SelectionData.PreviousNumVertices[currObjMesh.name]);
                    SelectionData.RecentSelectedIndices.Add(currObjMesh.name, SelectionData.PreviousSelectedIndices[currObjMesh.name]);
                }
                else
                {
                    //Moving previous info to recent before updating, for undoing - NEW
                    SelectionData.RecentUVs[currObjMesh.name] = SelectionData.PreviousUVs[currObjMesh.name];
                    SelectionData.RecentVertices[currObjMesh.name] = SelectionData.PreviousVertices[currObjMesh.name];
                    SelectionData.RecentNumVertices[currObjMesh.name] = SelectionData.PreviousNumVertices[currObjMesh.name];
                    if (SelectionData.RecentUnselectedIndices.ContainsKey(currObjMesh.name))
                    {
                        SelectionData.RecentUnselectedIndices[currObjMesh.name] = SelectionData.PreviousUnselectedIndices[currObjMesh.name];
                    }
                    else
                    {
                        //On the first selection everything is stored as selected.
                        SelectionData.RecentUnselectedIndices.Add(currObjMesh.name, SelectionData.PreviousUnselectedIndices[currObjMesh.name]);
                    }
                    SelectionData.RecentSelectedIndices[currObjMesh.name] = SelectionData.PreviousSelectedIndices[currObjMesh.name];
                }

                SelectionData.RecentTriangleStates = SelectionData.TriangleStates;

                SelectionData.RecentlySelectedObjNames.Add(currObjMesh.name);
                SelectionData.RecentlySelectedObj.Add(currObjMesh);

                SelectionData.TriangleStates = triangleStatesBeforeSelection;
                SelectionData.PreviousNumVertices[currObjMesh.name] = currObjMesh.GetComponent<MeshFilter>().mesh.vertices.Length;
                SelectionData.PreviousVertices[currObjMesh.name] = currObjMesh.GetComponent<MeshFilter>().mesh.vertices;

                UVList = new List<Vector2>();
                currObjMesh.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                SelectionData.PreviousUVs[currObjMesh.name] = UVList.ToArray<Vector2>();

                //Debug.Log("old: " + oldVertNum.ToString() + ". New: " + previousNumVertices[currObjMesh.name].ToString() +". for " + currObjMesh.name);

                //The submesh to start
                int submeshNum = 0;
                Material[] origMaterials = currObjMesh.GetComponent<Renderer>().materials;
                for (int i = 0; i < origMaterials.Length; i++)
                {
                    if (origMaterials[i].name == "Selected Transparent (Instance)")
                    {
                        submeshNum = i;
                    }
                }

                SelectionData.PreviousSelectedIndices[currObjMesh.name] = currObjMesh.GetComponent<MeshFilter>().mesh.GetIndices(submeshNum);     // updates original indices to store the the most recently selected portion

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

                if (SelectionData.PreviousUnselectedIndices.ContainsKey(currObjMesh.name))
                {
                    SelectionData.PreviousUnselectedIndices[currObjMesh.name] = currObjMesh.GetComponent<MeshFilter>().mesh.GetIndices(0);
                }
                else
                {
                    SelectionData.PreviousUnselectedIndices.Add(currObjMesh.name, currObjMesh.GetComponent<MeshFilter>().mesh.GetIndices(0));
                }

                ////test, following foreach statement
                //        foreach (int index in previousUnselectedIndices[currObjMesh.name])
                //        {
                //            if (!(index >= 0 && index < currObjMesh.GetComponent<MeshFilter>().mesh.vertices.Count()))
                //            {
                //                Debug.Log("Unselected index " + index.ToString() + " out of bounds for vertices at click update.");
                //            }
                //        }


                SelectionData.ObjectsWithSelections.Add(currObjMesh.name);



                // process outlines and associate them with the original objects
                //SelectionData.SavedOutlines[currObjMesh.name].Add(savedLeftOutline);
                //SelectionData.SavedOutlines[currObjMesh.name].Add(savedRightOutline);

                foreach (GameObject outline in SelectionData.SavedOutlines[currObjMesh.name])
                {
                    SelectionData.PreviousSelectedIndices[outline.name] = outline.GetComponent<MeshFilter>().mesh.GetIndices(0);
                    SelectionData.ObjectsWithSelections.Add(outline.name);
                    SelectionData.PreviousNumVertices[outline.name] = outline.GetComponent<MeshFilter>().mesh.vertices.Length;
                    SelectionData.PreviousVertices[outline.name] = outline.GetComponent<MeshFilter>().mesh.vertices;

                    UVList = new List<Vector2>();
                    outline.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                    SelectionData.PreviousUVs[outline.name] = UVList.ToArray<Vector2>();
                }


            }

        }
        return eventString;
    }

   

    private bool OnNormalSideOfPlane(Vector3 pt, Vector3 planeNormalInLocalSpace, Vector3 planePointInLocalSpace)
    {
        return Vector3.Dot(planeNormalInLocalSpace, pt) >= Vector3.Dot(planeNormalInLocalSpace, planePointInLocalSpace);
    }

    private void ProcessMesh(GameObject item)
    {
        Mesh mesh = item.GetComponent<MeshFilter>().mesh;
        selectedIndices.Clear();

        if (!SelectionData.ObjectsWithSelections.Contains(item.name) || item.CompareTag("highlightmesh"))
        {
            unselectedIndices.Clear();
        }
        else
        {
            unselectedIndices = SelectionData.PreviousUnselectedIndices[item.name].ToList<int>();
        }

        int[] indices = SelectionData.PreviousSelectedIndices[item.name];        // original indices is set to be JUST the selected part, that's why nothing else is drawn
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





        //List<Vector3> transformedVertices = new List<Vector3>(vertices.Count);

        // for (int i = 0; i < vertices.Count; i++)
        // {
        //     transformedVertices.Add(item.gameObject.transform.TransformPoint(vertices[i]));
        //  }

        //--------------------
        /*
        NativeArray<Vector3> verticesNArray = new NativeArray<Vector3>(SelectionData.PreviousVertices[item.name], Allocator.TempJob);
        NativeArray<Vector3> result = new NativeArray<Vector3>(verticesNArray.Length, Allocator.TempJob);
        Transform[] t = new Transform[verticesNArray.Length];
        for(int i = 0; i < t.Length; i++)
        {
            t[i] = item.gameObject.transform;
        }
        TransformAccessArray transformAccessArray = new TransformAccessArray(t);

        TransformVerticesJob transVertsJob = new TransformVerticesJob();
        transVertsJob.vertices = verticesNArray;
        transVertsJob.results = result;
        

        JobHandle handle = transVertsJob.Schedule(transformAccessArray);
        handle.Complete();
        verticesNArray.Dispose();
        transformAccessArray.Dispose();

        List<Vector3> transformedVertices = result.ToList();

        result.Dispose();
        */

        //-----------------------

        triangleStatesBeforeSelection = new Dictionary<Triangle, SelectionData.TriangleSelectionState>(SelectionData.TriangleStates);


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


            Vector3 planeNormalInLocalSpace = item.gameObject.transform.InverseTransformDirection(currentPlane.transform.up);
            Vector3 planePointInLocalSpace = item.gameObject.transform.InverseTransformPoint(currentPlane.transform.position);
            float dotPlaneNormalPoint = Vector3.Dot(planeNormalInLocalSpace, planePointInLocalSpace);

            for (int i = 0; i < indices.Length / 3; i++)
            {
                triangleIndex0 = indices[3 * i];
                triangleIndex1 = indices[3 * i + 1];
                triangleIndex2 = indices[3 * i + 2];


                SelectionData.TriangleSelectionState currentTriangleState = SelectionData.TriangleSelectionState.UnselectedOrigUnselectedNow;
                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                {
                    Triangle tri = new Triangle(triangleIndex0, triangleIndex1, triangleIndex2);
                    try
                    {
                        currentTriangleState = triangleStatesBeforeSelection[tri];
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

                float dotTriangleIndex0Plane = Vector3.Dot(planeNormalInLocalSpace, vertices[triangleIndex0]);
                float dotTriangleIndex1Plane = Vector3.Dot(planeNormalInLocalSpace, vertices[triangleIndex1]);
                float dotTriangleIndex2Plane = Vector3.Dot(planeNormalInLocalSpace, vertices[triangleIndex2]);

                //Test for intersection if all the points are not on the same side of the plane
                bool testIntersection = !((dotTriangleIndex0Plane >= dotPlaneNormalPoint && dotTriangleIndex1Plane >= dotPlaneNormalPoint && dotTriangleIndex2Plane >= dotPlaneNormalPoint) || (dotTriangleIndex0Plane < dotPlaneNormalPoint && dotTriangleIndex1Plane < dotPlaneNormalPoint && dotTriangleIndex2Plane < dotPlaneNormalPoint));
                    //BoundingCircleIntersectsWithPlane(planeNormalInLocalSpace, planePointInLocalSpace, dotPlaneNormalPoint, vertices[triangleIndex0], vertices[triangleIndex1], vertices[triangleIndex2]);

                if (testIntersection)
                {
                    side0 = IntersectsWithPlane(ref intersectPoint0, ref intersectUV0, UVs[triangleIndex0], UVs[triangleIndex1], vertices[triangleIndex0], vertices[triangleIndex1], planeNormalInLocalSpace, planePointInLocalSpace);
                    side1 = IntersectsWithPlane(ref intersectPoint1, ref intersectUV1, UVs[triangleIndex1], UVs[triangleIndex2], vertices[triangleIndex1], vertices[triangleIndex2], planeNormalInLocalSpace, planePointInLocalSpace);
                    side2 = IntersectsWithPlane(ref intersectPoint2, ref intersectUV2, UVs[triangleIndex0], UVs[triangleIndex2], vertices[triangleIndex0], vertices[triangleIndex2], planeNormalInLocalSpace, planePointInLocalSpace);
                }

                if (!side0 && !side1 && !side2) // 0 intersections
                {
                    if (dotTriangleIndex0Plane >= dotPlaneNormalPoint)//OnNormalSideOfPlane(vertices[triangleIndex0], planeNormalInLocalSpace, planePointInLocalSpace))
                    {
                        AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                        //selectedIndices.Add(triangleIndex0);
                        //selectedIndices.Add(triangleIndex1);
                        //selectedIndices.Add(triangleIndex2);
                        if (!notExperiment && item.gameObject.tag != "highlightmesh")
                        {
                            UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                        }
                    }
                    else
                    {
                        AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                        //unselectedIndices.Add(triangleIndex0);
                        //unselectedIndices.Add(triangleIndex1);
                        //unselectedIndices.Add(triangleIndex2);
                        if (!notExperiment && item.gameObject.tag != "highlightmesh")
                        {
                            UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
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
                            if (dotTriangleIndex0Plane >= dotPlaneNormalPoint)//OnNormalSideOfPlane(vertices[triangleIndex0], planeNormalInLocalSpace, planePointInLocalSpace))
                            {
                                AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                                //selectedIndices.Add(triangleIndex0);
                                //selectedIndices.Add(triangleIndex1);
                                //selectedIndices.Add(triangleIndex2);
                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                                }
                            }
                            else
                            {
                                AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                                //unselectedIndices.Add(triangleIndex0);
                                //unselectedIndices.Add(triangleIndex1);
                                //unselectedIndices.Add(triangleIndex2);
                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                                }
                            }
                        }
                        else
                        {

                            intersectIndex0 = numVertices++;
                            intersectIndex1 = numVertices++;

                            vertices.Add(intersectPoint0);
                            vertices.Add(intersectPoint1);


                           // transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint0));
                           // transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint1));
                            UVs.Add(intersectUV0);
                            UVs.Add(intersectUV1);

                            //AddToGraph(intersectPoint0, intersectPoint1, ref pointGraph);
                            unsortedOutlinePts.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint0), unsortedOutlinePts.Count, unsortedOutlinePts.Count + 1));
                            unsortedOutlinePts.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint1), unsortedOutlinePts.Count, unsortedOutlinePts.Count - 1));

                            if (dotTriangleIndex1Plane >= dotPlaneNormalPoint)//OnNormalSideOfPlane(vertices[triangleIndex1], planeNormalInLocalSpace, planePointInLocalSpace))
                            {

                                // Add the indices for various triangles to selected and unselected

                                AddNewIndices(selectedIndices, intersectIndex1, intersectIndex0, triangleIndex1);
                                //selectedIndices.Add(intersectIndex1);
                                //selectedIndices.Add(intersectIndex0);
                                //selectedIndices.Add(triangleIndex1);
                                AddNewIndices(unselectedIndices, triangleIndex0, intersectIndex0, intersectIndex1);
                                //unselectedIndices.Add(triangleIndex0);
                                //unselectedIndices.Add(intersectIndex0);
                                //unselectedIndices.Add(intersectIndex1);
                                AddNewIndices(unselectedIndices, triangleIndex2, triangleIndex0, intersectIndex1);
                                //unselectedIndices.Add(triangleIndex2);
                                //unselectedIndices.Add(triangleIndex0);
                                //unselectedIndices.Add(intersectIndex1);

                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, intersectIndex1, intersectIndex0, triangleIndex1, true);
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, intersectIndex0, intersectIndex1, false);
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex2, triangleIndex0, intersectIndex1, false);
                                }

                            }
                            else
                            {
                                AddNewIndices(unselectedIndices, intersectIndex1, intersectIndex0, triangleIndex1);
                                //unselectedIndices.Add(intersectIndex1);
                                //unselectedIndices.Add(intersectIndex0);
                                //unselectedIndices.Add(triangleIndex1);
                                AddNewIndices(selectedIndices, triangleIndex0, intersectIndex0, intersectIndex1);
                                //selectedIndices.Add(triangleIndex0);
                                //selectedIndices.Add(intersectIndex0);
                                //selectedIndices.Add(intersectIndex1);
                                AddNewIndices(selectedIndices, triangleIndex2, triangleIndex0, intersectIndex1);
                                //selectedIndices.Add(triangleIndex2);
                                //selectedIndices.Add(triangleIndex0);
                                //selectedIndices.Add(intersectIndex1);

                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, intersectIndex1, intersectIndex0, triangleIndex1, false);
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, intersectIndex0, intersectIndex1, true);
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex2, triangleIndex0, intersectIndex1, true);
                                }
                            }
                        }
                    }
                    else if (side0 && side2)
                    {
                        if (PlaneCollision.ApproximatelyEquals(intersectPoint0, intersectPoint2))
                        {
                            // plane intersects a triangle vertex
                            if (dotTriangleIndex1Plane >= dotPlaneNormalPoint)//OnNormalSideOfPlane(vertices[triangleIndex1], planeNormalInLocalSpace, planePointInLocalSpace))
                            {
                                AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                                //selectedIndices.Add(triangleIndex0);
                                //selectedIndices.Add(triangleIndex1);
                                //selectedIndices.Add(triangleIndex2);
                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                                }
                            }
                            else
                            {
                                AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                                //unselectedIndices.Add(triangleIndex0);
                                //unselectedIndices.Add(triangleIndex1);
                                //unselectedIndices.Add(triangleIndex2);
                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                                }
                            }
                        }
                        else
                        {
                            intersectIndex0 = numVertices++;
                            intersectIndex2 = numVertices++;

                            vertices.Add(intersectPoint0);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                            vertices.Add(intersectPoint2);

                           // transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint0));
                           // transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint2));
                            UVs.Add(intersectUV0);
                            UVs.Add(intersectUV2);

                            unsortedOutlinePts.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint0), unsortedOutlinePts.Count, unsortedOutlinePts.Count + 1));
                            unsortedOutlinePts.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint2), unsortedOutlinePts.Count, unsortedOutlinePts.Count - 1));

                            if (dotTriangleIndex0Plane >= dotPlaneNormalPoint)//OnNormalSideOfPlane(vertices[triangleIndex0], planeNormalInLocalSpace, planePointInLocalSpace))
                            {
                                AddNewIndices(selectedIndices, intersectIndex2, triangleIndex0, intersectIndex0);
                                //selectedIndices.Add(intersectIndex2);
                                //selectedIndices.Add(triangleIndex0);
                                //selectedIndices.Add(intersectIndex0);
                                AddNewIndices(unselectedIndices, triangleIndex2, intersectIndex2, intersectIndex0);
                                //unselectedIndices.Add(triangleIndex2);
                                //unselectedIndices.Add(intersectIndex2);
                                //unselectedIndices.Add(intersectIndex0);
                                AddNewIndices(unselectedIndices, triangleIndex1, triangleIndex2, intersectIndex0);
                                //unselectedIndices.Add(triangleIndex1);
                                //unselectedIndices.Add(triangleIndex2);
                                //unselectedIndices.Add(intersectIndex0);

                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, intersectIndex2, triangleIndex0, intersectIndex0, true);
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex2, intersectIndex2, intersectIndex0, false);
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex1, triangleIndex2, intersectIndex0, false);
                                }
                            }
                            else
                            {
                                AddNewIndices(unselectedIndices, intersectIndex2, triangleIndex0, intersectIndex0);
                                //unselectedIndices.Add(intersectIndex2);
                                //unselectedIndices.Add(triangleIndex0);
                                //unselectedIndices.Add(intersectIndex0);
                                AddNewIndices(selectedIndices, triangleIndex2, intersectIndex2, intersectIndex0);
                                //selectedIndices.Add(triangleIndex2);
                                //selectedIndices.Add(intersectIndex2);
                                //selectedIndices.Add(intersectIndex0);
                                AddNewIndices(selectedIndices, triangleIndex1, triangleIndex2, intersectIndex0);
                                //selectedIndices.Add(triangleIndex1);
                                //selectedIndices.Add(triangleIndex2);
                                //selectedIndices.Add(intersectIndex0);

                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, intersectIndex2, triangleIndex0, intersectIndex0, false);
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex2, intersectIndex2, intersectIndex0, true);
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex1, triangleIndex2, intersectIndex0, true);
                                }
                            }
                        }
                    }
                    else if (side1 && side2)
                    {
                        if (PlaneCollision.ApproximatelyEquals(intersectPoint1, intersectPoint2))
                        {
                            // plane intersects a triangle vertex
                            if (dotTriangleIndex1Plane >= dotPlaneNormalPoint)//OnNormalSideOfPlane(vertices[triangleIndex1], planeNormalInLocalSpace, planePointInLocalSpace))
                            {
                                AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                                //selectedIndices.Add(triangleIndex0);
                                //selectedIndices.Add(triangleIndex1);
                                //selectedIndices.Add(triangleIndex2);
                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                                }
                            }
                            else
                            {
                                AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                                //unselectedIndices.Add(triangleIndex0);
                                //unselectedIndices.Add(triangleIndex1);
                                //unselectedIndices.Add(triangleIndex2);
                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                                }
                            }
                        }
                        else
                        {

                            intersectIndex1 = numVertices++;
                            intersectIndex2 = numVertices++;

                            vertices.Add(intersectPoint1);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                            vertices.Add(intersectPoint2);

                           // transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint1));
                            //transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint2));
                            UVs.Add(intersectUV1);
                            UVs.Add(intersectUV2);

                            unsortedOutlinePts.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint1), unsortedOutlinePts.Count, unsortedOutlinePts.Count + 1));
                            unsortedOutlinePts.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint2), unsortedOutlinePts.Count, unsortedOutlinePts.Count - 1));

                            if (dotTriangleIndex2Plane >= dotPlaneNormalPoint)//OnNormalSideOfPlane(vertices[triangleIndex2], planeNormalInLocalSpace, planePointInLocalSpace))
                            {
                                AddNewIndices(selectedIndices, intersectIndex1, triangleIndex2, intersectIndex2);
                                //selectedIndices.Add(intersectIndex1);
                                //selectedIndices.Add(triangleIndex2);
                                //selectedIndices.Add(intersectIndex2);
                                AddNewIndices(unselectedIndices, intersectIndex2, triangleIndex0, intersectIndex1);
                                //unselectedIndices.Add(intersectIndex2);
                                //unselectedIndices.Add(triangleIndex0);
                                //unselectedIndices.Add(intersectIndex1);
                                AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, intersectIndex1);
                                //unselectedIndices.Add(triangleIndex0);
                                //unselectedIndices.Add(triangleIndex1);
                                //unselectedIndices.Add(intersectIndex1);

                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, intersectIndex1, triangleIndex2, intersectIndex2, true);
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, intersectIndex2, triangleIndex0, intersectIndex1, false);
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, intersectIndex1, false);
                                }
                            }
                            else
                            {
                                AddNewIndices(unselectedIndices, intersectIndex1, triangleIndex2, intersectIndex2);
                                //unselectedIndices.Add(intersectIndex1);
                                //unselectedIndices.Add(triangleIndex2);
                                //unselectedIndices.Add(intersectIndex2);
                                AddNewIndices(selectedIndices, intersectIndex2, triangleIndex0, intersectIndex1);
                                //selectedIndices.Add(intersectIndex2);
                                //selectedIndices.Add(triangleIndex0);
                                //selectedIndices.Add(intersectIndex1);
                                AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, intersectIndex1);
                                //selectedIndices.Add(triangleIndex0);
                                //selectedIndices.Add(triangleIndex1);
                                //selectedIndices.Add(intersectIndex1);

                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, intersectIndex1, triangleIndex2, intersectIndex2, false);
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, intersectIndex2, triangleIndex0, intersectIndex1, true);
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, intersectIndex1, true);
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
                            if (dotTriangleIndex2Plane >= dotPlaneNormalPoint)//OnNormalSideOfPlane(vertices[triangleIndex2], planeNormalInLocalSpace, planePointInLocalSpace))
                            {
                                AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                                //selectedIndices.Add(triangleIndex0);
                                //selectedIndices.Add(triangleIndex1);
                                //selectedIndices.Add(triangleIndex2);
                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                                }
                            }
                            else
                            {
                                AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                                //unselectedIndices.Add(triangleIndex0);
                                //unselectedIndices.Add(triangleIndex1);
                                //unselectedIndices.Add(triangleIndex2);
                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                                }
                            }
                        }
                        else if (side1)
                        {
                            // plane intersects a triangle vertex
                            if (dotTriangleIndex0Plane >= dotPlaneNormalPoint)//OnNormalSideOfPlane(vertices[triangleIndex0], planeNormalInLocalSpace, planePointInLocalSpace))
                            {
                                AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                                //selectedIndices.Add(triangleIndex0);
                                //selectedIndices.Add(triangleIndex1);
                                //selectedIndices.Add(triangleIndex2);
                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                                }
                            }
                            else
                            {
                                AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                                //unselectedIndices.Add(triangleIndex0);
                                //unselectedIndices.Add(triangleIndex1);
                                //unselectedIndices.Add(triangleIndex2);
                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                                }
                            }
                        }
                        else if (side2)
                        {
                            // plane intersects a triangle vertex
                            if (dotTriangleIndex1Plane >= dotPlaneNormalPoint)//OnNormalSideOfPlane(vertices[triangleIndex1], planeNormalInLocalSpace, planePointInLocalSpace))
                            {
                                AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                                //selectedIndices.Add(triangleIndex0);
                                //selectedIndices.Add(triangleIndex1);
                                //selectedIndices.Add(triangleIndex2);
                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, true);
                                }
                            }
                            else
                            {
                                AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                                //unselectedIndices.Add(triangleIndex0);
                                //unselectedIndices.Add(triangleIndex1);
                                //unselectedIndices.Add(triangleIndex2);
                                if (!notExperiment && item.gameObject.tag != "highlightmesh")
                                {
                                    UpdateTriangleState(triangleStatesBeforeSelection, currentTriangleState, triangleIndex0, triangleIndex1, triangleIndex2, false);
                                }
                            }
                        }
                    }
                }
            }

            if (item.gameObject.tag != "highlightmesh" && notExperiment)
            {
                OutlineManager.ResizePreselectedPoints(item, unsortedOutlinePts, planePass, currentPlane);
            }
            unsortedOutlinePts.Clear();                                                                                                                                                                  //should this be sorted or unsorted points??
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
            Material baseMaterial = item.GetComponent<Renderer>().sharedMaterials[0];
            materials[0] = baseMaterial;                                //DetermineBaseMaterial(baseMaterial);         // Sets unselected as transparent                               
            materials[1] = Resources.Load("Selected Transparent") as Material;      // May need to specify which submesh we get this from? -> THIS SETS SELECTION AS ORANGE STUFF
            item.GetComponent<Renderer>().sharedMaterials = materials;
        }

        else // set highlight meshes foreach (int index in selectedIndices)
        {
            mesh.subMeshCount = 1;
            mesh.SetTriangles(selectedIndices, 0);
        }

        //mesh.RecalculateBounds();
        mesh.RecalculateNormals();
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

    private bool IntersectsWithPlane(ref Vector3 intersectPoint, ref Vector2 intersectUV, Vector2 vertex0UV, Vector2 vertex1UV, Vector3 lineVertexLocal0, Vector3 lineVertexLocal1, Vector3 planeNormalInLocalSpace, Vector3 planePointInLocalSpace) // checks if a particular edge intersects with the plane, if true, returns point of intersection along edge
    {
        Vector3 lineSegmentLocal = lineVertexLocal1 - lineVertexLocal0;
        float dot = Vector3.Dot(planeNormalInLocalSpace, lineSegmentLocal);
        Vector3 w = planePointInLocalSpace - lineVertexLocal0;

        float epsilon = 0.00001f;
        if (Mathf.Abs(dot) > epsilon)
        {
            float factor = Vector3.Dot(planeNormalInLocalSpace, w) / dot;
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

 
    private bool BoundingCircleIntersectsWithPlane(Vector3 planeNormalInTriangleLocalSpace, Vector3 planePointInTriangleLocalSpace, float dotPlaneNormalPoint, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 AB = b - a;
        Vector3 AC = c - a;
        float dotABAB = Vector3.Dot(AB, AB);
        float dotABAC = Vector3.Dot(AB, AC);
        float dotACAC = Vector3.Dot(AC, AC);
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
        else center = a + s * (AB) + t * (AC);

        Vector3 crpt = center - referencePt;
        float sqrRadius = Vector3.Dot(crpt, crpt);

        Vector3 closestPointInPlaneToCenter = center + (planeNormalInTriangleLocalSpace * (-(Vector3.Dot(planeNormalInTriangleLocalSpace, center) - dotPlaneNormalPoint)));

        return (center - closestPointInPlaneToCenter).sqrMagnitude < sqrRadius;

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

    ///// <summary>
    ///// Make a Gameobject that will follow the user's hands
    ///// </summary>
    ///// <param name="meshName"></param>
    ///// <returns></returns>
    //private GameObject MakeHandOutline(String meshName)
    //{
    //    GameObject newOutline = new GameObject();
    //    newOutline.name = meshName + " highlight";
    //    newOutline.AddComponent<MeshRenderer>();
    //    newOutline.AddComponent<MeshFilter>();
    //    newOutline.GetComponent<MeshFilter>().mesh = new Mesh();
    //    newOutline.GetComponent<MeshFilter>().mesh.MarkDynamic();
    //    newOutline.GetComponent<Renderer>().material = Resources.Load("TestMaterial") as Material;
    //    newOutline.layer = LayerMask.NameToLayer("Ignore Raycast");

    //    return newOutline;
    //}
}

public struct TransformVerticesJob : IJobParallelForTransform
{
    [ReadOnly]
    public NativeArray<Vector3> vertices;

    public NativeArray<Vector3> results;
    

    public void Execute(int index, TransformAccess transform)
    {
        results[index] = transform.rotation * new Vector3(vertices[index].x * transform.localScale.x, vertices[index].y * transform.localScale.y, vertices[index].z * transform.localScale.z) + transform.position;//(item.gameObject.transform.TransformPoint(vertices[index]));
    }
}