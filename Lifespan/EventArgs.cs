using System;

namespace Lifespan
{
    [Serializable]
    public class CharacterAgedUpArgs
    {
        public string FamilyMemberId;
        public int NewAgeWeeks;
        
        public CharacterAgedUpArgs(FamilyMember member, int weeks)
        {
            FamilyMemberId = member.GetId().ToString();
            NewAgeWeeks = weeks;
        }
    }

    [Serializable]
    public class AgeChangedArgs
    {
        public string FamilyMemberId;
        public int NewAgeWeeks;
        public int OldAgeWeeks;

        public AgeChangedArgs(FamilyMember member, int newWeeks, int oldWeeks)
        {
            FamilyMemberId = member.GetId().ToString();
            NewAgeWeeks = newWeeks;
            OldAgeWeeks = oldWeeks;
        }
    }

    [Serializable]
    public class CharacterAgeGeneratedArgs
    {
        public string CharacterId;
        public int AgeWeeks;
        public AgeContext Context; 

        public CharacterAgeGeneratedArgs(BaseCharacter character, int weeks, AgeContext context)
        {
            CharacterId = character.GetId().ToString();
            AgeWeeks = weeks;
            Context = context;
        }
    }

    [Serializable]
    public class BecameAdultArgs
    {
        public string FamilyMemberId;
        public BecameAdultArgs(FamilyMember member)
        {
            FamilyMemberId = member.GetId().ToString();
        }
    }

    [Serializable]
    public class ElderIllnessAcquiredArgs
    {
        public string FamilyMemberId;
        public string IllnessId;
        public ElderIllnessAcquiredArgs(FamilyMember member, string illnessId)
        {
            FamilyMemberId = member.GetId().ToString();
            IllnessId = illnessId;
        }
    }

    [Serializable]
    public class DiedOfOldAgeArgs
    {
        public string FamilyMemberId;
        public int AgeYears;
        public DiedOfOldAgeArgs(FamilyMember member, int years)
        {
            FamilyMemberId = member.GetId().ToString();
            AgeYears = years;
        }
    }
}
