using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class MeshSaver {

    /* The code in SaveObject() and CreateNew() come from the Unity manual at https://docs.unity3d.com/ScriptReference/PrefabUtility.html 
     * Assumes localDirRelativeToAssets ends in a slash if not the empty string.
     */
    public static void SaveObject(GameObject obj, string localDirRelativeToAssets)
    {
        //Set the path as within the Assets folder, and name it as the GameObject's name with the .prefab format
        string localPath = "Assets/" + localDirRelativeToAssets + obj.name + ".prefab";
        Debug.Log("Saving to " + localPath);

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
                CreateNew(obj, localPath);
            }
        }
        //If the name doesn't exist, create the new Prefab
        else
        {
            Debug.Log(obj.name + " is not a prefab, will convert");
            CreateNew(obj, localPath);
        }
    }

    //altered with code from https://answers.unity.com/questions/540882/can-anyone-help-with-creating-prefabs-for-procedur.html to save mesh as an asset
    private static void CreateNew(GameObject obj, string localPath)
    {
        Mesh changedMesh = obj.GetComponent<MeshFilter>().mesh;

        AssetDatabase.CreateAsset(changedMesh, localPath.Substring(0, localPath.Length - 7) + " mesh.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        //Create a new prefab at the path given
        Object prefab = PrefabUtility.CreatePrefab(localPath, obj);
        PrefabUtility.ReplacePrefab(obj, prefab, ReplacePrefabOptions.ConnectToPrefab);
    }
}
