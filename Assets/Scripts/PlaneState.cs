using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlaneState : InteractionState {

    private readonly float VIEWPLANE_SCALE = 0.2f;

    //planes
    private GameObject newPlane;
    private int planeCounter;
    private Material m;

    private int worldUILayer;
    private Vector3 idealPosition;

    public PlaneState()
    {
        desc = "PlaneState";
        
        planeCounter = 0; //TODO: this is reset every time the state is entered
       
        worldUILayer = LayerMask.NameToLayer("WorldUI");

        m = Resources.Load("Plane Material") as Material;
        InitPlane();

    }

    public override void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {

        ChangePlane(controller0Info, controller1Info);
        updateView();

        if (controller0Info.device.GetHairTriggerUp() || controller1Info.device.GetHairTriggerUp())
        {
            GameObject.Find("PlaneCameraParent").transform.GetChild(0).gameObject.SetActive(false);
            //TODO: set viewing plane inactive as well

            GameObject.Find("UIController").GetComponent<UIController>().changeState(new NavigationState());
        }
    }

    private void updateView()
    {
        GameObject viewPlane = GameObject.Find("ViewPlane");
        Transform headset = GameObject.FindGameObjectWithTag("MainCamera").transform;
        
        bool hasMovedAway = Mathf.Approximately((viewPlane.transform.position - idealPosition).magnitude, 0.0f);

        viewPlane.transform.rotation = headset.rotation;
        viewPlane.transform.localRotation = Quaternion.LookRotation(-headset.up, -headset.forward);

        Vector3 prevIdealPosition = idealPosition;
        idealPosition = headset.position - (viewPlane.transform.up.normalized * 0.5f);
       // viewPlane.transform.Rotate(headset.right, -90f);
        //viewPlane.transform.Rotate(90, 0, 0);


        RaycastHit hit;

        List<Vector3> positions = new List<Vector3>();
        positions.Add(headset.position);
        positions.Add(headset.position - 10 * viewPlane.transform.localScale.x * headset.up - 10 * viewPlane.transform.localScale.x * headset.right); // - right, +up
        positions.Add(headset.position - 10 * viewPlane.transform.localScale.x * headset.up + 10 * viewPlane.transform.localScale.x * headset.right);
        positions.Add(headset.position + 10 * viewPlane.transform.localScale.x * headset.up - 10 * viewPlane.transform.localScale.x * headset.right);
        positions.Add(headset.position + 10 * viewPlane.transform.localScale.x * headset.up + 10 * viewPlane.transform.localScale.x * headset.right);

        Vector3 closestPosition = idealPosition;
        float closestHitDistance = float.MaxValue;
        float distanceToCast = (idealPosition - headset.position).magnitude;

        for (int i = 0; i < positions.Count; i++)
        {
            if (Physics.Raycast(positions.ElementAt(i), headset.forward, out hit, distanceToCast))
            {
                Vector3 headsetToHit = hit.point - headset.position;

                Vector3 potentialPosition = headset.position + Vector3.Dot(headsetToHit, headset.forward) * headset.forward;
                float distance = Vector3.Distance(positions.ElementAt(0), potentialPosition);
                if (distance < closestHitDistance)
                {
                    closestHitDistance = distance;
                    closestPosition = potentialPosition;
                }
            }
        }
        
        viewPlane.transform.position = closestPosition;
        


        /*
        if (Physics.Raycast(headset.position, headset.forward, out hit0, (idealPosition - headset.position).magnitude))
        {
            viewPlane.transform.position = hit0.point;
        }
        else
        {
            viewPlane.transform.position = idealPosition;
        }
        */
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

        Camera camera = GameObject.Find("PlaneCameraParent").transform.GetChild(0).GetComponent<Camera>();
        camera.gameObject.SetActive(true);
        
        camera.GetComponent<PlaneCameraController>().setPlane(newPlane);

        //TODO: initialize the viewplane position and idealposition
    }

    private void ChangePlane(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        // position plane at midpoint between controllers

        Vector3 leftPosition = controller0Info.isLeft ? controller0Info.trackedObj.transform.position : controller1Info.trackedObj.transform.position;
        Vector3 rightPosition = controller0Info.isLeft ? controller1Info.trackedObj.transform.position : controller0Info.trackedObj.transform.position;

        Vector3 halfWayBtwHands = Vector3.Lerp(leftPosition, rightPosition, 0.5f);
        Vector3 headPos = GameObject.FindGameObjectWithTag("MainCamera").transform.position;
        Vector3 offsetDirection = (halfWayBtwHands - headPos).normalized;


        newPlane.transform.position = halfWayBtwHands + 0.25f * offsetDirection;
        //newPlane.transform.Translate(0,-0.5f, 0.15f, newPlane.transform); // adjust where plane is relative to controllers

        // rotate plane w/ respect to both controllers
        RotatePlane(controller0Info, controller1Info, leftPosition, rightPosition, newPlane);

        // scale plane
        float distance = Vector3.Distance(rightPosition, leftPosition);
        float max = 0.5f;

        if (distance >= max)
        {
            /*
            float s = (float)(((0.5)*(distance - max)) * ((0.5) * (distance - max)) + max); // 0.5 comes from 1/b where b =2
            s = s / 10;
            newPlane.transform.localScale = new Vector3(s, s, s)* distance;
            */

            newPlane.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f) * ((distance / 2) + (distance - max));
        }
        else
        {
            newPlane.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f) * distance; // square
        }

    }

    private void RotatePlane(ControllerInfo controller0Info, ControllerInfo controller1Info, Vector3 leftPos, Vector3 rightPos, GameObject nPlane)
    {
        Vector3 xAxis = (rightPos - leftPos).normalized;

        Vector3 zAxis = controller0Info.isLeft ? controller1Info.trackedObj.transform.forward : controller0Info.trackedObj.transform.forward;
        zAxis = (zAxis - (Vector3.Dot(zAxis, xAxis) * xAxis)).normalized;
        Vector3 yAxis = Vector3.Cross(zAxis, xAxis).normalized;

        Vector3 groundY = new Vector3(0, 1);

        float controllerToGroundY = Vector3.Angle(yAxis, groundY);

        if (controllerToGroundY < 25 || controllerToGroundY > 155) // HORIZONTAL
        {
                nPlane.transform.rotation = Quaternion.AngleAxis(controllerToGroundY, Vector3.Cross(yAxis, groundY)) * Quaternion.LookRotation(zAxis, yAxis);
        }
        else if (controllerToGroundY > 65 && controllerToGroundY < 115) // VERTICAL
        {
                nPlane.transform.rotation = Quaternion.AngleAxis(-(90 - controllerToGroundY), Vector3.Cross(yAxis, groundY)) * Quaternion.LookRotation(zAxis, yAxis);
        }

        // normal transformation
        else
        {
            nPlane.transform.rotation = Quaternion.LookRotation(zAxis, yAxis);
        }
    }
}
