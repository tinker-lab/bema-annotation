using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

public class MeshCombiner : MonoBehaviour {

    public GameObject baseObject;

    // Use this for initialization
    void Start () {
		
	}

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            GameObject combined = CombineSelectedAndUnselected();
            if (combined != null)
            {
                MeshSaver.SaveObject(combined, "");
            }
        }
    }

    private GameObject CombineSelectedAndUnselected()
    {
        Mesh origMesh = baseObject.GetComponent<MeshFilter>().mesh;
        if (origMesh.subMeshCount != 2)
        {
            Debug.Log("Tried to combine a mesh with out a selection.");
            return null;
        }

        int[] previousSelectedIndices = origMesh.GetIndices(0);
        int[] previousUnselectedIndices = origMesh.GetIndices(1);

        GameObject combined = Object.Instantiate(baseObject);
        combined.name = baseObject.name + " Combined";
        Mesh mesh = combined.GetComponent<MeshFilter>().mesh;
        Material[] materials = new Material[1];
        mesh.subMeshCount = 1;
        int[] combinedIndicies = new int[previousSelectedIndices.Length + previousUnselectedIndices.Length];
        previousSelectedIndices.CopyTo(combinedIndicies, 0);
        previousUnselectedIndices.CopyTo(combinedIndicies, previousSelectedIndices.Length);
        mesh.SetTriangles(combinedIndicies, 0);

        materials[0] = Resources.Load("OverlaySeeThru") as Material;

        combined.GetComponent<Renderer>().materials = materials;
        mesh.RecalculateNormals();
        return combined;
    }
}
