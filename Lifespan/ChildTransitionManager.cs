using ModAPI.Core;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    /// <summary>
    /// Handles the transition from child to adult, including gameplay state and visual upgrades.
    /// Gameplay adulthood is applied first and remains authoritative even if visual refresh fails.
    /// </summary>
    public class ChildTransitionManager
    {
        private readonly LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly IGeneticState _geneticState;
        private readonly ModRandomStream _random;
        private IModLogger Log => _log;

        private readonly Dictionary<int, int> _pendingVisualRetries = new Dictionary<int, int>();
        private readonly Dictionary<int, float> _nextVisualRetryAt = new Dictionary<int, float>();
        private const int MaxVisualRetries = 10;
        private const float VisualRetryDelaySeconds = 5f;

        public ChildTransitionManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker, ModRandomStream random)
            : this(ctx, config, ageTracker != null ? ageTracker.State.Genetics : null, random)
        {
        }

        internal ChildTransitionManager(IPluginContext ctx, LifespanConfig config, IGeneticState geneticState, ModRandomStream random)
        {
            _config = config;
            _log = ctx.Log;
            _geneticState = geneticState;
            _random = random;
        }

        public void TransitionToAdult(FamilyMember member)
        {
            if (member == null) return;

            bool wasChild = member.isChild;
            int memberId = member.GetId();

            // No transition work needed if already adult and there is no pending visual follow-up.
            if (!wasChild && !_pendingVisualRetries.ContainsKey(memberId))
            {
                return;
            }

            if (wasChild)
            {
                Log.Debug($"Starting adult transition for {member.firstName}. (Away: {member.isAway})");
                ApplyAdultGameplayState(member);
                JournalEntryWriter.TryInsert($"{member.firstName} has reached adulthood!", Log);
                Log.Info($"{member.firstName} has reached adulthood!");
            }

            if (member.isAway)
            {
                QueueVisualRetry(member, "member is currently away");
                return;
            }

            if (TryApplyAdultVisualState(member))
            {
                ClearVisualRetry(memberId);
                if (Log.IsDebugEnabled) Log.Debug($"{member.firstName} transition visuals complete.");
            }
            else
            {
                QueueVisualRetry(member, "visual refresh failed");
            }
        }

        public void Update()
        {
            if (_pendingVisualRetries.Count == 0) return;
            if (FamilyManager.Instance == null) return;

            float now = Time.time;
            var ids = new List<int>(_pendingVisualRetries.Keys);
            foreach (int id in ids)
            {
                if (_nextVisualRetryAt.TryGetValue(id, out float retryAt) && now < retryAt)
                {
                    continue;
                }

                FamilyMember member = FindMemberById(id);
                if (member == null || member.isDead)
                {
                    ClearVisualRetry(id);
                    continue;
                }

                if (member.isAway)
                {
                    _nextVisualRetryAt[id] = now + VisualRetryDelaySeconds;
                    continue;
                }

                if (TryApplyAdultVisualState(member))
                {
                    ClearVisualRetry(id);
                    continue;
                }

                int attempts = _pendingVisualRetries[id] + 1;
                if (attempts >= MaxVisualRetries)
                {
                    Log.Warn($"Giving up visual retry for {member.firstName} after {attempts} attempts. Gameplay adulthood remains active.");
                    ClearVisualRetry(id);
                    continue;
                }

                _pendingVisualRetries[id] = attempts;
                _nextVisualRetryAt[id] = now + VisualRetryDelaySeconds;
                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"Visual retry {attempts}/{MaxVisualRetries} queued for {member.firstName}.");
                }
            }
        }

        private void ApplyAdultGameplayState(FamilyMember member)
        {
            string newMeshId = member.isMale ? "man" : "woman";

            TrySetMemberField(member, "m_child", false, "clear child gameplay flag");
            TrySetMemberField(member, "m_characterMeshId", newMeshId, "set adult mesh id");

            if (member.BaseStats != null)
            {
                UpdateStatCap(member.BaseStats.Strength, "strength");
                UpdateStatCap(member.BaseStats.Dexterity, "dexterity");
                UpdateStatCap(member.BaseStats.Intelligence, "intelligence");
                UpdateStatCap(member.BaseStats.Charisma, "charisma");
                UpdateStatCap(member.BaseStats.Perception, "perception");
            }

            var gene = GetOrGenerateDevelopmentGene(member);
            if (gene != null)
            {
                int oldPotential = gene.PostAdultPotential;
                gene.TransitionToAdult();
                if (gene.PostAdultPotential > oldPotential && Log.IsDebugEnabled)
                {
                    Log.Debug($"Carried over {gene.PostAdultPotential - oldPotential} points to adult potential for {member.firstName}.");
                }
            }

            if (member.traits != null)
            {
                var strengths = member.traits.GetStrengths(false);
                int strengthsToGrant = 2 - strengths.Count;

                if (strengthsToGrant > 0)
                {
                    List<Traits.Strength> available = new List<Traits.Strength>();
                    for (int i = 0; i < 8; i++)
                    {
                        Traits.Strength s = (Traits.Strength)i;
                        if (!strengths.Contains(s))
                        {
                            available.Add(s);
                        }
                    }

                    for (int i = 0; i < strengthsToGrant && available.Count > 0; i++)
                    {
                        int randomIndex = _random.Range(0, available.Count);
                        Traits.Strength randomStrength = available[randomIndex];
                        member.traits.AddStrength(randomStrength);
                        available.RemoveAt(randomIndex);
                        Log.Info($"{member.firstName} developed adult trait: {randomStrength}");
                    }
                }
            }

            try
            {
                Traverse.Create(member).Method("OnTraitsChanged").GetValue();
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to refresh traits after adult transition for {member.firstName}: {ex.Message}");
            }

            UpdateSaveTemp(member);
        }

        private void TrySetMemberField<T>(FamilyMember member, string fieldName, T value, string operation)
        {
            try
            {
                Traverse.Create(member).Field(fieldName).SetValue(value);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to {operation} for {member.firstName}: {ex.Message}");
            }
        }

        private bool TryApplyAdultVisualState(FamilyMember member)
        {
            if (member == null) return false;
            if (member.isAway) return false;

            string newMeshId = member.isMale ? "man" : "woman";
            bool meshFullyUpdated = false;

            try
            {
                Traverse.Create(member).Field("m_characterMeshId").SetValue(newMeshId);

                meshFullyUpdated = SwapMesh(member, newMeshId);
                member.UpdateSpritesAndAnimators();
                Traverse.Create(member).Method("SetMeshDepth", new object[] { 0 }).GetValue(); // BaseCharacter.MeshDepth.Normal
                Traverse.Create(member).Method("UpdateAvatarSprite").GetValue();
                RefreshUIHeight(member);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed visual adult refresh for {member.firstName}: {ex.Message}");
                return false;
            }

            return meshFullyUpdated;
        }

        private void QueueVisualRetry(FamilyMember member, string reason)
        {
            if (member == null) return;

            int id = member.GetId();
            if (!_pendingVisualRetries.ContainsKey(id))
            {
                _pendingVisualRetries[id] = 0;
            }

            _nextVisualRetryAt[id] = Time.time + VisualRetryDelaySeconds;
            if (Log.IsDebugEnabled)
            {
                Log.Debug($"Queued adult visual retry for {member.firstName}: {reason}");
            }
        }

        private void ClearVisualRetry(int memberId)
        {
            _pendingVisualRetries.Remove(memberId);
            _nextVisualRetryAt.Remove(memberId);
        }

        private FamilyMember FindMemberById(int memberId)
        {
            var members = FamilyManager.Instance?.GetAllFamilyMembers();
            if (members == null) return null;

            foreach (var member in members)
            {
                if (member != null && member.GetId() == memberId)
                {
                    return member;
                }
            }

            return null;
        }

        private void RefreshUIHeight(FamilyMember member)
        {
            if (member == null) return;

            try
            {
                UI_Character[] uiChars = GameObject.FindObjectsOfType<UI_Character>();
                if (uiChars == null) return;

                foreach (var ui in uiChars)
                {
                    bool match = false;
                    if (ui.familyMember == member) match = true;
                    else if (ui.baseCharacter == member) match = true;
                    else if (ui.character != null && ui.character.GetComponent<FamilyMember>() == member) match = true;

                    if (match)
                    {
                        float newHeight = member.meshUIHeight;
                        Traverse.Create(ui).Field("m_height").SetValue(newHeight);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to refresh UI height: {ex.Message}");
            }
        }

        private void UpdateStatCap(BaseStat stat, string name)
        {
            if (stat == null) return;
            int currentCap = stat.LevelCap;
            if (currentCap < 20)
            {
                if (Log.IsDebugEnabled) Log.Debug($"{name} cap -> 20.");
                Traverse.Create(stat).Field("cap").SetValue(20);
            }
        }

        private bool SwapMesh(FamilyMember member, string meshId)
        {
            if (CharacterMeshOptions.instance == null)
            {
                Log.Error("CharacterMeshOptions.instance is null! Cannot swap mesh.");
                return false;
            }

            CharacterMesh currentMesh = Traverse.Create(member).Field("m_mesh").GetValue<CharacterMesh>();
            if (currentMesh == null)
            {
                Log.Error($"CharacterMesh is null on {member.firstName}! Cannot swap.");
                return false;
            }

            Transform parent = currentMesh.transform.parent;
            int unityLayer = currentMesh.gameObject.layer;
            string meshLayerName = LayerMask.LayerToName(unityLayer);
            if (string.IsNullOrEmpty(meshLayerName)) meshLayerName = "mesh";

            Color hair = currentMesh.hairColor;
            Color skin = currentMesh.skinColor;
            Color shirt = currentMesh.shirtColor;
            Color pants = currentMesh.pantsColor;

            GameObject.Destroy(currentMesh.gameObject);

            CharacterMesh newMesh = CharacterMeshOptions.instance.InstantiateNewCharacterMesh(meshId, parent, meshLayerName, 1.0f);
            if (newMesh != null)
            {
                member.SetCharacterMesh(newMesh);

                newMesh.SetColor(CharacterMesh.ColorCustomization.HairColor, hair);
                newMesh.SetColor(CharacterMesh.ColorCustomization.SkinColor, skin);
                newMesh.SetColor(CharacterMesh.ColorCustomization.ShirtColor, shirt);
                newMesh.SetColor(CharacterMesh.ColorCustomization.PantsColor, pants);
                newMesh.RandomizeTextures();
                newMesh.RefreshTextures();
                newMesh.RefreshColors();

                var trv = Traverse.Create(member);
                trv.Field("m_headTexture").SetValue(newMesh.headTexture);
                trv.Field("m_torsoTexture").SetValue(newMesh.torsoTexture);
                trv.Field("m_legTexture").SetValue(newMesh.legTexture);
                trv.Field("m_originalHeadTexture").SetValue(newMesh.headTexture);
                trv.Field("m_originalTorsoTexture").SetValue(newMesh.torsoTexture);
                trv.Field("m_originalLegTexture").SetValue(newMesh.legTexture);

                return true;
            }

            Log.Error($"Failed to instantiate {meshId}! Attempting fallback to child mesh.");
            string fallbackId = member.isMale ? "boy" : "girl";
            CharacterMesh fallbackMesh = CharacterMeshOptions.instance.InstantiateNewCharacterMesh(fallbackId, parent, meshLayerName, 1.0f);
            if (fallbackMesh != null)
            {
                member.SetCharacterMesh(fallbackMesh);
                fallbackMesh.SetColor(CharacterMesh.ColorCustomization.HairColor, hair);
                fallbackMesh.SetColor(CharacterMesh.ColorCustomization.SkinColor, skin);
                fallbackMesh.SetColor(CharacterMesh.ColorCustomization.ShirtColor, shirt);
                fallbackMesh.SetColor(CharacterMesh.ColorCustomization.PantsColor, pants);
                fallbackMesh.RefreshColors();
                Log.Warn($"Using fallback child mesh visuals for {member.firstName}; gameplay adulthood remains active.");
            }

            return false;
        }

        private void UpdateSaveTemp(FamilyMember member)
        {
            if (SaveTemp.CharacterCustomisations == null) return;

            foreach (var custom in SaveTemp.CharacterCustomisations)
            {
                if (custom != null && custom.memberAttributes != null && custom.memberAttributes.m_firstName == member.firstName)
                {
                    custom.isAdult = true;
                    string meshId = member.isMale ? "Man" : "Woman";
                    custom.memberAttributes.m_meshId = meshId;

                    if (CharacterMeshOptions.instance != null)
                    {
                        var meshType = CharacterMeshOptions.instance.FindCharacterMesh(meshId);
                        if (meshType != null)
                        {
                            custom.memberAttributes.m_mesh = meshType;
                        }
                    }

                    break;
                }
            }
        }

        private DevelopmentGene GetOrGenerateDevelopmentGene(FamilyMember member)
        {
            if (member == null || _geneticState == null) return null;

            int id = member.GetId();
            return _geneticState.GetOrGenerateDevelopmentGene(id, _random);
        }
    }
}
