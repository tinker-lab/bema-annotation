using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


/**
 *  Temporary class to help us debug states
 */ 
public class StartState : InteractionState {

    private readonly float SCALE = 3;
    private int worldUILayer;

    // Laser Pointer stuff
    private GameObject laser0;
    private Transform laser0Transform;
    private Vector3 laser0StartPos;
    private Vector3 hitPoint0;
    private GameObject hitObject0;
    private int hitLayer0;

    private GameObject laser1;
    private Transform laser1Transform;
    private Vector3 laser1StartPos;
    private Vector3 hitPoint1;
    private GameObject hitObject1;
    private int hitLayer1;

    private GameObject lastObjectHighlighted;

    private GameObject canvas;
    private GameObject mainPanel;
    private Transform headTransform;


    public StartState()
    {
       
        desc = "StartState";
        worldUILayer = LayerMask.NameToLayer("WorldUI");

        laser0 = Instantiate(Resources.Load<GameObject>("Prefabs/LaserPointer"));
        laser0Transform = laser0.transform;
        laser1 = Instantiate(Resources.Load<GameObject>("Prefabs/LaserPointer"));
        laser1Transform = laser0.transform;

        canvas = GameObject.Find("StartMenu").transform.GetChild(0).gameObject;
        mainPanel = canvas.transform.Find("MainPanel").gameObject;
        headTransform = GameObject.FindGameObjectWithTag("MainCamera").transform;

        canvas.SetActive(true);
        PositionCanvas();

        GameObject.Find("PlaneCameraParent").transform.GetChild(0).gameObject.SetActive(false);
    }

    public override void deactivate()
    {
        base.deactivate();
        laser0.SetActive(false);
        laser1.SetActive(false);
        canvas.SetActive(false);
    }

    public override void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        DoRayCast(controller0Info, controller1Info);
        //DoRayCast(controller1Info, false);


        if (controller0Info.device.GetHairTriggerUp())
        {

            if (hitLayer0 == worldUILayer && hitObject0.CompareTag(PageButtonController.BUTTON_TAG))
            {
                if (hitObject0.name == "NavigationState")
                {
                    GameObject.Find("UIController").GetComponent<UIController>().changeState(new NavigationState());
                }
                else if (hitObject0.name == "PickResourceState")
                {
                    GameObject.Find("UIController").GetComponent<UIController>().changeState(new PickResourceState(controller1Info));
                }
                else if (hitObject0.name == "PlaneState")
                {
                    GameObject.Find("UIController").GetComponent<UIController>().changeState(new PlaneState());
                }
                else
                {
                    Debug.Log("Couldn't find state button");
                }
            }
        }
        else if (controller1Info.device.GetHairTriggerUp())
        { 

            if (hitLayer1 == worldUILayer && hitObject1.CompareTag(PageButtonController.BUTTON_TAG))
            {
                if (hitObject1.name == "NavigationState")
                {
                    GameObject.Find("UIController").GetComponent<UIController>().changeState(new NavigationState());
                }
                else if (hitObject1.name == "PickResourceState")
                {
                    GameObject.Find("UIController").GetComponent<UIController>().changeState(new PickResourceState(controller1Info));
                }
                else if (hitObject1.name == "PlaneState")
                {
                    GameObject.Find("UIController").GetComponent<UIController>().changeState(new PlaneState());
                }
                else
                {
                    Debug.Log("Couldn't find state button");
                }
            }
        }
    }

    void PositionCanvas()
    {

        //Debug.Log("Repositioning canvas");

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
            //hitPoint0 = hit.point;
            float heightOffset = 0.5f;
            canvas.transform.position = hit.point + new Vector3(0, mainPanel.GetComponent<RectTransform>().rect.height / 2 + heightOffset, 0);      // Move canvas to make sure it's no longer hitting geometry
        }
    }


    void DoRayCast(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        RaycastHit hit0;
        RaycastHit hit1;

        laser0StartPos = controller0Info.trackedObj.transform.position;
        laser1StartPos = controller1Info.trackedObj.transform.position;
        if (Physics.Raycast(laser0StartPos, controller0Info.trackedObj.transform.forward, out hit0, 1000))
        {
            // No matter what object is hit, show the laser pointing to it
            hitPoint0 = hit0.point;
            hitObject0 = hit0.collider.gameObject;
            hitLayer0 = hit0.collider.gameObject.layer;
            ShowLaser(hit0, true);

            // If hitting a UI element, and it's a button or resource, highlight it
            if (hitLayer0 == worldUILayer && (hitObject0.CompareTag(PageButtonController.BUTTON_TAG) || hitObject0.CompareTag(LoadImages.RESOURCE_TAG)))
            {
                if (lastObjectHighlighted != null && !lastObjectHighlighted.Equals(hitObject0))
                {
                    lastObjectHighlighted.GetComponent<Button>().image.GetComponent<Outline>().enabled = false;
                }

                hitObject0.GetComponent<Button>().image.GetComponent<Outline>().enabled = true;
                lastObjectHighlighted = hitObject0;

            }
        }
        else if (Physics.Raycast(laser1StartPos, controller1Info.trackedObj.transform.forward, out hit1, 1000))
        {
            // No matter what object is hit, show the laser pointing to it
            hitPoint1 = hit1.point;
            hitObject1 = hit1.collider.gameObject;
            hitLayer1 = hit1.collider.gameObject.layer;
            ShowLaser(hit1, false);

            // If hitting a UI element, and it's a button or resource, highlight it
            if (hitLayer1 == worldUILayer && (hitObject1.CompareTag(PageButtonController.BUTTON_TAG) || hitObject1.CompareTag(LoadImages.RESOURCE_TAG)))
            {
                if (lastObjectHighlighted != null && !lastObjectHighlighted.Equals(hitObject1))
                {
                    lastObjectHighlighted.GetComponent<Button>().image.GetComponent<Outline>().enabled = false;
                }

                hitObject1.GetComponent<Button>().image.GetComponent<Outline>().enabled = true;
                lastObjectHighlighted = hitObject1;

            }
        }
        else
        {
           laser0.SetActive(false);
           laser1.SetActive(false);
        }
    }

    private void ShowLaser(RaycastHit hit, bool isController0)
    {
        if (isController0)
        {
            // 1
            laser0.SetActive(true);
            // 2
            laser0Transform.position = Vector3.Lerp(laser0StartPos, hitPoint0, .5f);
            // 3
            laser0Transform.LookAt(hitPoint0);
            // 4
            laser0Transform.localScale = new Vector3(laser0Transform.localScale.x, laser0Transform.localScale.y,
                hit.distance);
        }
        else
        {
            // 1
            laser1.SetActive(true);
            // 2
            laser1Transform.position = Vector3.Lerp(laser1StartPos, hitPoint1, .5f);
            // 3
            laser1Transform.LookAt(hitPoint1);
            // 4
            laser1Transform.localScale = new Vector3(laser1Transform.localScale.x, laser1Transform.localScale.y,
                hit.distance);
        }
    }
}