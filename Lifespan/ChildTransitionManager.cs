using ModAPI.Core;
using HarmonyLib; // Replaces ModAPI.Reflection
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    /// <summary>
    /// Handles the transition from child to adult, including mesh swapping and stat recalculation.
    /// </summary>
    public class ChildTransitionManager
    {
        private readonly LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly AgeTracker _ageTracker;
        private readonly ModRandomStream _random;
        private IModLogger Log => _log;

        public ChildTransitionManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker, ModRandomStream random)
        {
            _config = config;
            _log = ctx.Log;
            _ageTracker = ageTracker;
            _random = random;
        }

        public void TransitionToAdult(FamilyMember member)
        {
            try
            {
                if (member == null) return;

                int currentStr = member.BaseStats.Strength.Level;
                Log.Debug($"Starting adult transition for {member.firstName}. Current Stats: Str:{currentStr}. (Away: {member.isAway})");

                if (member.isAway)
                {
                    Log.Info($"{member.firstName} is growing up while away! Visuals will update on return.");
                }
                Log.Info($"{member.firstName} has reached adulthood!");

                // 1. Update the m_child flag and mesh ID
                // 1. Update the m_child flag and mesh ID
                Traverse.Create(member).Field("m_child").SetValue(false);
                string newMeshId = member.isMale ? "man" : "woman";
                Traverse.Create(member).Field("m_characterMeshId").SetValue(newMeshId);
                Log.Debug($"Set m_child=false, m_characterMeshId={newMeshId}");

                // 2. Perform mesh swap
                Log.Debug($"Swapping mesh to {newMeshId}.");
                SwapMesh(member, newMeshId);

                // 3. Update core character components (animators, colliders, etc.)
                Log.Debug("Refreshing character components.");
                member.UpdateSpritesAndAnimators();

                // 4. Restore mesh depth (crucial for visibility in Sheltered's 2.5D view)
                // 4. Restore mesh depth (crucial for visibility in Sheltered's 2.5D view)
                Traverse.Create(member).Method("SetMeshDepth", new object[] { 0 }).GetValue(); // 0 corresponds to BaseCharacter.MeshDepth.Normal


                // 6. Increase BaseStat caps to 20 (Adult levels)
                Log.Debug("Increasing stat caps to 20.");
                if (member.BaseStats != null)
                {
                    UpdateStatCap(member.BaseStats.Strength, "strength");
                    UpdateStatCap(member.BaseStats.Dexterity, "dexterity");
                    UpdateStatCap(member.BaseStats.Intelligence, "intelligence");
                    UpdateStatCap(member.BaseStats.Charisma, "charisma");
                    UpdateStatCap(member.BaseStats.Perception, "perception");
                }

                // 6.5 Carry over leftover childhood potential
                var gene = _ageTracker.GetOrGenerateDevelopmentGene(member);
                if (gene != null)
                {
                    int oldPotential = gene.PostAdultPotential;
                    gene.TransitionToAdult();
                    if (gene.PostAdultPotential > oldPotential)
                    {
                        Log.Debug($"Carried over {gene.PostAdultPotential - oldPotential} points to adult potential for {member.firstName}.");
                    }
                }

                // 7. Ensure character has at least TWO adult Strength traits
                // Adults typically have 2 strengths. Without strengths to lose, characters 
                // transition immediately to Catatonic on max trauma. With only 1 strength,
                // they become catatonic after a single trauma event.
                if (member.traits != null)
                {
                    var strengths = member.traits.GetStrengths(false);
                    int strengthsToGrant = 2 - strengths.Count;

                    if (strengthsToGrant > 0)
                    {
                        Log.Debug($"Granting {strengthsToGrant} adult strength(s).");

                        // Build a list of available strengths (excluding any they already have)
                        List<Traits.Strength> available = new List<Traits.Strength>();
                        for (int i = 0; i < 8; i++)
                        {
                            Traits.Strength s = (Traits.Strength)i;
                            if (!strengths.Contains(s))
                                available.Add(s);
                        }

                        // Grant random strengths from available pool
                        for (int i = 0; i < strengthsToGrant && available.Count > 0; i++)
                        {
                            int randomIndex = _random.Range(0, available.Count);
                            Traits.Strength randomStrength = available[randomIndex];
                            member.traits.AddStrength(randomStrength);
                            available.RemoveAt(randomIndex);
                            Log.Info($"{member.firstName} developed adult trait: {randomStrength}");
                        }
                    }
                    else
                    {
                        Log.Debug("Character already has adult strengths. No traits granted.");
                    }
                }

                // 8. Force stat recalculation
                // 8. Force stat recalculation
                Log.Debug("Triggering OnTraitsChanged.");
                Traverse.Create(member).Method("OnTraitsChanged").GetValue();

                // 9. Journal entry
                if (JournalManager.Instance != null)
                {
                    if (JournalManager.Instance != null)
                    {
                        Log.Debug("Inserting Journal entry.");
                        Traverse.Create(JournalManager.Instance).Method("InsertJournalEntry", new object[] { $"{member.firstName} has reached adulthood!", "", false }).GetValue();
                    }

                    // 10. Update SaveTemp.CharacterCustomisations for persistence
                    UpdateSaveTemp(member);

                    // 11. Refresh UI Portrait (Moved to end to ensure data consistency)
                    Log.Debug("Updating avatar sprite.");
                    // 11. Refresh UI Portrait (Moved to end to ensure data consistency)
                    Log.Debug("Updating avatar sprite.");
                    Traverse.Create(member).Method("UpdateAvatarSprite").GetValue();

                    // 12. Force refresh UI height (speech bubble position)
                    RefreshUIHeight(member);

                    if (Log.IsDebugEnabled) Log.Debug($"{member.firstName} transition complete.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to transition {member?.firstName} to adult: {ex.Message}");
                Log.Error($"Stack: {ex.StackTrace}");
            }
        }

        private void RefreshUIHeight(FamilyMember member)
        {
            if (member == null) return;
            
            try
            {
                // Find UI_Character for this member
                UI_Character[] uiChars = GameObject.FindObjectsOfType<UI_Character>();
                if (uiChars == null) return;

                if (Log.IsDebugEnabled) Log.Debug($"Found {uiChars.Length} UI objects. Refreshing height for {member.firstName}.");

                foreach (var ui in uiChars)
                {
                    bool match = false;
                    if (ui.familyMember == member) match = true;
                    else if (ui.baseCharacter == member) match = true;
                    else if (ui.character != null && ui.character.GetComponent<FamilyMember>() == member) match = true;

                    if (match)
                    {
                        // Update m_height from baseCharacter.meshUIHeight
                        float newHeight = member.meshUIHeight;
                        if (Log.IsDebugEnabled) Log.Debug($"Updated height to {newHeight} for {member.firstName}.");
                        
                        // m_height is private field in UI_Character
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
                Log.Debug($"{name} cap -> 20.");
                Traverse.Create(stat).Field("cap").SetValue(20);
            }
        }

        private void SwapMesh(FamilyMember member, string meshId)
        {
            if (CharacterMeshOptions.instance == null)
            {
                Log.Error("CharacterMeshOptions.instance is null! Cannot swap mesh.");
                return;
            }

            // Get current mesh transform and layer
            CharacterMesh currentMesh = Traverse.Create(member).Field("m_mesh").GetValue<CharacterMesh>();
            if (currentMesh == null)
            {
                Log.Error($"CharacterMesh is null on {member.firstName}! Cannot swap.");
                return;
            }

            Transform parent = currentMesh.transform.parent;
            int unityLayer = currentMesh.gameObject.layer;
            string meshLayerName = LayerMask.LayerToName(unityLayer);
            if (string.IsNullOrEmpty(meshLayerName)) meshLayerName = "mesh";

            Log.Debug($"Re-instantiating mesh on layer {meshLayerName}.");

            // Store colors
            Color hair = currentMesh.hairColor;
            Color skin = currentMesh.skinColor;
            Color shirt = currentMesh.shirtColor;
            Color pants = currentMesh.pantsColor;

            // Destroy old
            GameObject.Destroy(currentMesh.gameObject);

            // Create new
            Log.Debug($"Instantiating new {meshId} mesh.");
            CharacterMesh newMesh = CharacterMeshOptions.instance.InstantiateNewCharacterMesh(meshId, parent, meshLayerName, 1.0f);
            if (newMesh != null)
            {
                member.SetCharacterMesh(newMesh);
                
                // Transfer and refresh colors/textures
                newMesh.SetColor(CharacterMesh.ColorCustomization.HairColor, hair);
                newMesh.SetColor(CharacterMesh.ColorCustomization.SkinColor, skin);
                newMesh.SetColor(CharacterMesh.ColorCustomization.ShirtColor, shirt);
                newMesh.SetColor(CharacterMesh.ColorCustomization.PantsColor, pants);
                
                // Randomize textures for the new adult body (since kids have different texture sets)
                newMesh.RandomizeTextures();
                newMesh.RefreshTextures();
                newMesh.RefreshColors();
 
                Log.Info($"Syncing BaseCharacter texture IDs with new mesh for {member.firstName}.");
                // Update the BaseCharacter fields so ObtainInfo/UI can read the correct new IDs (fixing empty portraits)
                var trv = Traverse.Create(member);
                trv.Field("m_headTexture").SetValue(newMesh.headTexture);
                trv.Field("m_torsoTexture").SetValue(newMesh.torsoTexture);
                trv.Field("m_legTexture").SetValue(newMesh.legTexture);
                trv.Field("m_originalHeadTexture").SetValue(newMesh.headTexture);
                trv.Field("m_originalTorsoTexture").SetValue(newMesh.torsoTexture);
                trv.Field("m_originalLegTexture").SetValue(newMesh.legTexture);
 
                Log.Debug($"Mesh swapped to {meshId} for {member.firstName}.");
            }
            else
            {
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
                    
                    // Revert adult status since we failed to become an adult visually
                    Traverse.Create(member).Field("m_child").SetValue(true);
                    Traverse.Create(member).Field("m_characterMeshId").SetValue(fallbackId);
                    Log.Warn($"Reverted {member.firstName} to child state due to mesh failure.");
                }
            }
        }

        private void UpdateSaveTemp(FamilyMember member)
        {
            if (SaveTemp.CharacterCustomisations == null) return;

            Log.Debug($"Syncing SaveTemp record for {member.firstName}.");
            foreach (var custom in SaveTemp.CharacterCustomisations)
            {
                if (custom != null && custom.memberAttributes != null && custom.memberAttributes.m_firstName == member.firstName)
                {
                    custom.isAdult = true;
                    string meshId = member.isMale ? "Man" : "Woman";
                    custom.memberAttributes.m_meshId = meshId;

                    // IMPORTANT: Update the mesh object itself so portraits and UI work correctly
                    if (CharacterMeshOptions.instance != null)
                    {
                        var meshType = CharacterMeshOptions.instance.FindCharacterMesh(meshId);
                        if (meshType != null)
                        {
                            custom.memberAttributes.m_mesh = meshType;
                        }
                    }

                    Log.Debug($"Updated SaveTemp entry for {member.firstName}.");
                    break;
                }
            }
        }
    }
}
