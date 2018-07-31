using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionState
{
    protected string desc
    {
        get;
        set;
    }

    public string Desc
    {
        get { return desc; }
    }

    /* Called by UIController if this state is currently active and another state is about to become active. This can be used to handle deactivating widgets etc.
     */
    public virtual void Deactivate() { }

    /*
     * Called every frame from the UIController update method if this interaction state is currently active. This should interpret the events and update any visual feedback.
     */
    public virtual string HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info) { return ""; }

    public virtual bool CanTransition() { return true; }

}

public class ControllerInfo
{
    public GameObject controller;
    public bool isLeft;
    public SteamVR_Controller.Device device;
    public SteamVR_TrackedObject trackedObj;

    public ControllerInfo (GameObject controller)
    {
        this.controller = controller;
        isLeft = false;
        trackedObj = controller.GetComponent<SteamVR_TrackedObject>();
        device = SteamVR_Controller.Input((int)trackedObj.index);
    }
}


public class UIController : MonoBehaviour {

    private InteractionState currentState;
    public GameObject controller0;
    public GameObject controller1;
    private ControllerInfo controller0Info;
    private ControllerInfo controller1Info;

    private SelectionData selectionData;
    public OutlineManager outlineManager;


    private InteractionState handSelectionState;
    private InteractionState volumeCubeSelectionState;
    private InteractionState sliceNSwipeSelectionState;
    private InteractionState rayCastSelectionState;

    private static int screenshotCount;

    bool firstUpdate = true;

    public InteractionState CurrentState
    {
        get { return currentState; }
    }

	// Use this for initialization
	void Init () {
        Debug.Log("init for UIController");

        screenshotCount = 0;

        controller0Info = new ControllerInfo(controller0);
        controller1Info = new ControllerInfo(controller1);

        selectionData = new SelectionData();
        outlineManager = new OutlineManager();

        volumeCubeSelectionState = new VolumeCubeSelectionState(controller0Info, controller1Info, selectionData);
        //volumeCubeSelectionState.Deactivate();
        sliceNSwipeSelectionState = new SliceNSwipeSelectionState(controller0Info, controller1Info, selectionData);
        sliceNSwipeSelectionState.Deactivate();
        rayCastSelectionState = new RayCastSelectionState(controller0Info, controller1Info, selectionData);
        rayCastSelectionState.Deactivate();

        //handSelectionState = new NavigationState(controller0Info, controller1Info, selectionData);

        //currentState = new PickResourceState(controller0Info);
        //currentState = new NavigationState(controller0Info, controller1Info);
        currentState = volumeCubeSelectionState; //handSelectionState;
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

        //if statement that changes states based on a keyboard press
        //this has been tested to make sure the keys are actually being registered, and they are
        if(Input.GetKeyDown("1"))
        {
            //ChangeState(new NavigationState(controller0Info, controller1Info));
            //ChangeState(handSelectionState);
        }
        else if(Input.GetKeyDown("2"))
        {
            //ChangeState(new VolumeCubeSelectionState(controller0Info, controller1Info));
            ChangeState(volumeCubeSelectionState);
        }
        else if(Input.GetKeyDown("3"))
        {
            //ChangeState(new SliceNSwipeSelectionState(controller0Info, controller1Info));
            ChangeState(sliceNSwipeSelectionState);
        }
        else if(Input.GetKeyDown("4"))
        {
            //ChangeState(new RayCastSelectionState(controller0Info, controller1Info));
            ChangeState(rayCastSelectionState);
        }

        determineLeftRightControllers();
        currentState.HandleEvents(controller0Info, controller1Info);
	}
    
    /*
    void LateUpdate()
    {
        if (controller0.GetComponent<SteamVR_TrackedObject>().index == SteamVR_TrackedObject.EIndex.None || controller1.GetComponent<SteamVR_TrackedObject>().index == SteamVR_TrackedObject.EIndex.None)
        {
            return;
        }

        if (controller0Info.device.GetPressDown(SteamVR_Controller.ButtonMask.Grip) || controller1Info.device.GetPressDown(SteamVR_Controller.ButtonMask.Grip))
        {
            string filename = "bema-screenshot" + screenshotCount + ".png";
            screenshotCount++;
            ScreenCapture.CaptureScreenshot(filename, 8);
            Debug.Log("Saving screenshot: "+filename);
        }
    }
    */

    public void ChangeState(InteractionState newState)
    {
        currentState.Deactivate();
        currentState = newState;
    }

    void determineLeftRightControllers()
    {
        try
        {
            //print("Leftmost device index" + SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost));
            //print("Tracked controller index: " + controller0Info.device.index);

            if ((int) controller0Info.device.index == (SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost)))
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
        catch(System.IndexOutOfRangeException e) {
            print(e.Message);
            controller0Info.isLeft = true;
            controller1Info.isLeft = false;
        }
    }
}
