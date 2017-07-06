using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewPlaneController : MonoBehaviour {

    public bool hasCollided = false;
	
	void OnTriggerEnter(Collider col)
    {
        hasCollided = true;
        Debug.Log("Collided with " + col.gameObject.name);
    }

    void OnTriggerExit(Collider col)
    {
        hasCollided = false;
        Debug.Log("No longer colliding with " + col.gameObject.name);
    }

    void OnTriggerStay()
    {
        hasCollided = true;
    }
}
