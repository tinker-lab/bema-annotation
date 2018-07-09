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

    private ControllerInfo controller0;
    private ControllerInfo controller1;

    private HandSelectionState handSelectionState;

    //Plane stuff to determine whether to go to hand selection state
    private GameObject leftPlane;
    private GameObject rightPlane;
    private GameObject centerCube;
    //CubeCollision leftComponent;
    //CubeCollision rightComponent;
    CubeCollision centerComponent;

    private HashSet<GameObject> cubeColliders;


    public NavigationState(ControllerInfo controller0Info, ControllerInfo controller1Info, SelectionData sharedData)
    {
        desc = "NavigationState";
        controller0 = controller0Info;
        controller1 = controller1Info;

        handSelectionState = new HandSelectionState(controller0, controller1, this, sharedData);
        leftPlane = GameObject.Find("handSelectionLeftPlane");
        rightPlane = GameObject.Find("handSelectionRightPlane");
        centerCube = GameObject.Find("handSelectionCenterCube");
        //leftComponent = leftPlane.GetComponent<CubeCollision>();
        //rightComponent = rightPlane.GetComponent<CubeCollision>();
        centerComponent = centerCube.GetComponent<CubeCollision>();

        cubeColliders = new HashSet<GameObject>();


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

    public void UpdatePlanes()
    {
        leftPlane.transform.position = controller0.controller.transform.position;
        rightPlane.transform.position = controller1.controller.transform.position;

        leftPlane.transform.up = (rightPlane.transform.position - leftPlane.transform.position).normalized;
        rightPlane.transform.up = (leftPlane.transform.position - rightPlane.transform.position).normalized;

        CenterCubeBetweenControllers();
    }

    private void CenterCubeBetweenControllers()
    {
        // position plane at midpoint between controllers

        Vector3 leftPosition = leftPlane.transform.position;
        Vector3 rightPosition = rightPlane.transform.position;

        Vector3 halfWayBtwHands = Vector3.Lerp(leftPosition, rightPosition, 0.5f);
        centerCube.transform.position = halfWayBtwHands;

        // rotate plane w/ respect to both controllers
        RotatePlane(controller0, controller1, leftPosition, rightPosition, centerCube);

        // scale plane
        float distance = Vector3.Distance(rightPosition, leftPosition);

        centerCube.transform.localScale = new Vector3(1f, 0, 0) * distance + new Vector3(0, 0.3f, 0.3f);


    }

    private void RotatePlane(ControllerInfo controller0Info, ControllerInfo controller1Info, Vector3 leftPos, Vector3 rightPos, GameObject nPlane)
    {
        Vector3 xAxis = (rightPos - leftPos).normalized;

        Vector3 zAxis = controller0Info.isLeft ? controller1Info.trackedObj.transform.forward : controller0Info.trackedObj.transform.forward;
        zAxis = (zAxis - (Vector3.Dot(zAxis, xAxis) * xAxis)).normalized;
        Vector3 yAxis = Vector3.Cross(zAxis, xAxis).normalized;

        Vector3 groundY = new Vector3(0, 1);

        //float controllerToGroundY = Vector3.Angle(yAxis, groundY);
        nPlane.transform.rotation = Quaternion.LookRotation(zAxis, yAxis);

    }

    // Update is called once per frame
    override public void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        /*
        // Check if controllers are close to plane
        if(NearPlane(controller0Info) && NearPlane(controller1Info) && NearPlane(EdgeSelectionState.ClosestPointToPlane(controller0Info.trackedObj.transform.position)) && NearPlane(EdgeSelectionState.ClosestPointToPlane(controller1Info.trackedObj.transform.position)))
        {
          //  GameObject.Destroy(laser);
            Debug.Log("Switching from NavigationState to EdgeSelection state");
            GameObject.Find("UIController").GetComponent<UIController>().changeState(new EdgeSelectionState(controller0Info, controller1Info));
        }
        */

        UpdatePlanes();

        // Take input from cube and both handplanes about what they collide with
        cubeColliders = centerComponent.CollidedObjects;
        //HashSet<Collider> leftColliders = leftComponent.CollidedObjects;
        //HashSet<Collider> rightColliders = rightComponent.CollidedObjects;

        // If both handplanes are colliding with something, just deal with all the meshes that hand planes are both colliding with.
        if (cubeColliders.Count > 0)
        {
            // Debug.Log("Switching to handselectionstate");

            //controller0.controller.gameObject.transform.GetChild(0).gameObject.SetActive(false); // Deactiveate rendering of controllers
            //controller1.controller.gameObject.transform.GetChild(0).gameObject.SetActive(false); //

            //controller0.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = true;    // Enable hand rendering
            //controller1.controller.gameObject.transform.GetChild(1).GetComponent<MeshRenderer>().enabled = true;    //

            foreach (GameObject activeHighlight in HandSelectionState.LeftOutlines.Values)
            {
                activeHighlight.GetComponent<MeshRenderer>().enabled = true;
            }
            foreach (GameObject activeHighlight in HandSelectionState.RightOutlines.Values)
            {
                activeHighlight.GetComponent<MeshRenderer>().enabled = true;
            }

            GameObject.Find("UIController").GetComponent<UIController>().ChangeState(handSelectionState);

        }

        // Teleport
        if (controller0.device.GetHairTrigger()) {

            laser1.SetActive(false);
            DoRayCast(controller0, laser0);
        }
        else if (controller1.device.GetHairTrigger())
        {
            laser0.SetActive(false);
            DoRayCast(controller1, laser1);
           
        }
        else if (controller0.device.GetHairTriggerUp() || controller1.device.GetHairTriggerUp())
        {
            if (shouldTeleport)
            {
                laser0.SetActive(false);
                laser1.SetActive(false);
                Teleport();
            }
        }
        /*
        else if (controller0Info.device.GetPress(SteamVR_Controller.ButtonMask.Touchpad) || controller1Info.device.GetPress(SteamVR_Controller.ButtonMask.Touchpad))    // Go back to start state, useful for debugging
        {
            GameObject.Find("UIController").GetComponent<UIController>().changeState(new StartState());
        }
        */
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

        var everythingExeceptPlaneLayer = LayerMask.NameToLayer("PlaneLayer");
        everythingExeceptPlaneLayer = 1 << everythingExeceptPlaneLayer;
        everythingExeceptPlaneLayer = ~everythingExeceptPlaneLayer;

        if (Physics.Raycast(laserStartPos, controllerInfo.trackedObj.transform.forward, out hit, 1000, everythingExeceptPlaneLayer))
        {
            // No matter what object is hit, show the laser pointing to it
            Debug.Log("raycast " + hit.collider.gameObject.name);
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
