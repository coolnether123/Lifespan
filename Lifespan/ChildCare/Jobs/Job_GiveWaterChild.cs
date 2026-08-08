using System.Collections.Generic;

namespace Lifespan
{
    public class Job_GiveWaterChild : ChildCareJobBase
    {
        public Job_GiveWaterChild() { }

        public Job_GiveWaterChild(FamilyMember caregiver, FamilyMember child)
            : base(ChildCareNeeds.GiveWater, caregiver, child, true)
        {
        }

        protected override ChildCareNeedDefinition Need => ChildCareNeeds.GiveWater;

        protected override bool BeginPreparation()
        {
            if (ObjectManager.Instance == null)
            {
                Cancel(true);
                return false;
            }

            List<Obj_Base> sources = ObjectManager.Instance.GetObjectsOfType(ObjectManager.ObjectType.WaterTank);
            if (sources == null || sources.Count == 0)
            {
                Cancel(true);
                return false;
            }

            location = sources[0].GetInteractionPosition();
            character.WalkToPosition(location);
            return false;
        }

        protected override bool UpdatePreparation()
        {
            if (!HasArrived(location)) return false;

            // Water is a shared resource. Consume it only after the caregiver
            // reaches the tank so cancelled or unreachable jobs cannot grant free thirst recovery.
            if (WaterManager.Instance == null || !WaterManager.Instance.UseWater(1f))
            {
                Cancel(true);
                return false;
            }

            return true;
        }

        protected override void ApplyCare()
        {
            if (Child.stats != null && Child.stats.thirst != null)
            {
                Child.stats.thirst.Set(0f);
            }
        }
    }
}
