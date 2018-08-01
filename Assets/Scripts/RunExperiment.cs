using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Diagnostics;

public class RunExperiment : MonoBehaviour {

    public GameObject testObjectParent;

    public GameObject controller0;
    public GameObject controller1;

    private static RecordData recorder;

    private static int stateIndex;

    private static TransitionSceneScript transition;
    private InteractionState currentState;

    private ControllerInfo controller0Info;
    private ControllerInfo controller1Info;
    private GameObject hmd;

    private SelectionData selectionData;
    public OutlineManager outlineManager;

    long startTrialTicks;
    long endTrialTicks;
    Stopwatch stopwatch;
    string selectionEvent;

    bool setupControllers;
    bool sceneIsLoaded;

    private static int sceneIndex;
    private static int sceneOrder;
    private static bool zeroDominant;

    public static RecordData Recorder
    {
        get { return recorder; }
        set { recorder = value; } 
    }

    public static int SceneIndex
    {
        get { return sceneIndex; }
        set { sceneIndex = value; }
    }

    public static int SceneOrder
    {
        get { return sceneOrder; }
        set { sceneOrder = value; }
    }

    public static int StateIndex
    {
        get { return stateIndex; }
        set { stateIndex = value; }
    }

    public static TransitionSceneScript Transition
    {
        get { return transition; }
        set { transition = value; }
    }

    public static bool ZeroDominant
    {
        get { return zeroDominant; }
        set { zeroDominant = value; }
    }

    // Use this for initialization
    public RunExperiment() {

        //Debug.Log("starting RunExperiment");
        //sceneIndex = sceneInd;
        //stateIndex = interactionState;

        //SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex(sceneIndex));  //scene 0 is the BasicScene which is in the project all the time bc it has the floor and the camera and this script. other scenes w test objects are loaded and unloaded.

        setupControllers = false;
        sceneIsLoaded = false;

        selectionData = new SelectionData();
        outlineManager = new OutlineManager();

        //recorder = dataset;

        

        //init landing zone, scene changer
        //into between state where you start timer by pressing a button -> "landing zone"
        //landing zone starts measurements and the scene. landing zone could be in this class??
    }

    void Init()
    {
        SceneManager.LoadScene(sceneIndex,LoadSceneMode.Additive);
        SceneManager.sceneLoaded += OnSceneLoaded;

        controller0 = GameObject.Find("Controller (left)");
        controller1 = GameObject.Find("Controller (right)");
        testObjectParent = GameObject.Find("TestObj");

        controller0Info = new ControllerInfo(controller0);
        controller1Info = new ControllerInfo(controller1);
        hmd = GameObject.Find("Camera (eye)");

        setupControllers = true;
        Debug.Log("Starting ExperimentController. state index == " + stateIndex.ToString() + " scene index: " + sceneIndex.ToString());

        selectionEvent = "";
    }


    // Update is called once per frame
    void Update () {
        //if (controller0.GetComponent<SteamVR_TrackedObject>().index == SteamVR_TrackedObject.EIndex.None || controller1.GetComponent<SteamVR_TrackedObject>().index == SteamVR_TrackedObject.EIndex.None)
        //{
        //    return;
        //}

        if (!setupControllers)
        {
            Init();
        }
        if (sceneIsLoaded)
        {
            if (stopwatch.IsRunning())//endTrialTicks == 0L)
            {
                controller0 = GameObject.Find("Controller (left)");
                controller1 = GameObject.Find("Controller (right)");
                testObjectParent = GameObject.Find("TestObj");

                controller0Info = new ControllerInfo(controller0);
                controller1Info = new ControllerInfo(controller1);

                DetermineLeftRightControllers();
                selectionEvent = currentState.HandleEvents(controller0Info, controller1Info);   //modified all HandleEvents methods to return "" or the name of an event to be recorded
                recorder.UpdateLists(controller0Info, controller1Info, hmd.gameObject.transform, System.DateTime.Now.Ticks, selectionEvent);                // ticks are 100 nanoseconds

                if (Input.GetKeyUp(KeyCode.L))      //reLoad scene
                {
                    Reset();
                }

                if (controller0Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad) || controller1Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad))     //finish trial
                {
                    if(currentState.CanTransition()){
                        Debug.Log("timer stopped " + currentState.Desc);
                        //endTrialTicks = System.DateTime.Now.Ticks;
                        stopwatch.Stop();

                        GameObject goalObj = testObjectParent.transform.GetChild(0).gameObject;
                        GameObject selectedObj = testObjectParent.transform.GetChild(1).gameObject;

                        Mesh goal = goalObj.GetComponent<MeshFilter>().mesh;
                        Mesh selection = selectedObj.GetComponent<MeshFilter>().mesh;
                        if (selection.subMeshCount > 0)
                        {
                            if (selection.subMeshCount > 2)
                            {
                                Debug.Log("SubMesh Count: " + selection.subMeshCount);
                            }
                            double goalArea = (double)TriangleArea(goalObj, goal, goal.vertices);
                            double selectionArea = (double)TriangleArea(selectedObj, selection, selection.vertices);

                            if (selectionArea == 0)
                            {
                                Debug.Log("selected 0 area - Keep Going");
                                //endTrialTicks = 0L;
                                stopwatch.Start(); // restart the time to keep counting since you haven't finished yet.
                                return;
                            }

                            double selectedAreaDiff = CalculateSelectedArea(goalArea, selectionArea);
                            double selectedPercentage = CalculateAreaPercentage(selectedAreaDiff, goalArea);
                            //if (selectedPercentage >= 60.0)
                            //{
                            //    endTrialTicks = 0L;
                            //    Debug.Log("large percent difference. Making example Objects");
                            //    GameObject selectedShowObject = OutlineManager.CopyObject(selectedObj);
                            //    selectedShowObject.GetComponent<MeshFilter>().mesh.SetTriangles(selection.GetTriangles(1), 0);
                            //    List<Vector2> uvs = new List<Vector2>();
                            //    selection.GetUVs(1, uvs);
                            //    selectedShowObject.GetComponent<MeshFilter>().mesh.SetUVs(1, uvs);
                            //    selectedShowObject.name = "Your Selection";
                            //    selectedShowObject.transform.localPosition = new Vector3(1.32222f, 0.248f, -1.0819333f);
                            //    selectedShowObject.transform.localRotation = new Quaternion(0, -3.084f, 0, 0);
                            //    selectedShowObject.GetComponent<MeshRenderer>().material = Resources.Load("GrayConcrete") as Material;

                            //    GameObject goalShowObject = OutlineManager.CopyObject(goalObj);
                            //    goalShowObject.GetComponent<MeshFilter>().mesh.SetTriangles(goal.GetTriangles(1), 0);
                            //    uvs.Clear();
                            //    goal.GetUVs(1, uvs);
                            //    goalShowObject.GetComponent<MeshFilter>().mesh.SetUVs(1, uvs);
                            //    goalShowObject.name = "The Goal";
                            //    goalShowObject.transform.localPosition = new Vector3(-0.34f, 0.248f, -1.39473f);
                            //    goalShowObject.transform.localRotation = new Quaternion(0, -338.124f, 0, 0);
                            //    goalShowObject.GetComponent<MeshRenderer>().material = Resources.Load("BlueConrete") as Material;
                            //    return;
                            //}
                            recorder.EndTrial(goalArea, selectionArea, selectedAreaDiff, selectedPercentage, stopwatch.ElapsedMilliseconds / 1000.0);//endTrialTicks - startTrialTicks);
                            Debug.Log(selectionArea + " - " + goalArea + " = " + selectedAreaDiff + ",  " + selectedPercentage + "%");
                            currentState.Deactivate();
                            LeaveTrialScene();
                        }
                        else
                        {
                            Debug.Log("Interaction shape not selected");
                        }
                    }
                }
            }
        }
	}

    private void Reset()
    {
        currentState.Deactivate();
        LeaveTrialScene();
        transition.ReloadScene();
    }

    private void OnSceneLoaded(Scene current, LoadSceneMode mode){
        SceneManager.SetActiveScene(current);

        if (stateIndex == 1)
        {
            Debug.Log("Init Yea-Big");
            currentState = new NavigationState(controller0Info, controller1Info, selectionData, true); // the true here an optional boolean for whether or not we are running the experiment. it defaults false, but when true you cannot teleport and changing states uses the ChangeState method in this class rather than UIController
        }
        else if (stateIndex == 2)
        {
            Debug.Log("Init Volume Cube");
            currentState = new VolumeCubeSelectionState(controller0Info, controller1Info, selectionData, true); 
        }
        else if (stateIndex == 3)
        {
            Debug.Log("Init SliceNSwipe");
            currentState = new SliceNSwipeSelectionState(controller0Info, controller1Info, selectionData, true, zeroDominant); // zeroDominant is a boolean that refers to whether controller0 is in the participant's dominant hand in TransitionScene.
        }
        else if (stateIndex == 4)
        {
            Debug.Log("Init Raycast");
            currentState = new RayCastSelectionState(controller0Info, controller1Info, selectionData);
        }
        else
        {
            currentState = recorder.GetSelectionState();

        }
        recorder.SetTrialID(sceneIndex, sceneOrder, currentState);
        //startTrialTicks = System.DateTime.Now.Ticks;
        stopwatch = Stopwatch.StartNew();

        sceneIsLoaded = true;
    }

    private double CalculateSelectedArea(double goal, double selection){

            double area = selection - goal;
            return area;
    }

    private double CalculateAreaPercentage (double areaDiff, double goalArea)
    {
        double percentArea = areaDiff / goalArea * 100;

        return percentArea;
    }

    /* code for area of triangles adapted from http://james-ramsden.com/area-of-a-triangle-in-3d-c-code/ */
    private float TriangleArea(GameObject obj, Mesh mesh, Vector3[] verts){
        float area = 0f;
        int[] trianglePts = mesh.GetTriangles(1);
        List<Vector3> transformedVertices = new List<Vector3>(verts.Length);

        for (int i = 0; i < verts.Length; i++)
        {
            transformedVertices.Add(obj.gameObject.transform.TransformPoint(verts[i]));
        }

        for (int i = 0; i < trianglePts.Length; i += 3){
            Vector3 pt0 = transformedVertices[trianglePts[i]];
            Vector3 pt1 = transformedVertices[trianglePts[i + 1]];
            Vector3 pt2 = transformedVertices[trianglePts[i + 2]];

            float sideA = Vector3.Distance(pt0, pt1);
            float sideB = Vector3.Distance(pt1, pt2);
            float sideC = Vector3.Distance(pt0, pt2);

            float s = (sideA + sideB + sideC) / 2;
            area += Mathf.Sqrt(s * (s - sideA) * (s - sideB) * (s - sideC));
        }
        return area;
    }

    void LeaveTrialScene()
    {
        if (!SceneManager.GetActiveScene().name.Equals("Basic Scene"))
        {
            SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex(0));      // set's the active scene to the basic scene since the testObj scene is going to be unloaded.
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        //SceneManager.LoadScene(0); // Transition Scene
        SceneManager.UnloadSceneAsync(sceneIndex);
        //SceneManager.LoadScene(0, LoadSceneMode.Single);
        
    }

    void DetermineLeftRightControllers()
    {
        try
        {
            if ((int)controller0Info.device.index == (SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost)))
            {
                controller0Info.isLeft = true;
                controller1Info.isLeft = false;
            }
            else
            {
                controller0Info.isLeft = false;
                controller1Info.isLeft = true;
            }
        }
        catch (System.IndexOutOfRangeException e)
        {
            print(e.Message);
            controller0Info.isLeft = true;
            controller1Info.isLeft = false;
        }
    }

    public void ChangeState(InteractionState newState)
    {
        currentState.Deactivate();
        currentState = newState;
    }
}
