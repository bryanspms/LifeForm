## High-Level Operational Overview

LifeForm is an autonomous multi-agent artificial life simulator running on a Deep Q-Network (DQN) decision cycle. Organisms continuously perceive an 11-dimensional state vector (relative bearings to food, poison, threats, prey, and health ratios), select directional actions using epsilon-greedy exploration, and store transition vectors into an experience replay buffer.

+-------------------------------------------------------------------------+
|                           SIMULATION LOOP                               |
|                                                                         |
|   [Sensors (11-D)] ---> [DQN Policy (11->32->4)] ---> [Action (WASD)]   |
|           |                                              |              |
|           v                                              v              |
|   [State Vector] ---> [Experience Buffer] <--- [Reward / Combat Event]  |
|                             |                                           |
|                             v                                           |
|                   [Behavior Profiling]                                  |
|                 (Forager / Apex Hunter)                                 |
+-------------------------------------------------------------------------+

The runtime workflow consists of three operating states:
1. Live Simulation & Environment: Agents forage, siphon energy from peers, and update neural policies.
2. Player Intervention: Direct manual organism possession to supply training demonstrations into peer replay buffers.
3. Post-Run Analytics & Replay: Inspecting leaderboards, reviewing agent behavioral archetypes, and executing chronological multi-agent ghost replays with floating combat metrics.

---

## Detailed Step-by-Step Operating Instructions

### 1. Launch & Arena Configuration

| Step | Action | Operational Notes |
| :--- | :--- | :--- |
| 1.1 Launch | Run binary (`./LifeForm.x86_64` / `LifeForm.exe`) or enter Play Mode in `Assets/LifeFormScene.unity`. | Verifies initial agent, food, and poison spawn counts. |
| 1.2 Setup | Configure agent population (1–20), food/poison counts (0–100), poison damage chance (0–100%), vision distance (1–20 units), and vision angle (15°–120° / 360°). | Independent toggles also control whether food and poison respawn dynamically upon consumption. |
| 1.3 Start Run | Click Start Simulation. | Initializes environment, activates the live sensory legend, and spawns organisms with custom vision cones. |

---

### 2. Live Simulation & Human Possession

* Monitoring Autonomous Agents:
  * Vision Cones: Dashed green lines outline each organism's field of view. Only resources and peers within this arc trigger sensory vectors.
  * Live Sensory Vectors & Legend: Colored rays project toward perceived targets within the cone:
    * Green (■): Nearest Food
    * Red (■): Nearest Hazard (Poison)
    * Gold (■): Prey Target (Weaker peer)
    * Magenta (■): Threat (Stronger predator peer)
  * Food (Green): Consumed for +30 energy and +10 reward. Spawns floating green +HP popups.
  * Poison (Red/Pink): Inflicts -40 energy and -15 penalty when damage is taken; displays gray "Resist!" when dodged.
  * Predation & Siphoning: When organisms collide, the higher-energy agent siphons up to 15 energy from the weaker peer, triggering sword clash audio and floating damage numbers (+HP / -HP). Dead organisms freeze physics (`rb.simulated = false`) and are ignored by living agents.

* Engaging Human Possession (Teacher Demonstration):
  1. Click directly on an active organism in the arena to possess it (turns Cyan), or cycle living agents using Tab / Gamepad North (Y / Triangle).
  2. Take direct manual control via W, A, S, D or Arrow Keys (Gamepad: Left Stick / D-Pad).
  3. While possessed:
     * Autonomous neural control is bypassed.
     * Movement transitions are tagged as demonstration data (`actionTaken = -1`).
     * Nearby organisms within visual range observe demonstrated movements and store discounted rewards into their own replay buffers.
  4. Click empty space, press Escape, or cycle to another agent to return the organism to autonomous neural control.

---

### 3. Leaderboard, Behavioral Profiles & Replays

1. Accessing the Leaderboard:
   * Displays automatically upon extinction, or manually by clicking "Stop Simulation" or pressing Escape.
   * Agents are ranked by survival longevity, food consumed, poison taken/resisted, damage inflicted/taken, and player imprint percentage.

2. Inspecting Brain Summaries:
   * Click on any `Agent #ID` link to open the Brain Inspector panel.
   * Displays experience counts, player influence %, exploration balance, and learned food/poison Q-value affinities.
   * Behavioral archetypes include:
     * Apex Predator: Delivers repeated siphon attacks against peers.
     * Forager: Strong preference for food paths and low-risk gathering.
     * Cautious Survivor: Demonstrates learned negative Q-value penalties for hazards.
     * Nomadic Pacifist: Long survival times with low encounter rates.
     * Erratic Explorer / Indecisive Wanderer: Broad arena traversal with balanced or evolving weights.

3. Executing Multi-Agent Replays:
   * Selection: Click the `[ ]` checkbox next to one or more agents on the leaderboard to toggle them `[X]`, then click **Replay Selected (N)**.
   * Visual Hierarchy:
     * Primary selected agents render brightly with active dashed vision cones and colored sensory vectors.
     * Non-selected background peers render at faint opacity (25%) so multi-agent interactions retain spatial context without visual clutter.
   * Replay Controls:
     * Use Play / Pause (Space) or drag the timeline scrubber slider.
     * Step frame-by-frame forward or backward using the step buttons.
     * Audio and background music synchronize to playback speed and stop automatically when the replay completes or exits.

---

## Verification & Sanity Checklist

- [ ] Standalone Binaries: Executable bit set on Linux (`chmod +x LifeForm.x86_64`) and launched directly without missing library warnings.
- [ ] Version Control Settings: Unity Version Control (Plastic SCM) window closed or package removed to prevent `InvalidOperationException` popups during Play mode; root `.gitignore` ignores `Library/`, `Temp/`, and `/.plastic/`.
- [ ] Combat VFX Alignment: Floating combat popups spawn at Z = -0.6 with unparented scale, rendering cleanly in front of arena planes and UI overlays.
- [ ] Vision Cone Calibration: Vision angle setting in setup menu accurately drives both live arena cones and replay ghost cones without 90° rotational offsets.