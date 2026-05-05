using ModAPI.Core;

namespace Lifespan
{
    public static class ChildCareJobFactory
    {
        public static Job Create(ChildCareNeedDefinition need, FamilyMember caregiver, FamilyMember child)
        {
            if (need == null)
            {
                return null;
            }

            switch (need.Kind)
            {
                case ChildCareNeedKind.Hunger:
                    return new Job_FeedChild(caregiver, child);
                case ChildCareNeedKind.Thirst:
                    return new Job_GiveWaterChild(caregiver, child);
                case ChildCareNeedKind.Toilet:
                    return new Job_ChangeDiaper(caregiver, child);
                case ChildCareNeedKind.Dirtiness:
                    return new Job_CleanChild(caregiver, child);
                case ChildCareNeedKind.Fatigue:
                    return new Job_HelpSleepChild(caregiver, child);
                default:
                    return null;
            }
        }
    }
}
