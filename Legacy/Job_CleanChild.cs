using UnityEngine;
using ModAPI.Core;

namespace Lifespan
{
    public class Job_CleanChild : Job
    {
        private FamilyMember _child;
        private FamilyMember _caregiver;
        private CleanState _stage;
        
        private enum CleanState
        {
            GoToChild,
            Clean
        }

        public Job_CleanChild() { }

        public Job_CleanChild(FamilyMember caregiver, FamilyMember child) : base("clean_child", child.transform.position, caregiver, null)
        {
            _caregiver = caregiver;
            _child = child;
            _stage = CleanState.GoToChild;
        }

        public override string GetJobType() => "Job_CleanChild";

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
            if (_stage == CleanState.GoToChild)
            {
                if (HasArrived(_child.transform.position))
                {
                    _stage = CleanState.Clean;
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
            else if (_stage == CleanState.Clean)
            {
                // Assume animation plays or just delay
                // Here we just apply effect instantly for responsiveness
                if (_child.stats != null && _child.stats.dirtiness != null)
                {
                    _child.stats.dirtiness.Set(0f); // Fully clean
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

            if (_stage == CleanState.GoToChild)
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
