using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaneCameraController : MonoBehaviour {

    private GameObject plane;

	void Start () {
      
        // near and far clip plane should be 0.01 and 1
        GetComponent<Camera>().aspect = 1.0f;
        plane = null; // this should be accounted for because the camera is now activated/deactivated

	}
	
	// Update is called once per frame
	void Update () {
        if (plane != null) { // only run if setPlane has been called
            transform.rotation = plane.transform.rotation; 
            transform.localRotation = Quaternion.LookRotation(-transform.up, -transform.forward); // camera follows plane, is always looking straight at it
            transform.position = plane.transform.position - transform.forward.normalized;

            GetComponent<Camera>().orthographicSize = plane.transform.localScale.x * 5; // camera view should be exactly the same size as the plane
            
        }
	}

    public void setPlane(GameObject p)
    {
        plane = p;
        Update();
    }
}
