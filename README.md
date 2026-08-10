# AAA Economy SLG Unity Starter

Unity C# starter architecture for an economic simulation + SLG game.

## Included

- Pure C# domain layer for resources, companies, factories, warehouses, markets, missions, and AI.
- Unity `ScriptableObject` data definitions for resources, recipes, missions, and AI profiles.
- Market price calculation driven by supply, demand, stock, and physical market absorption.
- Turn-based simulation engine with an explicit planning and resolution pipeline.
- Assembly definitions separating Domain, Application, Data, Presentation, and Editor code.
- Starter resource assets for iron, coal, wood, food, steel, machinery, medicine, and semiconductors.
- Korean-first player-facing names and a Korean localization table asset.
- Reusable order books with allocation-conscious matching for market settlement.
- Shipment routes, technology research, company operating costs, and turn performance metrics.
- A simulation settings asset and EditMode tests for core economic invariants.
- A 30-turn campaign with bankruptcy, capital destruction, economic dominance, and final ranking outcomes.
- A Civilization-scale 80×48 flat world with horizontal globe wrapping, one player base, three AI opponent bases, biomes, and protected company start tiles.
- Two readable mine types: pickaxe-marked industrial mines and coin-marked gold mines.
- A Korean single-player final-result view that resets both simulation state and the map after confirmation.
- An integrated world turn that moves factory inputs and outputs, warehouse stock, trade cash, operating costs, debt, and economic power through real domain objects.

## Open in Unity

1. Open this folder as a Unity project.
2. Use Unity 6.3 LTS `6000.3.20f1`.
3. Open the menu `게임 > 기본 경제 에셋 생성` if you want to regenerate the sample data.
4. Reference `Assets/Game/Scripts/Application/SimulationEngine.cs` as the entry point for the simulation.

## Korean-first UI

Internal IDs and C# identifiers remain in English for maintainability. Player-facing names are Korean by default:

- `Assets/Game/Data/Localization/korean.asset` contains common UI labels.
- Resource, recipe, and mission assets use Korean display names.
- Add `LocalizedTextLabel` to a Unity UI `Text` component and assign the Korean table plus a key such as `ui.market`, `ui.next_day`, or `ui.market_price`.
- `KoreanFormat` formats money, prices, quantities, percentages, and game days for Korean UI.

## Play mode selection

- Entering Play Mode opens a Korean `1인이서 하기` / `여러 명이서 하기` selection screen before the map or mode-specific assets are created.
- The common map and selected mode service are created only after the player chooses a mode.
- Single-player activates the local simulation and AI companies without a server.
- Single-player creates an 80×48 flat world after selection. Use WASD or arrow keys to pan and the mouse wheel to zoom; moving east or west continuously wraps around the world while the polar north/south edges remain bounded.
- Clicking a map tile reports its coordinates, content, and available interaction category in the Korean HUD.
- Multiplayer asks for a server endpoint and runtime access token, then uses the authoritative PvP server.
- Returning to mode selection disconnects multiplayer and prevents both simulation paths from running together.
- See `GAME_MODE_SELECTION_KO.md` for the player flow and setup details.

## Extended MVP systems

- `LogisticsService` advances shipments in batches without a `MonoBehaviour` per vehicle.
- `TechnologyState` tracks prerequisites, research progress, and completed effects.
- `CompanyFinanceSystem` processes wages, maintenance, interest, debt, and bankruptcy.
- `TurnBatchRunner` spreads multi-turn simulation across frames.
- `Assets/Game/Resources/SimulationSettings.asset` controls market volatility, target stock, world generation, events, military logistics, order limits, batching, and operating costs.
- `CampaignVictoryEvaluator` checks the 3x combined-opponent economic dominance rule from turn 15 and ends the campaign at turn 30.
- `WorldEconomyTurnService` resolves production, logistics arrivals, market settlement, company finance, and asset valuation before campaign evaluation.
- `AICompanyTurnService` submits deterministic buy and sell orders from prior market shortages and surpluses while respecting company cash, stock, and warehouse capacity.
- Transport-independent PvP foundations validate authenticated identity, ownership, revision, turn sequence, replayed requests, per-player action points, readiness, deterministic command packages, and SHA-256 checksums.
- `IPvpTransport` and `IPvpMessageCodec` keep NGO, Mirror, Photon, WebSocket, and custom server adapters outside the game rules.
- `PvpAuthoritativeGateway` provides server-authoritative routing, bounded idempotency caching, request-conflict detection, and reconnect snapshots.
- Open Unity Test Runner and run `Game.Tests.EditMode` to verify market, inventory, production, AI, PvP, finance, game-mode, turn, and campaign rules.
- See `PVP_PREPARATION_KO.md` for the authoritative-server protocol and remaining network work.
- See `UNITY_6_3_MIGRATION_KO.md` for the editor migration status and validation procedure.
- See `PERFORMANCE_GUIDE_KO.md` for scale targets and profiling rules.
- See `WORLD_MAP_RULES_KO.md` for the 80×48 flat-world, horizontal wrapping, controls, and PvP coordinate rules.
- See `REALTIME_GAME_CONTINUATION_README_KO.md` for the pause/1–5× realtime handoff state and the copy-ready continuation prompt.

## Architecture

`Game.Domain` contains no Unity API. `Game.Application` orchestrates turn resolution. `Game.Data` adapts ScriptableObjects into domain definitions. `Game.Presentation` is intentionally small and should contain the Unity-facing composition root and views.

## Turn flow

The MVP is player-driven turn-based SLG: plan commands, spend action points, press `턴 종료`, resolve player commands, resolve AI, settle production and markets, then show a report. One turn advances one internal calendar day by default. See `TURN_PIPELINE_KO.md` for the rules and extension points.

## Important rule

Do not set a resource price directly from a mission or UI. Missions should change production nodes, routes, inventory, or factory condition. The market then derives the price from those physical changes.
