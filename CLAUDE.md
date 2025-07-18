# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TinyClash is a Unity 3D mobile strategy game featuring building placement, map painting mechanics, and unit-based battles. The game is built using Unity 2023.3+ with URP (Universal Render Pipeline) and targets both mobile and PC platforms.

## Core Architecture

### Game Flow Management
- **Bootstrapper.cs**: Runtime initialization system that loads core systems prefab from Resources
- **GameManager.cs**: Central singleton managing game states (InMenu, Matchmaking, WaitingForGame, InGame, Win, Lose, EndGame)
- Game operates with alternating unit spawning between teams and map painting mechanics

### Building System
- **BuildingPlacer.cs**: Touch-based building placement with grid snapping and A* pathfinding integration
- **BuildingData.cs**: ScriptableObject configuration for buildings (prefabs, dimensions, icons)
- **BuildingController.cs**: Individual building component management
- Uses A* Pathfinding Project's GridGraph for placement validation and pathfinding obstacles

### Map Painting System
- **MapPainterManager.cs**: Orchestrates map painting operations using XDPaint
- **MapPainterUnit.cs**: Individual painter units for parallel painting
- **BattleTeam.cs**: Team management for unit spawning and battle coordination
- Uses XDPaint library for real-time terrain painting with customizable brushes

### Unit System
- **SimpleUnit.cs**: Basic unit AI with movement and painting capabilities
- Units use A* pathfinding for navigation and XDPaint for territory marking
- Teams spawn units alternately that move toward opposing territory

## Key Dependencies

### Core Packages
- **A* Pathfinding Project**: Grid-based pathfinding and building placement validation
- **XDPaint**: Real-time painting system for map territory mechanics
- **Odin Inspector (Sirenix)**: Enhanced Unity inspector with attributes like `[TitleGroup]`, `[Button]`
- **PrimeTween**: Animation and tweening system
- **Unity Enhanced Touch**: Mobile touch input handling
- **R3**: Reactive programming framework
- **UniTask**: Async/await support for Unity

### Input System
- Uses Unity's new Input System with Enhanced Touch Support
- Touch gestures: tap to place, long press to remove, drag for building placement
- Custom input actions defined in `BuildingPlacementInputActions.inputactions`

## Development Commands

Unity project - use Unity Editor for building and testing:
- Open project in Unity 2023.3+
- Build settings configured for multiple platforms (PC, Mobile)
- No custom build scripts identified - use standard Unity build process

## Project Structure

```
Assets/_Project/               # Main project assets
├── _Scripts/                 # All game scripts organized by feature
│   ├── Core/                # Bootstrap and GameManager
│   ├── Building/            # Building placement system
│   ├── Map/                 # Map painting mechanics
│   ├── Battle/              # Team and combat logic
│   └── Unit/                # Unit behavior and UI
├── Prefabs/                 # Game objects and UI prefabs
├── ScriptableObjects/       # Building data and configurations
├── Materials/               # Shaders and materials for buildings/map
├── Scenes/                  # Bootstrap scene
└── InputActions/            # Input system configuration
```

## Code Patterns

### Dependency Management
- Uses Resources.Load for core systems initialization
- ScriptableObject pattern for data configuration (BuildingData)
- Singleton pattern for GameManager with proper null checking

### Touch Input Handling
- Enhanced Touch API with gesture recognition (tap, long press, drag)
- Screen-to-world ray casting for 3D interaction
- Touch sensitivity and timing thresholds for different actions

### Grid-Based Systems
- World-to-grid coordinate conversion for building placement
- A* GridGraph integration for pathfinding and validation
- Multi-cell building support with center-based positioning

### Async Operations
- Coroutines for painting animations and feedback
- Timer-based interpolation for smooth map painting
- State machine pattern for game flow management

## Important Notes

- Building placement uses grid validation against A* pathfinding nodes
- Map painting operates on XDPaint's texture system with brush configurations
- Teams are defined by movement direction and spawn boundaries
- All Turkish comments indicate placeholder TODO items for future features
- Mobile-first design with haptic feedback integration
- Uses URP rendering pipeline with custom materials for building validation states