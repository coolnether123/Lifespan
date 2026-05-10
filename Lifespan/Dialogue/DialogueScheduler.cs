using ModAPI.Core;
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
        public struct ConversationTurn
        {
            public FamilyMember Member;
            public string Text;
            public bool IsJournal;
            public System.Func<bool> Validation;

            public ConversationTurn(FamilyMember member, string text, bool isJournal = false, System.Func<bool> validation = null)
            {
                Member = member;
                Text = text;
                IsJournal = isJournal;
                Validation = validation;
            }
        }

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
            public string ConversationId;
            public bool HasCustomDelay;
            public float MinDelay;
            public float MaxDelay;
        }

        private readonly List<QueuedMessage> _messageQueue = new List<QueuedMessage>();
        private readonly IModLogger _log;
        private readonly ModRandomStream _random;
        private float _nextMessageTime = 0f;
        private string _activeConversationId;
        private int _conversationIdCounter = 0;
        private int _budgetDay = -1;
        private int _speechPointsQueuedToday = 0;

        // Configuration for spacing
        private const float ROUTINE_SPACING = 15f;
        private const float REACTIVE_SPACING = 6f;
        private const float DEFAULT_CONVERSATION_MIN_SPACING = 1.0f;
        private const float DEFAULT_CONVERSATION_MAX_SPACING = 2.5f;
        private const int DAILY_SPEECH_BUDGET = 1;

        public DialogueScheduler(IModLogger log, ModRandomStream random)
        {
            _log = log;
            _random = random;
            _randomProvider = (min, max) => _random.Range(min, max);
        }

        public void Enqueue(FamilyMember member, string text, bool isJournal, Priority priority = Priority.Routine, System.Func<bool> validation = null)
        {
            if (!isJournal && !TryReserveSpeechBudget())
            {
                _log.Debug("[DialogueScheduler] Daily speech budget reached. Skipping queued speech line.");
                return;
            }

            _messageQueue.Add(new QueuedMessage 
            { 
                Member = member, 
                Text = text, 
                IsJournal = isJournal,
                Priority = priority,
                Validation = validation,
                ConversationId = null,
                HasCustomDelay = false
            });
            
            _log.Debug($"[DialogueScheduler] Enqueued {priority} message (Queue size: {_messageQueue.Count}).");
        }

        public void EnqueueConversation(List<ConversationTurn> turns, Priority priority = Priority.Reactive, float minTurnDelay = DEFAULT_CONVERSATION_MIN_SPACING, float maxTurnDelay = DEFAULT_CONVERSATION_MAX_SPACING)
        {
            if (turns == null || turns.Count == 0) return;

            bool hasSpeechTurn = false;
            for (int i = 0; i < turns.Count; i++)
            {
                if (!turns[i].IsJournal)
                {
                    hasSpeechTurn = true;
                    break;
                }
            }

            if (hasSpeechTurn && !TryReserveSpeechBudget())
            {
                _log.Debug("[DialogueScheduler] Daily speech budget reached. Skipping queued conversation.");
                return;
            }

            minTurnDelay = Mathf.Max(0f, minTurnDelay);
            maxTurnDelay = Mathf.Max(minTurnDelay, maxTurnDelay);

            string conversationId = "conv_" + (++_conversationIdCounter).ToString();
            for (int i = 0; i < turns.Count; i++)
            {
                var turn = turns[i];
                _messageQueue.Add(new QueuedMessage
                {
                    Member = turn.Member,
                    Text = turn.Text,
                    IsJournal = turn.IsJournal,
                    Priority = priority,
                    Validation = turn.Validation,
                    ConversationId = conversationId,
                    HasCustomDelay = true,
                    MinDelay = minTurnDelay,
                    MaxDelay = maxTurnDelay
                });
            }

            _log.Debug($"[DialogueScheduler] Enqueued conversation {conversationId} ({turns.Count} turns). Queue size: {_messageQueue.Count}.");
        }

        private delegate float TimeProvider();
        private delegate float RandomProvider(float min, float max);
        private TimeProvider _timeProvider = () => Time.time;
        private RandomProvider _randomProvider;

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
                int nextIndex = GetNextMessageIndex();
                if (nextIndex < 0 || nextIndex >= _messageQueue.Count) return;

                var msg = _messageQueue[nextIndex];
                _messageQueue.RemoveAt(nextIndex);

                if (!string.IsNullOrEmpty(msg.ConversationId) && string.IsNullOrEmpty(_activeConversationId))
                {
                    _activeConversationId = msg.ConversationId;
                }
                
                // VALIDATION CHECK: Run the predicate to see if conditions still apply
                if (msg.Validation == null || msg.Validation())
                {
                    ProcessMessage(msg);
                }
                else
                {
                    _log.Debug($"[DialogueScheduler] Invalidated message skipped: {msg.Text}");
                }

                if (!string.IsNullOrEmpty(msg.ConversationId) && _activeConversationId == msg.ConversationId && !HasPendingConversationMessages(msg.ConversationId))
                {
                    _activeConversationId = null;
                }

                float spacing = GetSpacingForMessage(msg);
                _nextMessageTime = now + spacing;
            }
        }

        private int GetNextMessageIndex()
        {
            if (!string.IsNullOrEmpty(_activeConversationId))
            {
                for (int i = 0; i < _messageQueue.Count; i++)
                {
                    if (_messageQueue[i].ConversationId == _activeConversationId)
                    {
                        return i;
                    }
                }

                _activeConversationId = null;
            }

            return _messageQueue.Count > 0 ? 0 : -1;
        }

        private bool HasPendingConversationMessages(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId)) return false;
            for (int i = 0; i < _messageQueue.Count; i++)
            {
                if (_messageQueue[i].ConversationId == conversationId)
                {
                    return true;
                }
            }
            return false;
        }

        private float GetSpacingForMessage(QueuedMessage msg)
        {
            if (msg.HasCustomDelay)
            {
                float min = Mathf.Max(0f, msg.MinDelay);
                float max = Mathf.Max(min, msg.MaxDelay);
                return _randomProvider(min, max);
            }

            float baseSpacing = (msg.Priority == Priority.Reactive) ? REACTIVE_SPACING : ROUTINE_SPACING;
            return baseSpacing + _randomProvider(0f, 5f);
        }

        private void RefreshDailyBudget()
        {
            int day = GameTime.Day;
            if (day != _budgetDay)
            {
                _budgetDay = day;
                _speechPointsQueuedToday = 0;
            }
        }

        private bool TryReserveSpeechBudget()
        {
            RefreshDailyBudget();
            if (_speechPointsQueuedToday >= DAILY_SPEECH_BUDGET)
            {
                return false;
            }

            _speechPointsQueuedToday++;
            return true;
        }

        private void ProcessMessage(QueuedMessage msg)
        {
            if (msg.IsJournal)
            {
                JournalEntryWriter.TryInsert(msg.Text, _log);
            }
            else if (msg.Member != null && !msg.Member.isDead)
            {
                try
                {
                    msg.Member.Say(msg.Text);
                }
                catch (System.Exception ex)
                {
                    if (_log.IsDebugEnabled) _log.Debug($"[DialogueScheduler] Failed to show speech line: {ex.Message}");
                }
            }
        }

        public void Clear()
        {
            _messageQueue.Clear();
            _nextMessageTime = 0f;
            _activeConversationId = null;
        }
    }
}
