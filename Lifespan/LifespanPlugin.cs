using System;
using System.Collections.Generic;
using ModAPI;
using ModAPI.Core;
using ModAPI.Util;
using ModAPI.Saves;
using ModAPI.Reflection;
using ModAPI.Events;
using UnityEngine;
using HarmonyLib;

namespace Lifespan
{
    public class LifespanPlugin : IModPlugin, IModShutdown, IModUpdate
    {
        private IPluginContext _ctx;
        private IModLogger _log;
        private LifespanConfig _config;
        private AgeTracker _ageTracker;
        private ChildTransitionManager _childManager;
        private ElderIllnessManager _illnessManager;
        private DeathManager _deathManager;
        private DevelopmentGeneManager _devGeneManager;
        private LifespanAPIImpl _api;
        private Harmony _harmony;
        private DebugManager _debugManager;

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

        public void ResetTransitions()
        {
            _activeTransitions.Clear();
            _meshCache.Clear();
        }

        public void Initialize(IPluginContext ctx)
        {
            Instance = this;
            _ctx = ctx;
            _log = ctx.Log;
            
            // 1. Load Configuration (sets VerboseEnabled)
            LoadConfiguration();

            LifespanLoggerExtensions.Debug(_log, "Initialize() complete.");
        }

        public void Start(IPluginContext ctx)
        {
            LifespanLoggerExtensions.Debug(_log, "Start() called.");

            // 2. Initialize Core components
            LifespanLoggerExtensions.Debug(_log, "Initializing core components...");
            _ageTracker = new AgeTracker(_ctx, _config);
            _childManager = new ChildTransitionManager(_ctx, _config, _ageTracker);
            _illnessManager = new ElderIllnessManager(_ctx, _config, _ageTracker);
            _deathManager = new DeathManager(_ctx, _config, _ageTracker);
            _devGeneManager = new DevelopmentGeneManager(_ctx, _config, _ageTracker);
            _illnessManager.SetDeathManager(_deathManager);
            _debugManager = new DebugManager(_log, _config);

            // 3. Register API
            LifespanLoggerExtensions.Debug(_log, "Registering ILifespanAPI...");
            _api = new LifespanAPIImpl(_ctx, _config, _ageTracker, _illnessManager, _devGeneManager);
            ModAPIRegistry.RegisterAPI<ILifespanAPI>("com.lifespan.api", _api);

            // 4. Initialize Harmony Patches
            LifespanLoggerExtensions.Debug(_log, "Applying Harmony patches...");
            _harmony = new Harmony("com.lifespan.patches");
            AgingPatches.Tracker = _ageTracker;
            AgingPatches.IllnessManager = _illnessManager;
            AgingPatches.DeathManager = _deathManager;
            AgingPatches.OnNewWeekCallback = OnNewWeek;

            // Try automatic patching for public methods first
            try
            {
                _harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
                _log.Info("Automatic Harmony patches applied successfully.");
            }
            catch (Exception ex)
            {
                _log.Error($"Error in automatic patching: {ex.Message}\n{ex.StackTrace}");
            }
        
            // Manually patch all non-public methods to ensure they are found.
            var tooltipPostfix = new HarmonyMethod(typeof(UI_CharacterTooltip_UpdateValues_Patch).GetMethod("Postfix")) { priority = Priority.LowerThanNormal };
            PatchManually(typeof(UI_CharacterTooltip), "UpdateValues", postfix: tooltipPostfix);

            var saveLoadPostfix = new HarmonyMethod(typeof(AgingPatches.BaseCharacter_SaveLoadCharacter_Patch).GetMethod("Postfix"));
            PatchManually(typeof(BaseCharacter), "SaveLoadCharacter", postfix: saveLoadPostfix, parameters: new[] { typeof(SaveData) });

            var onTraitsChangedPostfix = new HarmonyMethod(typeof(AgingPatches.BaseCharacter_OnTraitsChanged_Patch).GetMethod("Postfix"));
            PatchManually(typeof(BaseCharacter), "OnTraitsChanged", postfix: onTraitsChangedPostfix);

            var onFatalDamagePrefix = new HarmonyMethod(typeof(AgingPatches.FamilyMember_OnFatalDamageTaken_Patch).GetMethod("Prefix"));
            PatchManually(typeof(BaseCharacter), "OnFatalDamageTaken", prefix: onFatalDamagePrefix);

            // Fix: CreateObituaryInfo takes a BaseCharacter parameter
            var obituaryPostfix = new HarmonyMethod(typeof(GameOverPatches.FamilyManager_CreateObituaryInfo_Patch).GetMethod("Postfix"));
            PatchManually(typeof(FamilyManager), "CreateObituaryInfo", postfix: obituaryPostfix, parameters: new[] { typeof(BaseCharacter) });

            // Manual Patching for IntegrationPatches (NPCs) to ensure they work
            try 
            {
                var createNpcPostfix = new HarmonyMethod(typeof(IntegrationPatches.NpcVisitManager_CreateNpcVisitor_Patch).GetMethod("Postfix"));
                // CreateNpcVisitor is private: (NpcVisitor.NpcType, FamilySpawner.CharacterAttributes, Vector3)
                // We need to resolve FamilySpawner which might be in Assembly-CSharp
                // Use Type.GetType or typeof if available. FamilySpawner is likely available since we reference Assembly-CSharp.
                PatchManually(typeof(NpcVisitManager), "CreateNpcVisitor", postfix: createNpcPostfix, 
                    parameters: new[] { typeof(NpcVisitor.NpcType), typeof(FamilySpawner.CharacterAttributes), typeof(Vector3) });

                var adoptNpcPrefix = new HarmonyMethod(typeof(IntegrationPatches.FamilyManager_AdoptNpc_Patch).GetMethod("Prefix"));
                var adoptNpcPostfix = new HarmonyMethod(typeof(IntegrationPatches.FamilyManager_AdoptNpc_Patch).GetMethod("Postfix"));
                PatchManually(typeof(FamilyManager), "AdoptNpc", prefix: adoptNpcPrefix, postfix: adoptNpcPostfix, 
                    parameters: new[] { typeof(NpcVisitor) });
            }
            catch (Exception ex)
            {
                _log.Error($"Error manually patching IntegrationPatches: {ex.Message}");
            }
        
            // 5. Subscribe to game events via ModAPI
            LifespanLoggerExtensions.Debug(_log, "Subscribing to GameEvents...");
            ModAPI.Events.GameEvents.OnAfterLoad += OnGameLoad;
            ModAPI.Events.GameEvents.OnBeforeSave += OnGameSave;
            
            _log.Info("Mod successfully started.");
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
                    _log.Info($"Manually patched {type.Name}.{methodName}");
                }
                else
                {
                    _log.Warn($"Failed to find method for manual patch: {type.Name}.{methodName}");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Exception during manual patch of {type.Name}.{methodName}: {ex}");
            }
        }

        private void LoadConfiguration()
        {
            _config = new LifespanConfig();
            _config.verboseLogging = true; // Force verbose logging for debugging
            LifespanLoggerExtensions.Debug(_log, "Loading configuration from ModAPI Settings...");
            
            // Map ModSettings to our config object
            _config.adultAgeYears = _ctx.Settings.GetInt("adultAgeYears", 18);
            _config.elderAgeYears = _ctx.Settings.GetInt("elderAgeYears", 60);
            _config.maxAgeYears = _ctx.Settings.GetInt("maxAgeYears", 90);
            _config.verboseLogging = _ctx.Settings.GetBool("verboseLogging", false);

            // Sync static flag for extension method
            LifespanLoggerExtensions.VerboseEnabled = _config.verboseLogging;
            _config.initialAdultAgeYears = _ctx.Settings.GetInt("initialAdultAgeYears", 30);
            
            _config.elderIllnessBaseChance = _ctx.Settings.GetFloat("elderIllnessBaseChance", 0.001f);
            _config.deathProbabilityMultiplier = _ctx.Settings.GetFloat("deathProbabilityMultiplier", 1.0f);
            
            _config.enableDementia = _ctx.Settings.GetBool("enableDementia", true);
            _config.enableArthritis = _ctx.Settings.GetBool("enableArthritis", true);
            _config.enableHeartDisease = _ctx.Settings.GetBool("enableHeartDisease", true);
            _config.enableFrailty = _ctx.Settings.GetBool("enableFrailty", true);
            _config.enableRespiratory = _ctx.Settings.GetBool("enableRespiratory", true);
            
            _config.agingIntervalWeeks = _ctx.Settings.GetInt("agingIntervalWeeks", 1);
            _config.weeksAgedPerInterval = _ctx.Settings.GetInt("weeksAgedPerInterval", 52); // Default to 1 year

            _config.dementiaIntModifier = _ctx.Settings.GetFloat("dementiaIntModifier", 0.5f);
            _config.arthritisSpeedModifier = _ctx.Settings.GetFloat("arthritisSpeedModifier", 0.7f);
            _config.frailtyStrModifier = _ctx.Settings.GetFloat("frailtyStrModifier", 0.6f);
            _config.heartDiseaseAttackChance = _ctx.Settings.GetFloat("heartDiseaseAttackChance", 0.02f);
            _config.heartAttackDamage = _ctx.Settings.GetFloat("heartAttackDamage", 60f);

            _config.ValidateAndClamp();
            
            LifespanLoggerExtensions.Debug(_log, "Config validation complete.");
            _log.Info($"Configuration loaded (Verbose: {_config.verboseLogging})");
        }

        public void Update()
        {
            _debugManager?.Update();
            _deathManager?.Update();
            UpdateHairTransitions();
        }

        private void UpdateHairTransitions()
        {
            if (_activeTransitions.Count == 0) return;

            float dt = UnityEngine.Time.deltaTime;
            float lerpSpeed = _config.hairGreyingLerpSpeed * dt; 
            float maxDuration = _config.hairGreyingMaxDuration;

            for (int i = _activeTransitions.Count - 1; i >= 0; i--)
            {
                var context = _activeTransitions[i];

                if (context.Member == null || context.Member.isDead || context.CachedMesh == null)
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
                    if (LifespanLoggerExtensions.VerboseEnabled) 
                        LifespanLoggerExtensions.Debug(_log, "Hair transition timeout for " + context.Member.firstName);
                    context.CurrentColor = context.TargetColor;
                }

                try
                {
                    context.CurrentColor = Color.Lerp(context.CurrentColor, context.TargetColor, lerpSpeed);
                    
                    context.CachedMesh.SetColor(CharacterMesh.ColorCustomization.HairColor, context.CurrentColor);
                    context.CachedMesh.RefreshColors();

                    // Synchronize the "truth" color for portraits
                    Safe.SetField(context.Member, "m_hairColor", context.CurrentColor);

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
            if (LifespanLoggerExtensions.VerboseEnabled) LifespanLoggerExtensions.Debug(_log, $"OnNewWeek triggering processing (Week {GameTime.Week}).");
            if (FamilyManager.Instance == null)
            {
                _log.Warn("FamilyManager.Instance is null. Skipping aging cycle.");
                return;
            }
            
            // 1. Check if we should age this week based on interval
            if (GameTime.Week % _config.agingIntervalWeeks != 0)
            {
                if (LifespanLoggerExtensions.VerboseEnabled) LifespanLoggerExtensions.Debug(_log, $"Skipping aging this week (Interval: {_config.agingIntervalWeeks}).");
                return;
            }

            _log.Info($"Processing aging for members (+{_config.weeksAgedPerInterval} weeks).");
            
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null)
            {
                _log.Warn("[AGING] FamilyManager returned a null list of members. Skipping cycle.");
                return;
            }

            _log.Info($"[AGING] Found {members.Count} members to process.");
            foreach (var member in members)
            {
                _log.Info($"[AGING] ===== Processing member: {(member != null ? member.firstName : "NULL MEMBER")} =====");
                if (member == null)
                {
                    _log.Warn("[AGING] Member in list is null. Skipping.");
                    continue;
                }

                if (member.isDead)
                {
                    _log.Info($"[AGING] Member '{member.firstName}' is dead. Skipping.");
                    continue;
                }
                
                if (member.isDying)
                {
                    _log.Info($"[AGING] Member '{member.firstName}' is dying. Skipping.");
                    continue;
                }

                int newAgeWeeks = _ageTracker.IncrementAge(member, _config.weeksAgedPerInterval);

                if (LifespanLoggerExtensions.VerboseEnabled)
                {
                    _log.Info($"[AGING] Step 1: Incrementing age for '{member.firstName}'.");
                    _log.Info($"[AGING] -> New age is {newAgeWeeks} weeks.");
                }

                if (LifespanLoggerExtensions.VerboseEnabled) _log.Info($"[AGING] Step 2: Checking child transition for '{member.firstName}'. (IsChild: {member.isChild}, Age: {newAgeWeeks} weeks, Threshold: {_config.adultAgeYears * 52} weeks)");
                int adultWeeks = _config.adultAgeYears * 52;
                if (member.isChild && newAgeWeeks >= adultWeeks)
                {
                    _log.Info($"[AGING] -> {member.firstName} is now an adult. Transitioning...");
                    _childManager.TransitionToAdult(member);
                }

                if (LifespanLoggerExtensions.VerboseEnabled) _log.Info($"[AGING] Step 3: Checking elder illness for '{member.firstName}'. (Age: {newAgeWeeks} weeks, Threshold: {_config.elderAgeYears * 52} weeks)");
                int elderWeeks = _config.elderAgeYears * 52;
                if (newAgeWeeks >= elderWeeks)
                {
                    if (LifespanLoggerExtensions.VerboseEnabled) _log.Info($"[AGING] -> {member.firstName} is an elder. Processing illness roll...");
                    _illnessManager.ProcessElderIllnessRoll(member, newAgeWeeks);
                }

                if (LifespanLoggerExtensions.VerboseEnabled) _log.Info($"[AGING] Step 4: Processing hair greying for '{member.firstName}'.");
                ProcessHairGreying(member, newAgeWeeks);

                if (LifespanLoggerExtensions.VerboseEnabled) _log.Info($"[AGING] Step 5: Processing development for '{member.firstName}'.");
                _devGeneManager.ProcessDevelopment(member, newAgeWeeks, _config.weeksAgedPerInterval);

                if (member.isDead)
                {
                    _log.Info($"[AGING] -> Member '{member.firstName}' died during an illness or development step. Stopping processing for this member.");
                    continue;
                }

                if (LifespanLoggerExtensions.VerboseEnabled) _log.Info($"[AGING] Step 6: Processing death roll for '{member.firstName}'.");
                _deathManager.ProcessDeathRoll(member, newAgeWeeks);
                
                if (!member.isDead)
                {
                    if (LifespanLoggerExtensions.VerboseEnabled) _log.Info($"[AGING] -> Member '{member.firstName}' survived. Publishing event.");
                    ModEventBus.Publish("Lifespan.CharacterAgedUp", new CharacterAgedUpArgs(member, newAgeWeeks));
                }
                else
                {
                    _log.Info($"[AGING] -> Member '{member.firstName}' died from old age.");
                }
                if (LifespanLoggerExtensions.VerboseEnabled) _log.Info($"[AGING] ===== Finished processing member: {member.firstName} =====");
            }
            
            _log.Info("[AGING] Cycle complete. Cleaning up missing members from tracker.");
            _ageTracker.CleanupMissingMembers();

            // Force UI update for selected character portrait
            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.m_forceAvatarUpdate = true;
            }
        }

        private void ProcessHairGreying(FamilyMember member, int ageWeeks)
        {
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
                try { oldColor = Safe.GetField<Color>(member, "m_hairColor"); } catch {}

                // Apply field (Data) - this updates the "truth"
                Safe.SetField(member, "m_hairColor", newColor);
                
                // Refresh UI Portrait immediately (Visual Snap)
                member.UpdateAvatarSprite();
                
                // Set Target for Smooth Transition (3D Model Lerp)
                // Use cached reference instead of reflection
                int id = member.GetId();
                if (!_meshCache.TryGetValue(id, out CharacterMesh cm) || cm == null)
                {
                    object meshObj = ReflectionHelper.GetField<object>(member, "m_mesh");
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
            catch (Exception ex)
            {
                // Warn once per member/session ideally, but fine for now
                // _log.Warn($"[DEBUG] Greying failed for {member.firstName}: {ex.Message}");
            }
        }

        private void OnGameLoad(SaveData data)
        {
            if (LifespanLoggerExtensions.VerboseEnabled) LifespanLoggerExtensions.Debug(_log, "OnGameLoad() triggering tracker load.");
            _ageTracker.LoadAgeData();
            
            if (LifespanLoggerExtensions.VerboseEnabled) LifespanLoggerExtensions.Debug(_log, "Re-applying modifiers.");
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

            if (LifespanLoggerExtensions.VerboseEnabled) LifespanLoggerExtensions.Debug(_log, "Content load finalized.");
        }

        private void OnGameSave(SaveData data)
        {
            if (LifespanLoggerExtensions.VerboseEnabled) LifespanLoggerExtensions.Debug(_log, "OnGameSave() triggering tracker save.");
            _ageTracker.SaveAgeData();
            if (LifespanLoggerExtensions.VerboseEnabled) LifespanLoggerExtensions.Debug(_log, "Save process notify complete.");
        }

        public void Shutdown()
        {
            if (LifespanLoggerExtensions.VerboseEnabled) LifespanLoggerExtensions.Debug(_log, "Shutdown() starting.");
            _harmony?.UnpatchAll("com.lifespan.patches");
            ModAPI.Events.GameEvents.OnAfterLoad -= OnGameLoad;
            ModAPI.Events.GameEvents.OnBeforeSave -= OnGameSave;
            AgingPatches.OnNewWeekCallback = null;
            _log.Info("[Lifespan] Mod shut down.");
        }
    }
}
