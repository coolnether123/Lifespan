using UnityEngine;

namespace Lifespan
{
    public static class ChildCareJobSaveLoadHelper
    {
        public const string ChildIdKey = "lifespanChildCareChild";

        public static FamilyMember ResolveChild(int childId)
        {
            if (childId <= -1 || FamilyManager.Instance == null)
            {
                return null;
            }

            return FamilyManager.Instance.GetFamilyMember(childId);
        }

        public static FamilyMember FindClosestChildToSavedLocation(FamilyMember caregiver, Vector3 location)
        {
            if (FamilyManager.Instance == null)
            {
                return null;
            }

            FamilyMember closest = null;
            float closestDistance = float.MaxValue;
            foreach (FamilyMember member in FamilyManager.Instance.GetAllFamilyMembers())
            {
                if (member == null || member == caregiver || member.isDead)
                {
                    continue;
                }

                float distance = Vector3.Distance(member.transform.position, location);
                if (distance < closestDistance)
                {
                    closest = member;
                    closestDistance = distance;
                }
            }

            return closestDistance <= LifespanConstants.UpdateTargetDistance ? closest : null;
        }
    }
}
