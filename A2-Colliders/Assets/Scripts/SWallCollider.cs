using UnityEngine;

public class SWallCollider : MonoBehaviour, ICustomCollider
{
    public enum SlantDirection
    {
        LeftToBottom,  
        RightToBottom 
    }

    public SlantDirection slantDirection;
    public float restitution = 0.8f;
    public float thickness = 0.5f;
    public Vector3 startPoint;
    public Vector3 endPoint;

    private Vector3 wallNormal;
    private Vector3 wallDirection;

    void Start()
    {
        CalculateWallGeometry();
    }

    void CalculateWallGeometry()
    {
        // calc wall direction
        wallDirection = (endPoint - startPoint).normalized;

        // calc normal: it should point inside the board
        // in our board, z is horizontal, x is vertical. 
        if (slantDirection == SlantDirection.LeftToBottom)
        {
            // normal should be +z, -x
            wallNormal = new Vector3(wallDirection.z, 0, -wallDirection.x).normalized;
        }
        else // RightToBottom
        {
            // should be -z, +x
            wallNormal = new Vector3(-wallDirection.z, 0, wallDirection.x).normalized;
        }

        // make sure normal is pointed towards board
        if (Vector3.Dot(wallNormal, Vector3.right) < 0)
        {
            wallNormal = -wallNormal;
        }
    }

    public bool CheckCollision(Vector3 ballPosition, float ballRadius, out Vector3 collisionNormal, out float penetrationDepth)
    {
        collisionNormal = wallNormal;
        penetrationDepth = 0f;

        // check if the ball is within the range of wall
        Vector3 closestPoint = GetClosestPointOnLineSegment(ballPosition, startPoint, endPoint);
        float distanceToLine = Vector3.Distance(new Vector3(ballPosition.x, 0, ballPosition.z),
                                              new Vector3(closestPoint.x, 0, closestPoint.z));

        // no collision if distance greater than radius + half thickness
        if (distanceToLine > ballRadius + thickness * 0.5f)
            return false;

        // find distance from ball to wall plane
        float distanceToPlane = GetDistanceToSlantedPlane(ballPosition);

        if (distanceToPlane >= 0)
        {
            // ball at "inward" side
            penetrationDepth = ballRadius + thickness * 0.5f - distanceToPlane;
        }
        else
        {
            // ball at other side, use opposite normal (just in case)
            penetrationDepth = ballRadius + thickness * 0.5f + distanceToPlane;
            collisionNormal = -wallNormal;
        }

        return penetrationDepth > 0;
    }

    Vector3 GetClosestPointOnLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineDirection = lineEnd - lineStart;
        float lineLength = lineDirection.magnitude;
        lineDirection.Normalize();

        float projectLength = Mathf.Clamp(Vector3.Dot(point - lineStart, lineDirection), 0, lineLength);
        return lineStart + lineDirection * projectLength;
    }

    float GetDistanceToSlantedPlane(Vector3 ballPosition)
    {
        Vector3 pointOnPlane = startPoint;
        Vector3 planeNormal = wallNormal;

        return Vector3.Dot(ballPosition - pointOnPlane, planeNormal);
    }

    public void HandleCollision(ref Vector3 velocity, Vector3 collisionNormal, Vector3 ballPosition, float restitution)
    {
        float velocityAlongNormal = Vector3.Dot(velocity, collisionNormal);

        if (velocityAlongNormal < 0)
        {
            velocity -= (1 + restitution) * velocityAlongNormal * collisionNormal;
        }
    }

    //
    //void OnDrawGizmos()
    //{
    //    if (startPoint != Vector3.zero && endPoint != Vector3.zero)
    //    {
    //        CalculateWallGeometry();
    //        Gizmos.color = Color.cyan;
    //        Gizmos.DrawLine(startPoint, endPoint);

    //        Gizmos.color = new Color(0, 1, 1, 0.5f);
    //        Vector3 thicknessOffset = wallNormal * thickness;
    //        Gizmos.DrawLine(startPoint, startPoint + thicknessOffset);
    //        Gizmos.DrawLine(endPoint, endPoint + thicknessOffset);
    //        Gizmos.DrawLine(startPoint + thicknessOffset, endPoint + thicknessOffset);
    //        Vector3 midPoint = (startPoint + endPoint) / 2;
    //        Gizmos.color = Color.yellow;
    //        Gizmos.DrawLine(midPoint, midPoint + wallNormal * 2f);
    //        Gizmos.color = Color.white;
    //        Gizmos.DrawLine(midPoint, midPoint + wallNormal * thickness * 0.5f);
    //    }
    //}
}