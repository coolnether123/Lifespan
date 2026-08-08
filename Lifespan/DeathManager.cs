using ModAPI.Core;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    /// <summary>
    /// Handles death from old age based on probability.
    /// </summary>
    public class DeathManager
    {
        private readonly LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly AgeTracker _ageTracker;
        private readonly IDeathRecordState _deathRecords;
        private readonly ModRandomStream _random;
        private DialogueScheduler _scheduler;

        private const int SCHEDULE_DEATH_RANDOM_OFFSET_DAYS = 7;
        private const float FATAL_DAMAGE_AMOUNT = 999f;
        private IModLogger Log => _log;
 
        public DeathManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker, ModRandomStream random)
            : this(ctx, config, ageTracker, random, ageTracker != null ? ageTracker.State.Deaths : null)
        {
        }

        internal DeathManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker, ModRandomStream random, IDeathRecordState deathRecords)
        {
            _config = config;
            _log = ctx.Log;
            _ageTracker = ageTracker;
            _deathRecords = deathRecords;
            _random = random;
        }

        public void SetScheduler(DialogueScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        private void TriggerJournal(string text, DialogueScheduler.Priority priority = DialogueScheduler.Priority.Routine)
        {
            if (_scheduler != null) _scheduler.Enqueue(null, text, true, priority);
            else JournalEntryWriter.TryInsert(text, Log);
        }

        private void TriggerSpeech(FamilyMember member, string text, DialogueScheduler.Priority priority = DialogueScheduler.Priority.Routine)
        {
            if (_scheduler != null) _scheduler.Enqueue(member, text, false, priority);
            else
            {
                try
                {
                    member.Say(text);
                }
                catch (Exception ex)
                {
                    if (Log.IsDebugEnabled) Log.Debug($"Failed to show death speech line: {ex.Message}");
                }
            }
        }

        public void Reset()
        {
            if (Log.IsDebugEnabled) Log.Debug("Resetting pending deaths for fresh session.");
            _scheduledDeaths.Clear();
            _surgeDeaths.Clear();
        }

        private struct ScheduledDeath
        {
            public FamilyMember Member;
            public string Reason;
            public int TargetDay;
        }

        private struct SurgeDeath
        {
            public FamilyMember Member;
            public string Reason;
            public float DieAtTime;
            public int TargetDay; // Track the intended death day for accurate reporting
        }

        private readonly List<ScheduledDeath> _scheduledDeaths = new List<ScheduledDeath>();
        private readonly List<SurgeDeath> _surgeDeaths = new List<SurgeDeath>();

        private readonly string[] _surgePhrases = new string[]
        {
            "Man I feel the best I've felt in a long time!",
            "I finally feel like my old self again.",
            "The air is so clear today, isn't it?",
            "I think I'll go for a walk later, I have so much energy!"
        };

        public void Update()
        {
            ProcessSchedules();
            ProcessSurges();
        }

        private void ProcessSchedules()
        {
            if (_scheduledDeaths.Count == 0) return;

            int currentDay = GameTime.Day;
            for (int i = _scheduledDeaths.Count - 1; i >= 0; i--)
            {
                var sd = _scheduledDeaths[i];
                // If we reached the target day (or passed it due to time skip), trigger the surge
                if (currentDay >= sd.TargetDay)
                {
                    if (Log.IsDebugEnabled) Log.Debug($"Schedule reached for {sd.Member.firstName} (Day {currentDay} >= {sd.TargetDay}). Starting Surge.");
                    // Pass the original target day to ensure death date is recorded correctly even if we skipped time
                    StartSurge(sd.Member, sd.Reason, sd.TargetDay);
                    _scheduledDeaths.RemoveAt(i);
                }
            }
        }

        private void ProcessSurges()
        {
            if (_surgeDeaths.Count == 0) return;

            float currentTime = Time.time;
            for (int i = _surgeDeaths.Count - 1; i >= 0; i--)
            {
                var sd = _surgeDeaths[i];
                if (currentTime >= sd.DieAtTime)
                {
                    if (Log.IsDebugEnabled) Log.Debug($"Surge finished for {sd.Member.firstName}. Executing fatal damage.");
                    ExecuteDeath(sd.Member, sd.Reason, sd.TargetDay);
                    _surgeDeaths.RemoveAt(i);
                }
            }
        }

        public void ProcessDeathRoll(FamilyMember member, int ageWeeks, int elapsedBiologicalWeeks = 1)
        {
            if (member == null || member.isDead || IsDeathPending(member)) return;

            if (Log.IsDebugEnabled) Log.Debug($"Processing death roll for {member.firstName} ({ageWeeks/LifespanConstants.WeeksPerYear}y).");

            // 1. Probability of death increases with age past elder threshold
            int elderWeeks = _config.elderAgeYears * LifespanConstants.WeeksPerYear;
            if (ageWeeks >= elderWeeks)
            {
                float yearsPastElder = (float)(ageWeeks - elderWeeks) / (float)LifespanConstants.WeeksPerYear;
                // Config is now Percentage (0-100), convert to 0-1
                float baseProb = (_config.deathBaseProbability / 100f) + (yearsPastElder * (_config.deathProbabilityIncreasePerYear / 100f));
                
                float healthFactor = 1.0f - LifespanMath.NormalizeHealthFraction(member.health, member.maxHealth);
                // Non-linear HP impact: health increases chance MORE the lower the HP
                float healthImpact = 1.0f + (healthFactor * healthFactor * _config.healthImpactFactor);
                
                float weeklyProb = baseProb * healthImpact * _config.deathProbabilityMultiplier;
                weeklyProb = Mathf.Clamp01(weeklyProb);

                // Convert per-week probability into a multi-week tick probability.
                int rolledWeeks = Math.Max(1, elapsedBiologicalWeeks);
                float finalProb = LifespanMath.ProbabilityAtLeastOnce(weeklyProb, rolledWeeks);

                float roll = _random.Value();
                if (Log.IsDebugEnabled) Log.Debug($"Death Roll for {member.firstName}: {roll:F5} VS Prob: {finalProb:F5} (Weekly: {weeklyProb:F5}, Weeks: {rolledWeeks}, Base: {baseProb:F4}, HP Impact: {healthImpact:F2}, Mult: {_config.deathProbabilityMultiplier}).");

                if (roll < finalProb)
                {
                    if (Log.IsDebugEnabled) Log.Debug($"Death Roll SUCCESS for {member.firstName}.");
                    
                    // Logic: Use "Old age" if very old (80+), otherwise "Natural causes"
                    string reason = (ageWeeks >= 80 * LifespanConstants.WeeksPerYear) ? "Old age" : "Natural causes";
                    
                    // Brief delay 0-6 days to spread out mass deaths
                    int dayOffset = _random.Range(0, SCHEDULE_DEATH_RANDOM_OFFSET_DAYS); 
                    ScheduleDeath(member, reason, dayOffset);
                }
            }
            else
            {
                if (Log.IsDebugEnabled) Log.Debug($"{member.firstName} is below elder age threshold ({ageWeeks/LifespanConstants.WeeksPerYear}y < {_config.elderAgeYears}y).");
            }
        }

        public void QueueDeath(FamilyMember member, string reason)
        {
            // Public API compatibility alias - triggers immediate surge logic
            // Use current day as target since it's immediate
            StartSurge(member, reason, GameTime.Day);
        }

        public void ScheduleDeath(FamilyMember member, string reason)
        {
            // Randomize offset 0-6 days
            int offset = _random.Range(0, SCHEDULE_DEATH_RANDOM_OFFSET_DAYS);
            ScheduleDeath(member, reason, offset);
        }

        public void ScheduleDeath(FamilyMember member, string reason, int dayOffset)
        {
            if (IsDeathPending(member)) return;

            int targetDay = GameTime.Day + dayOffset;
            if (Log.IsDebugEnabled) Log.Debug($"Scheduling death for {member.firstName} on GameDay {targetDay}.");
            
            _scheduledDeaths.Add(new ScheduledDeath
            {
                Member = member,
                Reason = reason,
                TargetDay = targetDay
            });
        }

        private void StartSurge(FamilyMember member, string reason, int targetDay)
        {
            if (member == null || member.isDead) return;

            string phrase = _surgePhrases[_random.Range(0, _surgePhrases.Length)];
            if (Log.IsDebugEnabled) Log.Debug($"Starting Surge for {member.firstName}. Phrase: \"{phrase}\"");

            // Trigger the character's speech bubble
            TriggerSpeech(member, phrase, DialogueScheduler.Priority.Reactive);

            // Use constants
            float delay = _random.Value() * (_config.surgeMaxDelay - _config.surgeMinDelay) + _config.surgeMinDelay;
            _surgeDeaths.Add(new SurgeDeath
            {
                Member = member,
                Reason = reason,
                DieAtTime = Time.time + delay,
                TargetDay = targetDay
            });
        }

        private bool IsDeathPending(FamilyMember member)
        {
            foreach (var sd in _scheduledDeaths)
            {
                if (sd.Member == member) return true;
            }
            foreach (var sd in _surgeDeaths)
            {
                if (sd.Member == member) return true;
            }
            return false;
        }

        private void ExecuteDeath(FamilyMember member, string reason, int targetDay)
        {
            if (member == null || member.isDead) return;

            RecordDeath(member, targetDay);

            int memberId = member.GetId();
            try
            {
                // Temporarily override Day so the game's internal 'CreateObituaryInfo' uses our backdated day
                AgingPatches.SetDeathDayOverride(memberId, targetDay);
                Log.Info($"{member.firstName} has passed away due to {reason} (Ref day: {targetDay}).");

                // Handle Wasteland Radio Death if exploring
                if (member.isAway)
                {
                    if (TryWastelandDeath(member, reason, targetDay))
                    {
                        return; // Death execution deferred to radio callback
                    }
                    Log.Warn($"Radio death failed for {member.firstName}. Falling back to immediate damage.");
                }

                // Apply fatal damage - bypass unconscious phase with wasteland: true
                member.Damage((int)FATAL_DAMAGE_AMOUNT, BaseCharacter.DamageType.Undefined, reason, false, true);

                // 2. Ghost Expedition Cleanup: If they were away but radio failed, still clean up.
                if (member.isAway && ExplorationManager.Instance != null)
                {
                     foreach (var p in ExplorationManager.Instance.GetAllExplorarionParties())
                     {
                         if (p.ContainsFamilyMember(member))
                         {
                             try 
                             { 
                                 Traverse.Create(p).Method("RemoveDeadPartyMembers", new object[] { false }).GetValue(); 
                             } 
                             catch (Exception ex)
                             {
                                 Log.Error($"Party cleanup failed: {ex.Message}");
                             }
                             break;
                         }
                     }
                }
            }
            finally
            {
                // Clear global override
                AgingPatches.ClearDeathDayOverride(memberId);
            }
            
            // Note: We can't set 'dayDied' on member via reflection as the field likely doesn't exist on BaseCharacter/FamilyMember.
            // We rely on CustomDeathDates dictionary instead.

            // Add journal entry
            if (member.isDead)
            {
                if (Log.IsDebugEnabled) Log.Debug("Inserting death journal entry.");
                TriggerJournal($"{member.firstName} has passed away due to {reason}. They will be missed.", DialogueScheduler.Priority.Reactive);
            }
        }

        private bool TryWastelandDeath(FamilyMember member, string reason, int targetDay)
        {
            if (Log.IsDebugEnabled) Log.Debug($"Attempting Radio sequence for {member.firstName}.");
 
            if (ExplorationManager.Instance == null)
            {
                Log.Warn("ExplorationManager.Instance is null! Cannot trigger radio.");
                return false;
            }

            // 1. Find the party
            ExplorationParty party = null;
            var parties = ExplorationManager.Instance.GetAllExplorarionParties();
            if (parties == null) return false;

            foreach (var p in parties)
            {
                if (p.ContainsFamilyMember(member))
                {
                    party = p;
                    break;
                }
            }

            if (party == null) return false;

            // 2. Get Biome Name
            string biomeName = "the wasteland";
            if (party.currentRegion != null)
            {
                try
                {
                    // Use reflection if GetLocalisedName is not accessible, but it appeared public in decomp
                    // If it causes issues, fallback to "this place"
                    var name = party.currentRegion.GetLocalisedName();
                    if (!string.IsNullOrEmpty(name)) biomeName = name;
                }
                catch (Exception ex)
                {
                    if (Log.IsDebugEnabled) Log.Debug($"Could not resolve expedition biome name for death dialogue; using fallback. Error: {ex.Message}");
                }
            }

            // 3. Select Message
            string[] templates = new string[]
            {
                "This {0} is beautiful! I'm going to hang out here for a moment.",
                "The view of {0} is amazing... I'm just going to rest my eyes.",
                "I never thought {0} could look so peaceful. I'll catch up with you later.",
                "Go on without me. {0} is a good place to rest."
            };
            string msg = string.Format(templates[_random.Range(0, templates.Length)], biomeName);

            // 4. Setup Radio Params
            var radioParams = new ExplorationManager.RadioDialogParams();
            radioParams.caller = member;
            radioParams.questionTextId = msg; // Leveraging fallback for raw text
            radioParams.acceptButtonTextId = "Goodbye";
            radioParams.answer1TextId = "Goodbye";
            
            // Find a receiver (someone at home)
            if (InteractionManager.Instance != null)
            {
                for (int i = 0; i < InteractionManager.Instance.GetNumFamilyMembers(); i++)
                {
                    var m = InteractionManager.Instance.GetFamilyMemberByIndex(i);
                    if (!m.isAway && !m.isDead)
                    {
                        radioParams.receiver = m;
                        break;
                    }
                }
            }

            // 5. Define Callback
            radioParams.callback = (response) =>
            {
                Log.Info($"Radio confirmed: {member.firstName} has shared their last words from the wasteland.");
                
                if (targetDay > 0) 
                {
                    AgingPatches.SetDeathDayOverride(member.GetId(), targetDay);
                    RecordDeath(member, targetDay);
                }
                
                member.Damage((int)FATAL_DAMAGE_AMOUNT, BaseCharacter.DamageType.Undefined, reason, false, true);
                
                AgingPatches.ClearDeathDayOverride(member.GetId());

                // 2. Ghost Expedition Cleanup: If they were on a party, ensure the party disbands if empty.
                // This mimics how combat death handles it.
                if (party != null)
                {
                    try
                    {
                         // Use reflection to call the private cleanup method
                         Traverse.Create(party).Method("RemoveDeadPartyMembers", new object[] { false }).GetValue();
                    }
                    catch (Exception ex)
                    {
                         Log.Error($"Radio cleanup failed for {member.firstName}: {ex.Message}");
                    }
                }
                
                // 3. Journal entry
                TriggerJournal($"{member.firstName} has passed away peacefully in the wasteland.", DialogueScheduler.Priority.Reactive);
            };

            radioParams.acceptButtonTextId = "Goodbye";
            radioParams.answer1TextId = "Goodbye";
            radioParams.answer2TextId = "Wait, no!";
            radioParams.rejectButtonTextId = "Wait, no!";

            // 6. Trigger Dialog
            return ExplorationManager.Instance.ShowRadioDialog(radioParams);
        }

        private void RecordDeath(FamilyMember member, int targetDay)
        {
            if (member == null || _deathRecords == null) return;

            int ageWeeks = _ageTracker != null ? _ageTracker.GetAgeWeeks(member) : 0;
            _deathRecords.RecordDeath(member.GetId(), targetDay, ageWeeks);
            Log.Info($"[Lifespan] Recorded death for {member.firstName}. Day: {targetDay}, Age: {ageWeeks / LifespanConstants.WeeksPerYear}y.");
        }
    }
}
