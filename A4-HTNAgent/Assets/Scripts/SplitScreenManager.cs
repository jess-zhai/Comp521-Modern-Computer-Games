using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class SplitScreenManager : MonoBehaviour
{
    [Header("Required References")]
    public Camera gameCamera;
    public Canvas gameUICanvas;
    public OgreController ogre1;
    public OgreController ogre2;

    [Header("Layout Settings")]
    [Range(0.5f, 0.8f)]
    public float gameViewWidth = 0.65f;

    [Header("HTN Display Colors")]
    public Color currentTaskColor = new Color(0.2f, 0.8f, 0.2f);
    public Color pendingTaskColor = new Color(0.8f, 0.8f, 0.8f);
    public Color completedTaskColor = new Color(0.5f, 0.5f, 0.5f);
    public Color headerColor = new Color(1f, 0.8f, 0.2f);

    [Header("Debug")]
    public bool showDebugLogs = true;

    [Header("History Settings")]
    [Range(3, 10)]
    public int maxHistoryItems = 5;

    // Internal
    private Canvas htnCanvas;
    private RectTransform htnPanel;
    private TMP_Text ogre1Title;
    private TMP_Text ogre1StateText;
    private RectTransform ogre1TaskContainer;
    private List<GameObject> ogre1TaskEntries = new List<GameObject>();
    private TMP_Text ogre2Title;
    private TMP_Text ogre2StateText;
    private RectTransform ogre2TaskContainer;
    private List<GameObject> ogre2TaskEntries = new List<GameObject>();

    // Task history tracking
    private List<string> ogre1History = new List<string>();
    private List<string> ogre2History = new List<string>();
    private int ogre1LastCompletedIndex = -1;
    private int ogre2LastCompletedIndex = -1;
    private List<PrimitiveTask> ogre1LastPlan = null;
    private List<PrimitiveTask> ogre2LastPlan = null;

    private float updateInterval = 0.1f;
    private float lastUpdateTime = 0f;
    private int ogre1LastPlanHash = 0;
    private int ogre1LastTaskIndex = -1;
    private int ogre2LastPlanHash = 0;
    private int ogre2LastTaskIndex = -1;
    private int ogre1LastSubStepHash = 0;
    private int ogre2LastSubStepHash = 0;

    void Start()
    {
        // Auto-find references
        if (gameCamera == null)
            gameCamera = Camera.main;

        if (ogre1 == null || ogre2 == null)
        {
            var ogres = FindObjectsByType<OgreController>(FindObjectsSortMode.None);
            if (ogres.Length >= 1 && ogre1 == null) ogre1 = ogres[0];
            if (ogres.Length >= 2 && ogre2 == null) ogre2 = ogres[1];
        }

        SetupSplitScreen();
        CreateHTNDisplay();

        // Force initial update
        ForceUpdateDisplay();
    }

    void SetupSplitScreen()
    {
        // 1. Adjust camera viewport
        if (gameCamera != null)
        {
            gameCamera.rect = new Rect(0, 0, gameViewWidth, 1);
            if (showDebugLogs) Debug.Log($"[SplitScreenManager] Camera viewport set to {gameViewWidth * 100}%");
        }

        // 2. Constrain game UI to left portion
        if (gameUICanvas != null)
        {
            // Create container
            GameObject container = new GameObject("GameUIContainer");
            container.transform.SetParent(gameUICanvas.transform, false);

            var containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0);
            containerRect.anchorMax = new Vector2(gameViewWidth, 1);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            // Collect children (skip our container)
            List<Transform> children = new List<Transform>();
            for (int i = gameUICanvas.transform.childCount - 1; i >= 0; i--)
            {
                var child = gameUICanvas.transform.GetChild(i);
                if (child.gameObject != container)
                {
                    children.Add(child);
                }
            }

            // Reparent children - use false to keep local position/anchors
            foreach (var child in children)
            {
                // Store original anchor settings
                var childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                {
                    var anchorMin = childRect.anchorMin;
                    var anchorMax = childRect.anchorMax;
                    var anchoredPos = childRect.anchoredPosition;
                    var sizeDelta = childRect.sizeDelta;
                    var pivot = childRect.pivot;

                    child.SetParent(container.transform, false);

                    // Restore anchors (they're now relative to the container)
                    childRect.anchorMin = anchorMin;
                    childRect.anchorMax = anchorMax;
                    childRect.anchoredPosition = anchoredPos;
                    childRect.sizeDelta = sizeDelta;
                    childRect.pivot = pivot;
                }
                else
                {
                    child.SetParent(container.transform, false);
                }
            }

            if (showDebugLogs) Debug.Log($"[SplitScreenManager] Moved {children.Count} UI elements to container");
        }
    }

    void CreateHTNDisplay()
    {
        // Create HTN canvas
        GameObject canvasObj = new GameObject("HTNCanvas");
        htnCanvas = canvasObj.AddComponent<Canvas>();
        htnCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        htnCanvas.sortingOrder = 50;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Create panel on RIGHT side
        GameObject panelObj = new GameObject("HTNPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        htnPanel = panelObj.AddComponent<RectTransform>();

        // Anchor to right portion
        htnPanel.anchorMin = new Vector2(gameViewWidth, 0);
        htnPanel.anchorMax = new Vector2(1, 1);
        htnPanel.offsetMin = Vector2.zero;
        htnPanel.offsetMax = Vector2.zero;

        // Background
        var bg = panelObj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 1f);

        // Layout
        var layout = panelObj.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        // Title
        CreateLabel(htnPanel, "HTN PLANNER", 26, headerColor, TextAlignmentOptions.Center, FontStyles.Bold);
        CreateSeparator(htnPanel, 2);

        // Ogre panels
        CreateOgrePanel(htnPanel, "OGRE 1", out ogre1Title, out ogre1StateText, out ogre1TaskContainer);
        CreateSeparator(htnPanel, 1);
        CreateOgrePanel(htnPanel, "OGRE 2", out ogre2Title, out ogre2StateText, out ogre2TaskContainer);

        // Spacer
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(htnPanel, false);
        spacer.AddComponent<RectTransform>();
        spacer.AddComponent<LayoutElement>().flexibleHeight = 1;

        // Legend
        CreateLegend(htnPanel);

        if (showDebugLogs) Debug.Log("[SplitScreenManager] HTN display created");
    }

    void CreateOgrePanel(RectTransform parent, string title,
                         out TMP_Text titleText, out TMP_Text stateText, out RectTransform taskContainer)
    {
        GameObject panelObj = new GameObject(title.Replace(" ", "") + "Panel");
        panelObj.transform.SetParent(parent, false);
        var rect = panelObj.AddComponent<RectTransform>();

        var bg = panelObj.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.16f, 1f);

        var layout = panelObj.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 4;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        var layoutElem = panelObj.AddComponent<LayoutElement>();
        layoutElem.minHeight = 180;
        layoutElem.flexibleHeight = 1;

        // Title
        titleText = CreateLabel(rect, title, 22, headerColor, TextAlignmentOptions.Center, FontStyles.Bold);

        // State
        CreateLabel(rect, "State:", 14, new Color(0.6f, 0.6f, 0.6f), TextAlignmentOptions.Left, FontStyles.Normal);
        stateText = CreateLabel(rect, "Loading...", 16, Color.white, TextAlignmentOptions.Left, FontStyles.Normal);

        // Plan label
        CreateLabel(rect, "Task Sequence:", 14, new Color(0.6f, 0.6f, 0.6f), TextAlignmentOptions.Left, FontStyles.Normal);

        // Scroll View for tasks
        GameObject scrollObj = new GameObject("TaskScrollView");
        scrollObj.transform.SetParent(rect, false);
        var scrollRect = scrollObj.AddComponent<RectTransform>();

        var scrollView = scrollObj.AddComponent<ScrollRect>();
        scrollView.horizontal = false;
        scrollView.vertical = true;
        scrollView.movementType = ScrollRect.MovementType.Clamped;
        scrollView.scrollSensitivity = 20f;

        var scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = new Color(0.06f, 0.06f, 0.09f, 1f);

        // Mask for clipping
        var mask = scrollObj.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        var scrollLayoutElem = scrollObj.AddComponent<LayoutElement>();
        scrollLayoutElem.minHeight = 80;
        scrollLayoutElem.flexibleHeight = 1;

        // Content container inside scroll view
        GameObject contentObj = new GameObject("TaskContainer");
        contentObj.transform.SetParent(scrollObj.transform, false);
        taskContainer = contentObj.AddComponent<RectTransform>();

        // Anchor to top, stretch width
        taskContainer.anchorMin = new Vector2(0, 1);
        taskContainer.anchorMax = new Vector2(1, 1);
        taskContainer.pivot = new Vector2(0.5f, 1);
        taskContainer.offsetMin = Vector2.zero;
        taskContainer.offsetMax = Vector2.zero;

        var containerLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        containerLayout.padding = new RectOffset(6, 6, 4, 4);
        containerLayout.spacing = 2;
        containerLayout.childForceExpandWidth = true;
        containerLayout.childForceExpandHeight = false;
        containerLayout.childControlHeight = true;
        containerLayout.childControlWidth = true;
        containerLayout.childAlignment = TextAnchor.UpperLeft;

        // Content size fitter to auto-size based on children
        var fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Link scroll view to content
        scrollView.content = taskContainer;
        scrollView.viewport = scrollRect;
    }

    TMP_Text CreateLabel(RectTransform parent, string text, float fontSize, Color color,
                         TextAlignmentOptions alignment, FontStyles style)
    {
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent, false);

        var tmpText = labelObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.color = color;
        tmpText.alignment = alignment;
        tmpText.fontStyle = style;
        tmpText.richText = true;

        var layout = labelObj.AddComponent<LayoutElement>();
        layout.minHeight = fontSize + 6;
        layout.preferredHeight = fontSize + 6;

        return tmpText;
    }

    void CreateSeparator(RectTransform parent, float height)
    {
        GameObject sepObj = new GameObject("Separator");
        sepObj.transform.SetParent(parent, false);
        sepObj.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.35f, 1f);
        var layout = sepObj.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    void CreateLegend(RectTransform parent)
    {
        GameObject legendObj = new GameObject("Legend");
        legendObj.transform.SetParent(parent, false);
        legendObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 1f);

        var layout = legendObj.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.spacing = 10;
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.MiddleCenter;
        legendObj.AddComponent<LayoutElement>().minHeight = 32;

        CreateLegendItem(legendObj.transform, "► Current", currentTaskColor);
        CreateLegendItem(legendObj.transform, "○ Pending", pendingTaskColor);
        CreateLegendItem(legendObj.transform, "✓ Done", completedTaskColor);
    }

    void CreateLegendItem(Transform parent, string text, Color color)
    {
        GameObject itemObj = new GameObject("LegendItem");
        itemObj.transform.SetParent(parent, false);
        var tmpText = itemObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = 14;
        tmpText.color = color;
        itemObj.AddComponent<LayoutElement>().minWidth = 80;
    }

    void Update()
    {
        if (Time.time - lastUpdateTime < updateInterval) return;
        lastUpdateTime = Time.time;

        bool needsFullUpdate = false;

        // Track Ogre 1
        if (ogre1 != null)
        {
            var plan = ogre1.GetCurrentPlan();
            int idx = ogre1.CurrentTaskIndex;
            int hash = GetPlanHash(ogre1);

            // Check if plan changed (replan occurred)
            if (hash != ogre1LastPlanHash)
            {
                // Plan changed - save completed tasks from old plan to history
                if (ogre1LastPlan != null && ogre1LastCompletedIndex >= 0)
                {
                    for (int i = 0; i <= ogre1LastCompletedIndex && i < ogre1LastPlan.Count; i++)
                    {
                        AddToHistory(ogre1History, ogre1LastPlan[i].Name);
                    }
                }
                ogre1LastPlan = plan != null ? new List<PrimitiveTask>(plan) : null;
                ogre1LastCompletedIndex = -1;
                ogre1LastPlanHash = hash;
                needsFullUpdate = true;
            }

            // Check if task index advanced (task completed)
            if (idx != ogre1LastTaskIndex)
            {
                // Tasks between lastIndex and current idx-1 are newly completed
                if (idx > ogre1LastTaskIndex && ogre1LastTaskIndex >= 0 && plan != null)
                {
                    for (int i = ogre1LastTaskIndex; i < idx && i < plan.Count; i++)
                    {
                        AddToHistory(ogre1History, plan[i].Name);
                    }
                }
                ogre1LastCompletedIndex = idx - 1;
                ogre1LastTaskIndex = idx;
                needsFullUpdate = true;
            }

            // Check if sub-steps changed (state within current task)
            int subStepHash1 = GetSubStepHash(ogre1);
            if (subStepHash1 != ogre1LastSubStepHash)
            {
                ogre1LastSubStepHash = subStepHash1;
                needsFullUpdate = true;
            }
        }

        // Track Ogre 2
        if (ogre2 != null)
        {
            var plan = ogre2.GetCurrentPlan();
            int idx = ogre2.CurrentTaskIndex;
            int hash = GetPlanHash(ogre2);

            if (hash != ogre2LastPlanHash)
            {
                if (ogre2LastPlan != null && ogre2LastCompletedIndex >= 0)
                {
                    for (int i = 0; i <= ogre2LastCompletedIndex && i < ogre2LastPlan.Count; i++)
                    {
                        AddToHistory(ogre2History, ogre2LastPlan[i].Name);
                    }
                }
                ogre2LastPlan = plan != null ? new List<PrimitiveTask>(plan) : null;
                ogre2LastCompletedIndex = -1;
                ogre2LastPlanHash = hash;
                needsFullUpdate = true;
            }

            if (idx != ogre2LastTaskIndex)
            {
                if (idx > ogre2LastTaskIndex && ogre2LastTaskIndex >= 0 && plan != null)
                {
                    for (int i = ogre2LastTaskIndex; i < idx && i < plan.Count; i++)
                    {
                        AddToHistory(ogre2History, plan[i].Name);
                    }
                }
                ogre2LastCompletedIndex = idx - 1;
                ogre2LastTaskIndex = idx;
                needsFullUpdate = true;
            }

            // Check if sub-steps changed (state within current task)
            int subStepHash2 = GetSubStepHash(ogre2);
            if (subStepHash2 != ogre2LastSubStepHash)
            {
                ogre2LastSubStepHash = subStepHash2;
                needsFullUpdate = true;
            }
        }

        if (needsFullUpdate)
        {
            UpdateHTNDisplay();
        }

        UpdateStateDisplay();
    }

    void AddToHistory(List<string> history, string taskName)
    {
        // Don't add duplicates in a row
        if (history.Count > 0 && history[history.Count - 1] == taskName)
            return;

        history.Add(taskName);

        // Keep only the last N items
        while (history.Count > maxHistoryItems)
        {
            history.RemoveAt(0);
        }
    }

    void ForceUpdateDisplay()
    {
        // Initialize tracking for ogre 1
        if (ogre1 != null)
        {
            var plan1 = ogre1.GetCurrentPlan();
            ogre1LastPlan = plan1 != null ? new List<PrimitiveTask>(plan1) : null;
            ogre1LastTaskIndex = ogre1.CurrentTaskIndex;
            ogre1LastPlanHash = GetPlanHash(ogre1);
            if (showDebugLogs)
                Debug.Log($"[SplitScreenManager] Ogre1 plan: {(plan1 == null ? "null" : plan1.Count + " tasks")}");
        }

        // Initialize tracking for ogre 2
        if (ogre2 != null)
        {
            var plan2 = ogre2.GetCurrentPlan();
            ogre2LastPlan = plan2 != null ? new List<PrimitiveTask>(plan2) : null;
            ogre2LastTaskIndex = ogre2.CurrentTaskIndex;
            ogre2LastPlanHash = GetPlanHash(ogre2);
            if (showDebugLogs)
                Debug.Log($"[SplitScreenManager] Ogre2 plan: {(plan2 == null ? "null" : plan2.Count + " tasks")}");
        }

        UpdateHTNDisplay();
        UpdateStateDisplay();
    }

    int GetPlanHash(OgreController ogre)
    {
        var plan = ogre.GetCurrentPlan();
        if (plan == null || plan.Count == 0) return 0;
        int hash = plan.Count;
        foreach (var task in plan)
        {
            hash = hash * 31 + task.Name.GetHashCode();
        }
        return hash;
    }

    int GetSubStepHash(OgreController ogre)
    {
        var steps = ogre.GetCurrentTaskSteps();
        if (steps == null || steps.Count == 0) return 0;
        int hash = steps.Count;
        foreach (var step in steps)
        {
            hash = hash * 31 + step.Name.GetHashCode();
            hash = hash * 17 + step.Status;
        }
        return hash;
    }

    void UpdateHTNDisplay()
    {
        UpdateOgrePlan(ogre1, ogre1TaskContainer, ogre1TaskEntries, ogre1History, "Ogre1");
        UpdateOgrePlan(ogre2, ogre2TaskContainer, ogre2TaskEntries, ogre2History, "Ogre2");
    }

    void UpdateStateDisplay()
    {
        if (ogre1 != null && ogre1StateText != null)
            ogre1StateText.text = GetStateString(ogre1);
        if (ogre2 != null && ogre2StateText != null)
            ogre2StateText.text = GetStateString(ogre2);
    }

    string GetStateString(OgreController ogre)
    {
        var state = ogre.GetWorldState();
        if (state == null) return "No state";

        List<string> flags = new List<string>();
        if (state.seesPlayer) flags.Add("<color=#FF6666>SEES PLAYER</color>");
        if (state.hasBoulderInHand) flags.Add("<color=#FFAA44>BOULDER</color>");
        if (state.treasureStolen) flags.Add("<color=#FF4444>TREASURE!</color>");
        if (state.hungry) flags.Add("<color=#88DD88>Hungry</color>");
        if (state.playerInvisible) flags.Add("<color=#8888FF>Invisible</color>");

        if (flags.Count == 0) return "<color=#888888>Idle</color>";
        return string.Join("  ", flags);
    }

    void UpdateOgrePlan(OgreController ogre, RectTransform container, List<GameObject> entries,
                        List<string> history, string ogreName)
    {
        if (ogre == null || container == null) return;

        // Clear old entries
        foreach (var entry in entries)
        {
            if (entry != null) Destroy(entry);
        }
        entries.Clear();

        var plan = ogre.GetCurrentPlan();
        int currentIndex = ogre.CurrentTaskIndex;

        // === HISTORY SECTION ===
        if (history.Count > 0)
        {
            // Show history header
            CreateTaskEntry(container, entries, "── History ──", new Color(0.4f, 0.4f, 0.5f), false, true);

            // Show history items (faded)
            foreach (var taskName in history)
            {
                CreateTaskEntry(container, entries, "  ✓ " + taskName, new Color(0.35f, 0.35f, 0.4f), false, false);
            }

            // Separator
            CreateTaskEntry(container, entries, "── Current Plan ──", new Color(0.5f, 0.5f, 0.6f), false, true);
        }

        // === CURRENT PLAN SECTION ===
        if (plan == null || plan.Count == 0)
        {
            CreateTaskEntry(container, entries, "[ Waiting for plan... ]", pendingTaskColor, false, false);
            return;
        }

        for (int i = 0; i < plan.Count; i++)
        {
            string taskName = plan[i].Name;
            Color color;
            bool isCurrent = (i == currentIndex);

            if (i < currentIndex)
            {
                // Recently completed in THIS plan (will move to history on replan)
                color = completedTaskColor;
                taskName = "  ✓ " + taskName;
            }
            else if (i == currentIndex)
            {
                // Currently executing
                color = currentTaskColor;
                taskName = "► " + taskName;
            }
            else
            {
                // Pending
                color = pendingTaskColor;
                taskName = "  ○ " + taskName;
            }

            CreateTaskEntry(container, entries, taskName, color, isCurrent, false);

            // Show ALL sub-steps for the current task
            if (isCurrent)
            {
                var subSteps = ogre.GetCurrentTaskSteps();
                if (subSteps != null && subSteps.Count > 0)
                {
                    foreach (var step in subSteps)
                    {
                        string prefix;
                        Color stepColor;

                        switch (step.Status)
                        {
                            case 2: // Done
                                prefix = "      ✓ ";
                                stepColor = new Color(completedTaskColor.r, completedTaskColor.g, completedTaskColor.b, 0.8f);
                                break;
                            case 1: // Current
                                prefix = "      ► ";
                                stepColor = new Color(currentTaskColor.r, currentTaskColor.g, currentTaskColor.b, 1f);
                                break;
                            default: // Pending (0)
                                prefix = "      ○ ";
                                stepColor = new Color(pendingTaskColor.r * 0.7f, pendingTaskColor.g * 0.7f, pendingTaskColor.b * 0.7f, 0.7f);
                                break;
                        }

                        CreateTaskEntry(container, entries, prefix + step.Name, stepColor, step.Status == 1, false);
                    }
                }
            }
        }
    }

    void CreateTaskEntry(RectTransform container, List<GameObject> entries, string text, Color color,
                         bool highlight, bool isHeader)
    {
        GameObject entry = new GameObject("TaskEntry");
        entry.transform.SetParent(container, false);

        entry.AddComponent<RectTransform>();

        // Add layout element
        var layout = entry.AddComponent<LayoutElement>();
        layout.minHeight = isHeader ? 20 : 24;
        layout.preferredHeight = isHeader ? 20 : 24;

        if (highlight)
        {
            // Background + child text for highlighted entries
            var bg = entry.AddComponent<Image>();
            bg.color = new Color(currentTaskColor.r, currentTaskColor.g, currentTaskColor.b, 0.2f);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(entry.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4, 0);
            textRect.offsetMax = new Vector2(-4, 0);

            var tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = text;
            tmpText.fontSize = 16;
            tmpText.color = color;
            tmpText.alignment = TextAlignmentOptions.Left;
            tmpText.fontStyle = FontStyles.Bold;
        }
        else
        {
            var tmpText = entry.AddComponent<TextMeshProUGUI>();
            tmpText.text = text;
            tmpText.fontSize = isHeader ? 12 : 16;
            tmpText.color = color;
            tmpText.alignment = isHeader ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
            if (isHeader) tmpText.fontStyle = FontStyles.Italic;
        }

        entries.Add(entry);
    }

    void OnDestroy()
    {
        if (gameCamera != null)
        {
            gameCamera.rect = new Rect(0, 0, 1, 1);
        }
    }
}