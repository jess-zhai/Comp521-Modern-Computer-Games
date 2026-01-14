using System.Collections.Generic;
using UnityEngine;

public class TriangularPrismCollider : MonoBehaviour, ICustomCollider
{
    public bool oneSided = true;
    public float energyDamp = 0.8f;
    public float tangentialDamping = 0.03f;

    //public bool drawGizmos = true;
    //public Color edgeColor = new(0.1f, 0.7f, 1f, 1f);
    //public Color normalColor = new(1f, 0.4f, 0.1f, 1f);
    //[Range(0.1f, 1.5f)] public float normalLen = 0.8f;

    struct Edge { public Vector3 a, b, nOut; }
    Vector3[] tri = new Vector3[3];   // world coords (y ~ avg)
    Edge[] edges = new Edge[3];
    bool built = false;

    const float MergeEps = 0.0015f; // merge duplicate verts in XZ
    const float HitEps = 1e-5f;
    const float PenSlop = 0.002f;  // small redundancy to avoid re penetration

    public bool CheckCollision(Vector3 ballPos, float ballR,
                               out Vector3 normal, out float penetration)
    {
        EnsureBuilt();
        normal = Vector3.zero; penetration = 0f;
        if (!built) return false;

        // choose MIN positive penetration (MTV)
        float bestPen = float.PositiveInfinity;
        Vector3 bestN = Vector3.zero;

        for (int i = 0; i < 3; ++i)
        {
            Vector3 a = edges[i].a, b = edges[i].b;
            Vector3 ab = b - a;

            // closest point on segment in XZ
            Vector2 ab2 = new Vector2(ab.x, ab.z);
            float len2 = Mathf.Max(1e-8f, Vector2.Dot(ab2, ab2));
            Vector2 toBall = new Vector2(ballPos.x - a.x, ballPos.z - a.z);
            float t = Mathf.Clamp01(Vector2.Dot(toBall, ab2) / len2);
            Vector3 closest = a + t * ab;

            Vector3 toCenter = new Vector3(ballPos.x - closest.x, 0f, ballPos.z - closest.z);

            // when ball is on outward side
            if (oneSided && Vector3.Dot(edges[i].nOut, toCenter) < 0f)
                continue;

            float dist = toCenter.magnitude;
            float pen = (ballR + PenSlop) - dist;
            if (pen > 0f && pen < bestPen)
            {
                bestPen = pen;
                bestN = (dist > HitEps) ? (toCenter / dist) : edges[i].nOut;
            }
        }

        if (bestPen < float.PositiveInfinity)
        {
            normal = bestN;
            penetration = bestPen;
            return true;
        }
        return false;
    }

    public void HandleCollision(ref Vector3 velocity, Vector3 n,
                                Vector3 ballPosition, float restitutionFromCaller)
    {
        // static obstacle: no surface velocity
        float vn = Vector3.Dot(velocity, n);
        if (vn < -1e-4f)
        {
            float e = restitutionFromCaller * energyDamp; // dampening
            velocity -= (1f + e) * vn * n;

            // small tangential damping
            Vector3 vt = velocity - Vector3.Dot(velocity, n) * n;
            velocity -= tangentialDamping * vt;
        }
    }

    void EnsureBuilt()
    {
        // rebuild every frame in editor & during play to stay in sync
        if (!built || transform.hasChanged)
        {
            RebuildFromMesh();
            transform.hasChanged = false;
        }
    }

    void RebuildFromMesh()
    {
        var mf = GetComponent<MeshFilter>();
        var mesh = mf ? mf.sharedMesh : null;
        if (mesh == null || mesh.vertexCount < 3) { built = false; return; }

        var M = transform.localToWorldMatrix;
        var verts = mesh.vertices;

        // gather unique XZ points
        List<Vector2> pts = new List<Vector2>(verts.Length);
        float avgY = 0f;
        for (int i = 0; i < verts.Length; ++i)
        {
            Vector3 w = M.MultiplyPoint3x4(verts[i]);
            Vector2 p = new Vector2(w.x, w.z);
            bool dup = false;
            for (int k = 0; k < pts.Count; ++k)
            {
                if ((pts[k] - p).sqrMagnitude <= MergeEps * MergeEps) { dup = true; break; }
            }
            if (!dup) pts.Add(p);
            avgY += w.y;
        }
        avgY /= verts.Length;

        if (pts.Count < 3) { built = false; return; }

        // convex hull on XZ
        var hull = ConvexHull(pts);
        if (hull.Count < 3) { built = false; return; }

        // pick max area triangle from hull
        (int i0, int i1, int i2) = MaxAreaTriangle(hull);

        // world coords for drawing/collision (at average Y plane)
        Vector3 A = new Vector3(hull[i0].x, avgY, hull[i0].y);
        Vector3 B = new Vector3(hull[i1].x, avgY, hull[i1].y);
        Vector3 C = new Vector3(hull[i2].x, avgY, hull[i2].y);

        if (!IsCCW(A, B, C)) (B, C) = (C, B);
        tri[0] = A; tri[1] = B; tri[2] = C;

        // edges + outward normals
        Vector3 centroid = (A + B + C) / 3f; centroid.y = avgY;
        for (int i = 0; i < 3; ++i)
        {
            Vector3 va = tri[i];
            Vector3 vb = tri[(i + 1) % 3];

            Vector3 e = vb - va;
            Vector3 t = new Vector3(e.x, 0f, e.z);
            if (t.sqrMagnitude < 1e-8f) t = Vector3.right;
            t.Normalize();

            Vector3 n = new Vector3(-t.z, 0f, t.x); // left normal in XZ
            Vector3 mid = 0.5f * (va + vb);
            Vector3 outDir = new Vector3(mid.x - centroid.x, 0f, mid.z - centroid.z);
            if (Vector3.Dot(n, outDir) < 0f) n = -n;

            edges[i].a = va; edges[i].b = vb; edges[i].nOut = n;
        }

        built = true;
    }

    static bool IsCCW(Vector3 a, Vector3 b, Vector3 c)
    {
        float area2 = a.x * (b.z - c.z) + b.x * (c.z - a.z) + c.x * (a.z - b.z);
        return area2 > 0f;
    }

    static List<Vector2> ConvexHull(List<Vector2> pts)
    {
        pts.Sort((p, q) => p.x == q.x ? p.y.CompareTo(q.y) : p.x.CompareTo(q.x));
        List<Vector2> lower = new(), upper = new();
        foreach (var p in pts)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0) lower.RemoveAt(lower.Count - 1);
            lower.Add(p);
        }
        for (int i = pts.Count - 1; i >= 0; --i)
        {
            var p = pts[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0) upper.RemoveAt(upper.Count - 1);
            upper.Add(p);
        }
        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    static float Cross(Vector2 a, Vector2 b, Vector2 c)
        => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

    static (int, int, int) MaxAreaTriangle(List<Vector2> hull)
    {
        int n = hull.Count;
        float best = -1f;
        (int, int, int) ans = (0, 1, 2);
        for (int i = 0; i < n; ++i)
            for (int j = i + 1; j < n; ++j)
                for (int k = j + 1; k < n; ++k)
                {
                    float area = Mathf.Abs(Cross(hull[i], hull[j], hull[k])) * 0.5f;
                    if (area > best) { best = area; ans = (i, j, k); }
                }
        return ans;
    }

    //void OnDrawGizmos()
    //{
    //    if (!drawGizmos) return;
    //    EnsureBuilt();
    //    if (!built) return;

    //    Gizmos.color = edgeColor;
    //    Gizmos.DrawLine(tri[0], tri[1]);
    //    Gizmos.DrawLine(tri[1], tri[2]);
    //    Gizmos.DrawLine(tri[2], tri[0]);

    //    Gizmos.color = normalColor;
    //    for (int i = 0; i < 3; ++i)
    //    {
    //        Vector3 mid = 0.5f * (edges[i].a + edges[i].b);
    //        Gizmos.DrawLine(mid, mid + edges[i].nOut * normalLen);
    //    }
    //}
}
