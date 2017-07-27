using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;


public class VectorComparer : EqualityComparer<Vector3>
{
    public override bool Equals(Vector3 x, Vector3 y)
    {
        return PlaneCollision.ApproximatelyEquals(x, y);      // Using == should be an approximate comparison
    }

    public override int GetHashCode(Vector3 obj)
    {
        //throw new NotImplementedException();        // Best way to deal with this?
        return obj.GetHashCode();
    }
}

public class PlaneCollision : MonoBehaviour
{

    private HashSet<Collider> meshObjects;
    private HashSet<GameObject> crossSections;
    private Dictionary<List<Vector3>, GameObject> processedPoints;

    private VectorComparer comparator;
    private Boolean firstTime;
    private Stopwatch internalStopwatch;
    private Stopwatch externalStopwatch;
    private Stopwatch timer;

    private Vector3 planeUVec;
    private Vector3 planeVVec;
    private float uLength;
    private float vLength;

    Vector3 corner0;
    Vector3 corner1;
    Vector3 corner2;
    Vector3 corner3;

    // Use this for initialization
    void Start()
    {
        meshObjects = new HashSet<Collider>();
        crossSections = new HashSet<GameObject>();
        comparator = new VectorComparer();

        firstTime = false;
        internalStopwatch = new Stopwatch();
        externalStopwatch = new Stopwatch();
        timer = new Stopwatch();

    }

    // Update is called once per frame
    void Update()
    {
        Vector3 centerOfPlane = transform.position;
        float sideLength = (10 * transform.localScale.x) / 2;

        corner0 = centerOfPlane - sideLength * transform.right.normalized - sideLength * transform.forward.normalized;
        corner1 = centerOfPlane + sideLength * transform.right.normalized - sideLength * transform.forward.normalized;
        corner2 = centerOfPlane - sideLength * transform.right.normalized + sideLength * transform.forward.normalized;
        
        planeUVec = corner1 - corner0;
        planeVVec = corner0 - corner2;

        uLength = planeUVec.magnitude;
        vLength = planeVVec.magnitude;
        planeUVec = Vector3.Normalize(planeUVec);
        planeVVec = Vector3.Normalize(planeVVec);

        processedPoints = processMeshes();
        if (processedPoints.Count != 0)// && firstTime == false)
        {
            firstTime = true;
            // for each of the ordered lists of points (1 per intersected mesh), draw them in a new game object
            foreach (List<Vector3> meshPoints in processedPoints.Keys)
            {
                if (meshPoints.Count > 1)
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
                    //    sphere.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
                    //}
                }
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

            UnityEngine.Debug.Log("Parent of thing being collided with: " + other.transform.parent.name);
        }
    }

    void OnTriggerExit(Collider other)
    {
        meshObjects.Remove(other);
        GameObject.Destroy(GameObject.Find(other.name + " crosssection").gameObject);        // Might want to have a bunch of gameobjects that we maintain and rename
    }

    public static bool ApproximatelyEquals(Vector3 vector0, Vector3 vector1)
    {
        float epsilon = 0.001f;

        return (Mathf.Abs(vector0.x - vector1.x) < epsilon && Mathf.Abs(vector0.y - vector1.y) < epsilon && Mathf.Abs(vector0.z- vector1.z) < epsilon);
    }

    // Orders the points of one mesh. NOTE: currently just uses alreadyVisited HashSet, nothing else;
    List<Vector3> DFSOrderPoints(Dictionary<Vector3, HashSet<Vector3>> pointConnections)
    {
        HashSet<Vector3> alreadyVisited = new HashSet<Vector3>();
        List<Vector3> orderedPoints = new List<Vector3>();

        foreach (Vector3 pt in pointConnections.Keys)
        {
            if (!alreadyVisited.Contains(pt))
            {
                //TODO: make a new list for ordered points here to pass in
                DFSVisit(pt, pointConnections, ref alreadyVisited, ref orderedPoints);
            }
        }

        return orderedPoints;
    }

    // Basic BFS, adds the intersection points of edges in the order it visits them
    void DFSVisit(Vector3 pt, Dictionary<Vector3, HashSet<Vector3>> connectedEdges, ref HashSet<Vector3> alreadyVisited, ref List<Vector3> orderedPoints)
    {
        alreadyVisited.Add(pt);
        orderedPoints.Add(pt);
        //Stack<int> nextUp = new Stack<int>();

        //nextUp.Push(pt);
        //while (nextUp.Count != 0)
        //{
            //int currentIndex = nextUp.Pop()
        foreach (Vector3 otherIndex in connectedEdges[pt])//[currentIndex])
        {
            if (!alreadyVisited.Contains(otherIndex))
            {
              
            //nextUp.Push(otherIndex);
                DFSVisit(otherIndex, connectedEdges, ref alreadyVisited, ref orderedPoints);
            }
        }
        //}
    }

    Dictionary<List<Vector3>, GameObject> processMeshes() // each item in the list is a list of points corresponding to the intersection points of each mesh
    {
 /*TS*/ Dictionary<List<Vector3>, GameObject> intersectionPoints = new Dictionary<List<Vector3>, GameObject>(); // dictionary: keys are all the points for each element, in the order they should be drawn, values are the scale
        Bounds bound = GetComponent<MeshCollider>().bounds;

        Vector3 intersectPoint0 = new Vector3();
        Vector3 intersectPoint1 = new Vector3();
        Vector3 intersectPoint2 = new Vector3();

        foreach (Collider m in meshObjects)
        {
            Mesh mesh = m.GetComponent<MeshFilter>().mesh;
            Dictionary<Vector3, HashSet<Vector3>> pointConnections = new Dictionary<Vector3, HashSet<Vector3>>(comparator);  // Graph where each node/key is the index of a point in POINTS and the value associated with it is the indices of all the points it is connected to in POINTS

            Vector3[] vertices = mesh.vertices;
            int[] indices = mesh.triangles; // 0 refers to first set of triangles (because there is only one material on the mesh)

            Dictionary<EdgeInfo, Vector3> seenEdges = new Dictionary<EdgeInfo, Vector3>(vertices.Length);

            Vector3[] transformedVertices = new Vector3[vertices.Length];
            for (int i =0; i < vertices.Length; i++)
            {
                transformedVertices[i] = m.gameObject.transform.TransformPoint(vertices[i]);
            }

            externalStopwatch.Start();

            // TODO: switch up data structures
            // preprocess points so that TransformPoint isn't called in IntersectsWithPlane
            // 

            long initTime = 0;
            long intersectTime = 0;
            long graphTime = 0;


            int numTriangles = (int)(indices.Length / 3);
            UnityEngine.Debug.Log("Num triangles: " + numTriangles);
            Parallel.SerialFor(0, (int)(indices.Length / 3), i =>
            {

                timer.Start();


                bool side0 = intersectsWithPlane(transformedVertices[indices[3*i]], transformedVertices[indices[3*i + 1]], ref intersectPoint0, vertices[indices[3*i]], vertices[indices[3 * i + 1]], m.transform);
                bool side1 = intersectsWithPlane(transformedVertices[indices[3*i + 1]], transformedVertices[indices[3*i + 2]], ref intersectPoint1, vertices[indices[3 * i + 1]], vertices[indices[3 * i + 2]], m.transform);
                bool side2 = intersectsWithPlane(transformedVertices[indices[3*i]], transformedVertices[indices[3*i + 2]], ref intersectPoint2, vertices[indices[3 * i]], vertices[indices[3 * i + 2]], m.transform);
                
                timer.Stop();
                intersectTime += timer.ElapsedMilliseconds;

                timer.Reset();
                timer.Start();
                
                if (side0 && side1 && side2) {
                    if (ApproximatelyEquals(intersectPoint0, intersectPoint1))
                    {
                        AddToGraph(intersectPoint0, intersectPoint2, ref pointConnections);
                    }
                    else if (ApproximatelyEquals(intersectPoint0, intersectPoint2))
                    {
                        AddToGraph(intersectPoint0, intersectPoint1, ref pointConnections);

                    }
                    else  // Edge1Point == Edge2 point
                    {
                        AddToGraph(intersectPoint0, intersectPoint1, ref pointConnections);
                    }
                }
                else if(side0 && side1) {
                    AddToGraph(intersectPoint0, intersectPoint1, ref pointConnections);
                }
                else if(side0 && side2) {
                    AddToGraph(intersectPoint0, intersectPoint2, ref pointConnections);
                }
                else if(side1 && side2)
                {
                    AddToGraph(intersectPoint1, intersectPoint2, ref pointConnections);
                }

               
                timer.Stop();
                graphTime += timer.ElapsedMilliseconds;
                timer.Reset();
            });

           

            UnityEngine.Debug.Log("InitTime: " + initTime + " intersectTime: " + intersectTime + " graphTime: " + graphTime);
            
            externalStopwatch.Stop();
            UnityEngine.Debug.Log("Time taken to process one mesh: " + externalStopwatch.ElapsedMilliseconds + "ms");

            externalStopwatch.Reset();

            // TODO: wait until all other threads finish
            internalStopwatch.Start();
            List<Vector3> orderedMeshPoints = DFSOrderPoints(pointConnections);
            orderedMeshPoints = RemoveSequentialDuplicates(orderedMeshPoints);
            internalStopwatch.Stop();
            UnityEngine.Debug.Log("Time taken for DFS of one mesh: " + internalStopwatch.ElapsedMilliseconds + "ms");
            internalStopwatch.Reset();

            intersectionPoints.Add(orderedMeshPoints, m.gameObject);
            
        }
        return intersectionPoints;
    }

    // Takes two connected points and adds or updates entries in the list of actual points and the graph of their connections
    private void AddToGraph(Vector3 point0, Vector3 point1, ref Dictionary<Vector3, HashSet<Vector3>> pointConnections)
    {

            if (!pointConnections.ContainsKey(point0))
            {
                HashSet<Vector3> connections = new HashSet<Vector3>();
                connections.Add(point1);
                pointConnections.Add(point0, connections);
            }
            else
            {
                pointConnections[point0].Add(point1);
            }

            if (!pointConnections.ContainsKey(point1))
            {
                HashSet<Vector3> connections = new HashSet<Vector3>();
                connections.Add(point0);
                pointConnections.Add(point1, connections);
            }
            else
            {
                pointConnections[point1].Add(point0);
            }
        
    }

    private bool intersectsWithPlane(Vector3 lineVertex0, Vector3 lineVertex1, ref Vector3 intersectPoint, Vector3 original0, Vector3 original1, Transform meshTransform) // checks if a particular edge intersects with the plane, if true, returns point of intersection along edge
    { // TODO: check xy of plane
       // Vector3 lineVertex0World = meshGameObject.transform.TransformPoint(lineVertex0);        // How to replace TransformPoint for multithreading?
       Vector3 lineSegmentLocal = original1 - original0;
     //   Vector3 lineSegmentWorld = meshGameObject.transform.TransformPoint(lineVertex1) - lineVertex0World;
        float dot = Vector3.Dot(transform.up, lineVertex1 - lineVertex0);

        Vector3 w = transform.position - lineVertex0;

        float epsilon = 0.001f;
        if (Mathf.Abs(dot) > epsilon)
        {
            float factor = Vector3.Dot(transform.up, w) / dot;
            if (factor >= 0f && factor <= 1f)
            {
                lineSegmentLocal = factor * lineSegmentLocal;
                intersectPoint = original0 + lineSegmentLocal;

                Vector3 worldIntersect = meshTransform.TransformPoint(intersectPoint);
                float u = Vector3.Dot(worldIntersect - corner0, planeUVec)/uLength;
                float v  = Vector3.Dot(worldIntersect - corner2, planeVVec)/vLength;

               // UnityEngine.Debug.Log("U: " + u + " V: " + v);

                return u >=0.0f && u <=1.0f && v >=0.0f && v <=1.0f; // If this works out, we probs want to pass it along to be reused later
            }
        }
        return false;
    }

    private List<Vector3> RemoveSequentialDuplicates(List<Vector3> points)
    {
        List<Vector3> output = new List<Vector3>(points.Count);
        List<Vector3>.Enumerator it = points.GetEnumerator();
        it.MoveNext();
        Vector3 prev = it.Current;
        while (it.MoveNext())
        {
            if (!ApproximatelyEquals(it.Current, prev)) {
                output.Add(it.Current);
            }
            prev = it.Current;
        }
        return output;
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
                try
                {
                    Vector3 I = points.ElementAt(i);
                    Vector3 iMinusOne = points.ElementAt(i - 1);
                }
                catch (ArgumentOutOfRangeException)
                {
                    UnityEngine.Debug.LogError("Argument out of range.");
                    UnityEngine.Debug.LogError("Length of point list: " + points.Count + " i= " + i + ", " + (i - 1));
                }

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