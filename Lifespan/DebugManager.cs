using ModAPI.Core;
using ModAPI.Reflection;
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
                LifespanLoggerExtensions.Debug(_log, "[DEBUG] DebugManager: F7 pressed. Advancing game time by 1 day.");
                AdvanceDay();
            }
        }

        private void AdvanceDay()
        {
            GameTime gameTimeInstance = UnityEngine.Object.FindObjectOfType<GameTime>();
            if (gameTimeInstance == null)
            {
                _log.Warn("[DEBUG] DebugManager: GameTime instance not found via FindObjectOfType.");
                return;
            }

            // Get private game_time field
            FieldInfo gameTimeField = typeof(GameTime).GetField("game_time", BindingFlags.NonPublic | BindingFlags.Instance);
            if (gameTimeField == null)
            {
                _log.Warn("[DEBUG] DebugManager: Could not get game_time field.");
                return;
            }

            try
            {
                float currentTime = (float)gameTimeField.GetValue(gameTimeInstance);
                int currentDay = GameTime.Day;
                
                LifespanLoggerExtensions.Debug(_log, $"[DEBUG] DebugManager: Current Day: {currentDay}, Current Time: {currentTime}");

                // Let's assume day ends at 21600
                float nearEnd = 21595f; 
                gameTimeField.SetValue(gameTimeInstance, nearEnd);
                
                LifespanLoggerExtensions.Debug(_log, "[DEBUG] DebugManager: Set game_time to 21595. The game should trigger a New Day within the next few frames.");
            }
            catch (Exception ex)
            {
                _log.Error("[DEBUG] DebugManager: Failed to advance day: " + ex.Message);
            }
        }
    }
}