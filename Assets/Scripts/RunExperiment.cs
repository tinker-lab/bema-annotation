﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private SelectionData selectionData;
    public OutlineManager outlineManager;

    long startTrialTicks;
    long endTrialTicks;
    string selectionEvent;

    bool setupControllers;
    bool sceneIsLoaded;

    private static int sceneIndex;
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
            if (endTrialTicks == 0L)
            {
                controller0 = GameObject.Find("Controller (left)");
                controller1 = GameObject.Find("Controller (right)");
                testObjectParent = GameObject.Find("TestObj");

                controller0Info = new ControllerInfo(controller0);
                controller1Info = new ControllerInfo(controller1);

                DetermineLeftRightControllers();
                selectionEvent = currentState.HandleEvents(controller0Info, controller1Info);   //modified all HandleEvents methods to return "" or the name of an event to be recorded
                recorder.UpdateLists(controller0Info, controller1Info, System.DateTime.Now.Ticks, selectionEvent);                // ticks are 100 nanoseconds

                if (Input.GetKeyUp(KeyCode.L))      //reLoad scene
                {
                    transition.ReloadScene();
                    currentState.Deactivate();
                    LeaveTrialScene();
                }

                if (controller0Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad) || controller1Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad))     //finish trial
                {
                    Debug.Log("timer stopped " + currentState.Desc);
                    endTrialTicks = System.DateTime.Now.Ticks;
                    float selectedArea = CalculateSelectedArea();
                    recorder.EndTrial(selectedArea, endTrialTicks - startTrialTicks);
                    currentState.Deactivate();
                    LeaveTrialScene();
                }
            }
        }
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
        recorder.SetTrialID(sceneIndex, currentState);
        startTrialTicks = System.DateTime.Now.Ticks;

        sceneIsLoaded = true;
    }

    private float CalculateSelectedArea(){
        // in every trial scene (except the real world example) there is a parent gameObject TestObj 
        // where child 0 is the preselected goal object the participant cannot collide with and 
        // child 1 is the transparent object of the same shape that the participant selects.
        // The real world example has only one child of the test object that matters - the one participants interact with
        //  there is another object representing just the part that should be selected that can be measured as a goal area.

        Mesh goal;
        Mesh selection;
        if (SceneManager.GetActiveScene().name.Equals("Real World"))
        {
            goal = GameObject.Find("13na-selection").GetComponent<MeshFilter>().mesh;           //this object is in the real world scene, adjusted to the same scale as the full object, hidden under the floor with the MeshRenderer turned off
            selection = testObjectParent.transform.GetChild(1).GetComponent<MeshFilter>().mesh;
        }
        else
        {
            goal = testObjectParent.transform.GetChild(0).GetComponent<MeshFilter>().mesh;
            selection = testObjectParent.transform.GetChild(1).GetComponent<MeshFilter>().mesh;
        }
        
        if (selection.subMeshCount > 0)
        {
            float goalArea = TriangleArea(goal.GetTriangles(1), goal.vertices);
            float selectionArea = TriangleArea(selection.GetTriangles(1), selection.vertices);
            return selectionArea - goalArea;
        }
        else
        {
            Debug.Log("Interaction shape not selected");
        }
        // Both children should have submeshes 0 unselected and 1 selected.
        // Iterate over the triangles in submesh 1 to calculate their total areas.
        // Return the difference preselection area - participant selection area.
        return 0f;
    }

    /* code for area of triangles adapted from http://james-ramsden.com/area-of-a-triangle-in-3d-c-code/ */
    private float TriangleArea(int[] trianglePts, Vector3[] verts){
        float area = 0f;
        for (int i = 0; i < trianglePts.Length; i += 3){
            Vector3 pt0 = verts[trianglePts[i]];
            Vector3 pt1 = verts[trianglePts[i + 1]];
            Vector3 pt2 = verts[trianglePts[i + 2]];

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
