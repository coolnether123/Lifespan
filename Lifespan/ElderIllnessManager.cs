using ModAPI.Core;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    public class ElderIllnessManager
    {
        private readonly LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly AgeTracker _ageTracker;
        private readonly ModRandomStream _random;

        public const string ILLNESS_DEMENTIA = "lifespan.illness.dementia";
        public const string ILLNESS_MILD_DEMENTIA = "lifespan.illness.mild.dementia";
        
        public const string ILLNESS_ARTHRITIS = "lifespan.illness.arthritis";
        public const string ILLNESS_MILD_ARTHRITIS = "lifespan.illness.mild.arthritis";

        public const string ILLNESS_HEART = "lifespan.illness.heart";
        public const string ILLNESS_MILD_HEART = "lifespan.illness.mild.heart";

        public const string ILLNESS_FRAILTY = "lifespan.illness.frailty";
        public const string ILLNESS_MILD_FRAILTY = "lifespan.illness.mild.frailty";

        public const string ILLNESS_RESPIRATORY = "lifespan.illness.respiratory";
        public const string ILLNESS_MILD_RESPIRATORY = "lifespan.illness.mild.respiratory";

        private DeathManager _deathManager;
        private DialogueScheduler _scheduler;
        private IModLogger Log => _log;

        public ElderIllnessManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker, ModRandomStream random)
        {
            _config = config;
            _log = ctx.Log;
            _ageTracker = ageTracker;
            _random = random;
        }

        public void SetDeathManager(DeathManager deathManager)
        {
            _deathManager = deathManager;
        }

        public void SetScheduler(DialogueScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        private void TriggerJournal(string text, DialogueScheduler.Priority priority = DialogueScheduler.Priority.Routine, System.Func<bool> validation = null)
        {
            if (_scheduler != null) _scheduler.Enqueue(null, text, true, priority, validation);
            else if (JournalManager.Instance != null && (validation == null || validation()))
            {
                try { Traverse.Create(JournalManager.Instance).Method("InsertJournalEntry", new object[] { text, "", false }).GetValue(); } catch { }
            }
        }

        private void TriggerSpeech(FamilyMember member, string text, DialogueScheduler.Priority priority = DialogueScheduler.Priority.Routine, System.Func<bool> validation = null)
        {
            if (_scheduler != null) _scheduler.Enqueue(member, text, false, priority, validation);
            else try { if (validation == null || validation()) member.Say(text); } catch { }
        }

        public void ProcessElderIllnessRoll(FamilyMember member, int currentAgeWeeks)
        {
            if (member == null || member.isDead) return;

            Log.Debug($"Processing illness roll for {member.firstName} ({currentAgeWeeks/52}y).");

            // 0. Check for Progression of Mild Illnesses
            CheckProgression(member);

            // 1. Roll for new illness
            float healthFactor = (float)member.health / member.maxHealth;
            
            // Calculate stress factor (Trauma is 0-100 usually)
            float stressFactor = 0f;
            if (member.stats != null && member.stats.trauma != null)
            {
                stressFactor = member.stats.trauma.Value / 100f; // 0.0 to 1.0
            }

            // Calculate fatigue factor (Fatigue is 0-100)
            float fatigueFactor = 0f;
            if (member.stats != null && member.stats.fatigue != null)
            {
                fatigueFactor = member.stats.fatigue.Value / 100f; // 0.0 to 1.0
            }

            // Base chance (now much lower, e.g. 0.001)
            float baseChance = _config.elderIllnessBaseChance;
            
            // Multipliers
            // Health: 1.0 (full health) -> 3.0 (near death)
            float healthMult = 1.0f + (2.0f * (1.0f - healthFactor));
            
            // Stress: 1.0 (calm) -> 2.5 (max stress)
            float stressMult = 1.0f + (1.5f * stressFactor);
            
            // Fatigue: 1.0 (rested) -> 1.5 (exhausted)
            float fatigueMult = 1.0f + (0.5f * fatigueFactor);

            float acquiredChance = baseChance * healthMult * stressMult * fatigueMult;

            float rollValue = _random.Value();
            Log.Debug($"{member.firstName} Roll: {rollValue:F4} VS Chance: {acquiredChance:F4} (H:{healthMult:F1}x S:{stressMult:F1}x F:{fatigueMult:F1}x).");

            if (rollValue < acquiredChance)
            {
                Log.Debug($"Roll SUCCESS for {member.firstName}. Choosing illness...");
                AcquireRandomIllness(member);
            }

            // 2. Process existing illness effects
            ApplyOngoingEffects(member);
        }

        private void CheckProgression(FamilyMember member)
        {
            int currentWeek = _ageTracker.GetAgeWeeks(member);
            var illnesses = _ageTracker.GetIllnesses(member);
            var toUpgrade = new List<string>();

            // Check timing for each mild illness
            foreach (var ill in illnesses)
            {
                if (!ill.Contains(".mild.")) continue;

                int targetWeek = _ageTracker.GetOnsetWeek(member, ill);
                
                // If timing logic hasn't been initialized for this (legacy safe), generate it now
                if (targetWeek <= 0)
                {
                    targetWeek = CalculateNextStageWeek(member, currentWeek);
                    _ageTracker.SetOnsetWeek(member, ill, targetWeek);
                    Log.Debug($"Initialized legacy onset for {member.firstName} ({ill}). Target: {targetWeek/52}y.");
                }

                if (currentWeek >= targetWeek)
                {
                    toUpgrade.Add(ill);
                }
            }

            foreach (var mild in toUpgrade)
            {
                string severe = mild.Replace(".mild.", ".");
                Log.Info($"{member.firstName}'s condition has worsened: {GetIllnessName(mild)} -> {GetIllnessName(severe)}");
                
                _ageTracker.RemoveIllness(member, mild);
                _ageTracker.AddIllness(member, severe);

                ApplyInitialEffect(member, severe);

                // Notifications
                if (JournalManager.Instance != null && InteractionManager.Instance != null)
                {
                    // 1. Victim Reaction
                    string victimLine = GetFlavorDialogue(member, severe);
                    TriggerSpeech(member, victimLine, DialogueScheduler.Priority.Reactive, () => _ageTracker.GetIllnesses(member).Contains(severe));
                    TriggerJournal($"Condition Worsened: {member.firstName} now has {GetIllnessName(severe)}.", DialogueScheduler.Priority.Reactive, () => _ageTracker.GetIllnesses(member).Contains(severe));

                    // 2. Observer Reactions (Throttled: 1-6 people)
                    TriggerObserverReactions(member, severe);
                }
            }
        }

        private void TriggerObserverReactions(FamilyMember victim, string illnessId)
        {
            if (FamilyManager.Instance == null) return;
            var allMembers = FamilyManager.Instance.GetAllFamilyMembers();
            if (allMembers == null) return;

            // Filter potential observers (alive, not the victim, not an infant)
            var observers = new List<FamilyMember>();
            foreach (var m in allMembers)
            {
                if (m != null && !m.isDead && m.GetId() != victim.GetId() && !m.isChild) 
                {
                    observers.Add(m);
                }
            }

            if (observers.Count == 0) return;

            // Pick 1 to 3 observers randomly
            if (observers.Count == 0) return;

            // Pick 1 to 6 observers randomly, aiming for mean 3
            // Weighted Logic:
            // Base: 2 observers (Always)
            // +1 (75% chance) -> Base effectively ~2.75
            // +1 if Health < 50% (60% chance) 
            // +1 if Stress > 50% (60% chance)
            // +1 Rare Chance (20% chance)
            
            int countToPick = 2;

            if (_random.Value() < 0.75f) countToPick++;

            float healthFactor = (float)victim.health / victim.maxHealth;
            if (healthFactor < 0.5f && _random.Value() < 0.6f) countToPick++;

            if (victim.stats != null && victim.stats.trauma.Value > 50f && _random.Value() < 0.6f) countToPick++;
            
            if (_random.Value() < 0.20f) countToPick++;

            // Clamp
            countToPick = Math.Min(countToPick, observers.Count);
            // Cap at 6 manually
            if (countToPick > 6) countToPick = 6;

            // Shuffle list roughly
            for (int i = 0; i < observers.Count; i++)
            {
                var temp = observers[i];
                int randomIndex = _random.Range(i, observers.Count);
                observers[i] = observers[randomIndex];
                observers[randomIndex] = temp;
            }

            for (int i = 0; i < countToPick; i++)
            {
                var observer = observers[i];
                string line = GetObservationDialogue(observer, victim, illnessId);
                TriggerSpeech(observer, line, DialogueScheduler.Priority.Reactive, () => _ageTracker.GetIllnesses(victim).Contains(illnessId) && !victim.isDead);
            }
        }

        private int CalculateNextStageWeek(FamilyMember member, int currentWeek)
        {
             // 1. Base Duration: Random between Min/Max years
             int minWeeks = _config.illnessStageMinYears * 52;
             int maxWeeks = _config.illnessStageMaxYears * 52;
             
             // Weighted random towards the middle
             int duration = _random.Range(minWeeks, maxWeeks);

             // 2. Modifiers: Bad condition shortens the fuse
             float healthFactor = (float)member.health / member.maxHealth; // 1.0 = good
             float stressFactor = 0f;
             if (member.stats?.trauma != null) stressFactor = member.stats.trauma.Value / 100f; // 1.0 = bad
             
             // If healthy, extending duration (+20%)
             // If dying/stressed, reducing duration (-40%)
             float modifier = 1.0f;
             
             if (healthFactor < 0.5f) modifier -= 0.2f;
             if (stressFactor > 0.5f) modifier -= 0.2f;

             // Smart Trait checks
             if (member.traits != null)
             {
                 try
                 {
                     if (member.traits.HasWeakness(Traits.Weakness.Lazy)) modifier -= 0.1f; 
                 }
                 catch {} // Swallow errors from uninitialized traits in tests/edge cases
             }
             
             if (member.BaseStats != null && member.BaseStats.Dexterity != null && member.BaseStats.Dexterity.Level >= 12) modifier += 0.2f;

             duration = (int)(duration * modifier);
             if (duration < 4) duration = 4; // Absolute min 1 month

             return currentWeek + duration;
        }

        private void AcquireRandomIllness(FamilyMember member)
        {
            List<string> possible = new List<string>();
            var current = _ageTracker.GetIllnesses(member);

            if (_config.enableDementia && !current.Contains(ILLNESS_MILD_DEMENTIA) && !current.Contains(ILLNESS_DEMENTIA)) possible.Add(ILLNESS_MILD_DEMENTIA);
            if (_config.enableArthritis && !current.Contains(ILLNESS_MILD_ARTHRITIS) && !current.Contains(ILLNESS_ARTHRITIS)) possible.Add(ILLNESS_MILD_ARTHRITIS);
            if (_config.enableHeartDisease && !current.Contains(ILLNESS_MILD_HEART) && !current.Contains(ILLNESS_HEART)) possible.Add(ILLNESS_MILD_HEART);
            if (_config.enableFrailty && !current.Contains(ILLNESS_MILD_FRAILTY) && !current.Contains(ILLNESS_FRAILTY)) possible.Add(ILLNESS_MILD_FRAILTY);
            if (_config.enableRespiratory && !current.Contains(ILLNESS_MILD_RESPIRATORY) && !current.Contains(ILLNESS_RESPIRATORY)) possible.Add(ILLNESS_MILD_RESPIRATORY);
            
            if (possible.Count > 0)
            {
                string picked = possible[_random.Range(0, possible.Count)];
                Log.Info($"{member.firstName} has developed symptoms of {GetIllnessName(picked)}.");
                _ageTracker.AddIllness(member, picked);
                
                // Initialize Stage Timing based on current condition
                int currentWeek = _ageTracker.GetAgeWeeks(member);
                int targetWeek = CalculateNextStageWeek(member, currentWeek);
                _ageTracker.SetOnsetWeek(member, picked, targetWeek);
                Log.Debug($"{GetIllnessName(picked)} contracted. Stage 2 Target: Week {targetWeek} (in {(targetWeek-currentWeek)/52f:F1}y).");

                ApplyInitialEffect(member, picked);
                
                if (JournalManager.Instance != null)
                {
                    string flavor = GetFlavorDialogue(member, picked);
                    TriggerSpeech(member, flavor, DialogueScheduler.Priority.Reactive, () => _ageTracker.GetIllnesses(member).Contains(picked));
                    TriggerJournal($"{member.firstName} is showing signs of {GetIllnessName(picked)}.", DialogueScheduler.Priority.Reactive, () => _ageTracker.GetIllnesses(member).Contains(picked));
                }
            }
            else
            {
                Log.Debug($"{member.firstName} already has all possible illnesses (or mild variants).");
            }
        }

        public void ReapplyModifiers(FamilyMember member)
        {
            if (member == null || member.isDead) return;
            var illnesses = _ageTracker.GetIllnesses(member);
            if (illnesses.Count > 0)
            {
                Log.Debug($"Re-applying {illnesses.Count} modifiers for {member.firstName}.");
                foreach (var id in illnesses)
                {
                    ApplyInitialEffect(member, id, true);
                }
            }
        }

        private void ApplyInitialEffect(FamilyMember member, string illnessId, bool silent = false)
        {
            Log.Debug($"Applying effect: {GetIllnessName(illnessId)} -> {member.firstName}.");
            switch (illnessId)
            {
                case ILLNESS_ARTHRITIS:
                    // Only full arthritis gives traits
                     if (member.traits != null && !member.traits.HasWeakness(Traits.Weakness.Lazy))
                     {
                         member.traits.AddWeakness(Traits.Weakness.Lazy, true);
                     }
                    break;
                // Mild ones apply stat modifiers dynamically in Harmony patches usually, or light on-add logic here
                case ILLNESS_MILD_ARTHRITIS:
                     // No trait, maybe just a hidden flag handled by stats patch
                     break;
            }
        }

        private void ApplyOngoingEffects(FamilyMember member)
        {
            var illnesses = _ageTracker.GetIllnesses(member);
            foreach (var id in illnesses)
            {
                if (id == ILLNESS_HEART)
                {
                    float roll = _random.Value();
                    if (roll < _config.heartDiseaseAttackChance / 100f) // Divided by 100f
                    {
                        TriggerHeartAttack(member);
                    }
                }
                // Mild Heart = Palpitations? (Less severe, maybe just panic)
                else if (id == ILLNESS_MILD_HEART)
                {
                    if (_random.Value() < 0.05f) // Rare minor event
                    {
                        TriggerJournal($"{member.firstName} felt a confusing flutter in their chest.");
                    }
                }
            }
        }

        private void TriggerHeartAttack(FamilyMember member)
        {
            Log.Info($"HEART ATTACK! {member.firstName} is having a coronary event.");
            
            bool willKill = member.health <= (int)_config.heartAttackDamage;

            if (willKill && _deathManager != null)
            {
                Log.Info($"{member.firstName}'s heart has stopped.");
                _deathManager.ScheduleDeath(member, "Heart Failure");
            }
            else
            {
                Log.Warn($"{member.firstName} survived a heart attack.");
                member.Damage((int)_config.heartAttackDamage, BaseCharacter.DamageType.Undefined, "Heart Attack");
            }
            
            if (JournalManager.Instance != null)
            {
                TriggerJournal($"{member.firstName} suffered a heart attack!", DialogueScheduler.Priority.Reactive, () => !member.isDead);
            }
        }

        private string GetIllnessName(string id)
        {
            switch (id)
            {
                case ILLNESS_DEMENTIA: return "Severe Dementia";
                case ILLNESS_MILD_DEMENTIA: return "Early Onset Dementia";
                
                case ILLNESS_ARTHRITIS: return "Crippling Arthritis";
                case ILLNESS_MILD_ARTHRITIS: return "Joint Pain";
                
                case ILLNESS_HEART: return "Heart Disease";
                case ILLNESS_MILD_HEART: return "Arrhythmia";
                
                case ILLNESS_FRAILTY: return "Frailty";
                case ILLNESS_MILD_FRAILTY: return "General Weakness";
                
                case ILLNESS_RESPIRATORY: return "Lung Failure";
                case ILLNESS_MILD_RESPIRATORY: return "Chronic Cough";
                
                default: return "Unknown Condition";
            }
        }

        private string GetFlavorDialogue(FamilyMember victim, string illnessId)
        {
            var options = new List<string>();
            switch (illnessId)
            {
                case ILLNESS_MILD_DEMENTIA:
                    options.Add("I... I can't remember where I put my rations.");
                    options.Add("Did we just eat? Or was that yesterday?");
                    options.Add("Everyone's faces look so... blurry today.");
                    options.Add("I swear I heard the radio, but it's off.");
                    options.Add("Why did I come into this room again?");
                    options.Add("Something feels missing, but I don't know what.");
                    options.Add("The lights flicker, but maybe it's just my eyes.");
                    options.Add("I could have sworn I saw someone standing by the door.");
                    options.Add("My thoughts feel like they're slipping through my fingers.");
                    options.Add("Is it Tuesday? It feels like it should be Tuesday.");
                    break;
                case ILLNESS_DEMENTIA:
                    options.Add("Who are you people? Why are we underground?");
                    options.Add("I need to go home. My mother is waiting.");
                    options.Add("The walls... they're breathing again.");
                    options.Add("I don't know what this tool is for anymore.");
                    options.Add("The voices... they aren't making sense!");
                    options.Add("I want to leave! Open the door!");
                    options.Add("I'm late for work... I need to find my keys.");
                    options.Add("Don't look at me like that, I know who you're hiding!");
                    options.Add("The shadows... they're whispering about the surface.");
                    options.Add("Where is my bed? This isn't my room.");
                    break;
                case ILLNESS_MILD_ARTHRITIS:
                    options.Add("Ouch! My knees aren't what they used to be.");
                    options.Add("Cold damp air... goes right to the bone.");
                    options.Add("Just need a minute... stiff joints.");
                    options.Add("Can someone help me with this lid? My hands hurt.");
                    options.Add("Feels like rain coming... my ankles are throbbing.");
                    options.Add("I'm just a bit creaky this morning.");
                    options.Add("I'm moving like a rusty hinge today.");
                    options.Add("Every step feels like walking on gravel.");
                    options.Add("My back... it's just one long ache.");
                    options.Add("I need to sit for a bit, my hips are on fire.");
                    break;
                case ILLNESS_ARTHRITIS:
                    options.Add("I can't... I can't move my fingers.");
                    options.Add("The pain... it never stops.");
                    options.Add("I'm useless like this. I can't even stand.");
                    options.Add("My bones feel like they're grinding to dust.");
                    options.Add("Everything is locked up tight.");
                    options.Add("Please... make the pain stop.");
                    options.Add("My joints are swollen to twice their size.");
                    options.Add("I can't even hold a cup of water anymore.");
                    options.Add("Every movement is a battle I'm losing.");
                    options.Add("I feel like my bones are fusing together.");
                    break;
                case ILLNESS_MILD_HEART:
                    options.Add("My chest feels... tight.");
                    options.Add("Just a bit out of breath, give me a second.");
                    options.Add("Is it hot in here? I feel flushed.");
                    options.Add("Strange fluttering feeling...");
                    options.Add("Need to... catch my breath.");
                    options.Add("Everything is spinning a little.");
                    options.Add("My chest feels like it's in a vice.");
                    options.Add("I can feel my heart pounding against my ribs.");
                    options.Add("I'm seeing spots... just need a moment.");
                    options.Add("Why is it so hard to get a full breath?");
                    break;
                case ILLNESS_HEART:
                    options.Add("It feels like an elephant is sitting on my chest.");
                    options.Add("My left arm... it's going numb.");
                    options.Add("I can hear my own heartbeat in my ears.");
                    options.Add("I'm dizzy... I need to sit down immediately.");
                    options.Add("Call for help... can't breathe properly.");
                    options.Add("My heart feels like it's exploding.");
                    options.Add("It's a heavy weight... crushing me.");
                    options.Add("My jaw... why does my jaw hurt so much?");
                    options.Add("The room is spinning... I can't see straight.");
                    options.Add("Everything is going dark... help...");
                    break;
                case ILLNESS_MILD_FRAILTY:
                    options.Add("This pack feels heavier than it did yesterday.");
                    options.Add("I bruise so easily these days.");
                    options.Add("Just feeling a bit weak, that's all.");
                    options.Add("I stepped wrong and twisted something.");
                    options.Add("My grip isn't what it used to be.");
                    options.Add("Why is everything so heavy?");
                    options.Add("I feel like I'm losing weight without trying.");
                    options.Add("My skin looks so thin, like paper.");
                    options.Add("I'm exhausted after just a few steps.");
                    options.Add("I feel... diminished somehow.");
                    break;
                case ILLNESS_FRAILTY:
                    options.Add("I feel like I'm made of glass.");
                    options.Add("I don't have the strength to lift that.");
                    options.Add("My legs are shaking just standing here.");
                    options.Add("I'm wasting away... look at my arms.");
                    options.Add("I feel like a stiff breeze would break me.");
                    options.Add("Help me up... I can't do it.");
                    options.Add("I can barely support my own weight.");
                    options.Add("My muscles... they're just gone.");
                    options.Add("I'm cold... so very cold.");
                    options.Add("I'm just a ghost of what I was.");
                    break;
                case ILLNESS_MILD_RESPIRATORY:
                    options.Add("*Cough* Just a tickle in my throat.");
                    options.Add("It's... *wheeze*... dusty in here.");
                    options.Add("I get winded just walking up the stairs.");
                    options.Add("Can we turn up the ventilation?");
                    options.Add("Trying to clear my throat... won't go away.");
                    options.Add("Just a little shortness of breath.");
                    options.Add("My chest is whistling with every breath.");
                    options.Add("I can't seem to clear the congestion.");
                    options.Add("I'm always searching for more air.");
                    options.Add("This air feels so thin and heavy.");
                    break;
                case ILLNESS_RESPIRATORY:
                    options.Add("I can't... *gasp*... breathe.");
                    options.Add("It feels like drowning... on dry land.");
                    options.Add("*Violent coughing fit*");
                    options.Add("My lungs... burning...");
                    options.Add("Air... need... more... air.");
                    options.Add("Every breath hurts.");
                    options.Add("I'm drowning... help me...");
                    options.Add("My throat is closing...");
                    options.Add("I can't speak... no air...");
                    options.Add("It's like breathing through a straw.");
                    break;
                default:
                    options.Add("I don't feel quite right.");
                    options.Add("Age catches up to us all.");
                    options.Add("Just getting old, I suppose.");
                    options.Add("My body is failing me.");
                    options.Add("Days are getting harder.");
                    options.Add("Just need some rest.");
                    break;
            }
            return PickLineWithAntiRepetition($"Flavor_{illnessId}", options);
        }

         // Called by external systems (UI patches) to add flavor
        public string GetObservationDialogue(FamilyMember observer, FamilyMember victim, string illnessId)
        {
             var options = new List<string>();
             // Only if they are distinct people
             if (observer == null || victim == null || observer.GetId() == victim.GetId()) return "...";

             switch (illnessId)
             {
                 case ILLNESS_MILD_DEMENTIA:
                 case ILLNESS_DEMENTIA:
                     options.Add($"{victim.firstName} has been staring at walls lately.");
                     options.Add("I think they're forgetting things significantly.");
                     options.Add($"{victim.firstName} called me by the wrong name.");
                     options.Add("The lights are on but nobody's home with them.");
                     options.Add($"Did {victim.firstName} ask you that same question twice?");
                     options.Add("They look so lost lately.");
                     options.Add($"{victim.firstName} is talking to people who aren't there.");
                     options.Add("I found them wandering in the storage room again.");
                     options.Add($"{victim.firstName} doesn't seem to know where they are.");
                     options.Add("It's getting harder to reach them.");
                     break;
                case ILLNESS_MILD_ARTHRITIS:
                case ILLNESS_ARTHRITIS:
                     options.Add($"{victim.firstName} is really struggling to move.");
                     options.Add("We should let them rest, their joints are swollen.");
                     options.Add("It hurts just watching them walk.");
                     options.Add($"{victim.firstName} can barely hold a wrench.");
                     options.Add($"I can hear {victim.firstName}'s knees popping from here.");
                     options.Add("The cold floor isn't good for them.");
                     options.Add($"{victim.firstName} is rubbing their hands constantly.");
                     options.Add("They're wincing with every step today.");
                     options.Add($"{victim.firstName} is much slower than last week.");
                     options.Add("I think the arthritis is winning.");
                     break;
                case ILLNESS_MILD_HEART:
                case ILLNESS_HEART:
                     options.Add($"{victim.firstName} is clutching their chest.");
                     options.Add("They look pale and sweaty.");
                     options.Add("We need to keep {victim.firstName}'s stress down.");
                     options.Add("They're breathing irregularly.");
                     options.Add($"{victim.firstName} needs to sit down, now.");
                     options.Add("Their pulse is all over the place.");
                     options.Add($"{victim.firstName} looks like they've seen a ghost.");
                     options.Add("They're gasping for air after the slightest effort.");
                     options.Add($"{victim.firstName}'s lips look a bit blue.");
                     options.Add("Something is very wrong with their heart.");
                     break;
                case ILLNESS_MILD_FRAILTY:
                case ILLNESS_FRAILTY:
                     options.Add($"{victim.firstName} looks thinner every day.");
                     options.Add("They almost fell over just now.");
                     options.Add("Don't let them carry heavy loads.");
                     options.Add("They look so fragile.");
                     options.Add($"{victim.firstName} is fading away.");
                     options.Add("I'm worried they'll break something.");
                     options.Add($"{victim.firstName} is trembling just standing there.");
                     options.Add("They can't even lift a water bucket anymore.");
                     options.Add($"{victim.firstName} is skin and bones.");
                     options.Add("They have no strength left.");
                     break;
                case ILLNESS_MILD_RESPIRATORY:
                case ILLNESS_RESPIRATORY:
                     options.Add($"{victim.firstName}'s coughing is keeping me awake.");
                     options.Add("Their breathing sounds like a rattle.");
                     options.Add("Is there blood in that handkerchief?");
                     options.Add("They're gasping for air constantly.");
                     options.Add("That cough sounds deep in the chest.");
                     options.Add("Oxygen... they need more air.");
                     options.Add($"{victim.firstName} is wheezing with every single breath.");
                     options.Add("Their chest is heaving just to get a little air.");
                     options.Add("That sound... it sounds like fluid.");
                     options.Add("I don't think their lungs have long left.");
                     break;
                 default:
                     // General Aging - Check context for specific flavor
                     
                     // Context 1: Just turned Elder (early in elderhood)?
                     int age = _ageTracker.GetAgeWeeks(victim);
                     int elderThreshold = _config.elderAgeYears * 52;
                     // e.g. within first year of being elder
                     if (age >= elderThreshold && age < elderThreshold + 52) 
                     {
                         options.Add($"{victim.firstName} moves a lot slower than they used to.");
                         options.Add($"We should start giving {victim.firstName} the lighter shifts.");
                     }

                     // Context 2: High Fatigue?
                     if (victim.stats != null && victim.stats.fatigue.Value > 70)
                     {
                         options.Add($"{victim.firstName} needs more sleep these days.");
                         options.Add($"This bunker life is aging {victim.firstName} faster than normal.");
                     }

                     // Context 3: Low Stats (Strength/Dex)? -> Expedition concern
                     bool lowStats = false;
                     if (victim.BaseStats != null)
                     {
                         if (victim.BaseStats.Strength.Level < 5 || victim.BaseStats.Dexterity.Level < 5) lowStats = true;
                     }

                     if (lowStats)
                     {
                         options.Add($"Are we sure {victim.firstName} can still handle expeditions?");
                         options.Add($"If we have to run, {victim.firstName} isn't going to make it.");
                     }
                     
                     // General Fallbacks (always available)
                     options.Add($"I miss the energy {victim.firstName} used to have.");
                     options.Add($"At least {victim.firstName} is still here with us.");
                     options.Add($"{victim.firstName} clearly isn't well.");
                     options.Add($"We need to take care of {victim.firstName}.");
                     options.Add("It's sad watching them decline.");
                     options.Add("I hope I don't get like that.");
                     options.Add($"{victim.firstName} has seen better days.");
                     options.Add($"We should respect {victim.firstName}'s experience, even if they're slowing down.");
                     break;
             }
             return PickLineWithAntiRepetition($"Observe_{illnessId}", options);
        }
    
        public List<string> GetActiveIllnesses(FamilyMember member) => _ageTracker.GetIllnesses(member);


        public void AddIllnessExternal(FamilyMember member, string id)
        {
            Log.Debug($"ElderIllnessManager: AddIllnessExternal called for {id} on {member.firstName}.");
            _ageTracker.AddIllness(member, id);
            ApplyInitialEffect(member, id);
        }

        public void RemoveIllnessExternal(FamilyMember member, string id)
        {
            Log.Debug($"ElderIllnessManager: RemoveIllnessExternal called for {id} on {member.firstName}.");
            _ageTracker.RemoveIllness(member, id);
        }
        // Key: Dialogue Context (e.g. "Flavor_MildDementia"), Value: List of hash codes of used lines
        private Dictionary<string, HashSet<int>> _dialogueHistory = new Dictionary<string, HashSet<int>>();

        private string PickLineWithAntiRepetition(string contextKey, List<string> options)
        {
            if (options == null || options.Count == 0) return "...";

            if (!_dialogueHistory.ContainsKey(contextKey))
            {
                _dialogueHistory[contextKey] = new HashSet<int>();
            }

            var used = _dialogueHistory[contextKey];
            
            // If all options have been used, reset the bag
            if (used.Count >= options.Count)
            {
                used.Clear();
            }

            // Filter available
            var available = new List<string>();
            foreach (var opt in options)
            {
                if (!used.Contains(opt.GetHashCode()))
                {
                    available.Add(opt);
                }
            }

            // Fallback (shouldn't happen due to reset check, but safety)
            if (available.Count == 0) available = options;

            string picked = available[_random.Range(0, available.Count)];
            used.Add(picked.GetHashCode());

            return picked;
        }
    }
}
