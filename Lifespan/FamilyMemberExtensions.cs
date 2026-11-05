namespace Lifespan
{
    public static class FamilyMemberExtensions
    {
        public static int GetId(this BaseCharacter character)
        {
            if (character == null) return -1;

            if (character is FamilyMember fm)
            {
                return fm.GetId();
            }
            
            if (character is NpcVisitor npc)
            {
                return npc.npcId;
            }

            // BaseCharacter itself has no persistent identity.
            return -1;
        }
    }
}
