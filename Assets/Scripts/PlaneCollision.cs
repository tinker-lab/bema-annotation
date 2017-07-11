using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlaneCollision : MonoBehaviour {

    private HashSet<Collider> meshObjects;
    private List<List<Vector3>> processedPoints;

    //private GameObject crossSection;

	// Use this for initialization
	void Start () {
        meshObjects = new HashSet<Collider>();
    }

    // Update is called once per frame
    void Update()
    {
        processedPoints = processMeshes();
        if (processedPoints.Count != 0)
        {
            //HashSet<EdgeInfo> firstMeshEdges = processedPoints.ElementAt(0); // the following loop is an attempt to create spheres at each intersection point in the first mesh of the list

            //foreach (EdgeInfo e in firstMeshEdges) // works when processMeshes returns AllCheckedEdges
            //{
            //    GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //    s.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            //    s.transform.position = e.IntersectionPoint;
            //}

            // for each of the ordered lists of points (1 per intersected mesh), draw them in a new game object
            foreach (List<Vector3> meshPoints in processedPoints)
            {   
                GameObject crossSection = new GameObject();
                crossSection.AddComponent<MeshFilter>();
                crossSection.AddComponent<MeshRenderer>();
                crossSection.GetComponent<MeshRenderer>().material = Resources.Load("TestMaterial") as Material;
                crossSection.GetComponent<MeshFilter>().mesh = CreateMesh(meshPoints);
            }
            
        }
        else
        {
            print("List is empty"); //???
        }
	}

    // meshObjects contains only those objects that are currently intersected
    void OnTriggerEnter(Collider other) //use stay?
    {
        meshObjects.Add(other);
    }

    void OnTriggerExit(Collider other)
    {
        meshObjects.Remove(other);
    }

    // Orders the points of one mesh
    List<Vector3> BFSOrderPoints(Dictionary<EdgeInfo, List<EdgeInfo>> meshEdges)
    {
        HashSet<EdgeInfo> alreadyVisited = new HashSet<EdgeInfo>();
        List<Vector3> orderedPoints = new List<Vector3>();

        foreach (EdgeInfo edge in meshEdges.Keys)
        {
            if (!alreadyVisited.Contains(edge))
            {
                BFSVisit(edge, meshEdges, ref alreadyVisited, ref orderedPoints);
            }
        }

        return orderedPoints;
        
        //List<Vector3> unorderedList = new List<Vector3>();    // uncomment this and comment out the rest of the method to use the original unordered points
        //foreach(EdgeInfo edge in meshEdges.Keys)
        //{
        //    unorderedList.Add(edge.IntersectionPoint);
        //}
        //return unorderedList;
    }

    // Basic BFS, adds the intersection points of edges in the order it visits them
    void BFSVisit(EdgeInfo edge, Dictionary<EdgeInfo, List<EdgeInfo>> connectedEdges, ref HashSet<EdgeInfo> alreadyVisited, ref List<Vector3> orderedPoints)
    {
        alreadyVisited.Add(edge);
        orderedPoints.Add(edge.IntersectionPoint);
        Queue<EdgeInfo> nextUp = new Queue<EdgeInfo>();

        nextUp.Enqueue(edge);
        while(nextUp.Count != 0)
        {
            EdgeInfo currentEdge = nextUp.Dequeue();
            foreach (EdgeInfo otherEdge in connectedEdges[currentEdge])
            {
                if (!alreadyVisited.Contains(otherEdge))
                {
                    alreadyVisited.Add(otherEdge);
                    orderedPoints.Add(otherEdge.IntersectionPoint);
                    nextUp.Enqueue(otherEdge);
                }
            }
        }
    }

    List<List<Vector3>> processMeshes() // each item in the list is a list of points corresponding to the intersection points of each mesh
    {
        List<List<Vector3>> intersectionPoints = new List<List<Vector3>>(); // List of all the points for each element, in the order they should be drawn
        List<List<EdgeInfo>> allCheckedEdges = new List<List<EdgeInfo>>();
        List<List<int>> allIndexConnections = new List<List<int>>();      // Key: index, Value: List of indices it's connected to
        Bounds bound = GetComponent<MeshCollider>().bounds;

        foreach (Collider m in meshObjects)
        {
            Mesh mesh = m.GetComponent<MeshFilter>().mesh;
            List<EdgeInfo> checkedEdges = new List<EdgeInfo>();
            Dictionary<EdgeInfo, List<EdgeInfo>> edgeConnections = new Dictionary<EdgeInfo, List<EdgeInfo>>();  // Graph where each node/key is an edgeinfo and the value associated with it is all the other edges it has an index in common with (connected to) 
            List<Vector3> vertices = new List<Vector3>();

            int[] indices = mesh.GetTriangles(0); // 0 refers to first set of triangles (because there is only one material on the mesh)
            mesh.GetVertices(vertices);

            for (int i = 0; i < indices.Length; i += 3)
            {   
                // TODO: work on boundaries

                //if (bound.Contains(m.transform.TransformPoint(vertices.ElementAt(indices[i]))) || bound.Contains(m.transform.TransformPoint(vertices.ElementAt(indices[i+1])) ) || bound.Contains(m.transform.TransformPoint(vertices.ElementAt(indices[i+2]) )))
                //{
                    Triangle triangle = new Triangle(indices[i], indices[i + 1], indices[i + 2]);

                    AddEdge(triangle.Edge0, ref checkedEdges, ref edgeConnections, vertices, triangle); 
                    AddEdge(triangle.Edge1, ref checkedEdges, ref edgeConnections, vertices, triangle);
                    AddEdge(triangle.Edge2, ref checkedEdges, ref edgeConnections, vertices, triangle);
                //}
                //else
                //{
                //    Debug.LogError("No vertex within bounds");
                    //Debug.LogWarning(bound);
                //}
                //Debug.Log(m.transform.TransformPoint(vertices.ElementAt(indices[i])) + "point at" + i);
              
            }

            List<Vector3> orderedMeshPoints = BFSOrderPoints(edgeConnections);
            intersectionPoints.Add(orderedMeshPoints);
            allCheckedEdges.Add(checkedEdges);
        }

        return intersectionPoints;
    }

    private void AddEdge(EdgeInfo edge, ref List<EdgeInfo> checkedEdges, ref Dictionary<EdgeInfo, List<EdgeInfo>> edgeConnections, List<Vector3> vertices, Triangle t)
        // Adds information related to each edge (EdgeInfo) into a connected graph (EdgeConnections)
    {
        Vector3 intersectPoint = new Vector3();
        int edgeIndex0 = edge.Index0;
        int edgeIndex1 = edge.Index1;

        if (!checkedEdges.Contains(edge))
        {
            if (intersectsWithPlane(vertices.ElementAt(edgeIndex0), vertices.ElementAt(edgeIndex1), ref intersectPoint))
            {
                if (!edgeConnections.ContainsKey(edge)) { edgeConnections.Add(edge, new List<EdgeInfo>()); }    // If there isn't already an entry for this edgeinfo, add one

                edge.IntersectionPoint = intersectPoint;

                foreach(EdgeInfo otherEdge in edgeConnections.Keys)
                {
                    if (edge.HasCommonIndexWith(otherEdge) && !edge.Equals(otherEdge))    // IF this edge doesn't have a record of other edge, has an index in common, and isn't the same edge
                    {
                        edgeConnections[edge].Add(otherEdge);
                        edgeConnections[otherEdge].Add(edge);
                    }
                }
                checkedEdges.Add(edge);
            }
        }
        else
        {
            edge.addParent(t);
        }
    }

    private bool intersectsWithPlane(Vector3 lineVertex0, Vector3 lineVertex1, ref Vector3 intersectPoint) // checks if a particular edge intersects with the plane, if true, returns point of intersection along edge
    {
        Vector3 lineSegment = lineVertex1 - lineVertex0;
        float dot = Vector3.Dot(gameObject.transform.up, lineSegment);

        float epsilon = 0.001f;
        if (Mathf.Abs(dot) > epsilon)
        {
            float factor = -Vector3.Dot(gameObject.transform.up, lineVertex0) / dot;
            if (factor >= 0f && factor <= 1f)
            {
                lineSegment = factor * lineSegment;
                intersectPoint = lineVertex0 + lineSegment;
                return true;
            }
        }

        return false;
    }

    private Mesh CreateMesh(List<Vector3> points) // copied from EdgeSelectionState for testing purposes, not ideal for final version
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> faces = new List<int>();
        List<Vector2> uvCoordinates = new List<Vector2>();

        GameObject viewPlane = GameObject.Find("ViewPlane");

        float radius = .005f;

        Vector3 previousRight = new Vector3(0, 0, 0);

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 centerStart = points.ElementAt(i);
            Vector3 direction;
            if (i == points.Count - 1)
            {
                direction = points.ElementAt(i) - points.ElementAt(i - 1);  //NOTE: Threw exception when I made the curve incredibly small
            }
            else
            {
                direction = points.ElementAt(i + 1) - points.ElementAt(i);
            }

            Vector3 right = Vector3.Cross(viewPlane.transform.up, direction);

            Vector3 up = Vector3.Cross(direction, right);
            up = up.normalized * radius;
            right = right.normalized * radius;

            int numSections = 10;

            for (int slice = 0; slice <= numSections; slice++)
            {
                float theta = (float)slice / (float)numSections * 2.0f * Mathf.PI;
                Vector3 p = centerStart + right * Mathf.Sin(theta) + up * Mathf.Cos(theta);


                vertices.Add(p);
                uvCoordinates.Add(new Vector2((float)slice / (float)numSections, (float)i / (float)points.Count));


                if (slice > 0 && i > 0)
                {
                    faces.Add(slice + ((numSections + 1) * (i - 1)));
                    faces.Add(slice - 1 + ((numSections + 1) * (i - 1)));
                    faces.Add(slice + ((numSections + 1) * i));

                    faces.Add(slice + ((numSections + 1) * i));
                    faces.Add(slice - 1 + ((numSections + 1) * (i - 1)));
                    faces.Add(slice - 1 + ((numSections + 1) * i));
                }
            }
        }
        Mesh selectorMesh = new Mesh();
        selectorMesh.vertices = vertices.ToArray();
        selectorMesh.uv = uvCoordinates.ToArray();
        selectorMesh.triangles = faces.ToArray();

        selectorMesh.RecalculateBounds();
        selectorMesh.RecalculateNormals();
   
        return selectorMesh;
    }

}
