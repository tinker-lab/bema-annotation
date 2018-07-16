using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public class VolumeCubeSelectionState : InteractionState
{
    //private const bool debug = false;

    //InteractionState stateToReturnTo;
    private ControllerInfo controller0;
    private ControllerInfo controller1;

    //private GameObject leftPlane;   //
    //private GameObject rightPlane;  //Used to detect collision with meshes in the model

    private GameObject centerCube;  //Cube that is attached to the controllers

    private int planeLayer;                         //Layer that cube and planes are on
    private static int outlineObjectCount = 0;      //Keeps saved outlines distinguishable from one another

    private List<GameObject> collidingMeshes;       //List of meshes currently being collided with
    private HashSet<GameObject> cubeColliders;      //All the objects the cube is colliding with
    VolumeCubeCollision centerComponent;            //Script on cube that we get the list of colliders from

    SelectionData selectionData;
     
    //private static Dictionary<string, Vector3[]> previousVertices;              //Key = name of obj with mesh, Value = all vertices of the mesh at the time of last click
    //private static Dictionary<string, Vector2[]> previousUVs;                   //Key = name of obj with mesh, Value = all UVs of the mesh at the time of last click
    //private Dictionary<string, int[]> previousUnselectedIndices;                //Key = name of object with mesh, Value = all indices that have not been selected (updated when user clicks)
    //private static Dictionary<string, int> previousNumVertices;                 //Key = name of object with mesh, Value = original set of vertices (updated when user clicks and mesh is split)
    //private static Dictionary<string, int[]> previousSelectedIndices;           //key = name of object with mesh, Value = original set of selected indices (updated when user clicks)
    //private static HashSet<string> objWithSelections;                           //Collection of the the names of all the meshes that have had pieces selected from them.
    //private static Dictionary<string, HashSet<GameObject>> savedOutlines;       //Key = name of object in model, Value = all the SAVED outline game objects attached to it
    private static Dictionary<string, Dictionary<int, List<OutlinePoint>>> savedOutlinePoints;     //Key = name of the object in model, Value = all the sets of outline points
    //private static Dictionary<string, GameObject> leftOutlines;                 //left hand outlines per model that are currently being manipulated (KEY = name of model object, VALUE = outline object)
    //private static Dictionary<string, GameObject> rightOutlines;                //right hand outlines per model that are currently being manipulated (KEY = name of model object, VALUE = outline object)

    //private List<Vector3> outlinePoints;    //Pairs of two connected points to be used in drawing an outline mesh

    List<int> selectedIndices;      //Reused for each mesh during ProcessMesh()
    List<int> unselectedIndices;    //^^^^

    //make six vectors for the normals
    private Vector3[] normals;
    private enum cubeSides { back, top, right, front, bottom, left };
    Vector3[] rotationVectors;

    //starting vector between the hands
    private Vector3 startingDiagonal;

    private GameObject head;

    //dealing with rotation
    //Quaternion startingRotation;
    //Vector3 previousDiagonal;
    //Quaternion previousRotation;

    //getters for the dictionaries
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
    public Dictionary<string, Dictionary<int, List<OutlinePoint>>> SavedOutlinePoints
    {
        get { return savedOutlinePoints; }
        set { savedOutlinePoints = value;  }
    }
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
    public VolumeCubeSelectionState(ControllerInfo controller0Info, ControllerInfo controller1Info, SelectionData sharedData) 
    {
        //NOTE: Selecting more than one mesh will result in highlights appearing in the wrong place
        desc = "VolumeCubeSelectionState";
        controller0 = controller0Info;
        controller1 = controller1Info;

        planeLayer = LayerMask.NameToLayer("PlaneLayer");

        //leftPlane = CreateHandPlane(controller0, "handSelectionLeftPlane");
        //rightPlane = CreateHandPlane(controller1, "handSelectionRightPlane");

        //The center cube is anchored between controllers and detects collisions with other objects
        centerCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        centerCube.name = "volumeCube";
        centerCube.GetComponent<Renderer>().material = Resources.Load("Volume Cube") as Material; //might want to change material
        centerCube.AddComponent<MeshCollider>();
        centerCube.GetComponent<MeshCollider>().convex = true;
        centerCube.GetComponent<MeshCollider>().isTrigger = true;
        centerCube.AddComponent<Rigidbody>();
        centerCube.GetComponent<Rigidbody>().isKinematic = true;
        centerComponent = centerCube.AddComponent<VolumeCubeCollision>();
        //centerCube.layer = planeLayer;
        centerCube.layer = LayerMask.NameToLayer("Ignore Raycast");

        //if (!debug)
        //{
        //    centerCube.GetComponent<MeshRenderer>().enabled = false;
        //}

        collidingMeshes = new List<GameObject>();      
        cubeColliders = new HashSet<GameObject>();

        //TODO: should these persist between states? Yes so only make one instance of the state. Should use the Singleton pattern here

        selectionData = sharedData;

        //objWithSelections = new HashSet<string>();
        //previousNumVertices = new Dictionary<string, int>();              //Keeps track of how many vertices a mesh should have
        //previousUnselectedIndices = new Dictionary<string, int[]>();      //Keeps track of indices that were previously unselected
        //previousSelectedIndices = new Dictionary<string, int[]>();
        //previousVertices = new Dictionary<string, Vector3[]>();
        //previousUVs = new Dictionary<string, Vector2[]>();
        //savedOutlines = new Dictionary<string, HashSet<GameObject>>();
        savedOutlinePoints = new Dictionary<string, Dictionary<int, List<OutlinePoint>>>();
        selectedIndices = new List<int>();
        unselectedIndices = new List<int>();
        //outlinePoints = new List<Vector3>();

        //this.stateToReturnTo = stateToReturnTo;

        //leftOutlines = new Dictionary<string, GameObject>();
        //rightOutlines = new Dictionary<string, GameObject>();

        //set starting normal values
        normals = new Vector3[6];
        normals[(int)cubeSides.front] = Vector3.forward;
        normals[(int)cubeSides.back] = Vector3.back;
        normals[(int)cubeSides.top] = Vector3.down;
        normals[(int)cubeSides.bottom] = Vector3.up;
        normals[(int)cubeSides.left] = Vector3.right;
        normals[(int)cubeSides.right] = Vector3.left;

        rotationVectors = new Vector3[6];

        //set starting diagonal (between controllers)
        startingDiagonal = new Vector3(1f, 1f, 1f);
        //previousDiagonal = new Vector3(1f, -1f, -1f);

        head = GameObject.Find("Camera (eye)");

        //set starting rotation
        //startingRotation = Quaternion.FromToRotation(Vector3.forward, new Vector3(1, -1, -1));// Quaternion.AngleAxis(-Vector3.Angle(Vector3.forward, new Vector3(1, 0, -1)), Vector3.up) * Quaternion.AngleAxis(-Vector3.Angle(Vector3.forward, new Vector3(0, -1, 0)), Vector3.right);
        //previousRotation = Quaternion.identity;
    }

    ///// <summary>
    ///// Sets up the planes that follow each hand/controller
    ///// </summary>
    ///// <param name="c"></param>
    ///// <param name="name"></param>
    ///// <returns></returns>
    //public GameObject CreateHandPlane(ControllerInfo c, String name)
    //{
    //    GameObject handPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
    //    handPlane.name = name;
    //    handPlane.GetComponent<Renderer>().material = Resources.Load("Plane Material") as Material;
    //    handPlane.AddComponent<MeshCollider>();
    //    handPlane.GetComponent<MeshCollider>().convex = true;
    //    handPlane.GetComponent<MeshCollider>().isTrigger = true;
    //    handPlane.AddComponent<Rigidbody>();
    //    handPlane.GetComponent<Rigidbody>().isKinematic = true;

    //    handPlane.transform.position = c.controller.transform.position;
    //    handPlane.transform.rotation = c.controller.transform.rotation;
    //    handPlane.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f); //Previously 0.03

    //    handPlane.layer = planeLayer;
    //    if (!debug)
    //    {
    //        handPlane.GetComponent<MeshRenderer>().enabled = false;
    //    }

    //    return handPlane;
    //}

    ///// <summary>
    ///// Adjusts position of planes and cube.
    ///// </summary>
    //public void UpdatePlanes()
    //{
    //    leftPlane.transform.position = controller0.controller.transform.position;
    //    rightPlane.transform.position = controller1.controller.transform.position;

    //    //the normals of both planes are always facing each other
    //    leftPlane.transform.up = (rightPlane.transform.position - leftPlane.transform.position).normalized;
    //    rightPlane.transform.up = (leftPlane.transform.position - rightPlane.transform.position).normalized;

    //    CenterCubeBetweenControllers();
    //}

    //creates the volume cube
    private void CenterCubeBetweenControllers()
    {
        //create the midpoint between the corners (this equals the center of the cube)
        //set the cube to that point
        Vector3 nonDominantCorner = controller0.controller.transform.position;
        Vector3 dominantCorner = controller1.controller.transform.position;
        Vector3 centerOfCube = Vector3.Lerp(nonDominantCorner, dominantCorner, 0.5f);
        centerCube.transform.position = centerOfCube; // + new Vector3(0, 0, -0.3f); //to push cube away from controllers
        centerCube.gameObject.GetComponent<MeshRenderer>().enabled = true;

        //rotate cube to set the orientation
        Vector3 currentDiagonal = dominantCorner - nonDominantCorner;
        Quaternion yaw = Quaternion.LookRotation(new Vector3(head.transform.forward.x, 0, head.transform.forward.z), Vector3.up);


        //Quaternion.AngleAxis(40, currentDiagonal.normalized) *
        centerCube.transform.rotation = Quaternion.FromToRotation(yaw * startingDiagonal.normalized, currentDiagonal.normalized) * yaw;

        //Debug.DrawRay(nonDominantCorner, 0.25f * currentDiagonal.normalized, Color.cyan);

        //Quaternion.FromToRotation(startingDiagonal.normalized, currentDiagonal.normalized);
        //centerCube.transform.rotation = Quaternion.LookRotation(currentDiagonal.normalized, Vector3.up) * startingRotation;
        //centerCube.transform.rotation = Quaternion.AngleAxis(-Vector3.Angle(currentDiagonal.normalized, previousDiagonal.normalized), Vector3.Cross(currentDiagonal.normalized, previousDiagonal.normalized)) * previousRotation;
        //previousDiagonal = currentDiagonal;
        //previousRotation = centerCube.transform.rotation;

        //scale cube
        float scaleSize = currentDiagonal.magnitude / startingDiagonal.magnitude;
        Vector3 scaleVec = currentDiagonal / startingDiagonal.magnitude;
        centerCube.transform.localScale = new Vector3(1f, 1f, 1f) * scaleSize;

        //rotate cube w/ respect to both controllers -- sets orientation of cube
        //RotateCube(controller0, controller1, nonDominantCorner, dominantCorner, centerCube);

        ////scale cube
        //float distance = Vector3.Distance(nonDominantCorner, dominantCorner);
        //centerCube.transform.localScale = new Vector3(1f, 1f, 1f) * distance + new Vector3(0, 0.3f, 0.3f); //.3f pushes cube outwards and upwards slightly from user
    }

    //private void RotateCube(ControllerInfo controller0Info, ControllerInfo controller1Info, Vector3 leftPos, Vector3 rightPos, GameObject cube)
    //{
    //    Vector3 xAxis = (rightPos - leftPos).normalized;

    //    Vector3 zAxis = controller0Info.isLeft ? controller1Info.trackedObj.transform.forward : controller0Info.trackedObj.transform.forward;
    //    zAxis = (zAxis - (Vector3.Dot(zAxis, xAxis) * xAxis)).normalized;
    //    Vector3 yAxis = Vector3.Cross(zAxis, xAxis).normalized;

    //    Vector3 groundY = new Vector3(0, 1);

    //    cube.transform.rotation = Quaternion.LookRotation(zAxis, yAxis);
    //}

    private void Uncollide()
    {
        int[] indices;

        foreach (GameObject collidingObj in collidingMeshes)
        {
            Mesh mesh = collidingObj.GetComponent<MeshFilter>().mesh;
            mesh.subMeshCount = 2;

            //the indices of the last selection
            indices = SelectionData.PreviousSelectedIndices[collidingObj.name];

            //If it previously had a piece selected (clicked) then revert to that selection
            if (SelectionData.ObjectsWithSelections.Contains(collidingObj.name))
            {
                //Generate a mesh to fill the entire selected part of the collider
                Vector3[] verts = SelectionData.PreviousVertices[collidingObj.name];

                List<Vector2> uvs = new List<Vector2>();
                uvs = SelectionData.PreviousUVs[collidingObj.name].ToList();

                mesh.Clear();
                mesh.vertices = verts;
                mesh.SetUVs(0, uvs);

                //set unselected and selected regions back to what they were at the last click
                if (collidingObj.tag != "highlightmesh")
                {
                    mesh.subMeshCount = 2;
                    mesh.SetTriangles(SelectionData.PreviousUnselectedIndices[collidingObj.name], 0);
                    mesh.SetTriangles(indices, 1);
                }
                else //for meshes that are outlines, use only one material (unselected will not be drawn)
                {
                    mesh.subMeshCount = 1;
                    mesh.SetTriangles(indices, 0);
                }

                mesh.RecalculateBounds();
                mesh.RecalculateNormals();

                //Go through each outline associated with the current mesh object and reset it
                //foreach (GameObject outline in savedOutlines[collidingObj.name])
                //{
                //    Mesh outlineMesh = outline.GetComponent<MeshFilter>().mesh;
                //    //Vector3[] outlineVerts = outlineMesh.vertices;
                //    Vector3[] outlineVerts = previousVertices[outline.name];
                //    List<Vector2> outlineUVs = new List<Vector2>();
                //    outlineUVs = previousUVs[outline.name].ToList();
                //    //outlineMesh.GetUVs(0, outlineUVs);

                //    outlineMesh.Clear();
                //    outlineMesh.vertices = outlineVerts;
                //    outlineMesh.SetUVs(0, outlineUVs);

                //    outlineMesh.subMeshCount = 1;
                //    outlineMesh.SetTriangles(previousSelectedIndices[outline.name], 0);

                //    outlineMesh.RecalculateBounds();
                //    outlineMesh.RecalculateNormals();
                //}
            }
            else //When no click was made
            {
                //reset object to original state (before interaction)
                if (collidingObj.tag != "highlightmesh")
                {
                    Material baseMaterial = collidingObj.GetComponent<Renderer>().materials[0];
                    baseMaterial.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                    collidingObj.GetComponent<Renderer>().materials[1] = baseMaterial;

                    //leftOutlines[collidingObj.name].GetComponent<MeshFilter>().mesh.Clear();
                    //rightOutlines[collidingObj.name].GetComponent<MeshFilter>().mesh.Clear();
                }
            }

            ////stop rendering current outline whenever hands removed from collidingObj
            //if (leftOutlines.ContainsKey(collidingObj.name) || rightOutlines.ContainsKey(collidingObj.name))
            //{
            //    leftOutlines[collidingObj.name].GetComponent<MeshRenderer>().enabled = false;
            //    rightOutlines[collidingObj.name].GetComponent<MeshRenderer>().enabled = false;
            //}
        }
    }

    public override void Deactivate()
    {
        //controller0.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;    // Disable hand rendering
        //controller1.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;    //

        //controller0.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true); // Enable rendering of controllers
        //controller1.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true); //

        centerCube.gameObject.GetComponent<MeshRenderer>().enabled = false;

        Uncollide();
    }

    public override void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        List<Vector2> UVList = new List<Vector2>();

        //Update cube (method has position, rotation, and scale in it)
        CenterCubeBetweenControllers();

        //Take input from cube about what it collides with
        cubeColliders = centerComponent.CollidedObjects;

        //if there is an object inside the cube (must have at least one)
        //clear the meshes the cube was colliding with previously
        //and add the new meshes that are currently being colliding with to the list
        if (cubeColliders.Count > 0)
        {
            collidingMeshes.Clear();
            collidingMeshes = cubeColliders.ToList();
        }
        else //If not colliding with anything, change states
        {
            //GameObject.Find("UIController").GetComponent<UIController>().ChangeState(stateToReturnTo);
            Uncollide();
            return;
        }

        foreach (GameObject currObjMesh in collidingMeshes)
        {
            //first time seeing object, if the original vertices are not stored already, store them
            if (!SelectionData.PreviousNumVertices.ContainsKey(currObjMesh.name))
            {
                SelectionData.PreviousNumVertices.Add(currObjMesh.name, currObjMesh.GetComponent<MeshFilter>().mesh.vertices.Length);
                currObjMesh.GetComponent<MeshFilter>().mesh.MarkDynamic();
                SelectionData.PreviousSelectedIndices.Add(currObjMesh.name, currObjMesh.GetComponent<MeshFilter>().mesh.GetIndices(0));
                SelectionData.PreviousVertices.Add(currObjMesh.name, currObjMesh.GetComponent<MeshFilter>().mesh.vertices);

                UVList = new List<Vector2>();
                currObjMesh.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                SelectionData.PreviousUVs.Add(currObjMesh.name, UVList.ToArray<Vector2>());

                currObjMesh.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            //if (currObjMesh.tag != "highlight")
            //{
            //    if (!leftOutlines.ContainsKey(currObjMesh.name))                              //
            //    {                                                                             //Add a highlight for this mesh if there isn't one already
            //        leftOutlines.Add(currObjMesh.name, MakeHandOutline(currObjMesh.name));    //
            //    }
            //    if (!rightOutlines.ContainsKey(currObjMesh.name))
            //    {
            //        rightOutlines.Add(currObjMesh.name, MakeHandOutline(currObjMesh.name));
            //    }
            //}

            ProcessMesh(currObjMesh, new List<int>());
        }

        //The trigger is clicked and a selection of the volume has been made
        //this if loop should make every item within the cube highlighted
        //and save the outlines and new meshes
        if (controller0.device.GetHairTriggerDown() || controller1.device.GetHairTriggerDown())
        {
            foreach (GameObject currObjMesh in collidingMeshes)
            {
                //Debug.Log("Cube Selection: " + currObjMesh.name);

                currObjMesh.GetComponent<MeshFilter>().mesh.UploadMeshData(false);
                //GameObject savedLeftOutline = CopyObject(leftOutlines[currObjMesh.name]); //save the highlights at the point of selection
                //GameObject savedRightOutline = CopyObject(rightOutlines[currObjMesh.name]);

                SelectionData.PreviousNumVertices[currObjMesh.name] = currObjMesh.GetComponent<MeshFilter>().mesh.vertices.Length;
                SelectionData.PreviousVertices[currObjMesh.name] = currObjMesh.GetComponent<MeshFilter>().mesh.vertices;

                UVList = new List<Vector2>();
                currObjMesh.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                SelectionData.PreviousUVs[currObjMesh.name] = UVList.ToArray<Vector2>();

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

                //updates original indices to store the the most recently selected portion
                SelectionData.PreviousSelectedIndices[currObjMesh.name] = currObjMesh.GetComponent<MeshFilter>().mesh.GetIndices(submeshNum);

                if (SelectionData.PreviousUnselectedIndices.ContainsKey(currObjMesh.name))
                {
                    SelectionData.PreviousUnselectedIndices[currObjMesh.name] = currObjMesh.GetComponent<MeshFilter>().mesh.GetIndices(0);
                }
                else
                {
                    SelectionData.PreviousUnselectedIndices.Add(currObjMesh.name, currObjMesh.GetComponent<MeshFilter>().mesh.GetIndices(0));
                }

                SelectionData.ObjectsWithSelections.Add(currObjMesh.name);

                if (!SelectionData.SavedOutlines.ContainsKey(currObjMesh.name))
                {
                    SelectionData.SavedOutlines.Add(currObjMesh.name, new HashSet<GameObject>());
                }

                //process outlines and associate them with the original objects
                //savedOutlines[currObjMesh.name].Add(savedLeftOutline);
                //savedOutlines[currObjMesh.name].Add(savedRightOutline);

                List<GameObject> removeOutlines = new List<GameObject>();
                //if this object has outlines associated with it, process the outlines
                foreach (GameObject outline in SelectionData.SavedOutlines[currObjMesh.name])
                {
                    if (!SelectionData.PreviousNumVertices.ContainsKey(outline.name))
                    {
                        SelectionData.PreviousNumVertices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.vertices.Length);  //Maybe want to store vertices as array instead?

                        //TODO: should this be nested?
                        //if (!previousSelectedIndices.ContainsKey(outline.name))
                        //{
                        SelectionData.PreviousSelectedIndices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.GetIndices(0));
                        SelectionData.PreviousVertices.Add(outline.name, outline.GetComponent<MeshFilter>().mesh.vertices);

                        UVList = new List<Vector2>();
                        outline.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                        SelectionData.PreviousUVs.Add(outline.name, UVList.ToArray<Vector2>());
                            //previous two lines used to be currObjMesh instead of outline, trying to see if this is correct
                        //}
                    }

                    ProcessMesh(outline, new List<int>());
                   
                    if (outline.GetComponent<MeshFilter>().mesh.GetIndices(0).Count() == 0)
                    {
                        removeOutlines.Add(outline);
                    }

                    SelectionData.PreviousSelectedIndices[outline.name] = outline.GetComponent<MeshFilter>().mesh.GetIndices(0);
                    SelectionData.ObjectsWithSelections.Add(outline.name);
                    SelectionData.PreviousNumVertices[outline.name] = outline.GetComponent<MeshFilter>().mesh.vertices.Length;
                    SelectionData.PreviousVertices[outline.name] = outline.GetComponent<MeshFilter>().mesh.vertices;

                    UVList = new List<Vector2>();
                    outline.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                    SelectionData.PreviousUVs[outline.name] = UVList.ToArray<Vector2>();
                }

                for (int i = 0; i < removeOutlines.Count(); i++)
                {
                    Debug.Log("Removing before creating new outlines: " + removeOutlines[i].name);
                    SelectionData.SavedOutlines[currObjMesh.name].Remove(removeOutlines[i]);
                    UnityEngine.Object.Destroy(removeOutlines[i]);

                }
                removeOutlines.Clear();
                //Debug.Break();

                //List<GameObject> backOutlines = new List<GameObject>();
                for (int i = 0; i < 6; i++)
                {
                    List<List<OutlinePoint>> sortedPoints = OutlineManager.OrderPoints(SavedOutlinePoints[currObjMesh.name][i]);
                    //    Debug.Log("Just called order points");

                    for (int chainIndex = 0; chainIndex < sortedPoints.Count; chainIndex++)
                    {
                        GameObject outlineObject = OutlineManager.MakeNewOutline(currObjMesh);
                        Mesh outlineMesh = OutlineManager.CreateOutlineMesh(sortedPoints[chainIndex], rotationVectors[i], outlineObject);

                        SelectionData.SavedOutlines[currObjMesh.name].Add(outlineObject);

                        if (!SelectionData.PreviousNumVertices.ContainsKey(outlineObject.name))
                        {
                            SelectionData.PreviousNumVertices.Add(outlineObject.name, outlineObject.GetComponent<MeshFilter>().mesh.vertices.Length);  //Maybe want to store vertices as array instead?

                            //TODO: should this be nested?
                            //if (!previousSelectedIndices.ContainsKey(outline.name))
                            //{
                            SelectionData.PreviousSelectedIndices.Add(outlineObject.name, outlineObject.GetComponent<MeshFilter>().mesh.GetIndices(0));
                            SelectionData.PreviousVertices.Add(outlineObject.name, outlineObject.GetComponent<MeshFilter>().mesh.vertices);

                            UVList = new List<Vector2>();
                            outlineObject.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                            SelectionData.PreviousUVs.Add(outlineObject.name, UVList.ToArray<Vector2>());
                            //previous two lines used to be currObjMesh instead of outline, trying to see if this is correct
                            //}
                        }

                        List<int> ignorePassList = new List<int>();
                        /*
                        for (int j = i; j >= 0; j--)
                        {
                            ignorePassList.Add(j);
                        }
                        */
                        ignorePassList.Add(i);
                        //if (i == 0)
                        //{
                        //    backOutlines.Add(OutlineManager.CopyObject(outlineObject));
                        //}
                        ProcessMesh(outlineObject, ignorePassList);

                        if (outlineObject.GetComponent<MeshFilter>().mesh.GetIndices(0).Count() == 0)
                        {
                           // Debug.Break();
                           if(i == 0 )//&& SelectionData.PreviousSelectedIndices[outlineObject.name].Count() > 0)
                            {
                                Debug.Log("selection went to 0 for back plane" + outlineObject.name);
                                //Debug.Break();
                            }
                            removeOutlines.Add(outlineObject);
                        }

                        SelectionData.PreviousSelectedIndices[outlineObject.name] = outlineObject.GetComponent<MeshFilter>().mesh.GetIndices(0);
                        SelectionData.ObjectsWithSelections.Add(outlineObject.name);
                        SelectionData.PreviousNumVertices[outlineObject.name] = outlineObject.GetComponent<MeshFilter>().mesh.vertices.Length;
                        SelectionData.PreviousVertices[outlineObject.name] = outlineObject.GetComponent<MeshFilter>().mesh.vertices;

                        UVList = new List<Vector2>();
                        outlineObject.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
                        SelectionData.PreviousUVs[outlineObject.name] = UVList.ToArray<Vector2>();
                    }
                }

                for (int j = 0; j < removeOutlines.Count(); j++)
                {
                    Debug.Log("Removing new outlines after their first process: " + removeOutlines[j].name);
                    SelectionData.SavedOutlines[currObjMesh.name].Remove(removeOutlines[j]);
                    UnityEngine.Object.Destroy(removeOutlines[j]);

                }
                //Debug.Break();

            }
        }
    }
    
    /// <summary>
    /// Creates a new game object with the same position, rotation, scale, material, and mesh as the original.
    /// </summary>
    /// <param name="original"></param>
    /// <returns></returns>
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
    //    copy.name = "highlight" + outlineObjectCount;
    //    outlineObjectCount++;

    //    return copy;
    //}

    ////returns true if point is on the normal side
    //private bool OnNormalSideOfPlane(Vector3 pt, GameObject plane)
    //{
    //    return Vector3.Dot(plane.transform.up, pt) >= Vector3.Dot(plane.transform.up, plane.transform.position);
    //}

    //returns true if point is on normal side
    private bool OnNormalSideOfPlane(Vector3 otherPoint, Vector3 normalPoint, Vector3 planePoint)
    {
        return Vector3.Dot(normalPoint, otherPoint) >= Vector3.Dot(planePoint, normalPoint);
    }

    //processes the mesh of an item in the scene that is colliding with the volume cube
    //creates the new triangles in the mesh and adds the necessary vertices
    private void ProcessMesh(GameObject item, List<int> passSkipList)
    {

        Mesh mesh = item.GetComponent<MeshFilter>().mesh;
        selectedIndices.Clear();

        if (!SelectionData.ObjectsWithSelections.Contains(item.name) || item.CompareTag("highlightmesh"))
        {
            unselectedIndices.Clear();
        }
        else
        {
            unselectedIndices =  SelectionData.PreviousUnselectedIndices[item.name].ToList<int>();
        }

        int[] indices = SelectionData.PreviousSelectedIndices[item.name];  //previous indices is set to be JUST the selected part, that's why nothing else is drawn
        List<Vector3> vertices = SelectionData.PreviousVertices[item.name].ToList();

        List<Vector2> UVs = SelectionData.PreviousUVs[item.name].ToList();
        int numVertices = SelectionData.PreviousNumVertices[item.name];

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

        //calculate plane normals of all six sides of the cube after rotation
        rotationVectors[(int)cubeSides.front] = (centerCube.transform.rotation * normals[(int)cubeSides.front]).normalized;
        rotationVectors[(int)cubeSides.back] = (centerCube.transform.rotation * normals[(int)cubeSides.back]).normalized;
        rotationVectors[(int)cubeSides.top] = (centerCube.transform.rotation * normals[(int)cubeSides.top]).normalized;
        rotationVectors[(int)cubeSides.bottom] = (centerCube.transform.rotation * normals[(int)cubeSides.bottom]).normalized;
        rotationVectors[(int)cubeSides.left] = (centerCube.transform.rotation * normals[(int)cubeSides.left]).normalized;
        rotationVectors[(int)cubeSides.right] = (centerCube.transform.rotation * normals[(int)cubeSides.right]).normalized;

        Debug.DrawRay(controller1.controller.transform.position, 0.25f * rotationVectors[0].normalized, Color.blue,0f,false);
        Debug.DrawRay(controller1.controller.transform.position, 0.25f * rotationVectors[1].normalized, Color.red, 0f, false);
        Debug.DrawRay(controller1.controller.transform.position, 0.25f * rotationVectors[2].normalized, Color.green, 0f, false);

        Debug.DrawRay(controller0.controller.transform.position, 0.25f * rotationVectors[3].normalized, Color.magenta, 0f, false);
        Debug.DrawRay(controller0.controller.transform.position, 0.25f * rotationVectors[4].normalized, Color.yellow, 0f, false);
        Debug.DrawRay(controller0.controller.transform.position, 0.25f * rotationVectors[5].normalized, Color.black, 0f, false);

        for (int planePass = 0; planePass < 6; planePass++)
        {
            if (passSkipList.Contains(planePass))
            {
                //This handles the case where if plane pass is zero then the next condition will break since selectedindices was never set.
                if (planePass == 0)
                {
                    selectedIndices = indices.ToList<int>();
                }
                continue;
            }

            if (planePass != 0)
            {
                indices = selectedIndices.ToArray();
                selectedIndices.Clear();
            }

            List<OutlinePoint> unsortedOutlinePts = new List<OutlinePoint>();

            //GameObject currentPlane = leftPlane;
            //if (planePass == 1)
            //{
            //    currentPlane = rightPlane;
            //    indices = selectedIndices.ToArray();
            //    selectedIndices.Clear();
            //}

            //defining a point on the side of the cube depending on which side is being looped through
            Vector3 planePoint;

            //TODO: check if these are set to the correct controllers
            if(planePass < 3)
            {
                planePoint = controller1.controller.transform.position;
            }
            else
            {
                planePoint = controller0.controller.transform.position;
            }

           

            for (int i = 0; i < indices.Length / 3 ; i++)
            {
                triangleIndex0 = indices[3 * i];
                triangleIndex1 = indices[3 * i + 1];
                triangleIndex2 = indices[3 * i + 2];

                bool side0 = IntersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex1], ref intersectPoint0, ref intersectUV0, UVs[triangleIndex0], UVs[triangleIndex1], vertices[triangleIndex0], vertices[triangleIndex1], rotationVectors[planePass], planePoint);
                bool side1 = IntersectsWithPlane(transformedVertices[triangleIndex1], transformedVertices[triangleIndex2], ref intersectPoint1, ref intersectUV1, UVs[triangleIndex1], UVs[triangleIndex2], vertices[triangleIndex1], vertices[triangleIndex2], rotationVectors[planePass], planePoint);
                bool side2 = IntersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex2], ref intersectPoint2, ref intersectUV2, UVs[triangleIndex0], UVs[triangleIndex2], vertices[triangleIndex0], vertices[triangleIndex2], rotationVectors[planePass], planePoint);


                if (!side0 && !side1 && !side2) //0 intersections
                {
                    if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], rotationVectors[planePass], planePoint))
                    {
                        AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                    }
                    else
                    {
                        if (item.name == "highlight 6")
                        {
                            Debug.Log("planePass: " + planePass.ToString());
                            Debug.Break();
                        }
                        AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                    }
                }
                else
                {  //intersections have occurred
                   //determine which side of triangle has 1 vertex
                   //add vertex and indices to appropriate mesh
                   //for side with 2, add vertices, add 2 triangles
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
                        //outlinePoints.Add(intersectPoint0);
                        //outlinePoints.Add(intersectPoint1);

                        unsortedOutlinePts.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint0), unsortedOutlinePts.Count, unsortedOutlinePts.Count + 1));
                        unsortedOutlinePts.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint1), unsortedOutlinePts.Count, unsortedOutlinePts.Count - 1));

                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex1], rotationVectors[planePass], planePoint))
                        {
                            //Add the indices for various triangles to selected and unselected
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

                        //Adds intersection points (IN LOCAL SPACE) to vertex list, keeps track of which indices they've been placed at
                        vertices.Add(intersectPoint0);
                        vertices.Add(intersectPoint2);
                        
                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint0));
                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint2));
                        UVs.Add(intersectUV0);
                        UVs.Add(intersectUV2);

                        //outlinePoints.Add(intersectPoint0);
                        //outlinePoints.Add(intersectPoint2);

                        unsortedOutlinePts.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint0), unsortedOutlinePts.Count, unsortedOutlinePts.Count + 1));
                        unsortedOutlinePts.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint2), unsortedOutlinePts.Count, unsortedOutlinePts.Count - 1));

                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], rotationVectors[planePass], planePoint))
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

                        //Adds intersection points (IN LOCAL SPACE) to vertex list, keeps track of which indices they've been placed at
                        vertices.Add(intersectPoint1);
                        vertices.Add(intersectPoint2);
                        
                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint1));
                        transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint2));
                        UVs.Add(intersectUV1);
                        UVs.Add(intersectUV2);

                        //outlinePoints.Add(intersectPoint1);
                        //outlinePoints.Add(intersectPoint2);

                        unsortedOutlinePts.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint1), unsortedOutlinePts.Count, unsortedOutlinePts.Count + 1));
                        unsortedOutlinePts.Add(new OutlinePoint(item.gameObject.transform.TransformPoint(intersectPoint2), unsortedOutlinePts.Count, unsortedOutlinePts.Count - 1));

                        if (OnNormalSideOfPlane(transformedVertices[triangleIndex2], rotationVectors[planePass], planePoint))
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
                if (!savedOutlinePoints.ContainsKey(item.name))
                {
                    savedOutlinePoints.Add(item.name, new Dictionary<int, List<OutlinePoint>>());
                }

                if (!savedOutlinePoints[item.name].ContainsKey(planePass))
                {
                    savedOutlinePoints[item.name].Add(planePass, unsortedOutlinePts);
                    //Debug.Log("Saving outline points for " + item.name + " ponts size=" + outlinePoints.Count+" first time");
                }
                else
                {
                    savedOutlinePoints[item.name][planePass] = unsortedOutlinePts;
                    //Debug.Log("Saving outline points for " + item.name + " ponts size=" + outlinePoints.Count);
                }

                //unsortedOutlinePts.Clear();
            } else{
                if (selectedIndices.Count() < 1)
                {
                    Debug.Log("Volume Cube: " + "plane " + planePass.ToString() + " made an empty selection on " + item.name);
                  //  Debug.Break();
                }
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
            materials[0] = DetermineBaseMaterial(baseMaterial); //Sets unselected as transparent
            materials[1] = Resources.Load("Selected") as Material; //May need to specify which submesh we get this from? -> THIS SETS SELECTION AS ORANGE STUFF
            item.GetComponent<Renderer>().materials = materials;
        }

        else //set highlight meshes foreach (int index in selectedIndices)
        {
            mesh.subMeshCount = 1;
            mesh.SetTriangles(selectedIndices, 0);
        }

        mesh.RecalculateNormals();
    }


    //returns value of latest index added and adds to list
    private void AddNewIndices(List<int> indices, int numToAdd)
    {
        for (int i = 0; i < numToAdd; i++)
        {
            int latestIndex = indices.Count;
            indices.Add(latestIndex);
        }
    }

    //Adds a triangle with predefined indices into a list of indices
    private void AddNewIndices(List<int> indices, int index0, int index1, int index2)
    {
        indices.Add(index0);
        indices.Add(index1);
        indices.Add(index2);
    }

    //use this method for each side of the cube instead of the plane, replace plane with side
    //instead of plane, pass in normal vector and point (corner of cube that is on that side)
    private bool IntersectsWithPlane(Vector3 lineVertexWorld0, Vector3 lineVertexWorld1, ref Vector3 intersectPoint, ref Vector2 intersectUV, Vector2 vertex0UV, Vector2 vertex1UV, Vector3 lineVertexLocal0, Vector3 lineVertexLocal1, Vector3 normalVector, Vector3 planePoint) //checks if a particular edge intersects with the plane, if true, returns point of intersection along edge
    {
        Vector3 lineSegmentLocal = lineVertexLocal1 - lineVertexLocal0;
        float dot = Vector3.Dot(normalVector, lineVertexWorld1 - lineVertexWorld0);
        Vector3 w = planePoint - lineVertexWorld0;

        float epsilon = 0.001f;
        if (Mathf.Abs(dot) > epsilon)
        {
            float factor = Vector3.Dot(normalVector, w) / dot;
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

    ////Orders the points of one mesh. NOTE: currently just uses alreadyVisited HashSet, nothing else;
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

    ////Basic DFS, adds the intersection points of edges in the order it visits them
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

    ////Takes two connected points and adds or updates entries in the list of actual points and the graph of their connections
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
    //private GameObject MakeOutline(GameObject item)
    //{
    //    GameObject newOutline = new GameObject();
    //    newOutline.name = "Cube highlight" + outlineObjectCount;
    //    newOutline.AddComponent<MeshRenderer>();
    //    newOutline.AddComponent<MeshFilter>();
    //    newOutline.GetComponent<MeshFilter>().mesh = new Mesh();
    //    newOutline.GetComponent<MeshFilter>().mesh.MarkDynamic();
    //    newOutline.GetComponent<Renderer>().material = Resources.Load("TestMaterial") as Material;
    //    newOutline.tag = "highlightmesh";
    //    newOutline.layer = LayerMask.NameToLayer("Ignore Raycast");

    //    outlineObjectCount++;

    //    newOutline.transform.position = item.transform.position;
    //    newOutline.transform.localScale = item.transform.localScale;
    //    newOutline.transform.rotation = item.transform.rotation;

    //    //Debug.Log("Cube Selection makeOutline: " + item.name);

    //    return newOutline;
    //}
}