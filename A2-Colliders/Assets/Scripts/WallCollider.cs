using UnityEngine;
// a custom interface to unite all handle / check collision methods
public interface ICustomCollider
{
    bool CheckCollision(Vector3 ballPosition, float ballRadius, out Vector3 collisionNormal, out float penetrationDepth);
    void HandleCollision(ref Vector3 velocity, Vector3 collisionNormal, Vector3 ballPosition, float restitution);
}

public class WallCollider : MonoBehaviour, ICustomCollider
{
    public Vector3 normal; 
    public float restitution = 0.8f;
    public float thickness = 0.5f;

    public bool CheckCollision(Vector3 ballPosition, float ballRadius, out Vector3 collisionNormal, out float penetrationDepth)
    {
        collisionNormal = normal;
        penetrationDepth = 0f;

        // calculate distance from ball to wall plane
        Vector3 wallPosition = transform.position;
        float distanceToPlane = Vector3.Dot(ballPosition - wallPosition, normal);

        // if the distance > radius and half of thickness, there's no collision
        if (distanceToPlane > ballRadius + thickness * 0.5f || distanceToPlane < -ballRadius - thickness * 0.5f)
            return false;


        // calc penetration
        if (distanceToPlane >= 0)
        {
            penetrationDepth = ballRadius + thickness * 0.5f - distanceToPlane;
        }
        else
        {
            // use opposite normal if ball's at back of wall.
            penetrationDepth = ballRadius + thickness * 0.5f + distanceToPlane;
            collisionNormal = -normal;
        }

        return penetrationDepth > 0;
    }

    public void HandleCollision(ref Vector3 velocity, Vector3 collisionNormal, Vector3 ballPosition, float restitution)
    {
        float velocityAlongNormal = Vector3.Dot(velocity, collisionNormal);
        // change velocity of ball
        if (velocityAlongNormal < 0)
        {
            velocity -= (1 + restitution) * velocityAlongNormal * collisionNormal;
        }
    }

    //void OnDrawGizmos()
    //{
    //    Vector3 wallPosition = transform.position;
    //    Gizmos.color = Color.blue;
    //    Vector3 frontPoint = wallPosition + normal * thickness * 0.5f;
    //    DrawPlaneGizmo(frontPoint, normal);
    //    Gizmos.color = new Color(0, 0, 1, 0.3f); 
    //    Vector3 backPoint = wallPosition - normal * thickness * 0.5f;
    //    DrawPlaneGizmo(backPoint, -normal);
    //    Gizmos.color = Color.cyan;
    //    Gizmos.DrawLine(frontPoint, backPoint);
    //}

    //void DrawPlaneGizmo(Vector3 position, Vector3 normal)
    //{
    //    Vector3 tangent1 = Vector3.Cross(normal, Vector3.up);
    //    if (tangent1.magnitude < 0.1f)
    //        tangent1 = Vector3.Cross(normal, Vector3.forward);
    //    tangent1.Normalize();

    //    Vector3 tangent2 = Vector3.Cross(normal, tangent1);
    //    tangent2.Normalize();
    //    float size = 2f;
    //    Vector3 corner1 = position + tangent1 * size + tangent2 * size;
    //    Vector3 corner2 = position + tangent1 * size - tangent2 * size;
    //    Vector3 corner3 = position - tangent1 * size - tangent2 * size;
    //    Vector3 corner4 = position - tangent1 * size + tangent2 * size;
    //    Gizmos.DrawLine(corner1, corner2);
    //    Gizmos.DrawLine(corner2, corner3);
    //    Gizmos.DrawLine(corner3, corner4);
    //    Gizmos.DrawLine(corner4, corner1);
    //}
}