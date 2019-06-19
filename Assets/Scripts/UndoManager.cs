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

    //pass in object as well for submeshes
    public void UndoFunction(string objectName, GameObject currObj){

        Debug.Log(objectName.ToString() + " After Call");

        Mesh mesh = currObj.GetComponent<MeshFilter>().mesh;

        if(SelectionData.OnlyOneSelection.Contains(objectName)){
            //import the most recent indices for this object and 2 submeshes
            SelectionData.PreviousUVs = SelectionData.RecentUVs;
            SelectionData.PreviousVertices = SelectionData.RecentVertices;
            SelectionData.PreviousNumVertices = SelectionData.RecentNumVertices;
            SelectionData.PreviousUnselectedIndices = SelectionData.RecentUnselectedIndices;
            SelectionData.PreviousSelectedIndices = SelectionData.RecentSelectedIndices;

            mesh.Clear();
            mesh.SetVertices(SelectionData.PreviousVertices[objectName].ToList());
            mesh.SetUVs(0, SelectionData.PreviousUVs[objectName].ToList());

            mesh.subMeshCount = 2;
            mesh.SetTriangles(SelectionData.PreviousUnselectedIndices[objectName].ToList(), 0);
            mesh.SetTriangles(SelectionData.PreviousSelectedIndices[objectName].ToList(), 1);

            mesh.RecalculateNormals();
        }

        else{
            //back to one mesh and no info
            SelectionData.ObjectsWithSelections.Remove(objectName);
            SelectionData.PreviousUVs.Remove(objectName);
            SelectionData.PreviousVertices.Remove(objectName);
            SelectionData.PreviousNumVertices.Remove(objectName);
            SelectionData.PreviousSelectedIndices.Remove(objectName);
            SelectionData.PreviousUnselectedIndices.Remove(objectName);

            mesh.Clear();
            mesh.SetVertices(SelectionData.PreviousVertices[objectName].ToList());
            mesh.SetUVs(0, SelectionData.PreviousUVs[objectName].ToList());

            mesh.subMeshCount = 1;
            mesh.SetTriangles(SelectionData.PreviousSelectedIndices[objectName].ToList(), 0);

            mesh.RecalculateNormals();
        }
    } 
}
