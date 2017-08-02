using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OutlineHighlight : MonoBehaviour {

    /*This class was originally intended to handle the highlight objects
     * colliding with each other, but it is not used anymore.
     */

    private static HashSet<Collider> collidedHighlights;
    public static HashSet<Collider> CollidedHighlights
    {
        get { return collidedHighlights; }
    }

	// Use this for initialization
	void Start () {
        collidedHighlights = new HashSet<Collider>();
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    private void OnTriggerStay(Collider other)
    {
        // write outlinehighlight script to modify collided meshes
        // add other highlight objects that are colliding to set
        if (other.CompareTag("highlightmesh"))
        {
            collidedHighlights.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("highlightmesh"))
        {
            collidedHighlights.Remove(other);
        }
    }
}
