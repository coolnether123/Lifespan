namespace Lifespan
{
    public static class ChildCareNeedStatus
    {
        public static bool IsNeeded(FamilyMember child, ChildCareNeedDefinition need)
        {
            if (child == null || child.stats == null || need == null)
            {
                return false;
            }

            float value;
            return TryGetValue(child.stats, need.Kind, out value) && value >= need.MenuThreshold;
        }

        private static bool TryGetValue(BehaviourStats stats, ChildCareNeedKind kind, out float value)
        {
            value = 0f;

            switch (kind)
            {
                case ChildCareNeedKind.Hunger:
                    return TryRead(stats.hunger, out value);
                case ChildCareNeedKind.Thirst:
                    return TryRead(stats.thirst, out value);
                case ChildCareNeedKind.Toilet:
                    return TryRead(stats.toilet, out value);
                case ChildCareNeedKind.Dirtiness:
                    return TryRead(stats.dirtiness, out value);
                case ChildCareNeedKind.Fatigue:
                    return TryRead(stats.fatigue, out value);
                default:
                    return false;
            }
        }

        private static bool TryRead(BehaviourStat stat, out float value)
        {
            value = 0f;
            if (stat == null)
            {
                return false;
            }

            value = stat.Value;
            return true;
        }
    }
}
