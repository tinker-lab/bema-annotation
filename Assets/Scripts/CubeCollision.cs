using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeCollision : MonoBehaviour {

    private HashSet<GameObject> collidedObjects;
    private int planeLayer;
    //private bool isCenterCube = false;

    public HashSet<GameObject> CollidedObjects
    {
        get { return collidedObjects; }
    }

	// Use this for initialization
	void Start () {

        collidedObjects = new HashSet<GameObject>();
        planeLayer = LayerMask.NameToLayer("PlaneLayer");
        //if (this.name.Equals("handSelectionCenterCube"))
        //{
        //    isCenterCube = true;
        //}
    }

    private void OnTriggerEnter(Collider other)
    {
       

        if (other.gameObject.layer != planeLayer)
        {
            //UnityEngine.Debug.Log("Collided with: " + other.name);
            if (other.name != "SwordLine" && other.gameObject.layer != LayerMask.NameToLayer("Ignore Raycast"))
            {
                collidedObjects.Add(other.gameObject);
            }
            if (HandSelectionState.LeftOutlines.ContainsKey(other.name) || HandSelectionState.RightOutlines.ContainsKey(other.name))    // If we already have an active outline 
            {
                HandSelectionState.LeftOutlines[other.name].GetComponent<MeshRenderer>().enabled = true;
                HandSelectionState.RightOutlines[other.name].GetComponent<MeshRenderer>().enabled = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {

        //Debug.Log("On Trigger Exit");
        //reverts color back to previous full selection. Designed to handle exiting objects without leaving state
        if (!HandSelectionState.ObjectsWithSelections.Contains(other.name))
        {
            int numVertices;
            if (HandSelectionState.PreviousNumVertices.TryGetValue(other.name, out numVertices))
            {
                Mesh mesh = other.GetComponent<MeshFilter>().mesh;
                //mesh.GetVertices(vertices);
                //mesh.GetUVs(0, UVs);

                mesh.Clear();
                mesh.subMeshCount = 1;

                //vertices.RemoveRange(numVertices, vertices.Count - numVertices);
                //UVs.RemoveRange(numVertices, UVs.Count - numVertices);

                Vector3[] verticesArray = HandSelectionState.PreviousVertices[other.name];
                Vector2[] UVsArray = HandSelectionState.PreviousUVs[other.name];

                List<Vector3> vertices = new List<Vector3>(verticesArray);
                List<Vector2> UVs = new List<Vector2>(UVsArray);

                if (vertices.Count == UVs.Count)
                {
                    mesh.SetVertices(vertices);
                    mesh.SetUVs(0, UVs);
                    mesh.SetTriangles(HandSelectionState.PreviousSelectedIndices[other.name], 0);
                }

                mesh.RecalculateBounds();
                mesh.RecalculateNormals();

                Material material = other.GetComponent<Renderer>().material; // materials[0] corresponds to unselected
                other.GetComponent<Renderer>().material = material;
            }

        }

        collidedObjects.Remove(other.gameObject);

        if (HandSelectionState.LeftOutlines.ContainsKey(other.name) || HandSelectionState.RightOutlines.ContainsKey(other.name))
        {
            HandSelectionState.LeftOutlines[other.name].GetComponent<MeshRenderer>().enabled = false;
            HandSelectionState.RightOutlines[other.name].GetComponent<MeshRenderer>().enabled = false;
        }
    }
}
