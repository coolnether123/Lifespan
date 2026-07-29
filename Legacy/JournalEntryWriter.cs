using System;
using HarmonyLib;
using ModAPI.Core;

namespace Lifespan
{
    internal static class JournalEntryWriter
    {
        public static bool TryInsert(string text, IModLogger log = null)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (JournalManager.Instance == null) return false;

            try
            {
                Traverse.Create(JournalManager.Instance)
                    .Method("InsertJournalEntry", new object[] { text, "", false })
                    .GetValue();
                return true;
            }
            catch (Exception ex)
            {
                log?.Error($"Failed to insert journal entry: {ex.Message}");
                return false;
            }
        }
    }
}
