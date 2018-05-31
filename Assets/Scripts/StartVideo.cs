using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StartVideo : MonoBehaviour {

    bool toggle = false;
    GameObject mainCamera;

	// Use this for initialization
	void Start () {
        mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
	}
	
	// Update is called once per frame
	void Update () {

        transform.position = mainCamera.transform.position;
        transform.rotation = mainCamera.transform.rotation;

        if (Input.GetKeyDown(KeyCode.S))
        {
            if (toggle)
            {
                GetComponent<RockVR.Video.VideoCaptureCtrl>().StopCapture();
                Debug.Log("Stopping video capture");
            }
            else
            {
                GetComponent<RockVR.Video.VideoCaptureCtrl>().StartCapture();
                Debug.Log("Starting video capture");
            }
            toggle = !toggle;
        }
	}
}
