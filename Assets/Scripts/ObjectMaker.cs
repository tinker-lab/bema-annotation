/**
 * This class cuts the mesh of an object into two submeshes in order to show a shape on the object.
 * The mesh is cut using planes in unity.
 * There is a scene in unity called ObjectMaker that contains an ObjectMaker.
 * This ObjectMaker contains two slots for 'Base Object' and 'Plane Parent'
 * Add objects to the scene as you like.
 * In order to specify which object to cut the mesh of, the object name (from the scene list) must be dragged onto the Base Object slot.
 * There should also be a planeParent group in the scene list, and this group name (from the scene list) should dragged onto the Plane Parent slot.
 * Within the folder of planeParent, add as many planes as desired.
 * Position these planes on the object that will be cut.
 * Make sure the rotation faces into the object. Planes are one sided, and the side that can be seen in unity is the side where the cut is made.
 * Press play on unity, and a new object and mesh will be made.
 * In order to save this new object, you must rename the object and the mesh before hitting play again, or else it will be replaced with the new cut.
 * Only one object can be made per time play is hit.
 * The only exception to this is that you can make circles as well by hitting the key 'c' after pressing play once.
 * Additional planes will show up to do this, so delete the other planes for the circle to be successful.
 * The center of the circle will be the origin on the unity scene, so make sure the object is placed at the origin.
 * To change the size of number of planes of the circle, go to the MakeCircle class and edit radius, and numSections relatively.
 * The new submesh will be pink because there is no material attached to it.
 * You can add a material to the submesh on the object itself.
 */

using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public class ObjectMaker : MonoBehaviour
{

    public GameObject baseObject; //the object the planes are cutting into
    public GameObject planeParent;

    int[] previousSelectedIndices;
    int[] previousUnselectedIndices;
    Vector3[] previousVertices;
    List<Vector2> previousUVs;
    int previousNumVertices;
    List<Vector2> UVList;

    // Use this for initialization
    void Start()
    {
        //init globals
        previousSelectedIndices = baseObject.GetComponent<MeshFilter>().mesh.GetIndices(0);
        previousUnselectedIndices = new int[0];
        previousVertices = baseObject.GetComponent<MeshFilter>().mesh.vertices;
        UVList = new List<Vector2>();
        baseObject.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
        previousUVs = UVList;
        previousNumVertices = baseObject.GetComponent<MeshFilter>().mesh.vertices.Length;

        MakeObject();
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.C))
        {
            MakeCircle circle = new MakeCircle();
            circle.GeneratePlaneCircle();
            MakeObject();
        }
    }

    private void MakeObject()
    {
        foreach (Transform planeChild in planeParent.transform)
        {
            GameObject plane = planeChild.gameObject;
            SplitMesh(baseObject, plane); //cuts the mesh where the plane is placed
            previousVertices = baseObject.GetComponent<MeshFilter>().mesh.vertices;
            baseObject.GetComponent<MeshFilter>().mesh.GetUVs(0, UVList);
            previousUVs = UVList;
            previousNumVertices = baseObject.GetComponent<MeshFilter>().mesh.vertices.Length;

            ColorMesh();
            Debug.Break();
        }

        SaveObject();
    }


    /* The code in SaveObject() and CreateNew() come from the Unity manual at https://docs.unity3d.com/ScriptReference/PrefabUtility.html */
    private void SaveObject(){
        //Set the path as within the Assets folder, and name it as the GameObject's name with the .prefab format
        string localPath = "Assets/" + baseObject.name + ".prefab";

        //Check if the Prefab and/or name already exists at the path
        if (AssetDatabase.LoadAssetAtPath(localPath, typeof(GameObject)))
        {
            //Create dialog to ask if User is sure they want to overwrite existing prefab
            if (EditorUtility.DisplayDialog("Are you sure?",
                    "The prefab already exists. Do you want to overwrite it?",
                    "Yes",
                    "No"))
            //If the user presses the yes button, create the Prefab
            {
                CreateNew(baseObject, localPath);
            }
        }
        //If the name doesn't exist, create the new Prefab
        else
        {
            Debug.Log(baseObject.name + " is not a prefab, will convert");
            CreateNew(baseObject, localPath);
        }
    }

    //altered with code from https://answers.unity.com/questions/540882/can-anyone-help-with-creating-prefabs-for-procedur.html to save mesh as an asset
    static void CreateNew(GameObject obj, string localPath)
    {
        Mesh changedMesh = obj.GetComponent<MeshFilter>().mesh;
        AssetDatabase.CreateAsset(changedMesh, localPath.Substring(0,localPath.Length-7) + " mesh.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        //Create a new prefab at the path given
        Object prefab = PrefabUtility.CreatePrefab(localPath, obj);
        PrefabUtility.ReplacePrefab(obj, prefab, ReplacePrefabOptions.ConnectToPrefab);
    }

    private void ColorMesh(){
        Mesh mesh = baseObject.GetComponent<MeshFilter>().mesh;
        Material[] materials = new Material[2];
        mesh.subMeshCount = 2;
        //Debug.Log(item.name + " s: " + SelectionData.PreviousSelectedIndices[item.name].Count().ToString() + " u: " + SelectionData.PreviousUnselectedIndices[item.name].Count().ToString());
        mesh.SetTriangles(previousSelectedIndices, 1);
        mesh.SetTriangles(previousUnselectedIndices, 0);


        materials[0] = Resources.Load("GrayConcrete") as Material;
        materials[1] = Resources.Load("BlueConcrete") as Material;      // May need to specify which submesh we get this from? -> THIS SETS SELECTION AS ORANGE STUFF

        baseObject.GetComponent<Renderer>().materials = materials;
        mesh.RecalculateNormals();
    }

    private void SplitMesh(GameObject item, GameObject slicePlane)
    {
        Mesh mesh = item.GetComponent<MeshFilter>().mesh;
        List<int> selected0Indices = new List<int>();
        List<int> selected1Indices = new List<int>();

        int[] indices = previousSelectedIndices;        // original indices is set to be JUST the selected part, that's why nothing else is drawn
        List<Vector3> vertices = previousVertices.ToList<Vector3>();

        List<Vector2> UVs = previousUVs;
        int numVertices = previousNumVertices;

        List<Vector3> transformedVertices = new List<Vector3>(vertices.Count);

        for (int i = 0; i < vertices.Count; i++)
        {
            transformedVertices.Add(item.gameObject.transform.TransformPoint(vertices[i]));
        }

        Vector3 intersectPoint0 = new Vector3();
        Vector3 intersectPoint1 = new Vector3();
        Vector3 intersectPoint2 = new Vector3();

        Vector2 intersectUV0 = new Vector2();
        Vector2 intersectUV1 = new Vector2();
        Vector2 intersectUV2 = new Vector2();

        int triangleIndex0;
        int triangleIndex1;
        int triangleIndex2;

        int intersectIndex0;
        int intersectIndex1;
        int intersectIndex2;


        for (int i = 0; i < indices.Length / 3; i++)
        {
            triangleIndex0 = indices[3 * i];
            triangleIndex1 = indices[3 * i + 1];
            triangleIndex2 = indices[3 * i + 2];

            bool side0 = IntersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex1], ref intersectPoint0, ref intersectUV0, UVs[triangleIndex0], UVs[triangleIndex1], vertices[triangleIndex0], vertices[triangleIndex1], slicePlane);
            bool side1 = IntersectsWithPlane(transformedVertices[triangleIndex1], transformedVertices[triangleIndex2], ref intersectPoint1, ref intersectUV1, UVs[triangleIndex1], UVs[triangleIndex2], vertices[triangleIndex1], vertices[triangleIndex2], slicePlane);
            bool side2 = IntersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex2], ref intersectPoint2, ref intersectUV2, UVs[triangleIndex0], UVs[triangleIndex2], vertices[triangleIndex0], vertices[triangleIndex2], slicePlane);


            if (!side0 && !side1 && !side2) // 0 intersections
            {
                if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], slicePlane))
                {
                    AddNewIndices(selected0Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                }
                else
                {
                    AddNewIndices(selected1Indices, triangleIndex0, triangleIndex1, triangleIndex2);
                }
            }
            else
            {  // intersections have occurred
               // determine which side of triangle has 1 vertex
               // add vertex and indices to appropriate mesh
               // for side with 2, add vertices, add 2 triangles
                if (side0 && side1) // 2 intersections
                {
                    intersectIndex0 = numVertices++;
                    intersectIndex1 = numVertices++;

                    vertices.Add(intersectPoint0);
                    vertices.Add(intersectPoint1);


                    transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint0));
                    transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint1));
                    UVs.Add(intersectUV0);
                    UVs.Add(intersectUV1);

                    if (OnNormalSideOfPlane(transformedVertices[triangleIndex1], slicePlane))
                    {

                        // Add the indices for various triangles to selected and unselected

                        AddNewIndices(selected0Indices, intersectIndex1, intersectIndex0, triangleIndex1);
                        AddNewIndices(selected1Indices, triangleIndex0, intersectIndex0, intersectIndex1);
                        AddNewIndices(selected1Indices, triangleIndex2, triangleIndex0, intersectIndex1);

                    }
                    else
                    {
                        AddNewIndices(selected1Indices, intersectIndex1, intersectIndex0, triangleIndex1);
                        AddNewIndices(selected0Indices, triangleIndex0, intersectIndex0, intersectIndex1);
                        AddNewIndices(selected0Indices, triangleIndex2, triangleIndex0, intersectIndex1);
                    }
                }
                else if (side0 && side2)
                {
                    intersectIndex0 = numVertices++;
                    intersectIndex2 = numVertices++;

                    vertices.Add(intersectPoint0);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                    vertices.Add(intersectPoint2);

                    transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint0));
                    transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint2));
                    UVs.Add(intersectUV0);
                    UVs.Add(intersectUV2);

                    if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], slicePlane))
                    {
                        AddNewIndices(selected0Indices, intersectIndex2, triangleIndex0, intersectIndex0);
                        AddNewIndices(selected1Indices, triangleIndex2, intersectIndex2, intersectIndex0);
                        AddNewIndices(selected1Indices, triangleIndex1, triangleIndex2, intersectIndex0);
                    }
                    else
                    {
                        AddNewIndices(selected1Indices, intersectIndex2, triangleIndex0, intersectIndex0);
                        AddNewIndices(selected0Indices, triangleIndex2, intersectIndex2, intersectIndex0);
                        AddNewIndices(selected0Indices, triangleIndex1, triangleIndex2, intersectIndex0);
                    }
                }
                else if (side1 && side2)
                {
                    intersectIndex1 = numVertices++;
                    intersectIndex2 = numVertices++;

                    vertices.Add(intersectPoint1);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                    vertices.Add(intersectPoint2);

                    transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint1));
                    transformedVertices.Add(item.gameObject.transform.TransformPoint(intersectPoint2));
                    UVs.Add(intersectUV1);
                    UVs.Add(intersectUV2);

                    if (OnNormalSideOfPlane(transformedVertices[triangleIndex2], slicePlane))
                    {
                        AddNewIndices(selected0Indices, intersectIndex1, triangleIndex2, intersectIndex2);
                        AddNewIndices(selected1Indices, intersectIndex2, triangleIndex0, intersectIndex1);
                        AddNewIndices(selected1Indices, triangleIndex0, triangleIndex1, intersectIndex1);
                    }
                    else
                    {
                        AddNewIndices(selected1Indices, intersectIndex1, triangleIndex2, intersectIndex2);
                        AddNewIndices(selected0Indices, intersectIndex2, triangleIndex0, intersectIndex1);
                        AddNewIndices(selected0Indices, triangleIndex0, triangleIndex1, intersectIndex1);
                    }
                }
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, UVs);

        previousSelectedIndices = selected0Indices.ToArray<int>();
        previousUnselectedIndices = previousUnselectedIndices.Concat(selected1Indices).ToArray<int>();
    }

    private bool OnNormalSideOfPlane(Vector3 pt, GameObject plane)
    {
        return Vector3.Dot(plane.transform.up, pt) >= Vector3.Dot(plane.transform.up, plane.transform.position);
    }

    //not used?
    private bool NormalSwipe(Vector3 swipeDirection, GameObject slicePlane)
    {
        return Vector3.Dot(swipeDirection, slicePlane.transform.up) <= 0;
    }

    // Adds a triangle with predefined indices into a list of indices
    private void AddNewIndices(List<int> indices, int index0, int index1, int index2)
    {
        indices.Add(index0);
        indices.Add(index1);
        indices.Add(index2);
    }

    //tests if an intersection with one of the planes is made
    private bool IntersectsWithPlane(Vector3 lineVertexWorld0, Vector3 lineVertexWorld1, ref Vector3 intersectPoint, ref Vector2 intersectUV, Vector2 vertex0UV, Vector2 vertex1UV, Vector3 lineVertexLocal0, Vector3 lineVertexLocal1, GameObject plane) // checks if a particular edge intersects with the plane, if true, returns point of intersection along edge
    {
        Vector3 lineSegmentLocal = lineVertexLocal1 - lineVertexLocal0;
        float dot = Vector3.Dot(plane.transform.up, lineVertexWorld1 - lineVertexWorld0);
        Vector3 w = plane.transform.position - lineVertexWorld0;

        float epsilon = 0.001f;
        if (Mathf.Abs(dot) > epsilon)
        {
            float factor = Vector3.Dot(plane.transform.up, w) / dot;
            if (factor >= 0f && factor <= 1f)
            {
                lineSegmentLocal = factor * lineSegmentLocal;
                intersectPoint = lineVertexLocal0 + lineSegmentLocal;
                intersectUV = vertex0UV + factor * (vertex1UV - vertex0UV);

                return true;
            }
        }
        return false;
    }

}

