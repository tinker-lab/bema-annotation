using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlaneCollision : MonoBehaviour {

    private HashSet<Mesh> meshes;
    private List<HashSet<EdgeInfo>> processedPoints;

	// Use this for initialization
	void Start () {
        meshes = new HashSet<Mesh>();
	}

    // Update is called once per frame
    void Update()
    {
        processedPoints = processMeshes();
        // foreach(HashSet<EdgeInfo> )
        if (processedPoints != null) { 
        HashSet<EdgeInfo> firstMeshEdges = processedPoints.ElementAt(0);

        foreach (EdgeInfo e in firstMeshEdges)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            s.transform.position = e.IntersectionPoint;
        }
    }
	}

    void OnTriggerEnter(Collider other) //use stay?
    {
        Mesh otherMesh = other.GetComponent<MeshFilter>().mesh;
        meshes.Add(otherMesh);

    }

    void OnTriggerExit(Collider other)
    {
        meshes.Remove(other.GetComponent<MeshFilter>().mesh);
    }

    List<HashSet<EdgeInfo>> processMeshes()
    {
        List<List<Vector3>> intersectionPoints = new List<List<Vector3>>();
        List<HashSet<EdgeInfo>> allCheckedEdges = new List<HashSet<EdgeInfo>>();
        List<Dictionary<int, List<int>>> allIndexConnections = new List<Dictionary<int, List<int>>>();

        foreach (Mesh mesh in meshes)
        {
            HashSet<EdgeInfo> checkedEdges = new HashSet<EdgeInfo>();
            Dictionary<int, List<int>> indexConnections = new Dictionary<int, List<int>>();
            List<Vector3> vertices = new List<Vector3>();

            int[] indices = mesh.GetTriangles(0);
            mesh.GetVertices(vertices);

            for (int i = 0; i < indices.Length; i += 3)
            {
                Triangle triangle = new Triangle(indices[i], indices[i + 1], indices[i + 2]);

                AddEdge(triangle.Edge0, ref checkedEdges, ref indexConnections, vertices, triangle);
                AddEdge(triangle.Edge1, ref checkedEdges, ref indexConnections, vertices, triangle);
                AddEdge(triangle.Edge2, ref checkedEdges, ref indexConnections, vertices, triangle);
            }

            allIndexConnections.Add(indexConnections);
            allCheckedEdges.Add(checkedEdges);
        }

        return allCheckedEdges;// allIndexConnections? allCheckedEdges? Both?
    }

    private void AddEdge(EdgeInfo edge, ref HashSet<EdgeInfo> checkedEdges, ref Dictionary<int, List<int>> indexConnections, List<Vector3> vertices, Triangle t)
    {
        Vector3 intersectPoint = new Vector3();
        int edgeIndex0 = edge.Index0;
        int edgeIndex1 = edge.Index1;

        if (!checkedEdges.Contains(edge))
        {
            if (intersectsWithPlane(vertices.ElementAt(edgeIndex0), vertices.ElementAt(edgeIndex1), ref intersectPoint))
            {
                //meshIntersections.Add(intersect0);
                if (!indexConnections.ContainsKey(edgeIndex0)) { indexConnections.Add(edgeIndex0, new List<int>()); }
                if (!indexConnections.ContainsKey(edgeIndex1)) { indexConnections.Add(edgeIndex1, new List<int>()); }

                indexConnections[edgeIndex0].Add(edgeIndex1);
                indexConnections[edgeIndex1].Add(edgeIndex0);

                edge.IntersectionPoint = intersectPoint;
                checkedEdges.Add(edge);
            }
        }
        else
        {
            edge.addParent(t);
        }

        // TOOD: where is intersect point going?
    }

    private bool intersectsWithPlane(Vector3 lineVertex0, Vector3 lineVertex1, ref Vector3 intersectPoint)
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

    public List<HashSet<EdgeInfo>> GetProcessedPoints()
    {
        return processedPoints;
    }


    /*
    private bool intersectsWithPlane(Triangle t, List<Vector3> vertices)
    {
        Vector3 vertex00 = vertices.ElementAt(t.Edge0.ElementAt(0));
        Vector3 vertex01 = vertices.ElementAt(t.Edge0.ElementAt(1));
        Vector3 vertex10 = vertices.ElementAt(t.Edge0.ElementAt(0));
        Vector3 vertex11 = vertices.ElementAt(t.Edge0.ElementAt(1));
        Vector3 vertex20 = vertices.ElementAt(t.Edge0.ElementAt(0));
        Vector3 vertex21 = vertices.ElementAt(t.Edge0.ElementAt(1));

        return intersectsWithPlane(vertex00, vertex01) || intersectsWithPlane(vertex10, vertex11) || intersectsWithPlane(vertex20, vertex21);
    }
    */
}
