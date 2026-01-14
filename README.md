# Modern Computer Games (COMP 521) - Project Portfolio

## Project Overviews

### 1. First-Person Platformer & Resource Systems
A first-person exploration game that introduces core movement mechanics and inventory management.
* **Key Features:** Custom projectile physics used to bridge a "cavity" area, first-person WASD and mouse-look controls, and a permanent "trail mark" system to track player movement.
* **Technical Focus:** State-based level progression and collision-based object interaction.

### 2. Custom Physics Pinball Simulation
A pinball table that bypasses Unity's built-in physics engine to use a **hand-coded collision and response system**.
* **Key Features:** Implementation of an `ICustomCollider` interface to handle sphere, triangular prism, and cylinder collisions.
* **Technical Focus:** Elastic impulse resolution, restitution-based energy loss/gain, and pairwise ball-to-ball interaction physics.

### 3. Dynamic Pathfinding (Reduced Visibility Graph)
A simulation of mobile agents navigating complex environments with varying terrain costs using a custom $A^*$ implementation.
* **Key Features:** Generation of a **Reduced Visibility Graph (RVG)** based on obstacle reflex vertices and terrain-aware path smoothing.
* **Technical Focus:** Handling variable agent radii and optimizing paths across sub-areas with different movement costs.

### 4. Goal-Oriented Ogre AI (HTN)
A stealth-action scenario featuring enemy AI controlled by a custom **Hierarchical Task Network (HTN)**.
* **Key Features:** Advanced ogre AI with distinct "Idle" (3 types) and "Attack" (2 types) behaviors, plus a real time HTN plan visualizer.
* **Technical Focus:** Total-order forward planning, field-of-view (FOV), and world state management.
