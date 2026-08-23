using HarmonyLib;
using ModAPI.Core;
using ModAPI.Spine;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace Lifespan
{
    public static class ChildCapabilityPatches
    {
        public static ChildDevelopmentManager Manager;
        private static IModLogger Log;

        public static void Initialize(IPluginContext ctx, ChildDevelopmentManager manager)
        {
            Manager = manager;
            Log = ctx.Log;
        }

        // ====================================================================
        // MOVEMENT RESTRICTIONS
        // ====================================================================

        [HarmonyPatch(typeof(BaseCharacter), "GetWalkSpeed")]
        public static class WalkSpeedPatch
        {
            public static void Postfix(BaseCharacter __instance, ref float __result)
            {
                if (Manager == null || __instance == null) return;
                
                if (__instance is FamilyMember member)
                {
                    if (!Manager.CanMove(member))
                    {
                        // Newborns move very slowly instead of being hard-immobile to reduce soft-lock risk.
                        float slowSpeed = __result * 0.25f;
                        __result = Mathf.Max(0.2f, slowSpeed);
                    }
                    else
                    {
                         ChildStage stage = Manager.GetStage(member);
                         if (stage == ChildStage.Child)
                         {
                             // Mobile children retain the vanilla speed.
                         }
                    }
                }
            }
        }

        // ====================================================================
        // JOB RESTRICTIONS
        // ====================================================================

        [HarmonyPatch(typeof(JobQueue), "AddJob")]
        public static class JobQueuePatch
        {
            private static bool IsSystemMovementJob(Job job, string jobType)
            {
                if (job == null) return false;

                // Never block engine-driven movement/state-transition jobs.
                if (jobType == "Job_GoToLocation" ||
                    jobType == "Job_LeaveShelter" ||
                    jobType == "Job_TakeStasisHazmat")
                    return true;

                // Base Job type is used for some internal transitions (e.g. hazmat return).
                if (jobType == "Job")
                {
                    string type = job.type ?? string.Empty;
                    if (type == "go_to_location" || type == "return_hazmat_suit")
                        return true;
                }

                return false;
            }

            public static bool Prefix(JobQueue __instance, Job new_job)
            {
                if (Manager == null || new_job == null || new_job.character == null) return true;

                if (new_job.character is FamilyMember member)
                {
                    string jobType = new_job.GetJobType();

                    // Always permit system movement jobs to avoid soft-locking expedition leave/return states.
                    if (IsSystemMovementJob(new_job, jobType))
                        return true;

                    // Never allow non-system jobs to be inserted during expedition departure transit.
                    // This closes the race between FinaliseExpedition and BeginExploring.
                    if (IsMemberDeparting(member))
                        return false;

                    // Allow critical self-preservation jobs regardless (Eating, Sleeping)
                    if (jobType == "Job_EatFood" || jobType == "Job_Sleep" || jobType == "feed_job") 
                    {
                        // Newborns cannot feed themselves
                        if (Manager.NeedsFeeding(member) && jobType == "Job_EatFood")
                        {
                            return false; // Prevent self-feeding for newborns
                        }
                        return true; 
                    }

                    if (!Manager.CanDoJobs(member))
                    {
                        // Log.Debug($"Blocked job {jobType} for child {member.firstName}.");
                        return false; // Prevent assignment
                    }
                }
                return true;
            }

            private static bool IsMemberDeparting(FamilyMember member)
            {
                if (member == null) return false;

                // Once away, this transit guard is no longer relevant.
                if (member.isAway) return false;

                return ExpeditionStateHelper.HasDepartureJobQueued(member);
            }
        }

        // ====================================================================
        // EXPEDITION RESTRICTIONS
        // ====================================================================

        // ====================================================================
        // EXPEDITION ELIGIBILITY FILTERING
        // ====================================================================

        [HarmonyPatch(typeof(ExpeditionMainPanelNew), "OnShow")]
        public static class ExpeditionOnShowPatch
        {
            private static readonly FieldInfo EligiblePeopleField = typeof(ExpeditionMainPanelNew).GetField("m_eligiblePeople", BindingFlags.NonPublic | BindingFlags.Instance);

            public static void Postfix(ExpeditionMainPanelNew __instance)
            {
                if (Manager == null || __instance == null) return;
                if (EligiblePeopleField == null) return;

                List<FamilyMember> eligible = (List<FamilyMember>)EligiblePeopleField.GetValue(__instance);
                if (eligible == null) return;

                // Keep accompanied minors visible. ExpeditionUpdatePatch validates the selected escort.
                eligible.RemoveAll(m => !Manager.CanGoOnExpedition(m, true));

                Log.Debug($"[Lifespan] Filtered expedition list. Remaining: {eligible.Count}");
            }
        }

        // ====================================================================
        // MANUAL INTERACTION: FEED CHILD
        // ====================================================================

        [HarmonyPatch(typeof(InteractionManager), "Update_Standard")]
        public static class ManualFeedInteractionPatch
        {
            public static void Postfix(InteractionManager __instance)
            {
                if (Manager == null) return;

                if (PlatformInput.GetButtonUp(PlatformInput.InputButton.Interact))
                {
                    FamilyMember hovered = __instance.hoveredMember;
                    FamilyMember selected = __instance.GetSelectedFamilyMember();

                    if (hovered != null && selected != null && hovered != selected)
                    {
                        if (Manager.NeedsFeeding(hovered))
                        {
                            if (Manager.GetStage(selected) >= ChildStage.Teen)
                            {
                                ShowFeedMenu(__instance, selected, hovered);
                            }
                        }
                    }
                }
            }

            private static void ShowFeedMenu(InteractionManager im, FamilyMember feeder, FamilyMember child)
            {
                ContextMenuPanel menu = im.GetInteractionMenu();
                if (menu == null || child.stats == null) return;

                List<string> options = new List<string>();
                Dictionary<string, ChildCareNeedDefinition> optionNeeds = new Dictionary<string, ChildCareNeedDefinition>();

                foreach (ChildCareNeedDefinition need in ChildCareNeeds.GetManualMenuNeeds())
                {
                    if (ChildCareNeedStatus.IsNeeded(child, need))
                    {
                        options.Add(need.MenuLabel);
                        optionNeeds[need.MenuLabel] = need;
                    }
                }

                if (options.Count == 0) return;
                
                Camera worldCam = Traverse.Create(im).Field("m_WorldCamera").GetValue<Camera>();
                Camera uiCam = Traverse.Create(im).Field("m_UICamera").GetValue<Camera>();

                Vector2 pos = child.transform.position;
                Vector3 screenPos = uiCam.ViewportToWorldPoint(worldCam.WorldToViewportPoint(pos));

                // An empty localization prefix preserves the labels supplied above.
                menu.ShowContextMenu((Vector2)screenPos, "", options, (choice) => 
                {
                    if (feeder.job_queue != null)
                    {
                        ChildCareNeedDefinition selectedNeed;
                        if (!optionNeeds.TryGetValue(choice, out selectedNeed))
                        {
                            return;
                        }

                        Job job = ChildCareJobFactory.Create(selectedNeed, feeder, child);

                        if (job != null)
                        {
                            feeder.AddPlayerJob(job);
                            Log.Info($"[Interaction] Player ordered {feeder.firstName} to {choice} {child.firstName}.");
                        }
                    }
                });
            }
        }

        [HarmonyPatch(typeof(ExpeditionMainPanelNew), "Update")]
        public static class ExpeditionUpdatePatch
        {
            private static readonly FieldInfo IsReadyToGoField = typeof(ExpeditionMainPanelNew).GetField("m_isReadyToGo", BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo MapScreenLegendField = typeof(ExpeditionMainPanelNew).GetField("m_mapScreenLegend", BindingFlags.NonPublic | BindingFlags.Instance);

            public static void Postfix(ExpeditionMainPanelNew __instance)
            {
                if (Manager == null || __instance == null) return;
                if (!__instance.MapScreen.activeInHierarchy && !__instance.PartySetup.activeInHierarchy) return;

                int p1Idx = __instance.currentPerson1Index;
                int p2Idx = __instance.currentPerson2Index;

                if (p1Idx == -1 && p2Idx == -1) return; // Empty party

                var eligible = __instance.eligiblePeople;
                FamilyMember m1 = (p1Idx != -1 && p1Idx < eligible.Count) ? eligible[p1Idx] : null;
                FamilyMember m2 = (p2Idx != -1 && p2Idx < eligible.Count) ? eligible[p2Idx] : null;
                
                bool valid = IsPartyValid(m1, m2);

                if (!valid)
                {
                     if (IsReadyToGoField != null) IsReadyToGoField.SetValue(__instance, false);
                     if (__instance.mapScreenConfirmButton != null) __instance.mapScreenConfirmButton.SetEnabled(false);
                     var legend = MapScreenLegendField != null ? MapScreenLegendField.GetValue(__instance) as LegendContainer : null;
                     if (legend != null) legend.SetButtonEnabled(LegendContainer.ButtonEnum.XButton, false);
                }
            }

            private static bool IsPartyValid(FamilyMember m1, FamilyMember m2)
            {
                 if (m1 != null)
                 {
                     // If m1 needs a chaperone, m2 must be capable of going solo (13+).
                     bool hasCapableEscort = (m2 != null && Manager.IsSoloExpeditionCapable(m2)); 
                     if (!Manager.CanGoOnExpedition(m1, hasCapableEscort)) return false;
                 }
                 if (m2 != null)
                 {
                     bool hasCapableEscort = (m1 != null && Manager.IsSoloExpeditionCapable(m1));
                     if (!Manager.CanGoOnExpedition(m2, hasCapableEscort)) return false;
                 }
                 return true;
            }
        }
        
        // ====================================================================
        // FLOOR SLEEPING / FATIGUE
        // ====================================================================

        [HarmonyPatch(typeof(BehaviourStat), "Update")]
        public static class FatigueUpdatePatch
        {
            public static void Prefix(BehaviourStat __instance, ref float stat_change)
            {
            }
        }
        
        [HarmonyPatch(typeof(BehaviourStats), "UpdateStats")]
        public static class StatsUpdatePatch
        {
            public static void Postfix(BehaviourStats __instance)
            {
                if (Manager == null || __instance == null) return;
                
                FamilyMember member = Traverse.Create(__instance).Field("m_member").GetValue<FamilyMember>();
                if (member == null) return;

                if (Manager.NeedsFeeding(member))
                {
                    if (__instance.fatigue.Value >= 90f)
                    {
                        __instance.fatigue.Modify(-10f * Time.deltaTime); 
                    }
                    else if (__instance.fatigue.Value > 0f && __instance.fatigue.Value < 90f)
                    {
                    }
                }
            }
        }
    }
}
