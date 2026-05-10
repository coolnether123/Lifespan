using ModAPI.Core;
using HarmonyLib;

namespace Lifespan
{
    public static class FamilyMemberExtensions
    {
        public static int GetId(this BaseCharacter character)
        {
            if (character == null) return -1;

            if (character is FamilyMember fm)
            {
                // FamilyMember has a familyId field (sometimes exposed as GetId() method or property)
                // We use reflection via Traverse to be robust
                return Traverse.Create(fm).Field("familyId").GetValue<int>();
            }
            
            if (character is NpcVisitor npc)
            {
                // NpcVisitor has m_npcId field
                return Traverse.Create(npc).Field("m_npcId").GetValue<int>();
            }

            // Fallback: try generic "id" field
            return Traverse.Create(character).Field("id").GetValue<int>();
        }
    }
}
