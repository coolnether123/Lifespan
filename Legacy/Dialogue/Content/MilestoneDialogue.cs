using System.Collections.Generic;

namespace Lifespan.Dialogue.Content
{
    public static class MilestoneDialogue
    {
        public static List<DialogueLine> GetJournalBirthdayOptions(int age, bool oldest, bool youngest, bool hasChildren)
        {
            var options = new List<DialogueLine>();
            if (oldest)
            {
                options.Add("{0} is {1} today. The eldest of us all, a pillar of this family.");
                options.Add("Happy birthday to {0} ({1}). They carry the weight of our history.");
                options.Add("At {1}, {0} is the living memory of the world before the fallout.");
                options.Add("{0} reached {1}. Every grey hair is a story of a day the wastes didn't win.");
                options.Add("Wisdom comes with age, and {0} is now {1}. We are lucky to have them.");
                options.Add("{1} years for {0}. A remarkable feat in these harsh times.");
                options.Add("{0} is {1}. They've outlived the very world that built this bunker.");
                options.Add("The venerable {0} is {1} today. Respect is due to our longest survivor.");
                options.Add(DialogueLine.WithTrait("{0} reached {1} and still smiles. Their spirit is unbreakable.", "Optimist"));
                options.Add(DialogueLine.WithTrait("{0} is {1}. They say they've seen enough for ten lifetimes.", "Pessimist"));
            }
            else if (youngest)
            {
                options.Add("{0} turned {1} today. Our youngest, growing up in a world of shadows.");
                options.Add("Another year for our youngest, {0}. {1} years old and full of potential.");
                options.Add("{0} is {1}. The bunker's steel walls are the only sky they've ever known.");
                options.Add("Happy birthday to {0} ({1}). May they see a better world than we did.");
                options.Add("Our little miracle {0} is {1} now. Time is moving too fast in this hole.");
                options.Add("{0} reached {1}. They represent the future we're all fighting for.");
                options.Add("{1} years for {0}. They are the heart and hope of this family.");
                options.Add("{0} is {1} today. Their laughter makes the silence of the shelter easier to bear.");
                options.Add(DialogueLine.WithTrait("{0} is only {1} but already shows a true survivor's grit.", "Courageous"));
                options.Add(DialogueLine.WithTrait("Happy {1}th, {0}. Their relentless hope is our greatest fuel.", "Optimist"));
            }
            else
            {
                options.Add("{0} reached {1} today. Another year of survival in the bunker.");
                options.Add("Happy birthday, {0}. {1} years strong against the waste.");
                options.Add("{0} is {1} today. A milestone worth celebrating even in the dark.");
                options.Add("Another year down for {0}. They've earned every bit of their {1} years.");
                options.Add("{0} turned {1}. Life goes on, even three levels underground.");
                options.Add("{1} years for {0}. They've become an essential part of our survival story.");
                options.Add("Happy birthday, {0} ({1}). Here's to many more rotations of the air filters.");
                options.Add("{0} is {1} now. The years are hard, but they are yours to keep.");
                options.Add(DialogueLine.WithTrait("{0} reached {1}. They've spent these years keeping us running.", "HardWorker"));
                options.Add(DialogueLine.WithTrait("{0} is {1}. Just another year closer to the end, they say.", "Pessimist"));
            }

            if (hasChildren)
            {
                options.Add("Watching {0} reach {1}... it makes me wonder what kind of world their children will see.");
                options.Add("{0} is {1}. Their children look at them with such pride and love today.");
                options.Add("Happy birthday {0} ({1}). Being a parent in this place is a victory in itself.");
                options.Add("{0} reached {1}. They are building a legacy for the little ones to follow.");
                options.Add(DialogueLine.WithTrait("{0} is {1}. They hope to see their children flourish in the wastes.", "Optimist"));
            }

            return options;
        }

        public static List<DialogueLine> GetAgeObservationOptions(string firstName, int ageYears, int observerAge)
        {
            var options = new List<DialogueLine>();
            if (ageYears < 10)
            {
                options.Add(firstName + " is growing up so fast. " + ageYears + " years in the bunker already.");
                options.Add("Hard to believe they've only seen the sun in books.");
                options.Add("They have their whole life ahead of them, even in this place.");
                options.Add("Every year " + firstName + " gets a little taller. " + ageYears + " years of bunker rations seem to be doing the trick.");
                options.Add("I wonder what " + firstName + " thinks of us. Ten years in this hole is all they know.");
                options.Add(firstName + " is " + ageYears + ". I remember when they were just a bundle of blankets.");
                options.Add("They're so full of energy. I hope they never lose that spark.");
                options.Add("Watching them play... it's the only thing that makes this place feel like a home.");
                options.Add(DialogueLine.WithTrait(firstName + " is already trying to help with the filters. Such a hard worker.", "HardWorker"));
                options.Add(DialogueLine.WithTrait("The world is a dark place for a " + ageYears + " year old.", "Pessimist"));
            }
            else
            {
                options.Add(firstName + " won't be a child much longer. " + ageYears + " years... I've taught them what I can.");
                options.Add("They'll be an adult before we know it. I hope they're prepared.");
                options.Add(firstName + " is almost ready for the wastes. I wish they didn't have to be.");
                options.Add(firstName + " is standing taller every day. " + ageYears + " years of bunker life hasn't broken them.");
                options.Add("They're starting to carry themselves with real purpose. " + ageYears + " years and they're already a survivor.");
                options.Add("I see a lot of ourselves in " + firstName + ". For better or worse.");
                options.Add("They're " + ageYears + " now. Time to start giving them more responsibility.");
                options.Add("The transition from child to adult is hard enough without the radiation.");
                options.Add(DialogueLine.WithTrait("They have the grit we need. " + ageYears + " years of bunker life made them tough.", "Courageous"));
                options.Add(DialogueLine.WithTrait("I fear for them once they step outside these walls.", "Cowardly"));
                
                if (observerAge >= ageYears * 1.5)
                {
                    options.Add(ageYears + " years of surviving. They've got the grit of someone twice their age.");
                    options.Add("Only " + ageYears + ", but they carry themselves like a veteran of the surface.");
                    options.Add("I was twice their age when I first saw the wastes. They're ahead of the curve.");
                    options.Add("They've seen more at " + ageYears + " than most people did in a lifetime before.");
                    options.Add(DialogueLine.WithTrait("They're ready. I can see it in their eyes.", "Optimist"));
                }
            }
            return options;
        }

        public static List<DialogueLine> GetRoutineBirthdayOptions(string name, int age, string item, bool isElder, bool isYoung, bool isStressed, bool isHappy)
        {
            var options = new List<DialogueLine>();
            options.Add("Another year, another rotation. I'm " + age + " today.");
            options.Add("Don't suppose there's any extra " + item + " for my birthday?");
            options.Add("I'm " + age + ". Still here, still breathing.");
            options.Add("Someone tell the generator it's my birthday. Maybe it'll stop rattling.");
            options.Add("Happy birthday to me. Another year of processed rations.");
            options.Add("I've reached " + age + ". I wonder how many more I've got left.");
            
            if (isElder)
            {
                options.Add("Sixty years... I never thought I'd see this day down here.");
                options.Add("Being an elder is just a fancy way of saying I'm creaky.");
                options.Add("I've seen empires rise and fall, and here I am, " + age + " and still fixing pipes.");
                options.Add("The young ones look to me for wisdom. I hope I have some left at " + age + ".");
                options.Add(DialogueLine.WithTrait("The years are long, but the days are short. Grateful for another one.", "Optimist"));
                options.Add(DialogueLine.WithTrait("Every bone aches. " + age + " years is a lot for a human body.", "Pessimist"));
            }
            
            if (isYoung)
            {
                options.Add("I'm " + age + "! Soon I'll be old enough for the expeditions.");
                options.Add("I'm growing up! " + age + " is almost an adult, right?");
                options.Add("Wait until next year, I'll be even taller!");
                options.Add("I want to see the trees everyone talks about. Maybe for my next birthday.");
                options.Add(DialogueLine.WithTrait("I'm going to be the best explorer this family has ever seen!", "Optimist"));
            }

            if (isStressed)
            {
                options.Add("I'm " + age + "... and I'm tired. So very tired.");
                options.Add("Another birthday in this hole. It's starting to feel like a cage.");
                options.Add("What's the point of reaching " + age + " if this is all there is?");
                options.Add("I can feel the bunker walls closing in with every passing year.");
                options.Add(DialogueLine.WithTrait("Everything is fading... I can't hold onto the details anymore.", "Pessimist"));
                options.Add(DialogueLine.WithTrait("I don't think I can take another year of this.", "Cowardly"));
            }

            if (isHappy)
            {
                options.Add("It's my birthday! Let's make the most of it.");
                options.Add("I feel great today. Reaching " + age + " is a victory!");
                options.Add("Who wants to celebrate? I'm " + age + " and still going strong!");
                options.Add("Maybe we can find something special in the storage rooms today?");
                options.Add(DialogueLine.WithTrait("I'm " + age + " and still the sharpest tool in the shed.", "Optimist"));
                options.Add(DialogueLine.WithTrait("I'm ready for whatever the wastes throw at me next.", "Courageous"));
            }

            return options;
        }

        public static string GetStatPersonalNote(string name, int str, int dex, int intel, bool olderChild)
        {
            if (intel > 15) return name + " is getting too smart for their own good.";
            if (str > 15) return name + " is becoming a real powerhouse.";
            if (dex > 15) return name + " is faster than a radiation storm.";
            if (olderChild && intel > 10) return "I'm glad " + name + " is taking their studies seriously.";
            return null;
        }

        public static List<DialogueLine> GetSelfReflectionOptions(int age, bool isStressed)
        {
            var options = new List<DialogueLine>();
            options.Add("I'm " + age + " now. The years are starting to blur.");
            options.Add("Another birthday. Another year of keeping this family alive.");
            options.Add(age + " years. I've spent more than half of them underground.");
            options.Add("I wonder what I would have been if the world hadn't ended.");
            options.Add("I'm " + age + ". I hope I've made the right choices.");
            options.Add("Reaching " + age + " is a privilege many didn't get.");
            
            if (isStressed)
            {
                options.Add("Does it even matter that I'm " + age + "? We're just waiting to die.");
                options.Add("I can't remember the last time I saw a real flower.");
                options.Add("The air in here is choking me. " + age + " years of recycled oxygen.");
                options.Add("I'm " + age + " and I have nothing to show for it but scars.");
                options.Add(DialogueLine.WithTrait("The weight of the years is getting too heavy.", "Pessimist"));
                options.Add(DialogueLine.WithTrait("I'm afraid of what comes next. " + age + " was supposed to be easier.", "Cowardly"));
            }
            else
            {
                options.Add(DialogueLine.WithTrait("I've still got plenty of fight left in me at " + age + ".", "Courageous"));
                options.Add(DialogueLine.WithTrait("I'm proud of what we've built here.", "Optimist"));
            }

            return options;
        }
    }
}
