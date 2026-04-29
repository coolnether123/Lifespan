using ModAPI.Core;
using UnityEngine;

namespace Lifespan
{
    public abstract class ChildCareJobBase : Job
    {
        private ChildCareJobPhase _phase;

        protected ChildCareJobBase()
        {
            _phase = ChildCareJobPhase.GoToChild;
        }

        protected ChildCareJobBase(
            ChildCareNeedDefinition need,
            FamilyMember caregiver,
            FamilyMember child,
            bool requiresPreparation)
            : base(need.InteractionType, child.transform.position, caregiver, null)
        {
            Caregiver = caregiver;
            Child = child;
            _phase = requiresPreparation ? ChildCareJobPhase.Prepare : ChildCareJobPhase.GoToChild;
        }

        private enum ChildCareJobPhase
        {
            Prepare,
            GoToChild,
            ApplyCare
        }

        protected FamilyMember Child { get; private set; }
        protected FamilyMember Caregiver { get; private set; }

        protected abstract ChildCareNeedDefinition Need { get; }

        public override string GetJobType()
        {
            return Need.JobType;
        }

        public override void Activate()
        {
            BeginPhase();
            base.Activate();
        }

        public override bool BeginJob()
        {
            if (!HasParticipants())
            {
                Cancel(true);
                return false;
            }

            state = JobState.Started;
            return true;
        }

        public override void UpdateJob()
        {
            if (!HasParticipants())
            {
                Cancel(true);
                return;
            }

            if (_phase == ChildCareJobPhase.Prepare)
            {
                if (UpdatePreparation())
                {
                    MoveToNextPhase();
                }

                return;
            }

            if (_phase == ChildCareJobPhase.GoToChild)
            {
                if (HasArrived(Child.transform.position))
                {
                    MoveToNextPhase();
                }
                else
                {
                    UpdateChildDestinationIfNeeded();
                }

                return;
            }

            if (_phase == ChildCareJobPhase.ApplyCare)
            {
                ApplyCare();
                FinishJob();
            }
        }

        protected virtual bool BeginPreparation()
        {
            return true;
        }

        protected virtual bool UpdatePreparation()
        {
            return true;
        }

        protected abstract void ApplyCare();

        protected bool HasArrived(Vector3 target)
        {
            return character != null &&
                   Vector3.Distance(character.transform.position, target) < LifespanConstants.ArrivalDistance;
        }

        protected void FinishJob()
        {
            state = JobState.Finished;
            OnFinishedJob();
        }

        private void BeginPhase()
        {
            if (!HasParticipants())
            {
                Cancel(true);
                return;
            }

            if (GetCancelState() != JobCancelState.Active)
            {
                FinishJob();
                return;
            }

            if (_phase == ChildCareJobPhase.Prepare)
            {
                if (BeginPreparation())
                {
                    MoveToNextPhase();
                }

                return;
            }

            if (_phase == ChildCareJobPhase.GoToChild)
            {
                MoveToChild();
            }
        }

        private void MoveToNextPhase()
        {
            if (GetCancelState() != JobCancelState.Active)
            {
                FinishJob();
                return;
            }

            if (_phase == ChildCareJobPhase.Prepare)
            {
                _phase = ChildCareJobPhase.GoToChild;
                BeginPhase();
                return;
            }

            if (_phase == ChildCareJobPhase.GoToChild)
            {
                _phase = ChildCareJobPhase.ApplyCare;
                BeginPhase();
            }
        }

        private void MoveToChild()
        {
            location = Child.transform.position;
            character.WalkToPosition(location);
        }

        private void UpdateChildDestinationIfNeeded()
        {
            if (Vector3.Distance(character.transform.position, Child.transform.position) > LifespanConstants.UpdateTargetDistance)
            {
                MoveToChild();
            }
        }

        private bool HasParticipants()
        {
            return Child != null && Caregiver != null && character != null;
        }
    }
}
