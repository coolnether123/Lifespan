using UnityEngine;
using ModAPI.Core;
using System.Collections.Generic;

namespace Lifespan
{
    public class Job_FeedChild : Job
    {
        private FamilyMember _child;
        private FamilyMember _feeder;
        private FeedState _stage;
        
        private enum FeedState
        {
            GetFood,
            GoToChild,
            Feed
        }

        public Job_FeedChild() 
        { 
            // Required for serialization, though we might not support it fully
        }

        public Job_FeedChild(FamilyMember feeder, FamilyMember child) : base("feed_child_custom", child.transform.position, feeder, null)
        {
            _feeder = feeder;
            _child = child;
            _stage = FeedState.GetFood;
        }

        public override string GetJobType() => ChildCareJobTypes.FeedChild;

        public override void Activate()
        {
            BeginStage();
            base.Activate();
        }

        public override bool BeginJob()
        {
            if (_child == null || _feeder == null)
            {
                Cancel(true);
                return false;
            }

            // Fix for KeyNotFoundException:
            // Custom jobs that delegate UpdateInteraction to an Obj_Base MUST 
            // call BeginInteraction first to register the character with the object's 
            // interaction dictionary (Obj_Base.current_interaction).
            if (this.obj != null && !string.IsNullOrEmpty(this.type))
            {
                if (!this.obj.BeginInteraction(this.character, this.type))
                {
                    Cancel(true);
                    return false;
                }
            }

            this.state = JobState.Started;
            return true;
        }

        public override void UpdateJob()
        {
            if (_child == null || _feeder == null || character == null)
            {
                Cancel(true);
                return;
            }

            if (_stage == FeedState.GetFood)
            {
                 // We are walking to food or taking it.
                 // We reuse the standard interaction logic if we set 'this.obj'.
                 if (this.obj != null)
                 {
                     if (this.obj.UpdateInteraction(this.character, GetCancelState()))
                     {
                         this.obj.InteractionFinished(this.character);
                         // Food taken.
                         _stage = FeedState.GoToChild;
                         BeginStage();
                     }
                 }
                 else
                 {
                     // Failed to find food or lost it
                     Cancel(true);
                 }
            }
            else if (_stage == FeedState.GoToChild)
            {
                // Walking to child.
                // Manual distance check since child might move (though they shouldn't if immobile)
                float dist = Vector3.Distance(_feeder.transform.position, _child.transform.position);
                if (dist < 1.0f) // Close enough
                {
                    _stage = FeedState.Feed;
                    BeginStage();
                }
                else
                {
                    // Update destination in case child moved
                    if (dist > LifespanConstants.UpdateTargetDistance)
                    {
                        location = _child.transform.position;
                        _feeder.WalkToPosition(location);
                    }
                }
            }
            else if (_stage == FeedState.Feed)
            {
                // Instant feed for now, or animation?
                // Just apply hunger reduction.
                if (_child.stats != null)
                {
                    // Restore hunger. 
                    // Rations usually give large amounts.
                    // Assume we took a ration.
                    _child.stats.hunger.Modify(-100f); 
                    // Also reduce feeder's carried food?
                    // The 'GetFood' stage interaction (take_food) usually puts food in the character's inventory (FamilyAI.carriedFoodType).
                    
                    FamilyAI ai = _feeder.GetComponent<FamilyAI>();
                    if (ai != null)
                    {
                        ai.CarriedFood_Feed(); // Consumes the food visuals/logic
                    }
                }
                
                state = JobState.Finished;
                OnFinishedJob();
            }
        }

        private void BeginStage()
        {
            if (GetCancelState() != JobCancelState.Active)
            {
                ReturnFood();
                state = JobState.Finished;
                OnFinishedJob();
                return;
            }

            if (_stage == FeedState.GetFood)
            {
                // Find Pantry/Freezer
                // Simplified logic from Job_Feed
                // We need to set this.obj to the Food Container
                if (ObjectManager.Instance == null)
                {
                    Cancel(true);
                    return;
                }

                List<Obj_Base> sources = ObjectManager.Instance.GetObjectsOfType(ObjectManager.ObjectType.Pantry);
                if (sources == null || sources.Count == 0) sources = ObjectManager.Instance.GetObjectsOfType(ObjectManager.ObjectType.Freezer);
                
                // Filter for "Has Food" logic? 
                // Obj_Pantry has 'rationCount'.
                // We'll just pick the first valid one.
                Obj_Base bestSource = null;
                if (sources != null)
                {
                    foreach(var src in sources)
                    {
                        // Basic check, assume pantry has food if it exists for now.
                        bestSource = src;
                        break;
                    }
                }

                if (bestSource != null)
                {
                    this.obj = bestSource;
                    this.type = "take_food"; // Standard interaction string
                    this.location = bestSource.GetInteractionPosition();
                    this.character.WalkToPosition(this.location);
                    
                    // We rely on Obj_Pantry.BeginInteraction handling "take_food" which gives food to FamilyAI.
                }
                else
                {
                    // No food source
                    Cancel(true);
                }
            }
            else if (_stage == FeedState.GoToChild)
            {
                this.obj = null; // Clear object interaction
                this.location = _child.transform.position;
                this.character.WalkToPosition(this.location);
            }
        }

        public void ReturnFood()
        {
             // If we have food but cancelled, put it back.
             // Copied logic from Job_Feed.ReturnTakenItems if needed.
             // For simplicity, we skip this for the prototype, but in prod should be added.
             FamilyAI ai = _feeder.GetComponent<FamilyAI>();
             if (ai != null && ai.CarriedFood_GetFoodTaken() > 0 && FoodManager.Instance != null)
             {
                 FoodManager.Instance.AddRations(ai.CarriedFood_GetFoodTaken());
                 ai.CarriedFood_Feed(); // Clear hands
             }
        }

        public override void SaveLoadJob(SaveData data)
        {
            base.SaveLoadJob(data);

            int childId = _child != null ? _child.GetId() : -1;
            int stage = (int)_stage;

            bool savedChild = data.SaveLoad(ChildCareJobSaveLoadHelper.ChildIdKey, ref childId);
            bool savedStage = data.SaveLoad("lifespanFeedChildStage", ref stage);

            if (!data.isLoading)
            {
                return;
            }

            _feeder = character;
            _child = ChildCareJobSaveLoadHelper.ResolveChild(childId);

            if (_child == null && !savedChild)
            {
                _child = ChildCareJobSaveLoadHelper.FindClosestChildToSavedLocation(character, location);
            }

            if (savedStage && stage >= 0 && stage <= (int)FeedState.Feed)
            {
                _stage = (FeedState)stage;
            }
            else
            {
                _stage = FeedState.GoToChild;
            }
        }
    }
}
