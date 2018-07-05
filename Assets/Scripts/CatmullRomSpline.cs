/**
* /author Bret Jackson
*
* /file  CatmullRomSpline.cs
* /brief Represents a piecewise 3rd-order Catmull-Rom spline based on the G3D10 implementation translated from c++ to c#
*
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CatmullRomSpline
{
    private List<float> _time;
    private List<Vector3> _controls;
    private Matrix4x4 _basis;

    public CatmullRomSpline() {
        _time = new List<float>();
        _controls = new List<Vector3>();
        ComputeBasis();
    }


    /** Appends a Vector3 point at a specific time that must be
        greater than that of the previous point. */
    public void Append(float t, Vector3 c)
    {
        _time.Add(t);
        _controls.Add(c);
    }

    /** Appends Vector3 point spaced in time based on the previous
        Vector3 point, or spaced at unit intervals if this is the
        first Vector3 point. */
    public void Append(Vector3 c)
    {
        switch (_time.Count)
        {
            case 0:
                Append(0, c);
                break;

            case 1:
                Append(_time[0] + 1, c);
                break;

            default:
                Append(2 * _time[_time.Count - 1] - _time[_time.Count - 2], c);
                break;
        }
    }

    public void Clear()
    {
        _controls.Clear();
        _time.Clear();
    }


    /** Number of Vector3 points */
    public int Size()
    {
        return _controls.Count;
    }

    public float TotalTime()
    {
        return _time[_time.Count - 1];
    }

    public void ComputeIndexInBounds(float s, ref int i, ref float u)
    {
        int N = _time.Count;
        float t0 = _time[0];
        float tn = _time[N - 1];

        i = (int)Mathf.Floor((N - 1) * (s - t0) / (tn - t0));

        // Inclusive bounds for binary search
        int hi = N - 1;
        int lo = 0;

        while ((_time[i] > s) || (_time[i + 1] <= s))
        {

            if (_time[i] > s)
            {
                // too big
                hi = i - 1;
            }
            else if (_time[i + 1] <= s)
            {
                // too small
                lo = i + 1;
            }

            i = (hi + lo) / 2;
        }

        // Having exited the above loop, i must be correct, so compute u.
        u = (s - _time[i]) / (_time[i + 1] - _time[i]);
    }

    public void ComputeIndex(float s, ref int i, ref float u)
    {
        int N = _time.Count;

        Debug.Assert(N > 0);
        float t0 = _time[0];
        float tn = _time[N - 1];

        if (N < 2)
        {
            // No Vector3 points to work with
            i = 0;
            u = 0.0f;
        }
        else
        {
            // Non-cyclic
            if (s < t0)
            {
                // Non-cyclic, off the bottom.  Assume points are spaced
                // following the first time interval.

                float dt = _time[1] - t0;
                float x = (s - t0) / dt;
                i = (int) System.Math.Floor(x);
                u = x - i;
            }
            else if (s >= tn)
            {
                // Non-cyclic, off the top.  Assume points are spaced following
                // the last time interval.

                float dt = tn - _time[N - 2];
                float x = N - 1 + (s - tn) / dt;
                i = (int) System.Math.Floor(x);
                u = x - i;
            }
            else
            {
                // In bounds, non-cyclic.  Assume a regular
                // distribution (which gives O(1) for uniform spacing)
                // and then binary search to handle the general case
                // efficiently.
                ComputeIndexInBounds(s, ref i, ref u);

            } // if in bounds
        } // extrapolation Mode
    }

    /** Returns a series of N control points and times, fixing
        boundary issues.  The indices may be assumed to be treated
        cyclically. */
    public void GetControls(int i, float[] T, Vector3[] A, int N)
    {
        for (int j = 0; j < N; ++j)
        {
            GetControl(i + j, ref T[j], ref A[j]);
        }
    }

    /** Returns the requested Control point and time sample based on
       array index.  If the array index is out of bounds linearly extrapolates (for a non-cyclic
       spline), assuming time intervals follow the first or last
       sample recorded.

       Returns 0 if there are no control points.
   */
    public void GetControl(int i, ref float t, ref Vector3 c)
    {
        int N = _controls.Count;
        if (N == 0)
        {
            t = 0;
        }
        else if (i < 0)
        {
            // Are there enough points to extrapolate?
            if (N >= 2)
            {
                // Step away from Vector3 point 0
                float dt = _time[1] - _time[0];

                // Extrapolate (note; i is negative)
                c = _controls[1] * (i) + _controls[0] * ((1 - i));
                t = dt * i + _time[0];

            }
            else
            {
                // Just clamp
                c = _controls[0];

                // Only 1 time; assume 1s intervals
                t = _time[0] + i;
            }

        }
        else if (i >= N)
        {
            if (N >= 2)
            {
                float dt = _time[N - 1] - _time[N - 2];

                // Extrapolate (note; i is negative)
                c = _controls[N - 1] * ((i - N + 2)) + _controls[N - 2] * -((i - N + 1));
                // Extrapolate
                t = _time[N - 1] + dt * (i - N + 1);

            }
            else
            {
                // Return the last, clamping
                c = _controls[_controls.Count - 1];
                // Only 1 time; assume 1s intervals
                t = _time[0] + i;
            }
        }
        else
        {
            // In bounds
            c = _controls[i];
            t = _time[i];
        }
    }

    public void UpdateVector3(int i, Vector3 c)
    {
        Debug.Assert(i >= 0 && i < _controls.Count);
        _controls[i] = c;
    }

    public void ComputeBasis()
    {
        // The standard Catmull-Rom spline basis (e.g., Watt & Watt p108)
        // is for [u^3 u^2 u^1 u^0] * B * [p[0] p[1] p[2] p[3]]^T.
        // We need a basis formed for:
        //
        //     U * C * [2*p'[1] p[1] p[2] 2*p'[2]]^T 
        //
        //     U * C * [p2-p0 p1 p2 p3-p1]^T 
        //
        // To make this transformation, compute the differences of columns in C:
        // For [p0 p1 p2 p3]
        Matrix4x4 basis = new
            Matrix4x4(new Vector4(-1, 2, -1, 0) * 0.5f,
                      new Vector4(3, -5, 0, 2) * 0.5f,
                      new Vector4(-3, 4, 1, 0) * 0.5f,
                      new Vector4(1, -1, 0, 0) * 0.5f);

        // For [-p0 p1 p2 p3]^T 
        basis.SetColumn(0, -basis.GetColumn(0));

        // For [-p0 p1 p2 p3-p1]^T 
        basis.SetColumn(1, basis.GetColumn(1) + basis.GetColumn(3));

        // For [p2-p0 p1 p2 p3-p1]^T 
        basis.SetColumn(2, basis.GetColumn(2) - basis.GetColumn(0));

        _basis = basis;
    }

    /**
       Return the position at time s.  The spline is defined outside
       of the time samples by extrapolation or cycling.
     */
    public Vector3 Evaluate(float s)
    {
        /*
        @cite http://www.gamedev.net/reference/articles/article1497.asp 
        Derivation of basis matrix follows.

        Given control points with positions p[i] at times t[i], 0 <= i <= 3, find the position 
        at time t[1] <= s <= t[2].

        Let u = s - t[0]
        Let U = [u^0 u^1 u^2 u^3] = [1 u u^2 u^3]
        Let dt0 = t[0] - t[-1]
        Let dt1 = t[1] - t[0]
        Let dt2 = t[2] - t[1]
         */

        // Index of the first control point (i.e., the u = 0 point)
        int i = 0;
        // Fractional part of the time
        float u = 0;

        ComputeIndex(s, ref i, ref u);

        Vector3[] p = new Vector3[4];
        float[] t = new float[4];
        GetControls(i - 1, t, p, 4);

        Vector3 p0 = p[0];
        Vector3 p1 = p[1];
        Vector3 p2 = p[2];
        Vector3 p3 = p[3];

        // Compute the weighted sum of the neighboring Vector3 points.
        Vector3 sum;

        /*
        if (interpolationMode == SplineInterpolationMode::LINEAR) {
            const float a = (s - t[1]) / (t[2] - t[1]);
            sum = p1 * (1.0f - a) + p2 * a;
            correct(sum);
            return sum;
        }
        */

        float dt0 = t[1] - t[0];
        float dt1 = t[2] - t[1];
        float dt2 = t[3] - t[2];

        // Powers of u
        Vector4 uvec = new Vector4((float)(u * u * u), (float)(u * u), (float)u, 1f);

        // Compute the weights on each of the Vector3 points.
        Vector4 weights = new Vector4(uvec.x * _basis[0, 0] + uvec.y * _basis[1, 0] + uvec.z * _basis[2, 0] + uvec.w * _basis[3, 0],
                                       uvec.x * _basis[0, 1] + uvec.y * _basis[1, 1] + uvec.z * _basis[2, 1] + uvec.w * _basis[3, 1],
                                       uvec.x * _basis[0, 2] + uvec.y * _basis[1, 2] + uvec.z * _basis[2, 2] + uvec.w * _basis[3, 2],
                                       uvec.x * _basis[0, 3] + uvec.y * _basis[1, 3] + uvec.z * _basis[2, 3] + uvec.w * _basis[3, 3]);


        // The factor of 1/2 from averaging two time intervals is 
        // already factored into the basis 

        // tan1 = (dp0 / dt0 + dp1 / dt1) * ((dt0 + dt1) * 0.5);
        // The last term normalizes for unequal time intervals
        float x = (dt0 + dt1) * 0.5f;
        float n0 = x / dt0;
        float n1 = x / dt1;
        float n2 = x / dt2;

        Vector3 dp0 = p1 + (p0 * -1f);
        Vector3 dp1 = p2 + (p1 * -1f);
        Vector3 dp2 = p3 + (p2 * -1f);

        Vector3 dp1n1 = dp1 * n1;
        Vector3 tan1 = dp0 * n0 + dp1n1;
        Vector3 tan2 = dp1n1 + dp2 * n2;

        sum =
                    tan1 * weights[0] +
                     p1 * weights[1] +
                     p2 * weights[2] +
                    tan2 * weights[3];

        //assert(glm::isfinite(sum[0]) == true);
        //assert(glm::isnan(sum[0]) != true);
        return sum;
    }



}