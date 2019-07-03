using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;


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

    //long startTrialTicks;
    //long endTrialTicks;
    System.Diagnostics.Stopwatch stopwatch;
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
        Debug.Log("Starting Experiment Scene " + sceneOrder.ToString());

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
            if (stopwatch.IsRunning)//endTrialTicks == 0L)
            {
                controller0 = GameObject.Find("Controller (left)");
                controller1 = GameObject.Find("Controller (right)");

                controller0Info = new ControllerInfo(controller0);
                controller1Info = new ControllerInfo(controller1);

                DetermineLeftRightControllers();
                selectionEvent = currentState.HandleEvents(controller0Info, controller1Info);   //modified all HandleEvents methods to return "" or the name of an event to be recorded
                recorder.UpdateLists(controller0Info, controller1Info, hmd.gameObject.transform, System.DateTime.Now.Ticks, selectionEvent);                // ticks are 100 nanoseconds

                if (Input.GetKeyUp(KeyCode.L))      //reLoad scene
                {
                    if (currentState.CanTransition())
                    {
                        Reset();
                    }
                    else
                    {
                        Debug.Log("Cannot reset state if colliding with objects or currently selecting");
                    }
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
                            Vector4 selectionAreaStats = TriangleArea(selectedObj, selection, selection.vertices);

                            Vector2 selectionAreaGeneral = TriangleSubmeshAreas(selectedObj, selection, selection.vertices);
                            Vector2 goalAreaGeneral = TriangleSubmeshAreas(goalObj, goal, goal.vertices);
                            Debug.Log("Selection mesh --- Selection Area: " + selectionAreaGeneral.x + " Unselected Area: " + selectionAreaGeneral.y);
                            Debug.Log("Goal mesh --- Selection Area: " + goalAreaGeneral.x + " Unselected Area: " + goalAreaGeneral.y);

                            //TruePositives + falsePositives (i.e. Did they select any area)
                            if (selectionAreaStats.x + selectionAreaStats.y == 0)
                            {
                                Debug.Log("selected 0 area - Keep Going");
                                //endTrialTicks = 0L;
                                stopwatch.Start(); // restart the time to keep counting since you haven't finished yet.
                                return;
                            }

                            double f1 = CalculateF1(selectionAreaStats);
                            double mcc = CalculateMCC(selectionAreaStats);
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
                            recorder.EndTrial((double)selectionAreaStats.x, (double)selectionAreaStats.y, (double)selectionAreaStats.z, (double)selectionAreaStats.w, f1, mcc, stopwatch.ElapsedMilliseconds / 1000.0f);//endTrialTicks - startTrialTicks);
                            Debug.Log("TP: " + selectionAreaStats.x + " FP: " + selectionAreaStats.y + " FN: " + selectionAreaStats.z + " TN: " + selectionAreaStats.w + " F1: "+f1+" mcc: "+mcc);
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
        testObjectParent = GameObject.Find("TestObj");

        GameObject goalObj = testObjectParent.transform.GetChild(0).gameObject;
        GameObject selectedObj = testObjectParent.transform.GetChild(1).gameObject;
        Mesh goal = goalObj.GetComponent<MeshFilter>().mesh;
        Mesh selection = selectedObj.GetComponent<MeshFilter>().mesh;
        Dictionary<Triangle, SelectionData.TriangleSelectionState> triangleStates = new Dictionary<Triangle, SelectionData.TriangleSelectionState>();
        int[] goalSelectionIndices = goal.GetTriangles(1); // Get the indices for the target selection area (submesh 1)
        int[] goalUnselectionIndices = goal.GetTriangles(0);

        // There should only be 1 submesh at the start
        // We know that the selection mesh starts out as the combined selected and then unselected indices (In that order!) from the goal mesh
        // since that is how it was created in ObjectMaker.CombineSelectedAndUnselected().
        //int[] selectionObjectIndices = selection.GetTriangles(0);
        for (int i = 0; i < goalSelectionIndices.Length; i += 3)
        {
            Triangle tri = new Triangle(goalSelectionIndices[i], goalSelectionIndices[i + 1], goalSelectionIndices[i + 2]);
            triangleStates.Add(tri, SelectionData.TriangleSelectionState.SelectedOrigUnselectedNow);
        }
        for (int i = 0; i < goalUnselectionIndices.Length; i += 3)
        {
            Triangle tri = new Triangle(goalUnselectionIndices[i], goalUnselectionIndices[i + 1], goalUnselectionIndices[i + 2]);
            triangleStates.Add(tri, SelectionData.TriangleSelectionState.UnselectedOrigUnselectedNow);
        }
        SelectionData.TriangleStates = triangleStates;

        //Vector4 selectionAreaStats = TriangleArea(selectedObj, selection, selection.vertices);
        //double f1 = CalculateF1(selectionAreaStats);
       // double mcc = CalculateMCC(selectionAreaStats);
       // Debug.Log("On Load: TP: " + selectionAreaStats.x + " FP: " + selectionAreaStats.y + " FN: " + selectionAreaStats.z + " TN: " + selectionAreaStats.w + " F1: " + f1 + " mcc: " + mcc);

        UndoManager undoMgr = new UndoManager(controller0Info, controller1Info, selectionData);

        if (stateIndex == 1)
        {
            Debug.Log("Init Yea-Big");
            currentState = new NavigationState(controller0Info, controller1Info, selectionData, undoMgr, true); // the true here an optional boolean for whether or not we are running the experiment. it defaults false, but when true you cannot teleport and changing states uses the ChangeState method in this class rather than UIController
        }
        else if (stateIndex == 2)
        {
            Debug.Log("Init Volume Cube");
            currentState = new VolumeCubeSelectionState(controller0Info, controller1Info, selectionData, undoMgr, true); 
        }
        else if (stateIndex == 3)
        {
            Debug.Log("Init SliceNSwipe");
            currentState = new SliceNSwipeSelectionState(controller0Info, controller1Info, selectionData, undoMgr, true, zeroDominant); // zeroDominant is a boolean that refers to whether controller0 is in the participant's dominant hand in TransitionScene.
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
        stopwatch = System.Diagnostics.Stopwatch.StartNew();

        sceneIsLoaded = true;
    }

    private double CalculateF1(Vector4 areaStats){

        double TP = areaStats.x;
        double FP = areaStats.y;
        double FN = areaStats.z;
        //double TN = areaStats.w;
        // P = TP / (TP + FP)
        // R = TP / (TP + FN)
        double precision = TP / (TP + FP);
        double recall = TP / (TP + FN);
        // F1 = 2 * (P * R) / (P + R)
        return 2 * (precision * recall) / (precision + recall);
    }

    private double CalculateMCC (Vector4 areaStats)
    {
        double TP = areaStats.x;
        double FP = areaStats.y;
        double FN = areaStats.z;
        double TN = areaStats.w;

        return ((TP * TN) - (FP * FN)) / Math.Sqrt((TP + FP) * (TP + FN) * (TN + FP) * (TN + FN));
    }

    /* code for area of triangles adapted from http://james-ramsden.com/area-of-a-triangle-in-3d-c-code/ */
    private Vector4 TriangleArea(GameObject obj, Mesh mesh, Vector3[] verts){
        float truePositiveArea = 0f;
        float falsePositiveArea = 0f;
        float trueNegativeArea = 0f;
        float falseNegativeArea = 0f;
        int[] unselectedIndices = mesh.GetTriangles(0);

        List<Vector3> transformedVertices = new List<Vector3>(verts.Length);
        for (int i = 0; i < verts.Length; i++)
        {
            transformedVertices.Add(obj.gameObject.transform.TransformPoint(verts[i]));
        }

        /*
        int[] combinedIndices;
        if (mesh.subMeshCount > 1)
        {
            int[] selectedIndices = mesh.GetTriangles(1);
            combinedIndices = new int[unselectedIndices.Length + selectedIndices.Length];
            selectedIndices.CopyTo(combinedIndices, 0);
            unselectedIndices.CopyTo(combinedIndices, selectedIndices.Length);
        }
        else
        {
            combinedIndices = unselectedIndices;
        }
        

        

        for (int i = 0; i < combinedIndices.Length; i += 3){
            Vector3 pt0 = transformedVertices[combinedIndices[i]];
            Vector3 pt1 = transformedVertices[combinedIndices[i + 1]];
            Vector3 pt2 = transformedVertices[combinedIndices[i + 2]];

            float sideA = Vector3.Distance(pt0, pt1);
            float sideB = Vector3.Distance(pt1, pt2);
            float sideC = Vector3.Distance(pt0, pt2);

            float s = (sideA + sideB + sideC) / 2f;
            float area = Mathf.Sqrt(s * (s - sideA) * (s - sideB) * (s - sideC));

            try
            {
                Triangle tri = new Triangle(combinedIndices[i], combinedIndices[i + 1], combinedIndices[i + 2]);
                switch (SelectionData.TriangleStates[tri])
                {
                    case SelectionData.TriangleSelectionState.SelectedOrigSelectedNow:
                        truePositiveArea += area;
                        break;
                    case SelectionData.TriangleSelectionState.SelectedOrigUnselectedNow:
                        falseNegativeArea += area;
                        break;
                    case SelectionData.TriangleSelectionState.UnselectedOrigSelectedNow:
                        falsePositiveArea += area;
                        break;
                    case SelectionData.TriangleSelectionState.UnselectedOrigUnselectedNow:
                        trueNegativeArea += area;
                        break;
                }
            }
            catch (KeyNotFoundException)
            {
                Debug.Log("Can't find triangle key for state");
            }
        }
        */

        for (int i = 0; i < unselectedIndices.Length; i += 3)
        {
            Vector3 pt0 = transformedVertices[unselectedIndices[i]];
            Vector3 pt1 = transformedVertices[unselectedIndices[i + 1]];
            Vector3 pt2 = transformedVertices[unselectedIndices[i + 2]];

            float sideA = Vector3.Distance(pt0, pt1);
            float sideB = Vector3.Distance(pt1, pt2);
            float sideC = Vector3.Distance(pt0, pt2);

            float s = (sideA + sideB + sideC) / 2f;
            float area = Mathf.Sqrt(s * (s - sideA) * (s - sideB) * (s - sideC));

            try
            {
                Triangle tri = new Triangle(unselectedIndices[i], unselectedIndices[i + 1], unselectedIndices[i + 2]);
                switch (SelectionData.TriangleStates[tri])
                {
                    case SelectionData.TriangleSelectionState.SelectedOrigUnselectedNow:
                        falseNegativeArea += area;
                        break;
                    case SelectionData.TriangleSelectionState.UnselectedOrigUnselectedNow:
                        trueNegativeArea += area;
                        break;
                    default:
                        Debug.Log("Shouldn't get here. Case number " + SelectionData.TriangleStates[tri].ToString());
                        break;
                }
            }
            catch (KeyNotFoundException)
            {
                Debug.Log("Can't find triangle key for state");
            }
        }

        if (mesh.subMeshCount > 1)
        {
            int[] selectedIndices = mesh.GetTriangles(1);
            for (int i = 0; i < selectedIndices.Length; i += 3)
            {
                Vector3 pt0 = transformedVertices[selectedIndices[i]];
                Vector3 pt1 = transformedVertices[selectedIndices[i + 1]];
                Vector3 pt2 = transformedVertices[selectedIndices[i + 2]];

                float sideA = Vector3.Distance(pt0, pt1);
                float sideB = Vector3.Distance(pt1, pt2);
                float sideC = Vector3.Distance(pt0, pt2);

                float s = (sideA + sideB + sideC) / 2f;
                float area = Mathf.Sqrt(s * (s - sideA) * (s - sideB) * (s - sideC));

                try
                {
                    Triangle tri = new Triangle(selectedIndices[i], selectedIndices[i + 1], selectedIndices[i + 2]);
                    switch (SelectionData.TriangleStates[tri])
                    {
                        case SelectionData.TriangleSelectionState.SelectedOrigSelectedNow:
                            truePositiveArea += area;
                            break;
                        case SelectionData.TriangleSelectionState.UnselectedOrigSelectedNow:
                            falsePositiveArea += area;
                            break;
                        default:
                            Debug.Log("Shouldn't get here 2. Case number "+SelectionData.TriangleStates[tri].ToString());
                            break;
                    }
                }
                catch (KeyNotFoundException)
                {
                    Debug.Log("Can't find triangle key for state");
                }
            }
        }


        return new Vector4(truePositiveArea, falsePositiveArea, falseNegativeArea, trueNegativeArea);
    }

    /* code for area of triangles adapted from http://james-ramsden.com/area-of-a-triangle-in-3d-c-code/ */
    private Vector2 TriangleSubmeshAreas(GameObject obj, Mesh mesh, Vector3[] verts)
    {
        float selectedArea = 0f;
        float unselectedArea = 0f;
        int[] unselectedIndices = mesh.GetTriangles(0);
        int[] selectedIndices = mesh.GetTriangles(1);
        List<Vector3> transformedVertices = new List<Vector3>(verts.Length);

        for (int i = 0; i < verts.Length; i++)
        {
            transformedVertices.Add(obj.gameObject.transform.TransformPoint(verts[i]));
        }

        for (int i = 0; i < selectedIndices.Length; i += 3)
        {
            Vector3 pt0 = transformedVertices[selectedIndices[i]];
            Vector3 pt1 = transformedVertices[selectedIndices[i + 1]];
            Vector3 pt2 = transformedVertices[selectedIndices[i + 2]];

            float sideA = Vector3.Distance(pt0, pt1);
            float sideB = Vector3.Distance(pt1, pt2);
            float sideC = Vector3.Distance(pt0, pt2);

            float s = (sideA + sideB + sideC) / 2f;
            float area = Mathf.Sqrt(s * (s - sideA) * (s - sideB) * (s - sideC));
            selectedArea += area;
        }
        for (int i = 0; i < unselectedIndices.Length; i += 3)
        {
            Vector3 pt0 = transformedVertices[unselectedIndices[i]];
            Vector3 pt1 = transformedVertices[unselectedIndices[i + 1]];
            Vector3 pt2 = transformedVertices[unselectedIndices[i + 2]];

            float sideA = Vector3.Distance(pt0, pt1);
            float sideB = Vector3.Distance(pt1, pt2);
            float sideC = Vector3.Distance(pt0, pt2);

            float s = (sideA + sideB + sideC) / 2f;
            float area = Mathf.Sqrt(s * (s - sideA) * (s - sideB) * (s - sideC));
            unselectedArea += area;
        }

        return new Vector2(selectedArea, unselectedArea);
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
