using ModAPI.Core;
using HarmonyLib;
using System;
using System.Collections.Generic;

namespace Lifespan
{
    /// <summary>
    /// Handles major life milestones, birthdays, and philosophical dialogue for survivors.
    /// Integrated with the Journal and character speech systems.
    /// </summary>
    public class MilestoneManager
    {
        private readonly LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly AgeTracker _ageTracker;
        private readonly ModRandomStream _random;
        private DialogueScheduler _scheduler;

        // Tracks triggered milestones to avoid spamming the same event multiple times per age interval
        private readonly Dictionary<int, HashSet<string>> _triggeredMilestones = new Dictionary<int, HashSet<string>>();
        private readonly Dictionary<int, int> _lastBirthdayYear = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _lastSpeechLine = new Dictionary<int, int>();
        private IModLogger Log => _log;

        public MilestoneManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker, ModRandomStream random)
        {
            _config = config;
            _log = ctx.Log;
            _ageTracker = ageTracker;
            _random = random;
        }

        public void SetScheduler(DialogueScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        private void TriggerSpeech(FamilyMember member, string text, DialogueScheduler.Priority priority = DialogueScheduler.Priority.Routine, System.Func<bool> validation = null)
        {
            if (_scheduler != null) _scheduler.Enqueue(member, text, false, priority, validation);
            else try { if (validation == null || validation()) member.Say(text); } catch { }
        }

        private void TriggerJournal(string text, DialogueScheduler.Priority priority = DialogueScheduler.Priority.Routine, System.Func<bool> validation = null)
        {
            if (!_config.enableJournalEntries) return;
            if (_scheduler != null) _scheduler.Enqueue(null, text, true, priority, validation);
            else if (validation == null || validation()) InsertJournalEntry(text);
        }

        public void ProcessMilestones(FamilyMember member, int ageWeeks)
        {
            if (member == null || member.isDead) return;

            int ageYears = ageWeeks / 52;
            int memberId = member.GetId();

            if (!_triggeredMilestones.ContainsKey(memberId))
                _triggeredMilestones[memberId] = new HashSet<string>();

            // 1. Birthday Check (Journal Entry)
            if (!_lastBirthdayYear.ContainsKey(memberId))
                _lastBirthdayYear[memberId] = ageYears - 1;

            if (ageYears > _lastBirthdayYear[memberId])
            {
                _lastBirthdayYear[memberId] = ageYears;
                
                // Only write odd/even major years to the journal to reduce clutter, or if it's a specific milestone
                bool isMajorMilestone = (ageYears == 18 || ageYears == 21 || ageYears == 30 || ageYears == 40 || ageYears == 50 || ageYears == 60 || ageYears == 70 || ageYears >= 80);
                if (isMajorMilestone || ageYears == 5 || ageYears == 10 || ageYears == 15)
                {
                    TriggerBirthdayJournal(member, ageYears);
                }
                else
                {
                    // Routine birthdays (speech bubbles)
                    // 30% chance to say something to avoid spamming every single year
                    if (_random.Value() < 0.3f)
                    {
                         TriggerRoutineBirthdaySpeech(member, ageYears);
                    }
                }
            }

            // 2. Age-Specific Milestones (Childhood - Now Speech Bubbles to reduce journal spam)
            if (member.isChild)
            {
                if (ageYears == 5) TryTriggerUnique(member, "ChildAge5", TriggerAge5Milestone);
                if (ageYears == 10) TryTriggerUnique(member, "ChildAge10", TriggerAge10Milestone);
                if (ageYears == 15) TryTriggerUnique(member, "ChildAge15", TriggerAge15Milestone);
            }

            // 3. Elder Philosophy (Speech Bubbles - Random Chance)
            if (ageYears >= _config.elderAgeYears)
            {
                if (_random.Value() < 0.06f)
                {
                    TriggerElderPhilosophy(member);
                }
            }
        }

        private void TryTriggerUnique(FamilyMember member, string key, Action<FamilyMember> action)
        {
            int memberId = member.GetId();
            
            // Ensure the dictionary entry exists
            if (!_triggeredMilestones.ContainsKey(memberId))
                _triggeredMilestones[memberId] = new HashSet<string>();
            
            if (!_triggeredMilestones[memberId].Contains(key))
            {
                action(member);
                _triggeredMilestones[memberId].Add(key);
            }
        }

        public void ReportFirstSkill(FamilyMember member)
        {
            if (member == null || !member.isChild) return;
            TryTriggerUnique(member, "FirstSkill", TriggerFirstSkillMessage);
        }

        #region Trigger Logic

        private void TriggerBirthdayJournal(FamilyMember member, int ageYears)
        {
            var options = new List<string>();
            switch (ageYears)
            {
                case 18:
                    options.Add($"{member.firstName} turned {ageYears} and is officially an adult now.");
                    options.Add($"Happy {ageYears}th birthday to {member.firstName}, who is no longer a child.");
                    options.Add($"{member.firstName} reached {ageYears} years old today. They have grown into a capable adult.");
                    break;
                case 25:
                    options.Add($"{member.firstName} reached {ageYears} years old. They are in the prime of their life.");
                    options.Add($"Happy birthday to {member.firstName} for their {ageYears}th year.");
                    options.Add($"{member.firstName} has been with us for {ageYears} years now.");
                    break;
                case 40:
                    options.Add($"{member.firstName} turned {ageYears} today. They have shared four decades of life with us.");
                    options.Add($"Celebrating the {ageYears}th birthday for {member.firstName}.");
                    options.Add($"{member.firstName} reached {ageYears} years of age. Their experience is invaluable.");
                    break;
                case 50:
                    options.Add($"{member.firstName} has reached {ageYears} years old. Half a century lived.");
                    options.Add($"Happy {ageYears}th birthday, {member.firstName}. What a journey.");
                    options.Add($"Celebrating {member.firstName} and their {ageYears} years of memories today.");
                    break;
                case 60:
                    options.Add($"{member.firstName} is {ageYears} today and has reached elder status.");
                    options.Add($"Happy {ageYears}th birthday to {member.firstName}.");
                    options.Add($"Sixty years for {member.firstName}. What a milestone.");
                    break;
                case 70:
                    options.Add($"{member.firstName} reached {ageYears} years old. A remarkable achievement.");
                    options.Add($"Happy birthday to {member.firstName} on their {ageYears}th celebration.");
                    options.Add($"{member.firstName} turned {ageYears} today. They are a legend in this bunker.");
                    break;
                case 80:
                    options.Add($"{member.firstName} is {ageYears} years old now. Our oldest survivor.");
                    options.Add($"Happy {ageYears}th birthday to {member.firstName}. Truly extraordinary.");
                    break;
                case 100:
                    options.Add($"{member.firstName} is {ageYears} years old. A full century.");
                    options.Add($"Happy {ageYears}th birthday, {member.firstName}. Unprecedented.");
                    break;
                default:
                    options.Add($"{member.firstName} reached {ageYears} years old today.");
                    options.Add($"Happy {ageYears}th birthday, {member.firstName}!");
                    options.Add($"{member.firstName} is now {ageYears} years of age.");
                    break;
            }

            if (options.Count > 0)
            {
                TriggerJournal(options[_random.Range(0, options.Count)], DialogueScheduler.Priority.Reactive, () => _ageTracker.GetAgeWeeks(member) / 52 == ageYears);
            }
        }

        private FamilyMember GetObserver(FamilyMember excluded)
        {
            if (FamilyManager.Instance == null) return null;
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return null;

            List<FamilyMember> candidates = new List<FamilyMember>();
            foreach (var m in members)
            {
                if (m != null && !m.isDead && m.GetId() != excluded.GetId())
                    candidates.Add(m);
            }

            if (candidates.Count == 0) return null;
            return candidates[_random.Range(0, candidates.Count)];
        }

        private void TriggerAge5Milestone(FamilyMember member)
        {
            var observer = GetObserver(member);
            if (observer == null) return;

            int ageYears = _ageTracker.GetAgeWeeks(member) / 52;
            string[] lines = {
                $"{member.firstName} is getting so big at {ageYears} years old.",
                "They are learning so fast now.",
                $"{member.firstName} seems to understand everything we say!",
                "I can't believe how quickly they are growing.",
                $"{member.firstName} has such curiosity about the world."
            };
            TriggerSpeech(observer, lines[_random.Range(0, lines.Length)], DialogueScheduler.Priority.Routine, () => _ageTracker.GetAgeWeeks(member) / 52 == ageYears);
        }

        private void TriggerAge10Milestone(FamilyMember member)
        {
            var observer = GetObserver(member);
            if (observer == null) return;

            string[] lines = {
                $"{member.firstName} is growing up. Ten years already.",
                "Pretty soon they will be helping out more.",
                $"{member.firstName} is developing real skills now.",
                "It feels like they were just born, and now look at them at 10 years old."
            };
            TriggerSpeech(observer, lines[_random.Range(0, lines.Length)], DialogueScheduler.Priority.Routine, () => _ageTracker.GetAgeWeeks(member) / 52 == 10);
        }

        private void TriggerAge15Milestone(FamilyMember member)
        {
            var observer = GetObserver(member);
            if (observer == null) return;

            string[] lines = {
                $"{member.firstName} won't be a child much longer. 15 years already.",
                "Time flies. They will be an adult before we know it.",
                $"{member.firstName} is almost ready for the real world.",
                "I barely recognize the 15 year old they are becoming."
            };
            TriggerSpeech(observer, lines[_random.Range(0, lines.Length)], DialogueScheduler.Priority.Routine, () => _ageTracker.GetAgeWeeks(member) / 52 == 15);
        }

        private void TriggerFirstSkillMessage(FamilyMember member)
        {
            var observer = GetObserver(member);
            if (observer == null) return;

            string[] lines = {
                $"{member.firstName} finally figured out how to use that tool!",
                "Look at them go!",
                $"{member.firstName} managed to complete a real task today.",
                $"I am so proud of {member.firstName} for learning that."
            };
            TriggerSpeech(observer, lines[_random.Range(0, lines.Length)]);
        }

        private void TriggerElderPhilosophy(FamilyMember member)
        {
            int category = _random.Range(0, 4);
            string[] lines;

            switch (category)
            {
                case 0: // Gratitude
                    lines = new[] {
                        "I have lived a long life. I am grateful for my time here.",
                        "Every day is a gift, even down here in the bunker.",
                        "I never thought I would see so many seasons pass.",
                        "Looking back, I have had a good run.",
                        "Grateful doesn't even begin to cover it."
                    };
                    break;
                case 1: // Life Lessons
                    lines = new[] {
                        "Make the most of your youth. Time goes faster than you think.",
                        "I have learned that family is what matters most.",
                        "Don't take a single day for granted.",
                        "The young think they have forever. They don't.",
                        "Cherish the people around you while you still can."
                    };
                    break;
                case 2: // Legacy
                    lines = new[] {
                        "I hope I have taught you something worth remembering.",
                        "When I am gone, I hope you will carry on what we have built.",
                        "The young will outlive us all. That's how it should be.",
                        "Leave the world better than you found it.",
                        "Your children will carry your memories forward."
                    };
                    break;
                default: // Observing Young
                    lines = new[] {
                        "They remind me of myself at that age.",
                        "I hope they have it easier than I did.",
                        "They’ve got their whole life ahead of them.",
                        "Sometimes I wish I could warn them, but they wouldn't listen anyway.",
                        "The energy of youth. I miss that more than I care to admit."
                    };
                    break;
            }

            TriggerSpeech(member, lines[_random.Range(0, lines.Length)]);
        }

        private void InsertJournalEntry(string text)
        {
            if (JournalManager.Instance == null) return;
            try
            {
                Traverse.Create(JournalManager.Instance).Method("InsertJournalEntry", new object[] { text, "", false }).GetValue();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to insert journal entry: {ex.Message}");
            }
        }

        private void TriggerRoutineBirthdaySpeech(FamilyMember member, int age)
        {
            List<string> options = new List<string>();
            string item = GetRandomBunkerItem();
            
            // Context Checks
            bool isOlder = age >= 50;
            bool isYoung = age < 30;
            // Trauma is 0..1 normalized within the stats object
            float trauma = (member.stats != null && member.stats.trauma != null) ? member.stats.trauma.NormalizedValue : 0f;
            bool isStressed = trauma > 0.4f;
            bool isHappy = trauma < 0.2f;

            // 1. Universal options (always available)
            options.Add($"I'm {age} today. Another year survived.");
            options.Add($"I turned {age} today. Still here, at least.");
            options.Add($"It's my {age}th birthday. Wonder if I'll get an extra {item}?");
            options.Add($"I'm {age} now. Maybe there's a spare {item} around.");

            // 2. Older / "Catching up"
            if (isOlder)
            {
                options.Add($"Only {age} but I feel twice that.");
                options.Add($"It is really catching up to me. {age} years.");
                options.Add($"My joints aren't what they used to be at {age}.");
                options.Add($"{age} years... I've seen too much.");
            }

            // 3. Young & Happy / Optimistic
            if (isYoung && isHappy)
            {
                options.Add($"I feel I can really live my life, even at {age}.");
                options.Add($"Only {age}! I've got so much time left.");
                options.Add($"{age} is a good age. I feel strong.");
                options.Add($"Things are going okay. Happy {age}th to me.");
            }

            // 4. Stressed / Pessimistic
            if (isStressed)
            {
                options.Add($"Does it even matter that I'm {age}? We're just waiting to die.");
                options.Add($"Another year wasted in this hole. I'm {age} now.");
                options.Add($"I'm {age}. Every day feels the same anyway.");
            }

            // Ensure variety by avoiding the immediate last line spoken by this member
            string selectedLine = options[_random.Range(0, options.Count)];
            int memberId = member.GetId();
            
            if (_lastSpeechLine.ContainsKey(memberId))
            {
                // Try up to 3 times to get a different line
                for (int i = 0; i < 3; i++)
                {
                    if (selectedLine.GetHashCode() != _lastSpeechLine[memberId]) break;
                    selectedLine = options[_random.Range(0, options.Count)];
                }
            }
            
            _lastSpeechLine[memberId] = selectedLine.GetHashCode();
            TriggerSpeech(member, selectedLine, DialogueScheduler.Priority.Routine, () => _ageTracker.GetAgeWeeks(member) / 52 == age);
        }

        private string GetRandomBunkerItem()
        {
            // Simplified to 5 core shelter items
            string[] items = { "ration", "can of water", "bandage", "bit of soap", "proper book" };
            return items[_random.Range(0, items.Length)];
        }
        #endregion
    }
}
