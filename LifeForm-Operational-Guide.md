## High-Level Operational Overview

LifeForm is an autonomous multi-agent artificial life simulator running on a Deep Q-Network (DQN) decision cycle[cite: 4]. Organisms continuously perceive an 11-dimensional state vector (relative bearings to food, poison, threats, prey, and health ratios), select directional actions using epsilon-greedy exploration, and store transition vectors into an experience replay buffer[cite: 4].

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
1. Live Simulation & Environment: Agents forage, siphon energy from peers, and update neural policies[cite: 4].
2. Player Intervention: Direct manual organism possession to supply training demonstrations into peer replay buffers[cite: 4].
3. Post-Run Analytics & Replay: Inspecting leaderboards, reviewing dead-agent behavioral archetypes, and executing chronological ghost replays with floating combat metrics[cite: 1, 4].

---

## Detailed Step-by-Step Operating Instructions

### 1. Launch & Arena Configuration

| Step | Action | Operational Notes |
| :--- | :--- | :--- |
| 1.1 Launch | Run binary (`./LifeForm.x86_64` / `LifeForm.exe`) or enter Play Mode in `Assets/LifeFormScene.unity`. | Verifies initial agent, food, and poison spawn counts[cite: 4]. |
| 1.2 Setup | Use the on-screen configuration controls to tune population size, food respawn rates, and poison density. | Higher agent counts accelerate peer observation and combat opportunities. |
| 1.3 Start Run | Click Start Simulation. | Resets epoch clocks, zeroes the leaderboard, and initializes the target networks. |

---

### 2. Live Simulation & Human Possession

* Monitoring Autonomous Agents:
  * Food (Green): Consumed for +30 energy and +10 reward[cite: 4]. Displays floating green +HP text above the agent.
  * Poison (Red/Purple): Drains -40 energy and -15 penalty unless resisted[cite: 4].
  * Predation & Siphoning: When organisms collide, the higher-energy agent siphons energy from the weaker one[cite: 4]. This triggers the sword clash sound and spawns bold floating -15 combat popups[cite: 1, 4].
  * Sensor Lines: Visual raycasts project toward nearest food, poison, prey, and threat vectors[cite: 2].

* Engaging Human Possession (Teacher Demonstration):
  1. Click directly on an active organism in the arena to possess it[cite: 4].
  2. Take direct manual control via W, A, S, D or Arrow Keys (Gamepad: Left Stick / D-Pad)[cite: 4].
  3. While possessed:
     * Autonomous neural control is bypassed[cite: 4].
     * Movement transitions are tagged as demonstration data[cite: 4].
     * Nearby organisms observe demonstrated movements and store discounted rewards into their own buffers[cite: 4].
  4. Deselect or press escape to return the organism to autonomous neural control.

---

### 3. Leaderboard, Behavioral Profiles & Replays

1. Accessing the Leaderboard:
   * Open the Leaderboard panel during or after the simulation run.
   * Agents are ranked by survival longevity, total food consumed, energy siphoned, and combat victories.

2. Inspecting Brain Summaries & Behavioral Profiles:
   * Select any agent entry to open its Brain Summary.
   * Review classified behavioral archetypes determined by lifetime metrics:
     * Forager: Prioritizes food routes and low-risk gathering[cite: 4].
     * Apex Hunter: Actively routes toward weaker organisms to siphon energy[cite: 4].
     * Survivalist: Balances high poison resistance and hazard avoidance[cite: 4].
     * Explorer / Erratic Wanderer: Broad arena traversal with varied Q-value distributions[cite: 4].

3. Executing a Replay:
   * Highlight one or more combatants on the leaderboard and click Replay Selected.
   * Visual Hierarchy:
     * Primary selected agents display high-contrast, highlighted overhead #ID labels[cite: 1].
     * Background peer agents display muted #ID labels to preserve spatial context[cite: 1].
     * Siphon damage triggers sword clash audio and spawns red damage numbers floating upward at sorting order 50-100[cite: 1, 4].
   * Replay Controls:
     * Use the scrubber slider to scrub chronologically across recorded life frames.
     * Adjust playback rate (0.5x, 1.0x, 2.0x) to observe fast-forward or frame-by-frame emergent interactions.

---

## Verification & Sanity Checklist

- [ ] Standalone Binaries: Executable bit set on Linux (`chmod +x LifeForm.x86_64`) and launched directly without missing library warnings[cite: 4].
- [ ] VCS Isolation: Git commands executed strictly from outside the Unity project folder (`LifeForm-GitHub`), keeping Plastic SCM workspace files (`.plastic/`) intact.
- [ ] Combat VFX Alignment: Floating hit points spawn at Z = -0.5 to -1.0 with sorting order >= 50, rendering above background sprites and replay UI overlays in both arenas[cite: 1].