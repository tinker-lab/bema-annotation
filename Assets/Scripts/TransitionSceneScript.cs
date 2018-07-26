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
        Debug.Log("Intialize the Transition State - controllers are working!!");
        controller0Info = new ControllerInfo(controller0);
        controller1Info = new ControllerInfo(controller1);
        ExperimentController = new GameObject();

        ExperimentController.name = "ExperimentController";
        //UnityEngine.Object.DontDestroyOnLoad(ExperimentController);
        //UnityEngine.Object.DontDestroyOnLoad(controller0.gameObject);
        //UnityEngine.Object.DontDestroyOnLoad(controller1.gameObject);

        sceneIndices = new List<int>();
        for (int i = 1; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            sceneIndices.Add(i);
        }

        ShuffleScenes();            // scenesIndices is a list of ints that correspond to the indices of each scene in Unity Build Settings. They're shuffled up.

        recorder = new RecordData(controller0Info, controller1Info, sceneIndices.Count+1);

        nextSceneIndex = 1;         // nextSceneIndex increments linearly every time a scene is unloaded. Used to access the shuffled sceneIndices list.
        Debug.Log("next scene index: " + sceneIndices[nextSceneIndex].ToString());

        SceneManager.sceneUnloaded += OnSceneUnloaded;

        firstUpdate = false;
    }
	
	// Update is called once per frame
	void Update () {
      //  Debug.Log("transition script");
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
            if (Input.GetKeyUp(KeyCode.Alpha1))
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

                
                if(!(ExperimentController.GetComponent<RunExperiment>() == null))
                {
                    UnityEngine.Object.DestroyImmediate(ExperimentController.GetComponent<RunExperiment>());
                }
                ExperimentController.AddComponent<RunExperiment>();
               
                RunExperiment.SceneIndex = sceneIndices[nextSceneIndex];
                RunExperiment.Recorder = this.recorder;
                RunExperiment.StateIndex = selectionIndex;

                //currTrial.controller0 = controller0;
                //currTrial.controller1 = controller1;

                Debug.Log("Loading scene " + sceneIndices[nextSceneIndex].ToString() + " out of " + SceneManager.sceneCountInBuildSettings.ToString());
            }
        }
        
    }

    /* implementation of fisher-yates shuffle algorithm from 
    //  https://stackoverflow.com/questions/5383498/shuffle-rearrange-randomly-a-liststring 
    //  https://stackoverflow.com/revisions/1262619/1
    */
    private void ShuffleScenes()
    {
        System.Random rnd = new System.Random();  
        int n = sceneIndices.Count;  
        while (n > 1) 
        {  
            n--;  
            int k = rnd.Next(0,n);  
            int value = sceneIndices[k];  
            sceneIndices[k] = sceneIndices[n];  
            sceneIndices[n] = value;  
        }          
    }

    //listen for sceneUnload at nextSceneIndex. then increment nextSceneIndex and set timerStarted = false;
    private void OnSceneUnloaded(Scene current)
    {
        nextSceneIndex++;
        recorder.WriteToFile();
        timerStarted = false;

        if(nextSceneIndex >= sceneIndices.Count)
        {
            Debug.Break();
        }
    }
}
