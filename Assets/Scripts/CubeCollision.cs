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
       

        if (other.gameObject.layer != planeLayer && other.name != "Left outline" && other.name != "Right outline")
        {
            UnityEngine.Debug.Log("Collided with: " + other.name);
            collidedObjects.Add(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        //reverts color back to previous full selection. Designed to handle exiting objects without leaving state
        if (!HandSelectionState.SelectedMeshes.Contains(other.name))
        {
            int numVertices;
            if (HandSelectionState.SeenMeshes.TryGetValue(other.name, out numVertices))
            {
                Mesh mesh = other.GetComponent<MeshFilter>().mesh;
                List<Vector3> vertices = new List<Vector3>();
                List<Vector2> UVs = new List<Vector2>();
                mesh.GetVertices(vertices);
                mesh.GetUVs(0, UVs);

                mesh.Clear();
                mesh.subMeshCount = 1;

                vertices.RemoveRange(numVertices, vertices.Count - numVertices);
                UVs.RemoveRange(numVertices, UVs.Count - numVertices);

                mesh.SetVertices(vertices);
                mesh.SetUVs(0, UVs);
                mesh.SetTriangles(HandSelectionState.OriginalIndices[other.name], 0);

                mesh.RecalculateBounds();
                mesh.RecalculateNormals();

                Material material = other.GetComponent<Renderer>().material; // materials[0] corresponds to unselected
                other.GetComponent<Renderer>().material = material;
            }

        }

        HandSelectionState.ModelHighlights[other.name] = new HashSet<GameObject>();
        collidedObjects.Remove(other.gameObject);
    }
}
