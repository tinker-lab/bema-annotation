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

    RunExperiment currTrial;


    // Use this for initialization
    void Init () {
        controller0Info = new ControllerInfo(controller0);
        controller1Info = new ControllerInfo(controller1);

        sceneIndices = new List<int>();
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            sceneIndices.Add(i);
        }

        recorder = new RecordData(controller0Info, controller1Info, sceneIndices.Count);

        nextSceneIndex = recorder.trialID + 1;

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
                        Debug.Log("init Yea Big");
                        selectionIndex = 1;
                        interfaceChosen = true;
                    }
                    else if (stateStr.Equals("VolumeCubeSelectionState"))
                    {
                        Debug.Log("init Volume Cube");
                        selectionIndex = 2;
                        interfaceChosen = true;
                    }
                    else if (stateStr.Equals("SliceNSwipeSelectionState"))
                    {
                        Debug.Log("init Slice");
                        selectionIndex = 3;
                        interfaceChosen = true;
                    }
                    else if (stateStr.Equals("RayCastSelectionState"))
                    {
                        Debug.Log("init RayCast");
                        selectionIndex = 4;
                        interfaceChosen = true;
                    }
                }
            }
            else if (Input.GetKeyUp(KeyCode.Alpha1))
            {
                Debug.Log("init Yea Big");
                selectionIndex = 1;
                interfaceChosen = true;
            }
            else if (Input.GetKeyUp(KeyCode.Alpha2))
            {
                Debug.Log("init Volume Cube");
                selectionIndex = 2;
                interfaceChosen = true;
            }
            else if (Input.GetKeyUp(KeyCode.Alpha3))
            {
                Debug.Log("init Slice");
                selectionIndex = 3;
                interfaceChosen = true;
            }
            else if (Input.GetKeyUp(KeyCode.Alpha4))
            {
                Debug.Log("init RayCast");
                selectionIndex = 4;
                interfaceChosen = true;
            }
        }

        if (interfaceChosen && !timerStarted)
        {
            if (controller0Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad) || controller1Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad))
            {
                timerStarted = true;
                currTrial = new RunExperiment(controller0Info, controller1Info, recorder, selectionIndex, nextSceneIndex);
                SceneManager.LoadSceneAsync(nextSceneIndex);
            }
        }
    }
}
