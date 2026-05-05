using System;
using System.Collections.Generic;
using ModAPI;
using ModAPI.Core;
using ModAPI.Util;
using ModAPI.Events;
using ShelteredAPI.Events;
using UnityEngine;
using HarmonyLib;
using ModAPI.Spine; // Required for settings UI support

namespace Lifespan
{
    /// <summary>
    /// Main plugin entry point for the Lifespan mod.
    /// Handles initialization, life cycle events, and settings integration.
    /// </summary>
    public class LifespanPlugin : ModManagerBase<LifespanConfig>, IModPlugin, IModUpdate, IModShutdown
    {
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
        private DialogueHelper _dialogueHelper;
        private ChildDevelopmentManager _childDevManager;
        private NurseJobGiver _nurseJobGiver;
        private ExpeditionDialogueManager _expeditionDialogueManager;
        private WeeklyAgingService _weeklyAgingService;
        private bool _pendingHydration;
        private bool _pendingFreshGameHydration;

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

        /// <summary>
        /// Resets all internal buffers and manager states.
        /// Called during initialization and upon game reloading to ensure a clean slate.
        /// </summary>
        public void ResetAllState()
        {
            if (Log.IsDebugEnabled) Log.Debug("Resetting all mod state for fresh session.");
            
            _activeTransitions?.Clear();
            _meshCache?.Clear();
            TooltipCache.Clear();
            _dialogueScheduler?.Clear();
            _dialogueHelper?.ClearHistory();
            _milestoneManager?.Reset();
            _deathManager?.Reset();
            _expeditionDialogueManager?.Reset();
        }

        /// <summary>
        /// ModAPI v1.2 Entry Point. Initializes all singleton managers and 
        /// registers the data structures for the save system.
        /// </summary>
        public override void Initialize(IPluginContext ctx)
        {
            try
            {
                base.Initialize(ctx); // REQUIRED for v1.2 attribute binding and config loading
                Instance = this;
                Config?.ValidateAndClamp();
                
                // 1. Initialize core utilities
                _dialogueScheduler = new DialogueScheduler(Log, this.Random);
                _dialogueHelper = new DialogueHelper(this.Random);
                
                // 2. Initialize primary data tracker (AgeTracker)
                _ageTracker = new AgeTracker(ctx, Config, this.Random);
                _dialogueHelper.SetAgeTracker(_ageTracker);
                
                // 3. Initialize domain-specific managers
                _childManager = new ChildTransitionManager(ctx, Config, _ageTracker, this.Random);
                _illnessManager = new ElderIllnessManager(ctx, Config, _ageTracker, this.Random, _dialogueHelper);
                _milestoneManager = new MilestoneManager(ctx, Config, _ageTracker, this.Random, _dialogueHelper); 
                _deathManager = new DeathManager(ctx, Config, _ageTracker, this.Random);
                _devGeneManager = new DevelopmentGeneManager(ctx, Config, _ageTracker, this.Random);
                
                _childDevManager = new ChildDevelopmentManager(ctx, Config, _ageTracker);
                _nurseJobGiver = new NurseJobGiver(ctx, _childDevManager, _ageTracker, _dialogueScheduler);
                _expeditionDialogueManager = new ExpeditionDialogueManager(ctx, Config, _ageTracker, _illnessManager, _dialogueScheduler, _dialogueHelper, this.Random);
                
                _debugManager = new DebugManager(Log, Config);

                ResetAllState();
                if (Log.IsDebugEnabled) Log.Debug("Initialize() complete.");
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
            if (Log.IsDebugEnabled) Log.Debug("Start() called.");

            // 2. Wire managers
            _illnessManager.SetDeathManager(_deathManager);
            _illnessManager.SetScheduler(_dialogueScheduler);
            _milestoneManager.SetScheduler(_dialogueScheduler);
            _deathManager.SetScheduler(_dialogueScheduler);
            _devGeneManager.SetScheduler(_dialogueScheduler);
            _devGeneManager.SetMilestoneManager(_milestoneManager);

            // 3. Register API
            if (Log.IsDebugEnabled) Log.Debug("Registering ILifespanAPI...");
            _api = new LifespanAPIImpl(Context, Config, _ageTracker, _illnessManager, _devGeneManager, _childDevManager);
            ModAPIRegistry.RegisterAPI<ILifespanAPI>("com.lifespan.api", _api, Context.Mod.Id);
            _weeklyAgingService = new WeeklyAgingService(
                Log,
                Config,
                _ageTracker,
                _childManager,
                _illnessManager,
                _deathManager,
                _devGeneManager,
                _milestoneManager,
                () => _api,
                ProcessHairGreying);

            // 4. Initialize Harmony Patches
            if (Log.IsDebugEnabled) Log.Debug("Applying Harmony patches...");
            _harmony = new Harmony("com.lifespan.patches");
            AgingPatches.Tracker = _ageTracker;
            AgingPatches.IllnessManager = _illnessManager;
            AgingPatches.DeathManager = _deathManager;
            AgingPatches.OnNewWeekCallback = _weeklyAgingService.ProcessNewWeek;

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
            if (Config.enableChildDevelopment)
            {
                if (Log.IsDebugEnabled) Log.Debug("Initializing Child Capability Patches...");
                ChildCapabilityPatches.Initialize(Context, _childDevManager);
            }
        
            // Manually patch all non-public methods
            ApplyManualPatches();
        
            // 5. Subscribe to Sheltered game events
            if (Log.IsDebugEnabled) Log.Debug("Subscribing to ShelteredEvents...");
            ShelteredEvents.AfterLoad += OnGameLoad;
            ShelteredEvents.BeforeSave += OnGameSave;
            ShelteredEvents.SessionStarted += OnSessionStarted;
            ShelteredEvents.NewGame += OnNewGame;
            
            Log.Info("Mod successfully started.");
        }

        private void ApplyManualPatches()
        {
            int patchCount = 0;
            int attemptedCount = 0;

            var tooltipPostfix = new HarmonyMethod(typeof(UI_CharacterTooltip_UpdateValues_Patch).GetMethod("Postfix")) { priority = Priority.LowerThanNormal };
            attemptedCount++;
            if (PatchManually(typeof(UI_CharacterTooltip), "UpdateValues", postfix: tooltipPostfix)) patchCount++;

            var tooltipHidePostfix = new HarmonyMethod(typeof(UI_CharacterTooltip_HideTooltip_Patch).GetMethod("Postfix"));
            attemptedCount++;
            if (PatchManually(typeof(UI_CharacterTooltip), "HideTooltip", postfix: tooltipHidePostfix, parameters: new[] { typeof(bool) })) patchCount++;

            var saveLoadPostfix = new HarmonyMethod(typeof(AgingPatches.BaseCharacter_SaveLoadCharacter_Patch).GetMethod("Postfix"));
            attemptedCount++;
            if (PatchManually(typeof(BaseCharacter), "SaveLoadCharacter", postfix: saveLoadPostfix, parameters: new[] { typeof(SaveData) })) patchCount++;

            var onTraitsChangedPostfix = new HarmonyMethod(typeof(AgingPatches.BaseCharacter_OnTraitsChanged_Patch).GetMethod("Postfix"));
            attemptedCount++;
            if (PatchManually(typeof(BaseCharacter), "OnTraitsChanged", postfix: onTraitsChangedPostfix)) patchCount++;

            var onFatalDamagePrefix = new HarmonyMethod(typeof(AgingPatches.FamilyMember_OnFatalDamageTaken_Patch).GetMethod("Prefix"));
            attemptedCount++;
            if (PatchManually(typeof(BaseCharacter), "OnFatalDamageTaken", prefix: onFatalDamagePrefix)) patchCount++;

            var obituaryPostfix = new HarmonyMethod(typeof(GameOverPatches.FamilyManager_CreateObituaryInfo_Patch).GetMethod("Postfix"));
            attemptedCount++;
            if (PatchManually(typeof(FamilyManager), "CreateObituaryInfo", postfix: obituaryPostfix, parameters: new[] { typeof(BaseCharacter) })) patchCount++;

            var obituarySetupPostfix = new HarmonyMethod(typeof(GameOverPatches.ObituaryInfo_SetupObituary_Patch).GetMethod("Postfix"));
            attemptedCount++;
            if (PatchManually(typeof(ObituaryInfo), "SetupObituary", postfix: obituarySetupPostfix, parameters: new[] { typeof(FamilyManager.DeadCharacterInfo) })) patchCount++;

            var gameOverOnShowPostfix = new HarmonyMethod(typeof(GameOverPatches.GameOverPanel_OnShow_Patch).GetMethod("Postfix"));
            attemptedCount++;
            if (PatchManually(typeof(GameOverPanel), "OnShow", postfix: gameOverOnShowPostfix)) patchCount++;

            var partyMapOnShowPostfix = new HarmonyMethod(typeof(ExpeditionUIPatches.PartyMapPanel_OnShow_Patch).GetMethod("Postfix"));
            attemptedCount++;
            if (PatchManually(typeof(PartyMapPanel), "OnShow", postfix: partyMapOnShowPostfix)) patchCount++;

            try 
            {
                var createNpcPostfix = new HarmonyMethod(typeof(IntegrationPatches.NpcVisitManager_CreateNpcVisitor_Patch).GetMethod("Postfix"));
                attemptedCount++;
                if (PatchManually(typeof(NpcVisitManager), "CreateNpcVisitor", postfix: createNpcPostfix, 
                    parameters: new[] { typeof(NpcVisitor.NpcType), typeof(FamilySpawner.CharacterAttributes), typeof(Vector3) })) patchCount++;

                var adoptNpcPrefix = new HarmonyMethod(typeof(IntegrationPatches.FamilyManager_AdoptNpc_Patch).GetMethod("Prefix"));
                var adoptNpcPostfix = new HarmonyMethod(typeof(IntegrationPatches.FamilyManager_AdoptNpc_Patch).GetMethod("Postfix"));
                attemptedCount++;
                if (PatchManually(typeof(FamilyManager), "AdoptNpc", prefix: adoptNpcPrefix, postfix: adoptNpcPostfix, 
                    parameters: new[] { typeof(NpcVisitor) })) patchCount++;
            }
            catch (Exception ex)
            {
                Log.Error($"Error manually patching IntegrationPatches: {ex.Message}");
            }

            if (patchCount == attemptedCount)
            {
                Log.Info($"Applied {patchCount} manual patches successfully.");
            }
            else
            {
                Log.Warn($"Applied {patchCount}/{attemptedCount} manual patches. Check warnings above.");
            }
        }

        private bool PatchManually(Type type, string methodName, HarmonyMethod prefix = null, HarmonyMethod postfix = null, Type[] parameters = null)
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
                    return true;
                }
                else
                {
                    Log.Warn($"Failed to find method for manual patch: {type.Name}.{methodName}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Exception during manual patch of {type.Name}.{methodName}: {ex}");
                return false;
            }
        }



        public void Update()
        {
            if (Config == null) return; // Prevent crash if init failing

            TryHydrateAgeTrackerIfNeeded();
            
            _debugManager?.Update();
            _deathManager?.Update();
            _dialogueScheduler?.Update();
            _childManager?.Update();
            
            if (Config.enableChildDevelopment)
                _nurseJobGiver?.Update();

            _expeditionDialogueManager?.Update();

            UpdateHairTransitions();
        }

        private void TryHydrateAgeTrackerIfNeeded(bool force = false)
        {
            if (_ageTracker == null || _ageTracker.IsDataHydrated) return;

            var saveManager = SaveManager.instance;
            bool saveIsLoading = saveManager != null && saveManager.isLoading;

            if (!force && !_pendingHydration && !_pendingFreshGameHydration)
            {
                // Fallback path for fresh sessions where OnAfterLoad is not raised.
                if (saveManager == null || saveIsLoading) return;
                _pendingHydration = true;
            }

            if (saveIsLoading && !_pendingFreshGameHydration) return;
            if (FamilyManager.Instance == null && !force) return;

            if (_pendingFreshGameHydration)
            {
                _ageTracker.Reset(clearPersistentContainer: true);
            }

            _ageTracker.LoadAgeData();
            _pendingHydration = false;
            _pendingFreshGameHydration = false;
        }

        private void UpdateHairTransitions()
        {
            // Feature Toggle
            if (!Config.enableHairGreying)
            {
                if (_activeTransitions.Count > 0) _activeTransitions.Clear();
                return;
            }

            if (_activeTransitions.Count == 0) return;

            float dt = UnityEngine.Time.deltaTime;
            float lerpSpeed = Config.hairGreyingLerpSpeed * dt; 
            float maxDuration = Config.hairGreyingMaxDuration;

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

        private void ProcessHairGreying(FamilyMember member, int ageWeeks)
        {
            if (!Config.enableHairGreying) return;

            try
            {
                var profile = _ageTracker.GetOrGenerateGreyProfile(member);
                if (profile.Gene == null) return; // Should not happen due to migration

                float currentAgeYears = ageWeeks / (float)LifespanConstants.WeeksPerYear;
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
            Config?.ValidateAndClamp();
            if (Log.IsDebugEnabled) Log.Debug("OnGameLoad() triggering tracker load.");
            _ageTracker.LoadAgeData();
            _pendingHydration = false;
            _pendingFreshGameHydration = false;
            
            if (Log.IsDebugEnabled) Log.Debug("Re-applying modifiers.");
            if (FamilyManager.Instance != null)
            {
                var members = FamilyManager.Instance.GetAllFamilyMembers();
                if (members != null)
                {
                    int adultWeeks = Config.adultAgeYears * LifespanConstants.WeeksPerYear;
                    foreach (var m in members)
                    {
                        if (m == null || m.isDead) continue;

                        int ageWeeks = _ageTracker.GetAgeWeeks(m);
                        if (m.isChild && ageWeeks >= adultWeeks)
                        {
                            _childManager.TransitionToAdult(m);
                        }

                        _illnessManager.ReapplyModifiers(m);
                    }
                }
            }
 
            if (Log.IsDebugEnabled) Log.Debug("Content load finalized.");
        }

        private void OnGameSave(SaveData data)
        {
            if (_ageTracker != null && !_ageTracker.IsDataHydrated)
            {
                TryHydrateAgeTrackerIfNeeded(force: true);
            }

            if (Log.IsDebugEnabled) Log.Debug("OnGameSave() triggering tracker save.");
            _ageTracker.SaveAgeData();
            if (Log.IsDebugEnabled) Log.Debug("Save process notify complete.");
        }

        private void OnSessionStarted()
        {
            _pendingHydration = true;
        }

        private void OnNewGame()
        {
            _pendingHydration = true;
            _pendingFreshGameHydration = true;
            Log.Info("[AgeTracker] New game detected. Scheduling fresh age initialization.");
        }

        public void Shutdown()
        {
            if (Log.IsDebugEnabled) Log.Debug("Shutdown() starting.");
            _harmony?.UnpatchAll("com.lifespan.patches");
            ShelteredEvents.AfterLoad -= OnGameLoad;
            ShelteredEvents.BeforeSave -= OnGameSave;
            ShelteredEvents.SessionStarted -= OnSessionStarted;
            ShelteredEvents.NewGame -= OnNewGame;
            AgingPatches.OnNewWeekCallback = null;
            Log.Info("Mod shut down.");
        }


    }
}
