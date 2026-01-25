using ModAPI.Core;
using ModAPI.Reflection;

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
                // We use reflection via Safe helper to be robust
                return Safe.GetField<int>(fm, "familyId");
            }
            
            if (character is NpcVisitor npc)
            {
                // NpcVisitor has m_npcId field
                return Safe.GetField<int>(npc, "m_npcId");
            }

            // Fallback: try generic "id" field
            return Safe.GetField<int>(character, "id");
        }
    }
}
