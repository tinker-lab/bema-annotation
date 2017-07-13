using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using OpenCvSharp;

public class EdgeSelectionState : InteractionState {

    private readonly float OFFSET = 0.1f;
    private readonly float SEGMENT_SCALE = 130f;
    private readonly float VIEWPLANE_SCALE = 2f;

    private Quaternion initialRotation0;
    private Quaternion initialRotation1;
    private Vector3 initialController1to0;

    private GameObject viewPlane;
    private Texture2D planeTexture;
    private Transform headset;
    bool firstTime = true;

    private Mat gray;
    private Mat element;
    private Mat distPos;
    private Mat distNeg;
    private Mat distanceMap;
    private Mat grad_x;
    private Mat grad_y;
    private Mat grad_x_normalized;
    private Mat grad_y_normalized;
    private Mat gradient;

    private int width;
    private int height;

    Vector3 corner0;
    Vector3 corner1;
    Vector3 corner2;
    Vector3 corner3;


    public EdgeSelectionState(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        desc = "EdgeSelectionState";

        viewPlane = GameObject.Find("ViewPlane");
        headset = GameObject.FindGameObjectWithTag("MainCamera").transform;

        //TODO: Rotate viewplane to be perpendicular with ground
        viewPlane.transform.localScale *= VIEWPLANE_SCALE;

        initialRotation0 = controller0Info.trackedObj.transform.rotation;
        initialRotation1 = controller1Info.trackedObj.transform.rotation;
        initialController1to0 = (controller0Info.trackedObj.transform.position - controller1Info.trackedObj.transform.position).normalized; //TODO: account for left and right

        //viewPlane.GetComponent<BoxCollider>().enabled = true;   
    }

    public override void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        if (firstTime)
        {
            PreProcess();
        }

        Vector3 planeProjection0 = ClosestPointToPlane(controller0Info.trackedObj.transform.position) - viewPlane.transform.forward * OFFSET;       // Projection of controller0 onto plane
        Vector3 planeProjection1 = ClosestPointToPlane(controller1Info.trackedObj.transform.position) - viewPlane.transform.forward * OFFSET;       // Projection of controller 1 onto plane

        if (AwayFromPlane(controller0Info) || AwayFromPlane(controller1Info) || AwayFromPlane(planeProjection0) || AwayFromPlane(planeProjection1))
        {
            GameObject.Find("UIController").GetComponent<UIController>().changeState(new NavigationState());
        }

        Quaternion delta0 = controller0Info.trackedObj.transform.rotation * Quaternion.Inverse(initialRotation0);
        Quaternion delta1 = controller1Info.trackedObj.transform.rotation * Quaternion.Inverse(initialRotation1);
        Vector3 controller0Direction = (delta0 * -initialController1to0).normalized;
        Vector3 controller1Direction = (delta1 * initialController1to0).normalized;

        float offsetDistance = (controller0Info.trackedObj.transform.position - controller1Info.trackedObj.transform.position).magnitude * 0.5f;

        Vector3 controller0OffsetPoint = controller0Info.trackedObj.transform.position + offsetDistance * controller0Direction;
        Vector3 controller1OffsetPoint = controller1Info.trackedObj.transform.position + offsetDistance * controller1Direction;
        // Call closest point method to get control points
        Vector3 closestPoint0 = ClosestPointToPlane(controller0OffsetPoint) - viewPlane.transform.forward * OFFSET;
        Vector3 closestPoint1 = ClosestPointToPlane(controller1OffsetPoint) - viewPlane.transform.forward * OFFSET;

        BezierCurve curve = new BezierCurve(planeProjection0, closestPoint0, closestPoint1, planeProjection1);

        List<Vector3> points = new List<Vector3>();
        int numSegments = (int)(Vector3.Distance(planeProjection0, planeProjection1) * SEGMENT_SCALE);

        for (int i = 0; i <= numSegments; i++)
        {
            points.Add(ClosestPointToPlane(curve.Evaluate(i /(float) numSegments)));
        }

        GameObject.Find("BezierParent").transform.Find("GuideCurve").GetComponent<MeshFilter>().mesh = CreateMesh(points);
        points = SnapToContour(viewPlane, points);
        GameObject.Find("BezierParent").transform.Find("BezierCurve").GetComponent<MeshFilter>().mesh = CreateMesh(points);

        if (firstTime)
        {
            GameObject.Find("BezierParent").transform.GetChild(0).gameObject.SetActive(true);
            GameObject.Find("BezierParent").transform.GetChild(1).gameObject.SetActive(true);
            firstTime = false;
        }
    }

    public static Vector3 ClosestPointToPlane(Vector3 pt)
    {
        GameObject viewPlane = GameObject.Find("ViewPlane");
        return pt + (viewPlane.transform.up * (-(Vector3.Dot(viewPlane.transform.up, pt) - Vector3.Dot(viewPlane.transform.up, viewPlane.transform.position))));
    }

    public override void deactivate()
    {
        //viewPlane.GetComponent<BoxCollider>().enabled = false;
        viewPlane.transform.localScale = new Vector3(0.02f, 1f, 0.02f);
        GameObject.Find("BezierParent").transform.GetChild(0).gameObject.SetActive(false);
        GameObject.Find("BezierParent").transform.GetChild(1).gameObject.SetActive(false);
    }

    // Returns true if controller is certain distance away from plane
    private bool AwayFromPlane(ControllerInfo controllerInfo)
    {
        GameObject viewPlane = GameObject.Find("ViewPlane");
        if (viewPlane == null)
        {
            return false;
        }
        else
        {
            Vector3 controllerPos = controllerInfo.trackedObj.transform.position;
            Vector3 planePos = viewPlane.transform.position;

            float distance = Vector3.Distance(controllerPos, planePos);

            return (distance > NavigationState.MIN_DISTANCE * 2);
        }
    }

    // Returns true if point is certain distance from plane
    private bool AwayFromPlane(Vector3 projectedPoint)
    {
        GameObject viewPlane = GameObject.Find("ViewPlane");
        if (viewPlane == null)
        {
            return false;
        }
        else
        {
            Vector3 planePos = viewPlane.transform.position;

            float distance = Vector3.Distance(projectedPoint, planePos);

            return (distance > viewPlane.transform.localScale.x * 5 + NavigationState.MIN_DISTANCE);
        }
    }

    void PreProcess()
    {
        // Get corners from plane
        Vector3 centerOfPlane = viewPlane.transform.position;
        float sideLength = (10*viewPlane.transform.localScale.x) / 2;

        corner0 = centerOfPlane - sideLength * viewPlane.transform.right.normalized - sideLength * viewPlane.transform.forward.normalized;
        corner1 = centerOfPlane + sideLength * viewPlane.transform.right.normalized - sideLength * viewPlane.transform.forward.normalized;
        corner2 = centerOfPlane - sideLength * viewPlane.transform.right.normalized + sideLength * viewPlane.transform.forward.normalized;
        corner3 = centerOfPlane + sideLength * viewPlane.transform.right.normalized + sideLength * viewPlane.transform.forward.normalized;

        // Get texture from plane
        Material material = viewPlane.GetComponent<Renderer>().material;
        RenderTexture tex = material.mainTexture as RenderTexture;
        RenderTexture.active = tex;

        planeTexture = new Texture2D(tex.width, tex.height);
        planeTexture.ReadPixels(new UnityEngine.Rect(0, 0, planeTexture.width, planeTexture.height), 0, 0);
        planeTexture.Apply();

        byte[] textureData = planeTexture.GetRawTextureData();

        Mat raw = new Mat(planeTexture.width, planeTexture.height, MatType.CV_8UC4, textureData);

        gray = new Mat(raw.Size(), MatType.CV_8UC1);

        Cv2.CvtColor(raw, gray, ColorConversionCodes.RGBA2GRAY);

        gray.GaussianBlur(new Size(7, 7), 0);

        // Opening morph to remove small points
        int morph_size = 1;

        element = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2 * morph_size + 1, 2 * morph_size + 1));
        width = gray.Size().Width;
        height = gray.Size().Height;
        distPos = new Mat(height, width, MatType.CV_32FC1);
        distNeg = new Mat(height, width, MatType.CV_32FC1);
        distanceMap = new Mat(height, width, MatType.CV_32FC1);
        grad_x = Mat.Zeros(height, width, MatType.CV_32FC1);
        grad_y = Mat.Zeros(height, width, MatType.CV_32FC1);
        grad_x_normalized = Mat.Zeros(height, width, MatType.CV_32FC1);
        grad_y_normalized = Mat.Zeros(height, width, MatType.CV_32FC1);
        gradient = Mat.Zeros(height, width, MatType.CV_32FC1);

        double defaultThreshold = 180.0;
        UpdateGradient(defaultThreshold);
    }

    void UpdateGradient(double threshold)
    {
        //Cv2.ImShow("gray", gray);

        double currentThreshold = threshold;
        Mat binary = new Mat();
        Cv2.Threshold(gray, binary, threshold, 255.0, ThresholdTypes.BinaryInv);

        //Cv2.ImShow("binary", binary);

       // Cv2.MorphologyEx(binary, binary, MorphTypes.Open, element, new Point(-1, -1), 1);

       // Cv2.ImShow("binaryafter morph", binary);

        Mat invertBinary = 255 - binary;
        Cv2.DistanceTransform(binary, distPos, DistanceTypes.L2, DistanceMaskSize.Precise);
        Cv2.DistanceTransform(invertBinary, distNeg, DistanceTypes.L2, DistanceMaskSize.Precise);

        distanceMap = distPos - distNeg;

        //Cv2.ImShow("distmap", distanceMap);

        Cv2.Sobel(distanceMap, grad_x, MatType.CV_32FC1, 1, 0, 3);
        Cv2.Sobel(distanceMap, grad_y, MatType.CV_32FC1, 0, 1, 3);

        Cv2.Normalize(grad_x, grad_x_normalized, 0.0, 1.0, NormTypes.MinMax);
        Cv2.Normalize(grad_y, grad_y_normalized, 0.0, 1.0, NormTypes.MinMax);
        Cv2.AddWeighted(grad_x_normalized, 0.5, grad_y_normalized, 0.5, 0, gradient);

        //Cv2.ImShow("gradient", gradient);
    }

    List<Vector3> SnapToContour(GameObject viewPlane, List<Vector3> line)
    {
        Vector2 slideSize = new Vector2(planeTexture.width, planeTexture.height);

        List<Vector3> snappedLine = new List<Vector3>();
        //Resize<typeof(Vector3)>(snappedLine, line.Count, typeof(Vector3));
        snappedLine.Capacity = line.Count;  // NOTE: couldn't find equivalent to resize, so did what I think was similar

        List<Vector2> pixelPoints = new List<Vector2>(); 
        //List<Vector2> refinedPoints = new List<Vector2>();
        pixelPoints.Capacity = line.Count;
        //refinedPoints.Capacity = line.Count;

        // Convert from virtual space to pixel space

        // Parallelize?
        for (int i=0; i < line.Count; i++)
        {
            Vector2 uv = GetUVFromPoint(line.ElementAt(i));

            //uv.y = 1f - uv.y;   // Comment out if things are flipped around wrong
            Vector2 pixelSpacePoint = new Vector2(uv.x * slideSize.x, uv.y * slideSize.y);

            pixelSpacePoint = RefinePoint(pixelSpacePoint);

            uv = new Vector2(pixelSpacePoint.x/slideSize.x, pixelSpacePoint.y/slideSize.y);

            // Flip V back
            //uv.y = 1f - uv.y;
            snappedLine.Insert(i, GetPointFromUV(uv));
        }


        return snappedLine;
    }

    Vector2 GetUVFromPoint(Vector3 point)
    {
        Vector2 uv = new Vector2();

        Vector3 uVec = corner1 - corner0;
        Vector3 vVec = corner0 - corner2;

        float uLength = uVec.magnitude;
        float vLength = vVec.magnitude;
        uVec = Vector3.Normalize(uVec);
        vVec = Vector3.Normalize(vVec);

        uv.x = Vector3.Dot(point - corner0, uVec);
        uv.y = Vector3.Dot(point - corner2, vVec);

        uv.x /= uLength;
        uv.y /= vLength;

        //clamp
        uv.x = Mathf.Max(Mathf.Min(uv.x, 1f), 0f);
        uv.y = Mathf.Max(Mathf.Min(uv.y, 1f), 0f);

        return uv;
    }

    Vector3 GetPointFromUV(Vector2 uv)
    {
        Vector3 uVec = corner1 - corner0; 
        Vector3 vVec = corner0 - corner2;

        Vector3 multiplied_uVec = new Vector3(uv.x * uVec.x, uv.x * uVec.y, uv.x * uVec.z);
        Vector3 multiplied_vVec = new Vector3(uv.y * vVec.x, uv.y * vVec.y, uv.y * vVec.z);

        return (corner2 + multiplied_uVec + multiplied_vVec);
    }

    Vector2 RefinePoint(Vector2 point)
    {
        const int maxNumIterations = 200;
        const float step  = 0.1f;
        const double convergenceLength = 0.05;
        bool converged = false;

        Size patchSize = new Size(1, 1);
        Mat subPatch = new Mat();

        int numIterations = 0;
        Vector2 newPoint = point;
        Vector2 prevPoint;
        Vector2 force = new Vector2();
        while(!converged && numIterations < maxNumIterations)
        {
            numIterations++;
            Point2f cvPoint = new Point2f(newPoint.x, newPoint.y);
            Cv2.GetRectSubPix(grad_x, patchSize, cvPoint, subPatch);
            force.x = subPatch.At<float>(0, 0);
            Cv2.GetRectSubPix(grad_y, patchSize, cvPoint, subPatch);
            force.y = subPatch.At<float>(0, 0);

            prevPoint = newPoint;
            newPoint = newPoint + new Vector2(step * force.x, step * force.y);

            if (Vector3.Magnitude(newPoint - prevPoint) <= convergenceLength)
            {
                converged = true;
            }
        }

        return newPoint;
    }

    private Mesh CreateMesh (List<Vector3> points)
        // take list of vectors, center of one to next
        // sample points along curve, pass to list
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> faces = new List<int>();
        List<Vector2> uvCoordinates = new List<Vector2>();

         float radius = .005f;

        Vector3 previousRight = new Vector3(0,0,0);

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 centerStart = points.ElementAt(i);
            Vector3 direction;
            if (i == points.Count - 1)
            {
                direction = points.ElementAt(i) - points.ElementAt(i - 1);  //NOTE: Threw exception when I made the curve incredibly small
            }
            else {
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
                uvCoordinates.Add(new Vector2((float)slice / (float)numSections, (float)i/ (float)points.Count));


                if (slice > 0 && i > 0)
                {
                    faces.Add(slice + ((numSections+1) * (i-1)));
                    faces.Add(slice - 1 + ((numSections + 1) * (i - 1))); 
                    faces.Add(slice + ((numSections + 1) * i));

                    faces.Add(slice + ((numSections + 1) * i));
                    faces.Add(slice - 1 + ((numSections + 1) * (i - 1)));
                    faces.Add(slice - 1 + ((numSections + 1) * i));
                }
            }
        }

        GameObject bezierObject = GameObject.Find("BezierParent").transform.GetChild(0).gameObject;

        Mesh selectorMesh = new Mesh();
        selectorMesh.vertices = vertices.ToArray();
        selectorMesh.uv = uvCoordinates.ToArray();
        selectorMesh.triangles = faces.ToArray();

        selectorMesh.RecalculateBounds();
        selectorMesh.RecalculateNormals();
        //bezierObject.GetComponent<MeshFilter>().mesh = selectorMesh; 
        return selectorMesh;
    }



}
