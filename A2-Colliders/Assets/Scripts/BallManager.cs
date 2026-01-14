using UnityEngine;
using System.Collections.Generic;

public class BallManager : MonoBehaviour
{
    public Pinball ballPrefab; 
    public int maxBalls = 2;

    public float jitterImpulse = 2.2f;
    public float jitterCooldown = 0.15f;
    private float _lastJitterTime = -999f;

    private readonly List<Pinball> _balls = new List<Pinball>();

    void Update()
    {
        // spawn when hit space key if there is room
        if (Input.GetKeyDown(KeyCode.Space) && _balls.Count < maxBalls)
        {
            SpawnBall();
        }

        // table jitter
        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (Time.time - _lastJitterTime >= jitterCooldown)
            {
                _lastJitterTime = Time.time;
                foreach (var b in _balls)
                {
                    if (b == null) continue;
                    // small planar impulse
                    var kick = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * jitterImpulse;
                    b.ApplyImpulse(kick);
                }
            }
        }

        // pairwise ball–ball collisions
        for (int i = 0; i < _balls.Count; ++i)
        {
            for (int j = i + 1; j < _balls.Count; ++j)
            {
                ResolveBallBall(_balls[i], _balls[j]);
            }
        }

        // cleanup any despawned balls
        for (int i = _balls.Count - 1; i >= 0; --i)
        {
            if (_balls[i] == null) _balls.RemoveAt(i);
        }
    }

    void SpawnBall()
    {
        var b = Instantiate(ballPrefab, Vector3.up, Quaternion.identity);
        b.InitializeBallAtRandomPosition(); 
        b.OnDespawn += HandleDespawn;
        _balls.Add(b);
    }

    void HandleDespawn(Pinball b)
    {
        // called by the ball when it goes out to gutter
        int idx = _balls.IndexOf(b);
        if (idx >= 0) _balls.RemoveAt(idx);
        if (b != null) Destroy(b.gameObject);
    }

    void ResolveBallBall(Pinball a, Pinball c)
    {
        if (a == null || c == null) return;

        Vector3 pa = a.position;
        Vector3 pc = c.position;
        float ra = a.radius;
        float rc = c.radius;

        Vector3 delta = pc - pa;
        // work in XZ plane, leave change in y
        Vector2 da = new Vector2(delta.x, delta.z);
        float dist = da.magnitude;
        float minDist = ra + rc;

        if (dist > 0f && dist < minDist)
        {
            // positional correction 
            float penetration = (minDist - dist);
            Vector2 n2 = da / dist;
            Vector2 correction = n2 * (penetration * 0.5f);

            // move each by half in opposite directions
            a.position += new Vector3(-correction.x, 0f, -correction.y);
            c.position += new Vector3(correction.x, 0f, correction.y);
            a.transform.position = a.position;
            c.transform.position = c.position;

            Vector3 va = a.velocity;
            Vector3 vc = c.velocity;

            Vector3 n3 = new Vector3(n2.x, 0f, n2.y);      // lift back to 3D in XZ
            float relAlongN = Vector3.Dot(vc - va, n3);
            if (relAlongN < 0f)
            {
                float restitution = 0.9f;                  // tweak
                float j = -(1f + restitution) * relAlongN * 0.5f;  // 0.5 because equal masses

                Vector3 impulse = n3 * j;
                a.velocity -= impulse;
                c.velocity += impulse;

                // Speed cap
                CapSpeed(a, 22f);
                CapSpeed(c, 22f);
            }
        }
    }

    void CapSpeed(Pinball b, float maxSpd)
    {
        float sp = b.velocity.magnitude;
        if (sp > maxSpd) b.velocity *= (maxSpd / sp);
    }
}
