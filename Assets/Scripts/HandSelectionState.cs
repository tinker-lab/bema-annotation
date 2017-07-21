using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

public class HandSelectionState : InteractionState
{

    InteractionState stateToReturnTo;
    private ControllerInfo controller0;
    private ControllerInfo controller1;

    private GameObject leftPlane;
    private GameObject rightPlane;
    private GameObject centerCube;

    private int planeLayer;

    private List<Collider> collidedMeshes;
    private HashSet<Collider> cubeColliders;
    private HashSet<Collider> leftColliders;
    private HashSet<Collider> rightColliders;
    CubeCollision leftComponent;
    CubeCollision rightComponent;
    CubeCollision centerComponent;

    private Dictionary<string, int> seenMeshes;   // Key = name of object with mesh, Value = original set of vertices
    private Dictionary<string, int[]> originalIndices; // key = name of object with mesh, value = original indices for the entire object

    List<int> selectedIndices;
    List<int> unselectedIndices;


    public HandSelectionState(ControllerInfo controller0Info, ControllerInfo controller1Info, InteractionState stateToReturnTo)
    {
        desc = "HandSelectionState";
        controller0 = controller0Info;
        controller1 = controller1Info;


        planeLayer = LayerMask.NameToLayer("PlaneLayer");

        leftPlane = CreateHandPlane(controller0, "handSelectionLeftPlane");
        rightPlane = CreateHandPlane(controller1, "handSelectionRightPlane");

        centerCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        centerCube.name = "handSelectionCenterCube";
        centerCube.GetComponent<Renderer>().material = Resources.Load("Plane Material") as Material;
        centerCube.AddComponent<MeshCollider>();
        centerCube.GetComponent<MeshCollider>().convex = true;
        centerCube.GetComponent<MeshCollider>().isTrigger = true;
        centerCube.AddComponent<Rigidbody>();
        centerCube.GetComponent<Rigidbody>().isKinematic = true;
        centerComponent = centerCube.AddComponent<CubeCollision>();
        centerCube.transform.localScale = new Vector3(0.3f, 0.00000001f, 0.3f);
        centerCube.layer = planeLayer;

        collidedMeshes = new List<Collider>();      // TODO: figure out how to fill this up properly
        cubeColliders = new HashSet<Collider>();
        leftColliders = new HashSet<Collider>();
        rightColliders = new HashSet<Collider>();

        leftComponent = leftPlane.GetComponent<CubeCollision>();
        rightComponent = rightPlane.GetComponent<CubeCollision>();

        //TODO: should these persist between states? Yes so only make one instance of the state. Should use the Singleton pattern here//TODO
        seenMeshes = new Dictionary<string, int>();
        originalIndices = new Dictionary<string, int[]>();
        selectedIndices = new List<int>();
        unselectedIndices = new List<int>();
    }

    public GameObject CreateHandPlane(ControllerInfo c, String name)
    {
        GameObject handPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        handPlane.name = name;
        handPlane.GetComponent<Renderer>().material = Resources.Load("Plane Material") as Material;
        handPlane.AddComponent<MeshCollider>();
        handPlane.GetComponent<MeshCollider>().convex = true;
        handPlane.GetComponent<MeshCollider>().isTrigger = true;
        handPlane.AddComponent<Rigidbody>();
        handPlane.GetComponent<Rigidbody>().isKinematic = true;
        handPlane.AddComponent<CubeCollision>();

        handPlane.transform.position = c.controller.transform.position;
        handPlane.transform.rotation = c.controller.transform.rotation;
        //  handPlane.transform.localScale = new Vector3(0.3f, 0.00000001f, 0.3f);
        handPlane.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);

        handPlane.layer = planeLayer;
        return handPlane;
    }

    public void UpdatePlanes()
    {
        leftPlane.transform.position = controller0.controller.transform.position;
        rightPlane.transform.position = controller1.controller.transform.position;

        leftPlane.transform.up = (rightPlane.transform.position - leftPlane.transform.position).normalized;
        rightPlane.transform.up = (leftPlane.transform.position - rightPlane.transform.position).normalized;

        CenterCubeBetweenControllers();
    }

    public override void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        UpdatePlanes();

        // Take input from cube and both handplanes about what they collide with
        cubeColliders = centerComponent.CollidedObjects;
        leftColliders = leftComponent.CollidedObjects;
        rightColliders = rightComponent.CollidedObjects;

        // If both handplanes are colliding with something, just deal with all the meshes that hand planes are both colliding with.
        if (leftColliders.Count > 0 && rightColliders.Count > 0)
        {
            foreach (Collider c in leftColliders)
            {
                if (rightColliders.Contains(c))
                {
                    collidedMeshes.Add(c);
                }
            }
            print("Both planes colliding");
        }
        // If just the cube (or only one handplane) is colliding, we take the entire meshes of the objects cube collides with
        else if (cubeColliders.Count > 0 && (leftColliders.Count == 0 || rightColliders.Count == 0))
        {
            collidedMeshes = cubeColliders.ToList();
            print("Cube!");
        }


        foreach (Collider m in collidedMeshes)
        {
            if (!seenMeshes.ContainsKey(m.name))
            {
                m.GetComponent<MeshFilter>().mesh.MarkDynamic();
                seenMeshes.Add(m.name, m.GetComponent<MeshFilter>().mesh.vertices.Length);        // Maybe want to store vertices as array instead?


                originalIndices.Add(m.name, m.GetComponent<MeshFilter>().mesh.GetIndices(0));
            }
            ProcessMesh(m);
        }



        if (controller0.device.GetHairTriggerUp() || controller1.device.GetHairTriggerUp())
        {
            foreach (Collider m in collidedMeshes)
            {
                seenMeshes[m.name] = m.GetComponent<MeshFilter>().mesh.vertices.Length;

                //The submesh to start
                int submeshNum = 0;
                Material[] origMaterials = m.GetComponent<Renderer>().materials;
                for (int i = 0; i < origMaterials.Length; i++)
                {
                    if (origMaterials[i].name == "Selected")
                    {
                        submeshNum = i;
                    }
                }
                originalIndices[m.name] = m.GetComponent<MeshFilter>().mesh.GetIndices(submeshNum);
            }

            Debug.Log("Switching to " + stateToReturnTo.name + " state");
            GameObject.Find("UIController").GetComponent<UIController>().changeState(stateToReturnTo);
        }

        collidedMeshes.Clear();
    }



    private bool OnNormalSideOfPlane(Vector3 pt, GameObject plane)
    {
        return Vector3.Dot(plane.transform.up, pt) >= Vector3.Dot(plane.transform.up, plane.transform.position);
    }

    private void CenterCubeBetweenControllers()
    {
        // position plane at midpoint between controllers

        Vector3 leftPosition = leftPlane.transform.position;
        Vector3 rightPosition = rightPlane.transform.position;

        Vector3 halfWayBtwHands = Vector3.Lerp(leftPosition, rightPosition, 0.5f);
        centerCube.transform.position = halfWayBtwHands;

        // rotate plane w/ respect to both controllers
        RotatePlane(controller0, controller1, leftPosition, rightPosition, centerCube);

        // scale plane
        float distance = Vector3.Distance(rightPosition, leftPosition);

        centerCube.transform.localScale = new Vector3(1f, 0, 0) * distance + new Vector3(0, 0.3f, 0.3f);


    }

    private void RotatePlane(ControllerInfo controller0Info, ControllerInfo controller1Info, Vector3 leftPos, Vector3 rightPos, GameObject nPlane)
    {
        Vector3 xAxis = (rightPos - leftPos).normalized;

        Vector3 zAxis = controller0Info.isLeft ? controller1Info.trackedObj.transform.forward : controller0Info.trackedObj.transform.forward;
        zAxis = (zAxis - (Vector3.Dot(zAxis, xAxis) * xAxis)).normalized;
        Vector3 yAxis = Vector3.Cross(zAxis, xAxis).normalized;

        Vector3 groundY = new Vector3(0, 1);

        float controllerToGroundY = Vector3.Angle(yAxis, groundY);
        nPlane.transform.rotation = Quaternion.LookRotation(zAxis, yAxis);

    }

    private void ProcessMesh(Collider m)
    {
        Mesh mesh = m.GetComponent<MeshFilter>().mesh;
        selectedIndices.Clear();

        //TODO: When we get to multiple refinement we will need to not clear this here. Instead the new unselected should just be added to it.
        unselectedIndices.Clear();

        int[] indices = originalIndices[m.name];
        List<Vector3> vertices = new List<Vector3>();
        mesh.GetVertices(vertices);

        List<Vector2> UVs = new List<Vector2>();
        mesh.GetUVs(0, UVs); //TODO: are there multiple channels?

        int numVertices = seenMeshes[m.name];
        vertices.RemoveRange(numVertices, vertices.Count - numVertices);
        UVs.RemoveRange(numVertices, UVs.Count - numVertices);

        List<Vector3> transformedVertices = new List<Vector3>(vertices.Count);

        for (int i = 0; i < vertices.Count; i++)
        {
            transformedVertices.Add(m.gameObject.transform.TransformPoint(vertices[i]));
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

        int intersect0Index;
        int intersect1Index;
        int intersect2Index;

        for (int planePass = 0; planePass < 2; planePass++)
        {
            GameObject currentPlane = leftPlane;
            if (planePass == 1)
            {
                currentPlane = rightPlane;
                indices = selectedIndices.ToArray();
                selectedIndices.Clear();
            }


            for (int i = 0; i < (int)(indices.Length / 3); i++)
            {
                triangleIndex0 = indices[3 * i];
                triangleIndex1 = indices[3 * i + 1];
                triangleIndex2 = indices[3 * i + 2];

                bool side0 = intersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex1], ref intersectPoint0, ref intersectUV0, UVs[triangleIndex0], UVs[triangleIndex1], vertices[triangleIndex0], vertices[triangleIndex1], currentPlane);
                bool side1 = intersectsWithPlane(transformedVertices[triangleIndex1], transformedVertices[triangleIndex2], ref intersectPoint1, ref intersectUV1, UVs[triangleIndex1], UVs[triangleIndex2], vertices[triangleIndex1], vertices[triangleIndex2], currentPlane);
                bool side2 = intersectsWithPlane(transformedVertices[triangleIndex0], transformedVertices[triangleIndex2], ref intersectPoint2, ref intersectUV2, UVs[triangleIndex0], UVs[triangleIndex2], vertices[triangleIndex0], vertices[triangleIndex2], currentPlane);


                if (side0 && side1 && side2) // 3 intersections?
                {
                    Debug.Log("3 sides hit");
                }
                // determine which side of triangle has 1 vertex
                // add vertex and indices to appropriate mesh
                // for side with 2, add vertices, add 2 triangles
                else if (side0 && side1) // 2 intersections
                {
                    intersect0Index = numVertices++;
                    intersect1Index = numVertices++;
                    vertices.Add(intersectPoint0);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                    vertices.Add(intersectPoint1);
                    transformedVertices.Add(m.gameObject.transform.TransformPoint(intersectPoint0));
                    transformedVertices.Add(m.gameObject.transform.TransformPoint(intersectPoint1));
                    UVs.Add(intersectUV0);
                    UVs.Add(intersectUV1);

                    if (OnNormalSideOfPlane(transformedVertices[triangleIndex1], currentPlane))
                    {

                        // Add the indices for various triangles to selected and unselected

                        AddNewIndices(selectedIndices, intersect1Index, intersect0Index, triangleIndex1);
                        AddNewIndices(unselectedIndices, triangleIndex0, intersect0Index, intersect1Index);
                        AddNewIndices(unselectedIndices, triangleIndex2, triangleIndex0, intersect1Index);

                    }
                    else
                    {
                        AddNewIndices(unselectedIndices, intersect1Index, intersect0Index, triangleIndex1);
                        AddNewIndices(selectedIndices, triangleIndex0, intersect0Index, intersect1Index);
                        AddNewIndices(selectedIndices, triangleIndex2, triangleIndex0, intersect1Index);
                    }
                }
                else if (side0 && side2)
                {
                    intersect0Index = numVertices++;
                    intersect2Index = numVertices++;
                    vertices.Add(intersectPoint0);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                    vertices.Add(intersectPoint2);
                    transformedVertices.Add(m.gameObject.transform.TransformPoint(intersectPoint0));
                    transformedVertices.Add(m.gameObject.transform.TransformPoint(intersectPoint2));
                    UVs.Add(intersectUV0);
                    UVs.Add(intersectUV2);

                    if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], currentPlane))
                    {
                        AddNewIndices(selectedIndices, intersect2Index, triangleIndex0, intersect0Index);
                        AddNewIndices(unselectedIndices, triangleIndex2, intersect2Index, intersect0Index);
                        AddNewIndices(unselectedIndices, triangleIndex1, triangleIndex2, intersect0Index);
                    }
                    else
                    {
                        AddNewIndices(unselectedIndices, intersect2Index, triangleIndex0, intersect0Index);
                        AddNewIndices(selectedIndices, triangleIndex2, intersect2Index, intersect0Index);
                        AddNewIndices(selectedIndices, triangleIndex1, triangleIndex2, intersect0Index);
                    }
                }
                else if (side1 && side2)
                {
                    intersect1Index = numVertices++;
                    intersect2Index = numVertices++;
                    vertices.Add(intersectPoint1);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                    vertices.Add(intersectPoint2);
                    transformedVertices.Add(m.gameObject.transform.TransformPoint(intersectPoint1));
                    transformedVertices.Add(m.gameObject.transform.TransformPoint(intersectPoint2));
                    UVs.Add(intersectUV1);
                    UVs.Add(intersectUV2);

                    if (OnNormalSideOfPlane(transformedVertices[triangleIndex2], currentPlane))
                    {
                        AddNewIndices(selectedIndices, intersect1Index, triangleIndex2, intersect2Index);
                        AddNewIndices(unselectedIndices, intersect2Index, triangleIndex0, intersect1Index);
                        AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, intersect1Index);
                    }
                    else
                    {
                        AddNewIndices(unselectedIndices, intersect1Index, triangleIndex2, intersect2Index);
                        AddNewIndices(selectedIndices, intersect2Index, triangleIndex0, intersect1Index);
                        AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, intersect1Index);
                    }
                }
                else // 0 intersections
                {
                    if (OnNormalSideOfPlane(transformedVertices[triangleIndex0], currentPlane))
                    {
                        AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                    }
                    else
                    {
                        AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                    }
                }

            }
        }

        mesh.Clear();
        mesh.subMeshCount = 2;

        Debug.Log("vertex count: " + vertices.Count + " uv count: " + UVs.Count);

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, UVs);

        //mesh.SetTriangles(indices, 0);

        mesh.SetTriangles(unselectedIndices, 0);
        mesh.SetTriangles(selectedIndices, 1);

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        Material[] materials = new Material[2];
        materials[0] = m.GetComponent<Renderer>().materials[0];     // May need to specify which submesh we get this from?
        materials[1] = Resources.Load("Selected") as Material;
        // materials[2] = Resources.Load("Unselected") as Material;
        m.GetComponent<Renderer>().materials = materials;


        Debug.Log("Submesh count: " + mesh.subMeshCount);
    }

    // returns value of latest index added and adds to list
    private void AddNewIndices(List<int> indices, int numToAdd)
    {
        for (int i = 0; i < numToAdd; i++)
        {
            int latestIndex = indices.Count;
            indices.Add(latestIndex);
        }
    }

    // Adds a triangle with predefined indices into a list of indices
    private void AddNewIndices(List<int> indices, int index0, int index1, int index2)
    {
        /*
        if(index0 > .Count || index1 > updatedVertices.Count || index2 > updatedVertices.Count || index0 < 0 || index1 < 0 || index2 < 0)
        {
            Debug.Log("Index out of bounds. Index0: " + index0 + " Index1: " + index1 + " Index2: " + index2);
            Debug.Log("Num vertices: " + updatedVertices.Count);
        }
        */
        indices.Add(index0);
        indices.Add(index1);
        indices.Add(index2);
    }

    private bool intersectsWithPlane(Vector3 lineVertexWorld0, Vector3 lineVertexWorld1, ref Vector3 intersectPoint, ref Vector2 intersectUV, Vector2 vertex0UV, Vector2 vertex1UV, Vector3 lineVertexLocal0, Vector3 lineVertexLocal1, GameObject plane) // checks if a particular edge intersects with the plane, if true, returns point of intersection along edge
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
