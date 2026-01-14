using UnityEngine;
using System;
using System.Collections.Generic;

public class Pinball : MonoBehaviour
{
    public event Action<Pinball> OnDespawn; // for manager to control spawning

    public Vector3 position;
    public Vector3 velocity;
    public float radius = 0.5f;

    public float gravity = -5.0f; // pointing at -x direction
    private float boardY = 1f;
    public float maxSpeed = 22f; // prevent ball too fast
    private bool _initialized = false;

    // list to hold all colliders
    private readonly List<ICustomCollider> colliders = new List<ICustomCollider>();

    void Start()
    {
        FindAllColliders(); // add all colliders on board

        if (!_initialized) // usually won't trigger until the initializer in manager fails
            InitializeBallAtRandomPosition();
    }

    // Find a valid position for ball to be spawn at. Called by BallManager after instantiation
    public void InitializeBallAtRandomPosition()
    {
        Vector3 randomPos;
        bool validPosition = false;
        int attempts = 0;
        const int maxAttempts = 100;

        while (!validPosition && attempts < maxAttempts)
        {
            // a rectangular area I chose to avoid boarder walls, paddles, etc
            randomPos = new Vector3(
                UnityEngine.Random.Range(-9f + radius, 24f - radius),
                boardY,
                UnityEngine.Random.Range(-13f + radius, 13f - radius)
            );
            // if not overlapping with any obstacle, set it to this position
            if (!OverlapsWithObstacles(randomPos))
            {
                position = randomPos;
                velocity = Vector3.zero;
                validPosition = true;
            }
            //Debug.Log("pinball try failed");
            attempts++;
        }
        // if fail all attempts, fall back to init at 0,1,0.
        if (!validPosition)
        {
            position = new Vector3(0f, boardY, 0f);
            velocity = Vector3.zero;
        }

        transform.position = position;
        _initialized = true;
    }

    void Update()
    {
        // update physics every frame if initialized & spawned
        if (position != Vector3.zero)
        {
            UpdatePhysics(Time.deltaTime);
        }
    }

    // check collision with each obstacle / wall. the obstacles will return the distance between ball and itself,
    // in this case, it is actually the potential position of the ball and the obstacle.
    bool OverlapsWithObstacles(Vector3 testPosition)
    {
        foreach (var collider in colliders)
        {
            if (collider.CheckCollision(testPosition, radius, out _, out _))
                return true;
        }
        return false;
    }
    // for the BallManager to use, not used here
    public void ApplyImpulse(Vector3 dv)
    {
        velocity += dv;
    }

    void UpdatePhysics(float dt)
    {
        // apply gravity (to -x direction)
        velocity.x += gravity * dt;

        // limit the speed
        float sp = velocity.magnitude;
        if (sp > maxSpeed) velocity = velocity * (maxSpeed / sp);

        // next position of ball if applying the velocity
        Vector3 newPosition = position + velocity * dt;

        // try resolve collisions (for 6 times max)
        ResolveCollisionsIterative(ref newPosition, ref velocity);

        // lock y to board because we're basically 2d
        newPosition.y = boardY;

        position = newPosition; 
        transform.position = position;

        // falling into gutter. BallManager will destroy this ball
        if (position.x < -20f - radius)
        {
            OnDespawn?.Invoke(this);
        }
    }

    void ResolveCollisionsIterative(ref Vector3 pos, ref Vector3 vel)
    {
        const int maxIters = 6;

        for (int iter = 0; iter < maxIters; ++iter)
        {
            ICustomCollider bestCol = null;
            Vector3 bestN = Vector3.zero;
            float bestPen = float.PositiveInfinity;

            // iteratively check the collision objects with smallest penestration
            foreach (var col in colliders)
            {
                if (col.CheckCollision(pos, radius, out var n, out var pen))
                {
                    if (pen > 0f && pen < bestPen)
                    {
                        bestPen = pen;
                        bestN = n;
                        bestCol = col;
                    }
                }
            }

            if (bestCol == null) break;

            pos += bestN * (bestPen + 0.0015f);
            bestCol.HandleCollision(ref vel, bestN, pos, 0.8f); // the obstacle itself will handle the collision
        }
    }

    // add all colliders to list
    void FindAllColliders()
    {
        colliders.Clear();
        WallCollider[] walls = FindObjectsByType<WallCollider>(FindObjectsSortMode.None);
        foreach (var wall in walls) colliders.Add(wall);

        SWallCollider[] slantedWalls = FindObjectsByType<SWallCollider>(FindObjectsSortMode.None);
        foreach (var slantedWall in slantedWalls) colliders.Add(slantedWall);

        PaddleCollider[] paddles = FindObjectsByType<PaddleCollider>(FindObjectsSortMode.None);
        foreach (var paddle in paddles) colliders.Add(paddle);

        TriangularPrismCollider[] triangles = FindObjectsByType<TriangularPrismCollider>(FindObjectsSortMode.None);
        foreach (var triangle in triangles) colliders.Add(triangle);

        CylinderCollider[] cylinders = FindObjectsByType<CylinderCollider>(FindObjectsSortMode.None);
        foreach (var cylinder in cylinders) colliders.Add(cylinder);
    }
}
