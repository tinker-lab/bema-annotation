using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeCollision : MonoBehaviour {

    private HashSet<Collider> collidedObjects;
    int planeLayer;

    public HashSet<Collider> CollidedObjects
    {
        get { return collidedObjects; }
    }

	// Use this for initialization
	void Start () {

        collidedObjects = new HashSet<Collider>();
        planeLayer = LayerMask.NameToLayer("PlaneLayer");
    }

    private void OnTriggerEnter(Collider other)
    {
       

        if (other.gameObject.layer != planeLayer)
        {
            UnityEngine.Debug.Log("Collided with: " + other.name);
            collidedObjects.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        collidedObjects.Remove(other);
    }
}
