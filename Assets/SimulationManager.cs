using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SimulationManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject agentPrefab;
    public GameObject foodPrefab;
    public GameObject poisonPrefab;

    [Header("UI Setup Menu")]
    public GameObject setupPanel;
    public TMP_InputField inputAgents;
    public Slider sliderAgents;
    public TMP_InputField inputFood;
    public Slider sliderFood;
    public TMP_InputField inputPoison;
    public Slider sliderPoison;
    public TMP_InputField inputPoisonChance;
    public Slider sliderPoisonChance;
    public TMP_InputField inputVision;
    public Slider sliderVision;

    // Normalized runtime chance (0.0f to 1.0f)
    public static float PoisonDamageChance = 0.5f;

    [Header("UI Leaderboard")]
    public GameObject leaderboardPanel;
    public TextMeshProUGUI txtLeaderboardBody;
    public RectTransform rowHighlightRect;
    private int lastHoveredLine = -1;

    [Header("Simulation Parameters (Defaults)")]
    public int agentCount = 10;
    public int foodCount = 40;
    public int poisonCount = 20;
    public int globalVisionRadius = 10;

    [HideInInspector] public Vector2 arenaSize;
    
    // Active agents (pruned as they die)
    private List<AgentController> activeAgents = new List<AgentController>();
    // Master registry of every agent that ever spawned (persists after death)
    private List<AgentController> allSpawnedAgents = new List<AgentController>();

    private int stepCounter = 0;
    private bool simulationRunning = false;
    private bool gameOverTriggered = false;

    [Header("In-Game HUD / Controls")]
    public GameObject BtnStopSimulation; // Reference to the stop button

    [Header("Audio")]
    public AudioSource musicSource;

    [Header("Possession Management")]
    public AgentController currentlyPossessedAgent;

    [Header("Setup Panel Controls")]
    public Toggle toggleRespawn;

    [Header("Live Running HUD")]
    public TextMeshProUGUI txtRunningStats;
    private float simulationElapsedTime = 0f;

    [Header("Agent Brain Inspection")]
    public GameObject brainInspectorPanel;
    public TextMeshProUGUI txtBrainDetails;
    private List<AgentRecord> cachedRecords = new List<AgentRecord>();

    void Start()
    {
        if (txtRunningStats != null) txtRunningStats.gameObject.SetActive(false); //
        if (BtnStopSimulation != null) BtnStopSimulation.gameObject.SetActive(false); 
        if (brainInspectorPanel != null) brainInspectorPanel.SetActive(false); 

        Camera cam = Camera.main; 
        if (cam != null && cam.orthographic) 
        {
            float screenHeight = cam.orthographicSize; 
            float screenWidth = screenHeight * cam.aspect; 
            arenaSize = new Vector2(screenWidth * 0.9f, screenHeight * 0.9f); 
        }
        else
        {
            arenaSize = new Vector2(16f, 9f); 
        }

        if (inputAgents != null) inputAgents.text = agentCount.ToString(); 
        if (inputFood != null) inputFood.text = foodCount.ToString(); 
        if (inputPoison != null) inputPoison.text = poisonCount.ToString(); 
        if (inputPoisonChance != null) inputPoisonChance.text = Mathf.RoundToInt(PoisonDamageChance * 100f).ToString();
        if (inputVision != null) inputVision.text = globalVisionRadius.ToString(); 

        if (setupPanel != null) setupPanel.SetActive(true); 
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false); 
        if (BtnStopSimulation != null) BtnStopSimulation.SetActive(false); 

        //  Setup Agent Count Slider & bind listener in code
        if (sliderAgents != null) 
        {
            sliderAgents.wholeNumbers = true; 
            sliderAgents.minValue = 1f; // Clamped to 1 so you don't spawn 0 agents
            sliderAgents.maxValue = 20f; 
            sliderAgents.value = agentCount;

            // Automatically updates the agent text box whenever the slider moves:
            sliderAgents.onValueChanged.RemoveAllListeners();
            sliderAgents.onValueChanged.AddListener(delegate { UpdateNumberOfAgents(); });
        }

        // Setup Food Slider & bind listener in code
        if (sliderFood != null) 
        {
            sliderFood.wholeNumbers = true; 
            sliderFood.minValue = 0f; 
            sliderFood.maxValue = 100f; 
            if (inputFood.text != null  && int.TryParse(inputFood.text, out int x)) sliderFood.value = Mathf.Max(0, x);

            // Automatically updates the food chance text box whenever the slider moves:
            sliderFood.onValueChanged.RemoveAllListeners();
            sliderFood.onValueChanged.AddListener(delegate { UpdateFood(); });

            Debug.Log("[SimulationManager.cs] - Start() - sliderFood.value = " + sliderFood.value);
        }

        // Setup Poison Slider & bind listener in code
        if (sliderPoison != null) 
        {
            sliderPoison.wholeNumbers = true; 
            sliderPoison.minValue = 0f; 
            sliderPoison.maxValue = 100f; 
            if (inputPoison.text != null  && int.TryParse(inputPoison.text, out int x)) sliderPoison.value = Mathf.Max(0, x);

            // Automatically updates the poison text box whenever the slider moves:
            sliderPoison.onValueChanged.RemoveAllListeners();
            sliderPoison.onValueChanged.AddListener(delegate { UpdatePoison(); });

            Debug.Log("[SimulationManager.cs] - Start() - sliderPoison.value = " + sliderPoison.value);
        }

        // Setup Poison Chance Slider & bind listener in code
        if (sliderPoisonChance != null) 
        {
            sliderPoisonChance.wholeNumbers = true; 
            sliderPoisonChance.minValue = 0f; 
            sliderPoisonChance.maxValue = 100f; 
            sliderPoisonChance.value = Mathf.RoundToInt(PoisonDamageChance * 100f);

            // Automatically updates the poison chance text box whenever the slider moves:
            sliderPoisonChance.onValueChanged.RemoveAllListeners();
            sliderPoisonChance.onValueChanged.AddListener(delegate { UpdatePoisonChance(); });
        }

        // Setup Vision Slider & bind listener in code
        if (sliderVision != null) 
        {
            sliderVision.wholeNumbers = true; 
            sliderVision.minValue = 0f; 
            sliderVision.maxValue = 20f; 
            sliderVision.value = globalVisionRadius;

            // Automatically updates the food chance text box whenever the slider moves:
            sliderVision.onValueChanged.RemoveAllListeners();
            sliderVision.onValueChanged.AddListener(delegate { UpdateVision(); });

            Debug.Log("[SimulationManager.cs] - Start() - sliderVision.value = " + sliderVision.value);
        }
    }

    public void UpdateNumberOfAgents()
    {
        Debug.Log($"[SimulationManager] In UpdateNumberOfAgents!");
        if (sliderAgents != null && inputAgents != null)
        {
            inputAgents.text = Mathf.RoundToInt(sliderAgents.value).ToString();
        }
    }

    public void UpdateFood()
    {
        Debug.Log($"[SimulationManager] In UpdateFood!");
        if (sliderFood != null && inputFood != null)
        {
            inputFood.text = Mathf.RoundToInt(sliderFood.value).ToString();
        }
    }

    public void UpdatePoison()
    {
        Debug.Log($"[SimulationManager] In UpdatePoison!");
        if (sliderPoison != null && inputPoison != null)
        {
            inputPoison.text = Mathf.RoundToInt(sliderPoison.value).ToString();
        }
    }

    public void UpdatePoisonChance()
    {
        Debug.Log($"[SimulationManager] In UpdatePoisonChance!");
        if (sliderPoisonChance != null && inputPoisonChance != null)
        {
            inputPoisonChance.text = Mathf.RoundToInt(sliderPoisonChance.value).ToString();
        }
    }

    public void UpdateVision()
    {
        Debug.Log($"[SimulationManager] In UpdateVision!");
        if (sliderVision != null)
        {
            inputVision.text = Mathf.RoundToInt(sliderVision.value).ToString();
        }
    }

    public void StartSimulationFromUI()
    {
        simulationElapsedTime = 0f;
        if (txtRunningStats != null) txtRunningStats.gameObject.SetActive(true);
        if (BtnStopSimulation != null) BtnStopSimulation.gameObject.SetActive(true);

        // Set the global respawn rule from the checkbox state
        if (toggleRespawn != null)
        {
            global::SpawnItem.AllowRespawn = toggleRespawn.isOn;
        }
        else
        {
            global::SpawnItem.AllowRespawn = true;
        }

        if (sliderAgents != null)
        {
            agentCount = Mathf.RoundToInt(sliderAgents.value);
            inputAgents.text = agentCount.ToString();
        }
        if (sliderFood != null)
        {
            inputFood.text = sliderFood.value.ToString();
        }
        if (sliderPoison != null)
        {
            inputPoison.text = sliderPoison.value.ToString();
        }
        if (sliderPoisonChance != null)
        {
            inputPoisonChance.text = (Mathf.RoundToInt(sliderPoisonChance.value)).ToString();
        }
        if (sliderVision != null)
        {
            inputVision.text = sliderVision.value.ToString();
        }

        if (inputFood != null && int.TryParse(inputFood.text, out int f)) foodCount = Mathf.Max(0, f);
        if (inputPoison != null && int.TryParse(inputPoison.text, out int p)) poisonCount = Mathf.Max(0, p);
        if (inputVision != null && int.TryParse(inputVision.text, out int v)) globalVisionRadius = Mathf.Max(0, v);
        if (inputPoisonChance != null)
        {
            // Divide by 100f so 50 becomes 0.50f, 100 becomes 1.0f, etc.
            //PoisonDamageChance = inputPoisonChance.value / 100f;
            float.TryParse(inputPoisonChance.text, out PoisonDamageChance);
            PoisonDamageChance = (PoisonDamageChance / 100f);
        }

        if (setupPanel != null) setupPanel.SetActive(false);

        for (int i = 0; i < foodCount; i++) SpawnItem(foodPrefab, "Food");
        for (int i = 0; i < poisonCount; i++) SpawnItem(poisonPrefab, "Poison");

        for (int i = 0; i < agentCount; i++)
        {
            Vector2 pos = GetRandomArenaPosition();
            GameObject go = Instantiate(agentPrefab, pos, Quaternion.identity);
            AgentController controller = go.GetComponent<AgentController>();
            controller.agentIndex = i + 1;
            
            // Updates both the internal radius and the visual ring vertices:
            controller.UpdateVisionRadius(globalVisionRadius);
            
            activeAgents.Add(controller);
            allSpawnedAgents.Add(controller);
        }

        simulationRunning = true;

        if (musicSource != null && !musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    public Vector2 GetRandomArenaPosition()
    {
        return new Vector2(
            Random.Range(-arenaSize.x, arenaSize.x),
            Random.Range(-arenaSize.y, arenaSize.y)
        );
    }

    private void SpawnItem(GameObject prefab, string tag)
    {
        Vector2 pos = GetRandomArenaPosition();
        GameObject obj = Instantiate(prefab, pos, Quaternion.identity);
        obj.tag = tag;
        var spawner = obj.GetComponent<SpawnItem>();
        if (spawner == null) spawner = obj.AddComponent<SpawnItem>();
        spawner.bounds = arenaSize;
    }

    void Update()
    {
        // If the Brain Inspector is open, ignore Leaderboard hover and click events
        if (brainInspectorPanel != null && brainInspectorPanel.activeSelf)
        {
            // Hide the hover highlight behind the inspector
            if (rowHighlightRect != null && rowHighlightRect.gameObject.activeSelf)
            {
                rowHighlightRect.gameObject.SetActive(false);
                lastHoveredLine = -1;
            }
            return;
        }
        
        // Handle Leaderboard hover and clicks while the leaderboard is visible:
        if (gameOverTriggered && leaderboardPanel != null && leaderboardPanel.activeSelf) //
        {
            HandleLeaderboardHover();
            HandleLeaderboardLinkClicks(); //
            return;
        }

        if (!simulationRunning || gameOverTriggered) return;

        // Track elapsed simulation time
        simulationElapsedTime += Time.deltaTime;
        UpdateRunningStatsHUD();

        // Existing Escape key check...
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            StopSimulation();
            return;
        }

        HandleAgentSelection();
    }

    private void HandleLeaderboardLinkClicks()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && txtLeaderboardBody != null)
        {
            Vector3 mousePos = Mouse.current.position.ReadValue();

            // Canvas in Overlay mode requires a null camera for TMP link math
            Canvas parentCanvas = txtLeaderboardBody.canvas;
            Camera uiCamera = (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) 
                ? null 
                : (parentCanvas != null ? parentCanvas.worldCamera : Camera.main);

            // Force an update of the mesh data in case the text was just set
            txtLeaderboardBody.ForceMeshUpdate();

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(txtLeaderboardBody, mousePos, uiCamera);

            if (linkIndex != -1)
            {
                TMP_LinkInfo linkInfo = txtLeaderboardBody.textInfo.linkInfo[linkIndex];
                string linkId = linkInfo.GetLinkID();
                
                Debug.Log($"<color=cyan>[UI] Clicked Link ID: {linkId}</color>");

                if (int.TryParse(linkId, out int clickedAgentId))
                {
                    DisplayAgentBrain(clickedAgentId);
                }
            }
        }
    }

    private void HandleLeaderboardHover()
    {
        if (txtLeaderboardBody == null || rowHighlightRect == null || Mouse.current == null) return;

        Vector3 mousePos = Mouse.current.position.ReadValue();

        Canvas parentCanvas = txtLeaderboardBody.canvas;
        Camera uiCamera = (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) 
            ? null 
            : (parentCanvas != null ? parentCanvas.worldCamera : Camera.main);

        txtLeaderboardBody.ForceMeshUpdate();

        int lineIndex = TMP_TextUtilities.FindIntersectingLine(txtLeaderboardBody, mousePos, uiCamera);

        // Header lines are line 0 (labels) and line 1 (divider dashed line).
        // Agent rows start at lineIndex 2 and go up to 2 + cachedRecords.Count - 1.
        int firstAgentLine = 2;
        int lastAgentLine = firstAgentLine + cachedRecords.Count - 1;

        if (lineIndex >= firstAgentLine && lineIndex <= lastAgentLine)
        {
            if (lineIndex != lastHoveredLine)
            {
                lastHoveredLine = lineIndex;

                TMP_LineInfo lineInfo = txtLeaderboardBody.textInfo.lineInfo[lineIndex];

                // Calculate the Y coordinate of the line's baseline relative to the text RectTransform
                float lineY = (lineInfo.ascender + lineInfo.descender) * 0.5f;
                float lineHeight = lineInfo.lineHeight > 0 ? lineInfo.lineHeight : (lineInfo.ascender - lineInfo.descender) * 1.2f;

                // Position the highlight bar behind the row
                if (!rowHighlightRect.gameObject.activeSelf)
                {
                    rowHighlightRect.gameObject.SetActive(true);
                }

                // Match width to the text bounding width or parent width
                RectTransform textRect = txtLeaderboardBody.rectTransform;
                rowHighlightRect.sizeDelta = new Vector2(textRect.rect.width, lineHeight);
                
                // Align pivot and position
                rowHighlightRect.position = textRect.TransformPoint(new Vector3(0f, lineY, 0f));
            }
        }
        else
        {
            if (rowHighlightRect.gameObject.activeSelf)
            {
                rowHighlightRect.gameObject.SetActive(false);
            }
            lastHoveredLine = -1;
        }
    }

    private void DisplayAgentBrain(int agentId)
    {
        Debug.Log($"<color=yellow>[BrainInspector] Looking up Agent #{agentId} in {cachedRecords.Count} cached records...</color>");

        int index = cachedRecords.FindIndex(r => r.agentId == agentId);
        if (index == -1)
        {
            Debug.LogWarning($"[BrainInspector] Agent #{agentId} was not found in cachedRecords!");
            return;
        }

        AgentRecord record = cachedRecords[index];

        if (brainInspectorPanel != null)
        {
            brainInspectorPanel.SetActive(true);
            brainInspectorPanel.transform.SetAsLastSibling();
        }
        else
        {
            Debug.LogError("[BrainInspector] 'Brain Inspector Panel' is NOT assigned in SimulationManager!");
        }

        if (txtBrainDetails != null)
        {
            string influenceText = record.wasEverPossessed 
            ? "<color=#00FFFF>Directly Player-Controlled</color>"
            : $"<color=#55FF55>{record.playerObservationsLogged} steps</color> ({record.playerInfluencePercent:F1}% of memory)";

        txtBrainDetails.text = 
            $"<b><size=120%><color=#FFD700>Agent #{record.agentId} Brain Summary</color></size></b>\n\n" +
            $"<b>Archetype:</b> <color=#00FFFF>{record.primaryDrive}</color>\n" +
            $"<b>Replay Memories:</b> {record.experiencesLogged} steps\n" +
            $"<b>Player Imprint / Imitation:</b> {influenceText}\n" +
            $"<b>Exploration vs Exploitation:</b> {(1f - record.finalEpsilon) * 100f:F1}% Policy Driven\n\n" +
            $"<b>Learned Instincts:</b>\n" +
            $"• Food Seeking Drive: {(record.foodAffinity > 0f ? "<color=#55FF55>High (+)</color>" : "<color=#AAAAAA>Low (0)</color>")}\n" +
            $"• Poison Avoidance: {(record.poisonAvoidance > 0f ? "<color=#55FF55>Cautious (+)</color>" : "<color=#FF5555>Blind (-)</color>")}";
            // Force the text mesh to re-render immediately
            txtBrainDetails.ForceMeshUpdate();
        }
        else
        {
            Debug.LogError("[BrainInspector] 'Txt Brain Details' slot is NOT assigned in SimulationManager!");
        }
    }


    private void UpdateRunningStatsHUD()
    {
        if (txtRunningStats == null) return;

        // Aggregate stats from all agents that have spawned in this run
        int totalFood = 0; 
        int totalPoison = 0; 
        int totalResisted = 0;
        int totalBites = 0; 

        for (int i = 0; i < allSpawnedAgents.Count; i++) 
        {
            AgentController agent = allSpawnedAgents[i]; 
            if (agent != null) 
            {
                totalFood += agent.foodEatenCount; 
                totalPoison += agent.poisonEatenCount; 
                totalResisted += agent.poisonResistedCount;
                totalBites += agent.bitesDeliveredCount; 
            }
        }

        int minutes = Mathf.FloorToInt(simulationElapsedTime / 60f); 
        int seconds = Mathf.FloorToInt(simulationElapsedTime % 60f); 
        int livingCount = activeAgents.Count; 

        txtRunningStats.text = $"<b>Time:</b> {minutes:00}:{seconds:00}   |   " + 
                               $"<b>Living:</b> {livingCount}/{allSpawnedAgents.Count}   |   " + 
                               $"<b>Food:</b> <color=#55FF55>{totalFood}</color>   |   " +
                               $"<b>Poison:</b> <color=#FF5555>{totalPoison}</color> (<color=#00FFFF>{totalResisted}</color> Resisted)   |   " +
                               $"<b>Attacks:</b> <color=#FFD700>{totalBites}</color>";
    }

    private void HandleAgentSelection()
    {
        // 1. Mouse Click Selection
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero);

            if (hit.collider != null)
            {
                AgentController target = hit.collider.GetComponent<AgentController>();
                if (target != null && target.isAlive)
                {
                    PossessAgent(target);
                    return;
                }
            }

            // Clicking empty space releases possession
            ReleaseCurrentAgent();
        }

        // 2. Controller / Keyboard Cycle shortcut (Tab or Gamepad Y / Triangle)
        bool tabPressed = Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
        bool gamepadCycle = Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;

        if (tabPressed || gamepadCycle)
        {
            CycleNextAgent();
        }
    }

    public void PossessAgent(AgentController agent)
    {
        if (currentlyPossessedAgent != null)
        {
            currentlyPossessedAgent.SetPossessed(false);
        }

        currentlyPossessedAgent = agent;
        if (currentlyPossessedAgent != null)
        {
            currentlyPossessedAgent.SetPossessed(true);
        }
    }

    public void ReleaseCurrentAgent()
    {
        if (currentlyPossessedAgent != null)
        {
            currentlyPossessedAgent.SetPossessed(false);
            currentlyPossessedAgent = null;
        }
    }

    private void CycleNextAgent()
    {
        var aliveAgents = activeAgents.FindAll(a => a != null && a.isAlive);
        if (aliveAgents.Count == 0) return;

        if (currentlyPossessedAgent == null)
        {
            PossessAgent(aliveAgents[0]);
        }
        else
        {
            int currentIndex = aliveAgents.IndexOf(currentlyPossessedAgent);
            int nextIndex = (currentIndex + 1) % aliveAgents.Count;
            PossessAgent(aliveAgents[nextIndex]);
        }
    }

    void FixedUpdate()
    {
        if (!simulationRunning || gameOverTriggered) return;

        // 1. Remove dead or destroyed agents
        activeAgents.RemoveAll(a => a == null || !a.isAlive);

        // 2. Extinction check: Did we spawn agents, and are they now all dead?
        if (activeAgents.Count == 0 && allSpawnedAgents.Count > 0)
        {
            Debug.Log("<color=yellow>[SimulationManager] All agents dead! Triggering Leaderboard...</color>");
            ShowLeaderboard();
            return;
        }

        stepCounter++;

        foreach (var agent in activeAgents) agent.StepSimulation(arenaSize);
        foreach (var agent in activeAgents) agent.ResolveExperience(arenaSize);

        for (int i = 0; i < activeAgents.Count; i++)
        {
            for (int j = 0; j < activeAgents.Count; j++)
            {
                if (i != j) activeAgents[i].ObservePeer(activeAgents[j], arenaSize);
            }
        }

        foreach (var agent in activeAgents) agent.Train();

        if (stepCounter % 200 == 0)
        {
            foreach (var agent in activeAgents) agent.UpdateTargetNet();
        }
    }

    private void ShowLeaderboard()
    {
        Debug.Log("<color=cyan>[ShowLeaderboard] Method called!</color>");
        leaderboardPanel.transform.SetAsLastSibling();
        
        if (txtRunningStats != null) txtRunningStats.gameObject.SetActive(false); //
        if (BtnStopSimulation != null) BtnStopSimulation.SetActive(false); //

        if (musicSource != null && musicSource.isPlaying) //
        {
            musicSource.Stop(); //
        }

        gameOverTriggered = true; //

        if (leaderboardPanel == null) //
        {
            Debug.LogError("[SimulationManager] 'Leaderboard Panel' slot is NOT assigned in the Inspector!"); //
            return; //
        }

        leaderboardPanel.SetActive(true); //
        Debug.Log($"<color=cyan>[ShowLeaderboard] LeaderboardPanel activeSelf is now: {leaderboardPanel.activeSelf}</color>");

        if (rowHighlightRect != null) rowHighlightRect.gameObject.SetActive(false);
        lastHoveredLine = -1;

        List<AgentRecord> records = allSpawnedAgents //
            .Where(a => a != null) //
            .Select(a => a.GetRecord()) //
            .OrderByDescending(r => r.survivalTime) //
            .ToList(); //

        int livingCount = activeAgents.Count(a => a != null && a.isAlive); //
        int deadCount = allSpawnedAgents.Count - livingCount; //

        int totalFoodEaten = 0;
        int totalPoisonEaten = 0;
        int totalPoisonResisted = 0;
        int totalBitesDelivered = 0;
        int totalBitesReceived = 0;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        //sb.AppendLine("<b><color=#AAAAAA>RANK<pos=60>AGENT<pos=155>LIVED<pos=225>FOOD<pos=275>POIS<pos=325>RES<pos=375>GAVE DMG<pos=465>TOOK DMG<pos=565>IMPRINT</color></b>");
        sb.AppendLine("<b><color=#AAAAAA>RANK<pos=60>AGENT<pos=155>LIVED<pos=225>FOOD<pos=295>POISON<pos=385>RESIST<pos=475>GAVE DMG<pos=585>TOOK DMG<pos=700>IMPRINT</color></b>");
        sb.AppendLine("<color=#555555>---------------------------------------------------------------------------------------------------------------------------------</color>");

        for (int i = 0; i < records.Count; i++)
        {
            AgentRecord r = records[i];
            string rank = (i == 0) ? "<color=#FFD700>#1 *</color>" : $"#{i + 1}";
            string agentLink = $"<link=\"{r.agentId}\"><u>Agent #{r.agentId}</u></link>";

            string imprintDisplay;
            if (r.wasEverPossessed)
            {
                imprintDisplay = "<color=#00FFFF>POSSESSED</color>";
            }
            else if (r.playerObservationsLogged > 0)
            {
                imprintDisplay = $"<color=#55FF55>{r.playerObservationsLogged}</color> <size=80%><color=#AAAAAA>({r.playerInfluencePercent:F0}%)</color></size>";
            }
            else
            {
                imprintDisplay = "<color=#555555>0 (0%)</color>";
            }

            // Data rows locked exactly to each header's column anchor:
            sb.AppendLine($"{rank}<pos=60>{agentLink}<pos=155>{r.survivalTime:F1}s<pos=225>{r.foodEaten}<pos=295>{r.poisonEaten}<pos=385>{r.poisonResisted}<pos=475>{r.bitesDelivered}<pos=585>{r.bitesReceived}<pos=700>{imprintDisplay}");

            totalFoodEaten += r.foodEaten;
            totalPoisonEaten += r.poisonEaten;
            totalPoisonResisted += r.poisonResisted;
            totalBitesDelivered += r.bitesDelivered;
            totalBitesReceived += r.bitesReceived;
        }

        sb.AppendLine("<color=#555555>---------------------------------------------------------------------------------------------------------------------------------</color>");
        
        // Align Totals directly beneath the corresponding columns:
        sb.AppendLine($"<b><color=#FFFFFF>TOTALS</color></b>" +
                      $"<pos=225><b><color=#55FF55>{totalFoodEaten}</color></b>" +
                      $"<pos=295><b><color=#FF5555>{totalPoisonEaten}</color></b>" +
                      $"<pos=385><b><color=#00FFFF>{totalPoisonResisted}</color></b>" +
                      $"<pos=475><b><color=#FFD700>{totalBitesDelivered}</color></b>" +
                      $"<pos=585><b><color=#FFAAAA>{totalBitesReceived}</color></b>");

        // Status Row: Living vs Dead Agents
        sb.AppendLine($"<b><color=#AAAAAA>AGENTS:</color></b> <color=#55FF55>{livingCount} Alive</color>  |  <color=#FF5555>{deadCount} Dead</color> <size=85%><color=#888888>({allSpawnedAgents.Count} Total)</color></size>");

        if (txtLeaderboardBody != null) //
        {
            txtLeaderboardBody.text = sb.ToString();
        }
        else
        {
            Debug.LogError("[SimulationManager] 'Txt Leaderboard Body' slot is NOT assigned in the Inspector!"); //
        }

        Debug.Log("[SimulationManager] txtLeaderboardBody = " + txtLeaderboardBody.text);

        cachedRecords = records; //

        // Explicitly ensure the text body GameObject itself is active:
        if (txtLeaderboardBody != null)
        {
            txtLeaderboardBody.gameObject.SetActive(true);
        }
    }

    public void OnRestartSameParameters()
    {
        simulationElapsedTime = 0f;
        if (txtRunningStats != null) txtRunningStats.gameObject.SetActive(true);
        if (BtnStopSimulation != null) BtnStopSimulation.gameObject.SetActive(true);

        if (toggleRespawn != null)
        {
            global::SpawnItem.AllowRespawn = toggleRespawn.isOn;
        }

        CleanupEnvironment();

        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);

        // Re-spawn items
        for (int i = 0; i < foodCount; i++) SpawnItem(foodPrefab, "Food");
        for (int i = 0; i < poisonCount; i++) SpawnItem(poisonPrefab, "Poison");

        // Re-spawn agents
        for (int i = 0; i < agentCount; i++)
        {
            Vector2 pos = GetRandomArenaPosition();
            GameObject go = Instantiate(agentPrefab, pos, Quaternion.identity);
            AgentController controller = go.GetComponent<AgentController>();
            controller.agentIndex = i + 1;

            // Updates both the internal radius and the visual ring vertices:
            controller.UpdateVisionRadius(globalVisionRadius);

            activeAgents.Add(controller);
            allSpawnedAgents.Add(controller);
        }

        gameOverTriggered = false;
        simulationRunning = true;

        if (musicSource != null && !musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    // 2. Button: Reconfigure with new parameters
    public void OnReconfigureNewParameters()
    {
        simulationElapsedTime = 0f;
        if (txtRunningStats != null) txtRunningStats.gameObject.SetActive(false);
        if (BtnStopSimulation != null) BtnStopSimulation.gameObject.SetActive(false);

        CleanupEnvironment();

        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        if (setupPanel != null) setupPanel.SetActive(true);

        gameOverTriggered = false;
        simulationRunning = false;

        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop(); // or musicSource.Pause();
        }
    }

    // Helper: Clears out all old agents (dead or alive), food, and poison
    private void CleanupEnvironment()
    {
        // Destroy all existing agents
        foreach (var agent in allSpawnedAgents)
        {
            if (agent != null) Destroy(agent.gameObject);
        }
        activeAgents.Clear();
        allSpawnedAgents.Clear();

        // Destroy all lingering food and poison items
        GameObject[] foods = GameObject.FindGameObjectsWithTag("Food");
        foreach (var f in foods) Destroy(f);

        GameObject[] poisons = GameObject.FindGameObjectsWithTag("Poison");
        foreach (var p in poisons) Destroy(p);

        // Destroy any lingering floating combat text
        GameObject[] floatingTexts = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var obj in floatingTexts)
        {
            if (obj.name.Contains("FloatingCombatText")) Destroy(obj);
        }

        stepCounter = 0;
        ReleaseCurrentAgent();
    }

    // Call this from the UI Button or by pressing the Escape key
    public void StopSimulation()
    {
        if (!simulationRunning || gameOverTriggered) return;

        Debug.Log("<color=cyan>[SimulationManager] Simulation manually stopped by user.</color>");

        // 1. Hide the Stop button so it's not visible during the leaderboard
        if (BtnStopSimulation != null) BtnStopSimulation.SetActive(false);

        // 2. Stop all live agent movement immediately
        foreach (var agent in activeAgents)
        {
            if (agent != null && agent.isAlive)
            {
                Rigidbody2D rb = agent.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
        }

        // 3. Trigger the leaderboard with current stats
        ShowLeaderboard();
    }

    public void QuitApplication()
    {
        Debug.Log("<color=yellow>[SimulationManager] Quitting application...</color>");

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
