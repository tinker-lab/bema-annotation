using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class UndoManager
{
    //private List<string> recentlySelectedObj;     //most recently selected object, should only store one object

    private ControllerInfo controller0;
    private ControllerInfo controller1;

    private SelectionData selectionData;

    //public List<string> RecentlySelectedObj
    //{
    //    get { return recentlySelectedObj; }
    //    set { recentlySelectedObj = value; }
    //}

    public UndoManager(ControllerInfo controller0Info, ControllerInfo controller1Info, SelectionData sharedData)
    {
        //recentlySelectedObj = new List<string>();
        controller0 = controller0Info;
        controller1 = controller1Info;

        selectionData = sharedData;
    }

    public void Undo()
    {
        for (int i = 0; i < SelectionData.RecentlySelectedObj.Count; i++)
        {
            Debug.Log(SelectionData.RecentlySelectedObjNames.ElementAt(i).ToString() + " Before Call");
            UndoFunction(SelectionData.RecentlySelectedObjNames.ElementAt(i), SelectionData.RecentlySelectedObj.ElementAt(i));
        }

        if (SelectionData.RecentlySelectedObj.Count == 0)
        {
            Debug.Log("User must make a selection before undoing");
        }

        SelectionData.RecentlySelectedObj.Clear();
        SelectionData.RecentlySelectedObjNames.Clear();
    }

    //pass in object as well for submeshes
    private void UndoFunction(string objectName, GameObject currObj){

        Mesh mesh = currObj.GetComponent<MeshFilter>().mesh;

        Debug.Assert(SelectionData.NumberOfSelections.ContainsKey(objectName)); //It better have at least one if we are undoing it

        if(SelectionData.NumberOfSelections[objectName] > 1){
            //import the most recent indices for this object and 2 submeshes
            SelectionData.PreviousUVs[objectName] = SelectionData.RecentUVs[objectName];
            SelectionData.PreviousVertices[objectName] = SelectionData.RecentVertices[objectName];
            SelectionData.PreviousNumVertices[objectName] = SelectionData.RecentNumVertices[objectName];
            SelectionData.PreviousUnselectedIndices[objectName] = SelectionData.RecentUnselectedIndices[objectName];
            SelectionData.PreviousSelectedIndices[objectName] = SelectionData.RecentSelectedIndices[objectName];

            mesh.Clear();
            mesh.SetVertices(SelectionData.PreviousVertices[objectName].ToList());
            mesh.SetUVs(0, SelectionData.PreviousUVs[objectName].ToList());

            mesh.subMeshCount = 2;
            mesh.SetTriangles(SelectionData.PreviousUnselectedIndices[objectName].ToList(), 0);
            mesh.SetTriangles(SelectionData.PreviousSelectedIndices[objectName].ToList(), 1);

            mesh.RecalculateNormals();

            SelectionData.NumberOfSelections[objectName] = SelectionData.NumberOfSelections[objectName] - 1;

            Debug.Log(objectName.ToString() + " After Call - more than one selection");
        }
        else{
            //back to one mesh and no info
            Material material = currObj.GetComponent<Renderer>().materials[0]; //materials[0] corresponds to unselected
            Material m2 = currObj.GetComponent<Renderer>().materials[1]; 
            SelectionData.ObjectsWithSelections.Remove(objectName);
            SelectionData.PreviousUVs.Remove(objectName);
            SelectionData.PreviousVertices.Remove(objectName);
            SelectionData.PreviousNumVertices.Remove(objectName);
            SelectionData.PreviousSelectedIndices.Remove(objectName);
            SelectionData.PreviousUnselectedIndices.Remove(objectName);

            mesh.Clear();
            mesh.subMeshCount = 1;
            mesh.SetVertices(SelectionData.RecentVertices[objectName].ToList());
            mesh.SetUVs(0, SelectionData.RecentUVs[objectName].ToList());

            
            mesh.SetTriangles(SelectionData.RecentSelectedIndices[objectName].ToList(), 0);

            
            Debug.Log(objectName.ToString() + " After Call -- only one selection");

            SelectionData.NumberOfSelections.Remove(objectName);

            Material[] materials = new Material[1];
            materials[0] = material;                          
            currObj.GetComponent<Renderer>().materials = materials;

            mesh.RecalculateNormals();

            SelectionData.RecentUVs.Remove(objectName);
            SelectionData.RecentVertices.Remove(objectName);
            SelectionData.RecentNumVertices.Remove(objectName);
            SelectionData.RecentSelectedIndices.Remove(objectName);
        }

        SelectionData.TriangleStates = SelectionData.RecentTriangleStates;
    } 
}
