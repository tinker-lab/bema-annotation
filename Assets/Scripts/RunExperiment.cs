using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunExperiment : MonoBehaviour {

    RecordData recorder;

    private InteractionState currentState;
    public GameObject controller0;
    public GameObject controller1;
    private ControllerInfo controller0Info;
    private ControllerInfo controller1Info;

    private SelectionData selectionData;
    public OutlineManager outlineManager;

    bool firstUpdate = true;
    bool trialStarted = false;

    long startTrialTicks;
    long endTrialTicks;

    List<int> sceneIndices;
    int nextSceneIndex;

	// Use this for initialization
	void Init () {
        controller0Info = new ControllerInfo(controller0);
        controller1Info = new ControllerInfo(controller1);

        selectionData = new SelectionData();
        outlineManager = new OutlineManager();

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++){
            sceneIndices.Add(i);
        }
        nextSceneIndex = 0;                 //????????? we need to figure out how we want to randomize these.

        StartCoroutine("SelectInterface");
        StartCoroutine("WaitForTimer");

        recorder = new RecordData(controller0Info, controller1Info, currentState);

        //init landing zone, scene changer
		//into between state where you start timer by pressing a button -> "landing zone"
        //landing zone starts measurements and the scene. landing zone could be in this class??
	}

    IEnumerator WaitForTimer(){
        while(!trialStarted){
            if(controller0Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad) || controller0Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad)){
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
            determineLeftRightControllers();
            currentState.HandleEvents(controller0Info, controller1Info);
            //TODO: make all HandleEvents calls return an event name or empty string
            recorder.UpdateLists(System.DateTime.Now.Ticks, eventName);
            if (controller0Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad) || controller0Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad))
            {
                endTrialTicks = System.DateTime.Now.Ticks;
                recorder.WriteToFile(selectedArea, endTrialTicks - startTrialTicks);
                ChangeTrialScene();
                trialStarted = false;
            }
            //TODO: Get selectedArea
        }
	}

    void ChangeTrialScene(){
        nextSceneIndex = nextSceneIndex++;
        SceneManager.LoadScene(sceneIndices[nextSceneIndex]);
        StartCoroutine("WaitForTimer");
    }

    void determineLeftRightControllers()
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
