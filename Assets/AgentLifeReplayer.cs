using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AgentLifeReplayer : MonoBehaviour
{
    [Header("UI Controls")]
    public Slider timelineSlider;
    public Button btnPlayPause;
    public TextMeshProUGUI txtPlayPauseLabel;
    public TextMeshProUGUI txtTimestamp;
    public TextMeshProUGUI txtStepStats;

    [Header("Replay Dummy/Ghost")]
    public Transform ghostAgentTransform;
    public SpriteRenderer ghostRenderer;
    public LineRenderer foodSensorLine;
    public LineRenderer poisonSensorLine;

    private List<AgentSnapshot> activeHistory;
    private int currentFrame = 0;
    private bool isPlaying = false;
    private float playbackSpeed = 1f;

    [Header("Playback Timing")]
    [Tooltip("Seconds to wait between snapshots. Increase this to slow down the replay.")]
    [Range(0.05f, 1.0f)]
    public float stepInterval = 0.25f; // 0.25s = 4 frames per second (smooth slow motion)

    [Header("Vision Cone Settings")]
    public LineRenderer visionConeLine;
    public float visionAngle = 120f;
    public float visionRadius = 10f;

    [Header("Ghost Item Markers")]
    public Sprite foodSprite;
    public Sprite poisonSprite;
    private List<GameObject> spawnedGhostItems = new List<GameObject>();
    private List<ConsumptionEvent> activeConsumptionHistory;

    [Header("Audio Settings")]
    public AudioClip foodEatClip;
    public AudioClip poisonEatClip;
    [Range(0f, 1f)] public float soundVolume = 0.8f;
    private AudioSource replayAudioSource;
    private int lastPlayedEventIndex = -1;

    [Header("Combat Sensory Rays")]
    public LineRenderer preySensorLine;    // Amber ray pointing toward weaker agent
    public LineRenderer threatSensorLine;  // Purple/Magenta ray pointing toward larger threat
    public AudioClip swordClashClip;       // Clash sound for combat hits

    [Header("Peer Ghost Renderers")]
    public SpriteRenderer threatGhostRenderer;
    public SpriteRenderer preyGhostRenderer;

    private Coroutine playbackCoroutine;

    void Awake()
    {        
        if (timelineSlider != null)
        {
            timelineSlider.onValueChanged.AddListener(OnScrubbed);
        }
        if (btnPlayPause != null)
        {
            btnPlayPause.onClick.AddListener(TogglePlayPause);
        }

        // Configure Food Ray
        if (foodSensorLine != null)
        {
            foodSensorLine.useWorldSpace = true;
            foodSensorLine.startWidth = 0.05f;
            foodSensorLine.endWidth = 0.02f;
            foodSensorLine.sortingOrder = 15;
            foodSensorLine.material = new Material(Shader.Find("Sprites/Default"));
            foodSensorLine.startColor = new Color(0.2f, 1f, 0.2f, 0.8f);
            foodSensorLine.endColor = new Color(0.2f, 1f, 0.2f, 0.1f);
        }

        // Configure Poison Ray
        if (poisonSensorLine != null)
        {
            poisonSensorLine.useWorldSpace = true;
            poisonSensorLine.startWidth = 0.05f;
            poisonSensorLine.endWidth = 0.02f;
            poisonSensorLine.sortingOrder = 15;
            poisonSensorLine.material = new Material(Shader.Find("Sprites/Default"));
            poisonSensorLine.startColor = new Color(1f, 0.2f, 0.2f, 0.8f);
            poisonSensorLine.endColor = new Color(1f, 0.2f, 0.2f, 0.1f);
        }

        // Configure Vision Cone Arc
        if (visionConeLine != null)
        {
            visionConeLine.useWorldSpace = false;
            visionConeLine.loop = false;
            visionConeLine.startWidth = 0.05f; // Slightly thinner line
            visionConeLine.endWidth = 0.05f;
            visionConeLine.sortingOrder = 14;

            // 1. Toned-down, subtle color (low alpha for transparency)
            Color subtleColor = new Color(0.15f, 0.65f, 0.25f, 0.18f); // 18% opacity
            visionConeLine.startColor = subtleColor;
            visionConeLine.endColor = subtleColor;

            // 2. Dash Texture Setup
            Texture2D dashTex = CreateDashTexture();
            Material dashMat = new Material(Shader.Find("Sprites/Default"));
            dashMat.mainTexture = dashTex;

            visionConeLine.material = dashMat;
            visionConeLine.textureMode = LineTextureMode.Tile; // Tiles the texture along line length
        }

        // Configure Prey Ray (Hunting - Gold/Amber)
        if (preySensorLine != null)
        {
            preySensorLine.useWorldSpace = true;
            preySensorLine.startWidth = 0.05f;
            preySensorLine.endWidth = 0.02f;
            preySensorLine.sortingOrder = 15;
            preySensorLine.material = new Material(Shader.Find("Sprites/Default"));
            preySensorLine.startColor = new Color(1f, 0.75f, 0f, 0.85f);
            preySensorLine.endColor = new Color(1f, 0.75f, 0f, 0.1f);
        }

        // Configure Threat Ray (Hunted - Magenta/Purple)
        if (threatSensorLine != null)
        {
            threatSensorLine.useWorldSpace = true;
            threatSensorLine.startWidth = 0.06f;
            threatSensorLine.endWidth = 0.02f;
            threatSensorLine.sortingOrder = 15;
            threatSensorLine.material = new Material(Shader.Find("Sprites/Default"));
            threatSensorLine.startColor = new Color(0.9f, 0.1f, 0.9f, 0.85f);
            threatSensorLine.endColor = new Color(0.9f, 0.1f, 0.9f, 0.1f);
        }

        replayAudioSource = gameObject.GetComponent<AudioSource>();
        if (replayAudioSource == null)
        {
            replayAudioSource = gameObject.AddComponent<AudioSource>();
        }
        replayAudioSource.playOnAwake = false;
    }

    // Helper: Generates a small alternating solid/transparent texture
    private Texture2D CreateDashTexture()
    {
        int width = 16;
        Texture2D tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[width];
        for (int i = 0; i < width; i++)
        {
            // First half solid white, second half fully transparent
            pixels[i] = (i < width / 2) ? Color.white : Color.clear;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    public void LoadAgent(AgentController agent)
    {
        if (agent == null || agent.lifeHistory == null || agent.lifeHistory.Count == 0)
        {
            Debug.LogWarning("[Replayer] No recorded history for this agent.");
            return;
        }

        // Copy audio clips directly from the agent
        foodEatClip = agent.foodEatClip;
        poisonEatClip = agent.poisonEatClip;
        swordClashClip = agent.swordClashClip;
        lastPlayedEventIndex = -1;

        activeConsumptionHistory = agent.consumptionHistory;
        SpawnGhostMarkers(activeConsumptionHistory);

        // Match the loaded agent's vision parameters
        visionRadius = agent.visionRadius;
        visionAngle = agent.visionAngle;
        DrawVisionArc(24);

        if (visionConeLine != null) visionConeLine.enabled = true;

        activeHistory = agent.lifeHistory;
        currentFrame = 0;
        isPlaying = false;

        if (timelineSlider != null)
        {
            timelineSlider.minValue = 0;
            timelineSlider.maxValue = activeHistory.Count - 1;
            timelineSlider.value = 0;
        }

        if (ghostAgentTransform != null) ghostAgentTransform.gameObject.SetActive(true);
        ApplySnapshot(0);
    }

    public void TogglePlayPause()
    {
        isPlaying = !isPlaying;

        if (isPlaying)
        {
            if (txtPlayPauseLabel != null) txtPlayPauseLabel.text = "PAUSE";

            // If we've reached the end of the timeline, loop back to the beginning
            if (currentFrame >= activeHistory.Count - 1)
            {
                currentFrame = 0;
                lastPlayedEventIndex = -1; // Reset sound index
            }

            // Start a single, controlled coroutine
            if (playbackCoroutine != null) StopCoroutine(playbackCoroutine);
            playbackCoroutine = StartCoroutine(PlaybackRoutine());
        }
        else
        {
            if (txtPlayPauseLabel != null) txtPlayPauseLabel.text = "PLAY";
            if (playbackCoroutine != null)
            {
                StopCoroutine(playbackCoroutine);
                playbackCoroutine = null;
            }
        }
    }

    private IEnumerator PlaybackRoutine()
    {
        while (isPlaying && currentFrame < activeHistory.Count - 1)
        {
            currentFrame++;

            if (timelineSlider != null) timelineSlider.SetValueWithoutNotify(currentFrame);
            ApplySnapshot(currentFrame);

            // Wait for the configured interval before advancing to the next snapshot
            //yield return new WaitForSecondsRealtime(stepInterval);
            yield return new WaitForSeconds(stepInterval);
        }

        // Replay finished
        isPlaying = false;
        playbackCoroutine = null;
        if (txtPlayPauseLabel != null) txtPlayPauseLabel.text = "REPLAY";
    }

    private void ApplySnapshot(int frameIndex)
    {
        if (activeHistory == null || frameIndex >= activeHistory.Count) return;

        AgentSnapshot snap = activeHistory[frameIndex];

        // --- PLAY CONSUMPTION AUDIO ---
        CheckAndPlayConsumptionAudio(snap.timeStamp);

        // 1. Move and rotate the ghost organism
        if (ghostAgentTransform != null)
        {
            ghostAgentTransform.position = snap.position;
            ghostAgentTransform.rotation = Quaternion.Euler(0, 0, snap.rotationAngle);
        }

        // Draw sensor vectors (Food & Poison rays)
        // Sensor state layout:
        // [0,1]   = Pos (normX, normY)
        // [2,3]   = Nearest Food Dir
        // [4,5]   = Nearest Poison Dir
        // [6,7]   = Nearest Threat Dir (peer.energy > this.energy)
        // [8,9]   = Nearest Prey Dir   (peer.energy < this.energy)
        // [10]    = Health ratio
        if (snap.sensorState != null && snap.sensorState.Length >= 10)
        {
            Vector3 origin = new Vector3(snap.position.x, snap.position.y, -0.5f);
            float rayLength = visionRadius;
            // Vector components from sensor state (indices 2,3 for food, 4,5 for poison)
            Vector3 foodDir = new Vector3(snap.sensorState[2], snap.sensorState[3], 0f);
            Vector3 poisonDir = new Vector3(snap.sensorState[4], snap.sensorState[5], 0f);
            // Combat vectors components from sensor state (indices 6,7 for threat, 7,9 for pray)
            Vector3 threatDir = new Vector3(snap.sensorState[6], snap.sensorState[7], 0f);
            Vector3 preyDir = new Vector3(snap.sensorState[8], snap.sensorState[9], 0f);

            // Ray length: show a 2.5 unit visual ray if pointing toward a target
            //float rayLength = 2.5f;

            // Draw Threat Ray (Magenta)
            if (threatSensorLine != null)
            {
                bool hasThreat = threatDir.sqrMagnitude > 0.01f;
                threatSensorLine.enabled = hasThreat;
                if (hasThreat)
                {
                    threatSensorLine.positionCount = 2;
                    threatSensorLine.SetPosition(0, origin);
                    threatSensorLine.SetPosition(1, origin + threatDir.normalized * rayLength);
                }
            }

            // Draw Prey Ray (Gold)
            if (preySensorLine != null)
            {
                bool hasPrey = preyDir.sqrMagnitude > 0.01f;
                preySensorLine.enabled = hasPrey;
                if (hasPrey)
                {
                    preySensorLine.positionCount = 2;
                    preySensorLine.SetPosition(0, origin);
                    preySensorLine.SetPosition(1, origin + preyDir.normalized * rayLength);
                }
            }

            if (foodSensorLine != null)
            {
                bool hasFoodTarget = foodDir.sqrMagnitude > 0.01f;
                foodSensorLine.enabled = hasFoodTarget;
                if (hasFoodTarget)
                {
                    foodSensorLine.positionCount = 2;
                    foodSensorLine.SetPosition(0, origin);
                    foodSensorLine.SetPosition(1, origin + foodDir.normalized * rayLength);
                }
            }

            if (poisonSensorLine != null)
            {
                bool hasPoisonTarget = poisonDir.sqrMagnitude > 0.01f;
                poisonSensorLine.enabled = hasPoisonTarget;
                if (hasPoisonTarget)
                {
                    poisonSensorLine.positionCount = 2;
                    poisonSensorLine.SetPosition(0, origin);
                    poisonSensorLine.SetPosition(1, origin + poisonDir.normalized * rayLength);
                }
            }

            // --- DISPLAY VISIBLE PEER GHOSTS ---
            if (threatGhostRenderer != null)
            {
                threatGhostRenderer.gameObject.SetActive(snap.hasThreatVisible);
                if (snap.hasThreatVisible)
                {
                    // Force Z to -0.5f so it renders in front of background planes
                    threatGhostRenderer.transform.position = new Vector3(snap.threatWorldPos.x, snap.threatWorldPos.y, -0.5f);
                }
            }

            if (preyGhostRenderer != null)
            {
                preyGhostRenderer.gameObject.SetActive(snap.hasPreyVisible);
                if (snap.hasPreyVisible)
                {
                    // Force Z to -0.5f so it renders in front of background planes
                    preyGhostRenderer.transform.position = new Vector3(snap.preyWorldPos.x, snap.preyWorldPos.y, -0.5f);
                }
            }
        }

        // 3. Update HUD stats
        string[] actionNames = { "UP", "DOWN", "LEFT", "RIGHT" };
        string actionStr = snap.actionTaken >= 0 && snap.actionTaken < 4 ? actionNames[snap.actionTaken] : "?";

        if (txtTimestamp != null)
        {
            txtTimestamp.text = $"Time: {snap.timeStamp:F1}s (Frame {frameIndex}/{activeHistory.Count})";
        }

        if (txtStepStats != null)
        {
            txtStepStats.text = $"HP: <color={(snap.energy > 30 ? "#55FF55" : "#FF5555")}>{snap.energy:F0}</color> | " +
                                $"Action: <color=#00FFFF>{actionStr}</color> | " +
                                $"Step Reward: <color={(snap.reward >= 0 ? "#55FF55" : "#FF5555")}>{snap.reward:F2}</color>";
        }
    }

    private void DrawVisionArc(int segments = 24)
    {
        if (visionConeLine == null) return;

        visionConeLine.positionCount = segments + 2;

        float halfAngleRad = (visionAngle * 0.5f) * Mathf.Deg2Rad;
        float startAngle = -halfAngleRad;
        float deltaAngle = (visionAngle * Mathf.Deg2Rad) / segments;

        // Point 0: Center
        visionConeLine.SetPosition(0, Vector3.zero);

        // Points 1 to segments+1: The curved perimeter
        for (int i = 0; i <= segments; i++)
        {
            float theta = startAngle + (deltaAngle * i);
            float x = visionRadius * Mathf.Cos(theta);
            float y = visionRadius * Mathf.Sin(theta);
            visionConeLine.SetPosition(i + 1, new Vector3(x, y, -0.1f));
        }

        // Controls how dense the dashes are along the line length
        if (visionConeLine.material != null)
        {
            // Increase the 'X' tiling value for more frequent, smaller dashes
            visionConeLine.material.mainTextureScale = new Vector2(4f, 1f);
        }

        // Final Point: Back to center to complete the wedge
        visionConeLine.SetPosition(segments + 1, Vector3.zero);
    }

    private void SpawnGhostMarkers(List<ConsumptionEvent> events)
    {
        ClearGhostMarkers();
        if (events == null) return;

        foreach (var ev in events)
        {
            GameObject ghost = new GameObject($"Ghost_{ev.itemType}");
            ghost.transform.position = new Vector3(ev.position.x, ev.position.y, 0.1f);

            SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
            sr.sprite = (ev.itemType == "Food") ? foodSprite : poisonSprite;
            
            // Faint, semi-transparent ghost tint
            Color baseColor = (ev.itemType == "Food") ? new Color(0.4f, 1f, 0.4f, 0.28f) : new Color(1f, 0.3f, 0.3f, 0.28f);
            sr.color = baseColor;
            sr.sortingOrder = 5;

            spawnedGhostItems.Add(ghost);
        }
    }

    private void ClearGhostMarkers()
    {
        foreach (var item in spawnedGhostItems)
        {
            if (item != null) Destroy(item);
        }
        spawnedGhostItems.Clear();
    }

    private void CheckAndPlayConsumptionAudio(float currentTime)
    {
        if (activeConsumptionHistory == null || activeConsumptionHistory.Count == 0) return;

        for (int i = 0; i < activeConsumptionHistory.Count; i++)
        {
            // If the agent reached or just passed this event and we haven't played it yet
            if (currentTime >= activeConsumptionHistory[i].timeStamp && i > lastPlayedEventIndex)
            {
                lastPlayedEventIndex = i;

                if (replayAudioSource != null)
                {
                    if (activeConsumptionHistory[i].itemType == "Food" && foodEatClip != null)
                    {
                        replayAudioSource.PlayOneShot(foodEatClip, soundVolume);
                    }
                    else if (activeConsumptionHistory[i].itemType == "Poison" && poisonEatClip != null)
                    {
                        replayAudioSource.PlayOneShot(poisonEatClip, soundVolume);
                    }
                    else if (activeConsumptionHistory[i].itemType == "Attack" || activeConsumptionHistory[i].itemType == "Attacked")
                    {
                        if (swordClashClip != null)
                        {
                            replayAudioSource.PlayOneShot(swordClashClip, soundVolume);
                        }
                    }
                }
            }
        }
    }

    public void OnScrubbed(float value)
    {
        if (activeHistory == null || activeHistory.Count == 0) return;
        currentFrame = Mathf.Clamp(Mathf.RoundToInt(value), 0, activeHistory.Count - 1);

        // Reset sound index to the scrubbed point so earlier sounds can replay
        float scrubTime = activeHistory[currentFrame].timeStamp;
        lastPlayedEventIndex = -1;
        if (activeConsumptionHistory != null)
        {
            for (int i = 0; i < activeConsumptionHistory.Count; i++)
            {
                if (activeConsumptionHistory[i].timeStamp < scrubTime)
                {
                    lastPlayedEventIndex = i;
                }
            }
        }

        ApplySnapshot(currentFrame);
    }

    public void CloseReplay()
    {
        isPlaying = false;

        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }
        if (ghostAgentTransform != null) ghostAgentTransform.gameObject.SetActive(false);
        if (foodSensorLine != null) foodSensorLine.enabled = false;
        if (poisonSensorLine != null) poisonSensorLine.enabled = false;
        if (visionConeLine != null) visionConeLine.enabled = false;
        if (preySensorLine != null) preySensorLine.enabled = false;
        if (threatSensorLine != null) threatSensorLine.enabled = false;
        if (threatGhostRenderer != null) threatGhostRenderer.gameObject.SetActive(false);
        if (preyGhostRenderer != null) preyGhostRenderer.gameObject.SetActive(false);

        gameObject.SetActive(false);

        // Reset speed to normal for UI navigation
        Time.timeScale = 1f;

        ClearGhostMarkers();

        // Find the SimulationManager and bring the leaderboard back into view
        SimulationManager sim = Object.FindAnyObjectByType<SimulationManager>();
        if (sim != null)
        {
            if (sim.sliderSimulationSpeed != null) sim.sliderSimulationSpeed.value = 1f;
            if (sim.leaderboardPanel != null)
            {
                sim.leaderboardPanel.SetActive(true);
                sim.leaderboardPanel.transform.SetAsLastSibling();
            }
        }
    }
}