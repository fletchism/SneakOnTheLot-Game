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



## DEC-U010 — Photon Realtime for Multiplayer (Not PUN2, Not Fusion, Not Mirror)

**Decision:** Real-time multiplayer uses Photon Realtime v5.1.9 with manual `Service()` tick. Not PUN2 (deprecated), not Fusion (overkill for this use case), not Mirror (requires dedicated server).
**Rationale:** Photon Realtime is the lightest-weight Photon SDK. No MonoBehaviour network objects, no PhotonView, no scene synchronization — just raw events and player properties. Fits the architectural pattern of a singleton `LotNetworkManager` that owns the `RealtimeClient`.
**Tradeoff:** No high-level abstractions like `PhotonView.IsMine`. All sync logic is manual. Acceptable for the minimal sync surface (position + appearance).

---

## DEC-U011 — Character Appearance Sync via Photon Custom Player Properties

**Decision:** Character appearance data (`CharacterAppearanceData` JSON) is synced via Photon Custom Player Properties under key `"avatar"`. Not via events.
**Rationale:** Player properties persist for the room lifetime and are automatically delivered to late-joining players. Events would require manual re-send logic on join.
**Data format:** `CharacterAppearanceData` serialized as JSON via `JsonUtility`. Contains part slot→name mappings (as `List<PartEntry>` for JsonUtility compatibility — not Dictionary) and 4 body blend floats.
**Tradeoff:** ~1.4KB per player property write. Well within Photon limits. Properties are only updated on customization confirm, not per-frame.

---

## DEC-U012 — Position Sync via Photon Unreliable Events at 10Hz

**Decision:** Local player position/rotation/animation state is sent as `float[6]` via unreliable Photon events (event code 1) at 10Hz. Remote players interpolate between received positions.
**Rationale:** Unreliable events minimize latency (no retransmission). 10Hz is sufficient for the walking-speed movement in this game. Interpolation smooths the gaps.
**Data:** `[posX, posY, posZ, rotationY, moveSpeed, gait]` — enough to position and animate remote characters.
**Tradeoff:** Unreliable means occasional dropped packets. Interpolation masks this. No state history or rollback needed for a non-competitive shared world.

---

## DEC-U013 — SidekickRuntime Wrapped in SidekickCharacterManager Singleton

**Decision:** `SidekickCharacterManager` is a MonoBehaviour singleton that owns the `DatabaseManager` and `SidekickRuntime` instances. All character building goes through `BuildCharacter(CharacterAppearanceData, modelName, existingModel?)`.
**Rationale:** Synty's `SidekickRuntime` is a plain C# class with SQLite dependencies. Wrapping it in a singleton provides lifecycle management, init-state checking (`IsReady`), and a clean API for both local and remote character construction.
**Implementation note:** Despite `PopulateToolData` having an `async Task` signature, the actual implementation is synchronous (`Task.CompletedTask`). Safe to call on main thread.

---

## DEC-U014 — Character Customization Is Preset-Based (Not Per-Part)

**Decision:** The character creator UI uses Synty's preset system: Head / Upper Body / Lower Body preset groups + body shape presets. Students cycle through named presets, not individual parts.
**Rationale:** Per-part customization (38 `CharacterPartType` slots) is overwhelming for a student-facing UI. Presets provide meaningful variety (~28 head, ~12 upper, ~11 lower combinations = ~3,700+ unique looks) with minimal UI complexity. Per-part unlocks can layer on later via the prestige store.
**Tradeoff:** Less granular control. Acceptable for first launch. Prestige store (future) will add per-part upgrades.

---

## DEC-U015 — Character Appearance Persists to Wix via avatarJson on MemberXP

**Decision:** Character appearance JSON is stored on the existing `MemberXP` CMS record as a string field `avatarJson`. Saved via `POST /_functions/avatarSave`, loaded as part of `GET /_functions/memberState`.
**Rationale:** Reuses the existing `MemberXP` record (already queried on session start). No new collection needed. The JSON is ~1.4KB — well within Wix text field limits. Fail-open: save failures don't block gameplay.
**Flow:** On session start → `FetchMemberState()` → if `avatarJson` non-null → auto-apply, skip creator. If null → show character creator. On confirm → `SaveAvatar()` → persist to Wix.
**Tradeoff:** avatarJson is coupled to the MemberXP record. If MemberXP schema changes, avatar data travels with it. Acceptable — they're both per-member career data.

---

## DEC-U016 — LocalCharacterSync Lives in Assembly-CSharp (Not SOTL.Multiplayer)

**Decision:** `LocalCharacterSync` is in `Assets/Scripts/Player/` (Assembly-CSharp), not in the `SOTL.Multiplayer` asmdef.
**Rationale:** It bridges `LotPlayerController` (Assembly-CSharp) and `SidekickCharacterManager`/`LotNetworkManager` (SOTL.Multiplayer asmdef). asmdef assemblies cannot reference Assembly-CSharp, but Assembly-CSharp can reference all autoReferenced asmdefs. Placing it in Assembly-CSharp avoids circular dependency.
**Tradeoff:** One multiplayer-adjacent script lives outside the SOTL.Multiplayer assembly. Documented here to prevent accidental migration.

---

## DEC-U017 — New Animator Gets Grounded State Forced on Swap

**Decision:** When `LocalCharacterSync.RebuildLocal()` swaps the player's Animator, it immediately sets `IsGrounded=true`, `IsStopped=true`, `MoveSpeed=0`, `FallingDuration=0` on the new Animator before handing it to `LotPlayerController`.
**Rationale:** Synty's locomotion state machine defaults to `IsGrounded=false`, which enters the fall blend tree. Without forcing grounded state, every character rebuild produces a T-pose or falling animation for one frame until the controller catches up.
**Tradeoff:** None. This is a one-time initialization, overwritten by the controller's `UpdateAnimator()` on the next frame.

---

## DEC-U018 — RemotePlayerManager Implements IMatchmakingCallbacks for OnJoinedRoom

**Decision:** `RemotePlayerManager` implements both `IInRoomCallbacks` and `IMatchmakingCallbacks`. `ProcessExistingPlayers()` runs inside `OnJoinedRoom()`, not during `TryRegister()`.
**Rationale:** `TryRegister()` fires when connected to master, before room join. At that point the player list is empty. `OnJoinedRoom` is the correct callback for scanning existing room members. `OnPlayerEnteredRoom` only fires for players who join *after* you — it does not fire for players already in the room.
**Tradeoff:** Additional interface to implement (6 stub methods). Minimal code cost for correct behavior.

