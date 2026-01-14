using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class OgreWorldState
{
    public bool treasureStolen;
    public bool treasureInvestigated;   // whether we have already checked theft spot

    public bool seesPlayer;
    public bool playerInAttackRange;
    public bool playerInvisible;

    public float distanceToPlayer;
    public float distanceToNearestBoulder;
    public bool boulderCloserThanPlayer;
    public bool hasBoulderInHand;
    public bool hungry;
    public bool mushroomAvailable;
    public bool isAlert;

    public Vector3 lastSeenPlayerPos;
    public Vector3 lastTreasureSpot;    // last place treasure got stolen since there are 2 treasures
    public float timeSinceLastSeen;
    public Vector3 homePosition;

    public OgreWorldState Clone()
    {
        return new OgreWorldState
        {
            treasureStolen = this.treasureStolen,
            treasureInvestigated = this.treasureInvestigated,
            seesPlayer = this.seesPlayer,
            playerInAttackRange = this.playerInAttackRange,
            playerInvisible = this.playerInvisible,
            distanceToPlayer = this.distanceToPlayer,
            distanceToNearestBoulder = this.distanceToNearestBoulder,
            boulderCloserThanPlayer = this.boulderCloserThanPlayer,
            hasBoulderInHand = this.hasBoulderInHand,
            hungry = this.hungry,
            mushroomAvailable = this.mushroomAvailable,
            isAlert = this.isAlert,
            lastSeenPlayerPos = this.lastSeenPlayerPos,
            lastTreasureSpot = this.lastTreasureSpot,
            timeSinceLastSeen = this.timeSinceLastSeen,
            homePosition = this.homePosition
        };
    }
}

public abstract class Task
{
    public string Name { get; protected set; }
    protected Task(string name) { Name = name; }
}

public class CompoundTask : Task
{
    public CompoundTask(string name) : base(name) { }
}

public class PrimitiveTask : Task
{
    public System.Func<OgreWorldState, bool> Preconditions { get; set; }
    public System.Action<OgreWorldState> Effects { get; set; }
    public System.Action<OgreController> Action { get; set; }
    public PrimitiveTask(string name) : base(name) { }
}

public class Method
{
    public string TaskName { get; set; }
    public string MethodName { get; set; }
    public System.Func<OgreWorldState, bool> Preconditions { get; set; }
    public List<Task> Subtasks { get; set; }

    public Method(string taskName, string methodName)
    {
        TaskName = taskName;
        MethodName = methodName;
        Subtasks = new List<Task>();
    }
}

public class HTNPlanner
{
    private List<Method> methods = new List<Method>();
    private Dictionary<string, PrimitiveTask> primitives = new Dictionary<string, PrimitiveTask>();

    public void RegisterMethod(Method method) { methods.Add(method); }
    public void RegisterPrimitive(PrimitiveTask task) { primitives[task.Name] = task; }

    public bool Plan(OgreWorldState state, Task root, out List<PrimitiveTask> outPlan)
    {
        outPlan = new List<PrimitiveTask>();
        var taskStack = new Stack<Task>();
        taskStack.Push(root);
        OgreWorldState simState = state.Clone();

        while (taskStack.Count > 0)
        {
            Task t = taskStack.Pop();

            if (t is PrimitiveTask pt)
            {
                if (pt.Preconditions != null && !pt.Preconditions(simState))
                    return false;
                outPlan.Add(pt);
                if (pt.Effects != null)
                    pt.Effects(simState);
            }
            else if (t is CompoundTask ct)
            {
                Method chosenMethod = ChooseMethodForTask(ct.Name, simState);
                if (chosenMethod == null)
                    return false;
                for (int i = chosenMethod.Subtasks.Count - 1; i >= 0; i--)
                    taskStack.Push(chosenMethod.Subtasks[i]);
            }
        }
        return true;
    }

    private Method ChooseMethodForTask(string taskName, OgreWorldState state)
    {
        var candidates = methods.FindAll(m =>
            m.TaskName == taskName &&
            (m.Preconditions == null || m.Preconditions(state))
        );

        if (candidates.Count == 0)
            return null;

        // idle will pick random tasks - walk, turn, get boulder
        if (taskName == "Idle")
        {
            return candidates[Random.Range(0, candidates.Count)];
        }
        // otherwise pick the first rask
        return candidates[0];
    }
}

[RequireComponent(typeof(NavMeshAgent))]
public class OgreController : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform cave;

    [Header("Sensors")]
    public float viewRange = 50f;
    public float viewAngle = 120f;
    public float attackRange = 2f;

    [Header("Movement")]
    public float idleSpeed = 5f;
    public float attackSpeed = 10f;
    public float patrolRadius = 15f;

    [Header("Turning Behavior")]
    public float turnDuration = 3f;  // how long the looking around takes
    public int lookAroundTurns = 3;  // number of direction changes while looking

    [Header("Debug")]
    public bool showDebugInfo = true;

    // LookAround FSM state
    private int lookState = 0;
    private float lookTimer = 0f;
    private float targetLookAngle = 0f;

    private NavMeshAgent agent;
    private HTNPlanner planner;
    private OgreWorldState worldState;
    private List<PrimitiveTask> currentPlan;
    private int currentTaskIndex = 0;
    private PrimitiveTask currentTask = null;

    [SerializeField] private float treasureSearchRadius = 3f;

    private Vector3 treasureSearchTarget;
    private bool hasTreasureSearchTarget = false;

    // state for primitive actions
    private Vector3 moveTarget;
    private bool hasTarget = false;
    private bool actionComplete = false;

    // state for turning behavior
    private List<float> turnAngles;
    private float turnStartTime;

    private float hungerTimer = 0f;
    private float nextHungerTime = 0f;
    private Transform targetMushroom;
    private float eatTimer = 0f;
    private bool isEating = false;

    public LayerMask visionMask = ~0;
    public float eyeHeight = 1.2f;

    // boulder throw FSM
    enum BoulderState
    {
        RunToBoulder,
        PickUp,
        Holding,
        Throwing,
        Done
    }
    private BoulderState boulderState = BoulderState.Done;
    private float boulderHoldTimer = 0f;
    public float throwSpeed = 15f;
    public float throwUpwardBoost = 4f;
    public float handOffsetForward = 0.8f;
    public float handOffsetUp = 1.2f;
    [SerializeField] private float throwRange = 25f;  // only throw when player is within this distance

    bool IsInEatSequence()
    {
        if (currentTask == null) return false;
        string n = currentTask.Name;
        return n == "GoToMushroom" ||
               n == "ForageMushroom" ||
               n == "ReturnHome";
    }

    private PlayerController playerController;
    private int lastKnownTreasureCount = 0;
    // melee attack state
    private enum MeleeState { Chasing, GoingToLastSeen, Searching, Done }
    private MeleeState meleeState = MeleeState.Done;
    private float searchTimer = 0f;
    public float searchDuration = 3f;   // how long to spin looking for player
    // shared for both attack modes
    private Transform attackBoulder;
    [SerializeField] private float alertFOVRelaxTime = 2f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        ConfigureNavMeshAgent();

        InitializeHTN();
        worldState = new OgreWorldState
        {
            homePosition = cave.position
        };
        if (player != null)
            playerController = player.GetComponent<PlayerController>();

        if (playerController != null)
            lastKnownTreasureCount = playerController.TreasuresCollected;

        nextHungerTime = Random.Range(15f, 25f);

        Replan();
    }

    void ConfigureNavMeshAgent()
    {
        // movement settings
        agent.speed = idleSpeed;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 0.5f;

        agent.radius = 1f;
        agent.height = 2f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = 50;
    }

    void Update()
    {
        UpdateWorldState();

        if (ShouldReplan())
        {
            Replan();
        }

        ExecuteCurrentTask();
    }

    void InitializeHTN()
    {
        planner = new HTNPlanner();

        // Primitive Tasks
        var walkRandomly = new PrimitiveTask("WalkRandomly")
        {
            Preconditions = (state) => true,
            Effects = (state) => { },
            Action = (ogre) => ogre.ExecuteWalkRandomly()
        };

        var lookAround = new PrimitiveTask("LookAround")
        {
            Preconditions = (state) => true,
            Effects = (state) => { },
            Action = (ogre) => ogre.ExecuteLookAround()
        };
        var goToMushroom = new PrimitiveTask("GoToMushroom")
        {
            Preconditions = (state) => state.hungry && state.mushroomAvailable,
            Effects = (state) => { },   // state.hungry stays true until eaten
            Action = (ogre) => ogre.ExecuteGoToMushroom()
        };

        var forageMushroom = new PrimitiveTask("ForageMushroom")
        {
            Preconditions = (state) => state.hungry && state.mushroomAvailable,
            Effects = (state) =>
            {
                state.hungry = false;
            },
            Action = (ogre) => ogre.ExecuteForageMushroom()
        };

        var returnHome = new PrimitiveTask("ReturnHome")
        {
            Preconditions = (state) => true,
            Effects = (state) => { },
            Action = (ogre) => ogre.ExecuteReturnHome()
        };

        // melee attack
        var meleeAttack = new PrimitiveTask("MeleeAttack")
        {
            // prevent melee with boulder in hand
            Preconditions = (state) => state.seesPlayer && !state.hasBoulderInHand,
            Effects = (state) => { },
            Action = (ogre) => ogre.ExecuteMeleeAttack()
        };

        var goToTreasureSpot = new PrimitiveTask("GoToTreasureSpot")
        {
            // only go treasure spot once, later focus on attacking player
            Preconditions = (state) => state.treasureStolen && !state.treasureInvestigated,
            Effects = (state) => { state.treasureInvestigated = true; },
            Action = (ogre) => ogre.ExecuteGoToTreasureSpot()
        };

        // pick up a boulder only if not already holding one
        var pickUpBoulder = new PrimitiveTask("PickUpBoulder")
        {
            Preconditions = (state) => !state.hasBoulderInHand && state.distanceToNearestBoulder < Mathf.Infinity,
            Effects = (state) => { state.hasBoulderInHand = true; },
            Action = (ogre) => ogre.ExecutePickUpBoulder()
        };

        // throw held boulder at player only if holding one and sees player
        var throwBoulder = new PrimitiveTask("ThrowBoulder")
        {
            Preconditions = (state) => state.seesPlayer && state.hasBoulderInHand,
            Effects = (state) => { state.hasBoulderInHand = false; },
            Action = (ogre) => ogre.ExecuteThrowBoulder()
        };

        planner.RegisterPrimitive(goToTreasureSpot);
        planner.RegisterPrimitive(meleeAttack);
        planner.RegisterPrimitive(pickUpBoulder);
        planner.RegisterPrimitive(throwBoulder);
        planner.RegisterPrimitive(walkRandomly);
        planner.RegisterPrimitive(lookAround);
        planner.RegisterPrimitive(goToMushroom);
        planner.RegisterPrimitive(forageMushroom);
        planner.RegisterPrimitive(returnHome);

        // Compound Tasks
        var manageOgre = new CompoundTask("ManageOgre");
        var idle = new CompoundTask("Idle");
        var engagePlayer = new CompoundTask("EngagePlayer");

        var manageOgre_InvestigateTreasure = new Method("ManageOgre", "InvestigateTreasure")
        {
            Preconditions = (state) => state.treasureStolen && !state.treasureInvestigated,
            Subtasks = new List<Task> { goToTreasureSpot }
        };
        var manageOgre_HuntPlayer = new Method("ManageOgre", "HuntPlayer")
        {
            Preconditions = (state) => state.treasureStolen &&
                                       state.treasureInvestigated &&
                                       !state.playerInvisible,
            Subtasks = new List<Task> { engagePlayer }
        };
        var manageOgre_IdleAfterTreasure = new Method("ManageOgre", "IdleAfterTreasure")
        {
            Preconditions = (state) => state.treasureStolen &&
                                       state.treasureInvestigated &&
                                       state.playerInvisible,
            Subtasks = new List<Task> { idle }
        };
        var manageOgre_Eat = new Method("ManageOgre", "EatWhenHungry")
        {
            Preconditions = (state) => !state.treasureStolen &&
                                       state.hungry &&
                                       state.mushroomAvailable,
            Subtasks = new List<Task>
            {
                goToMushroom,
                forageMushroom,
                returnHome
            }
        };

        var manageOgre_Idle = new Method("ManageOgre", "Idle")
        {
            Preconditions = (state) => !state.treasureStolen &&
                                       !state.seesPlayer &&
                                       !state.hungry,
            Subtasks = new List<Task> { idle }
        };

        var manageOgre_Attack = new Method("ManageOgre", "AttackPlayer")
        {
            // attack player when visible
            Preconditions = (state) => state.seesPlayer && !state.playerInvisible,
            Subtasks = new List<Task> { engagePlayer }
        };

        var idle_Walk = new Method("Idle", "Walk")
        {
            Preconditions = (state) => true,
            Subtasks = new List<Task> { walkRandomly }
        };

        var idle_LookAround = new Method("Idle", "LookAround")
        {
            Preconditions = (state) => true,
            Subtasks = new List<Task> { lookAround }
        };

        // pick up boulder as idle behavior
        var idle_PickUpBoulder = new Method("Idle", "PickUpBoulder")
        {
            Preconditions = (state) => !state.hasBoulderInHand && state.distanceToNearestBoulder < Mathf.Infinity,
            Subtasks = new List<Task> { pickUpBoulder }
        };

        // throwing boulder
        var engage_Throw = new Method("EngagePlayer", "EngageThrow")
        {
            Preconditions = (state) => state.seesPlayer && state.hasBoulderInHand,
            Subtasks = new List<Task> { throwBoulder }
        };

        // melee (only if NOT holding a boulder)
        var engage_Melee = new Method("EngagePlayer", "EngageMelee")
        {
            Preconditions = (state) => state.seesPlayer && !state.hasBoulderInHand,
            Subtasks = new List<Task> { meleeAttack }
        };

        planner.RegisterMethod(manageOgre_Attack);
        planner.RegisterMethod(manageOgre_InvestigateTreasure);
        planner.RegisterMethod(manageOgre_HuntPlayer);
        planner.RegisterMethod(manageOgre_IdleAfterTreasure);

        planner.RegisterMethod(engage_Throw);
        planner.RegisterMethod(engage_Melee);

        planner.RegisterMethod(manageOgre_Eat);
        planner.RegisterMethod(manageOgre_Idle);

        planner.RegisterMethod(idle_Walk);
        planner.RegisterMethod(idle_LookAround);
        planner.RegisterMethod(idle_PickUpBoulder);
    }

    void UpdateWorldState()
    {
        if (player != null)
        {
            Vector3 toPlayer = player.position - transform.position;
            worldState.distanceToPlayer = toPlayer.magnitude;
            // track invisibility & treasure theft
            if (playerController != null)
            {
                worldState.playerInvisible = playerController.IsInvisible;

                int currentTreasure = playerController.TreasuresCollected;
                if (currentTreasure > lastKnownTreasureCount)
                {
                    worldState.treasureStolen = true;
                    worldState.lastTreasureSpot = player.position;   // approx theft spot
                    worldState.treasureInvestigated = false;
                    lastKnownTreasureCount = currentTreasure;
                }
                else if (currentTreasure > 0)
                {
                    worldState.treasureStolen = true;
                }
            }

            // visible only if in FOV and not invisible
            worldState.seesPlayer = IsPlayerVisible();
            worldState.playerInAttackRange = worldState.distanceToPlayer <= attackRange;

            if (worldState.seesPlayer)
            {
                worldState.lastSeenPlayerPos = player.position;
                worldState.timeSinceLastSeen = 0f;
            }
            else
            {
                worldState.timeSinceLastSeen += Time.deltaTime;
            }
        }

        // nearest boulder (for deciding melee vs ranged)
        Transform nearestBoulder = ObstacleSpawner.GetNearestBoulder(transform.position);

        // track if we're currently holding a boulder
        worldState.hasBoulderInHand = (attackBoulder != null);

        if (nearestBoulder != null)
        {
            worldState.distanceToNearestBoulder =
                Vector3.Distance(transform.position, nearestBoulder.position);
            worldState.boulderCloserThanPlayer =
                worldState.distanceToNearestBoulder < worldState.distanceToPlayer;
        }
        else
        {
            worldState.distanceToNearestBoulder = Mathf.Infinity;
            worldState.boulderCloserThanPlayer = false;
        }

        // mushrooms available?
        worldState.mushroomAvailable = ObstacleSpawner.Mushrooms.Exists(t => t != null);
        bool canAccumulateHunger = !worldState.hungry && !IsInEatSequence();

        if (canAccumulateHunger)
        {
            hungerTimer += Time.deltaTime;

            if (hungerTimer >= nextHungerTime)
            {
                if (worldState.mushroomAvailable)
                {
                    worldState.hungry = true;
                }
                else
                {
                    hungerTimer = 0f;
                    nextHungerTime = Random.Range(15f, 25f);
                }
            }
        }
        // if treasure stolen, melee player. ignore hunger & mushrooms & range
        if (worldState.treasureStolen && worldState.treasureInvestigated)
        {
            worldState.hungry = false;
            worldState.mushroomAvailable = false;
            worldState.boulderCloserThanPlayer = false;

            if (!worldState.playerInvisible && player != null)
            {
                Vector3 toP = player.position - transform.position;
                worldState.distanceToPlayer = toP.magnitude;
                worldState.playerInAttackRange = worldState.distanceToPlayer <= attackRange;

                worldState.seesPlayer = true;
                worldState.lastSeenPlayerPos = player.position;
                worldState.timeSinceLastSeen = 0f;
            }
            else
            {
                // lose the target and become idle when player toggle invisible
                worldState.seesPlayer = false;
            }
        }
    }


    bool IsPlayerVisible()
    {
        if (player == null) return false;
        if (playerController != null && playerController.IsInvisible) return false;

        Vector3 toPlayer = player.position - transform.position;
        float distance = toPlayer.magnitude;

        // distance check
        if (distance > viewRange) return false;

        if (distance <= attackRange + 0.5f)
            return true;

        // if we've seen the player recently, relax the FOV requirement
        bool recentlySeen = worldState != null && worldState.timeSinceLastSeen < alertFOVRelaxTime;

        if (!recentlySeen)
        {
            float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
            if (angle > viewAngle / 2f) return false;
        }

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        Vector3 dir = (player.position + Vector3.up * 0.5f - eyePos).normalized;

        // RaycastAll and ignore other ogres
        var hits = Physics.RaycastAll(
            eyePos,
            dir,
            viewRange,
            visionMask,
            QueryTriggerInteraction.Ignore);

        if (hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            // skip ourselves and other ogre
            if (h.transform == transform) continue;
            if (h.collider.CompareTag("Ogre")) continue;

            return (h.transform == player || h.transform.IsChildOf(player));
        }

        return false;
    }


    bool ShouldReplan()
    {
        // seeing the player interrupt everything except active attacks
        if (worldState.seesPlayer && !worldState.playerInvisible)
        {
            if (currentTask != null)
            {
                string n = currentTask.Name;
                if (n == "MeleeAttack" || n == "ThrowBoulder")
                    return false;
            }
            // interrupt any other task
            if (showDebugInfo && currentTask != null)
                Debug.Log($"[{gameObject.name}] INTERRUPTING {currentTask.Name} - player spotted!");

            // reset eat sequence state if we were eating
            if (IsInEatSequence())
            {
                isEating = false;
                if (targetMushroom != null)
                {
                    ObstacleSpawner.ReleaseMushroom(targetMushroom);
                    targetMushroom = null;
                }
            }

            return true;
        }

        // plan finished or empty
        if (currentPlan == null || currentPlan.Count == 0) return true;
        if (currentTaskIndex >= currentPlan.Count) return true;
        if (actionComplete)
        {
            currentTaskIndex++;
            if (currentTaskIndex >= currentPlan.Count)
                return true;
        }

        // treasure stolen etc.
        if (worldState.treasureStolen && !worldState.treasureInvestigated)
        {
            if (currentTask == null || currentTask.Name != "GoToTreasureSpot")
                return true;
        }

        // hunger: replan only if not already in eat sequence
        if (worldState.hungry && currentTask != null)
        {
            string n = currentTask.Name;
            bool inEatSeq =
                n == "GoToMushroom" ||
                n == "ForageMushroom" ||
                n == "ReturnHome";

            if (!inEatSeq)
                return true;
        }

        return false;
    }


    void Replan()
    {
        agent.isStopped = false;
        agent.updateRotation = true;
        agent.ResetPath();

        // reset attack state
        meleeState = MeleeState.Done;
        boulderState = BoulderState.Done;

        var root = new CompoundTask("ManageOgre");

        if (planner.Plan(worldState, root, out currentPlan))
        {
            currentTaskIndex = 0;
            currentTask = null;
            actionComplete = false;
            hasTarget = false;

            if (showDebugInfo)
            {
                Debug.Log($"[{gameObject.name}] Plan created with {currentPlan.Count} tasks");
                PrintPlan();
            }
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] Planning failed!");
            currentPlan = new List<PrimitiveTask>();
        }
    }

    void ExecuteCurrentTask()
    {
        if (currentPlan == null || currentTaskIndex >= currentPlan.Count) return;

        if (currentTask != currentPlan[currentTaskIndex])
        {
            currentTask = currentPlan[currentTaskIndex];
            actionComplete = false;
            hasTarget = false;
            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] Starting task: {currentTask.Name}");
        }

        if (currentTask.Action != null && !actionComplete)
        {
            currentTask.Action(this);
        }

        if (actionComplete)
        {
            currentTaskIndex++;
            currentTask = null;
        }
    }

    void ExecuteWalkRandomly()
    {
        if (!hasTarget)
        {
            moveTarget = GetRandomPointNearCave();
            agent.SetDestination(moveTarget);
            agent.speed = idleSpeed;
            hasTarget = true;

            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] Walking to {moveTarget}");
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                actionComplete = true;
                if (showDebugInfo)
                    Debug.Log($"[{gameObject.name}] Reached walk destination");
            }
        }
    }

    void ExecuteGoToTreasureSpot()
    {
        if (!hasTarget)
        {
            //choose a random point around the theft location
            if (!hasTreasureSearchTarget)
            {
                Vector2 offset2D = Random.insideUnitCircle * treasureSearchRadius;
                Vector3 rawTarget = worldState.lastTreasureSpot +
                                    new Vector3(offset2D.x, 0f, offset2D.y);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(rawTarget, out hit, 2f, NavMesh.AllAreas))
                    treasureSearchTarget = hit.position;
                else
                    treasureSearchTarget = worldState.lastTreasureSpot;

                hasTreasureSearchTarget = true;
            }

            agent.isStopped = false;
            agent.updateRotation = true;
            agent.speed = attackSpeed;
            agent.SetDestination(treasureSearchTarget);
            hasTarget = true;

            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] Going to stolen treasure area at {treasureSearchTarget}");
        }

        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance + 0.1f &&
            (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f))
        {
            hasTarget = false;
            hasTreasureSearchTarget = false;

            // mark finished investigating the spot
            worldState.treasureInvestigated = true;

            actionComplete = true;

            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] Reached stolen treasure area");
        }
    }


    void ExecuteLookAround()
    {
        if (!hasTarget)
        {
            agent.ResetPath();
            agent.isStopped = true;
            agent.updateRotation = false;

            lookState = 0;
            lookTimer = 0f;
            targetLookAngle = transform.eulerAngles.y;

            hasTarget = true;

            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] LookAround START");
        }

        lookTimer += Time.deltaTime;

        switch (lookState)
        {
            case 0:
                // immediately transition to wait phase
                lookState = 1;
                lookTimer = 0f;
                break;
            case 1:
                if (lookTimer >= 2f)
                {
                    // choose random angle
                    targetLookAngle = Random.Range(0f, 360f);
                    lookState = 2;
                    lookTimer = 0f;

                    if (showDebugInfo)
                        Debug.Log($"[{gameObject.name}] Turning to {targetLookAngle}");
                }
                break;

            case 2:
                {
                    Quaternion targetRot = Quaternion.Euler(0, targetLookAngle, 0);
                    float turnSpeed = 90f;

                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRot,
                        turnSpeed * Time.deltaTime
                    );

                    // when close enough, move to next phase
                    if (Quaternion.Angle(transform.rotation, targetRot) < 3f)
                    {
                        lookState = 3;
                        lookTimer = 0f;

                        if (showDebugInfo)
                            Debug.Log($"[{gameObject.name}] Finished turning");
                    }
                }
                break;
            case 3:
                if (lookTimer >= 2f)
                {
                    agent.isStopped = false;
                    agent.updateRotation = true;

                    actionComplete = true;
                    hasTarget = false;

                    if (showDebugInfo)
                        Debug.Log($"[{gameObject.name}] LookAround DONE");

                    lookState = 4;
                }
                break;
        }
    }


    Vector3 GetRandomPointNearCave()
    {
        for (int i = 0; i < 30; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * patrolRadius;
            Vector3 randomPoint = worldState.homePosition + new Vector3(randomOffset.x, 0, randomOffset.y);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, 2.0f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        Debug.LogWarning($"[{gameObject.name}] Couldn't find valid NavMesh point, staying in place");
        return transform.position;
    }

    void PrintPlan()
    {
        string planString = "=== PLAN ===\n";
        for (int i = 0; i < currentPlan.Count; i++)
        {
            planString += $"  {i}: {currentPlan[i].Name}\n";
        }
        Debug.Log(planString);
    }

    void ExecuteGoToMushroom()
    {
        if (!hasTarget)
        {
            targetMushroom = ObstacleSpawner.GetNearestFreeMushroom(transform.position);

            if (targetMushroom == null)
            {
                // no food after all – abort hunger
                worldState.hungry = false;
                actionComplete = true;
                if (showDebugInfo)
                    Debug.Log($"[{gameObject.name}] No mushrooms found.");
                return;
            }

            agent.isStopped = false;
            agent.updateRotation = true;
            agent.speed = idleSpeed;
            agent.SetDestination(targetMushroom.position);
            hasTarget = true;

            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] Going to mushroom at {targetMushroom.position}");
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f)
            {
                actionComplete = true;
                hasTarget = false;

                if (showDebugInfo)
                    Debug.Log($"[{gameObject.name}] Reached mushroom");
            }
        }
    }

    // wait 1 second, then eat mushroom
    void ExecuteForageMushroom()
    {
        if (!isEating)
        {
            agent.ResetPath();
            agent.isStopped = true;
            agent.updateRotation = true;
            eatTimer = 0f;
            isEating = true;

            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] Start foraging mushroom");
        }

        eatTimer += Time.deltaTime;

        if (eatTimer >= 1f)
        {
            if (targetMushroom != null)
            {
                ObstacleSpawner.RemoveMushroom(targetMushroom);
                Destroy(targetMushroom.gameObject);
                targetMushroom = null;
            }
            else
            {
                // If somehow lost the reference, release reservation
                ObstacleSpawner.ReleaseMushroom(targetMushroom);
            }

            // reset hunger cycle
            worldState.hungry = false;
            hungerTimer = 0f;
            nextHungerTime = Random.Range(15f, 25f);

            agent.isStopped = false;
            isEating = false;
            actionComplete = true;

            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] Finished eating mushroom");
        }
    }

    // go back to cave entrance
    void ExecuteReturnHome()
    {
        if (!hasTarget)
        {
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.speed = idleSpeed;
            moveTarget = worldState.homePosition;
            agent.SetDestination(moveTarget);
            hasTarget = true;

            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] Returning home to {moveTarget}");
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f)
            {
                actionComplete = true;
                hasTarget = false;
                if (showDebugInfo)
                    Debug.Log($"[{gameObject.name}] Reached home");
            }
        }
    }

    void ExecuteMeleeAttack()
    {
        if (!hasTarget)
        {
            // start chasing
            agent.isStopped = false;
            agent.updateRotation = false;
            agent.speed = attackSpeed;

            meleeState = MeleeState.Chasing;
            searchTimer = 0f;
            hasTarget = true;

            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] MeleeAttack START");
        }

        switch (meleeState)
        {
            // chase while we can see the player
            case MeleeState.Chasing:
                {
                    if (player != null)
                    {
                        agent.SetDestination(player.position);

                        // face player with faster rotation
                        Vector3 dir = (player.position - transform.position);
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.01f)
                        {
                            Quaternion lookRot = Quaternion.LookRotation(dir);
                            float rotationSpeed = 360f;
                            transform.rotation = Quaternion.RotateTowards(
                                transform.rotation, lookRot, rotationSpeed * Time.deltaTime);
                        }

                        //damage check
                        float dist = Vector3.Distance(transform.position, player.position);
                        if (dist <= attackRange)
                        {
                            if (playerController != null)
                            {
                                playerController.TakeDamage();
                                if (showDebugInfo)
                                    Debug.Log($"[{gameObject.name}] Melee hit player!");
                            }

                            meleeState = MeleeState.Done;
                            break;
                        }
                    }

                    // if we lose LOS and haven't hit yet, fallback to last seen pos
                    if (!worldState.seesPlayer)
                    {
                        agent.SetDestination(worldState.lastSeenPlayerPos);
                        meleeState = MeleeState.GoingToLastSeen;
                        if (showDebugInfo)
                            Debug.Log($"[{gameObject.name}] Lost player, going to last seen pos");
                    }
                    break;
                }

            // move to last seen position
            case MeleeState.GoingToLastSeen:
                {
                    if (!agent.pathPending &&
                        agent.remainingDistance <= agent.stoppingDistance + 0.1f &&
                        (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f))
                    {
                        // start searching (turning)
                        searchTimer = 0f;
                        meleeState = MeleeState.Searching;

                        agent.ResetPath();
                        agent.isStopped = true;
                        agent.updateRotation = false;

                        if (showDebugInfo)
                            Debug.Log($"[{gameObject.name}] Reached last seen pos, searching...");
                    }
                    break;
                }

            // search: 360 degree spin to look for the player again
            case MeleeState.Searching:
                {
                    searchTimer += Time.deltaTime;

                    float anglePerSecond = 360f / searchDuration;
                    transform.Rotate(0f, anglePerSecond * Time.deltaTime, 0f);

                    // if we see the player again, resume chase
                    if (worldState.seesPlayer)
                    {
                        agent.isStopped = false;
                        agent.updateRotation = true;
                        meleeState = MeleeState.Chasing;
                        if (showDebugInfo)
                            Debug.Log($"[{gameObject.name}] Reacquired player while searching!");
                    }
                    else if (searchTimer >= searchDuration)
                    {
                        // give up, go idle
                        meleeState = MeleeState.Done;
                    }
                    break;
                }

            case MeleeState.Done:
            default:
                {
                    agent.isStopped = false;
                    agent.updateRotation = true;
                    hasTarget = false;
                    actionComplete = true;
                    if (showDebugInfo)
                        Debug.Log($"[{gameObject.name}] MeleeAttack DONE");
                    break;
                }
        }
    }

    private System.Collections.IEnumerator EnableBoulderColliderLater(Collider col, float delay)
    {
        if (col == null)
            yield break;

        yield return new WaitForSeconds(delay);

        if (col != null)
            col.enabled = true;
    }


    void ExecutePickUpBoulder()
    {
        // if we already have a boulder, we're done
        if (attackBoulder != null)
        {
            actionComplete = true;
            hasTarget = false;
            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] Already holding a boulder");
            return;
        }

        // else, find nearest boulder
        if (!hasTarget)
        {
            attackBoulder = ObstacleSpawner.GetNearestBoulder(transform.position);

            if (attackBoulder == null)
            {
                actionComplete = true;
                if (showDebugInfo)
                    Debug.Log($"[{gameObject.name}] No boulder found to pick up");
                return;
            }

            // start running to the boulder
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.speed = idleSpeed;
            agent.SetDestination(attackBoulder.position);

            boulderState = BoulderState.RunToBoulder;
            hasTarget = true;

            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] Going to pick up boulder at {attackBoulder.position}");
        }

        switch (boulderState)
        {
            case BoulderState.RunToBoulder:
                {
                    if (!agent.pathPending &&
                        agent.remainingDistance <= agent.stoppingDistance + 0.1f &&
                        (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f))
                    {
                        boulderState = BoulderState.PickUp;
                        if (showDebugInfo)
                            Debug.Log($"[{gameObject.name}] Reached boulder, picking up");
                    }
                    break;
                }

            case BoulderState.PickUp:
                {
                    if (attackBoulder == null)
                    {
                        actionComplete = true;
                        hasTarget = false;
                        boulderState = BoulderState.Done;
                        break;
                    }

                    // disable NavMeshObstacle
                    var obstacle = attackBoulder.GetComponent<NavMeshObstacle>();
                    if (obstacle != null) obstacle.enabled = false;

                    // disable collider while holding
                    var col = attackBoulder.GetComponent<Collider>();
                    if (col != null) col.enabled = false;

                    // make kinematic
                    var rb = attackBoulder.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = true;
                        rb.useGravity = false;
                    }

                    // remove from boulder list
                    ObstacleSpawner.Boulders.Remove(attackBoulder);

                    // parent to ogre hand
                    attackBoulder.SetParent(transform);
                    attackBoulder.localPosition = new Vector3(0f, handOffsetUp, handOffsetForward);

                    boulderState = BoulderState.Done;

                    // mark action complete, return to idle while holding boulder
                    actionComplete = true;
                    hasTarget = false;
                    agent.isStopped = false;
                    agent.updateRotation = true;

                    if (showDebugInfo)
                        Debug.Log($"[{gameObject.name}] Picked up boulder, returning to idle");
                    break;
                }

            case BoulderState.Done:
            default:
                {
                    actionComplete = true;
                    hasTarget = false;
                    break;
                }
        }
    }

    void ExecuteThrowBoulder()
    {
        // if we don't have a boulder, abort
        if (attackBoulder == null)
        {
            actionComplete = true;
            hasTarget = false;
            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] No boulder to throw!");
            return;
        }

        if (!hasTarget)
        {
            agent.isStopped = false;
            agent.updateRotation = false;
            agent.speed = attackSpeed;
            boulderState = BoulderState.Holding;
            boulderHoldTimer = 0f;
            hasTarget = true;

            if (showDebugInfo)
                Debug.Log($"[{gameObject.name}] ThrowBoulder START - approaching player");
        }

        switch (boulderState)
        {
            case BoulderState.Holding:
                {
                    if (attackBoulder == null)
                    {
                        actionComplete = true;
                        hasTarget = false;
                        boulderState = BoulderState.Done;
                        break;
                    }

                    // keep facing the player
                    Vector3 targetPos = worldState.seesPlayer && player != null
                        ? player.position
                        : worldState.lastSeenPlayerPos;
                    Vector3 dir = targetPos - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                    {
                        Quaternion lookRot = Quaternion.LookRotation(dir);
                        float rotationSpeed = 360f;
                        transform.rotation = Quaternion.RotateTowards(
                            transform.rotation,
                            lookRot,
                            rotationSpeed * Time.deltaTime);
                    }

                    boulderHoldTimer += Time.deltaTime;

                    float distToPlayer = player != null
                        ? Vector3.Distance(transform.position, player.position)
                        : float.MaxValue;

                    // if player visible and in range, throw!!!
                    if (worldState.seesPlayer && distToPlayer <= throwRange)
                    {
                        if (showDebugInfo)
                            Debug.Log($"[{gameObject.name}] Player in range ({distToPlayer:F1}m), throwing!");
                        boulderState = BoulderState.Throwing;
                    }
                    // if player is too far, go closer
                    else if (worldState.seesPlayer && distToPlayer > throwRange)
                    {
                        agent.isStopped = false;
                        agent.SetDestination(player.position);
                    }
                    // if player not visible, stop and wait
                    else
                    {
                        agent.isStopped = true;
                    }
                    break;
                }

            case BoulderState.Throwing:
                {
                    if (attackBoulder == null)
                    {
                        actionComplete = true;
                        hasTarget = false;
                        boulderState = BoulderState.Done;
                        break;
                    }

                    // unparent from ogre
                    attackBoulder.SetParent(null);
                    var col = attackBoulder.GetComponent<Collider>();
                    if (col != null) col.enabled = false;

                    var rb = attackBoulder.GetComponent<Rigidbody>();
                    if (rb == null)
                        rb = attackBoulder.gameObject.AddComponent<Rigidbody>();

                    rb.isKinematic = false;
                    rb.useGravity = true;

                    // start position
                    Vector3 startPos = transform.position
                                       + transform.forward * handOffsetForward
                                       + Vector3.up * handOffsetUp;
                    attackBoulder.position = startPos;

                    // aim at player
                    Vector3 targetPos;
                    if (worldState.seesPlayer && player != null &&
                        (playerController == null || !playerController.IsInvisible))
                    {
                        targetPos = player.position;
                    }
                    else
                    {
                        targetPos = worldState.lastSeenPlayerPos;
                    }
                    targetPos += Vector3.up * 0.5f;

                    Vector3 dir = (targetPos - startPos).normalized;
                    Vector3 launchVel = dir * throwSpeed + Vector3.up * throwUpwardBoost;
                    rb.linearVelocity = launchVel;

                    // setup projectile
                    var proj = attackBoulder.GetComponent<BoulderProjectile>();
                    if (proj == null)
                        proj = attackBoulder.gameObject.AddComponent<BoulderProjectile>();
                    proj.Initialize(playerController);

                    // ignore collision with self
                    var myCol = GetComponent<Collider>();
                    if (myCol != null && col != null)
                        Physics.IgnoreCollision(col, myCol);

                    // re enable collider after delay
                    if (col != null)
                        StartCoroutine(EnableBoulderColliderLater(col, 0.1f));

                    // clear state
                    attackBoulder = null;
                    hasTarget = false;
                    actionComplete = true;
                    boulderState = BoulderState.Done;
                    agent.isStopped = false;
                    agent.updateRotation = true;

                    if (showDebugInfo)
                        Debug.Log($"[{gameObject.name}] Threw boulder!");
                    break;
                }

            case BoulderState.Done:
            default:
                {
                    hasTarget = false;
                    actionComplete = true;
                    break;
                }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw FOV
        Gizmos.color = Color.yellow;
        Vector3 leftBoundary = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward * viewRange;
        Vector3 rightBoundary = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward * viewRange;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);

        // Draw patrol radius around cave
        if (cave != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(cave.position, patrolRadius);
        }

        // Draw current move target
        if (hasTarget && !actionComplete)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(moveTarget, 0.5f);
            Gizmos.DrawLine(transform.position, moveTarget);
        }

        // Draw avoidance radius
        if (agent != null)
        {
            Gizmos.color = new Color(1, 0, 0, 0.2f);
            Gizmos.DrawWireSphere(transform.position, agent.radius);
        }
    }


    public List<PrimitiveTask> GetCurrentPlan()
    {
        return currentPlan;
    }

    public OgreWorldState GetWorldState()
    {
        return worldState;
    }

    public int CurrentTaskIndex => currentTaskIndex;

    public string CurrentTaskName => currentTask != null ? currentTask.Name : "None";

    public bool HasBoulder => attackBoulder != null;
    public string CurrentExecutionStep
    {
        get
        {
            if (currentTask == null) return "Idle";

            string taskName = currentTask.Name;

            // MeleeAttack states
            if (taskName == "MeleeAttack")
            {
                switch (meleeState)
                {
                    case MeleeState.Chasing: return "Chasing Player";
                    case MeleeState.GoingToLastSeen: return "Going to Last Seen Position";
                    case MeleeState.Searching: return "Searching (Spinning)";
                    case MeleeState.Done: return "Attack Complete";
                    default: return "Attacking";
                }
            }

            // ThrowBoulder states
            if (taskName == "ThrowBoulder")
            {
                switch (boulderState)
                {
                    case BoulderState.Holding: return "Holding Boulder (Approaching)";
                    case BoulderState.Throwing: return "Throwing Boulder";
                    case BoulderState.Done: return "Throw Complete";
                    default: return "Preparing Throw";
                }
            }

            // PickUpBoulder states
            if (taskName == "PickUpBoulder")
            {
                switch (boulderState)
                {
                    case BoulderState.RunToBoulder: return "Running to Boulder";
                    case BoulderState.PickUp: return "Picking Up Boulder";
                    case BoulderState.Done: return "Pickup Complete";
                    default: return "Getting Boulder";
                }
            }

            // Eat sequence
            if (taskName == "GoToMushroom")
            {
                return hasTarget ? "Walking to Mushroom" : "Finding Mushroom";
            }
            if (taskName == "ForageMushroom")
            {
                return isEating ? "Eating Mushroom" : "Starting to Eat";
            }
            if (taskName == "ReturnHome")
            {
                return hasTarget ? "Walking Home" : "Finding Home";
            }

            // Walk randomly
            if (taskName == "WalkRandomly")
            {
                if (actionComplete) return "Reached Destination";
                return hasTarget ? "Walking to Point" : "Choosing Destination";
            }

            // Look around
            if (taskName == "LookAround")
            {
                switch (lookState)
                {
                    case 0: return "Turning Left";
                    case 1: return "Turning Right";
                    case 2: return "Looking Forward";
                    default: return "Looking Around";
                }
            }

            // InvestigateTreasure
            if (taskName == "InvestigateTreasure")
            {
                if (actionComplete) return "Investigating Area";
                return hasTarget ? "Going to Treasure Spot" : "Finding Treasure Location";
            }

            // HuntPlayer
            if (taskName == "HuntPlayer")
            {
                return hasTarget ? "Hunting Player" : "Searching for Player";
            }

            // Default - just indicate it's running
            return "Executing";
        }
    }

    public struct TaskSubStep
    {
        public string Name;
        public int Status; // 0 = pending, 1 = current, 2 = done

        public TaskSubStep(string name, int status)
        {
            Name = name;
            Status = status;
        }
    }

    public List<TaskSubStep> GetCurrentTaskSteps()
    {
        var steps = new List<TaskSubStep>();
        if (currentTask == null) return steps;

        string taskName = currentTask.Name;

        // MeleeAttack has 4 states
        if (taskName == "MeleeAttack")
        {
            int currentIdx = 0;
            switch (meleeState)
            {
                case MeleeState.Chasing: currentIdx = 0; break;
                case MeleeState.GoingToLastSeen: currentIdx = 1; break;
                case MeleeState.Searching: currentIdx = 2; break;
                case MeleeState.Done: currentIdx = 3; break;
            }

            steps.Add(new TaskSubStep("Chasing Player", currentIdx > 0 ? 2 : (currentIdx == 0 ? 1 : 0)));
            steps.Add(new TaskSubStep("Going to Last Seen", currentIdx > 1 ? 2 : (currentIdx == 1 ? 1 : 0)));
            steps.Add(new TaskSubStep("Searching Area", currentIdx > 2 ? 2 : (currentIdx == 2 ? 1 : 0)));
            steps.Add(new TaskSubStep("Complete", currentIdx == 3 ? 1 : 0));
            return steps;
        }

        // ThrowBoulder
        if (taskName == "ThrowBoulder")
        {
            int currentIdx = 0;
            switch (boulderState)
            {
                case BoulderState.Holding: currentIdx = 0; break;
                case BoulderState.Throwing: currentIdx = 1; break;
                case BoulderState.Done: currentIdx = 2; break;
                default: currentIdx = 0; break;
            }

            steps.Add(new TaskSubStep("Approaching Player", currentIdx > 0 ? 2 : (currentIdx == 0 ? 1 : 0)));
            steps.Add(new TaskSubStep("Throwing Boulder", currentIdx > 1 ? 2 : (currentIdx == 1 ? 1 : 0)));
            steps.Add(new TaskSubStep("Complete", currentIdx == 2 ? 1 : 0));
            return steps;
        }

        // PickUpBoulder
        if (taskName == "PickUpBoulder")
        {
            int currentIdx = 0;
            switch (boulderState)
            {
                case BoulderState.RunToBoulder: currentIdx = 0; break;
                case BoulderState.PickUp: currentIdx = 1; break;
                case BoulderState.Done: currentIdx = 2; break;
                default: currentIdx = 0; break;
            }

            steps.Add(new TaskSubStep("Running to Boulder", currentIdx > 0 ? 2 : (currentIdx == 0 ? 1 : 0)));
            steps.Add(new TaskSubStep("Picking Up", currentIdx > 1 ? 2 : (currentIdx == 1 ? 1 : 0)));
            steps.Add(new TaskSubStep("Complete", currentIdx == 2 ? 1 : 0));
            return steps;
        }

        // GoToMushroom
        if (taskName == "GoToMushroom")
        {
            bool finding = !hasTarget;
            bool walking = hasTarget && !actionComplete;
            bool done = actionComplete;

            steps.Add(new TaskSubStep("Finding Mushroom", done || walking ? 2 : (finding ? 1 : 0)));
            steps.Add(new TaskSubStep("Walking to Mushroom", done ? 2 : (walking ? 1 : 0)));
            steps.Add(new TaskSubStep("Arrived", done ? 1 : 0));
            return steps;
        }

        // ForageMushroom
        if (taskName == "ForageMushroom")
        {
            bool starting = !isEating && !actionComplete;
            bool eating = isEating && !actionComplete;
            bool done = actionComplete;

            steps.Add(new TaskSubStep("Starting to Eat", eating || done ? 2 : (starting ? 1 : 0)));
            steps.Add(new TaskSubStep("Eating Mushroom", done ? 2 : (eating ? 1 : 0)));
            steps.Add(new TaskSubStep("Finished Eating", done ? 1 : 0));
            return steps;
        }

        // ReturnHome
        if (taskName == "ReturnHome")
        {
            bool finding = !hasTarget;
            bool walking = hasTarget && !actionComplete;
            bool done = actionComplete;

            steps.Add(new TaskSubStep("Finding Home", done || walking ? 2 : (finding ? 1 : 0)));
            steps.Add(new TaskSubStep("Walking Home", done ? 2 : (walking ? 1 : 0)));
            steps.Add(new TaskSubStep("Arrived Home", done ? 1 : 0));
            return steps;
        }

        // WalkRandomly
        if (taskName == "WalkRandomly")
        {
            bool choosing = !hasTarget;
            bool walking = hasTarget && !actionComplete;
            bool done = actionComplete;

            steps.Add(new TaskSubStep("Choosing Destination", done || walking ? 2 : (choosing ? 1 : 0)));
            steps.Add(new TaskSubStep("Walking", done ? 2 : (walking ? 1 : 0)));
            steps.Add(new TaskSubStep("Reached Destination", done ? 1 : 0));
            return steps;
        }

        // LookAround
        if (taskName == "LookAround")
        {
            int currentIdx = lookState;
            if (actionComplete) currentIdx = 3;

            steps.Add(new TaskSubStep("Turn Left", currentIdx > 0 ? 2 : (currentIdx == 0 ? 1 : 0)));
            steps.Add(new TaskSubStep("Turn Right", currentIdx > 1 ? 2 : (currentIdx == 1 ? 1 : 0)));
            steps.Add(new TaskSubStep("Look Forward", currentIdx > 2 ? 2 : (currentIdx == 2 ? 1 : 0)));
            steps.Add(new TaskSubStep("Complete", currentIdx == 3 ? 1 : 0));
            return steps;
        }

        // InvestigateTreasure
        if (taskName == "InvestigateTreasure")
        {
            bool finding = !hasTarget && !actionComplete;
            bool going = hasTarget && !actionComplete;
            bool investigating = actionComplete;

            steps.Add(new TaskSubStep("Locating Theft Spot", going || investigating ? 2 : (finding ? 1 : 0)));
            steps.Add(new TaskSubStep("Going to Location", investigating ? 2 : (going ? 1 : 0)));
            steps.Add(new TaskSubStep("Investigating", investigating ? 1 : 0));
            return steps;
        }

        // HuntPlayer
        if (taskName == "HuntPlayer")
        {
            bool searching = !hasTarget;
            bool hunting = hasTarget;

            steps.Add(new TaskSubStep("Searching for Player", hunting ? 2 : (searching ? 1 : 0)));
            steps.Add(new TaskSubStep("Hunting Player", hunting ? 1 : 0));
            return steps;
        }

        // Default - single step
        steps.Add(new TaskSubStep("Executing", 1));
        return steps;
    }
}