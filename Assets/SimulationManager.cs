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
    public TMP_InputField inputVisionAngle;
    public Slider sliderVisionAngle;

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
    public float globalVisionAngle = 90f;

    [HideInInspector] public Vector2 arenaSize;
    
    // Active agents (pruned as they die)
    private List<AgentController> activeAgents = new List<AgentController>();
    // Master registry of every agent that ever spawned (persists after death)
    private List<AgentController> allSpawnedAgents = new List<AgentController>();

    private int stepCounter = 0;
    private bool simulationRunning = false;
    private bool gameOverTriggered = false;

    [Header("In-Game Runtime Controls")]
    public GameObject BtnStopSimulation; // Reference to the stop button
    public Slider sliderSimulationSpeed;
    public TextMeshProUGUI txtSpeedValue; // Optional: Displays "1.0x", "2.0x", etc.

    [Header("Audio")]
    public AudioSource musicSource;
    public AudioClip introSoundClip;

    [Header("Possession Management")]
    public AgentController currentlyPossessedAgent;

    [Header("Setup Panel Controls")]
    public Toggle toggleFoodRespawn;
    public Toggle togglePoisonRespawn;

    [Header("Live Running HUD")]
    public TextMeshProUGUI txtRunningStats;
    public TextMeshProUGUI txtLiveLegend;
    private float simulationElapsedTime = 0f;

    [Header("Agent Brain Inspection & Replay")]
    public GameObject brainInspectorPanel;
    public TextMeshProUGUI txtBrainDetails;
    public Button btnReplayLife;
    public AgentLifeReplayer lifeReplayer;
    private AgentController inspectedAgentController;
    private List<AgentRecord> cachedRecords = new List<AgentRecord>();

    private HashSet<int> selectedAgentIds = new HashSet<int>();
    public Button btnReplaySelected;
    
    
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
            sliderVision.interactable = true; // Ensure UI interactable flag is ON
            sliderVision.wholeNumbers = true; 
            sliderVision.minValue = 1f; // Clamped to 1 so vision is never 0
            sliderVision.maxValue = 20f; 

            if (inputVision != null && int.TryParse(inputVision.text, out int v))
            {
                sliderVision.value = Mathf.Clamp(v, (int)sliderVision.minValue, (int)sliderVision.maxValue);
            }
            else
            {
                sliderVision.value = globalVisionRadius;
            }

            sliderVision.onValueChanged.RemoveAllListeners();
            sliderVision.onValueChanged.AddListener(delegate { UpdateVisionFromSlider(); });

            if (inputVision != null)
            {
                inputVision.onValueChanged.RemoveAllListeners();
                inputVision.onValueChanged.AddListener(delegate { UpdateVisionFromInput(); });
            }

            Debug.Log("[SimulationManager.cs] - Start() - sliderVision.value = " + sliderVision.value);
        }

        if (inputVisionAngle != null) inputVisionAngle.text = Mathf.RoundToInt(globalVisionAngle).ToString();

        // Setup Vision Angle Slider
        if (sliderVisionAngle != null)
        {
            sliderVisionAngle.interactable = true;
            sliderVisionAngle.wholeNumbers = true;
            sliderVisionAngle.minValue = 15f;
            sliderVisionAngle.maxValue = 120f;
            sliderVisionAngle.value = globalVisionAngle;

            sliderVisionAngle.onValueChanged.RemoveAllListeners();
            sliderVisionAngle.onValueChanged.AddListener(delegate { UpdateVisionAngleFromSlider(); });

            if (inputVisionAngle != null)
            {
                inputVisionAngle.onValueChanged.RemoveAllListeners();
                inputVisionAngle.onValueChanged.AddListener(delegate { UpdateVisionAngleFromInput(); });
            }
        }

        if (txtRunningStats != null) txtRunningStats.gameObject.SetActive(false);
        if (BtnStopSimulation != null) BtnStopSimulation.gameObject.SetActive(false); 
        if (brainInspectorPanel != null) brainInspectorPanel.SetActive(false); 

        // Setup Live Sensory Legend
        if (txtLiveLegend != null)
        {
            txtLiveLegend.text = "<b><size=110%>Sensory Vectors</size></b>\n" +
                                 "<color=#33FF33>■</color> Nearest Food\n" +
                                 "<color=#FF3333>■</color> Nearest Hazard (Poison)\n" +
                                 "<color=#FFC000>■</color> Prey Target (Weaker)\n" +
                                 "<color=#E600E6>■</color> Threat (Predator)";

            // Ensure sorting and visibility defaults
            txtLiveLegend.color = new Color(1f, 1f, 1f, 0.85f);
            txtLiveLegend.ForceMeshUpdate();
            txtLiveLegend.gameObject.SetActive(true);

            Debug.Log("[SimulationManager.cs] - Start() - txtLiveLegend.text = " + txtLiveLegend.text);
        }

        // Runtime user controls.
        if (BtnStopSimulation != null) BtnStopSimulation.SetActive(false); 
        if (sliderSimulationSpeed != null)
        {
            sliderSimulationSpeed.minValue = 0.5f; // Slow motion
            sliderSimulationSpeed.maxValue = 5.0f; // 5x speed
            sliderSimulationSpeed.value = 1.0f;    // Default standard speed

            sliderSimulationSpeed.onValueChanged.RemoveAllListeners();
            sliderSimulationSpeed.onValueChanged.AddListener(OnSpeedSliderChanged);
        }

        if (btnReplayLife != null)
        {
            btnReplayLife.onClick.RemoveAllListeners();
            btnReplayLife.onClick.AddListener(StartReplayForCurrentAgent);
        }
    }

    void Awake()
    {
        if (introSoundClip != null)
        {
            // Plays cleanly at full volume without needing an active AudioSource component
            AudioSource.PlayClipAtPoint(introSoundClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 1f);
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

    public void UpdateVisionFromSlider()
    {
        if (sliderVision != null && inputVision != null)
        {
            inputVision.SetTextWithoutNotify(Mathf.RoundToInt(sliderVision.value).ToString());
            globalVisionRadius = Mathf.RoundToInt(sliderVision.value);
        }
    }

    public void UpdateVisionFromInput()
    {
        if (inputVision != null && sliderVision != null && int.TryParse(inputVision.text, out int val))
        {
            sliderVision.SetValueWithoutNotify(Mathf.Clamp(val, sliderVision.minValue, sliderVision.maxValue));
            globalVisionRadius = Mathf.RoundToInt(sliderVision.value);
        }
    }

    public void UpdateVisionAngleFromSlider()
    {
        if (sliderVisionAngle != null && inputVisionAngle != null)
        {
            inputVisionAngle.SetTextWithoutNotify(Mathf.RoundToInt(sliderVisionAngle.value).ToString());
            globalVisionAngle = sliderVisionAngle.value;
        }
    }

    public void UpdateVisionAngleFromInput()
    {
        if (inputVisionAngle != null && sliderVisionAngle != null && float.TryParse(inputVisionAngle.text, out float val))
        {
            sliderVisionAngle.SetValueWithoutNotify(Mathf.Clamp(val, sliderVisionAngle.minValue, sliderVisionAngle.maxValue));
            globalVisionAngle = sliderVisionAngle.value;
        }
    }

    public void StartReplayForCurrentAgent()
    {
        if (inspectedAgentController == null)
        {
            Debug.LogWarning("[SimulationManager] No agent selected for replay.");
            return;
        }

        if (lifeReplayer == null)
        {
            Debug.LogError("[SimulationManager] 'Life Replayer' is not assigned in the Inspector!");
            return;
        }

        // Hide or dim the brain details panel so the replay view takes focus
        if (brainInspectorPanel != null) brainInspectorPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);

        // Keep the speed slider active and bring to front if parented under HUD
        if (sliderSimulationSpeed != null)
        {
            sliderSimulationSpeed.gameObject.SetActive(true);
            sliderSimulationSpeed.transform.SetAsLastSibling();
            // Apply current slider value to Time.timeScale
            Time.timeScale = sliderSimulationSpeed.value;
        }
        else
        {
            Time.timeScale = 1f;
        }

        if (txtLiveLegend != null) txtLiveLegend.gameObject.SetActive(false);

        lifeReplayer.gameObject.SetActive(true);
        lifeReplayer.transform.SetAsLastSibling();
        lifeReplayer.LoadAgent(inspectedAgentController);
    }

    public void StartSimulationFromUI()
    {
        simulationElapsedTime = 0f;
        if (txtRunningStats != null) txtRunningStats.gameObject.SetActive(true);
        if (BtnStopSimulation != null) BtnStopSimulation.gameObject.SetActive(true);
        
        if (txtLiveLegend != null)
        {
            txtLiveLegend.gameObject.SetActive(true);
            txtLiveLegend.transform.SetAsLastSibling(); // Brings it above background panels
            txtLiveLegend.ForceMeshUpdate();
        }

        // Set speed settings
        float startSpeed = sliderSimulationSpeed != null ? sliderSimulationSpeed.value : 1f;
        OnSpeedSliderChanged(startSpeed);

        // Set the global respawn rule from the checkbox state
        // Apply independent respawn flags from the UI toggles
        global::SpawnItem.AllowFoodRespawn = toggleFoodRespawn != null ? toggleFoodRespawn.isOn : true;
        global::SpawnItem.AllowPoisonRespawn = togglePoisonRespawn != null ? togglePoisonRespawn.isOn : true;

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
        if (sliderVisionAngle != null)
        {
            inputVisionAngle.text = sliderVisionAngle.value.ToString();
        }
        if (inputVisionAngle != null && float.TryParse(inputVisionAngle.text, out float va))
        {
            globalVisionAngle = Mathf.Clamp(va, 15f, 120f);
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
            
            // Explicitly force the vision angle and radius
            controller.UpdateVisionRadius(globalVisionRadius);
            controller.UpdateVisionAngle(globalVisionAngle);
            
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

            Canvas parentCanvas = txtLeaderboardBody.canvas;
            Camera uiCamera = (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) 
                ? null 
                : (parentCanvas != null ? parentCanvas.worldCamera : Camera.main);

            txtLeaderboardBody.ForceMeshUpdate();

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(txtLeaderboardBody, mousePos, uiCamera);

            if (linkIndex != -1)
            {
                TMP_LinkInfo linkInfo = txtLeaderboardBody.textInfo.linkInfo[linkIndex];
                string linkId = linkInfo.GetLinkID();
                
                Debug.Log($"<color=cyan>[UI] Clicked Link ID: {linkId}</color>");

                // 1. Checkbox clicked: Toggle multi-replay selection
                if (linkId.StartsWith("toggle_"))
                {
                    string idPart = linkId.Substring("toggle_".Length);
                    if (int.TryParse(idPart, out int clickedAgentId))
                    {
                        if (selectedAgentIds.Contains(clickedAgentId))
                            selectedAgentIds.Remove(clickedAgentId);
                        else
                            selectedAgentIds.Add(clickedAgentId);

                        UpdateLeaderboardVisualSelection();
                    }
                }
                // 2. Agent Name clicked: Open Brain Inspector
                else if (linkId.StartsWith("brain_"))
                {
                    string idPart = linkId.Substring("brain_".Length);
                    if (int.TryParse(idPart, out int clickedAgentId))
                    {
                        DisplayAgentBrain(clickedAgentId);
                    }
                }
                // Fallback for raw numeric ID
                else if (int.TryParse(linkId, out int rawAgentId))
                {
                    DisplayAgentBrain(rawAgentId);
                }
            }
        }
    }

    /// <summary>
    /// Re-renders the leaderboard text body to show [X] checkboxes and highlights on selected agents.
    /// </summary>
    private void UpdateLeaderboardVisualSelection()
    {
        if (txtLeaderboardBody == null || cachedRecords == null || cachedRecords.Count == 0) return;

        int livingCount = activeAgents.Count(a => a != null && a.isAlive);
        int deadCount = allSpawnedAgents.Count - livingCount;

        int totalFoodEaten = 0;
        int totalPoisonEaten = 0;
        int totalPoisonResisted = 0;
        int totalBitesDelivered = 0;
        int totalBitesReceived = 0;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<b><color=#AAAAAA>SEL<pos=45>RANK<pos=95>AGENT<pos=185>LIVED<pos=255>FOOD<pos=325>POISON<pos=415>RESIST<pos=505>GAVE DMG<pos=615>TOOK DMG<pos=730>IMPRINT</color></b>");
        sb.AppendLine("<color=#555555>---------------------------------------------------------------------------------------------------------------------------------</color>");

        for (int i = 0; i < cachedRecords.Count; i++)
        {
            AgentRecord r = cachedRecords[i];
            bool isSelected = selectedAgentIds != null && selectedAgentIds.Contains(r.agentId);

            // Wrap the checkbox inside a toggle link
            string checkSymbol = isSelected 
                ? "<color=#00FFFF><b>[X]</b></color>" 
                : "<color=#555555>[ ]</color>";
            string selectBox = $"<link=\"toggle_{r.agentId}\">{checkSymbol}</link>";

            string rank = (i == 0) ? "<color=#FFD700>#1 *</color>" : $"#{i + 1}";
            string agentColor = isSelected ? "#00FFFF" : "#FFFFFF";
            
            // Prefix with "brain_" so clicking the name opens the inspector
            string agentLink = $"<link=\"brain_{r.agentId}\"><color={agentColor}><u>Agent #{r.agentId}</u></color></link>";

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

            sb.AppendLine($"{selectBox}<pos=45>{rank}<pos=95>{agentLink}<pos=185>{r.survivalTime:F1}s<pos=255>{r.foodEaten}<pos=325>{r.poisonEaten}<pos=415>{r.poisonResisted}<pos=505>{r.bitesDelivered}<pos=615>{r.bitesReceived}<pos=730>{imprintDisplay}");

            totalFoodEaten += r.foodEaten;
            totalPoisonEaten += r.poisonEaten;
            totalPoisonResisted += r.poisonResisted;
            totalBitesDelivered += r.bitesDelivered;
            totalBitesReceived += r.bitesReceived;
        }

        sb.AppendLine("<color=#555555>---------------------------------------------------------------------------------------------------------------------------------</color>");

        sb.AppendLine($"<b><color=#FFFFFF>TOTALS</color></b>" +
                      $"<pos=255><b><color=#55FF55>{totalFoodEaten}</color></b>" +
                      $"<pos=325><b><color=#FF5555>{totalPoisonEaten}</color></b>" +
                      $"<pos=415><b><color=#00FFFF>{totalPoisonResisted}</color></b>" +
                      $"<pos=505><b><color=#FFD700>{totalBitesDelivered}</color></b>" +
                      $"<pos=615><b><color=#FFAAAA>{totalBitesReceived}</color></b>");

        sb.AppendLine($"<b><color=#AAAAAA>AGENTS:</color></b> <color=#55FF55>{livingCount} Alive</color>  |  <color=#FF5555>{deadCount} Dead</color> <size=85%><color=#888888>({allSpawnedAgents.Count} Total)</color></size>");

        txtLeaderboardBody.text = sb.ToString();
        txtLeaderboardBody.ForceMeshUpdate();

        // Update button text / state if assigned
        if (btnReplaySelected != null)
        {
            btnReplaySelected.interactable = selectedAgentIds.Count > 0;
            var btnText = btnReplaySelected.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = selectedAgentIds.Count > 0 
                    ? $"Replay Selected ({selectedAgentIds.Count})" 
                    : "Replay Selected";
            }
        }
    }

    public void StartMultiAgentReplay()
    {
        if (selectedAgentIds.Count == 0) return;

        List<AgentController> targets = allSpawnedAgents
            .Where(a => a != null && selectedAgentIds.Contains(a.agentIndex))
            .ToList();

        // Temporarily hide original dead agent GameObjects so they don't block the ghosts
        foreach (var agent in allSpawnedAgents)
        {
            if (agent != null) agent.gameObject.SetActive(false);
        }

        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        if (brainInspectorPanel != null) brainInspectorPanel.SetActive(false);

        if (txtLiveLegend != null) txtLiveLegend.gameObject.SetActive(false);

        lifeReplayer.gameObject.SetActive(true);
        lifeReplayer.transform.SetAsLastSibling();
        
        // Pass both selected targets and allSpawnedAgents
        lifeReplayer.LoadSelectedAgents(targets, allSpawnedAgents);
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

        inspectedAgentController = allSpawnedAgents.Find(a => a != null && a.agentIndex == agentId);

        if (btnReplayLife != null)
        {
            // Only enable the button if the agent actually has recorded history
            bool hasHistory = inspectedAgentController != null && inspectedAgentController.lifeHistory != null && inspectedAgentController.lifeHistory.Count > 0;
            btnReplayLife.interactable = hasHistory;
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

            string foodStatus = record.foodAffinity > 0.005f 
                ? $"<color=#55FF55>High (+{record.foodAffinity:F3})</color>" 
                : $"<color=#AAAAAA>Low ({record.foodAffinity:F3})</color>";

            string poisonStatus = record.poisonAvoidance > 0.005f 
                ? $"<color=#55FF55>Cautious (+{record.poisonAvoidance:F3})</color>" 
                : $"<color=#FF5555>Blind ({record.poisonAvoidance:F3})</color>";

            txtBrainDetails.text = 
                $"<b><size=120%><color=#FFD700>Agent #{record.agentId} Brain Summary</color></size></b>\n\n" +
                $"<b>Archetype:</b> <color=#00FFFF>{record.primaryDrive}</color>\n" +
                $"<b>Replay Memories:</b> {record.experiencesLogged} steps\n" +
                $"<b>Peer Observations:</b> <color=#FFAA55>{record.peerObservationsLogged} steps</color>\n" +
                $"<b>Player Imprint / Imitation:</b> {influenceText}\n" +
                $"<b>Exploration vs Exploitation:</b> {(1f - record.finalEpsilon) * 100f:F1}% Policy Driven\n\n" +
                $"<b>Learned Instincts:</b>\n" +
                $"• Food Seeking Drive: {foodStatus}\n" +
                $"• Poison Avoidance: {poisonStatus}";
                
            // Force the text mesh to re-render immediately
            txtBrainDetails.ForceMeshUpdate();
        }
        else
        {
            Debug.LogError("[BrainInspector] 'Txt Brain Details' slot is NOT assigned in SimulationManager!");
        }
    }

    public void OnClickStartReplay()
    {
        if (inspectedAgentController != null && lifeReplayer != null)
        {
            lifeReplayer.gameObject.SetActive(true);
            lifeReplayer.LoadAgent(inspectedAgentController);
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
        
        if (txtRunningStats != null) txtRunningStats.gameObject.SetActive(false);
        if (BtnStopSimulation != null) BtnStopSimulation.SetActive(false);
        if (txtLiveLegend != null) txtLiveLegend.gameObject.SetActive(false);

        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop();
        }

        gameOverTriggered = true;

        if (leaderboardPanel == null)
        {
            Debug.LogError("[SimulationManager] 'Leaderboard Panel' slot is NOT assigned in the Inspector!");
            return;
        }

        leaderboardPanel.SetActive(true);
        Debug.Log($"<color=cyan>[ShowLeaderboard] LeaderboardPanel activeSelf is now: {leaderboardPanel.activeSelf}</color>");

        if (rowHighlightRect != null) rowHighlightRect.gameObject.SetActive(false);
        lastHoveredLine = -1;

        List<AgentRecord> records = allSpawnedAgents
            .Where(a => a != null)
            .Select(a => a.GetRecord())
            .OrderByDescending(r => r.survivalTime)
            .ToList();

        int livingCount = activeAgents.Count(a => a != null && a.isAlive);
        int deadCount = allSpawnedAgents.Count - livingCount;

        int totalFoodEaten = 0;
        int totalPoisonEaten = 0;
        int totalPoisonResisted = 0;
        int totalBitesDelivered = 0;
        int totalBitesReceived = 0;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<b><color=#AAAAAA>SEL<pos=45>RANK<pos=95>AGENT<pos=185>LIVED<pos=255>FOOD<pos=325>POISON<pos=415>RESIST<pos=505>GAVE DMG<pos=615>TOOK DMG<pos=730>IMPRINT</color></b>");
        sb.AppendLine("<color=#555555>---------------------------------------------------------------------------------------------------------------------------------</color>");

        for (int i = 0; i < records.Count; i++)
        {
            AgentRecord r = records[i];
            string rank = (i == 0) ? "<color=#FFD700>#1 *</color>" : $"#{i + 1}";
            
            // Checkbox link
            string selectBox = $"<link=\"toggle_{r.agentId}\"><color=#555555>[ ]</color></link>";
            // Brain Inspector link
            string agentLink = $"<link=\"brain_{r.agentId}\"><u>Agent #{r.agentId}</u></link>";

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

            // Aligned with the SEL column header
            sb.AppendLine($"{selectBox}<pos=45>{rank}<pos=95>{agentLink}<pos=185>{r.survivalTime:F1}s<pos=255>{r.foodEaten}<pos=325>{r.poisonEaten}<pos=415>{r.poisonResisted}<pos=505>{r.bitesDelivered}<pos=615>{r.bitesReceived}<pos=730>{imprintDisplay}");

            totalFoodEaten += r.foodEaten;
            totalPoisonEaten += r.poisonEaten;
            totalPoisonResisted += r.poisonResisted;
            totalBitesDelivered += r.bitesDelivered;
            totalBitesReceived += r.bitesReceived;
        }

        sb.AppendLine("<color=#555555>---------------------------------------------------------------------------------------------------------------------------------</color>");
        
        sb.AppendLine($"<b><color=#FFFFFF>TOTALS</color></b>" +
                      $"<pos=255><b><color=#55FF55>{totalFoodEaten}</color></b>" +
                      $"<pos=325><b><color=#FF5555>{totalPoisonEaten}</color></b>" +
                      $"<pos=415><b><color=#00FFFF>{totalPoisonResisted}</color></b>" +
                      $"<pos=505><b><color=#FFD700>{totalBitesDelivered}</color></b>" +
                      $"<pos=615><b><color=#FFAAAA>{totalBitesReceived}</color></b>");

        sb.AppendLine($"<b><color=#AAAAAA>AGENTS:</color></b> <color=#55FF55>{livingCount} Alive</color>  |  <color=#FF5555>{deadCount} Dead</color> <size=85%><color=#888888>({allSpawnedAgents.Count} Total)</color></size>");

        if (txtLeaderboardBody != null)
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

        // Reset speed
        Time.timeScale = 1f;
        if (musicSource != null) musicSource.pitch = 1f;

        // Clear selected agents for replay
        selectedAgentIds.Clear();
    }

    public void OnRestartSameParameters()
    {
        simulationElapsedTime = 0f;
        if (txtRunningStats != null) txtRunningStats.gameObject.SetActive(true);
        if (BtnStopSimulation != null) BtnStopSimulation.gameObject.SetActive(true);
        
        if (txtLiveLegend != null)
        {
            txtLiveLegend.gameObject.SetActive(true);
            txtLiveLegend.transform.SetAsLastSibling(); // Brings it above background panels
            txtLiveLegend.ForceMeshUpdate();
        }

        // Retain the independent respawn settings
        global::SpawnItem.AllowFoodRespawn = toggleFoodRespawn != null ? toggleFoodRespawn.isOn : true;
        global::SpawnItem.AllowPoisonRespawn = togglePoisonRespawn != null ? togglePoisonRespawn.isOn : true;

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
            controller.UpdateVisionAngle(120f);

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
        if (txtLiveLegend != null) txtLiveLegend.gameObject.SetActive(false);

        CleanupEnvironment();

        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        if (setupPanel != null) setupPanel.SetActive(true);

        gameOverTriggered = false;
        simulationRunning = false;

        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop(); // or musicSource.Pause();
        }

        // Reset Speed
        Time.timeScale = 1f;
        if (musicSource != null) musicSource.pitch = 1f;
    }

    public void OnSpeedSliderChanged(float speed)
    {
        // 1. Scale physics, agent timers, and fixed updates
        Time.timeScale = speed;

        // 2. Adjust music playback speed/pitch proportionally
        if (musicSource != null)
        {
            musicSource.pitch = speed;
        }

        // 3. Update UI text label if assigned
        if (txtSpeedValue != null)
        {
            txtSpeedValue.text = $"{speed:F1}x";
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

        // Clear selected agents for replay
        selectedAgentIds.Clear();
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

        // Reset speed
        Time.timeScale = 1f;
        if (musicSource != null) musicSource.pitch = 1f;
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
