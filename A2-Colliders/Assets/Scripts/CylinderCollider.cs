using UnityEngine;

public class CylinderCollider : MonoBehaviour, ICustomCollider
{
    [Header("Cylinder Properties")]
    public float radius = 2.0f;
    public float energyBoost = 1.5f;

    private Vector3 center;

    void Start()
    {
        center = transform.position;
    }

    public bool CheckCollision(Vector3 ballPosition, float ballRadius,
                               out Vector3 collisionNormal, out float penetrationDepth)
    {
        collisionNormal = Vector3.zero;
        penetrationDepth = 0f;

        // calc on XZ plane
        Vector2 ballPos2D = new Vector2(ballPosition.x, ballPosition.z);
        Vector2 center2D = new Vector2(center.x, center.z);

        float distance = Vector2.Distance(ballPos2D, center2D);
        float combinedRadius = radius + ballRadius;

        if (distance < combinedRadius)
        {
            Vector3 toBall = ballPosition - center;
            toBall.y = 0; // ignore Y
            collisionNormal = toBall.normalized;
            penetrationDepth = combinedRadius - distance;
            return true;
        }

        return false;
    }

    public void HandleCollision(ref Vector3 velocity, Vector3 collisionNormal,
                                Vector3 ballPosition, float restitution)
    {
        float velocityAlongNormal = Vector3.Dot(velocity, collisionNormal);

        if (velocityAlongNormal < 0)
        {
            // change velocity with higher boost
            velocity -= (1 + restitution * energyBoost) * velocityAlongNormal * collisionNormal;
        }
    }

    //void OnDrawGizmos()
    //{
    //    Gizmos.color = Color.yellow;

    //    DrawCircleGizmo(center, radius);

    //}

    //void DrawCircleGizmo(Vector3 center, float radius)
    //{
    //    Vector3[] points = GetCirclePoints(center, radius, 16);

    //    for (int i = 0; i < 16; i++)
    //    {
    //        Gizmos.DrawLine(points[i], points[(i + 1) % 16]);
    //    }
    //}

    //Vector3[] GetCirclePoints(Vector3 center, float radius, int segments)
    //{
    //    Vector3[] points = new Vector3[segments];

    //    for (int i = 0; i < segments; i++)
    //    {
    //        float angle = i * (360f / segments) * Mathf.Deg2Rad;
    //        points[i] = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius)+ Vector3.up * 1.5f;
    //    }

    //    return points;
    //}
}