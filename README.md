# LifeForm: 2D Emergent Reinforcement Learning Simulation

**LifeForm** is an interactive, real-time artificial life simulation built with Unity and C#. Autonomous agents operate inside a closed arena populated by food, poison, and rival organisms. Powered by Q-learning policy networks and experiential memory buffers, agents independently discover behavioral archetypes—evolving from basic foragers into apex hunters or cautious survivalists through direct environmental feedback and peer observation.

---

## Key Features

- **Emergent Deep Q-Learning (DQN):** Agents train dynamically via real-time experience replay buffers, balancing exploration ($\epsilon$-greedy policies) with exploitation.
- **Dynamic Behavioral Archetypes:** Agents adapt based on survival pressure, developing distinct instincts for foraging, poison avoidance, evasion, and predatory combat.
- **Social Learning & Observation:** Organisms observe peer behaviors (`ObservePeer`), allowing learned survival patterns to spread across the population.
- **Field of View (FOV) Sensory Cones:** Visual sensory arcs project directional fields of view using dynamic vertex generation, scaling cleanly across agent transforms.
- **Manual Possession & Direct Control:** Seamlessly switch between passive observation and active intervention. Take control of any living organism using Gamepad or Keyboard/Mouse.
- **In-Game Observability & Analytics:**
  - Real-time running stats ticker tracking elapsed time, population counts, and resource consumption.
  - End-of-round leaderboards sorting agents by longevity and metabolic metrics.
  - Interactive Agent Brain Inspector to view policy drives, replay memory sizes, and learned food/poison weights.
- **Cross-Platform & Steam Deck Ready:** Engineered for Linux standalone targets (`x86_64`) with native Vulkan rendering and built-in SteamOS controller integration.

---

## Simulation Mechanics

| Element            | Description                                                                                                                                                    |
|:------------------ |:-------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Agents**         | Autonomous entities driven by neural policies. Energy decays over time; eating food replenishes health, while consuming poison drains it.                      |
| **Predation**      | Desperate or predatory agents can attack peers within range, siphoning health from weaker organisms. Dead agents drop out of the physics loop as inert matter. |
| **Sensory System** | Forward-facing directional arcs detect proximity to food, hazards, and other organisms, passing normalized sensor inputs directly to policy evaluators.        |
| **Food & Poison**  | Dynamically spawned resources with configurable counts and optional arena respawn rules.                                                                       |

---

## Controls & Keybindings

| Action                       | Keyboard / Mouse                | Steam Deck / Gamepad       |
|:---------------------------- |:------------------------------- |:-------------------------- |
| **Select / Inspect Agent**   | Left Mouse Click                | Right Trackpad Click / `A` |
| **Possess Living Agent**     | Left Click on Agent             | Aim + Left Click / `A`     |
| **Release Possession**       | Left Click empty space          | Click empty space          |
| **Cycle Agents**             | `Tab`                           | North Button (`Y`)         |
| **Manual Movement**          | `W`, `A`, `S`, `D` / Arrow Keys | Left Joystick / D-Pad      |
| **Stop Simulation / Return** | `Escape`                        | Menu / Start (`☰`)         |

---

## Getting Started

### Prerequisites

- **Unity Engine:** Unity 6 (6000.x) or later
- **Render Pipeline:** Universal Render Pipeline (URP)
- **UI System:** TextMeshPro (TMP) & Unity Input System Package

### Installation & Editor Setup

1. Clone the repository:
   
   ```bash
   git clone [https://github.com/your-username/LifeForm.git](https://github.com/your-username/LifeForm.git)
   ```

2. Open the project in Unity Hub.

3. Open `Assets/Scenes/SampleScene.unity` (or your primary simulation scene).

4. Press **Play** to start the setup menu, configure arena parameters, and run the simulation.

### Building for Steam Deck (SteamOS)

LifeForm targets Linux Standalone natively without requiring Proton translation:

1. In Unity, navigate to **File > Build Settings**.

2. Select **PC, Mac & Linux Standalone** and set:
   
   - **Target Platform:** `Linux`
   
   - **Architecture:** `Intel 64-bit (x86_64)`

3. Under **Player Settings > Other Settings > Rendering**, ensure **Vulkan** is set as the primary Graphics API.

4. Export the build and deploy to the Steam Deck:
   
   ```
   rsync -avz --delete ./Build_Linux/ deck@<steam-deck-ip>:/home/deck/games/simulation/LifeForm/
   ```

5. On the Steam Deck, add `LifeForm.x86_64` as a Non-Steam Game in Desktop Mode, then apply the **Gamepad With Mouse Trackpad** controller template in Gaming Mode.

## Tech Stack & Architecture

- **Engine:** Unity (C#)

- **Physics:** 2D Rigidbody & raycast-based spatial queries

- **Rendering:** Universal Render Pipeline (URP) with Vulkan backends

- **Machine Learning:** Custom on-policy/off-policy reinforcement learning with experience replay

- **UI:** TextMeshPro UGUI with event-driven link parsing and responsive canvas anchors