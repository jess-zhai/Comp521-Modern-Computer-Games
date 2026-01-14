using UnityEngine;

public class PaddleCollider : MonoBehaviour, ICustomCollider
{
    public enum PaddleSide { Left, Right }
    public Transform pivotAnchor;
    // since paddle is basically triangle collider, use base and height to find the vertices and sides
    public float height = 7.0f;
    public float baseWidth = 1.5f;

    public PaddleSide paddleSide = PaddleSide.Left; // calculation depends on which side it is
    public float currentAngle = 0f;

    private float restAngle = -10f;
    private float liftAngle = 45f;
    private float rotationSpeed = 200f;

    [Range(0f, 0.2f)] public float tangentialDamping = 0.05f; // prevent sliding
    public bool oneSided = true; // prevent ball stuck in side

    //public bool drawGizmos = true;
    //public Color edgeColor = new(0.1f, 0.7f, 1f, 1f);
    //public Color normalColor = new(1f, 0.4f, 0.1f, 1f);
    //public Color contactColor = Color.yellow;
    //[Range(0.1f, 1.5f)] public float normalLen = 0.8f;

    private Vector3 pivotPoint;
    private float previousAngle = 0f;
    private float omegaY = 0f;
    private Vector3[] vtx = new Vector3[3];
    private Edge[] edges = new Edge[3];
    private Vector3[] vtxOutward = new Vector3[3];

    private float targetAngle = 0f;
    private bool isMoving = false;

    private struct Edge { public Vector3 a, b, nOut; }

    // last contact (for v_surf)
    private bool hasLastContact = false;
    private Vector3 lastContactPoint = Vector3.zero;

    private Quaternion initialRotation;

    // setups to prevent "stucking on ball"
    private const float kPenSlop = 0.002f; 
    private const float kHitEps = 1e-5f;
    private const float kSepThresh = 0.35f;
    private const float kSepBias = 0.55f; 

    void Start()
    {
        // init pivot point, and set all angle to rest angle
        pivotPoint = pivotAnchor.position;
        currentAngle = restAngle;
        targetAngle = restAngle;
        previousAngle = restAngle;

        initialRotation = transform.rotation;
        RebuildTriangle();
    }

    void Update()
    {
        HandleInput(); // take user's keyboard input

        pivotPoint = pivotAnchor.position;
        // handle rotation
        float newAngle = Mathf.MoveTowards(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
        isMoving = Mathf.Abs(newAngle - currentAngle) > 0.01f;
        currentAngle = newAngle;

        float dt = Mathf.Max(Time.deltaTime, 1e-5f); // give small epsilon to prevent weird behavior
        omegaY = (currentAngle - previousAngle) * Mathf.Deg2Rad / dt;
        previousAngle = currentAngle;

        UpdatePaddleTransform();
        RebuildTriangle();
    }

    void UpdatePaddleTransform()
    {
        float actualAngle = (paddleSide == PaddleSide.Left) ? -currentAngle : currentAngle;
        Vector3 newRotation = initialRotation.eulerAngles;
        newRotation.y = actualAngle;
        transform.rotation = Quaternion.Euler(newRotation);
    }

    void HandleInput()
    {
        if (paddleSide == PaddleSide.Left)
            targetAngle = Input.GetKey(KeyCode.A) ? liftAngle : restAngle;
        else
            targetAngle = Input.GetKey(KeyCode.D) ? liftAngle : restAngle;
    }

    void RebuildTriangle()
    {
        Vector3 up = Vector3.up;

        // from the direction of base side, take the "right" direction and project to plane XZ
        Vector3 baseDir = Vector3.ProjectOnPlane(transform.right, up).normalized;
        if (baseDir.sqrMagnitude < 1e-8f) baseDir = Vector3.right;

        Vector3 baseCenter = pivotPoint;

        // position of 2 points on both sides of base
        Vector3 baseHalf = baseDir * (baseWidth * 0.5f);
        Vector3 baseL = baseCenter - baseHalf;
        Vector3 baseR = baseCenter + baseHalf;

        // the direction of the tip is normal to base, at height units away
        Vector3 perp = new Vector3(-baseDir.z, 0f, baseDir.x);
        float tipSign = (paddleSide == PaddleSide.Left) ? -1f : 1f;
        Vector3 tip = pivotPoint + tipSign * perp * height;

        // CCW order
        Vector3 a = tip, b = baseL, c = baseR;
        if (!IsCCW(a, b, c)) { (b, c) = (c, b); }
        vtx[0] = a; vtx[1] = b; vtx[2] = c;

        // construct sides and normals
        Vector3 centroid = (vtx[0] + vtx[1] + vtx[2]) / 3f; centroid.y = 0f;
        for (int i = 0; i < 3; ++i)
        {
            Vector3 va = vtx[i];
            Vector3 vb = vtx[(i + 1) % 3];

            Vector3 e = vb - va;
            Vector3 t = new Vector3(e.x, 0f, e.z);
            if (t.sqrMagnitude < 1e-8f) t = Vector3.right;
            t.Normalize();

            // in CCW direction, the "left" normal is pointing out, which is what we want
            Vector3 n = new Vector3(-t.z, 0f, t.x);

            // make sure the normal is pointing out, else, reverse it
            Vector3 mid = 0.5f * (va + vb);
            Vector3 outDir = new Vector3(mid.x - centroid.x, 0f, mid.z - centroid.z);
            if (Vector3.Dot(n, outDir) < 0f) n = -n;

            edges[i].a = va;
            edges[i].b = vb;
            edges[i].nOut = n.normalized;
        }

        // for every vertex, calculate the normal, which is sum of normal of neighbouring sides
        for (int i = 0; i < 3; ++i)
        {
            Vector3 n0 = edges[(i + 2) % 3].nOut; // the last side that ends with vertex i
            Vector3 n1 = edges[i].nOut;           // the next side that starts with i
            Vector3 vN = n0 + n1;
            if (vN.sqrMagnitude < 1e-8f) vN = n1; // fallback to just use the same as next side
            vtxOutward[i] = vN.normalized;
        }
    }

    static bool IsCCW(Vector3 a, Vector3 b, Vector3 c)
    {
        float area2 = a.x * (b.z - c.z) + b.x * (c.z - a.z) + c.x * (a.z - b.z);
        return area2 > 0f;
    }

    public bool CheckCollision(Vector3 ballPos, float ballR, out Vector3 normal, out float penetration)
    {
        normal = Vector3.zero;
        penetration = 0f;
        hasLastContact = false;

        float bestPen = float.PositiveInfinity;
        Vector3 bestN = Vector3.zero;
        Vector3 bestC = Vector3.zero;
        bool hit = false;

        // loop through all sides
        for (int i = 0; i < 3; ++i)
        {
            Vector3 a = edges[i].a, b = edges[i].b;
            Vector3 ab = b - a;

            // closest point in 2D, from ball centre to the side
            Vector2 ab2 = new Vector2(ab.x, ab.z);
            float len2 = Mathf.Max(1e-8f, Vector2.Dot(ab2, ab2));
            Vector2 toBall = new Vector2(ballPos.x - a.x, ballPos.z - a.z);
            float t = Mathf.Clamp01(Vector2.Dot(toBall, ab2) / len2);
            Vector3 closest = a + t * ab;

            // only respond to ball coming from "outside", avoid trapping ball
            Vector3 toCenter = new Vector3(ballPos.x - closest.x, 0f, ballPos.z - closest.z);
            if (oneSided && Vector3.Dot(edges[i].nOut, toCenter) < 0f)
                continue;

            // calc penetration and distance
            float dist = toCenter.magnitude;
            float pen = (ballR + kPenSlop) - dist;

            if (pen > 0f)
            {
                // if t is close to 0 or 1 (very close to a vertex), we'll use the vertez
                // but still register the side. yet it'll only be chosen if it has smaller pen.
                Vector3 n = (dist > kHitEps) ? (toCenter / dist) : edges[i].nOut;
                if (pen < bestPen)
                {
                    bestPen = pen;
                    bestN = n;
                    bestC = closest;
                    hit = true;
                }
            }
        }

        // vertex candidates
        for (int i = 0; i < 3; ++i)
        {
            Vector3 v = vtx[i];
            Vector3 d = new Vector3(ballPos.x - v.x, 0f, ballPos.z - v.z);
            float dist = d.magnitude;
            float pen = (ballR + kPenSlop) - dist;
            if (pen <= 0f) continue;

            // use outward normal to see if the ball is outside the paddle or inside paddle.
            // ignore inside paddle situation
            if (oneSided && Vector3.Dot(vtxOutward[i], d) < 0f)
                continue;

            // try use direction of ball first, else use normal
            Vector3 n = (dist > kHitEps) ? (d / dist) : vtxOutward[i];

            if (pen < bestPen)
            {
                bestPen = pen;
                bestN = n;
                bestC = v; 
                hit = true;
            }
        }

        if (hit)
        {
            normal = bestN;
            penetration = bestPen;
            hasLastContact = true;
            lastContactPoint = bestC;
            return true;
        }
        return false;
    }

    public void HandleCollision(ref Vector3 velocity, Vector3 n, Vector3 ballPosition, float restitutionFromCaller)
    {
        Vector3 pC = hasLastContact ? lastContactPoint : ballPosition;

        // v_surf = ω × r
        Vector3 r = pC - pivotPoint;
        if (paddleSide == PaddleSide.Left)
        {
            omegaY = -omegaY;
        }
        Vector3 vSurf = Vector3.Cross(new Vector3(0f, omegaY, 0f), r);

        // restitution depends on moving or not
        float e = isMoving ? 1.2f : 0.8f;

        // use relative velocity
        float vn = Vector3.Dot(velocity - vSurf, n);

        if (vn < -1e-3f)
        {
            velocity -= (1f + e) * vn * n;

            // reduce "sticking" on side
            Vector3 vRel = velocity - vSurf;
            Vector3 vt = vRel - Vector3.Dot(vRel, n) * n;
            velocity -= tangentialDamping * vt;
        }
        else
        {
            // when "touching" but not separating, provide a small velocity to separate
            if (vn < kSepThresh)
            {
                float add = (kSepBias - vn);
                if (add > 0f) velocity += add * n;
            }
        }

        hasLastContact = false;
    }

//    void OnDrawGizmos()
//    {
//        if (!drawGizmos) return;

//        pivotPoint = pivotAnchor ? pivotAnchor.position : transform.position;
//        if (!Application.isPlaying)
//        {
//            currentAngle = restAngle;
//            RebuildTriangle();
//        }

//        Gizmos.color = edgeColor;
//        Gizmos.DrawLine(vtx[0], vtx[1]);
//        Gizmos.DrawLine(vtx[1], vtx[2]);
//        Gizmos.DrawLine(vtx[2], vtx[0]);

//        Gizmos.color = normalColor;
//        for (int i = 0; i < 3; ++i)
//        {
//            Vector3 mid = 0.5f * (edges[i].a + edges[i].b);
//            Gizmos.DrawLine(mid, mid + edges[i].nOut.normalized * normalLen);

//            Vector3 v = vtx[i];
//            Gizmos.DrawLine(v, v + vtxOutward[i] * (normalLen * 0.8f));
//        }

//        Gizmos.color = Color.green;
//        Gizmos.DrawSphere(pivotPoint, 0.06f);

//        if (hasLastContact)
//        {
//            Gizmos.color = contactColor;
//            Gizmos.DrawSphere(lastContactPoint, 0.06f);
//        }

//#if UNITY_EDITOR
//        UnityEditor.Handles.Label(pivotPoint + Vector3.up * 0.5f,
//            $"Side: {paddleSide}\nAngle: {currentAngle:F1}°\nMoving: {isMoving}\nω (deg/s): {(omegaY * Mathf.Rad2Deg):F1}");
//#endif
//    }
}
