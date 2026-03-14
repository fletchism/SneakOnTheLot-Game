using System;
using System.Collections.Generic;
using System.Linq;
using Synty.SidekickCharacters.API;
using Synty.SidekickCharacters.Database;
using Synty.SidekickCharacters.Database.DTO;
using Synty.SidekickCharacters.Enums;
using Synty.SidekickCharacters.Utils;
using UnityEngine;

namespace SOTL.Multiplayer
{
    /// <summary>
    /// Singleton bridge between SOTL and Synty SidekickRuntime.
    /// Owns the DatabaseManager + SidekickRuntime instance.
    /// Provides BuildCharacter() for local and remote player construction.
    ///
    /// Attach to a GameObject under [Managers]. Requires nothing in the scene
    /// beyond the Synty Resources folders (Meshes/SK_BaseModel, Materials/M_BaseMaterial).
    /// </summary>
    public class SidekickCharacterManager : MonoBehaviour
    {
        public static SidekickCharacterManager Instance { get; private set; }

        [Header("Animation")]
        [Tooltip("Masculine animator controller (default).")]
        [SerializeField] private RuntimeAnimatorController _animatorController;

        [Tooltip("Feminine animator controller.")]
        [SerializeField] private RuntimeAnimatorController _feminineAnimatorController;

        private DatabaseManager _dbManager;
        private SidekickRuntime _runtime;
        private bool _initialized;

        /// <summary>True after SidekickRuntime and part library are ready.</summary>
        public bool IsReady => _initialized;

        // ── Lifecycle ─────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            Initialize();
        }

        // ── Init ──────────────────────────────────────────────────────────

        void Initialize()
        {
            try
            {
                _dbManager = new DatabaseManager();

                var baseModel = Resources.Load<GameObject>("Meshes/SK_BaseModel");
                var baseMaterial = Resources.Load<Material>("Materials/M_BaseMaterial");

                if (baseModel == null || baseMaterial == null)
                {
                    Debug.LogError("[SOTL Sidekick] Failed to load Synty base model or material from Resources. " +
                                   "Ensure Meshes/SK_BaseModel and Materials/M_BaseMaterial exist.");
                    return;
                }

                _runtime = new SidekickRuntime(baseModel, baseMaterial, _animatorController, _dbManager);

                // PopulateToolData loads part library + preset library.
                // Despite the async Task signature, the actual implementation is synchronous
                // (returns Task.CompletedTask). Safe to call on main thread.
                SidekickRuntime.PopulateToolData(_runtime);

                _initialized = true;
                Debug.Log($"[SOTL Sidekick] Initialized. Parts loaded: {_runtime.PartCount}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SOTL Sidekick] Init failed: {e}");
            }
        }

        // ── Build character from appearance data ──────────────────────────

        /// <summary>
        /// Build (or rebuild) a character GameObject from appearance data.
        /// Returns the built GameObject. If <paramref name="existingModel"/> is provided,
        /// the character is rebuilt in-place (Synty's CreateModelFromParts handles cleanup).
        /// </summary>
        /// <param name="data">Appearance data (parts + blend shapes).</param>
        /// <param name="modelName">Name for the output GameObject.</param>
        /// <param name="existingModel">Optional existing character to rebuild.</param>
        /// <returns>The built character GameObject, or null on failure.</returns>
        public GameObject BuildCharacter(CharacterAppearanceData data, string modelName, GameObject existingModel = null)
        {
            if (!_initialized)
            {
                Debug.LogWarning("[SOTL Sidekick] Cannot build character — not initialized.");
                return null;
            }

            if (data == null || data.parts.Count == 0)
            {
                Debug.LogWarning("[SOTL Sidekick] Cannot build character — no parts in appearance data.");
                return null;
            }

            var partDict = _runtime.MappedPartDictionary;
            var meshes = new List<SkinnedMeshRenderer>();

            foreach (var entry in data.parts)
            {
                if (!Enum.TryParse<CharacterPartType>(entry.slot, out var partType))
                {
                    Debug.LogWarning($"[SOTL Sidekick] Unknown slot '{entry.slot}', skipping.");
                    continue;
                }

                if (!partDict.TryGetValue(partType, out var slotParts))
                {
                    Debug.LogWarning($"[SOTL Sidekick] No parts available for slot {partType}, skipping.");
                    continue;
                }

                if (!slotParts.TryGetValue(entry.name, out var sidekickPart))
                {
                    Debug.LogWarning($"[SOTL Sidekick] Part '{entry.name}' not found in {partType}, skipping.");
                    continue;
                }

                var partModel = sidekickPart.GetPartModel();
                if (partModel == null)
                {
                    Debug.LogWarning($"[SOTL Sidekick] Part model null for '{entry.name}', skipping.");
                    continue;
                }

                var smr = partModel.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null) meshes.Add(smr);
            }

            if (meshes.Count == 0)
            {
                Debug.LogWarning("[SOTL Sidekick] No valid meshes resolved. Character not built.");
                return null;
            }

            // Set blend shape values before building
            _runtime.BodyTypeBlendValue = data.bodyType;
            _runtime.BodySizeSkinnyBlendValue = data.skinny;
            _runtime.BodySizeHeavyBlendValue = data.heavy;
            _runtime.MusclesBlendValue = data.muscles;

            // Build with separate meshes (not combined) for faster rebuilds on sync
            var character = _runtime.CreateCharacter(
                modelName,
                meshes,
                combineMesh: false,
                processBoneMovement: true,
                existingModel: existingModel
            );

            return character;
        }

        // ── Query API for UI ──────────────────────────────────────────────

        /// <summary>
        /// Get available presets for a given part group (Head, UpperBody, LowerBody).
        /// Returns list of (presetName, preset) pairs. Only presets with all parts available.
        /// </summary>
        public List<SidekickPartPreset> GetPresetsForGroup(PartGroup group)
        {
            if (!_initialized) return new List<SidekickPartPreset>();

            var all = SidekickPartPreset.GetAllByGroup(_dbManager, group);
            return all.Where(p => p.HasAllPartsAvailable(_dbManager)).ToList();
        }

        /// <summary>
        /// Get all available body shape presets.
        /// </summary>
        public List<SidekickBodyShapePreset> GetBodyShapePresets()
        {
            if (!_initialized) return new List<SidekickBodyShapePreset>();
            return SidekickBodyShapePreset.GetAll(_dbManager);
        }

        /// <summary>
        /// Resolve a preset into its constituent part entries for CharacterAppearanceData.
        /// </summary>
        public List<CharacterAppearanceData.PartEntry> ResolvePreset(SidekickPartPreset preset)
        {
            if (!_initialized) return new List<CharacterAppearanceData.PartEntry>();

            var entries = new List<CharacterAppearanceData.PartEntry>();
            var rows = SidekickPartPresetRow.GetAllByPreset(_dbManager, preset);

            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.PartName)) continue;

                var typeName = CharacterPartTypeUtils.GetTypeNameFromShortcode(row.PartType);
                entries.Add(new CharacterAppearanceData.PartEntry
                {
                    slot = typeName,
                    name = row.PartName
                });
            }

            return entries;
        }

        /// <summary>Exposes the DatabaseManager for advanced queries (presets, colors, etc).</summary>
        public DatabaseManager DB => _dbManager;

        /// <summary>Get the appropriate animator controller for gender.</summary>
        public RuntimeAnimatorController GetAnimatorController(bool isFeminine)
            => isFeminine && _feminineAnimatorController != null ? _feminineAnimatorController : _animatorController;
    }
}
