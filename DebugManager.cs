using ModAPI.Core;
using UnityEngine;
using System;
using System.Reflection;

namespace Lifespan
{
    /// <summary>
    /// Temporary debug helper to advance time for testing purposes.
    /// Press F7 to advance 1 day.
    /// </summary>
    public class DebugManager : IModUpdate
    {
        private readonly IModLogger _log;
        private readonly LifespanConfig _config;

        public DebugManager(IModLogger log, LifespanConfig config)
        {
            _log = log;
            _config = config;
        }

        public void Update()
        {
            if (_config == null || !_config.enableDebugKeys) return;

            if (Input.GetKeyDown(KeyCode.F7))
            {
                _log.Debug("DebugManager: F7 pressed. Advancing game time by 1 day.");
                AdvanceDay();
            }
        }

        private void AdvanceDay()
        {
            // Get private static game_time field
            FieldInfo gameTimeField = typeof(GameTime).GetField("game_time", BindingFlags.NonPublic | BindingFlags.Static);
            if (gameTimeField == null)
            {
                _log.Warn("DebugManager: Could not get static game_time field.");
                return;
            }

            try
            {
                float currentTime = (float)gameTimeField.GetValue(null);
                int currentDay = GameTime.Day;
                
                _log.Debug($"DebugManager: Current Day: {currentDay}, Current Time: {currentTime}");

                // Day resets/advances when game_time hits 21600 (from below)
                // If we are already past 21600, we need to go to 86400 (end of day) then it wraps and hits 21600.
                // Or we can just set it to 21595 to trigger the logic in Update()
                float nearEnd = 21595f; 
                gameTimeField.SetValue(null, nearEnd);
                
                _log.Debug("DebugManager: Set static game_time to 21595. The game should trigger a New Day within the next few frames.");
            }
            catch (Exception ex)
            {
                _log.Error("DebugManager: Failed to advance day: " + ex.Message);
            }
        }
    }
}