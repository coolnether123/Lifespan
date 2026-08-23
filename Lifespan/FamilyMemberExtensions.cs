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
                // The game stores this ID in a private field.
                return Traverse.Create(fm).Field("familyId").GetValue<int>();
            }
            
            if (character is NpcVisitor npc)
            {
                return Traverse.Create(npc).Field("m_npcId").GetValue<int>();
            }

            // Other BaseCharacter types use the generic id field.
            return Traverse.Create(character).Field("id").GetValue<int>();
        }
    }
}
