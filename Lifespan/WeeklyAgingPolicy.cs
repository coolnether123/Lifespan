using System;

namespace Lifespan
{
    internal static class WeeklyAgingPolicy
    {
        public const int NoProcessedWeek = -1;

        public static bool ShouldProcessAgingWeek(int currentWeek, int lastProcessedWeek, out string reason)
        {
            if (currentWeek <= 0)
            {
                reason = "invalid-week";
                return false;
            }

            if (currentWeek <= lastProcessedWeek)
            {
                reason = currentWeek == lastProcessedWeek
                    ? "week-already-processed"
                    : "week-before-processed";
                return false;
            }

            reason = null;
            return true;
        }

        public static bool ShouldSkipWeeklyAging(
            FamilyMember member,
            Func<BaseCharacter, bool> shouldCancelAging,
            out string reason)
        {
            if (object.ReferenceEquals(member, null))
            {
                reason = "null-member";
                return true;
            }

            if (member.isDead)
            {
                reason = "dead";
                return true;
            }

            if (member.isDying)
            {
                reason = "dying";
                return true;
            }

            if (ExpeditionStateHelper.IsExpeditionTransitionOrAway(member, out reason))
            {
                return true;
            }

            if (shouldCancelAging != null && shouldCancelAging(member))
            {
                reason = "api-cancelled";
                return true;
            }

            reason = null;
            return false;
        }
    }
}
