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
            // The job loader constructs this type before restoring its saved fields.
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

            // Obj_Base requires this registration before UpdateInteraction.
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
                ReturnFood();
                Cancel(true);
                return;
            }

            if (GetCancelState() != JobCancelState.Active)
            {
                ReturnFood();
                state = JobState.Finished;
                OnFinishedJob();
                return;
            }

            if (_stage == FeedState.GetFood)
            {
                 if (this.obj != null)
                 {
                     if (this.obj.UpdateInteraction(this.character, GetCancelState()))
                     {
                         this.obj.InteractionFinished(this.character);
                         _stage = FeedState.GoToChild;
                         BeginStage();
                     }
                 }
                 else
                 {
                     Cancel(true);
                 }
            }
            else if (_stage == FeedState.GoToChild)
            {
                float dist = Vector3.Distance(_feeder.transform.position, _child.transform.position);
                if (dist < 1.0f)
                {
                    _stage = FeedState.Feed;
                    BeginStage();
                }
                else
                {
                    if (dist > LifespanConstants.UpdateTargetDistance)
                    {
                        location = _child.transform.position;
                        _feeder.WalkToPosition(location);
                    }
                }
            }
            else if (_stage == FeedState.Feed)
            {
                FamilyAI ai = _feeder.GetComponent<FamilyAI>();
                int foodTaken = ai != null ? ai.CarriedFood_GetFoodTaken() : 0;
                if (_child.stats == null || _child.stats.hunger == null || ai == null || foodTaken <= 0)
                {
                    ReturnFood();
                    Cancel(true);
                    return;
                }

                _child.stats.hunger.Modify(-100f);
                ai.CarriedFood_Feed();
                
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
                // Find a pantry with an actual ration available. A container's
                // existence is not evidence that the interaction can provide food.
                if (ObjectManager.Instance == null)
                {
                    Cancel(true);
                    return;
                }

                List<Obj_Base> sources = ObjectManager.Instance.GetObjectsOfType(ObjectManager.ObjectType.Pantry);
                Obj_Base bestSource = null;
                if (sources != null)
                {
                    foreach (var src in sources)
                    {
                        Obj_Pantry pantry = src as Obj_Pantry;
                        if (pantry != null && pantry.GetRations() > 0)
                        {
                            bestSource = pantry;
                            break;
                        }
                    }
                }

                if (bestSource != null)
                {
                    this.obj = bestSource;
                    this.type = "take_food";
                    this.location = bestSource.GetInteractionPosition();
                    this.character.WalkToPosition(this.location);
                    
                    // The pantry's take_food interaction transfers the ration to FamilyAI.
                }
                else
                {
                    Cancel(true);
                }
            }
            else if (_stage == FeedState.GoToChild)
            {
                this.obj = null;
                this.location = _child.transform.position;
                this.character.WalkToPosition(this.location);
            }
        }

        public void ReturnFood()
        {
            if (_feeder == null) return;

            FamilyAI ai = _feeder.GetComponent<FamilyAI>();
            int foodTaken = ai != null ? ai.CarriedFood_GetFoodTaken() : 0;
            if (foodTaken > 0 && FoodManager.Instance != null && FoodManager.Instance.AddRations(foodTaken))
            {
                ai.CarriedFood_Feed();
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
