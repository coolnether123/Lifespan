namespace Lifespan
{
    public class Job_ChangeDiaper : ChildCareJobBase
    {
        public Job_ChangeDiaper() { }

        public Job_ChangeDiaper(FamilyMember caregiver, FamilyMember child)
            : base(ChildCareNeeds.ChangeDiaper, caregiver, child, false)
        {
        }

        protected override ChildCareNeedDefinition Need => ChildCareNeeds.ChangeDiaper;

        protected override void ApplyCare()
        {
            if (Child.stats != null && Child.stats.toilet != null)
            {
                Child.stats.toilet.Set(0f);

                if (Child.stats.dirtiness != null)
                {
                    Child.stats.dirtiness.Modify(-20f);
                }
            }
        }
    }
}
