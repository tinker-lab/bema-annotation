using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ViveCustomController : MonoBehaviour {

    private SteamVR_TrackedObject trackedObj;

    public int doNotTeleportLayer;
    public int worldUILayer;

    // Laser Pointer stuff
    public GameObject laserPrefab;
    private GameObject laser;
    private Transform laserTransform;
    private Vector3 hitPoint;
    private GameObject hitObject;
    private int hitLayer;

    // Teleport stuff
    public Transform cameraRigTransform;
    public GameObject teleportReticlePrefab;
    private GameObject reticle;
    private Transform teleportReticleTransform;
    public Transform headTransform;
    public Vector3 teleportReticleOffset;
    private bool shouldTeleport;

    //planes
    public GameObject otherController;
    private GameObject newPlane;
    private int planeCounter;
    public Material m;
    private bool snapH, snapV;

    public GameObject buttonPrefab;
    private GameObject lastObjectHighlighted;

    private SteamVR_Controller.Device Controller
    {
        get { return SteamVR_Controller.Input((int)trackedObj.index); }
    }

    void Awake()
    {
        trackedObj = GetComponent<SteamVR_TrackedObject>();
    }

    void Start()
    {
        laser = Instantiate(laserPrefab);
        laserTransform = laser.transform;

        reticle = Instantiate(teleportReticlePrefab);
        teleportReticleTransform = reticle.transform;

        newPlane = new GameObject();
        planeCounter = 0;
        snapH = snapV = false;
    }

    // Update is called once per frame
    void Update()
    {

        // Cutting plane
        SteamVR_Controller.Device otherDev = null;
        try // fix later
        {
            otherDev = SteamVR_Controller.Input((int)otherController.GetComponent<SteamVR_TrackedObject>().index);
        }
        catch (System.IndexOutOfRangeException e) { }

        if (otherDev != null)
        {

            if (Controller.GetHairTriggerDown() && !otherDev.GetHairTrigger()) //!otherController.GetComponent<SteamVR_TrackedController>().triggerPressed))
            {

                // create plane
                newPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                while (GameObject.Find("Plane" + planeCounter))
                {
                    planeCounter += 1;
                }
                newPlane.name = "Plane" + planeCounter;
                planeCounter += 1;

                // cannot teleport to created plane
                newPlane.layer = worldUILayer;

                newPlane.GetComponent<Renderer>().material = m;
                snapH = snapV = false;
            }

            if (Controller.GetHairTrigger() && newPlane != null)
            {
                ChangePlane(newPlane);
            }

            if (Controller.GetHairTriggerUp())
            {
                newPlane = null;
            }

            // Teleport
            else if (Controller.GetPress(SteamVR_Controller.ButtonMask.Touchpad))
            {
                RaycastHit hit;

                if (Physics.Raycast(trackedObj.transform.position, transform.forward, out hit, 1000))
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
                    // If hitting a UI element, and it's a button, highlight it
                    if (hitLayer == worldUILayer && hitObject.CompareTag(LoadImages.BUTTON_TAG))
                    {
                        if (lastObjectHighlighted != null && !lastObjectHighlighted.Equals(hitObject))
                        {
                            lastObjectHighlighted.GetComponent<Button>().image.GetComponent<Outline>().enabled = false;
                        }

                        hitObject.GetComponent<Button>().image.GetComponent<Outline>().enabled = true;
                        lastObjectHighlighted = hitObject;
                    }

                    //print(hitObject.name);        // For dealing with rogue mesh colliders in the model
                }
                else
                {
                    laser.SetActive(false);
                    reticle.SetActive(false);
                    shouldTeleport = false;
                }
            }
            else
            {
                laser.SetActive(false);
                reticle.SetActive(false);
            }

            if (Controller.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad))
            {
                if (shouldTeleport)
                {
                    Teleport();
                }
                // If hitting a UI element, and it's a button, invoke its onClick method
                else if (hitLayer == worldUILayer && hitObject.CompareTag(LoadImages.BUTTON_TAG))
                {
                    hitObject.GetComponent<Button>().onClick.Invoke();

                }
            }
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
        laserTransform.position = Vector3.Lerp(trackedObj.transform.position, hitPoint, .5f);
        // 3
        laserTransform.LookAt(hitPoint);
        // 4
        laserTransform.localScale = new Vector3(laserTransform.localScale.x, laserTransform.localScale.y,
            hit.distance);
    }

    private void ChangePlane(GameObject newPlane)
    {   
        // position plane at midpoint between controllers

        SteamVR_TrackedObject otherControllerTrackedObj = otherController.GetComponent<SteamVR_TrackedObject>();
        Vector3 otherControllerPosition = otherControllerTrackedObj.transform.position;
        Vector3 thisControllerPosition = trackedObj.transform.position;

        newPlane.transform.position = Vector3.Lerp(otherControllerPosition, thisControllerPosition, 0.5f);

        // rotate plane w/ respect to both controllers
        RotatePlane(thisControllerPosition, otherControllerPosition, newPlane);

        // scale plane
        newPlane.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f)* Vector3.Distance(otherControllerPosition, thisControllerPosition); // square right now

        // TASKS
        // scale in front of you (keep square shape? non-linear scaling?)
        // make plane snap to boundaries
  
    }

    private void RotatePlane(Vector3 thisControllerPos, Vector3 otherControllerPos, GameObject nPlane)
    {
        Vector3 xAxis = (thisControllerPos - otherControllerPos).normalized;
        if ((int)trackedObj.index == (SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost))) { xAxis = -xAxis; }       // Account for flipping

        Vector3 zAxis = otherController.transform.forward;
        zAxis = (zAxis - (Vector3.Dot(zAxis, xAxis) * xAxis)).normalized;
        Vector3 yAxis = Vector3.Cross(zAxis, xAxis).normalized;

        Vector3 groundY = new Vector3(0, 1);

        float controllerToGroundY = Vector3.Angle(yAxis, groundY);
        print(snapH);

        if (controllerToGroundY < 25 || controllerToGroundY > 155){
            print("controller in H range");
            print("controller " + controllerToGroundY.ToString());
            if (!snapH)  // HORIZONTAL
            {
                nPlane.transform.rotation = Quaternion.AngleAxis(controllerToGroundY, Vector3.Cross(yAxis, groundY)) * Quaternion.LookRotation(zAxis, yAxis);
                print("snapped to H" + nPlane.transform.rotation.eulerAngles.ToString());
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
        else {
            nPlane.transform.rotation = Quaternion.LookRotation(zAxis, yAxis);
            snapH = snapV = false;
            print("not snapped");
            print("controller " + controllerToGroundY.ToString());
        }

        // (zAxis, yAxis) = horizontal
        // (xAxis, -zAxis) = vertical (facing viewer)
        // (xAxis, -zAxis + yAxis) = 45 degree angle
        // 30 = <0,2,1>
        // 15 = <0, 100, 26.79f>
    }

}
