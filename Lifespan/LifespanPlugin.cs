using System;
using System.Collections.Generic;
using ModAPI;
using ModAPI.Core;
using ModAPI.Util;
using ModAPI.Saves;
using ModAPI.Events;
using UnityEngine;
using HarmonyLib;
using ModAPI.Spine; // Required for settings UI support

namespace Lifespan
{
    /// <summary>
    /// Main plugin entry point for the Lifespan mod.
    /// Handles initialization, life cycle events, and settings integration.
    /// </summary>
    public class LifespanPlugin : ModManagerBase, IModPlugin, IModUpdate, IModShutdown, ISettingsProvider
    {
        private LifespanConfig _config;
        private AgeTracker _ageTracker;
        private MilestoneManager _milestoneManager; 
        private ChildTransitionManager _childManager;
        private ElderIllnessManager _illnessManager;
        private DeathManager _deathManager;
        private DevelopmentGeneManager _devGeneManager;
        private LifespanAPIImpl _api;
        private Harmony _harmony;
        private DebugManager _debugManager;
        private DialogueScheduler _dialogueScheduler;
        private ChildDevelopmentManager _childDevManager;
        private NurseJobGiver _nurseJobGiver;

        public ILifespanAPI Api => _api;
        
        // Hair Greying Transition State - using a Context object to avoid reflection every frame
        private class HairTransitionContext
        {
            public FamilyMember Member;
            public CharacterMesh CachedMesh;
            public Color TargetColor;
            public Color CurrentColor;
            public float StartTime;
        }
        
        public static LifespanPlugin Instance { get; private set; }
        
        private List<HairTransitionContext> _activeTransitions = new List<HairTransitionContext>();
        private Dictionary<int, CharacterMesh> _meshCache = new Dictionary<int, CharacterMesh>();

        public void ResetAllState()
        {
            if (Log.IsDebugEnabled) Log.Debug("Resetting all mod state for fresh session.");
            
            _activeTransitions?.Clear();
            _meshCache?.Clear();
            TooltipCache.Clear();
            _dialogueScheduler?.Clear();
            _deathManager?.Reset();
        }

        public override void Initialize(IPluginContext ctx)
        {
            try
            {
                base.Initialize(ctx); // REQUIRED for v1.2 attribute binding
                Instance = this;
                
                // 1. Setup Configuration & Core components (early for save system registration)
                if (_config == null) _config = new LifespanConfig(); 
                
                _dialogueScheduler = new DialogueScheduler(Log);
                
                // Use local 'ctx' to ensure we don't hit property null refs
                _ageTracker = new AgeTracker(ctx, _config);
                _childManager = new ChildTransitionManager(ctx, _config, _ageTracker);
                _illnessManager = new ElderIllnessManager(ctx, _config, _ageTracker);
                _milestoneManager = new MilestoneManager(ctx, _config, _ageTracker); 
                _deathManager = new DeathManager(ctx, _config, _ageTracker);
                _devGeneManager = new DevelopmentGeneManager(ctx, _config, _ageTracker);
                
                _childDevManager = new ChildDevelopmentManager(ctx, _config, _ageTracker);
                _nurseJobGiver = new NurseJobGiver(ctx, _childDevManager);
                
                _debugManager = new DebugManager(Log, _config);

                ResetAllState();
                Log.Debug("Initialize() complete.");
            }
            catch (Exception ex)
            {
                // Fallback logging if standard Log fails (using Unity's Debug)
                UnityEngine.Debug.LogError($"[Lifespan] FATAL ERROR during Initialize: {ex.Message}\n{ex.StackTrace}");
                if (Log != null) Log.Error($"[Lifespan] FATAL ERROR during Initialize: {ex}");
                throw; // Rethrow so loader knows
            }
        }

        public void Start(IPluginContext ctx)
        {
            Log.Debug("Start() called.");

            // 2. Wire managers
            _illnessManager.SetDeathManager(_deathManager);
            _illnessManager.SetScheduler(_dialogueScheduler);
            _milestoneManager.SetScheduler(_dialogueScheduler);
            _deathManager.SetScheduler(_dialogueScheduler);
            _devGeneManager.SetScheduler(_dialogueScheduler);
            _devGeneManager.SetMilestoneManager(_milestoneManager);

            // 3. Register API
            Log.Debug("Registering ILifespanAPI...");
            _api = new LifespanAPIImpl(Context, _config, _ageTracker, _illnessManager, _devGeneManager, _childDevManager);
            ModAPIRegistry.RegisterAPI<ILifespanAPI>("com.lifespan.api", _api, Context.Mod.Id);

            // 4. Initialize Harmony Patches
            Log.Debug("Applying Harmony patches...");
            _harmony = new Harmony("com.lifespan.patches");
            AgingPatches.Tracker = _ageTracker;
            AgingPatches.IllnessManager = _illnessManager;
            AgingPatches.DeathManager = _deathManager;
            AgingPatches.OnNewWeekCallback = OnNewWeek;

            // Try automatic patching for public methods first
            try
            {
                _harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
                Log.Info("Automatic Harmony patches applied successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"Error in automatic patching: {ex.Message}\n{ex.StackTrace}");
            }

            // Initialize Child Capability Patches
            if (_config.enableChildDevelopment)
            {
                Log.Debug("Initializing Child Capability Patches...");
                ChildCapabilityPatches.Initialize(Context, _childDevManager);
            }
        
            // Manually patch all non-public methods
            ApplyManualPatches();
        
            // 5. Subscribe to game events via ModAPI
            Log.Debug("Subscribing to GameEvents...");
            ModAPI.Events.GameEvents.OnAfterLoad += OnGameLoad;
            ModAPI.Events.GameEvents.OnBeforeSave += OnGameSave;
            
            Log.Info("Mod successfully started (ModAPI v1.2 Compatibility enabled).");
        }

        private void ApplyManualPatches()
        {
            var tooltipPostfix = new HarmonyMethod(typeof(UI_CharacterTooltip_UpdateValues_Patch).GetMethod("Postfix")) { priority = Priority.LowerThanNormal };
            PatchManually(typeof(UI_CharacterTooltip), "UpdateValues", postfix: tooltipPostfix);

            var tooltipHidePostfix = new HarmonyMethod(typeof(UI_CharacterTooltip_HideTooltip_Patch).GetMethod("Postfix"));
            PatchManually(typeof(UI_CharacterTooltip), "HideTooltip", postfix: tooltipHidePostfix, parameters: new[] { typeof(bool) });

            var saveLoadPostfix = new HarmonyMethod(typeof(AgingPatches.BaseCharacter_SaveLoadCharacter_Patch).GetMethod("Postfix"));
            PatchManually(typeof(BaseCharacter), "SaveLoadCharacter", postfix: saveLoadPostfix, parameters: new[] { typeof(SaveData) });

            var onTraitsChangedPostfix = new HarmonyMethod(typeof(AgingPatches.BaseCharacter_OnTraitsChanged_Patch).GetMethod("Postfix"));
            PatchManually(typeof(BaseCharacter), "OnTraitsChanged", postfix: onTraitsChangedPostfix);

            var onFatalDamagePrefix = new HarmonyMethod(typeof(AgingPatches.FamilyMember_OnFatalDamageTaken_Patch).GetMethod("Prefix"));
            PatchManually(typeof(BaseCharacter), "OnFatalDamageTaken", prefix: onFatalDamagePrefix);

            var obituaryPostfix = new HarmonyMethod(typeof(GameOverPatches.FamilyManager_CreateObituaryInfo_Patch).GetMethod("Postfix"));
            PatchManually(typeof(FamilyManager), "CreateObituaryInfo", postfix: obituaryPostfix, parameters: new[] { typeof(BaseCharacter) });

            var obituarySetupPostfix = new HarmonyMethod(typeof(GameOverPatches.ObituaryInfo_SetupObituary_Patch).GetMethod("Postfix"));
            PatchManually(typeof(ObituaryInfo), "SetupObituary", postfix: obituarySetupPostfix, parameters: new[] { typeof(FamilyManager.DeadCharacterInfo) });

            var gameOverOnShowPostfix = new HarmonyMethod(typeof(GameOverPatches.GameOverPanel_OnShow_Patch).GetMethod("Postfix"));
            PatchManually(typeof(GameOverPanel), "OnShow", postfix: gameOverOnShowPostfix);

            var partyMapOnShowPostfix = new HarmonyMethod(typeof(ExpeditionUIPatches.PartyMapPanel_OnShow_Patch).GetMethod("Postfix"));
            PatchManually(typeof(PartyMapPanel), "OnShow", postfix: partyMapOnShowPostfix);

            try 
            {
                var createNpcPostfix = new HarmonyMethod(typeof(IntegrationPatches.NpcVisitManager_CreateNpcVisitor_Patch).GetMethod("Postfix"));
                PatchManually(typeof(NpcVisitManager), "CreateNpcVisitor", postfix: createNpcPostfix, 
                    parameters: new[] { typeof(NpcVisitor.NpcType), typeof(FamilySpawner.CharacterAttributes), typeof(Vector3) });

                var adoptNpcPrefix = new HarmonyMethod(typeof(IntegrationPatches.FamilyManager_AdoptNpc_Patch).GetMethod("Prefix"));
                var adoptNpcPostfix = new HarmonyMethod(typeof(IntegrationPatches.FamilyManager_AdoptNpc_Patch).GetMethod("Postfix"));
                PatchManually(typeof(FamilyManager), "AdoptNpc", prefix: adoptNpcPrefix, postfix: adoptNpcPostfix, 
                    parameters: new[] { typeof(NpcVisitor) });
            }
            catch (Exception ex)
            {
                Log.Error($"Error manually patching IntegrationPatches: {ex.Message}");
            }
        }

        private void PatchManually(Type type, string methodName, HarmonyMethod prefix = null, HarmonyMethod postfix = null, Type[] parameters = null)
        {
            try
            {
                var method = type.GetMethod(
                    methodName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                    null,
                    parameters ?? Type.EmptyTypes,
                    null
                );

                if (method != null)
                {
                    _harmony.Patch(method, prefix, postfix);
                    Log.Info($"Manually patched {type.Name}.{methodName}");
                }
                else
                {
                    Log.Warn($"Failed to find method for manual patch: {type.Name}.{methodName}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Exception during manual patch of {type.Name}.{methodName}: {ex}");
            }
        }



        public void Update()
        {
            if (_config == null) return; // Prevent crash if init failing
            
            _debugManager?.Update();
            _deathManager?.Update();
            _dialogueScheduler?.Update();
            
            if (_config.enableChildDevelopment)
                _nurseJobGiver?.Update();

            UpdateHairTransitions();
        }

        private void UpdateHairTransitions()
        {
            // Feature Toggle
            if (!_config.enableHairGreying)
            {
                if (_activeTransitions.Count > 0) _activeTransitions.Clear();
                return;
            }

            if (_activeTransitions.Count == 0) return;

            float dt = UnityEngine.Time.deltaTime;
            float lerpSpeed = _config.hairGreyingLerpSpeed * dt; 
            float maxDuration = _config.hairGreyingMaxDuration;

            // Performance: Cap the number of mesh updates per frame
            const int TRANSITION_FRAME_BUDGET = 5;
            int processedThisFrame = 0;

            for (int i = _activeTransitions.Count - 1; i >= 0; i--)
            {
                if (processedThisFrame >= TRANSITION_FRAME_BUDGET) break;
                processedThisFrame++;

                var context = _activeTransitions[i];

                if (context.Member == null || context.Member.isDead || context.CachedMesh == null || context.CachedMesh.gameObject == null)
                {
                    if (context.Member != null)
                    {
                        _meshCache.Remove(context.Member.GetId());
                    }
                    _activeTransitions.RemoveAt(i);
                    continue;
                }
                
                // Force completion if timeout exceeded
                float elapsed = Time.time - context.StartTime;
                if (elapsed > maxDuration)
                {
                    // Safety timeout
                    if (Log.IsDebugEnabled) 
                        Log.Debug("Hair transition timeout for " + context.Member.firstName);
                    context.CurrentColor = context.TargetColor;
                }

                try
                {
                    context.CurrentColor = Color.Lerp(context.CurrentColor, context.TargetColor, lerpSpeed);
                    
                    context.CachedMesh.SetColor(CharacterMesh.ColorCustomization.HairColor, context.CurrentColor);
                    context.CachedMesh.RefreshColors();

                    // Synchronize the "truth" color for portraits
                    Traverse.Create(context.Member).Field("m_hairColor").SetValue(context.CurrentColor);

                    // Optimization: Only force UI refresh if color has changed significantly enough to be visible
                    // This prevents spamming the expensive UI rebuild every frame
                    if (InteractionManager.Instance != null && 
                        InteractionManager.Instance.GetSelectedFamilyMember() == context.Member &&
                        Time.frameCount % 5 == 0) // Limit to once every 5 frames max
                    {
                        InteractionManager.Instance.m_forceAvatarUpdate = true;
                    }

                    // Check if done
                    if (Mathf.Abs(context.CurrentColor.r - context.TargetColor.r) < 0.01f && 
                        Mathf.Abs(context.CurrentColor.g - context.TargetColor.g) < 0.01f && 
                        Mathf.Abs(context.CurrentColor.b - context.TargetColor.b) < 0.01f)
                    {
                        // Snap to final
                        context.CachedMesh.SetColor(CharacterMesh.ColorCustomization.HairColor, context.TargetColor);
                        context.CachedMesh.RefreshColors();
                        _activeTransitions.RemoveAt(i);
                    }
                }
                catch
                {
                    _activeTransitions.RemoveAt(i);
                }
            }
        }

        private void OnNewWeek()
        {
            if (Log.IsDebugEnabled) Log.Debug($"OnNewWeek triggering processing (Week {GameTime.Week}).");
            if (FamilyManager.Instance == null)
            {
                Log.Warn("FamilyManager.Instance is null. Skipping aging cycle.");
                return;
            }
            
            // 1. Check if we should age this week based on interval
            if (GameTime.Week % _config.agingIntervalWeeks != 0)
            {
                if (Log.IsDebugEnabled) Log.Debug($"Skipping aging this week (Interval: {_config.agingIntervalWeeks}).");
                return;
            }

            Log.Debug($"Processing aging for members (+{_config.weeksAgedPerInterval} weeks).");
            
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null)
            {
                Log.Warn("FamilyManager returned a null list of members. Skipping cycle.");
                return;
            }
 
            Log.Debug($"Found {members.Count} members to process.");
            foreach (var member in members)
            {
                Log.Debug($"===== Processing member: {(member != null ? member.firstName : "NULL MEMBER")} =====");
                if (member == null)
                {
                    Log.Warn("Member in list is null. Skipping.");
                    continue;
                }

                if (member.isDead)
                {
                    Log.Debug($"Member '{member.firstName}' is dead. Skipping.");
                    continue;
                }
                
                if (member.isDying)
                {
                    Log.Debug($"Member '{member.firstName}' is dying. Skipping.");
                    continue;
                }

                // Check for Cancellation via API Pre-Event
                if (_api != null && _api.ShouldCancelAging(member))
                {
                    Log.Debug($"Aging cancelled for '{member.firstName}' by external mod.");
                    continue;
                }

                int newAgeWeeks = _ageTracker.IncrementAge(member, _config.weeksAgedPerInterval);

                if (Log.IsDebugEnabled)
                {
                    Log.Info($"Incrementing age for '{member.firstName}' to {newAgeWeeks} weeks.");
                }
 
                if (Log.IsDebugEnabled) Log.Info($"Checking child transition for '{member.firstName}'. (IsChild: {member.isChild}, Age: {newAgeWeeks} weeks, Threshold: {_config.adultAgeYears * 52} weeks)");
                int adultWeeks = _config.adultAgeYears * 52;
                if (member.isChild && newAgeWeeks >= adultWeeks)
                {
                    Log.Info($"{member.firstName} has reached adulthood.");
                    _childManager.TransitionToAdult(member);
                }

                if (Log.IsDebugEnabled) Log.Info($"Checking elder illness for '{member.firstName}'. (Age: {newAgeWeeks} weeks, Threshold: {_config.elderAgeYears * 52} weeks)");
                int elderWeeks = _config.elderAgeYears * 52;
                if (newAgeWeeks >= elderWeeks)
                {
                    if (Log.IsDebugEnabled) Log.Info($"{member.firstName} is an elder. Processing illness roll...");
                    _illnessManager.ProcessElderIllnessRoll(member, newAgeWeeks);
                }

                if (Log.IsDebugEnabled) Log.Info($"Processing hair greying, development, and milestones for '{member.firstName}'.");
                ProcessHairGreying(member, newAgeWeeks);
                _devGeneManager.ProcessDevelopment(member, newAgeWeeks, _config.weeksAgedPerInterval);
                _milestoneManager.ProcessMilestones(member, newAgeWeeks);

                if (member.isDead)
                {
                    Log.Debug($"Member '{member.firstName}' died during an illness or development step.");
                    continue;
                }
 
                if (Log.IsDebugEnabled) Log.Info($"Processing death roll for '{member.firstName}'.");
                
                if (_config.enableNaturalDeath)
                {
                    _deathManager.ProcessDeathRoll(member, newAgeWeeks);
                }
                
                if (!member.isDead)
                {
                    if (Log.IsDebugEnabled) Log.Info($"Member '{member.firstName}' survived. Publishing event.");
                    ModEventBus.Publish("Lifespan.CharacterAgedUp", new CharacterAgedUpArgs(member, newAgeWeeks));
                }
                else
                {
                    Log.Info($"{member.firstName} died of old age.");
                }
                Log.Debug($"===== Finished processing member: {member.firstName} =====");
            }
            
            Log.Debug("Cycle complete. Cleaning up missing members from tracker.");
            _ageTracker.CleanupMissingMembers();

            // Automatic Aging for External Characters (NPCs)
            if (_api != null)
            {
                Log.Debug("Processing aging for external characters (NPCs)...");
                _api.UpdateExternalCharacters(_config.weeksAgedPerInterval);
            }

            // Force UI update for selected character portrait
            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.m_forceAvatarUpdate = true;
            }
        }

        private void ProcessHairGreying(FamilyMember member, int ageWeeks)
        {
            if (!_config.enableHairGreying) return;

            try
            {
                var profile = _ageTracker.GetOrGenerateGreyProfile(member);
                if (profile.Gene == null) return; // Should not happen due to migration

                float currentAgeYears = ageWeeks / 52f;
                float greyFactor = profile.Gene.GetGreyFactor(currentAgeYears);

                if (greyFactor <= 0f) return;
                
                // Construct colors
                Color original = new Color(profile.OriginalColor[0], profile.OriginalColor[1], profile.OriginalColor[2], profile.OriginalColor[3]);
                Color white = Color.white; 

                // Interpolate based on genetic factor (0.0 to 1.0)
                // If factor is 0.5, we get a 50% blend (Salt & Pepper)
                // If factor is 1.0, we get full White
                Color newColor = Color.Lerp(original, white, greyFactor);
                
                // Apply field (Data)
                // Capture OLD color before we update it
                Color oldColor = Color.white;
                // Try to get current value from field
                try { oldColor = Traverse.Create(member).Field("m_hairColor").GetValue<Color>(); } catch {}

                // Apply field (Data) - this updates the "truth"
                Traverse.Create(member).Field("m_hairColor").SetValue(newColor);
                
                // Refresh UI Portrait immediately (Visual Snap)
                member.UpdateAvatarSprite();
                
                // Set Target for Smooth Transition (3D Model Lerp)
                // Use cached reference instead of reflection
                int id = member.GetId();
                if (!_meshCache.TryGetValue(id, out CharacterMesh cm) || cm == null)
                {
                    object meshObj = Traverse.Create(member).Field("m_mesh").GetValue<object>();
                    cm = meshObj as CharacterMesh;
                    if (cm != null) _meshCache[id] = cm;
                }
                
                if (cm != null)
                {
                    // Check if already in list
                    var existing = _activeTransitions.Find(x => x.Member == member);
                    if (existing != null)
                    {
                        existing.TargetColor = newColor;
                        existing.CachedMesh = cm; // Update ref just in case
                    }
                    else
                    {
                        _activeTransitions.Add(new HairTransitionContext 
                        { 
                            Member = member, 
                            CachedMesh = cm,
                            TargetColor = newColor,
                            CurrentColor = oldColor, // Start from OLD color so we lerp
                            StartTime = Time.time
                        });
                    }
                }
            }
            catch (Exception)
            {
                // Warn once per member/session ideally, but fine for now
                // Log.Warn($"[DEBUG] Greying failed for {member.firstName}");
            }
        }

        private void OnGameLoad(SaveData data)
        {
            ResetAllState();
            if (Log.IsDebugEnabled) Log.Debug("OnGameLoad() triggering tracker load.");
            _ageTracker.LoadAgeData();
            
            if (Log.IsDebugEnabled) Log.Debug("Re-applying modifiers.");
            if (FamilyManager.Instance != null)
            {
                var members = FamilyManager.Instance.GetAllFamilyMembers();
                if (members != null)
                {
                    foreach (var m in members)
                    {
                        if (m != null && !m.isDead)
                            _illnessManager.ReapplyModifiers(m);
                    }
                }
            }
 
            if (Log.IsDebugEnabled) Log.Debug("Content load finalized.");
        }

        private void OnGameSave(SaveData data)
        {
            if (Log.IsDebugEnabled) Log.Debug("OnGameSave() triggering tracker save.");
            _ageTracker.SaveAgeData();
            if (Log.IsDebugEnabled) Log.Debug("Save process notify complete.");
        }

        public void Shutdown()
        {
            if (Log.IsDebugEnabled) Log.Debug("Shutdown() starting.");
            _harmony?.UnpatchAll("com.lifespan.patches");
            ModAPI.Events.GameEvents.OnAfterLoad -= OnGameLoad;
            ModAPI.Events.GameEvents.OnBeforeSave -= OnGameSave;
            AgingPatches.OnNewWeekCallback = null;
            Log.Info("Mod shut down.");
        }

        // ====================================================================
        // ISETTINGSPROVIDER IMPLEMENTATION
        // ====================================================================

        /// <summary>
        /// Provides the metadata for the ModAPI settings UI.
        /// </summary>
        public IEnumerable<SettingDefinition> GetSettings()
        {
            if (_config == null) _config = new LifespanConfig();
            return SpineSettingsHelper.Scan(_config);
        }

        public override void OnSettingsLoaded()
        {
            // Sync static logger enabled state
            // Log.IsDebugEnabled is read-only, controlled by ModAPI core
            if (_config != null)
                Log.Info($"Settings auto-loaded (Verbose: {_config.verboseLogging})");
        }

        public void ResetToDefaults()
        {
            // Fix: Don't replace the object (managers hold a reference to it).
            // Instead, create a temporary default one and copy values over, 
            // or just use JsonUtility to overwrite from a fresh instance.
            var defaults = new LifespanConfig();
            string json = JsonUtility.ToJson(defaults);
            JsonUtility.FromJsonOverwrite(json, _config);
            
            Log.Info("Settings reset to defaults (values overridden in current instance).");
        }

        public object GetSettingsObject() => _config ?? (_config = new LifespanConfig());

    }
}
