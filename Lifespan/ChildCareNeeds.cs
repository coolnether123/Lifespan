using System.Collections.Generic;

namespace Lifespan
{
    public static class ChildCareNeeds
    {
        public const float DefaultMenuThreshold = 30f;

        public static readonly ChildCareNeedDefinition Feed = new ChildCareNeedDefinition(
            ChildCareNeedKind.Hunger,
            "Feed",
            "feed_child_custom",
            "Job_FeedChild",
            DefaultMenuThreshold);

        public static readonly ChildCareNeedDefinition GiveWater = new ChildCareNeedDefinition(
            ChildCareNeedKind.Thirst,
            "Give Water",
            "give_water_child",
            "Job_GiveWaterChild",
            DefaultMenuThreshold);

        public static readonly ChildCareNeedDefinition ChangeDiaper = new ChildCareNeedDefinition(
            ChildCareNeedKind.Toilet,
            "Change Diaper",
            "change_diaper",
            "Job_ChangeDiaper",
            DefaultMenuThreshold);

        public static readonly ChildCareNeedDefinition Clean = new ChildCareNeedDefinition(
            ChildCareNeedKind.Dirtiness,
            "Clean",
            "clean_child",
            "Job_CleanChild",
            DefaultMenuThreshold);

        public static readonly ChildCareNeedDefinition ComfortSleep = new ChildCareNeedDefinition(
            ChildCareNeedKind.Fatigue,
            "Comfort (Sleep)",
            "help_sleep_child",
            "Job_HelpSleepChild",
            DefaultMenuThreshold);

        private static readonly ChildCareNeedDefinition[] ManualMenuNeeds =
        {
            Feed,
            GiveWater,
            ChangeDiaper,
            Clean,
            ComfortSleep
        };

        public static IEnumerable<ChildCareNeedDefinition> GetManualMenuNeeds()
        {
            return ManualMenuNeeds;
        }
    }
}
