using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[ExecuteAlways]
public class ObstacleGeometry : MonoBehaviour
{
    public List<Vector3> vertices = new List<Vector3>();
    public float boundingRadius = 0f;

    public float collisionPadding = 6.0f;
    private Vector2 _boundingCenterLocal;

    private void Awake()
    {
        CalculateGeometryData();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CalculateGeometryData();
    }
#endif

    public void CalculateGeometryData()
    {
        vertices.Clear();

        if (!gameObject.CompareTag("Obstacle"))
        {
            boundingRadius = 0f;
            return;
        }

        List<Vector2> localPoints = new List<Vector2>();
        foreach (var box in GetComponentsInChildren<BoxCollider>())
        {
            if (!box.gameObject.CompareTag("Obstacle"))
                continue;

            Vector3 c = box.center;
            Vector3 s = box.size * 0.5f;
            Vector3[] localCorners =
            {
                new Vector3(c.x - s.x, c.y - s.y, c.z - s.z),
                new Vector3(c.x - s.x, c.y - s.y, c.z + s.z),
                new Vector3(c.x + s.x, c.y - s.y, c.z + s.z),
                new Vector3(c.x + s.x, c.y - s.y, c.z - s.z),
            };

            foreach (var lc in localCorners)
            {
                // transform to local space
                Vector3 world = box.transform.TransformPoint(lc);
                Vector3 local = transform.InverseTransformPoint(world);
                localPoints.Add(new Vector2(local.x, local.z));
            }
        }

        if (localPoints.Count == 0) return;
        localPoints = RemoveDuplicatePoints(localPoints, 0.001f);

        var selected = SelectExtremePoints(localPoints);
        if (gameObject.name.Contains("BigU"))
            selected = ProcessUShape(selected);

        foreach (var p in selected)
        {
            Vector3 local = new Vector3(p.x, 1f, p.y);
            vertices.Add(local);
        }

        CalculateBoundingCircle(selected);
    }


    private void CalculateBoundingCircle(List<Vector2> points)
    {
        if (points == null || points.Count == 0)
        {
            boundingRadius = 0f;
            return;
        }

        // turn local point to world space
        List<Vector2> worldPoints = points
            .Select(p =>
            {
                Vector3 world = transform.TransformPoint(new Vector3(p.x, 0f, p.y));
                return new Vector2(world.x, world.z);
            })
            .ToList();

        // find furthest points
        Vector2 p1 = worldPoints[0], p2 = worldPoints[0];
        float maxDist = 0f;

        for (int i = 0; i < worldPoints.Count; i++)
        {
            for (int j = i + 1; j < worldPoints.Count; j++)
            {
                float dist = Vector2.Distance(worldPoints[i], worldPoints[j]);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    p1 = worldPoints[i];
                    p2 = worldPoints[j];
                }
            }
        }

        Vector2 centerWorld = (p1 + p2) * 0.5f;
        float radius = maxDist * 0.5f;

        // expand the circle until all points are included
        foreach (var p in worldPoints)
        {
            float dist = Vector2.Distance(p, centerWorld);
            if (dist > radius)
            {
                float newRadius = (radius + dist) * 0.5f;
                float move = newRadius - radius;
                Vector2 dir = (p - centerWorld).normalized;
                centerWorld += dir * move;
                radius = newRadius;
            }
        }

        boundingRadius = radius + collisionPadding * 0.5f;

        Vector3 localCenter3D = transform.InverseTransformPoint(new Vector3(centerWorld.x, 0f, centerWorld.y));
        _boundingCenterLocal = new Vector2(localCenter3D.x, localCenter3D.z);
    }

    private List<Vector2> RemoveDuplicatePoints(List<Vector2> points, float epsilon)
    {
        List<Vector2> result = new List<Vector2>();

        foreach (var point in points)
        {
            bool isDuplicate = false;
            foreach (var existing in result)
            {
                if (Vector2.Distance(point, existing) < epsilon)
                {
                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate)
                result.Add(point);
        }

        return result;
    }

    private List<Vector2> SelectExtremePoints(List<Vector2> points)
    {
        if (points.Count <= 4) return points;

        List<Vector2> result = new List<Vector2>();

        float minX = points.Min(p => p.x);
        float maxX = points.Max(p => p.x);
        float minZ = points.Min(p => p.y);
        float maxZ = points.Max(p => p.y);

        foreach (var point in points)
        {
            // keep the point if it's at the "outer" part of the shape
            if (Mathf.Approximately(point.x, minX) || Mathf.Approximately(point.x, maxX) ||
                Mathf.Approximately(point.y, minZ) || Mathf.Approximately(point.y, maxZ))
            {
                result.Add(point);
            }
        }
        return OrderPointsByAngle(result);
    }

    // special processing of u shape obstacle to remove extra points
    private List<Vector2> ProcessUShape(List<Vector2> points)
    {
        Vector2 center = Vector2.zero;
        foreach (var p in points) center += p;
        center /= points.Count;

        List<Vector2> sortedPoints = points.OrderBy(p => Mathf.Atan2(p.y - center.y, p.x - center.x)).ToList();

        List<(Vector2 point, float distance)> pointsWithDistance = sortedPoints
            .Select(p => (p, Vector2.Distance(p, center)))
            .ToList();

        pointsWithDistance = pointsWithDistance.OrderBy(p => p.distance).ToList();

        List<Vector2> result = new List<Vector2>();
        for (int i = 0; i < pointsWithDistance.Count; i++)
        {
            if (i != 0 && i != 1) // skip closest points
            {
                result.Add(pointsWithDistance[i].point);
            }
        }

        return OrderPointsByAngle(result);
    }

    private List<Vector2> OrderPointsByAngle(List<Vector2> points)
    {
        if (points.Count <= 3) return new List<Vector2>(points);

        // find midpoint
        Vector2 center = Vector2.zero;
        foreach (var p in points) center += p;
        center /= points.Count;

        // order by angle
        return points.OrderBy(p => Mathf.Atan2(p.y - center.y, p.x - center.x)).ToList();
    }

    public List<Vector2> GetWorldVerticesXZ()
    {
        return vertices
            .Select(v =>
            {
                Vector3 w = transform.TransformPoint(v);
                return new Vector2(w.x, w.z);
            })
            .ToList();
    }

    public List<Vector2> GetOffsetWorldVerticesXZ(float offsetAmount = 0.01f)
    {
        var worldVerts = GetWorldVerticesXZ();
        if (worldVerts.Count < 3) return worldVerts;

        List<Vector2> offsetVerts = new List<Vector2>(worldVerts.Count);

        for (int i = 0; i < worldVerts.Count; i++)
        {
            Vector2 prev = worldVerts[(i - 1 + worldVerts.Count) % worldVerts.Count];
            Vector2 current = worldVerts[i];
            Vector2 next = worldVerts[(i + 1) % worldVerts.Count];

            Vector2 e1 = (current - prev).normalized;
            Vector2 e2 = (next - current).normalized;

            Vector2 n1 = new Vector2(e1.y, -e1.x);
            Vector2 n2 = new Vector2(e2.y, -e2.x);

            Vector2 avg = (n1 + n2).normalized;
            Vector2 offsetVertex = current + avg * offsetAmount;
            offsetVerts.Add(offsetVertex);
        }

        return offsetVerts;
    }

    private void OnDrawGizmos()
    {
        if (!gameObject.CompareTag("Obstacle")) return;

        //Gizmos.color = Color.yellow;
        //Vector3 worldCenter = transform.TransformPoint(new Vector3(_boundingCenterLocal.x, 1.02f, _boundingCenterLocal.y));
        //Gizmos.DrawWireSphere(worldCenter, boundingRadius);

        // draw selected reflex points
        Gizmos.color = Color.red;
        var wv = GetWorldVerticesXZ();
        foreach (var vertex in wv)
        {
            Vector3 worldPos = new Vector3(vertex.x, 1.02f, vertex.y);
            Gizmos.DrawSphere(worldPos, 0.15f);
        }

        
        //Gizmos.color = Color.white;
        //for (int i = 0; i < wv.Count; i++)
        //{
        //    Vector3 a = new Vector3(wv[i].x, 1.01f, wv[i].y);
        //    Vector3 b = new Vector3(wv[(i + 1) % wv.Count].x, 1.01f, wv[(i + 1) % wv.Count].y);
        //    Gizmos.DrawLine(a, b);
        //}
    }
    public Vector2 BoundingCenterLocal => _boundingCenterLocal;
    public float BoundingRadius => boundingRadius;
}