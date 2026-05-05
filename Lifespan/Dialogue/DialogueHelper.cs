using System.Collections.Generic;
using ModAPI.Core;
using HarmonyLib;

namespace Lifespan
{
    /// <summary>
    /// Shared utility for picking dialogue with anti-repetition logic.
    /// Centralizes the "weighted bag" logic used across various managers.
    /// Supports trait-based weighting to give characters distinct personalities.
    /// </summary>
    public class DialogueHelper
    {
        public const float DEFAULT_WEIGHT = 1.0f;
        public const float TRAIT_WEIGHT_BOOST = 1.25f;

        private IDialogueHistoryState _historyState;
        private readonly Dictionary<string, HashSet<int>> _localHistory = new Dictionary<string, HashSet<int>>();
        private Dictionary<string, HashSet<int>> History => _historyState != null ? _historyState.History : _localHistory;
        private readonly ModRandomStream _random;

        public DialogueHelper(ModRandomStream random)
            : this(random, null)
        {
        }

        internal DialogueHelper(ModRandomStream random, IDialogueHistoryState historyState)
        {
            _random = random;
            _historyState = historyState;
        }

        public void SetAgeTracker(AgeTracker tracker)
        {
            _historyState = tracker != null ? tracker.State.Dialogue : null;
        }

        internal void SetHistoryState(IDialogueHistoryState historyState)
        {
            _historyState = historyState;
        }

        /// <summary>
        /// Selects a line from a list of options, avoiding recent repetition.
        /// </summary>
        /// <param name="contextKey">A unique key for the dialogue context (e.g. 'Dementia_Opener') to track history separately.</param>
        /// <param name="options">The list of available dialogue lines.</param>
        /// <param name="speaker">The character speaking, used for trait-based weighting.</param>
        /// <returns>The selected text string.</returns>
        public string PickLine(string contextKey, List<DialogueLine> options, FamilyMember speaker = null)
        {
            if (options == null || options.Count == 0) return "...";

            if (!History.ContainsKey(contextKey))
            {
                History[contextKey] = new HashSet<int>();
            }

            var used = History[contextKey];

            // If all options (by text hash) have been used, reset the bag to allow fresh selection
            if (used.Count >= options.Count)
            {
                used.Clear();
            }

            // Filter out used options to ensure uniqueness until the pool is exhausted
            var available = new List<DialogueLine>();
            foreach (var opt in options)
            {
                if (!used.Contains(opt.Text.GetHashCode()))
                {
                    available.Add(opt);
                }
            }

            if (available.Count == 0) available = options;

            // Perform weighted selection based on speaker traits
            DialogueLine picked = SelectWeighted(available, speaker);
            used.Add(picked.Text.GetHashCode());

            return picked.Text;
        }

        private DialogueLine SelectWeighted(List<DialogueLine> available, FamilyMember speaker)
        {
            if (available.Count == 1) return available[0];

            float totalWeight = 0f;
            var weights = new List<float>();

            foreach (var line in available)
            {
                float weight = DEFAULT_WEIGHT;
                if (speaker != null && !string.IsNullOrEmpty(line.TraitId))
                {
                    if (HasTrait(speaker, line.TraitId))
                    {
                        weight = TRAIT_WEIGHT_BOOST;
                    }
                }
                weights.Add(weight);
                totalWeight += weight;
            }

            float roll = _random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < available.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative) return available[i];
            }

            return available[available.Count - 1];
        }

        private bool HasTrait(FamilyMember member, string traitId)
        {
            if (member == null || member.traits == null) return false;
            
            // Using HarmonyLib's Traverse as it's reliable for private member access and reflection safety in this codebase.
            try 
            {
                var tr = Traverse.Create(member.traits);
                if (tr.Method("HasTrait", traitId).GetValue<bool>()) return true;
                if (tr.Method("HasWeakness", traitId).GetValue<bool>()) return true;
                return false;
            }
            catch 
            {
                return false;
            }
        }

        public void ClearHistory()
        {
            History.Clear();
        }
    }
}
