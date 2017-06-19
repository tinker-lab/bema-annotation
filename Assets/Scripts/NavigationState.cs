using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NavigationState : InteractionState {

    private int doNotTeleportLayer;
    private int worldUILayer;

    // Laser Pointer stuff
    private GameObject laser;
    private Transform laserTransform;
    private Vector3 laserStartPos;
    private Vector3 hitPoint;
    private GameObject hitObject;
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
        cameraRigTransform = GameObject.Find("[CameraRig]").transform;
        headTransform = GameObject.FindGameObjectWithTag("MainCamera").transform;

        teleportReticleOffset = new Vector3(0, 0.005f, 0);
        doNotTeleportLayer = 9;
        worldUILayer = 10;

        laser = Instantiate(Resources.Load<GameObject>("Prefabs/LaserPointer"));
        laserTransform = laser.transform;

        reticle = Instantiate(Resources.Load<GameObject>("Prefabs/Reticle"));
        teleportReticleTransform = reticle.transform;
    }

    // Update is called once per frame
    override public void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        // Teleport
        if (controller0Info.device.GetHairTrigger()) {
            print("controller0 trigger down in update");
            DoRayCast(controller0Info);
        }
        else if (controller1Info.device.GetHairTrigger())
        {
            DoRayCast(controller1Info);
            print("controller0 trigger down in update");
        }
        else if (controller0Info.device.GetHairTriggerUp() || controller1Info.device.GetHairTriggerUp())
        {
            if (shouldTeleport)
            {
                Teleport();
            }
        }
        else
        {
            laser.SetActive(false);
            reticle.SetActive(false);
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
