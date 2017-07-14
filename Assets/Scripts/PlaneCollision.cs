﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class PlaneCollision : MonoBehaviour
{

    private HashSet<Collider> meshObjects;
    private HashSet<GameObject> crossSections;
    private Dictionary<List<Vector3>, GameObject> processedPoints;

    private Boolean firstTime;

    //private GameObject crossSection;

    // Use this for initialization
    void Start()
    {
        meshObjects = new HashSet<Collider>();
        crossSections = new HashSet<GameObject>();

        firstTime = false;
    }

    // Update is called once per frame
    void Update()
    {
        processedPoints = processMeshes();
        if (processedPoints.Count != 0)// && firstTime == false)
        {
            firstTime = true;
            // for each of the ordered lists of points (1 per intersected mesh), draw them in a new game object
            foreach (List<Vector3> meshPoints in processedPoints.Keys)
            {
                GameObject crossSection = GameObject.Find(processedPoints[meshPoints].name + " crosssection");  // Find crossSection associated with the colliding mesh
                crossSection.GetComponent<MeshFilter>().mesh = CreateMesh(meshPoints);
                crossSection.transform.position = processedPoints[meshPoints].transform.position;
                crossSection.transform.localScale = processedPoints[meshPoints].transform.localScale;
                crossSection.transform.rotation = processedPoints[meshPoints].transform.rotation;   // or localrotation?

                //foreach (Vector3 pt in meshPoints)
                //{
                //    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                //    sphere.transform.position = pt;
                //    sphere.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                //}
            }
        }
    }

    // meshObjects contains only those objects that are currently intersected
    void OnTriggerEnter(Collider other) //use stay?
    {
        if (!other.transform.parent.name.Equals("TestPlanes") && !GameObject.Find(other.name + " crosssection"))
        {
            meshObjects.Add(other);

            GameObject crossSection = new GameObject();
            crossSection.AddComponent<MeshFilter>();
            crossSection.AddComponent<MeshRenderer>();
            crossSection.GetComponent<MeshRenderer>().material = Resources.Load("TestMaterial") as Material;
            crossSection.name = other.name + " crosssection";

            Debug.Log("Parent of thing being collided with: " + other.transform.parent.name);
        }
    }

    void OnTriggerExit(Collider other)
    {
        meshObjects.Remove(other);
        GameObject.Destroy(GameObject.Find(other.name + " crosssection").gameObject);        // Might want to have a bunch of gameobjects that we maintain and rename
    }

    public static bool ApproximatelyEquals(Vector3 vector0, Vector3 vector1)
    {
        float epsilon = 0.00001f;

        return (Mathf.Abs(vector0.x - vector1.x) < epsilon && Mathf.Abs(vector0.y - vector1.y) < epsilon && Mathf.Abs(vector0.z- vector1.z) < epsilon);
    }

    // Orders the points of one mesh. NOTE: currently just uses alreadyVisited HashSet, nothing else;
    List<Vector3> DFSOrderPoints(Dictionary<int, HashSet<int>> pointConnections, List<IntersectingPoint> actualPoints)
    {
        HashSet<int> alreadyVisited = new HashSet<int>();
        List<Vector3> orderedPoints = new List<Vector3>();

        foreach (int pt in pointConnections.Keys)
        {
            if (!alreadyVisited.Contains(pt))
            {
                //TODO: make a new list for ordered points here to pass in
                DFSVisit(pt, pointConnections, ref alreadyVisited, ref orderedPoints, actualPoints);
            }
        }

        return orderedPoints;
    }

    // Basic BFS, adds the intersection points of edges in the order it visits them
    void DFSVisit(int pt, Dictionary<int, HashSet<int>> connectedEdges, ref HashSet<int> alreadyVisited, ref List<Vector3> orderedPoints, List<IntersectingPoint> actualPoints)
    {
        alreadyVisited.Add(pt);
        orderedPoints.Add(actualPoints.ElementAt(pt).Point);
        //Stack<int> nextUp = new Stack<int>();

        //nextUp.Push(pt);
        //while (nextUp.Count != 0)
        //{
            //int currentIndex = nextUp.Pop();
        foreach (int otherIndex in connectedEdges[pt])//[currentIndex])
        {
            if (!alreadyVisited.Contains(otherIndex))
            {
              
            //nextUp.Push(otherIndex);
                DFSVisit(otherIndex, connectedEdges, ref alreadyVisited, ref orderedPoints, actualPoints);
            }
        }
        //}
    }

    Dictionary<List<Vector3>, GameObject> processMeshes() // each item in the list is a list of points corresponding to the intersection points of each mesh
    {
        Dictionary<List<Vector3>, GameObject> intersectionPoints = new Dictionary<List<Vector3>, GameObject>(); // dictionary: keys are all the points for each element, in the order they should be drawn, values are the scale
        Bounds bound = GetComponent<MeshCollider>().bounds;

        foreach (Collider m in meshObjects)
        {
            Mesh mesh = m.GetComponent<MeshFilter>().mesh;
            List<IntersectingPoint> points = new List<IntersectingPoint>();     // Information about each point in a mesh (including whether or not it has been seen)
            Dictionary<int, HashSet<int>> pointConnections = new Dictionary<int, HashSet<int>>();  // Graph where each node/key is the index of a point in POINTS and the value associated with it is the indices of all the points it is connected to in POINTS
            List<Vector3> vertices = new List<Vector3>();

            mesh.GetVertices(vertices);
            int[] indices = mesh.GetTriangles(0); // 0 refers to first set of triangles (because there is only one material on the mesh)

            Dictionary<EdgeInfo, Vector3> seenEdges = new Dictionary<EdgeInfo, Vector3>();

            List<Vector3> intersectingEdges = new List<Vector3>();
            //   int intersectingPointIndex = 0;

            for (int i = 0; i < indices.Length; i += 3)
            {
                // TODO: work on boundaries

                //if (bound.Contains(m.transform.TransformPoint(vertices.ElementAt(indices[i]))) || bound.Contains(m.transform.TransformPoint(vertices.ElementAt(indices[i+1])))  || bound.Contains(m.transform.TransformPoint(vertices.ElementAt(indices[i+2]) ))) //m.transform.transformpoint does not work
                //{

                Vector3 intersectPoint0 = new Vector3();
                Vector3 intersectPoint1 = new Vector3();
                Vector3 intersectPoint2 = new Vector3();

                EdgeInfo edge0 = new EdgeInfo(indices[i], indices[i + 1]);
                EdgeInfo edge1 = new EdgeInfo(indices[i+1], indices[i + 2]);
                EdgeInfo edge2 = new EdgeInfo(indices[i], indices[i + 2]);


                //TODO: check if you have already processed an edge. If so, get it's intersection point.

                // for each edge check if intersects with plane
                // if 2 intersections then add the edge between them to the graph
                // if 1 intersection then it goes through a vertex and we ignore it?
                // if 3 (potentially through a vertex) then check which two are the same. Add edge to graph

                bool side0;
                if (seenEdges.TryGetValue(edge0, out intersectPoint0))
                {
                    side0 = true;
                }
                else
                {
                    side0 = intersectsWithPlane(vertices.ElementAt(indices[i]), vertices.ElementAt(indices[i + 1]), m.gameObject, ref intersectPoint0);
                    seenEdges.Add(edge0, intersectPoint0);
                }

                bool side1;
                if (seenEdges.TryGetValue(edge1, out intersectPoint1))
                {
                    side1 = true;
                }
                else
                {
                    side1 = intersectsWithPlane(vertices.ElementAt(indices[i+1]), vertices.ElementAt(indices[i + 2]), m.gameObject, ref intersectPoint1);
                    seenEdges.Add(edge1, intersectPoint1);
                }

                bool side2;
                if (seenEdges.TryGetValue(edge2, out intersectPoint2))
                {
                    side2 = true;
                }
                else
                {
                    side2 = intersectsWithPlane(vertices.ElementAt(indices[i]), vertices.ElementAt(indices[i + 2]), m.gameObject, ref intersectPoint2);
                    seenEdges.Add(edge2, intersectPoint2);
                }


                if (side0) { intersectingEdges.Add(intersectPoint0); }
                if (side1) { intersectingEdges.Add(intersectPoint1); }
                if (side2) { intersectingEdges.Add(intersectPoint2); }

                switch (intersectingEdges.Count)
                {
                    case 0:
                        Debug.Log("0 edges hit");
                        break;
                    case 1:     // if 1 intersection then it goes through a vertex and we ignore it?
                        Debug.Log("1 edge hit");
                        break;
                    case 2:     // if 2 intersections then add the edge between them to the graph

                        AddToGraph(intersectingEdges.ElementAt(0), intersectingEdges.ElementAt(1), ref points, ref pointConnections);
                        Debug.Log("2 edges hit");
                        break;
                    case 3:     // if 3 (potentially through a vertex) then check which two are the same. Add edge to graph
                        if (ApproximatelyEquals(intersectPoint0, intersectPoint1))
                        {
                            AddToGraph(intersectPoint0, intersectPoint2, ref points, ref pointConnections);
                        }
                        else if (ApproximatelyEquals(intersectPoint0, intersectPoint2))
                        {
                            AddToGraph(intersectPoint0, intersectPoint1, ref points, ref pointConnections);

                        }
                        else  // Edge1Point == Edge2 point
                        {
                            AddToGraph(intersectPoint0, intersectPoint1, ref points, ref pointConnections);
                        }
                        Debug.Log("3 edges hit");       // NOTE: we are always hitting this case
                        break;
                }
                //}

                intersectingEdges.Clear();

                //}
                //else
                //{
                //    Debug.LogError("No vertex within bounds");
                //    Debug.LogWarning(bound);
                //}
                //Debug.Log(m.transform.TransformPoint(vertices.ElementAt(indices[i])) + "point at " + i);

            }
            List<Vector3> orderedMeshPoints = DFSOrderPoints(pointConnections, points);

            StringBuilder builder = new StringBuilder();
            foreach(Vector3 vec in orderedMeshPoints)
            {
                builder.Append(" ");
                builder.Append(vec.ToString());
            }

            Debug.LogError(builder.ToString());
            intersectionPoints.Add(orderedMeshPoints, m.gameObject);
        }
        return intersectionPoints;
    }

    // Takes two connected points and adds or updates entries in the list of actual points and the graph of their connections
    private void AddToGraph(Vector3 point0, Vector3 point1, ref List<IntersectingPoint> points, ref Dictionary<int, HashSet<int>> pointConnections)
    {
        IntersectingPoint intersect0 = new IntersectingPoint(point0);
        IntersectingPoint intersect1 = new IntersectingPoint(point1);
        int index0 = points.IndexOf(intersect0);
        int index1 = points.IndexOf(intersect1);

        if (index0 == -1)   // IndexOf returns -1 if it can't find an index     //
        {                                                                       //
            index0 = points.Count;                                              //    
            points.Add(intersect0);                                             //
        } 
        else
        {
            Debug.Log("p0 is already in the list");
        }
        // Check if each point is already in List of points
                                                                                // If not, figure out what index it will be in then add it to list
                                                                                // If so, simply find its index in the list
        if (index1 == -1)                                                       //
        {                                                                       //
            index1 = points.Count;                                              //
            points.Add(intersect1);                                             //
        }
        else
        {
            Debug.Log("p1 is already in the list");
        }//

        if (!pointConnections.ContainsKey(index0))
        {
            HashSet<int> connections = new HashSet<int>();
            connections.Add(index1);
            pointConnections.Add(index0, connections);
        }
        else
        {
            pointConnections[index0].Add(index1);
        }

        if (!pointConnections.ContainsKey(index1))
        {
            HashSet<int> connections = new HashSet<int>();
            connections.Add(index0);
            pointConnections.Add(index1, connections);
        }
        else
        {
            pointConnections[index1].Add(index0);
        }
    }

    private bool intersectsWithPlane(Vector3 lineVertex0, Vector3 lineVertex1, GameObject meshGameObject, ref Vector3 intersectPoint) // checks if a particular edge intersects with the plane, if true, returns point of intersection along edge
    { // TODO: check xy of plane
        Vector3 lineVertex0World = meshGameObject.transform.TransformPoint(lineVertex0);
        Vector3 lineSegmentLocal = lineVertex1 - lineVertex0;
        Vector3 lineSegmentWorld = meshGameObject.transform.TransformPoint(lineVertex1) - lineVertex0World;
        float dot = Vector3.Dot(transform.up, lineSegmentWorld);

        Vector3 w = transform.position - lineVertex0World;

        float epsilon = 0.001f;
        if (Mathf.Abs(dot) > epsilon)
        {
            float factor = Vector3.Dot(transform.up, w) / dot;
            if (factor >= 0f && factor <= 1f)
            {
                lineSegmentLocal = factor * lineSegmentLocal;
                intersectPoint = lineVertex0 + lineSegmentLocal;
                return true;
            }
        }
        return false;
    }

    private Mesh CreateMesh(List<Vector3> points) // copied from EdgeSelectionState for testing purposes, not ideal for final version
    {
        //TODO: This should actually take a list of lists. For each list you do the same thing we are doing below.
        // indicies will need to be updated for each additional list.

        List<Vector3> vertices = new List<Vector3>();
        List<int> faces = new List<int>();
        List<Vector2> uvCoordinates = new List<Vector2>();

        float radius = .005f;

        Vector3 previousRight = new Vector3(0, 0, 0);

        //foreach (List<Vector3> points in allPoints)

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

            Vector3 right = Vector3.Cross(transform.up, direction);

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