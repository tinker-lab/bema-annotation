using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NavigationState : InteractionState {

    public static readonly float MIN_DISTANCE = 0.25f;    // How close controllers need to be to plane

    private int doNotTeleportLayer;
    private int worldUILayer;

    // Laser Pointer stuff
    private GameObject laser0;
    private GameObject laser1;
    //private Transform laserTransform;
    //private Vector3 laserStartPos;
    private Vector3 hitPoint;
    private int hitLayer;

    // Teleport stuff
    private Transform cameraRigTransform;
    private GameObject reticle;
    private Transform teleportReticleTransform;
    private Transform headTransform;
    private Vector3 teleportReticleOffset;
    private bool shouldTeleport;


    public NavigationState()
    {
        desc = "NavigationState";

        cameraRigTransform = GameObject.Find("[CameraRig]").transform;
        headTransform = GameObject.FindGameObjectWithTag("MainCamera").transform;

        teleportReticleOffset = new Vector3(0, 0.005f, 0);
        doNotTeleportLayer = LayerMask.NameToLayer("Do not teleport");
        worldUILayer = LayerMask.NameToLayer("WorldUI");

        //laser =  Instantiate(Resources.Load<GameObject>("Prefabs/LaserPointer"));
        //laserTransform = laser.transform;
        laser0 = GameObject.Find("LaserParent").transform.GetChild(0).gameObject;
        laser1 = GameObject.Find("LaserParent").transform.GetChild(1).gameObject;

        reticle = GameObject.Find("ReticleParent").transform.GetChild(0).gameObject; //Instantiate(Resources.Load<GameObject>("Prefabs/Reticle"));
        teleportReticleTransform = reticle.transform;
    }

    // Update is called once per frame
    override public void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        // Check if controllers are close to plane
        if(NearPlane(controller0Info) && NearPlane(controller1Info) && NearPlane(EdgeSelectionState.ClosestPointToPlane(controller0Info.trackedObj.transform.position)) && NearPlane(EdgeSelectionState.ClosestPointToPlane(controller1Info.trackedObj.transform.position)))
        {
          //  GameObject.Destroy(laser);
            Debug.Log("Switching from NavigationState to EdgeSelection state");
            GameObject.Find("UIController").GetComponent<UIController>().changeState(new EdgeSelectionState(controller0Info, controller1Info));
        }

        // Teleport
        if (controller0Info.device.GetHairTrigger()) {

            laser1.SetActive(false);
            DoRayCast(controller0Info, laser0);
        }
        else if (controller1Info.device.GetHairTrigger())
        {
            laser0.SetActive(false);
            DoRayCast(controller1Info, laser1);
           
        }
        else if (controller0Info.device.GetHairTriggerUp() || controller1Info.device.GetHairTriggerUp())
        {
            if (shouldTeleport)
            {
                Teleport();
            }
        }
        else if (controller0Info.device.GetPress(SteamVR_Controller.ButtonMask.Touchpad) || controller1Info.device.GetPress(SteamVR_Controller.ButtonMask.Touchpad))    // Go back to start state, useful for debugging
        {
            GameObject.Find("UIController").GetComponent<UIController>().changeState(new StartState());
        }
        else
        {
            laser0.SetActive(false);
            laser1.SetActive(false);
            reticle.SetActive(false);
        }
    }

    void DoRayCast(ControllerInfo controllerInfo, GameObject laser)
    {
        RaycastHit hit;
        Vector3 laserStartPos = controllerInfo.trackedObj.transform.position;

        if (Physics.Raycast(laserStartPos, controllerInfo.trackedObj.transform.forward, out hit, 1000))
        {
            // No matter what object is hit, show the laser pointing to it
            hitPoint = hit.point;
            hitLayer = hit.collider.gameObject.layer;
            ShowLaser(hit, laser, laserStartPos);

            if (hitLayer != doNotTeleportLayer && hitLayer != worldUILayer)
            {
                reticle.SetActive(true);
                teleportReticleTransform.position = hitPoint + teleportReticleOffset;
                shouldTeleport = true;
            }
            else
            {
                reticle.SetActive(false);
                shouldTeleport = false;
            }
        }
        else
        {
            laser.SetActive(false);
            reticle.SetActive(false);
            shouldTeleport = false;
        }
    }

    private void Teleport()
    {
        // 1
        shouldTeleport = false;
        // 2
        reticle.SetActive(false);
        // 3
        Vector3 difference = cameraRigTransform.position - headTransform.position;
        // 4
        difference.y = 0;
        // 5
        cameraRigTransform.position = hitPoint + difference;
    }

    private void ShowLaser(RaycastHit hit, GameObject laser, Vector3 laserStartPos)
    {
        laser.SetActive(true);
        Transform laserTransform = laser.transform;

        laserTransform.position = Vector3.Lerp(laserStartPos, hitPoint, .5f);
        laserTransform.LookAt(hitPoint);
        laserTransform.localScale = new Vector3(laserTransform.localScale.x, laserTransform.localScale.y, hit.distance);
    }

    private bool NearPlane(ControllerInfo controllerInfo)
    {
        GameObject viewPlane = GameObject.Find("ViewPlane");
        if (viewPlane == null)
        {
            return false;
        }
        else
        {
            Vector3 controllerPos = controllerInfo.trackedObj.transform.position;
            Vector3 planePos = viewPlane.transform.position;

            float distance = Vector3.Distance(controllerPos, planePos);

            return (distance <= MIN_DISTANCE);
        }
    }

    // Returns true if point is certain distance from plane
    private bool NearPlane(Vector3 projectedPoint)
    {
        GameObject viewPlane = GameObject.Find("ViewPlane");
        if (viewPlane == null)
        {
            return false;
        }
        else
        {
            Vector3 planePos = viewPlane.transform.position;

            float distance = Vector3.Distance(projectedPoint, planePos);

            return (distance <= viewPlane.transform.localScale.x * 5 + NavigationState.MIN_DISTANCE);
        }
    }
}
