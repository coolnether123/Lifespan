using UnityEngine;
using ModAPI.Core;

namespace Lifespan
{
    public class Job_ChangeDiaper : Job
    {
        private FamilyMember _child;
        private FamilyMember _caregiver;
        private DiaperState _stage;
        
        private enum DiaperState
        {
            GoToChild,
            Change
        }

        public Job_ChangeDiaper() { }

        public Job_ChangeDiaper(FamilyMember caregiver, FamilyMember child) : base("change_diaper", child.transform.position, caregiver, null)
        {
            _caregiver = caregiver;
            _child = child;
            _stage = DiaperState.GoToChild;
        }

        public override string GetJobType() => "Job_ChangeDiaper";

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
            if (_stage == DiaperState.GoToChild)
            {
                if (HasArrived(_child.transform.position))
                {
                    _stage = DiaperState.Change;
                    BeginStage();
                }
                else
                {
                    if (Vector3.Distance(character.transform.position, _child.transform.position) > LifespanConstants.UpdateTargetDistance)
                    {
                        this.location = _child.transform.position;
                        character.WalkToPosition(this.location);
                    }
                }
            }
            else if (_stage == DiaperState.Change)
            {
                if (_child.stats != null && _child.stats.toilet != null)
                {
                    _child.stats.toilet.Set(0f); // Reset toilet need
                    
                    // Clear dirtiness/hygiene too since diaper change implies cleaning
                    if (_child.stats.dirtiness != null) _child.stats.dirtiness.Modify(-20f);
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

            if (_stage == DiaperState.GoToChild)
            {
                this.location = _child.transform.position;
                this.character.WalkToPosition(this.location);
            }
        }

        private bool HasArrived(Vector3 target)
        {
            return Vector3.Distance(character.transform.position, target) < LifespanConstants.ArrivalDistance;
        }
    }
}
