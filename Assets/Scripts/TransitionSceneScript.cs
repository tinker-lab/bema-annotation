using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TransitionSceneScript : MonoBehaviour {

    public GameObject controller0;
    public GameObject controller1;

    private ControllerInfo controller0Info;
    private ControllerInfo controller1Info;

    List<int> sceneIndices;
    int nextSceneIndex;

    RecordData recorder;

    bool firstUpdate = true;
    bool interfaceChosen = false;
    bool timerStarted = false;
    int selectionIndex;

    GameObject ExperimentController;


    // Use this for initialization
    void Init () {
        //Debug.Log("Intialize the Transition State - controllers are working!!");
        controller0Info = new ControllerInfo(controller0);
        controller1Info = new ControllerInfo(controller1);
        ExperimentController = new GameObject();

        ExperimentController.name = "ExperimentController";
        UnityEngine.Object.DontDestroyOnLoad(ExperimentController);
        //UnityEngine.Object.DontDestroyOnLoad(controller0.gameObject);
        //UnityEngine.Object.DontDestroyOnLoad(controller1.gameObject);

        sceneIndices = new List<int>();
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            sceneIndices.Add(i);
        }

        recorder = new RecordData(controller0Info, controller1Info, sceneIndices.Count);

        nextSceneIndex = recorder.trialID + 1;
        Debug.Log("next scene index: " + nextSceneIndex.ToString());

        firstUpdate = false;
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
        }


        if (!interfaceChosen)
        {
            if (!(recorder == null))
            {
                string stateStr = recorder.GetSelectionState();
                if (!stateStr.Equals(""))
                {
                    if (stateStr.Equals("HandSelectionState"))
                    {
                        Debug.Log("set Yea Big");
                        selectionIndex = 1;
                        interfaceChosen = true;
                    }
                    else if (stateStr.Equals("VolumeCubeSelectionState"))
                    {
                        Debug.Log("set Volume Cube");
                        selectionIndex = 2;
                        interfaceChosen = true;
                    }
                    else if (stateStr.Equals("SliceNSwipeSelectionState"))
                    {
                        Debug.Log("set Slice");
                        selectionIndex = 3;
                        interfaceChosen = true;
                    }
                    else if (stateStr.Equals("RayCastSelectionState"))
                    {
                        Debug.Log("set RayCast");
                        selectionIndex = 4;
                        interfaceChosen = true;
                    }
                }
            }
            else if (Input.GetKeyUp(KeyCode.Alpha1))
            {
                Debug.Log("set Yea Big");
                selectionIndex = 1;
                interfaceChosen = true;
            }
            else if (Input.GetKeyUp(KeyCode.Alpha2))
            {
                Debug.Log("set Volume Cube");
                selectionIndex = 2;
                interfaceChosen = true;
            }
            else if (Input.GetKeyUp(KeyCode.Alpha3))
            {
                Debug.Log("set Slice");
                selectionIndex = 3;
                interfaceChosen = true;
            }
            else if (Input.GetKeyUp(KeyCode.Alpha4))
            {
                Debug.Log("set RayCast");
                selectionIndex = 4;
                interfaceChosen = true;
            }
        }

        if (interfaceChosen && !timerStarted)
        {
            //Debug.Log("Interface has been chosen. Waiting for timer");
            if (controller0Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad) || controller1Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad))
            {
                Debug.Log("HIT BUTTON");
                timerStarted = true;
                RunExperiment currTrial = ExperimentController.AddComponent<RunExperiment>();
                RunExperiment.SceneIndex = nextSceneIndex;
                RunExperiment.Recorder = this.recorder;
                RunExperiment.StateIndex = selectionIndex;

                //currTrial.controller0 = controller0;
                //currTrial.controller1 = controller1;

                Debug.Log("Loading scene " + nextSceneIndex.ToString() + " out of " + SceneManager.sceneCountInBuildSettings.ToString());
                timerStarted = false;
            }
        }
    }
}
