using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PickResourceState : InteractionState
{
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

    private ControllerInfo activeController;
    private GameObject canvas;
    private GameObject mainPanel;
    private Transform headTransform;


    public PickResourceState(ControllerInfo initializingController)
    {
        activeController = initializingController;
        desc = "PickResourceState";
        worldUILayer = LayerMask.NameToLayer("WorldUI");

        laser = Instantiate(Resources.Load<GameObject>("Prefabs/LaserPointer"));
        laserTransform = laser.transform;

        canvas = GameObject.Find("FileSelector").transform.GetChild(0).gameObject;
        //canvas.SetActive(true);

        mainPanel = canvas.transform.Find("MainPanel").gameObject;

        headTransform = GameObject.FindGameObjectWithTag("MainCamera").transform;

        //TODO change the canvas transform so that it appears right in front of you

    }

    public override void deactivate()
    {
        base.deactivate();
        laser.SetActive(false);
        canvas.SetActive(false);
    }

    public override void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        DoRayCast(activeController);

        if (activeController.device.GetHairTriggerUp())
        {
            //TODO: Make images have their own resources tag. Then change the line below to only swap state if you have selected one

            if (hitLayer == worldUILayer && hitObject.CompareTag(PageButtonController.BUTTON_TAG))
            { 
                hitObject.GetComponent<Button>().onClick.Invoke(); 
            }
            else if (hitLayer == worldUILayer && hitObject.CompareTag(LoadImages.RESOURCE_TAG))
            {
                // Carry resource over to next state. Maybe reconfirm with user about their choice?
                GameObject.Find("UIController").GetComponent<UIController>().changeState(new NavigationState());
            }
        }
        ////////////////////////////////////////////////////////////////////////////////////////////
        // NOTE: the rest of this method is temporary. Used to test appropriate distance for UI menu
        ////////////////////////////////////////////////////////////////////////////////////////////
        else if (activeController.device.GetPressDown(SteamVR_Controller.ButtonMask.Touchpad))
        {
            bool activeCanvas = canvas.activeSelf ? false : true;
            canvas.SetActive(activeCanvas);
            PositionCanvas();
        }

    }

    void PositionCanvas()
    {

        Debug.Log("Repositioning canvas");
    
        Vector3 newCanvasPosition = headTransform.position + (new Vector3(headTransform.forward.x, 0, headTransform.forward.z)).normalized * SCALE;

        //canvas.transform.position = newCanvasPosition;
   
     //   canvas.transform.rotation = Quaternion.LookRotation(headTransform.forward, canvas.transform.up);

        Vector3 xAxis = headTransform.right.normalized;
        Vector3 yAxis = new Vector3(0, 1);
        Vector3 zAxis = Vector3.Cross(xAxis, yAxis);
        canvas.transform.rotation = Quaternion.LookRotation(zAxis, yAxis);

        RaycastHit hit;

        if (Physics.Raycast(newCanvasPosition, new Vector3(0, -1, 0), out hit, 100))     // Send a raycast straight down to see if canvas hits anything
        {
            hitPoint = hit.point;
            float heightOffset = 0.5f;
            canvas.transform.position = hit.point + new Vector3(0, mainPanel.GetComponent<RectTransform>().rect.height / 2 + heightOffset , 0);      // Move canvas to make sure it's no longer hitting geometry
        }
    }


    void DoRayCast(ControllerInfo controllerInfo)
    {
        RaycastHit hit;

        laserStartPos = controllerInfo.trackedObj.transform.position;
        if (Physics.Raycast(laserStartPos, controllerInfo.trackedObj.transform.forward, out hit, 1000))
        {
            // No matter what object is hit, show the laser pointing to it
            hitPoint = hit.point;
            hitObject = hit.collider.gameObject;
            hitLayer = hit.collider.gameObject.layer;
            ShowLaser(hit);

            // If hitting a UI element, and it's a button, highlight it
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
        laserTransform.localScale = new Vector3(laserTransform.localScale.x, laserTransform.localScale.y,
            hit.distance);
    }
}
