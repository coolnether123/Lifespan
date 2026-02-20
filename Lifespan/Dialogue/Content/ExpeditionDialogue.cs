using System.Collections.Generic;

namespace Lifespan.Dialogue.Content
{
    public static class ExpeditionDialogue
    {
        public static List<DialogueLine> GetOneOffOptions(int ageYears, string illnessName, bool hasIllness)
        {
            var options = new List<DialogueLine>();

            if (ageYears >= 60)
            {
                options.Add("I'm not as fast as I used to be, but I've seen everything the wastes can throw.");
                options.Add("Just checking my filters. Don't want any surprises top-side.");
                options.Add("The surface hasn't changed. Same dust, same danger.");
                options.Add(DialogueLine.WithTrait("I've survived sixty years. I'm not going to let a bit of rubble stop me now.", "Courageous"));
            }
            else
            {
                options.Add("Preparing for the run. I'll bring back something useful, I promise.");
                options.Add("Just making sure the pack is balanced.");
                options.Add(DialogueLine.WithTrait("We'll be back before you know it. Keep the stove warm.", "Optimist"));
            }

            if (hasIllness && !string.IsNullOrEmpty(illnessName))
            {
                options.Add($"Are you sure about this? That {illnessName} is going to make it hard to run if you have to.");
                options.Add($"Don't push yourself too hard. If that {illnessName} flares up, just head back.");
                options.Add($"I've packed some extra supplies in case your {illnessName} gets worse out there.");
                options.Add(DialogueLine.WithTrait($"I've got this {illnessName} under control. Mostly.", "Optimist"));
            }

            return options;
        }

        public class ExpeditionConvData
        {
            public List<DialogueLine> Openers = new List<DialogueLine>();
            public List<DialogueLine> Responses = new List<DialogueLine>();
            public List<DialogueLine> Closers = new List<DialogueLine>();
        }

        public static ExpeditionConvData GetAgingConversation(int ageYears)
        {
            var data = new ExpeditionConvData();
            if (ageYears >= 65)
            {
                data.Openers.Add($"You've been out there more times than I can count. Maybe let someone else take this run?");
                data.Openers.Add($"I see you're checking the gear again. It's a long walk to the surface at {ageYears}.");
                data.Openers.Add($"The radiation is getting worse. At {ageYears}, your body might not handle it as well.");
                data.Openers.Add($"You've already done your share for the family. Why not stay and help in the lab?");
                
                data.Responses.Add($"I've got more experience in my pinky than those kids have in their whole bodies. I'll be fine.");
                data.Responses.Add($"The shelter feels smaller every year. I need to see the horizon, even if it's grey.");
                data.Responses.Add($"I've survived sixty-five years of this. A bit of dust isn't going to stop me now.");
                data.Responses.Add($"The bunker isn't going anywhere. I need to know there's still a world left up there.");
                data.Responses.Add(DialogueLine.WithTrait("I'm the only one who knows the old shortcuts. You need me out there.", "Courageous"));
                
                data.Closers.Add($"Just... don't stay out after dark. We need you back here.");
                data.Closers.Add($"Your experience is exactly why we need you back safe. Promise you'll be careful.");
                data.Closers.Add($"If it gets too much, drop the gear and run. Your life is worth more than scrap.");
                data.Closers.Add($"I'll be waiting by the radio. Don't be late.");
            }
            else
            {
                data.Openers.Add($"You're ready for your first real run?");
                data.Responses.Add($"I've been waiting for this. I'm ready.");
                data.Closers.Add($"Just keep your head down and your mask on.");
            }
            return data;
        }

        public static ExpeditionConvData GetIllnessConversation(string survivorName, string illnessId)
        {
            var data = new ExpeditionConvData();
            switch (illnessId)
            {
                case ElderIllnessManager.ILLNESS_DEMENTIA:
                case ElderIllnessManager.ILLNESS_MILD_DEMENTIA:
                    data.Openers.Add($"Are you sure you remember the route? We can't afford you getting lost out there.");
                    data.Responses.Add($"I... I have the map. The roads... they'll look familiar once I'm there.");
                    data.Closers.Add($"Stick to the main highways. Don't go exploring any side streets.");
                    break;
                case ElderIllnessManager.ILLNESS_ARTHRITIS:
                case ElderIllnessManager.ILLNESS_MILD_ARTHRITIS:
                    data.Openers.Add($"I saw you winced when you picked up that pack. The joints acting up again?");
                    data.Openers.Add($"Your hands look stiff today. Are you sure you can handle a weapon if you have to?");
                    data.Openers.Add($"That's a lot of weight for those knees. Maybe split the load with someone else?");
                    
                    data.Responses.Add($"It's just the damp air in here. Once I'm moving top-side, the heat will help.");
                    data.Responses.Add($"They'll loosen up once the blood starts pumping. It's just the morning chill.");
                    data.Responses.Add($"I'll manage. I've walked through worse pain than a bit of clicking in my bones.");
                    data.Responses.Add(DialogueLine.WithTrait("A bit of pain is nothing compared to an empty pantry.", "Courageous"));
                    
                    data.Closers.Add($"If it gets worse, drop the heavy salvage. Your knees aren't worth a چند pieces of scrap.");
                    data.Closers.Add($"Keep them warm. I've packed an extra blanket in your kit.");
                    data.Closers.Add($"Just... don't push it. If you can't walk, you're a liability to yourself.");
                    break;
                case ElderIllnessManager.ILLNESS_RESPIRATORY:
                case ElderIllnessManager.ILLNESS_MILD_RESPIRATORY:
                    data.Openers.Add($"You're sounding a bit heavy on the intake. Sure the gas mask is enough for those lungs?");
                    data.Openers.Add($"That cough sounds worse near the airlock. Maybe the filters are failing?");
                    data.Openers.Add($"Breath is life out there. Are you certain you can keep up the pace?");
                    
                    data.Responses.Add($"The filters are fresh. I just need to pace myself, that's all.");
                    data.Responses.Add($"It's just the dust from the storage room. It'll clear once I'm out in the open.");
                    data.Responses.Add($"I've got my inhaler. I'll be fine as long as I don't start running a marathon.");
                    data.Responses.Add(DialogueLine.WithTrait("I've breathed worse air than this and lived. I'll be fine.", "Optimist"));
                    
                    data.Closers.Add($"Pace yourself then. No use bringing back gear if you can't breathe to use it.");
                    data.Closers.Add($"Stay away from the collapsed tunnels. The dust is thickest there.");
                    data.Closers.Add($"If you start wheezing, head back immediately. No arguments.");
                    break;
                default:
                    data.Openers.Add($"You've had a rough few weeks. Maybe stay in the bunker this time?");
                    data.Responses.Add($"I need to be useful. I'm not ready to be a burden yet.");
                    data.Closers.Add($"We all contribute in our own way. Your health is a contribution too.");
                    break;
            }
            return data;
        }
        public static List<DialogueLine> GetAwayObservationOptions(string awayMemberName, int ageYears, List<string> illnesses)
        {
            var options = new List<DialogueLine>();

            // General worry/thinking about them
            options.Add($"I hope {awayMemberName} is staying safe out there. The wastes are unpredictable today.");
            options.Add($"It's too quiet in here without {awayMemberName}. I hope they've found a good place to hunk down.");
            options.Add($"I find myself checking the airlock every hour, waiting for {awayMemberName} to cycle through.");
            options.Add($"Did {awayMemberName} take enough water? I should have checked their pack one last time.");
            
            if (ageYears >= 60)
            {
                options.Add($"{awayMemberName} is {ageYears}... I worry about their persistence on these long hauls.");
                options.Add($"Most people {awayMemberName}'s age are long gone. Every run they take is a miracle.");
                options.Add(DialogueLine.WithTrait($"I trust {awayMemberName}. They've seen more than the rest of us combined.", "Optimist"));
            }

            foreach (var illId in illnesses)
            {
                if (illId.Contains("arthritis"))
                    options.Add($"The cold on the surface is going to be hell on {awayMemberName}'s joints.");
                if (illId.Contains("dementia"))
                    options.Add($"I just hope {awayMemberName} remembers the cardinal directions if the map fails.");
                if (illId.Contains("heart"))
                    options.Add($"Every time the radio crackles, I'm afraid it's {awayMemberName}'s heart finally giving out.");
                if (illId.Contains("respiratory"))
                    options.Add($"The dust storms are picking up. {awayMemberName}'s lungs can't handle that kind of grit.");
            }

            return options;
        }
    }
}
