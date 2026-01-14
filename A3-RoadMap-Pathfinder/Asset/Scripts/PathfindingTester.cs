using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PathfindingTester : MonoBehaviour
{
    [Header("Test Settings")]
    public int testCount = 100;
    public float agentRadius = 2f;
    public bool runOnStart = true;

    [Header("References")]
    public AgentManager agentManager;
    public RVGGenerator rvgGenerator;
    public Pathfinder pathfinder;
    public ImprovedPathfinder improvedPathfinder;

    private class TestResult
    {
        public Vector3 startPos;
        public Vector3 goalPos;
        public int naivePointCount;
        public int improvedPointCount;
        public float naiveCost;
        public float improvedCost;
        public bool improvedWasBetter;
        public float improvementPercentage;
    }

    private List<TestResult> testResults = new List<TestResult>();
    private int testsCompleted = 0;
    private int improvementsFound = 0;
    private float totalImprovementPercentage = 0f;

    void Start()
    {
        if (runOnStart)
            StartCoroutine(RunAutomatedTests());
    }

    private IEnumerator RunAutomatedTests()
    {
        yield return new WaitForSeconds(1f);
        testResults.Clear();
        testsCompleted = 0;
        improvementsFound = 0;
        totalImprovementPercentage = 0f;

        Debug.Log($"=== Starting Automated Tests ({testCount} iterations) ===");

        for (int i = 0; i < testCount; i++)
        {
            yield return StartCoroutine(RunSingleTest(i));

            if ((i + 1) % 10 == 0)
            {
                Debug.Log($"Completed {i + 1}/{testCount} tests...");
            }
        }

        GenerateFinalReport();
    }

    private IEnumerator RunSingleTest(int testIndex)
    {
        Vector3 startPos = agentManager.GetRandomValidPosition(agentRadius);
        Vector3 goalPos = agentManager.GetRandomValidPosition(agentRadius);

        int safety = 0;
        while (Vector3.Distance(startPos, goalPos) < 5f && safety < 50)
        {
            goalPos = agentManager.GetRandomValidPosition(agentRadius);
            safety++;
        }

        GameObject tempStart = new GameObject("TempStart");
        GameObject tempGoal = new GameObject("TempGoal");
        tempStart.transform.position = startPos;
        tempGoal.transform.position = goalPos;

        rvgGenerator.AddDynamicPoints(tempStart.transform, tempGoal.transform);

        List<Vector3> naivePath = pathfinder.FindPath(startPos, goalPos);
        float naiveCost = agentManager.ComputeTotalPathCost(naivePath);

        List<Vector3> improvedPath = improvedPathfinder.FindPathWithRDP(
            startPos, goalPos, PathfindingMode.ImprovedRDP);
        float improvedCost = agentManager.ComputeTotalPathCost(improvedPath);

        TestResult result = new TestResult
        {
            startPos = startPos,
            goalPos = goalPos,
            naivePointCount = naivePath.Count,
            improvedPointCount = improvedPath.Count,
            naiveCost = naiveCost,
            improvedCost = improvedCost,
            improvedWasBetter = improvedCost < naiveCost,
            improvementPercentage = naiveCost > 0 ? ((naiveCost - improvedCost) / naiveCost) * 100f : 0f
        };

        testResults.Add(result);

        if (result.improvedWasBetter)
        {
            improvementsFound++;
            totalImprovementPercentage += result.improvementPercentage;

            Debug.Log($"[Test {testIndex + 1}] IMPROVEMENT FOUND: " +
                     $"Naive: {naiveCost:F2} ({naivePath.Count} points) → " +
                     $"Improved: {improvedCost:F2} ({improvedPath.Count} points) " +
                     $"(+{result.improvementPercentage:F1}%)");
        }
        else if (Mathf.Approximately(naiveCost, improvedCost))
        {
            Debug.Log($"[Test {testIndex + 1}] SAME: " +
                     $"Both: {naiveCost:F2} (Naive: {naivePath.Count} points, Improved: {improvedPath.Count} points)");
        }
        else
        {
            Debug.Log($"[Test {testIndex + 1}] WORSE: " +
                     $"Naive: {naiveCost:F2} ({naivePath.Count} points) → " +
                     $"Improved: {improvedCost:F2} ({improvedPath.Count} points) " +
                     $"(-{Mathf.Abs(result.improvementPercentage):F1}%)");
        }

        DestroyImmediate(tempStart);
        DestroyImmediate(tempGoal);
        rvgGenerator.RemoveLastDynamicPoints();

        testsCompleted++;

        yield return null;
    }

    private void GenerateFinalReport()
    {
        Debug.Log($"\n=== TEST RESULTS SUMMARY ===");
        Debug.Log($"Total Tests: {testsCompleted}");
        Debug.Log($"Improvements Found: {improvementsFound}");
        Debug.Log($"Success Rate: {(float)improvementsFound / testsCompleted * 100f:F1}%");

        if (improvementsFound > 0)
        {
            float avgImprovement = totalImprovementPercentage / improvementsFound;
            Debug.Log($"Average Improvement: +{avgImprovement:F1}%");
        }

        int totalPointsSaved = 0;
        int pathShorteningCases = 0;

        foreach (var result in testResults)
        {
            int pointsSaved = result.naivePointCount - result.improvedPointCount;
            totalPointsSaved += pointsSaved;

            if (pointsSaved > 0)
                pathShorteningCases++;
        }

        Debug.Log($"Path Shortening Cases: {pathShorteningCases}/{testsCompleted} ({(float)pathShorteningCases / testsCompleted * 100f:F2}%)");
        Debug.Log($"Average Points Saved: {(float)totalPointsSaved / testsCompleted:F2}");

        TestResult bestImprovement = null;
        foreach (var result in testResults)
        {
            if (result.improvedWasBetter &&
                (bestImprovement == null || result.improvementPercentage > bestImprovement.improvementPercentage))
            {
                bestImprovement = result;
            }
        }

        if (bestImprovement != null)
        {
            Debug.Log($"\nBEST IMPROVEMENT CASE:");
            Debug.Log($"Start: {bestImprovement.startPos}, Goal: {bestImprovement.goalPos}");
            Debug.Log($"Naive: {bestImprovement.naiveCost:F2} ({bestImprovement.naivePointCount} points)");
            Debug.Log($"Improved: {bestImprovement.improvedCost:F2} ({bestImprovement.improvedPointCount} points)");
            Debug.Log($"Improvement: +{bestImprovement.improvementPercentage:F1}%");
        }

        Debug.Log($"=== END OF REPORT ===\n");
    }
}
