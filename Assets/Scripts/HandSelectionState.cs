using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

public class HandSelectionState : InteractionState {

    private ControllerInfo controller0;
    private ControllerInfo controller1;

    private GameObject leftPlane;
    private GameObject rightPlane;
    private GameObject centerCube;

    private int planeLayer;

    private Dictionary<string, List<Vector3>> seenMeshes;   // Key = name of object with mesh, Value = original set of vertices

    private List<Collider> collidedMeshes;
    private HashSet<Collider> cubeColliders;
    private HashSet<Collider> leftColliders;
    private HashSet<Collider> rightColliders;

    private Vector3 leftPlaneUVec;
    private Vector3 leftPlaneVVec;
    private float leftULength;
    private float leftVLength;

    private Vector3 rightPlaneUVec;
    private Vector3 rightPlaneVVec;
    private float rightULength;
    private float rightVLength;

    Vector3 corner0Left;
    Vector3 corner1Left;
    Vector3 corner2Left;

    Vector3 corner0Right;
    Vector3 corner1Right;
    Vector3 corner2Right;


    bool firstTime;

    List<int> selectedIndices;
    List<int> unselectedIndices;

    CubeCollision leftComponent;
    CubeCollision rightComponent;
    CubeCollision centerComponent;

    List<Vector3> updatedVertices;

    public HandSelectionState(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        desc = "HandSelectionState";
        controller0 = controller0Info;
        controller1 = controller1Info;


        planeLayer = LayerMask.NameToLayer("PlaneLayer");

        leftPlane = CreateHandPlane(controller0);
        rightPlane = CreateHandPlane(controller1);


        centerCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
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

        firstTime = true;
        selectedIndices = new List<int>();
        unselectedIndices = new List<int>();

        leftComponent = leftPlane.GetComponent<CubeCollision>();
        rightComponent = rightPlane.GetComponent<CubeCollision>();
    }

    public GameObject CreateHandPlane(ControllerInfo c)
    {
        GameObject handPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handPlane.GetComponent<Renderer>().material = Resources.Load("Plane Material") as Material;
        handPlane.AddComponent<MeshCollider>();
        handPlane.GetComponent<MeshCollider>().convex = true;
        handPlane.GetComponent<MeshCollider>().isTrigger = true;
        handPlane.AddComponent<Rigidbody>();
        handPlane.GetComponent<Rigidbody>().isKinematic = true;
        handPlane.AddComponent<CubeCollision>();

        handPlane.transform.position = c.controller.transform.position;
        handPlane.transform.rotation = c.controller.transform.rotation;
        handPlane.transform.localScale = new Vector3(0.3f, 0.00000001f, 0.3f);

        handPlane.layer = planeLayer;
        return handPlane;
    }

    public void UpdatePlanes()
    {
        leftPlane.transform.position = controller0.controller.transform.position;
        rightPlane.transform.position = controller1.controller.transform.position;

        leftPlane.transform.up = (rightPlane.transform.position - leftPlane.transform.position).normalized;
        rightPlane.transform.up = (leftPlane.transform.position - rightPlane.transform.position).normalized;
        //leftPlane.transform.rotation = controller0.controller.transform.rotation;
        //leftPlane.transform.Rotate(0, 0, -90);
        //rightPlane.transform.rotation = controller1.controller.transform.rotation;
        //rightPlane.transform.Rotate(0, 0, 90);

        CenterPlane();

        float sideLength = leftPlane.transform.localScale.x / 2; // should be the same for both planes

        corner0Left = leftPlane.transform.position - sideLength * leftPlane.transform.right.normalized - sideLength * leftPlane.transform.forward.normalized;
        corner1Left = leftPlane.transform.position + sideLength * leftPlane.transform.right.normalized - sideLength * leftPlane.transform.forward.normalized;
        corner2Left = leftPlane.transform.position - sideLength * leftPlane.transform.right.normalized + sideLength * leftPlane.transform.forward.normalized;

        corner0Right = rightPlane.transform.position - sideLength * rightPlane.transform.right.normalized - sideLength * rightPlane.transform.forward.normalized;
        corner1Right = rightPlane.transform.position + sideLength * rightPlane.transform.right.normalized - sideLength * rightPlane.transform.forward.normalized;
        corner2Right = rightPlane.transform.position - sideLength * rightPlane.transform.right.normalized + sideLength * rightPlane.transform.forward.normalized;

        leftPlaneUVec = corner1Left - corner0Left;
        leftPlaneVVec = corner0Left - corner2Left;

        leftULength = leftPlaneUVec.magnitude;
        leftVLength = leftPlaneVVec.magnitude;

        leftPlaneUVec = Vector3.Normalize(leftPlaneUVec);
        leftPlaneVVec = Vector3.Normalize(leftPlaneVVec);

        rightPlaneUVec = corner1Right - corner0Right;
        rightPlaneVVec = corner0Right - corner2Right;

        rightULength = rightPlaneUVec.magnitude;
        rightVLength = rightPlaneVVec.magnitude;

        rightPlaneUVec = Vector3.Normalize(rightPlaneUVec);
        rightPlaneVVec = Vector3.Normalize(rightPlaneVVec);

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
            foreach(Collider c in leftColliders)
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

        if (firstTime)
        {
            //List<Vector3[]> allPoints = GetTrianglesBetweenPlanes();
            //foreach (Vector3[] points in allPoints)
            //{
            //    for (int i = 0; i < points.Length; i++)
            //    {
            //        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //        sphere.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            //        sphere.transform.position = points[i];
            //    }
            //}
           // if (collidedMeshes.Count > 0) { firstTime = false; }
            foreach (Collider m in collidedMeshes)
            {
                if (!seenMeshes.ContainsKey(m.name))
                {
                    seenMeshes.Add(m.name, m.GetComponent<MeshFilter>().mesh.vertices.ToList());        // Maybe want to store vertices as array instead?
                }
                ProcessMesh(m);
            }
        }

        if (controller0.device.GetHairTriggerUp() || controller1.device.GetHairTriggerUp())
        {
            GameObject.Find("UIController").GetComponent<UIController>().changeState(new NavigationState());
        }

        collidedMeshes.Clear();
    }

    private List<Vector3[]> GetTrianglesBetweenPlanes()
    {
        // For each mesh:
        //  Go through points
        //  Translate them to real world coords
        //  Check if real world coord is between planes
        //      if so, keep track of it
        //  

        List<Vector3[]> pointsBetweenPlanes = new List<Vector3[]>();
        Vector3 transformedPoint = new Vector3();

        foreach (Collider m in collidedMeshes)
        {
            Mesh mesh = m.GetComponent<MeshFilter>().mesh;
            Vector3[] vertices = mesh.vertices;
            int[] indices = mesh.GetTriangles(0); // 0 refers to first set of triangles (because there is only one material on the mesh)

            Vector3[] transformedVertices = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                transformedPoint = m.gameObject.transform.TransformPoint(vertices[i]);
                if (PointIsBetweenPlanes(transformedPoint)) { transformedVertices[i] = transformedPoint; }
            }

            pointsBetweenPlanes.Add(transformedVertices);
        }

        return pointsBetweenPlanes;
    }

    private bool PointIsBetweenPlanes(Vector3 pt)
    {
        bool left = Vector3.Dot(leftPlane.transform.up, pt) >= Vector3.Dot(leftPlane.transform.up, leftPlane.transform.position);
        bool right = Vector3.Dot(rightPlane.transform.up, pt) >= Vector3.Dot(rightPlane.transform.up, rightPlane.transform.position);
        return left && right;
    }

    private bool OnNormalSideLeft(Vector3 pt)
    {
        return Vector3.Dot(leftPlane.transform.up, pt) >= Vector3.Dot(leftPlane.transform.up, leftPlane.transform.position);
    }

    private bool OnNormalSideRight(Vector3 pt)
    {
        return Vector3.Dot(rightPlane.transform.up, pt) >= Vector3.Dot(rightPlane.transform.up, rightPlane.transform.position);
    }

    private void CenterPlane()
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

        int[] indices = mesh.GetTriangles(0);
        Vector3[] vertices = seenMeshes[m.name].ToArray();
        updatedVertices = seenMeshes[m.name];
        Vector3[] transformedVertices = new Vector3[updatedVertices.Count];       // Will this need to be a list since we're adding things to it?
        

        for (int i = 0; i < vertices.Length; i++)
        {
            transformedVertices[i] = m.gameObject.transform.TransformPoint(vertices[i]);
        }

        Vector3 intersectPoint0 = new Vector3();
        Vector3 intersectPoint1 = new Vector3();
        Vector3 intersectPoint2 = new Vector3();

        int triangleIndex0;
        int triangleIndex1;
        int triangleIndex2;

        int intersect0Index;
        int intersect1Index;
        int intersect2Index;

        for (int i = 0; i < (int)(indices.Length / 3); i++)
        {
            triangleIndex0 = indices[3 * i];
            triangleIndex1 = indices[3 * i + 1];
            triangleIndex2 = indices[3 * i + 2];

            //LEFT PLANE
            bool side0 = intersectsWithPlane(transformedVertices[indices[3 * i]], transformedVertices[indices[3 * i + 1]], ref intersectPoint0, vertices[indices[3 * i]], vertices[indices[3 * i + 1]], m.transform, true, leftPlane);
            bool side1 = intersectsWithPlane(transformedVertices[indices[3 * i + 1]], transformedVertices[indices[3 * i + 2]], ref intersectPoint1, vertices[indices[3 * i + 1]], vertices[indices[3 * i + 2]], m.transform, true, leftPlane);
            bool side2 = intersectsWithPlane(transformedVertices[indices[3 * i]], transformedVertices[indices[3 * i + 2]], ref intersectPoint2, vertices[indices[3 * i]], vertices[indices[3 * i + 2]], m.transform, true, leftPlane);


            if (side0 && side1 && side2) // 3 intersections?
            {
                Debug.Log("3 sides hit");
                if (PlaneCollision.ApproximatelyEquals(intersectPoint0, intersectPoint1))
                {
                    
                }
                else if (PlaneCollision.ApproximatelyEquals(intersectPoint0, intersectPoint2))
                {
                   

                }
                else  // Edge1Point == Edge2 point
                {
                    
                }
            }
            // determine which side of triangle has 1 vertex
            // add vertex and indices to appropriate mesh
            // for side with 2, add vertices, add 2 triangles
            else if (side0 && side1) // 2 intersections
            {
                intersect0Index = updatedVertices.Count;
                intersect1Index = intersect0Index + 1;
                updatedVertices.Add(intersectPoint0);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                updatedVertices.Add(intersectPoint1);

                if (OnNormalSideLeft(transformedVertices[indices[3 * i + 1]]))
                {

                    // Add the indices for various triangles to selected and unselected

                    AddNewIndices(selectedIndices, triangleIndex1, intersect0Index, intersect1Index);
                    AddNewIndices(unselectedIndices, intersect1Index, intersect0Index, triangleIndex0);
                    AddNewIndices(unselectedIndices, intersect1Index, triangleIndex0, triangleIndex2);

                }
                else
                {
                    AddNewIndices(unselectedIndices, triangleIndex1, intersect0Index, intersect1Index);
                    AddNewIndices(selectedIndices, intersect1Index, intersect0Index, triangleIndex0);
                    AddNewIndices(selectedIndices, intersect1Index, triangleIndex0, triangleIndex2);
                }
            }
            else if (side0 && side2)
            {
                intersect0Index = updatedVertices.Count;
                intersect2Index = intersect0Index + 1;
                updatedVertices.Add(intersectPoint0);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                updatedVertices.Add(intersectPoint2);

                if (OnNormalSideLeft(transformedVertices[indices[3 * i]]))
                {
                    AddNewIndices(selectedIndices, intersect0Index, triangleIndex0, intersect2Index);
                    AddNewIndices(unselectedIndices, intersect0Index, intersect2Index, triangleIndex2);
                    AddNewIndices(unselectedIndices, intersect0Index, triangleIndex2, triangleIndex1);
                }
                else
                {
                    AddNewIndices(unselectedIndices, intersect0Index, triangleIndex0, intersect2Index);
                    AddNewIndices(selectedIndices, intersect0Index, intersect2Index, triangleIndex2);
                    AddNewIndices(selectedIndices, intersect0Index, triangleIndex2, triangleIndex1);
                }
            }
            else if (side1 && side2)
            {
                intersect1Index = updatedVertices.Count;
                intersect2Index = intersect1Index + 1;
                updatedVertices.Add(intersectPoint1);   // Add intersection points (IN LOCAL SPACE) to vertex list, keep track of which indices they've been placed at
                updatedVertices.Add(intersectPoint2);

                if (OnNormalSideLeft(transformedVertices[indices[3 * i + 2]]))
                {
                    AddNewIndices(selectedIndices, intersect2Index, triangleIndex2, intersect1Index);
                    AddNewIndices(unselectedIndices, intersect1Index, triangleIndex0, intersect2Index);
                    AddNewIndices(unselectedIndices, intersect1Index, triangleIndex1, triangleIndex0);
                }
                else
                {
                    AddNewIndices(unselectedIndices, intersect2Index, triangleIndex2, intersect1Index);
                    AddNewIndices(selectedIndices, intersect1Index, triangleIndex0, intersect2Index);
                    AddNewIndices(selectedIndices, intersect1Index, triangleIndex1, triangleIndex0);
                }
            }
            else // 0 intersections
            {
                if (OnNormalSideLeft(transformedVertices[indices[3 * i]])) // do we need to order these/add to a graph?
                {
                    AddNewIndices(selectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                }
                else
                {
                    AddNewIndices(unselectedIndices, triangleIndex0, triangleIndex1, triangleIndex2);
                }
            }

        };
        //TODO: repeat with right plane
        // remove previously selected triangles




        //TODO: add selected vertices to mesh as submesh 1
        // add unselected as submesh 2
        // check for multiple existing submeshes?
        // figure out what to do about submesh 0 (the original)

        // mesh.SetTriangles(selected.ToArray(), 1);

        mesh.subMeshCount = 3;
        mesh.SetVertices(updatedVertices);
        try
        {
            mesh.SetTriangles(selectedIndices, 1);
            mesh.SetTriangles(unselectedIndices, 2);
        }
        catch (Exception e)
        {
            Debug.Log("Number of vertices: " + updatedVertices.Count + "\nNumber of selected indices: " + selectedIndices.Count + "\nNumber of unselected indices: " + unselectedIndices.Count);
            Debug.Log("Last value in selectedIndices: " + selectedIndices.ElementAt(selectedIndices.Count - 1)  +"\nLast val in unselected: " + unselectedIndices.ElementAt(unselectedIndices.Count - 1));

        }
       

        Material[] materials = new Material[3];
        materials[0] = m.GetComponent<Renderer>().material;     // May need to specify which submesh we get this from?
        materials[1] = Resources.Load("TestMaterial") as Material;
        materials[2] = materials[0];
        m.GetComponent<Renderer>().materials = materials;

        selectedIndices.Clear();
        unselectedIndices.Clear();
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
        if(index0 > updatedVertices.Count || index1 > updatedVertices.Count || index2 > updatedVertices.Count || index0 < 0 || index1 < 0 || index2 < 0)
        {
            Debug.Log("Index out of bounds. Index0: " + index0 + " Index1: " + index1 + " Index2: " + index2);
            Debug.Log("Num vertices: " + updatedVertices.Count);
        }
        indices.Add(index0);
        indices.Add(index1);
        indices.Add(index2);
    }

    private bool intersectsWithPlane(Vector3 lineVertex0, Vector3 lineVertex1, ref Vector3 intersectPoint, Vector3 original0, Vector3 original1, Transform meshTransform, bool isLeftPlane, GameObject plane) // checks if a particular edge intersects with the plane, if true, returns point of intersection along edge
    { 
        Vector3 lineSegmentLocal = original1 - original0;
        float dot = Vector3.Dot(plane.transform.up, lineVertex1 - lineVertex0);
        Vector3 w = plane.transform.position - lineVertex0;
       

        float epsilon = 0.001f;
        if (Mathf.Abs(dot) > epsilon)
        {
            float factor = Vector3.Dot(plane.transform.up, w) / dot;
            if (factor >= 0f && factor <= 1f)
            {
                lineSegmentLocal = factor * lineSegmentLocal;
                intersectPoint = original0 + lineSegmentLocal;

                Vector3 worldIntersect = meshTransform.TransformPoint(intersectPoint);

                float u, v;
                if (isLeftPlane)
                {
                    u = Vector3.Dot(worldIntersect - corner0Left, leftPlaneUVec) / leftULength;
                    v = Vector3.Dot(worldIntersect - corner2Left, leftPlaneVVec) / leftVLength;
                }
                else
                {
                    u = Vector3.Dot(worldIntersect - corner0Right, rightPlaneUVec) / rightULength;
                    v = Vector3.Dot(worldIntersect - corner2Right, rightPlaneVVec) / rightVLength;
                }

                // UnityEngine.Debug.Log("U: " + u + " V: " + v);

                return u >= 0.0f && u <= 1.0f && v >= 0.0f && v <= 1.0f; // If this works out, we probs want to pass it along to be reused later
            }
        }
        return false;
    }

    // returns true if at least one plane intersects with point
    private bool intersectsWithPlanes(Vector3 lineVertex0, Vector3 lineVertex1, ref Vector3 intersectPoint, Vector3 original0, Vector3 original1, Transform meshTransform)
    {
        return intersectsWithPlane(lineVertex0, lineVertex1, ref intersectPoint, original0, original1, meshTransform, true, leftPlane) || intersectsWithPlane(lineVertex0, lineVertex1, ref intersectPoint, original0, original1, meshTransform, false, rightPlane);
    }
}
