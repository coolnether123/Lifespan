using HarmonyLib;
using ModAPI.Core;
using ModAPI.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    public static class ExpeditionUIPatches
    {
        public static class PartyMapPanel_OnShow_Patch
        {
            public static void Postfix(PartyMapPanel __instance)
            {
                try
                {
                    // Refresh the party list. 
                    // This fixes the bug where deaths during radio sequences leave "ghost" pages in the UI.
                    var parties = ExplorationManager.Instance.GetAllExplorarionParties();
                    Traverse.Create(__instance).Field("m_allParties").SetValue(parties);

                    int count = parties.Count;
                    int currentIndex = Traverse.Create(__instance).Field("m_currentPartyIndex").GetValue<int>();

                    // Clamp index
                    if (currentIndex >= count)
                    {
                        currentIndex = Math.Max(0, count - 1);
                        Traverse.Create(__instance).Field("m_currentPartyIndex").SetValue(currentIndex);
                    }

                    // Sync the Map UI
                    UI_ExpeditionMap mapUI = Traverse.Create(__instance).Field("m_mapUI").GetValue<UI_ExpeditionMap>();
                    if (mapUI != null)
                    {
                        mapUI.shownPartyIndex = currentIndex;
                    }

                    // Refresh the visual elements (labels, health bars, etc.)
                    Traverse.Create(__instance).Method("UpdateUI").GetValue();
                }
                catch (Exception ex)
                {
                   LifespanPlugin.Instance.Log.Error($"Expedition UI refresh error: {ex.Message}");
                }
            }
        }
    }
}
