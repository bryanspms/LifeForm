using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[System.Serializable]
public struct AgentRecord
{
    public int agentId;
    public float survivalTime;
    public int foodEaten;
    public int poisonEaten;
    public int poisonResisted;  // Nuber of times the agent was able to avoid being poisoned
    public int bitesDelivered;  // Bites inflicted on others
    public int bitesReceived;   // Bites taken/sustained from others

    // Player / Possession Tracking
    public bool wasEverPossessed;
    public int playerObservationsLogged;   // Total experiences copied from the player
    public int peerObservationsLogged;     // Total experiences observed
    public float playerInfluencePercent;   // Percentage of buffer influenced by player
    
    // Cognitive / Training summaries
    public int experiencesLogged;
    public float finalEpsilon;
    public string primaryDrive;     // e.g. "Food Seeker", "Predator", "Cautious"
    public float foodAffinity;      // Q-value difference when sensing food
    public float poisonAvoidance;   // Q-value penalty when sensing poison
}

public class AgentController : MonoBehaviour
{
    [Header("Manual Possession")]
    public bool isPossessed = false;
    public float manualMoveSpeed = 5f;

    // Call this to toggle possession on/off
    public void SetPossessed(bool possessed)
    {
        isPossessed = possessed;
        if (possessed) hasBeenPossessed = true;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && isAlive)
        {
            // Cyan when possessed, original yellow when released
            sr.color = isPossessed ? Color.cyan : defaultColor;
        }

        if (possessed) isHovered = false;

        Debug.Log($"[Control] Agent #{agentIndex} possession: {isPossessed}");
    }

    [Header("Organism Identification")]
    public int agentIndex = 0;

    [Header("Organism State")]
    public float energy = 100f;
    public bool isAlive = true;
    public float visionRadius = 5f;
    [Range(10f, 360f)] public float visionAngle = 120f; // <-- Vision cone spread in degrees

    [Range(0f, 1f)] public float imitationWeight = 0.7f;
    public float lifetime = 0f;

    [Header("Leaderboard Stats")]
    public int foodEatenCount = 0;
    public int poisonEatenCount = 0;
    public int poisonResistedCount = 0;
    public int bitesDeliveredCount = 0;
    public int bitesReceivedCount = 0;

    [Header("Predatory Mechanics")]
    public float siphonAmount = 15f;
    private float siphonCooldown = 0.5f;
    private float lastSiphonTime = 0f;

    [Header("Hyperparameters")]
    public float epsilon = 0.35f;
    public float epsilonMin = 0.05f;
    public float epsilonDecay = 0.999f;
    public float gamma = 0.95f;
    public float learningRate = 0.005f;

    [Header("Audio")]
    public AudioClip swordClashClip;
    public AudioClip foodEatClip;
    public AudioClip poisonEatClip;
    public AudioClip deathClip;
    [Range(0f, 1f)] public float soundVolume = 1f;

    [HideInInspector] public NeuralNetwork policyNet;
    [HideInInspector] public NeuralNetwork targetNet;
    [HideInInspector] public ReplayBuffer memory;

    private Rigidbody2D rb;
    private Vector2[] actions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
    private float moveSpeed = 4f;

    private AudioSource audioSource;

    // Movement smoothing & action persistence
    private int decisionCooldown = 0;
    private const int DECISION_INTERVAL = 8;

    // Overhead HUD
    private TextMeshPro overheadText;

    public float[] LastState { get; private set; }
    public int LastAction { get; private set; }
    public float LastReward { get; private set; }

    private Color defaultColor = Color.yellow;

    [Header("Inspection / Hover")]
    public bool isHovered = false;

    private LineRenderer visionRing;

    [Header("Player & Peer Tracking")]
    public bool hasBeenPossessed = false;
    public int playerMemoriesImprinted = 0;
    public int peerMemoriesImprinted = 0; // Tracks observations of other AI agents

    [Header("Visuals & Death")]
    public Sprite deadSprite; // Assign the death slice from your sprite sheet in the Inspector

    // Distance-based reward shaping
    private float previousFoodDistance = -1f;
    private float currentFoodDistance = -1f;
    private float previousPoisonDistance = -1f;
    private float currentPoisonDistance = -1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            defaultColor = sr.color;
        }

        policyNet = new NeuralNetwork(11, 32, 4);
        targetNet = policyNet.Clone();
        /* =========================================================================================
         * REPLAY BUFFER CAPACITY & MEMORY TUNING GUIDE
         * =========================================================================================
         *
         * 1. MEMORY FOOTPRINT PER AGENT:
         *    A standard 2D RL transition step (S, A, R, S', done + overhead) is roughly 100 - 150 bytes.
         *    - 3,000 steps  : ~0.3 - 0.45 MB per agent
         *    - 6,000 steps  : ~0.6 - 0.90 MB per agent
         *    - 10,000 steps : ~1.0 - 1.50 MB per agent
         *    - 15,000 steps : ~1.5 - 2.25 MB per agent
         *
         *    Even across 10-20 active agents, 10,000 steps consumes only ~20 - 30 MB total RAM.
         *    System memory is NOT the bottleneck on modern hardware or Steam Deck (16 GB unified).
         *
         * 2. REINFORCEMENT LEARNING CONSTRAINTS:
         *    - Policy Staleness:
         *      If the buffer is too large (15k+ steps), an agent that has survived for minutes will
         *      continue to sample random memories from when it was a naive agent flailing into poison.
         *      This can dilute recent learning and slow down convergence.
         *    - GC Spikes & Collection Overhead:
         *      Avoid using List<T>.RemoveAt(0) when the buffer exceeds capacity, as shifting thousands
         *      of elements every FixedUpdate creates frame stutters. Use a Queue<T> or a fixed-size
         *      circular ring buffer array with a rolling write index.
         *
         * 3. RECOMMENDATION:
         *    Set capacity to ~10,000 steps.
         *    This allows an agent surviving 3 to 4 minutes of simulation time to retain a rich history
         *    without stalling policy convergence on obsolete early-game transitions.
         * ========================================================================================= */
        const int REPLAY_BUFFER_CAPACITY = 10000;
        memory = new ReplayBuffer(REPLAY_BUFFER_CAPACITY);

        SetupOverheadText();
        SetupVisionRing(); // <-- Initialize the vision circle
    }

    void Update()
    {
        if (isAlive)
        {
            lifetime += Time.deltaTime;
            UpdateHUD();
        }
    }

    void FixedUpdate()
    {
        if (!isAlive) return;

        if (isPossessed)
        {
            HandleManualMovement();
        }
    }

    private void HandleManualMovement()
    {
        Vector2 inputDir = Vector2.zero;

        // 1. Check all connected gamepads (fixes Steam virtual controller device indexing)
        var pad = Gamepad.current;
        if (pad == null && Gamepad.all.Count > 0) pad = Gamepad.all[0];

        if (pad != null)
        {
            inputDir = pad.leftStick.ReadValue();

            if (inputDir.sqrMagnitude < 0.01f)
            {
                inputDir = pad.dpad.ReadValue();
            }
        }

        // 2. Keyboard fallback (WASD & Arrow Keys)
        if (inputDir.sqrMagnitude < 0.01f && Keyboard.current != null)
        {
            Vector2 kbDir = Vector2.zero;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) kbDir.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) kbDir.y -= 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) kbDir.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) kbDir.x += 1f;
            inputDir = kbDir;
        }

        if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

        if (rb != null)
        {
            rb.linearVelocity = inputDir * manualMoveSpeed;

            if (inputDir.sqrMagnitude > 0.05f)
            {
                float angle = Mathf.Atan2(inputDir.y, inputDir.x) * Mathf.Rad2Deg;
                rb.rotation = angle;
            }
        }
    }

    private void SetupOverheadText()
    {
        GameObject textObj = new GameObject("OverheadStats");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = new Vector3(0f, 3.2f, 0f);

        overheadText = textObj.AddComponent<TextMeshPro>();

        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(8f, 4f);

        overheadText.fontSize = 10.5f;
        overheadText.textWrappingMode = TextWrappingModes.NoWrap;
        overheadText.alignment = TextAlignmentOptions.Center;
        overheadText.sortingOrder = 10;
    }

    private void UpdateHUD()
    {
        if (overheadText == null) return;

        if (isAlive)
        {
            overheadText.text = $"<b>#{agentIndex}</b>\nHP: {energy:F0}\n<size=70%>{lifetime:F1}s</size>";
            overheadText.color = energy > 35f ? Color.green : Color.yellow;
        }
        else
        {
            overheadText.text = $"<b>#{agentIndex}</b>\n<color=#FF5555>DEAD</color>\n<size=70%>Lived: {lifetime:F1}s</size>";
            overheadText.color = new Color(0.7f, 0.7f, 0.7f, 0.9f);
        }
    }

    public float[] GetSensorState(Vector2 arenaBounds)
    {
        float normX = (transform.position.x / arenaBounds.x) * 2f - 1f;
        float normY = (transform.position.y / arenaBounds.y) * 2f - 1f;

        // Mask out layer 2 ("Ignore Raycast") where corpses reside
        int mask = ~(1 << 2);
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, visionRadius, mask);

        Vector2 nearestFoodDir = Vector2.zero;
        Vector2 nearestPoisonDir = Vector2.zero;
        Vector2 nearestThreatDir = Vector2.zero;
        Vector2 nearestPreyDir = Vector2.zero;

        float minFDist = float.MaxValue;
        float minPDist = float.MaxValue;
        float minTDist = float.MaxValue;
        float minPreyDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            AgentController peer = hit.GetComponent<AgentController>();
            if (peer != null)
            {
                if (peer.isAlive && peer.energy > 0f)
                {
                    float pDist = Vector2.Distance(transform.position, peer.transform.position);
                    Vector2 dirToPeer = ((Vector2)peer.transform.position - (Vector2)transform.position).normalized;

                    if (peer.energy > this.energy)
                    {
                        if (pDist < minTDist)
                        {
                            minTDist = pDist;
                            nearestThreatDir = dirToPeer;
                        }
                    }
                    else if (peer.energy < this.energy)
                    {
                        if (pDist < minPreyDist)
                        {
                            minPreyDist = pDist;
                            nearestPreyDir = dirToPeer;
                        }
                    }
                }
                continue;
            }

            float dist = Vector2.Distance(transform.position, hit.transform.position);

            if (hit.CompareTag("Food") && dist < minFDist)
            {
                minFDist = dist;
                nearestFoodDir = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
            }
            else if (hit.CompareTag("Poison") && dist < minPDist)
            {
                minPDist = dist;
                nearestPoisonDir = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
            }
        }

        // Track nearest food and poison distance for reward shaping (-1 if not in sight)
        currentFoodDistance = (minFDist < float.MaxValue) ? minFDist : -1f;
        currentPoisonDistance = (minPDist < float.MaxValue) ? minPDist : -1f;

        float healthRatio = Mathf.Clamp01(energy / 100f);

        return new float[] {
            normX, normY,
            nearestFoodDir.x, nearestFoodDir.y,
            nearestPoisonDir.x, nearestPoisonDir.y,
            nearestThreatDir.x, nearestThreatDir.y,
            nearestPreyDir.x, nearestPreyDir.y,
            healthRatio
        };
    }

    public int SelectAction(float[] state)
    {
        if (Random.value < epsilon) return Random.Range(0, 4);

        float[] hidden;
        float[] q = policyNet.Forward(state, out hidden);

        int bestAction = 0;
        float bestVal = q[0];
        for (int i = 1; i < 4; i++)
        {
            if (q[i] > bestVal)
            {
                bestVal = q[i];
                bestAction = i;
            }
        }
        return bestAction;
    }

    public void StepSimulation(Vector2 arenaBounds)
    {
        if (!isAlive) return;

        // Freeze AI and movement when hovered by the mouse cursor
        if (isHovered && !isPossessed)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        // Bypasses AI neural logic while player is controlling the agent:
        if (isPossessed)
        {
            energy -= 0.04f;
            if (energy <= 0f) Die();

            // Wrap boundaries for possessed player
            Vector3 pos = transform.position;
            if (pos.x > arenaBounds.x) pos.x = -arenaBounds.x;
            else if (pos.x < -arenaBounds.x) pos.x = arenaBounds.x;

            if (pos.y > arenaBounds.y) pos.y = -arenaBounds.y;
            else if (pos.y < -arenaBounds.y) pos.y = arenaBounds.y;

            transform.position = pos;
            return;
        }

        float desperation = 1f - Mathf.Clamp01(energy / 100f);
        int dynamicInterval = Mathf.RoundToInt(Mathf.Lerp(DECISION_INTERVAL, 3, desperation));

        if (decisionCooldown <= 0)
        {
            LastState = GetSensorState(arenaBounds);
            LastAction = SelectAction(LastState);
            LastReward = 0f; // Reset per decision frame so rewards accurately reflect immediate transitions
            decisionCooldown = dynamicInterval;
        }
        else
        {
            decisionCooldown--;
        }

        float dynamicSpeed = moveSpeed * (1f + desperation * 0.75f);
        Vector2 targetVelocity = actions[LastAction] * dynamicSpeed;
        float steerResponsiveness = Mathf.Lerp(0.25f, 0.55f, desperation);

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVelocity, steerResponsiveness);

        // Rotate agent to face current direction so the vision arc aims forward
        if (targetVelocity.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(targetVelocity.y, targetVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // Wrap around screen boundaries
        Vector3 wrapPos = transform.position;
        if (wrapPos.x > arenaBounds.x) wrapPos.x = -arenaBounds.x;
        else if (wrapPos.x < -arenaBounds.x) wrapPos.x = arenaBounds.x;

        if (wrapPos.y > arenaBounds.y) wrapPos.y = -arenaBounds.y;
        else if (wrapPos.y < -arenaBounds.y) wrapPos.y = arenaBounds.y;

        transform.position = wrapPos;
        rb.position = wrapPos; // removes a visual flicker when an agent crosses screen boundariaes.

        energy -= 0.04f;
        LastReward -= 0.005f;

        // Give a tiny positive reward if the agent reduces its distance to the nearest visible food item.
        // This gives the agent a "scent trail" so it doesn't need to bump into food purely by luck before learning begins.
        // The same is true for poison so it can try to avoid it.
        // --- REWARD SHAPING: APPROACHING / RETREATING ---
        // 1. Food Attraction
        if (currentFoodDistance > 0f && previousFoodDistance > 0f)
        {
            float distanceDelta = previousFoodDistance - currentFoodDistance;
            LastReward += distanceDelta * 0.05f;
        }
        previousFoodDistance = currentFoodDistance;

        // 2. Poison Repulsion (penalty for stepping closer, reward for steering away)
        if (currentPoisonDistance > 0f && previousPoisonDistance > 0f)
        {
            float poisonDelta = previousPoisonDistance - currentPoisonDistance;
            // Negative if moving closer, positive if moving away
            LastReward -= poisonDelta * 0.08f;
        }
        previousPoisonDistance = currentPoisonDistance;

        energy -= 0.04f;
        LastReward -= 0.005f;

        if (energy <= 0f) Die();
    }

    public void ResolveExperience(Vector2 arenaBounds)
    {
        if (!isAlive) return;
        float[] nextState = GetSensorState(arenaBounds);
        memory.Push(LastState, LastAction, LastReward, nextState, !isAlive, false);
    }

    public void ObservePeer(AgentController peer, Vector2 arenaBounds)
    {
        if (!isAlive || peer == this || !peer.isAlive) return;

        float dist = Vector2.Distance(transform.position, peer.transform.position);
        if (dist <= visionRadius)
        {
            float discountedReward = peer.LastReward * imitationWeight;
            float[] peerNextState = peer.GetSensorState(arenaBounds);

            // Track whether this came from the player or a regular peer
            if (peer.isPossessed)
            {
                playerMemoriesImprinted++;
            }
            else
            {
                peerMemoriesImprinted++;
            }

            memory.Push(peer.LastState, peer.LastAction, discountedReward, peerNextState, !peer.isAlive, true);
        }
    }

    public void Train()
    {
        if (memory.Count < 32 || !isAlive) return;

        for (int i = 0; i < 4; i++)
        {
            Experience exp = memory.Sample();
            float targetQ = exp.Reward;

            if (!exp.Done)
            {
                float[] h;
                float[] nextQ = targetNet.Forward(exp.NextState, out h);
                targetQ += gamma * nextQ.Max();
            }

            policyNet.Train(exp.State, exp.Action, targetQ, learningRate);
        }

        epsilon = Mathf.Max(epsilonMin, epsilon * epsilonDecay);
    }

    public void UpdateTargetNet() => targetNet = policyNet.Clone();

    private void OnCollisionEnter2D(Collision2D collision) => HandleOverlap(collision.collider);

    private void OnTriggerEnter2D(Collider2D other) => HandleOverlap(other);

    private void HandleOverlap(Collider2D other)
    {
        if (!isAlive) return;

        if (other.CompareTag("Food"))
        {
            energy += 30f;
            LastReward += 10f;
            foodEatenCount++;

            if (audioSource != null && foodEatClip != null)
            {
                audioSource.PlayOneShot(foodEatClip, soundVolume);
            }

            other.gameObject.GetComponent<SpawnItem>()?.Respawn();
        }
        else if (other.CompareTag("Poison"))
        {
            if (Random.value < SimulationManager.PoisonDamageChance)
            {
                poisonEatenCount++;
                energy -= 40f;
                LastReward -= 15f;

                if (audioSource != null && poisonEatClip != null)
                {
                    audioSource.PlayOneShot(poisonEatClip, soundVolume);
                }

                if (energy <= 0f)
                {
                    // Push terminal death transition to memory BEFORE calling Die()
                    float[] nextState = GetSensorState(Vector2.zero);
                    memory.Push(LastState, LastAction, LastReward - 20f, nextState, true, false);
                    Die();
                }
            } 
            else 
            {
                // Resisted / Dodged: No damage taken
                SpawnCombatText("Resist!", Color.gray);
                poisonResistedCount++;
                //LastReward += 1.0f; // Small encouragement for surviving near poison
            }

            // Reset distance tracking so respawn teleport isn't counted as movement
            previousPoisonDistance = -1f;
            currentPoisonDistance = -1f;

            other.gameObject.GetComponent<SpawnItem>()?.Respawn();
        }
        else if (other.CompareTag("Player") || other.CompareTag("Agent"))
        {
            AgentController peer = other.GetComponent<AgentController>();
            if (peer != null && peer.isAlive && peer.energy > 0f && Time.time >= lastSiphonTime + siphonCooldown)
            {
                ResolveLifeSteal(peer);
            }
        }
    }

    private void ResolveLifeSteal(AgentController peer)
    {
        Debug.Log($"[Combat] Agent #{agentIndex} collided with Agent #{peer.agentIndex}. Energy: {this.energy} vs {peer.energy}");

        if (!isAlive || peer == null || !peer.isAlive || peer.energy <= 0f) return;

        if (this.energy >= peer.energy)
        {
            float drain = Mathf.Min(siphonAmount, peer.energy);
            if (drain <= 0f) return;

            float desperation = 1f - Mathf.Clamp01(this.energy / 100f);
            float reward = 8f + (desperation * 16f);

            this.energy += drain;
            this.LastReward += reward;
            this.lastSiphonTime = Time.time;
            bitesDeliveredCount++;

            if (swordClashClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(swordClashClip, soundVolume);
            }
            if (swordClashClip == null)
            {
                Debug.LogError($"[Audio] Agent #{agentIndex} collided, but swordClashClip is NULL!");
            }

            StartCoroutine(FlashColor(Color.magenta, 0.15f));
            SpawnCombatText($"+{drain:F0}", new Color(0.2f, 1f, 0.2f));

            peer.TakeDamage(drain, this);
        }
    }

    public void TakeDamage(float amount, AgentController attacker)
    {
        if (!isAlive) return;

        energy -= amount;
        bitesReceivedCount++;
        LastReward -= 10f;

        StartCoroutine(FlashColor(Color.red, 0.15f));
        SpawnCombatText($"-{amount:F0}", new Color(1f, 0.25f, 0.25f));

        if (attacker != null)
        {
            Vector2 escapeDir = ((Vector2)transform.position - (Vector2)attacker.transform.position).normalized;
            if (escapeDir == Vector2.zero) escapeDir = Random.insideUnitCircle.normalized;

            rb.linearVelocity = escapeDir * (moveSpeed * 2.2f);
            decisionCooldown = DECISION_INTERVAL;
        }

        if (energy <= 0f) Die();
    }

    private void Die()
    {
        isAlive = false;
        LastReward -= 20f;

        if (visionRing != null)
        {
            visionRing.enabled = false;
        }

        // Hide overhead HUD so dead agents don't bleed through the leaderboard UI
        if (overheadText != null)
        {
            overheadText.gameObject.SetActive(false);
        }

        // Play death sound effect
        if (deathClip != null)
        {
            AudioSource.PlayClipAtPoint(deathClip, new Vector3(transform.position.x, transform.position.y, -10f), soundVolume);
        }

        // Shift layer and tag
        gameObject.layer = 2; // "Ignore Raycast"
        gameObject.tag = "Untagged";

        // Completely disable all colliders on both the root object and children
        Collider2D rootCol = GetComponent<Collider2D>();
        if (rootCol != null) rootCol.enabled = false;

        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>();
        foreach (var col in allColliders)
        {
            col.enabled = false;
        }

        // Shut down Rigidbody completely
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false; // Prevents any physical interaction with moving agents
        }

        //SpriteRenderer sr = GetComponent<SpriteRenderer>();
        //if (sr != null) sr.color = new Color(0.35f, 0.35f, 0.35f, 0.5f); // Dim and slightly transparent
        // --- SWAP TO DEAD SPRITE ---
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (deadSprite != null)
            {
                sr.sprite = deadSprite;
            }

            // Keep the corpse visible with a subtle dim or tint
            // If your dead sprite is already stylized/colored, use pure Color.white:
            sr.color = new Color(0.7f, 0.7f, 0.7f, 0.85f);
        }

        UpdateHUD();

        if (isPossessed)
        {
            isPossessed = false;
            isHovered = false;
        }
    }

    private void SpawnCombatText(string text, Color color)
    {
        GameObject go = new GameObject("FloatingCombatText");
        Vector3 spawnPos = transform.position + new Vector3(Random.Range(-0.4f, 0.4f), 1.0f, 0f);
        FloatingText ft = go.AddComponent<FloatingText>();
        ft.Initialize(text, color, spawnPos);
    }

    private IEnumerator FlashColor(Color flashColor, float duration)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null || !isAlive) yield break;

        sr.color = flashColor;
        yield return new WaitForSeconds(duration);
        
        if (isAlive)
        {
            sr.color = isPossessed ? Color.cyan : defaultColor;
        }
    }

    public AgentRecord GetRecord()
    {
        float foodScore = 0f;
        float poisonPenalty = 0f;
        string drive = "Erratic Wanderer";

        if (policyNet != null)
        {
            float[] hidden;

            // Probe 1: Food directly UP (Does agent prefer Action 0: Up over Action 1: Down?)
            float[] probeFoodUp = new float[] { 0f, 0f,   0f, 1f,   0f, 0f,   0f, 0f,   0f, 0f,   1f };
            float[] qFoodUp = policyNet.Forward(probeFoodUp, out hidden);
            float foodScoreUp = qFoodUp[0] - qFoodUp[1]; // Up vs Down

            // Probe 2: Food directly RIGHT (Does agent prefer Action 3: Right over Action 2: Left?)
            float[] probeFoodRight = new float[] { 0f, 0f,   1f, 0f,   0f, 0f,   0f, 0f,   0f, 0f,   1f };
            float[] qFoodRight = policyNet.Forward(probeFoodRight, out hidden);
            float foodScoreRight = qFoodRight[3] - qFoodRight[2]; // Right vs Left

            foodScore = (foodScoreUp + foodScoreRight) * 0.5f;

            // Probe 3: Poison directly UP (Does agent prefer Action 1: Down/Retreat over Action 0: Up/Hazard?)
            float[] probePoisonUp = new float[] { 0f, 0f,   0f, 0f,   0f, 1f,   0f, 0f,   0f, 0f,   1f };
            float[] qPoisonUp = policyNet.Forward(probePoisonUp, out hidden);
            float poisonAvoidUp = qPoisonUp[1] - qPoisonUp[0]; // Down (retreat) minus Up (danger)

            // Probe 4: Poison directly RIGHT (Does agent prefer Action 2: Left/Retreat over Action 3: Right/Hazard?)
            float[] probePoisonRight = new float[] { 0f, 0f,   0f, 0f,   1f, 0f,   0f, 0f,   0f, 0f,   1f };
            float[] qPoisonRight = policyNet.Forward(probePoisonRight, out hidden);
            float poisonAvoidRight = qPoisonRight[2] - qPoisonRight[3]; // Left (retreat) minus Right (danger)

            poisonPenalty = (poisonAvoidUp + poisonAvoidRight) * 0.5f;

            // --- REFINED ARCHETYPE ASSIGNMENT ---
            if (bitesDeliveredCount >= 2)
            {
                drive = "Apex Predator";
            }
            else if (foodEatenCount >= 3 && (foodScore > 0f || foodEatenCount > poisonEatenCount))
            {
                drive = "Forager";
            }
            else if (poisonResistedCount >= 2 || poisonPenalty > 0.005f)
            {
                drive = "Cautious Survivor";
            }
            else if (lifetime > 45f && foodEatenCount == 0)
            {
                drive = "Nomadic Pacifist";
            }
            else if (memory != null && memory.Count > 3000 && Mathf.Abs(foodScore) < 0.02f && Mathf.Abs(poisonPenalty) < 0.02f)
            {
                drive = "Indecisive Wanderer";
            }
            else
            {
                drive = "Erratic Explorer";
            }
        }

        int totalMemory = memory != null ? memory.Count : 0;
        float influence = totalMemory > 0 
            ? Mathf.Clamp01((float)playerMemoriesImprinted / totalMemory) * 100f 
            : 0f;

        return new AgentRecord
        {
            agentId = agentIndex,
            survivalTime = lifetime,
            foodEaten = foodEatenCount,
            poisonEaten = poisonEatenCount,
            poisonResisted = poisonResistedCount,
            bitesDelivered = bitesDeliveredCount,
            bitesReceived = bitesReceivedCount,
            
            wasEverPossessed = hasBeenPossessed,
            playerObservationsLogged = playerMemoriesImprinted,
            peerObservationsLogged = peerMemoriesImprinted,
            playerInfluencePercent = influence,

            experiencesLogged = totalMemory,
            finalEpsilon = epsilon,
            primaryDrive = drive,
            foodAffinity = foodScore,
            poisonAvoidance = poisonPenalty
        };
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, visionRadius);
    }

    private void OnMouseEnter()
    {
        // Ignore corpses and possessed agents
        if (!isAlive || isPossessed) return;

        isHovered = true;

        // Immediately kill momentum so the agent freezes in place
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void OnMouseExit()
    {
        if (isHovered)
        {
            isHovered = false;
        }
    }

    private void SetupVisionRing()
    {
        GameObject ringObj = new GameObject("VisionRing");
        ringObj.transform.SetParent(transform);
        ringObj.transform.localPosition = Vector3.zero;

        visionRing = ringObj.AddComponent<LineRenderer>();
        visionRing.useWorldSpace = false;
        visionRing.loop = false; // <-- Changed from true to false

        visionRing.material = new Material(Shader.Find("Sprites/Default"));
        
        Color ringColor = new Color(0.2f, 0.8f, 0.2f, 0.35f);
        visionRing.startColor = ringColor;
        visionRing.endColor = ringColor;

        visionRing.startWidth = 0.08f;
        visionRing.endWidth = 0.08f;
        visionRing.sortingOrder = 1;

        DrawVisionArc(24);
    }

    public void UpdateVisionRadius(float newRadius)
    {
        visionRadius = newRadius;
        DrawVisionArc(24);
    }

    public void DrawVisionCircle(int segments = 36)
    {
        if (visionRing == null) return;

        visionRing.positionCount = segments;
        float deltaTheta = (2f * Mathf.PI) / segments;
        float theta = 0f;

        // Compensate for any scaling applied to the Agent GameObject
        float scaleFactor = transform.lossyScale.x != 0f ? transform.lossyScale.x : 1f;
        float unscaledRadius = visionRadius / scaleFactor;

        for (int i = 0; i < segments; i++)
        {
            float x = unscaledRadius * Mathf.Cos(theta);
            float y = unscaledRadius * Mathf.Sin(theta);
            visionRing.SetPosition(i, new Vector3(x, y, 0f));
            theta += deltaTheta;
        }
    }

    public void DrawVisionArc(int segments = 24)
    {
        if (visionRing == null) return;

        // We need segments + 2 points: Center -> Arc Points -> Center
        visionRing.positionCount = segments + 2;

        float scaleFactor = transform.lossyScale.x != 0f ? transform.lossyScale.x : 1f;
        float unscaledRadius = visionRadius / scaleFactor;

        // Convert vision angle spread to radians centered around 0 (forward)
        float halfAngleRad = (visionAngle * 0.5f) * Mathf.Deg2Rad;
        float startAngle = -halfAngleRad;
        float deltaAngle = (visionAngle * Mathf.Deg2Rad) / segments;

        // Point 0: Start at center
        visionRing.SetPosition(0, Vector3.zero);

        // Points 1 to segments+1: The curved perimeter
        for (int i = 0; i <= segments; i++)
        {
            float theta = startAngle + (deltaAngle * i);
            float x = unscaledRadius * Mathf.Cos(theta);
            float y = unscaledRadius * Mathf.Sin(theta);
            visionRing.SetPosition(i + 1, new Vector3(x, y, 0f));
        }

        // Final Point: Back to center to complete the wedge
        visionRing.SetPosition(segments + 1, Vector3.zero);
    }
}