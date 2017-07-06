using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using OpenCvSharp;

public class EdgeSelectionState : InteractionState {

    private readonly float OFFSET = 0f;
    private readonly float VIEWPLANE_SCALE = 2f;

    private Quaternion initialRotation0;
    private Quaternion initialRotation1;
    private Vector3 initialController1to0;

    private Vector3 planeProjection0;   // Projection of controller0 onto plane
    private Vector3 planeProjection1;   // Projection of controller 1 onto plane

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

        viewPlane.GetComponent<BoxCollider>().enabled = true;   // Allows 
    }

    public override void HandleEvents(ControllerInfo controller0Info, ControllerInfo controller1Info)
    {
        if (firstTime)
        {
            PreProcess();
            firstTime = false;
        }

        if (controller0Info.device.GetPress(SteamVR_Controller.ButtonMask.Touchpad) || controller1Info.device.GetPress(SteamVR_Controller.ButtonMask.Touchpad))    // Go back to start state, useful for debugging
        {
            GameObject.Find("UIController").GetComponent<UIController>().changeState(new StartState());
        }

        planeProjection0 = ClosestPointToPlane(controller0Info.trackedObj.transform.position);
        planeProjection1 = ClosestPointToPlane(controller1Info.trackedObj.transform.position);


        Quaternion delta0 = controller0Info.trackedObj.transform.rotation * Quaternion.Inverse(initialRotation0);
        Quaternion delta1 = controller1Info.trackedObj.transform.rotation * Quaternion.Inverse(initialRotation1);
        Vector3 controller0Direction = (delta0 * -initialController1to0).normalized;
        Vector3 controller1Direction = (delta1 * initialController1to0).normalized;

        float offsetDistance = (controller0Info.trackedObj.transform.position - controller1Info.trackedObj.transform.position).magnitude * 0.5f;

        Vector3 controller0OffsetPoint = controller0Info.trackedObj.transform.position + offsetDistance * controller0Direction;
        Vector3 controller1OffsetPoint = controller1Info.trackedObj.transform.position + offsetDistance * controller1Direction;
        // Call closest point method to get control points
        Vector3 closestPoint0 = ClosestPointToPlane(controller0OffsetPoint);
        Vector3 closestPoint1 = ClosestPointToPlane(controller1OffsetPoint);

        BezierCurve curve = new BezierCurve(planeProjection0, closestPoint0, closestPoint1, planeProjection1);

        List<Vector3> points = new List<Vector3>();
        int numSegments = 20;
        for (int i = 0; i <= numSegments; i++)
        {
            points.Add(ClosestPointToPlane(curve.Evaluate(i /(float) numSegments)));
        }

        CreateMesh(points);

        /*
        Texture2D bezierTexture = Resources.Load("Textures/GRASS2") as Texture2D;
        try
        {
            Handles.DrawBezier(controller0Info.trackedObj.transform.position, controller1Info.trackedObj.transform.position, initialRotation0, initialRotation1, Color.cyan, null, 2f);
        }
        catch (System.NullReferenceException n)
        {
            Debug.Log("controller0 pos is null: " + (controller0Info.trackedObj.transform.position == null));
            Debug.Log("controller1 pos is null: " + (controller1Info.trackedObj.transform.position == null));
            Debug.Log("intitialrotation0 is null: " + (initialRotation0 == null));
            Debug.Log("intitialrotation1 is null: " + (initialRotation0 == null));
            Debug.Log("Bezier texture is null: " + (bezierTexture == null));
            Debug.Log(n);
        }
        */

    }

    Vector3 ClosestPointToPlane(Vector3 pt)
    {
        return pt + (viewPlane.transform.up * (-(Vector3.Dot(viewPlane.transform.up, pt) - Vector3.Dot(viewPlane.transform.up, viewPlane.transform.position))));
    }

    public override void deactivate()
    {
        viewPlane.GetComponent<BoxCollider>().enabled = false;
        viewPlane.transform.localScale = new Vector3(0.02f, 1f, 0.02f);
    }

    void PreProcess()
    {
        // Get corners from plane
        Vector3 centerOfPlane = viewPlane.transform.position;
        float sideLength = 10 * viewPlane.transform.localScale.x;

        corner0 = centerOfPlane - sideLength * viewPlane.transform.right - sideLength * viewPlane.transform.forward;
        corner1 = centerOfPlane + sideLength * viewPlane.transform.right - sideLength * viewPlane.transform.forward;
        corner2 = centerOfPlane - sideLength * viewPlane.transform.right + sideLength * viewPlane.transform.forward;
        corner3 = centerOfPlane + sideLength * viewPlane.transform.right + sideLength * viewPlane.transform.forward;

        // Get texture from plane
        Material material = viewPlane.GetComponent<Renderer>().material;
        RenderTexture tex = material.mainTexture as RenderTexture;
        RenderTexture.active = tex;

        planeTexture = new Texture2D(tex.width, tex.height);
        planeTexture.ReadPixels(new UnityEngine.Rect(0, 0, planeTexture.width, planeTexture.height), 0, 0);
        planeTexture.Apply();

        byte[] textureData = planeTexture.GetRawTextureData();

        //Debug.Log("Texture format: " + texture.format);

        Mat raw = new Mat(planeTexture.width, planeTexture.height, MatType.CV_8UC4, textureData);

        gray = new Mat(raw.Size(), MatType.CV_8UC1);

        Cv2.CvtColor(raw, gray, ColorConversionCodes.RGBA2GRAY);

        gray.GaussianBlur(new Size(7, 7), 0);

        // Opening morph to remove small points
        int morph_size = 1;

        element = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2 * morph_size + 1, 2 * morph_size + 1));
        width = gray.Size().Width;
        height = gray.Size().Height;
        //Mat binary = Mat.Zeros(height, width, MatType.CV_32FC1);
        distPos = new Mat(height, width, MatType.CV_32FC1);
        distNeg = new Mat(height, width, MatType.CV_32FC1);
        distanceMap = new Mat(height, width, MatType.CV_32FC1);
        grad_x = Mat.Zeros(height, width, MatType.CV_32FC1);
        grad_y = Mat.Zeros(height, width, MatType.CV_32FC1);
        grad_x_normalized = Mat.Zeros(height, width, MatType.CV_32FC1);
        grad_y_normalized = Mat.Zeros(height, width, MatType.CV_32FC1);
        gradient = Mat.Zeros(height, width, MatType.CV_32FC1);
        //float[] buffer = new float[height * width];
        double defaultThreshold = 180.0;

        UpdateGradient(defaultThreshold);
    }

    void UpdateGradient(double threshold)
    {
        double currentThreshold = threshold;
        Mat binary = new Mat();
        Cv2.Threshold(gray, binary, threshold, 255.0, ThresholdTypes.BinaryInv);

        Cv2.MorphologyEx(binary, binary, MorphTypes.Open, element, new Point(-1, -1), 1);

        Mat invertBinary = 255 - binary;
        Debug.Log(binary.Type());
        Cv2.DistanceTransform(binary, distPos, DistanceTypes.L2, DistanceMaskSize.Precise);
        Cv2.DistanceTransform(invertBinary, distNeg, DistanceTypes.L2, DistanceMaskSize.Precise);

        distanceMap = distPos - distNeg;

        Cv2.Sobel(distanceMap, grad_x, MatType.CV_32FC1, 1, 0, 3);
        Cv2.Sobel(distanceMap, grad_y, MatType.CV_32FC1, 0, 1, 3);

        Cv2.Normalize(grad_x, grad_x_normalized, 0.0, 1.0, NormTypes.MinMax);
        Cv2.Normalize(grad_y, grad_y_normalized, 0.0, 1.0, NormTypes.MinMax);
        Mat tmp1, tmp2;
        Cv2.AddWeighted(grad_x_normalized, 0.5, grad_y_normalized, 0.5, 0, gradient);
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

            uv.y = 1f - uv.y;   // Comment out if things are flipped around wrong
            Vector2 pixelSpacePoint = new Vector2(uv.x * slideSize.x, uv.y * slideSize.y);

            pixelSpacePoint = RefinePoint(pixelSpacePoint);

            uv = new Vector2(pixelSpacePoint.x/slideSize.x, pixelSpacePoint.y/slideSize.y);

            // Flip V back
            uv.y = 1f - uv.y;
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
        Vector3.Normalize(uVec);
        Vector3.Normalize(vVec);

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

    public void CreateMesh (List<Vector3> points)
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
                direction = points.ElementAt(i) - points.ElementAt(i - 1);
            }
            else {
               direction = points.ElementAt(i + 1) - points.ElementAt(i);
            }

            Vector3 right = Vector3.Cross(viewPlane.transform.up, direction);

            /*
            Vector3 right = Vector3.Cross(new Vector3(0, 1, 0), direction);

            if (i > 0)
            {
                if (Vector3.Dot(right.normalized, previousRight) < 0)
                {
                    right *= -1;
                }
            }
            previousRight = right.normalized;
            */

            Vector3 up = Vector3.Cross(direction, right);
            up = up.normalized * radius; 
            right = right.normalized * radius;

             int numSections = 10; // added and arbitrary (testing purposes)

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

        GameObject bezierObject = GameObject.Find("BezierCurve");

        Mesh selectorMesh = new Mesh();
        selectorMesh.vertices = vertices.ToArray();
        selectorMesh.uv = uvCoordinates.ToArray();
        selectorMesh.triangles = faces.ToArray();

        selectorMesh.RecalculateBounds();
        selectorMesh.RecalculateNormals();

        bezierObject.GetComponent<MeshFilter>().mesh = selectorMesh; 
        
    }



}
