namespace Lifespan
{
    public class Job_CleanChild : ChildCareJobBase
    {
        public Job_CleanChild() { }

        public Job_CleanChild(FamilyMember caregiver, FamilyMember child)
            : base(ChildCareNeeds.Clean, caregiver, child, false)
        {
        }

        protected override ChildCareNeedDefinition Need => ChildCareNeeds.Clean;

        protected override void ApplyCare()
        {
            if (Child.stats != null && Child.stats.dirtiness != null)
            {
                Child.stats.dirtiness.Set(0f);
            }
        }
    }
}
