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
    List<int> trainingIndices;
    int nextSceneIndex;

    RecordData recorder;

    bool firstUpdate = true;
    bool interfaceChosen = false;
    bool timerStarted = false;
    int selectionIndex;
    bool zeroDominant = true;
    bool training = true;
    int trainingSceneCount = 0;
    bool allowProgress = true;

    GameObject ExperimentController;

    // Use this for initialization
    void Init () {
        Debug.Log("Intialize the Transition State - controllers are working!!");
        controller0Info = new ControllerInfo(controller0);
        controller1Info = new ControllerInfo(controller1);

        controller0Info.controller.gameObject.transform.GetChild(0).gameObject.SetActive(false);                    //turn off 0 controller
        controller0Info.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = true; //defaults controller 0 to be rendered as a hand

        ExperimentController = new GameObject();

        ExperimentController.name = "ExperimentController";
        //UnityEngine.Object.DontDestroyOnLoad(ExperimentController);
        //UnityEngine.Object.DontDestroyOnLoad(controller0.gameObject);
        //UnityEngine.Object.DontDestroyOnLoad(controller1.gameObject);

        sceneIndices = new List<int>();
        trainingIndices = new List<int>();
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 1; i < sceneCount-3; i++)
        {
            sceneIndices.Add(i);
            //Debug.Log(i);
        }
        

        for (int j = sceneCount-2; j < sceneCount; j++)
        {
            trainingIndices.Add(j);
            //Debug.Log("training: " + i);
        }

        ShuffleScenes();                    // scenesIndices is a list of ints that correspond to the indices of each scene in Unity Build Settings. They're shuffled up.
        sceneIndices.Add(sceneCount - 3);   // real world example scene is always last

        string s = "";
        foreach(int i in sceneIndices)
        {
            s += i;
        }
        s += " Training: ";
        foreach(int i in trainingIndices)
        {
            s += i;
        }
        //Debug.Log(s);

        recorder = new RecordData(controller0Info, controller1Info, sceneCount);

        nextSceneIndex = 0;         // nextSceneIndex increments linearly every time a scene is unloaded. Used to access the shuffled sceneIndices list.
        //Debug.Log("next scene index: " + sceneIndices[nextSceneIndex].ToString());

        if(!training)
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }
        else
        {
            SceneManager.sceneUnloaded += OnSceneUnloadedTraining;
        }

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

        if (Input.GetKeyUp(KeyCode.S))
        {
            zeroDominant = !zeroDominant;
            if (zeroDominant)
            {
                controller0Info.controller.gameObject.transform.GetChild(0).gameObject.SetActive(false);                    //turn off 0 controller
                controller0Info.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = true;    //turn on 0 hand

                controller1Info.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;   //turn off 1 hand
                controller1Info.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true);                     //turn on 1 controller
            }
            else
            {
                controller0Info.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true);                    //turn on 0 controller
                controller0Info.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;    //turn off 0 hand

                controller1Info.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = true;   //turn on 1 hand
                controller1Info.controller.gameObject.transform.GetChild(0).gameObject.SetActive(false);                     //turn off 1 controller
            }
        }

        if (Input.GetKeyUp(KeyCode.T))
        {
            training = !training;
            if (!training)
            {
                Debug.Log("~~~~Training Off");
                SceneManager.sceneUnloaded -= OnSceneUnloadedTraining;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
            }
            else
            {
                Debug.Log("~~~~Training On");
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
                SceneManager.sceneUnloaded += OnSceneUnloadedTraining;
            }

        }

        if(!allowProgress && Input.GetKeyUp(KeyCode.Space))
        {
            allowProgress = true;
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

        if (interfaceChosen && !timerStarted && allowProgress)
        {
            //Debug.Log("Interface has been chosen. Waiting for timer");
            if (controller0Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad) || controller1Info.device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad))
            {
                Debug.Log("HIT BUTTON");
                timerStarted = true;

                if (zeroDominant)
                {
                    controller0Info.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true);                    //turn on 0 controller
                    controller0Info.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;    //turn off 0 hand
                }
                else
                {
                    controller1Info.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;   //turn off 1 hand
                    controller1Info.controller.gameObject.transform.GetChild(0).gameObject.SetActive(true);                     //turn on 1 controller
                }

                GameObject.Find("Sphere").GetComponent<MeshRenderer>().enabled = false;             //turn off orb

                if(!(ExperimentController.GetComponent<RunExperiment>() == null))
                {
                    UnityEngine.Object.DestroyImmediate(ExperimentController.GetComponent<RunExperiment>());
                }

                ExperimentController.AddComponent<RunExperiment>();

                int loadScene;
                if (training)
                {
                    loadScene = trainingIndices[nextSceneIndex];
                    //Debug.Log("loading training scene");
                }
                else
                {
                    loadScene = sceneIndices[nextSceneIndex];
                    //Debug.Log("loading experiment scene");
                }

                RunExperiment.SceneIndex = loadScene;       ///somehow save nextSceneIndex to recorder
                RunExperiment.SceneOrder = nextSceneIndex;
                RunExperiment.Recorder = this.recorder;
                RunExperiment.StateIndex = selectionIndex;
                RunExperiment.Transition = this;
                RunExperiment.ZeroDominant = zeroDominant;

                //currTrial.controller0 = controller0;
                //currTrial.controller1 = controller1;

                Debug.Log("Loading scene " + loadScene.ToString() + " out of " + SceneManager.sceneCountInBuildSettings.ToString());
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
        string sceneOrder = "";
        while (n > 1) 
        {  
            n--;  
            int k = rnd.Next(0,n);  
            int value = sceneIndices[k];  
            sceneIndices[k] = sceneIndices[n];  
            sceneIndices[n] = value;
            sceneOrder += value.ToString() + " ";
        }
        //Debug.Log(sceneOrder);
    }

    //listen for sceneUnload at nextSceneIndex. then increment nextSceneIndex and set timerStarted = false;
    private void OnSceneUnloaded(Scene current)
    {
        
        recorder.WriteToFile();
        timerStarted = false;
        GameObject.Find("Sphere").GetComponent<MeshRenderer>().enabled = true;  //turn on orb
        //if (nextSceneIndex >= 0)
        //{
        //    Debug.Log("scene " + sceneIndices[nextSceneIndex] + " accuracy: " + recorder.GetSelectedPercentage().ToString());
        //}
        //else
        //{
        //    Debug.Log("scene " + 0 + " accuracy: " + recorder.GetSelectedPercentage().ToString());
        //}
        nextSceneIndex++;

        if(nextSceneIndex == sceneIndices.Count - 1)
        {
            allowProgress = false;
        }
        if (nextSceneIndex >= sceneIndices.Count)
        {
            Debug.Break();
        }
    }

    private void OnSceneUnloadedTraining(Scene current)
    {
        nextSceneIndex++;
        recorder.WriteToFile();
        timerStarted = false;
        GameObject.Find("Sphere").GetComponent<MeshRenderer>().enabled = true;  //turn on orb
        trainingSceneCount++;

        double percent = recorder.GetSelectedPercentage();
        //Debug.Log("training " + trainingSceneCount + " accuracy: " + percent.ToString());
        if (percent < 50f && percent > -50f && trainingSceneCount >= 4)
        {
            training = false;
            allowProgress = false;
            Debug.Log("~~~~Training OFF");
            SceneManager.sceneUnloaded -= OnSceneUnloadedTraining;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            nextSceneIndex = 0;
            return;
        }

        if (nextSceneIndex >= trainingIndices.Count)
        {
            nextSceneIndex = 0;
        }
    }

    public void ReloadScene()
    {
        nextSceneIndex--;
        if (training)
        {
            trainingSceneCount--;
        }
        UnityEngine.Object.DestroyImmediate(ExperimentController.GetComponent<RunExperiment>());
    }
}
