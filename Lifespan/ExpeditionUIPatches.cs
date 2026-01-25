using HarmonyLib;
using ModAPI.Core;
using ModAPI.Reflection;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    public static class ExpeditionUIPatches
    {
        [HarmonyPatch(typeof(PartyMapPanel), "OnResume")]
        public static class PartyMapPanel_OnResume_Patch
        {
            public static void Postfix(PartyMapPanel __instance)
            {
                try
                {
                    // Refresh the party list. 
                    // This fixes the bug where deaths during radio sequences leave "ghost" pages in the UI.
                    var parties = ExplorationManager.Instance.GetAllExplorarionParties();
                    ReflectionHelper.SetField(__instance, "m_allParties", parties);

                    int count = parties.Count;
                    int currentIndex = ReflectionHelper.GetField<int>(__instance, "m_currentPartyIndex");

                    // Clamp index
                    if (currentIndex >= count)
                    {
                        currentIndex = Math.Max(0, count - 1);
                        ReflectionHelper.SetField(__instance, "m_currentPartyIndex", currentIndex);
                    }

                    // Sync the Map UI
                    UI_ExpeditionMap mapUI = ReflectionHelper.GetField<UI_ExpeditionMap>(__instance, "m_mapUI");
                    if (mapUI != null)
                    {
                        mapUI.shownPartyIndex = currentIndex;
                    }

                    // Refresh the visual elements (labels, health bars, etc.)
                    ReflectionHelper.InvokeMethod(__instance, "UpdateUI");
                    
                    // MMLog.Write($"[Lifespan] Refreshed Expedition Panel on resume. New count: {count}, Current Index: {currentIndex}");
                }
                catch (Exception ex)
                {
                   MMLog.Write($"[Lifespan] Expedition UI refresh error: {ex.Message}");
                }
            }
        }
    }
}
