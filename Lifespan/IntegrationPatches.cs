using HarmonyLib;
using ModAPI.Core;
using ModAPI.Util;
using System;
using UnityEngine;

namespace Lifespan
{
    public static class IntegrationPatches
    {
        // ====================================================================
        // 1. NPC Generation Hook
        // Intercepts creation of NpcVisitor objects (Traders, Recruits, Breachers, etc.)
        // and assigns them an age immediately.
        // ====================================================================
        
        public static class NpcVisitManager_CreateNpcVisitor_Patch
        {
            public static void Postfix(NpcVisitor __result, NpcVisitor.NpcType type)
            {
                if (__result == null || LifespanPlugin.Instance == null) return;

                try
                {
                    // Determine context based on NPC type
                    AgeContext context = AgeContext.Unknown;

                    switch (type)
                    {
                        case NpcVisitor.NpcType.Recruit:
                        case NpcVisitor.NpcType.Joiner:
                        case NpcVisitor.NpcType.Family: // Scenario family members
                            context = AgeContext.ShelterRecruit;
                            break;

                        case NpcVisitor.NpcType.Trader:
                            context = AgeContext.NPC_Trader;
                            break;

                        case NpcVisitor.NpcType.Breacher:
                        case NpcVisitor.NpcType.Worm:
                        case NpcVisitor.NpcType.MutantLurker:
                        case NpcVisitor.NpcType.MutantPasserby:
                            context = AgeContext.ExplorerEncounter; // Treat enemies as "encounters"
                            break;

                        case NpcVisitor.NpcType.Passerby:
                        case NpcVisitor.NpcType.KickstarterBacker:
                        case NpcVisitor.NpcType.BinMan:
                            context = AgeContext.NPC_Wanderer;
                            break;
                            
                        default:
                            context = AgeContext.Unknown;
                            break;
                    }

                    // Generate age. This stores it in AgeTracker using the NpcVisitor's ID.
                    int age = LifespanPlugin.Instance.Api.GenerateAgeForNPC(__result, context);
                    
                    if (LifespanLoggerExtensions.VerboseEnabled)
                        MMLog.Write($"[Lifespan] Generated age for {type} {__result.firstName}: {age / 52} years ({context})");
                }
                catch (Exception ex)
                {
                    MMLog.Write($"[Lifespan] Error in CreateNpcVisitor patch: {ex.Message}");
                }
            }
        }

        // ====================================================================
        // 2. Recruitment Hook
        // When an NpcVisitor is adopted, they become a FamilyMember.
        // The NpcVisitor component is destroyed and a FamilyMember component is added.
        // We must transfer the age from the old NpcVisitor ID to the new FamilyMember ID.
        // ====================================================================

        public static class FamilyManager_AdoptNpc_Patch
        {
            // Capture the age of the NPC before it is potentially destroyed/converted
            public static void Prefix(NpcVisitor npc, out int __state)
            {
                __state = 0;
                if (npc != null && LifespanPlugin.Instance != null)
                {
                    // Get the age associated with this NPC visitor
                    __state = LifespanPlugin.Instance.Api.GetCharacterAgeWeeks(npc);
                }
            }

            public static void Postfix(bool __result, NpcVisitor npc, int __state)
            {
                // If adoption failed or we have no age state to transfer, abort
                if (!__result || __state <= 0 || npc == null || LifespanPlugin.Instance == null) return;

                try
                {
                    // The NpcVisitor component is destroyed in AdoptNpc, but the GameObject persists
                    // and now has a FamilyMember component.
                    FamilyMember newMember = npc.gameObject.GetComponent<FamilyMember>();

                                    if (newMember != null)
                                    {
                                        if (__state > 0)
                                        {
                                            LifespanPlugin.Instance.Api.SetCharacterAgeWeeks(newMember, __state);
                                            MMLog.Write($"[Lifespan] Transferred age {__state / 52}y from NPC to new FamilyMember {newMember.firstName}.");
                                        }
                                        else
                                        {
                                            MMLog.Write($"[Lifespan] Warning: Age transfer failed - NPC age state was invalid ({__state}). Initializing fresh.");
                                        }
                                    }
                                    else
                                    {
                                        MMLog.Write("[Lifespan] Warning: AdoptNpc succeeded but could not find FamilyMember component on GameObject.");
                                    }                }
                catch (Exception ex)
                {
                    MMLog.Write($"[Lifespan] Error in AdoptNpc patch: {ex.Message}");
                }
            }
        }
    }
}
