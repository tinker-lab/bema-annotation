using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectionData {

    private static Dictionary<string, Vector3[]> previousVertices;              // Key = name of obj with mesh, Value = all vertices of the mesh at the time of last click
    private static Dictionary<string, Vector2[]> previousUVs;                   // Key = name of obj with mesh, Value = all UVs of the mesh at the time of last click
    private static Dictionary<string, int[]> previousUnselectedIndices;                // Key = name of object with mesh, Value = all indices that have not been selected (updated when user clicks)
    private static Dictionary<string, int[]> previousSelectedIndices;           // key = name of object with mesh, Value = original set of selected indices (updated when user clicks)
    private static Dictionary<string, int> previousNumVertices;                 // Key = name of object with mesh, Value = original set of vertices (updated when user clicks and mesh is split)
    private static HashSet<string> objWithSelections;                           // Collection of the the names of all the meshes that have had pieces selected from them.
    private static Dictionary<string, HashSet<GameObject>> savedOutlines;       // Key = name of object in model, Value = all the SAVED outline game objects attached to it
   
    //private List<Vector3> outlinePoints;    // Pairs of two connected points to be used in drawing an outline mesh

    public static Dictionary<string, Vector3[]> PreviousVertices
    {
        get { return previousVertices; }
        set { previousVertices = value; }
    }

    public static Dictionary<string, Vector2[]> PreviousUVs
    {
        get { return previousUVs; }
        set { previousUVs = value; }
    }

    public static Dictionary<string, int[]> PreviousUnselectedIndices
    {
        get { return previousUnselectedIndices; }
        set { previousUnselectedIndices = value; }
    }

    public static Dictionary<string, int[]> PreviousSelectedIndices
    {
        get { return previousSelectedIndices; }
        set { previousSelectedIndices = value; }
    }

    public static Dictionary<string, int> PreviousNumVertices
    {
        get { return previousNumVertices; }
        set { previousNumVertices = value; }
    }

    public static HashSet<string> ObjectsWithSelections
    {
        get { return objWithSelections; }
        set { objWithSelections = value; }
    }

    public static Dictionary<string, HashSet<GameObject>> SavedOutlines
    {
        get { return savedOutlines; }
        set { savedOutlines = value; }
    }

    public SelectionData(){
        objWithSelections = new HashSet<string>();
        previousNumVertices = new Dictionary<string, int>();              // Keeps track of how many vertices a mesh should have
        previousUnselectedIndices = new Dictionary<string, int[]>();      // Keeps track of indices that were previously unselected
        previousSelectedIndices = new Dictionary<string, int[]>();
        previousVertices = new Dictionary<string, Vector3[]>();
        previousUVs = new Dictionary<string, Vector2[]>();
        savedOutlines = new Dictionary<string, HashSet<GameObject>>();

       // outlinePoints = new List<Vector3>();                                        // Do we need this?????????????????????????

    }
}
