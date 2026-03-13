using System;
using System.Collections.Generic;
using UnityEngine;

namespace SOTL.Multiplayer
{
    /// <summary>
    /// Serializable character appearance data for Photon Custom Player Properties.
    /// Covers part selections + body blend shapes. Colors deferred to Phase 3.
    ///
    /// Wire format: JSON via JsonUtility, stored under Photon property key "avatar".
    /// Uses string slot names (CharacterPartType.ToString()) to avoid Synty enum
    /// dependency in the data contract.
    /// </summary>
    [Serializable]
    public class CharacterAppearanceData
    {
        /// <summary>SidekickSpecies database ID (e.g. 1 = HumanSpecies_01).</summary>
        public int speciesId = 1;

        /// <summary>Selected parts per slot. Each entry maps a CharacterPartType name to a part name.</summary>
        public List<PartEntry> parts = new List<PartEntry>();

        /// <summary>Body type blend (0–100, maps to SidekickRuntime.BodyTypeBlendValue).</summary>
        public float bodyType = 50f;

        /// <summary>Skinny blend (0–100, maps to SidekickRuntime.BodySizeSkinnyBlendValue).</summary>
        public float skinny;

        /// <summary>Heavy blend (0–100, maps to SidekickRuntime.BodySizeHeavyBlendValue).</summary>
        public float heavy;

        /// <summary>Muscle blend (0–100, maps to SidekickRuntime.MusclesBlendValue).</summary>
        public float muscles = 50f;

        // ── Serialization ────────────────────────────────────────────────

        public string ToJson() => JsonUtility.ToJson(this);

        public static CharacterAppearanceData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<CharacterAppearanceData>(json); }
            catch (Exception e)
            {
                Debug.LogWarning($"[SOTL] Failed to deserialize CharacterAppearanceData: {e.Message}");
                return null;
            }
        }

        // ── Part helpers ─────────────────────────────────────────────────

        /// <summary>Get the part name for a given slot, or null if not set.</summary>
        public string GetPart(string slot)
        {
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].slot == slot) return parts[i].name;
            return null;
        }

        /// <summary>Set (or add) the part name for a given slot.</summary>
        public void SetPart(string slot, string partName)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].slot == slot)
                {
                    parts[i] = new PartEntry { slot = slot, name = partName };
                    return;
                }
            }
            parts.Add(new PartEntry { slot = slot, name = partName });
        }

        // ── Photon property key ──────────────────────────────────────────

        public const string PhotonKey = "avatar";

        // ── Inner types ──────────────────────────────────────────────────

        [Serializable]
        public struct PartEntry
        {
            /// <summary>CharacterPartType name (e.g. "Head", "Torso").</summary>
            public string slot;

            /// <summary>Sidekick part name (e.g. "SK_HumanSpecies_01_Head_01").</summary>
            public string name;
        }
    }
}
