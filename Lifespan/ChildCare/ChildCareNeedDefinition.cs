namespace Lifespan
{
    public enum ChildCareNeedKind
    {
        Hunger,
        Thirst,
        Toilet,
        Dirtiness,
        Fatigue
    }

    public sealed class ChildCareNeedDefinition
    {
        public ChildCareNeedDefinition(
            ChildCareNeedKind kind,
            string menuLabel,
            string interactionType,
            string jobType,
            float menuThreshold)
        {
            Kind = kind;
            MenuLabel = menuLabel;
            InteractionType = interactionType;
            JobType = jobType;
            MenuThreshold = menuThreshold;
        }

        public ChildCareNeedKind Kind { get; private set; }
        public string MenuLabel { get; private set; }
        public string InteractionType { get; private set; }
        public string JobType { get; private set; }
        public float MenuThreshold { get; private set; }
    }
}
