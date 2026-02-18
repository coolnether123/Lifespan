using UnityEngine;
using ModAPI.Core;
using System.Collections.Generic;

namespace Lifespan
{
    public class Job_GiveWaterChild : Job
    {
        private FamilyMember _child;
        private FamilyMember _caregiver;
        private WaterState _stage;
        
        private enum WaterState
        {
            GetWater,
            GoToChild,
            GiveWater
        }

        public Job_GiveWaterChild() { }

        public Job_GiveWaterChild(FamilyMember caregiver, FamilyMember child) : base("give_water_child", child.transform.position, caregiver, null)
        {
            _caregiver = caregiver;
            _child = child;
            _stage = WaterState.GetWater;
        }

        public override string GetJobType() => "Job_GiveWaterChild";

        public override void Activate()
        {
            BeginStage();
            base.Activate();
        }

        public override bool BeginJob()
        {
            if (_child == null || _caregiver == null) { Cancel(true); return false; }
            this.state = JobState.Started;
            return true;
        }

        public override void UpdateJob()
        {
            if (_stage == WaterState.GetWater)
            {
                // Simple version: Walk to waterbutt, wait, assume water taken.
                // We don't use 'obj' interaction to avoid wrong animations/consumption logic for now
                // unless we know the exact 'fill_bottle' or similar interaction.
                if (HasArrived(this.location))
                {
                    _stage = WaterState.GoToChild;
                    BeginStage();
                }
            }
            else if (_stage == WaterState.GoToChild)
            {
                if (HasArrived(_child.transform.position))
                {
                    _stage = WaterState.GiveWater;
                    BeginStage();
                }
                else
                {
                    // Update target if child moves
                    if (Vector3.Distance(character.transform.position, _child.transform.position) > 2.0f)
                    {
                        this.location = _child.transform.position;
                        character.WalkToPosition(this.location);
                    }
                }
            }
            else if (_stage == WaterState.GiveWater)
            {
                if (_child.stats != null && _child.stats.thirst != null)
                {
                    _child.stats.thirst.Set(0f); // Fully hydrate
                }
                
                state = JobState.Finished;
                OnFinishedJob();
            }
        }

        private void BeginStage()
        {
            if (GetCancelState() != JobCancelState.Active)
            {
                state = JobState.Finished;
                OnFinishedJob();
                return;
            }

            if (_stage == WaterState.GetWater)
            {
                // Find WaterTank (Water Butt)
                List<Obj_Base> sources = ObjectManager.Instance.GetObjectsOfType(ObjectManager.ObjectType.WaterTank);
                if (sources.Count > 0)
                {
                    this.location = sources[0].GetInteractionPosition();
                    this.character.WalkToPosition(this.location);
                }
                else
                {
                    // No water source? Just fail or magic water?
                    // Allow magic water if no butt exists (rare)
                    _stage = WaterState.GoToChild;
                    BeginStage();
                }
            }
            else if (_stage == WaterState.GoToChild)
            {
                this.location = _child.transform.position;
                this.character.WalkToPosition(this.location);
            }
        }

        private bool HasArrived(Vector3 target)
        {
            return Vector3.Distance(character.transform.position, target) < 1.0f;
        }
    }
}
