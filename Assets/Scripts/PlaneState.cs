using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaneState : InteractionState {

    //planes
    private GameObject newPlane;
    private int planeCounter;
    private Material m;
    private bool snapH, snapV;

    private int worldUILayer;

    public PlaneState()
    {
        desc = "PlaneState";
        
        planeCounter = 0; //TODO: is this reset every time the state is entered? 
        snapH = snapV = false;
        

        worldUILayer = LayerMask.NameToLayer("WorldUI");

        m = Resources.Load("Plane Material") as Material;
        InitPlane();
    }

    public override void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {

        ChangePlane(controller0Info, controller1Info);
            
        if (controller0Info.device.GetHairTriggerUp() || controller1Info.device.GetHairTriggerUp())
        {
            GameObject.Find("PlaneCameraParent").transform.GetChild(0).gameObject.SetActive(false);
            //TODO: set viewing plane inactive as well


            // GameObject.Find("UIController").GetComponent<UIController>().changeState(new NavigationState());
            GameObject.Find("UIController").GetComponent<UIController>().changeState(new StartState());
        }
        
    }

    private void InitPlane()
    {
        // create plane
        newPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        newPlane.name = "Plane" + planeCounter;
        planeCounter += 1;

        // cannot teleport to created plane
        newPlane.layer = worldUILayer;

        newPlane.GetComponent<Renderer>().material = m;
        snapH = snapV = false;

        Camera camera = GameObject.Find("PlaneCameraParent").transform.GetChild(0).GetComponent<Camera>();
        camera.gameObject.SetActive(true);
        
        camera.GetComponent<PlaneCameraController>().setPlane(newPlane);
    }

    private void ChangePlane(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        // position plane at midpoint between controllers

        Vector3 leftPosition = controller0Info.isLeft ? controller0Info.trackedObj.transform.position : controller1Info.trackedObj.transform.position;
        Vector3 rightPosition = controller0Info.isLeft ? controller1Info.trackedObj.transform.position : controller0Info.trackedObj.transform.position;

        newPlane.transform.position = Vector3.Lerp(leftPosition, rightPosition, 0.5f);

        // rotate plane w/ respect to both controllers
        RotatePlane(controller0Info, controller1Info, leftPosition, rightPosition, newPlane);

        // scale plane
        newPlane.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f) * Vector3.Distance(rightPosition, leftPosition); // square right now

        // TASKS
        // scale in front of you (keep square shape? non-linear scaling?)
        // make plane snap to boundaries

    }

    private void RotatePlane(ControllerInfo controller0Info, ControllerInfo controller1Info, Vector3 leftPos, Vector3 rightPos, GameObject nPlane)
    {
        Vector3 xAxis = (rightPos - leftPos).normalized;

        Vector3 zAxis = controller0Info.isLeft ? controller1Info.trackedObj.transform.forward : controller0Info.trackedObj.transform.forward;
        zAxis = (zAxis - (Vector3.Dot(zAxis, xAxis) * xAxis)).normalized;
        Vector3 yAxis = Vector3.Cross(zAxis, xAxis).normalized;

        Vector3 groundY = new Vector3(0, 1);

        float controllerToGroundY = Vector3.Angle(yAxis, groundY);
       // print(snapH);

        if (controllerToGroundY < 25 || controllerToGroundY > 155)
        {
            if (!snapH)  // HORIZONTAL
            {
                nPlane.transform.rotation = Quaternion.AngleAxis(controllerToGroundY, Vector3.Cross(yAxis, groundY)) * Quaternion.LookRotation(zAxis, yAxis);
                //print("snapped to H" + nPlane.transform.rotation.eulerAngles.ToString());
                snapH = true;
            }

        }
        else if (controllerToGroundY > 65 && controllerToGroundY < 115)
        {
            if (!snapV) // VERTICAL
            {
                nPlane.transform.rotation = Quaternion.AngleAxis(-(90 - controllerToGroundY), Vector3.Cross(yAxis, groundY)) * Quaternion.LookRotation(zAxis, yAxis);
                snapV = true;
            }
        }

        // normal transformation
        else
        {
            nPlane.transform.rotation = Quaternion.LookRotation(zAxis, yAxis);
            snapH = snapV = false;
            //print("not snapped");
            //print("controller " + controllerToGroundY.ToString());
        }
    }
}
