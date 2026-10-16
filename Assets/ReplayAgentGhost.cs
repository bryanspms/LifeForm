using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ReplayAgentGhost : MonoBehaviour
{
    [Header("Visual Components")]
    public SpriteRenderer ghostRenderer;
    public LineRenderer foodSensorLine;
    public LineRenderer poisonSensorLine;
    public LineRenderer preySensorLine;
    public LineRenderer threatSensorLine;
    public LineRenderer visionConeLine;

    [HideInInspector] public AgentController sourceAgent;
    [HideInInspector] public List<AgentSnapshot> history;
    private float visionRadius = 5f;
    //private float visionAngle = 30f; //120f;
    [Range(15f, 120f)]
    public float visionAngle = 90f;

    [HideInInspector] public bool isBackgroundGhost = false;

    [Header("Overhead Label")]
    public TextMeshPro overheadText;

    /**/
    [Header("Replay Combat Feedback")]
    public GameObject floatingTextPrefab; // Assign FloatingCombatTextPrefab in Inspector or Resources
    private float lastEvaluatedEnergy = -1f;
    private float lastEvaluatedTime = -1f;
    

    public void Initialize(AgentController agent, bool asBackgroundGhost = false)
    {
        isBackgroundGhost = asBackgroundGhost;
        sourceAgent = agent;
        history = agent.lifeHistory;
        visionRadius = agent.visionRadius;
        visionAngle = agent.visionAngle;

        Debug.Log(
            $"Ghost #{agent.agentIndex} angle={visionAngle}");

        SetupOverheadText();

        // Background peers: small, faint label; Selected primary agents: bold bright label
        if (overheadText != null)
        {
            overheadText.color = isBackgroundGhost 
                ? new Color(0.7f, 0.7f, 0.7f, 0.4f) 
                : new Color(0f, 1f, 1f, 0.95f); // Cyan header for selected agents
        }

        // Configure Ghost Body Sprite
        if (ghostRenderer != null)
        {
            ghostRenderer.enabled = true;

            if (agent.aliveSprite != null)
            {
                ghostRenderer.sprite = agent.aliveSprite;
            }
            else
            {
                SpriteRenderer agentSR = agent.GetComponent<SpriteRenderer>();
                if (agentSR != null && agentSR.sprite != null)
                {
                    ghostRenderer.sprite = agentSR.sprite;
                }
            }

            // Primary ghosts: 85% opacity; Background interacting peers: faint 25% opacity
            float alpha = isBackgroundGhost ? 0.25f : 0.85f;
            Color baseColor = (agent != null) ? Color.yellow : Color.white;
            ghostRenderer.color = new Color(baseColor.r * 0.7f, baseColor.g * 0.7f, baseColor.b * 0.7f, alpha);
            ghostRenderer.sortingOrder = isBackgroundGhost ? 22 : 25;
        }

        transform.localScale = agent.transform.localScale;

        // Only generate vision arcs and sensory lines for primary selected agents
        if (!isBackgroundGhost)
        {
            SetupRays();
            DrawVisionArc(24);
        }
        else
        {
            // Ensure rays and cones are disabled on secondary ghosts
            if (foodSensorLine != null) foodSensorLine.enabled = false;
            if (poisonSensorLine != null) poisonSensorLine.enabled = false;
            if (preySensorLine != null) preySensorLine.enabled = false;
            if (threatSensorLine != null) threatSensorLine.enabled = false;
            if (visionConeLine != null) visionConeLine.enabled = false;
        }
    }

    private void SetupRays()
    {
        if (foodSensorLine != null) ConfigureRay(foodSensorLine, new Color(0.2f, 1f, 0.2f, 0.8f), new Color(0.2f, 1f, 0.2f, 0.1f));
        if (poisonSensorLine != null) ConfigureRay(poisonSensorLine, new Color(1f, 0.2f, 0.2f, 0.8f), new Color(1f, 0.2f, 0.2f, 0.1f));
        if (preySensorLine != null) ConfigureRay(preySensorLine, new Color(1f, 0.75f, 0f, 0.85f), new Color(1f, 0.75f, 0f, 0.1f));
        if (threatSensorLine != null) ConfigureRay(threatSensorLine, new Color(0.9f, 0.1f, 0.9f, 0.85f), new Color(0.9f, 0.1f, 0.9f, 0.1f));

        if (visionConeLine != null)
        {
            visionConeLine.useWorldSpace = false;
            visionConeLine.loop = false;
            visionConeLine.startWidth = 0.04f;
            visionConeLine.endWidth = 0.04f;
            visionConeLine.sortingOrder = 18;

            Color subtleGreen = new Color(0.15f, 0.65f, 0.25f, 0.35f);
            visionConeLine.startColor = subtleGreen;
            visionConeLine.endColor = subtleGreen;

            Texture2D dashTex = CreateDashTexture();
            Material dashMat = new Material(Shader.Find("Sprites/Default"));
            dashMat.mainTexture = dashTex;
            visionConeLine.material = dashMat;
            visionConeLine.textureMode = LineTextureMode.Tile;
        }
    }

    private void ConfigureRay(LineRenderer line, Color start, Color end)
    {
        if (line == null) return;
        line.useWorldSpace = true;
        // Lock widths thin regardless of prefab inspector values
        line.startWidth = 0.04f;
        line.endWidth = 0.02f;
        line.sortingOrder = 20;

        // Guarantees proper unlit sprite shader
        if (line.material == null || line.material.shader.name != "Sprites/Default")
        {
            line.material = new Material(Shader.Find("Sprites/Default"));
        }

        line.startColor = start;
        line.endColor = end;
    }

    private Texture2D CreateDashTexture()
    {
        int width = 16;
        Texture2D tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[width];
        for (int i = 0; i < width; i++)
        {
            pixels[i] = (i < width / 2) ? Color.white : Color.clear;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    public void EvaluateAtTime(float time)
    {
        if (history == null || history.Count == 0) return;

        // If beyond this agent's lifetime, hide the ghost
        if (time > history[history.Count - 1].timeStamp)
        {
            SetVisible(false);
            return;
        }

        int frameIndex = FindSnapshotIndex(time);
        if (frameIndex < 0)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        ApplySnapshot(history[frameIndex]);
    }

    private int FindSnapshotIndex(float time)
    {
        int low = 0;
        int high = history.Count - 1;

        if (time < history[0].timeStamp) return -1;
        if (time >= history[high].timeStamp) return high;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            if (history[mid].timeStamp <= time)
            {
                if (mid == history.Count - 1 || history[mid + 1].timeStamp > time)
                    return mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }
        return low;
    }

    private void ApplySnapshot(AgentSnapshot snap)
    {
        Debug.Log(
            $"Ghost #{sourceAgent.agentIndex} " +
            $"Rotation={snap.rotationAngle:F0}");

        Debug.Log(
            $"Ghost #{sourceAgent.agentIndex} " +
            $"Prey Visible={snap.hasPreyVisible} " +
            $"Dir=({snap.sensorState:F2},{snap.sensorState[9]:F2})");

        Debug.Log(
            $"Ghost #{sourceAgent.agentIndex} " +
            $"Threat Visible={snap.hasThreatVisible} " +
            $"Dir=({snap.sensorState:F2},{snap.sensorState[7]:F2})");


        transform.position = new Vector3(snap.position.x, snap.position.y, -0.5f);
        //transform.rotation = Quaternion.Euler(0, 0, snap.rotationAngle + 90f);
        transform.rotation = Quaternion.Euler(0, 0, snap.rotationAngle);
        /*
        Debug.DrawRay(
            transform.position,
            transform.right * 5f,
            Color.cyan,
            0.5f);
        */
        Debug.DrawRay(
            transform.position,
            transform.right * visionRadius,
            Color.cyan,
            0.1f);
        //transform.position = snap.position;
        //transform.rotation = Quaternion.Euler(0, 0, snap.rotationAngle + 90f);

        // Combat Damage Detection during forward playback
        if (lastEvaluatedTime >= 0f && snap.timeStamp > lastEvaluatedTime)
        {
            if (lastEvaluatedEnergy > 0f && snap.energy < lastEvaluatedEnergy)
            {
                float hpLost = lastEvaluatedEnergy - snap.energy;
                if (hpLost >= 5f)
                {
                    SpawnCombatPopup(snap.position, Mathf.RoundToInt(hpLost));
                }
            }
        }

        lastEvaluatedTime = snap.timeStamp;
        lastEvaluatedEnergy = snap.energy;

        if (ghostRenderer != null && sourceAgent != null)
        {
            if (snap.energy <= 0f && sourceAgent.deadSprite != null)
            {
                ghostRenderer.sprite = sourceAgent.deadSprite;
                ghostRenderer.color = new Color(0.7f, 0.7f, 0.7f, isBackgroundGhost ? 0.25f : 0.85f);
            }
            else
            {
                if (sourceAgent.aliveSprite != null)
                {
                    ghostRenderer.sprite = sourceAgent.aliveSprite;
                }

                // Check if this frame was human possessed (actionTaken == -1)
                bool isPossessedFrame = snap.actionTaken == -1;

                float alpha = isBackgroundGhost ? 0.25f : 0.85f;

                if (isPossessedFrame)
                {
                    // Match live arena possession color (Cyan)
                    ghostRenderer.color = new Color(0f, 1f, 1f, alpha);
                }
                else
                {
                    // Default organism color
                    Color baseColor = Color.yellow;
                    ghostRenderer.color = new Color(baseColor.r * 0.7f, baseColor.g * 0.7f, baseColor.b * 0.7f, alpha);
                }
            }
        }

        // --- OVERHEAD TEXT FOR ALL GHOSTS (Primary & Background Peers) ---
        if (overheadText != null && sourceAgent != null)
        {
            overheadText.transform.rotation = Quaternion.identity;
            overheadText.transform.position = transform.position + new Vector3(0f, 1.2f, -0.2f);

            int agentId = sourceAgent.agentIndex;

            if (snap.energy > 0f)
            {
                bool isPossessedFrame = snap.actionTaken == -1;

                if (isBackgroundGhost)
                {
                    string tag = isPossessedFrame ? " <color=#00FFFF>(PLAYER)</color>" : "";
                    overheadText.text = $"<color=#B0B0B0><size=75%>#{agentId}{tag}</size>\n<size=60%>{snap.energy:F0} HP</size></color>";
                }
                else
                {
                    string title = isPossessedFrame 
                        ? $"<color=#00FFFF><b>Agent #{agentId} (PLAYER)</b></color>" 
                        : $"<color=#00FFFF><b>Agent #{agentId}</b></color>";
                        
                    string hpColor = snap.energy > 35f ? "#55FF55" : "#FF5555";
                    overheadText.text = $"{title}\n<size=75%>HP: <color={hpColor}>{snap.energy:F0}</color></size>";
                }
            }
        }

        // Disable sensor rays & vision cone for background ghosts, then exit early
        if (isBackgroundGhost)
        {
            if (foodSensorLine != null) foodSensorLine.enabled = false;
            if (poisonSensorLine != null) poisonSensorLine.enabled = false;
            if (preySensorLine != null) preySensorLine.enabled = false;
            if (threatSensorLine != null) threatSensorLine.enabled = false;
            if (visionConeLine != null) visionConeLine.enabled = false;
            return;
        }

        // Primary agents continue below to draw sensor lines...
        if (snap.sensorState != null && snap.sensorState.Length >= 10)
        {
            Vector3 origin = new Vector3(snap.position.x, snap.position.y, -0.5f);
            Vector3 foodDir = new Vector3(snap.sensorState[2], snap.sensorState[3], 0f);
            Vector3 poisonDir = new Vector3(snap.sensorState[4], snap.sensorState[5], 0f);
            Vector3 threatDir = new Vector3(snap.sensorState[6], snap.sensorState[7], 0f);
            Vector3 preyDir = new Vector3(snap.sensorState[8], snap.sensorState[9], 0f);

            UpdateRay(foodSensorLine, origin, foodDir);
            UpdateRay(poisonSensorLine, origin, poisonDir);

            if (preySensorLine != null)
            {
                bool showPrey = snap.hasPreyVisible && preyDir.sqrMagnitude > 0.01f;
                preySensorLine.enabled = showPrey;
                if (showPrey)
                {
                    preySensorLine.positionCount = 2;
                    preySensorLine.SetPosition(0, origin);
                    preySensorLine.SetPosition(
                        1,
                        new Vector3(
                        snap.preyWorldPos.x,
                        snap.preyWorldPos.y,
                        -0.5f));
                }
            }

            if (threatSensorLine != null)
            {
                bool showThreat = snap.hasThreatVisible && threatDir.sqrMagnitude > 0.01f;
                threatSensorLine.enabled = showThreat;
                if (showThreat)
                {
                    threatSensorLine.positionCount = 2;
                    threatSensorLine.SetPosition(0, origin);
                    threatSensorLine.SetPosition(
                        1,
                        new Vector3(
                        snap.threatWorldPos.x,
                        snap.threatWorldPos.y,
                        -0.5f));
                }
            }
        }

        Debug.Log(
            $"Replay Prey Visible={snap.hasPreyVisible} " +
            $"Dir=({snap.sensorState[8]:F2}, {snap.sensorState[9]:F2})");

        Debug.Log(
            $"Replay Threat Visible={snap.hasThreatVisible} " +
            $"Dir=({snap.sensorState[6]:F2}, {snap.sensorState[7]:F2})");
    }

    private void SpawnCombatPopup(Vector2 position, int damage)
    {
        if (sourceAgent != null && sourceAgent.swordClashClip != null)
        {
            AudioSource.PlayClipAtPoint(sourceAgent.swordClashClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, sourceAgent.soundVolume);
        }

        if (floatingTextPrefab == null)
        {
            Debug.LogWarning($"[ReplayAgentGhost] floatingTextPrefab is null on Agent #{sourceAgent?.agentIndex}");
            return;
        }

        Vector3 spawnPos = new Vector3(position.x + Random.Range(-0.2f, 0.2f), position.y + 1.0f, -0.6f);
        GameObject popup = Instantiate(floatingTextPrefab, spawnPos, Quaternion.identity);

        // Keep it unparented in world space so parent scaling doesn't affect it
        popup.transform.SetParent(null);
        popup.transform.position = spawnPos;
        popup.transform.rotation = Quaternion.identity;
        popup.transform.localScale = Vector3.one; // Explicit 1.0 scale

        FloatingCombatText fct = popup.GetComponent<FloatingCombatText>();
        if (fct != null)
        {
            fct.Setup(damage, isCritical: damage >= 20, isDamage: true);
        }
    }

    private void UpdateRay(LineRenderer line, Vector3 origin, Vector3 dir)
    {
        if (line == null) return;
        bool hasTarget = dir.sqrMagnitude > 0.01f;
        line.enabled = hasTarget;
        if (hasTarget)
        {
            line.positionCount = 2;
            line.SetPosition(0, origin);
            line.SetPosition(1, origin + dir.normalized * visionRadius);
        }
    }

    public void SetVisible(bool visible)
    {
        if (overheadText != null) overheadText.gameObject.SetActive(visible);
        if (ghostRenderer != null) ghostRenderer.enabled = visible;

        if (!isBackgroundGhost)
        {
            if (foodSensorLine != null) foodSensorLine.enabled = visible;
            if (poisonSensorLine != null) poisonSensorLine.enabled = visible;
            if (preySensorLine != null) preySensorLine.enabled = visible;
            if (threatSensorLine != null) threatSensorLine.enabled = visible;
            if (visionConeLine != null) visionConeLine.enabled = visible;
        }
    }

    private void DrawVisionArc(int segments = 32)
    {
        if (visionConeLine == null) return;

        visionConeLine.positionCount = segments + 2;

        float halfAngleRad = (visionAngle * 0.5f) * Mathf.Deg2Rad;
        float startAngle = -halfAngleRad;
        float deltaAngle = (visionAngle * Mathf.Deg2Rad) / segments;

        visionConeLine.SetPosition(0, Vector3.zero);

        for (int i = 0; i <= segments; i++)
        {
            float theta = startAngle + (deltaAngle * i);
            float x = visionRadius * Mathf.Cos(theta);
            float y = visionRadius * Mathf.Sin(theta);
            visionConeLine.SetPosition(i + 1, new Vector3(x, y, -0.1f));
        }

        visionConeLine.SetPosition(segments + 1, Vector3.zero);
    }

    private void SetupOverheadText()
    {
        if (overheadText != null) return;

        GameObject textObj = new GameObject("OverheadStats");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = new Vector3(0f, 1.8f, 0f);

        overheadText = textObj.AddComponent<TextMeshPro>();
        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(6f, 3f);

        overheadText.fontSize = 8.5f;
        overheadText.textWrappingMode = TextWrappingModes.NoWrap;
        overheadText.alignment = TextAlignmentOptions.Center;
        overheadText.sortingOrder = 30; // Above all sprites and rays
    }

    public void ResetCombatTracker()
    {
        lastEvaluatedEnergy = -1f;
        lastEvaluatedTime = -1f;
    }
}