using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionState : MonoBehaviour
{
    private string name
    {
        get;
        set;
    }

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
        Debug.Log((int)trackedObj.index + " trackedObj");
        device = SteamVR_Controller.Input((int)trackedObj.index);
        
    }
}

public class UIController : MonoBehaviour {

    private InteractionState currentState;
    public GameObject controller0;
    public GameObject controller1;
    private ControllerInfo controller0Info;
    private ControllerInfo controller1Info;

	// Use this for initialization
	void Start () {
        //TODO initialized current state

        SteamVR_TrackedObject trackedObj = controller0.GetComponent<SteamVR_TrackedObject>();
        //print("Tracked Obj index: " + trackedObj.index);


        controller0Info = new ControllerInfo(controller0);
        controller1Info = new ControllerInfo(controller1);

        currentState = new NavigationState();
	}
	
	// Update is called once per frame
	void Update () {

        controller0Info = new ControllerInfo(controller0);
        controller1Info = new ControllerInfo(controller1);

        determineLeftRightControllers();
        currentState.HandleEvents(controller0Info, controller1Info);
	}

    public void changeState(InteractionState newState)
    {
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
