using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunExperiment : MonoBehaviour {

    public GameObject controller0;
    public GameObject controller1;
    public GameObject testObjectParent;

    RecordData recorder;

    private InteractionState currentState;

    private ControllerInfo controller0Info;
    private ControllerInfo controller1Info;

    private SelectionData selectionData;
    public OutlineManager outlineManager;

    bool firstUpdate = true;
    bool trialStarted = false;

    long startTrialTicks;
    long endTrialTicks;
    string selectionEvent;

    List<int> sceneIndices;
    int nextSceneIndex;

	// Use this for initialization
	void Init () {
        controller0Info = new ControllerInfo(controller0);
        controller1Info = new ControllerInfo(controller1);

        selectionData = new SelectionData();
        outlineManager = new OutlineManager();

        sceneIndices = new List<int>();
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++){
            sceneIndices.Add(i);
        }
        nextSceneIndex = 0;                 //????????? we need to figure out how we want to randomize these.

        selectionEvent = "";

        StartCoroutine("SelectInterface");
        StartCoroutine("WaitForTimer");

        recorder = new RecordData(controller0Info, controller1Info, currentState, SceneManager.sceneCountInBuildSettings);

        //init landing zone, scene changer
		//into between state where you start timer by pressing a button -> "landing zone"
        //landing zone starts measurements and the scene. landing zone could be in this class??
	}

    IEnumerator WaitForTimer(){
        while(!trialStarted){
            if(controller0Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad) || controller1Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad)){
                Debug.Log("timer started");
                recorder.SetTrialID(SceneManager.GetActiveScene().buildIndex);
                startTrialTicks = System.DateTime.Now.Ticks;
                trialStarted = true;
            }
            yield return null;
        }
    }

    IEnumerator SelectInterface(){
        bool achieved = false;
        while(!achieved){
            if(Input.GetKeyDown(KeyCode.Alpha1)){
                currentState = new NavigationState(controller0Info, controller1Info, selectionData, true); //outlines/selection are following hands around after a selection when you pull them out of an object, as well as the white cube and z-fighting problems
                achieved = true;
            } else if (Input.GetKeyDown(KeyCode.Alpha2)){
                currentState = new VolumeCubeSelectionState(controller0Info, controller1Info, selectionData); //transparent cube turns white when you collide
                achieved = true;
            } else if (Input.GetKeyDown(KeyCode.Alpha3)){
                currentState = new SliceNSwipeSelectionState(controller0Info, controller1Info, selectionData); //z-fighting between the overlayed cube, gaze selection too opaque, swordLine at weird rotation
                achieved = true;
            } else if (Input.GetKeyDown(KeyCode.Alpha4)){
                currentState = new RayCastSelectionState(controller0Info, controller1Info, selectionData);
                achieved = true;
            }
            yield return null;
        }
    }
	
	// Update is called once per frame
	void Update () {
        if (controller0.GetComponent<SteamVR_TrackedObject>().index == SteamVR_TrackedObject.EIndex.None || controller1.GetComponent<SteamVR_TrackedObject>().index == SteamVR_TrackedObject.EIndex.None)
        {
            return;
        }

        if (firstUpdate)
        {
            Init();
            firstUpdate = false;
        }
        if (currentState != null && trialStarted)
        {
            DetermineLeftRightControllers();
            selectionEvent = currentState.HandleEvents(controller0Info, controller1Info);   //modified all HandleEvents methods to return "" or the name of an event to be recorded
            recorder.UpdateLists(System.DateTime.Now.Ticks, selectionEvent);                // ticks are 100 nanoseconds
            if (controller0Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad) || controller0Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad))
            {
                Debug.Log("timer stopped");
                endTrialTicks = System.DateTime.Now.Ticks;
                float selectedArea = CalculateSelectedArea();
                recorder.WriteToFile(selectedArea, endTrialTicks - startTrialTicks);
                ChangeTrialScene();
                trialStarted = false;
            }
        }
	}

    private float CalculateSelectedArea(){
        // in every trial scene (except Basic Scene) there is a parent gameObject TestObj 
            // where child 0 is the preselected goal object the participant cannot collide with and 
            // child 1 is the transparent object of the same shape that the participant selects.
        if(!SceneManager.GetActiveScene().name.Equals("Basic Scene")){
            Mesh goal = testObjectParent.transform.GetChild(0).GetComponent<MeshFilter>().mesh;
            Mesh selection = testObjectParent.transform.GetChild(1).GetComponent<MeshFilter>().mesh;
            if (selection.subMeshCount > 0)
            {
                float goalArea = TriangleArea(goal.GetTriangles(1), goal.vertices);
                float selectionArea = TriangleArea(selection.GetTriangles(1), selection.vertices);
                return goalArea - selectionArea;
            } else
            {
                Debug.Log("Participant cube not selected");
            }
        } else{
            Debug.Log("No Object to select in Basic Scene");
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

    void ChangeTrialScene(){
        nextSceneIndex = nextSceneIndex++;
        SceneManager.LoadScene(sceneIndices[nextSceneIndex]);
        StartCoroutine("WaitForTimer");
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
        RecordData.CurrentState = newState;
    }
}
