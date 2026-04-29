namespace Lifespan
{
    public class Job_HelpSleepChild : ChildCareJobBase
    {
        public Job_HelpSleepChild() { }

        public Job_HelpSleepChild(FamilyMember caregiver, FamilyMember child)
            : base(ChildCareNeeds.ComfortSleep, caregiver, child, false)
        {
        }

        protected override ChildCareNeedDefinition Need => ChildCareNeeds.ComfortSleep;

        protected override void ApplyCare()
        {
            if (Child.stats != null && Child.stats.fatigue != null)
            {
                Child.stats.fatigue.Modify(-50f);
            }
        }
    }
}
