# SYNDICATES & EMPIRES

Unity C# architecture for a real-time economy and strategy game.

## Play in browser

- Submission build: <https://mymh121-spec.github.io/AAA_EconomySLG_Starter/>
- No installation or login is required. A desktop Chrome, Edge, Firefox, or Safari browser is recommended.

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
- Real-time map gameplay with a Korean action panel, player and AI units, horizontally wrapped land pathfinding, mine capture, ownership markers, and daily mine production.
- Single-player neutral-castle occupation with garrison tracking, faction flags, selectable outpost roles, and defended-siege state transitions.
- Distinct military-site rules: headquarters/castles recruit from recovering local manpower pools, while mines allow one guard and never recruit locally.
- Release-MVP sea travel: friendly coastal castles act as simple ports with automatic embarkation and disembarkation; fleets, naval combat, and amphibious assaults remain optional extensions.
- A disabled-by-default HIVE platform extension slot keeps future authentication, achievements, cloud-save, analytics, and notification integrations outside the playable core.

## Open in Unity

1. Open this folder as a Unity project.
2. Use Unity 6.3 LTS `6000.3.21f1`.
3. Open the menu `게임 > 기본 경제 에셋 생성` if you want to regenerate the sample data.
4. Reference `Assets/Game/Scripts/Application/SimulationEngine.cs` as the entry point for the simulation.

## Windows build and run

- Use Unity menu `게임 > Windows EXE 빌드` to create the standalone game.
- When a `D:` drive is available, the default output is `D:\SyndicatesAndEmpires\Builds\Windows\SyndicatesAndEmpires.exe`.
- Set `SYNDICATES_AND_EMPIRES_BUILD_ROOT` before launching Unity to choose another build root.
- Run `RUN_WINDOWS_EXE.cmd`; it checks the D: build first and falls back to the project-local `Builds` folder.

## SYNDICATES & EMPIRES WebGL build

- Install Unity's Web Build Support module for `6000.3.21f1`.
- Use Unity menu `게임 > SYNDICATES & EMPIRES WebGL 제출 빌드`, or run Unity in batch mode with `-buildTarget WebGL -executeMethod Game.Editor.StandaloneBuild.BuildWebGlSubmission`.
- The command writes a GitHub Pages-ready build to `docs/`, including `.nojekyll` and a responsive Korean loading page.
- See `SUBMISSION_TRACK1_KO.md` for the final title, short description, controls, and Codex collaboration notes.

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
- Single-player creates an 80×48 flat world with visible faction castles. Use WASD/arrow keys or drag with the middle mouse button to pan, use the mouse wheel to zoom, press `L` to return to the player faction center, and press `Space` to pause or resume; east/west wraps while north/south remains bounded.
- A new single-player game starts with 500,000 won and one player unit already selected at headquarters.
- Map play is real-time: a unit has 10 stamina, movement costs 1 stamina, and 1 stamina regenerates every six in-game hours. Speed buttons affect the clock, movement, capture, AI, and stamina recovery together.
- Units use the existing six military archetypes: swordsman, spearman, maceman, archer, slinger, and cavalry. The headquarters panel lets the player cycle the unit type before recruitment.
- Left-clicking selects a tile. Right-clicking opens context actions for troop inspection, missions, movement, mining, and capture. Speed is selected with the on-screen 1–4× buttons, while pause/resume also supports `Space`.
- Multiplayer supports direct authoritative-server connection plus an optional HIVE individual-matchmaking path. HIVE finds players; the C# server still resolves the game.
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
- Run `Game.Tests.PlayMode` to verify the real menu lifecycle, initial unit selection, map movement, 360-day campaign completion, and final-result UI.
- See `PVP_PREPARATION_KO.md` for the authoritative-server protocol and remaining network work.
- See `HIVE_CONNECTION_KO.md` for the connection-first HIVE adapter, installation, console setup, and current limitations.
- See `UNITY_6_3_MIGRATION_KO.md` for the editor migration status and validation procedure.
- See `PERFORMANCE_GUIDE_KO.md` for scale targets and profiling rules.
- See `WORLD_MAP_RULES_KO.md` for the 80×48 flat-world, horizontal wrapping, controls, and PvP coordinate rules.
- See `CASTLE_CONTROL_RULES_KO.md` for neutral-castle capture, garrisons, roles, siege transitions, and the single-player-to-server boundary.
- See `GARRISON_RECRUITMENT_RULES_KO.md` for headquarters/castle capacities, local recruitment pools, and mine-guard rules.
- See `LAND_SEA_MOVEMENT_KO.md` for port-based embarkation, sea transport, landing, and authoritative-server rules.
- See `GAME_SCOPE_REVIEW_KO.md` for what to add next, what to defer, and the recommended mine-density reduction.
- See `REALTIME_GAME_CONTINUATION_README_KO.md` for the realtime handoff state and the copy-ready continuation prompt. The current release supports pause and 1–4× speed.
- See `ECONOMIC_OPERATION_SYSTEM_KO.md` for the 12-month calendar and multi-approach economic operation rules.

## Architecture

`Game.Domain` contains no Unity API. `Game.Application` orchestrates turn resolution. `Game.Data` adapts ScriptableObjects into domain definitions. `Game.Presentation` is intentionally small and should contain the Unity-facing composition root and views.

## Real-time flow

The playable map runs continuously. Player and AI map actions use regenerating per-unit stamina rather than daily action points. Production, markets, finance, events, and victory checks are resolved automatically at each in-game midnight, preserving deterministic daily settlement without requiring a turn-end button.

## Important rule

Do not set a resource price directly from a mission or UI. Missions should change production nodes, routes, inventory, or factory condition. The market then derives the price from those physical changes.
