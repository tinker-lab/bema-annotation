using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


/**
 *  Temporary class to help us debug states                                                                                                                 We're not using this class. The code is out of date so we're commenting it out. Can we just delete this??
 */ 
 /*
public class StartState : InteractionState {

    private readonly float SCALE = 3;
    private int worldUILayer;

    // Laser Pointer stuff
    private GameObject laser;
    private Transform laserTransform;
    private Vector3 laserStartPos;
    private Vector3 hitPoint;
    private GameObject hitObject;
    private int hitLayer;

    private GameObject lastObjectHighlighted;

    private GameObject canvas;
    private GameObject mainPanel;
    private Transform headTransform;

    public StartState()
    {
       
        desc = "StartState";
        worldUILayer = LayerMask.NameToLayer("WorldUI");

        laser = GameObject.Find("LaserParent").transform.GetChild(0).gameObject; ;
        laserTransform = laser.transform;

        canvas = GameObject.Find("StartMenu").transform.GetChild(0).gameObject;
        mainPanel = canvas.transform.Find("MainPanel").gameObject;
        headTransform = GameObject.FindGameObjectWithTag("MainCamera").transform;

        canvas.SetActive(true);
        PositionCanvas();

        GameObject.Find("PlaneCameraParent").transform.GetChild(0).gameObject.SetActive(false);
    }

    public override void Deactivate()
    {
        base.Deactivate();
        laser.SetActive(false);
        canvas.SetActive(false);
    }

    public override void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        DoRayCast(controller0Info, controller1Info);

        if (controller0Info.device.GetHairTriggerUp())
        {
            if (hitLayer == worldUILayer && hitObject.CompareTag(PageButtonController.BUTTON_TAG))
            {
                if (hitObject.name == "NavigationState")
                {
                    GameObject.Find("UIController").GetComponent<UIController>().ChangeState(new NavigationState(controller0Info, controller1Info));
                }
                else if (hitObject.name == "PickResourceState")
                {
                    GameObject.Find("UIController").GetComponent<UIController>().ChangeState(new PickResourceState(controller1Info));
                }
                else if (hitObject.name == "PlaneState")  // TODO: change the name here to reflect the new state
                {
                    GameObject.Find("UIController").GetComponent<UIController>().ChangeState(new HandSelectionState(controller0Info, controller1Info, new NavigationState(controller0Info, controller1Info)));
                }
                else
                {
                    Debug.Log("Couldn't find state button");
                }
            }
            //Debug.Log(hitObject.name);
            //Debug.Log("Trigger pulled");
        }
    }

    void PositionCanvas()
    {
        Vector3 newCanvasPosition = headTransform.position + (new Vector3(headTransform.forward.x, 0, headTransform.forward.z)).normalized * SCALE;

        Vector3 xAxis = headTransform.right.normalized;
        Vector3 yAxis = new Vector3(0, 1);
        Vector3 zAxis = Vector3.Cross(xAxis, yAxis);
        canvas.transform.rotation = Quaternion.LookRotation(zAxis, yAxis);

        RaycastHit hit;

        if (Physics.Raycast(newCanvasPosition, new Vector3(0, -1, 0), out hit, 2))     // Send a raycast straight down to see if canvas hits anything
        {
            //hitPoint0 = hit.point;
            float heightOffset = 0.5f;
            canvas.transform.position = hit.point + new Vector3(0, mainPanel.GetComponent<RectTransform>().rect.height / 2 + heightOffset, 0);      // Move canvas to make sure it's no longer hitting geometry
        }
    }


    void DoRayCast(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        RaycastHit hit;

        laserStartPos = controller0Info.trackedObj.transform.position;
        if (Physics.Raycast(laserStartPos, controller0Info.trackedObj.transform.forward, out hit, 1000))
        {
            // No matter what object is hit, show the laser pointing to it
            hitPoint = hit.point;
            hitObject = hit.collider.gameObject;
            hitLayer = hit.collider.gameObject.layer;
            ShowLaser(hit);

            // If hitting a UI element, and it's a button or resource, highlight it
            if (hitLayer == worldUILayer && (hitObject.CompareTag(PageButtonController.BUTTON_TAG) || hitObject.CompareTag(LoadImages.RESOURCE_TAG)))
            {
                if (lastObjectHighlighted != null && !lastObjectHighlighted.Equals(hitObject))
                {
                    lastObjectHighlighted.GetComponent<Button>().image.GetComponent<Outline>().enabled = false;
                }

                hitObject.GetComponent<Button>().image.GetComponent<Outline>().enabled = true;
                lastObjectHighlighted = hitObject;

            }
        }
        else
        {
            laser.SetActive(false);
        }
    }

    private void ShowLaser(RaycastHit hit)
    {
        // 1
        laser.SetActive(true);
        // 2
        laserTransform.position = Vector3.Lerp(laserStartPos, hitPoint, .5f);
        // 3
        laserTransform.LookAt(hitPoint);
        // 4
        laserTransform.localScale = new Vector3(laserTransform.localScale.x, laserTransform.localScale.y, hit.distance);
    }

    public GameObject getLaser0()
    {
        return laser;
    }
}

*/