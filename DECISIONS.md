# DECISIONS

Architecture and design decisions for the SOTL Unity game. Append new entries — do not edit existing ones.

---

## DEC-U001 — "Unity Game" vs "Wix Game" Terminology

**Decision:** This repo is the "Unity game." The browser-based gamification layer on SneakOnTheLot.com is the "Wix game." These terms are canonical across both repos and all documentation.
**Rationale:** Both systems share some state (XP, Prestige) but have entirely different architectures, runtimes, and player experiences. Clear terminology prevents design confusion.
**Tradeoff:** None.

---

## DEC-U002 — Wix Is Stats-Sink Only; Not a Game Server

**Decision:** Wix receives stat writes from the Unity game only: XP, Prestige, Fame, and level — via the existing `xpAward` and `prestigeAward` REST endpoints. Wix has no role in session management, player position, real-time world state, or matchmaking. No new Wix-side architecture is needed to support the Unity game.
**Rationale:** Wix HTTP functions have a 14-second timeout and stateless serverless execution — incompatible with real-time or near-real-time game state. Wix is the right durable store for career stats; it is the wrong system for live world state.
**Implementation:** Unity flushes stats to Wix at meaningful events (pickup collected, session end, configurable interval). Not per-frame.
**Tradeoff:** Career stats on the Wix site lag behind in-game activity by up to the flush interval. Acceptable for a stat display.

---

## DEC-U003 — Real-Time Multiplayer via Dedicated WebSocket Relay (Not Wix, Not Vercel)

**Decision:** The Unity game implements real-time multiplayer — students see other connected players moving in the shared lot simultaneously. This requires a dedicated WebSocket relay server, separate from Wix and Vercel.
**Rationale:** True real-time shared world (seeing other players move) requires persistent bidirectional connections. Neither Wix nor Vercel supports this architecture. A dedicated relay is the only viable path.
**Technology decision pending:** Options under evaluation: Photon Fusion, Mirror Networking + custom relay, Nakama, or a lightweight custom Node.js WebSocket server. Decision will be recorded as DEC-U004 once evaluated.
**Tradeoff:** New infrastructure cost and operational complexity. The relay server is the only new persistent server in the stack — independent of Wix and Vercel.

---

## DEC-U004 — Assembly Structure: SOTL.API / SOTL.Core / SOTL.UI + Assembly-CSharp

**Decision:** Three custom assembly definitions exist: `SOTL.API` (Wix REST client), `SOTL.Core` (game systems), `SOTL.UI` (HUD, stats panel, toggler). Player and NPC scripts live in `Assembly-CSharp` (no asmdef). Cross-assembly references use fully qualified type names via `System.Type.GetType()`.
**Rationale:** Assembly isolation reduces compile times and enforces dependency direction. Player/NPC scripts in Assembly-CSharp avoids circular dependency issues with the engine-facing character controller.
**Tradeoff:** New Unity-sourced XP events (e.g., `unity_pickup`) require the source allowlist update on the Wix side (see Wix DEC-034).

---

## DEC-U005 — Wix Auth Uses Shared Secret (UNITY_API_SECRET) Stored in SOTLConfig.asset

**Decision:** All Unity REST calls to Wix include `Authorization: Bearer <UNITY_API_SECRET>` from `SOTLConfig.asset`, which is gitignored. The secret is set manually per machine.
**Rationale:** No per-member JWT layer exists in Wix HTTP functions. Shared secret is the simplest viable auth for a trusted client. `SOTLConfig.asset` is gitignored to prevent secret leakage.
**Tradeoff:** Rotating the secret requires updating both Wix Secrets Manager and `SOTLConfig.asset` on every machine simultaneously.

---

## DEC-U006 — Input System Only; No Legacy Input

**Decision:** All Unity input uses the new Input System package (`Keyboard.current.xKey.wasPressedThisFrame`, etc.). `Input.GetKeyDown()` and all legacy input calls are prohibited.
**Rationale:** The project targets the Input System package from the start. Mixing both systems causes undefined behavior and package conflicts.
**Tradeoff:** Input System API is more verbose. All new input code must use `Keyboard.current` / `InputAction` patterns.

---

## DEC-U007 — One Custom Unity Menu (SOTL); All Editor Code in LotSceneBuilder.cs

**Decision:** There is exactly one custom Unity menu: "SOTL" in the menu bar with three items: `SOTL/2 - Build Lot Scene`, `SOTL/3 - Create NPC Idle Controller`, `SOTL/Scene/Clean Rebuild (delete + rebuild)`. All editor code lives in `Assets/Scripts/Editor/LotSceneBuilder.cs` only. New menu items are never added without explicit instruction.
**Rationale:** A single editor file and constrained menu prevents editor code sprawl and makes the build sequence deterministic.
**Critical rule:** Before instructing a Clean Rebuild, always verify `LotSceneBuilder.cs` is fully updated to match current scene state. Running Clean Rebuild against a stale builder destroys scene work.

---

## DEC-U008 — Prestige Pickups Flush Immediately to Wix (No 15-Minute Batch)

**Decision:** `PrestigePickup.OnTriggerEnter` calls `PrestigeSyncManager.AddPending()` followed immediately by `PrestigeSyncManager.ForceFlush()`. The 15-minute batch timer remains as a safety net but pickup collection triggers an immediate Wix flush.
**Rationale:** Prestige pickups are meaningful in-game events. Students expect their balance to update promptly. A 15-minute delay would make the Wix stats feel disconnected from gameplay.
**Tradeoff:** One Wix API call per pickup collected. At academic session volumes this is negligible. If pickup density increases significantly, batching can be restored.

---

## DEC-U009 — FX_Pickup_Boost_01 as Ambient World Pickup Visual (Looping Particles)

**Decision:** `PrestigePickup_01` uses `FX_Pickup_Boost_01` (Synty PolygonParticleFX) as its world visual — looping particles (Sparkles, Glow, Beams, Boost sub-systems). No sphere primitive. No bob/spin code. The same prefab is the collection effect (destroyed with the pickup parent on collect).
**Rationale:** The Synty FX prefab is designed as an ambient looping pickup visual. It handles its own motion. A gold sphere primitive was a placeholder.
**Tradeoff:** No separate one-shot burst on collection — the looping effect simply ceases when the parent is destroyed. Acceptable visual result; can be revisited if a more pronounced collection effect is needed.

