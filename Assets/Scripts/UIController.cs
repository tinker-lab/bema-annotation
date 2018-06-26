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
    public virtual void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info) { }

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

    private static int screenshotCount;

    bool firstUpdate = true;

    public InteractionState CurrentState
    {
        get { return currentState; }
    }

	// Use this for initialization
	void Init () {

        controller0Info = new ControllerInfo(controller0);
        controller1Info = new ControllerInfo(controller1);

        screenshotCount = 0;

        //currentState = new PickResourceState(controller0Info);
        //currentState = new NavigationState(controller0Info, controller1Info);
        currentState = new SliceNSwipeSelectionState(controller0Info, controller1Info);
        //currentState = new VolumeCubeSelectionState(controller0Info, controller1Info);
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
