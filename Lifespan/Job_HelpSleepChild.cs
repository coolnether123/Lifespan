using UnityEngine;
using ModAPI.Core;

namespace Lifespan
{
    public class Job_HelpSleepChild : Job
    {
        private FamilyMember _child;
        private FamilyMember _caregiver;
        private SleepState _stage;
        
        private enum SleepState
        {
            GoToChild,
            HelpSleep
        }

        public Job_HelpSleepChild() { }

        public Job_HelpSleepChild(FamilyMember caregiver, FamilyMember child) : base("help_sleep_child", child.transform.position, caregiver, null)
        {
            _caregiver = caregiver;
            _child = child;
            _stage = SleepState.GoToChild;
        }

        public override string GetJobType() => "Job_HelpSleepChild";

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
            if (_stage == SleepState.GoToChild)
            {
                if (HasArrived(_child.transform.position))
                {
                    _stage = SleepState.HelpSleep;
                    BeginStage();
                }
                else
                {
                     if (Vector3.Distance(character.transform.position, _child.transform.position) > 2.0f)
                    {
                        this.location = _child.transform.position;
                        character.WalkToPosition(this.location);
                    }
                }
            }
            else if (_stage == SleepState.HelpSleep)
            {
                if (_child.stats != null && _child.stats.fatigue != null)
                {
                    // Comfort the child, reducing fatigue significantly (power nap assistance)
                    _child.stats.fatigue.Modify(-50f); 
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

            if (_stage == SleepState.GoToChild)
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
