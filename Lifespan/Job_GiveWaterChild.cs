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
            List<Obj_Base> sources = ObjectManager.Instance.GetObjectsOfType(ObjectManager.ObjectType.WaterTank);
            if (sources.Count == 0)
            {
                return true;
            }

            location = sources[0].GetInteractionPosition();
            character.WalkToPosition(location);
            return false;
        }

        protected override bool UpdatePreparation()
        {
            return HasArrived(location);
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
