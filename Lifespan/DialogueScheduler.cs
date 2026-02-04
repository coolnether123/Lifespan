using ModAPI.Core;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    /// <summary>
    /// Prevents dialogue 'flooding' by queuing messages and releasing them over time.
    /// Spreads weekly updates throughout the week's gameplay.
    /// </summary>
    public class DialogueScheduler
    {
        public enum Priority
        {
            Routine, // Spaced out (Birthdays, Philosophy)
            Reactive // Faster (Illness reactions, condition updates)
        }

        private struct QueuedMessage
        {
            public FamilyMember Member;
            public string Text;
            public bool IsJournal;
            public Priority Priority;
            public System.Func<bool> Validation;
        }

        private readonly Queue<QueuedMessage> _messageQueue = new Queue<QueuedMessage>();
        private readonly IModLogger _log;
        private float _nextMessageTime = 0f;

        // Configuration for spacing
        private const float ROUTINE_SPACING = 15f;
        private const float REACTIVE_SPACING = 6f;

        public DialogueScheduler(IModLogger log)
        {
            _log = log;
        }

        public void Enqueue(FamilyMember member, string text, bool isJournal, Priority priority = Priority.Routine, System.Func<bool> validation = null)
        {
            _messageQueue.Enqueue(new QueuedMessage 
            { 
                Member = member, 
                Text = text, 
                IsJournal = isJournal,
                Priority = priority,
                Validation = validation
            });
            
            _log.Debug($"[DialogueScheduler] Enqueued {priority} message (Queue size: {_messageQueue.Count}).");
        }

        private delegate float TimeProvider();
        private delegate float RandomProvider(float min, float max);
        private TimeProvider _timeProvider = () => Time.time;
        private RandomProvider _randomProvider = (min, max) => Random.Range(min, max);

        /// <summary>
        /// Used by unit tests to avoid Unity ECall errors.
        /// </summary>
        public void SetTestProvider(System.Func<float> time, System.Func<float, float, float> rand)
        {
            _timeProvider = () => time();
            _randomProvider = (min, max) => rand(min, max);
        }

        public void Update()
        {
            if (_messageQueue.Count == 0) return;

            float now = _timeProvider();
            if (now >= _nextMessageTime)
            {
                var msg = _messageQueue.Dequeue();
                
                // VALIDATION CHECK: Run the predicate to see if conditions still apply
                if (msg.Validation == null || msg.Validation())
                {
                    ProcessMessage(msg);
                }
                else
                {
                    _log.Debug($"[DialogueScheduler] Invalidated message skipped: {msg.Text}");
                }
                
                // Spacing depends on the message we just processed (even if skipped)
                float baseSpacing = (msg.Priority == Priority.Reactive) ? REACTIVE_SPACING : ROUTINE_SPACING;
                float spacing = baseSpacing + _randomProvider(0f, 5f);
                _nextMessageTime = now + spacing;
            }
        }

        private void ProcessMessage(QueuedMessage msg)
        {
            if (msg.IsJournal)
            {
                if (JournalManager.Instance != null)
                {
                    try
                    {
                        Traverse.Create(JournalManager.Instance).Method("InsertJournalEntry", new object[] { msg.Text, "", false }).GetValue();
                    }
                    catch { }
                }
            }
            else if (msg.Member != null && !msg.Member.isDead)
            {
                try
                {
                    msg.Member.Say(msg.Text);
                }
                catch { }
            }
        }

        public void Clear()
        {
            _messageQueue.Clear();
            _nextMessageTime = 0f;
        }
    }
}
