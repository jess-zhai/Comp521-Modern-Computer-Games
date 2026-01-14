using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class AgentManager : MonoBehaviour
{
    [Header("References")]
    public GameObject agentPrefab;
    public GameObject goalPrefab;
    public LevelGenerator levelGenerator;

    [Header("Agent Sizes")]
    public Vector2 smallSize = new Vector2(2f, 2f);
    public Vector2 mediumSize = new Vector2(3f, 3f);
    public Vector2 largeSize = new Vector2(4f, 4f);

    [Header("Agent Materials / Colors")]
    public Material smallMat;
    public Material mediumMat;
    public Material largeMat;

    private GameObject currentAgent;
    private GameObject currentGoal;

    [Header("UI Buttons")]
    public Button smallButton;
    public Button mediumButton;
    public Button largeButton;
    public Button testButton; // generates new random agent and destination
    public Button toggleModeButton;

    [Header("Test Positions")]
    public Vector3 testAgentPos;
    public Vector3 testGoalPos;

    private RVGGenerator rvgGenerator;
    private Pathfinder pathfinder;
    private Coroutine moveRoutine;
    private ImprovedPathfinder improvedPf;
    public PathfindingMode mode = PathfindingMode.NaiveRVG;
    private List<Vector3> lastPath = new List<Vector3>();
    private float lastPathCost = 0f;

    private void Start()
    {
        rvgGenerator = FindObjectsByType<RVGGenerator>(FindObjectsSortMode.None)[0];
        pathfinder = FindObjectsByType<Pathfinder>(FindObjectsSortMode.None)[0];
        improvedPf = FindObjectsByType<ImprovedPathfinder>(FindObjectsSortMode.None)[0];

        // bind button event
        if (smallButton) smallButton.onClick.AddListener(() => SpawnAgent(smallSize, smallMat));
        if (mediumButton) mediumButton.onClick.AddListener(() => SpawnAgent(mediumSize, mediumMat));
        if (largeButton) largeButton.onClick.AddListener(() => SpawnAgent(largeSize, largeMat));
        if (testButton) testButton.onClick.AddListener(GenerateNewTestScenario);
        if (toggleModeButton) toggleModeButton.onClick.AddListener(ToggleModeAndRecalculate);

    }

    public void GenerateNewTestScenario()
    {
        // Generate new random positions
        testAgentPos = GetRandomValidPosition(2f);
        testGoalPos = GetRandomValidPosition(2f);

        Debug.Log($"[Test] New scenario: Agent={testAgentPos}, Goal={testGoalPos}");

        // Calculate and display path with current mode
        RecalculatePathWithCurrentMode();
    }

    public void ToggleModeAndRecalculate()
    {
        if (mode == PathfindingMode.NaiveRVG)
        {
            mode = PathfindingMode.ImprovedRDP;
            Debug.Log("Switched to Improved RVG (purple)");
        }
        else
        {
            mode = PathfindingMode.NaiveRVG;
            Debug.Log("Switched to Naive RVG (red)");
        }

        rvgGenerator.displayMode = mode;

        // Recalculate path with the new mode using the same positions
        RecalculatePathWithCurrentMode();
    }

    private void RecalculatePathWithCurrentMode()
    {
        // Remove old agent/goal if they exist
        if (currentAgent) Destroy(currentAgent);
        if (currentGoal) Destroy(currentGoal);

        // Create new agent and goal at test positions
        currentAgent = Instantiate(agentPrefab, testAgentPos, Quaternion.identity);
        currentGoal = Instantiate(goalPrefab, testGoalPos, Quaternion.identity);

        // Small agent visual setup for consistent testing
        currentAgent.transform.localScale = new Vector3(smallSize.x, 1f, smallSize.y);
        MeshRenderer renderer = currentAgent.GetComponentInChildren<MeshRenderer>();
        if (renderer && smallMat) renderer.material = smallMat;

        // Update RVG with new points
        rvgGenerator.AddDynamicPoints(currentAgent.transform, currentGoal.transform);

        // Stop any existing movement
        if (moveRoutine != null) StopCoroutine(moveRoutine);

        // Calculate path with current mode
        List<Vector3> path = improvedPf.FindPathWithRDP(testAgentPos, testGoalPos, mode);
        lastPath = path;
        lastPathCost = ComputeTotalPathCost(path);

        Debug.Log($"[Pathfinder] Mode={mode}, Path points={path.Count}, Total cost={lastPathCost:F2}");

        // Start movement if path is valid
        if (path.Count > 0)
            moveRoutine = StartCoroutine(FollowPath(path));
        else
            Debug.LogWarning("[Pathfinder] No valid path found!");
    }

    public void SpawnAgent(Vector2 size, Material mat)
    {
        // Remove old agent and goal
        if (currentAgent) Destroy(currentAgent);
        if (currentGoal) Destroy(currentGoal);

        float radius = size.x / 2f;
        Vector3 pos = GetRandomValidPosition(radius);
        currentAgent = Instantiate(agentPrefab, pos, Quaternion.identity);
        currentAgent.transform.localScale = new Vector3(size.x, 1f, size.y);

        // Change mat for diff size agent, show diff colour 
        MeshRenderer renderer = currentAgent.GetComponentInChildren<MeshRenderer>();
        if (renderer != null)
        {
            if (mat != null) renderer.material = mat;
            else
            {
                if (Mathf.Approximately(size.x, smallSize.x)) renderer.material.color = Color.green;
                else if (Mathf.Approximately(size.x, mediumSize.x)) renderer.material.color = Color.blue;
                else renderer.material.color = Color.red;
            }
        }

        // Create random goal
        Vector3 goalPos = GetRandomValidPosition(radius);
        currentGoal = Instantiate(goalPrefab, goalPos, Quaternion.identity);
        currentGoal.name = "Goal";

        // Update test positions for consistency
        testAgentPos = pos;
        testGoalPos = goalPos;

        rvgGenerator.AddDynamicPoints(currentAgent.transform, currentGoal.transform);
        if (moveRoutine != null) StopCoroutine(moveRoutine);

        List<Vector3> path = improvedPf.FindPathWithRDP(pos, goalPos, mode);
        lastPath = path;
        lastPathCost = ComputeTotalPathCost(path);
        Debug.Log($"[Pathfinder] Mode={mode}, Path length={path.Count}, Total cost={lastPathCost:F2}");

        if (path.Count > 0)
            moveRoutine = StartCoroutine(FollowPath(path));
    }

    public float ComputeTotalPathCost(List<Vector3> path)
    {
        if (path == null || path.Count < 2) return 0f;

        LevelGenerator level = FindObjectsByType<LevelGenerator>(FindObjectsSortMode.None)[0];
        var regions = level.GetRegions();
        float total = 0f;
        for (int i = 0; i < path.Count - 1; i++)
            total += improvedPf.GetSegmentCost(path[i], path[i + 1]);
        return total;
    }

    private System.Collections.IEnumerator FollowPath(List<Vector3> path)
    {
        if (path.Count < 2) yield break;

        LevelGenerator level = FindObjectsByType<LevelGenerator>(FindObjectsSortMode.None)[0];
        var regions = level.GetRegions();

        int idx = 0;
        while (idx < path.Count - 1)
        {
            Vector3 start = path[idx];
            Vector3 target = path[idx + 1];

            float terrainCost = improvedPf.GetTerrainCostAtPoint((start + target) * 0.5f, regions);
            float speed = Mathf.Lerp(60f, 30f, Mathf.InverseLerp(0.5f, 5f, terrainCost));

            while (Vector3.Distance(currentAgent.transform.position, target) > 0.1f)
            {
                currentAgent.transform.position = Vector3.MoveTowards(
                    currentAgent.transform.position, target, speed * Time.deltaTime);
                yield return null;
            }
            idx++;
        }
    }

    public Vector3 GetRandomValidPosition(float radius)
    {
        Vector3 planeCenter = levelGenerator.plane.transform.position;
        Vector3 planeSize = levelGenerator.plane.GetComponent<Renderer>().bounds.size;

        int maxAttempts = 100;
        for (int i = 0; i < maxAttempts; i++)
        {
            float x = Random.Range(
                planeCenter.x - planeSize.x / 2 + radius,
                planeCenter.x + planeSize.x / 2 - radius);
            float z = Random.Range(
                planeCenter.z - planeSize.z / 2 + radius,
                planeCenter.z + planeSize.z / 2 - radius);
            Vector3 candidate = new Vector3(x, 1f, z);

            if (IsFreeOfObstacles(candidate, radius))
                return candidate;
        }

        Debug.LogWarning("Could not find valid spawn position, using center");
        return new Vector3(planeCenter.x, 1f, planeCenter.z);
    }

    public bool IsFreeOfObstacles(Vector3 position, float radius)
    {
        foreach (GameObject obs in levelGenerator.GetObstacles())
        {
            ObstacleGeometry geo = obs.GetComponent<ObstacleGeometry>();
            float obsR = (geo != null ? geo.boundingRadius : 1.5f);
            float dist = Vector3.Distance(position, obs.transform.position);
            if (dist < (radius + obsR + 1.0f))
                return false;
        }
        return true;
    }

    private void OnDrawGizmos()
    {
        if (lastPath == null || lastPath.Count < 2) return;

        // Use different colors for different modes
        if (mode == PathfindingMode.ImprovedRDP)
            Gizmos.color = new Color(0.8f, 0.3f, 0.8f); // Purple for improved
        else
            Gizmos.color = Color.red; // Red for naive

        // Draw the path
        for (int i = 0; i < lastPath.Count - 1; i++)
            Gizmos.DrawLine(lastPath[i] + Vector3.up * 0.2f, lastPath[i + 1] + Vector3.up * 0.2f);

#if UNITY_EDITOR
        // Display cost info
        Vector3 labelPos = lastPath[lastPath.Count / 2] + Vector3.up * 0.5f;
        string modeText = mode == PathfindingMode.ImprovedRDP ? "Improved" : "Naive";
        UnityEditor.Handles.Label(labelPos, $"{modeText} Mode\nCost: {lastPathCost:F2}\nPoints: {lastPath.Count}");
#endif
    }
}