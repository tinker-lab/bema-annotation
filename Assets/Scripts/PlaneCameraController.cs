using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaneCameraController : MonoBehaviour {

    private GameObject plane;
    private Vector3 offset;

	// Use this for initialization
	void Start () {
       // plane = GameObject.Find("P");
      //  offset = transform.position - plane.transform.position;
        // near and far clip plane should be 0.01 and 1
        GetComponent<Camera>().aspect = 1.0f;
        plane = null;

        //TODO: fix offset so that it places camera constant 1 meter from plane
        // fix rotation

		
	}
	
	// Update is called once per frame
	void Update () {
        if (plane != null) { 
        transform.position = plane.transform.position + offset;
        GetComponent<Camera>().orthographicSize = plane.transform.localScale.x * 5;
            //transform.rotation = plane.transform.rotation; // TEST THIS
            //transform.Rotate(0, 90, 0);
            transform.LookAt(plane.transform);
        }
	}

    public void setPlane(GameObject p)
    {
        plane = p;
        //transform.rotation = plane.transform.rotation;
        //transform.Rotate(0, 90, 0);
        transform.position = plane.transform.position;
        transform.Translate(new Vector3(0, 1, 0)); // units?
        transform.LookAt(plane.transform);
        offset = transform.position - plane.transform.position; // TRANSLATE BY 1 METER
    }
}
