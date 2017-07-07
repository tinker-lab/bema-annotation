using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaneCollision : MonoBehaviour {

    private HashSet<Mesh> meshes;

	// Use this for initialization
	void Start () {
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    void OnTriggerEnter(Collider other) //use stay?
    {
        Mesh otherMesh = other.GetComponent<MeshFilter>().mesh;
        // int[] triangles = otherMesh.GetTriangles(0);
        meshes.Add(otherMesh);

    }

    void OnTriggerExit(Collider other)
    {
        meshes.Remove(other.GetComponent<MeshFilter>().mesh);
    }
}
