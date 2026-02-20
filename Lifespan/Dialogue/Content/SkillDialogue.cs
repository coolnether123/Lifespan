using System.Collections.Generic;

namespace Lifespan.Dialogue.Content
{
    public class SkillThemeData
    {
        public List<DialogueLine> Openers = new List<DialogueLine>();
        public List<DialogueLine> Responses = new List<DialogueLine>();
    }

    public static class SkillDialogue
    {
        public static SkillThemeData GetTheme(BaseStats.StatType statType, bool isCompetitive)
        {
            var theme = new SkillThemeData();
            switch (statType)
            {
                case BaseStats.StatType.Strength:
                    if (isCompetitive)
                    {
                        theme.Openers.Add("I didn't think anyone could pin my arm down. Well done!");
                        theme.Openers.Add("You've got a hell of a grip. Putting up a real fight!");
                        theme.Openers.Add("Almost had me there. Those muscles aren't just for show.");
                        theme.Openers.Add("I'm going to have to start training harder just to keep up with you.");
                        theme.Openers.Add("Whew! Where did you find that strength? You're like a machine.");
                        
                        theme.Responses.Add("I'll be as strong as you soon! Just you wait.");
                        theme.Responses.Add("Actually pinning your arm down? I must be getting better.");
                        theme.Responses.Add("It's all in the reach. You taught me that yourself.");
                        theme.Responses.Add("I just didn't want to let you win this time!");
                        theme.Responses.Add(DialogueLine.WithTrait("You're lucky I didn't break anything. I don't know my own strength anymore.", "Pessimist"));
                    }
                    else
                    {
                        theme.Openers.Add("Look at you lifting that! You're getting powerful.");
                        theme.Openers.Add("Good to see you focusing on your physical training.");
                        theme.Openers.Add("You're becoming a real asset for the heavy lifting.");
                        theme.Openers.Add("Keep that form steady. You're building real power there.");
                        theme.Openers.Add("I can see the progress. You’re handling those weights like they’re nothing.");
                        
                        theme.Responses.Add("I just want to be useful out there.");
                        theme.Responses.Add("Found some spare weights in storage.");
                        theme.Responses.Add("I want to be ready for the next expedition.");
                        theme.Responses.Add("It's hard work, but I'm starting to feel the difference.");
                        theme.Responses.Add(DialogueLine.WithTrait("Power is the only thing that matters in the wastes.", "Cowardly")); // Maybe they want power to feel safe
                    }
                    break;

                case BaseStats.StatType.Dexterity:
                    theme.Openers.Add(isCompetitive ? "You're getting almost as fast as me! Quick hands." : "You're getting really handy with those tools.");
                    theme.Openers.Add(isCompetitive ? "I can barely keep track of your movements. So fast!" : "That's some fine work on the generator. Your fingers are nimble.");
                    theme.Openers.Add("You move with a lot of grace. That'll save your life top-side.");
                    
                    theme.Responses.Add("Practice makes perfect, I guess.");
                    theme.Responses.Add("My hands used to shake so much. Glad they're steady now.");
                    theme.Responses.Add("I've been practicing with the lockpicks every night.");
                    theme.Responses.Add("Speed is the only thing the wastes can't catch.");
                    theme.Responses.Add(DialogueLine.WithTrait("The faster I am, the sooner I can get back home.", "Cowardly"));
                    break;

                case BaseStats.StatType.Intelligence:
                    theme.Openers.Add(isCompetitive ? "You're starting to outthink me. Scary thought!" : "Amazing how quickly you pick up new concepts.");
                    theme.Openers.Add(isCompetitive ? "Wait, how did you solve that puzzle so fast? I'm impressed." : "You've got a real knack for the technical stuff.");
                    theme.Openers.Add("Deep thinking is a rare trait these days. Keep that brain sharp.");
                    
                    theme.Responses.Add("I just read through that old manual. It's actually helpful.");
                    theme.Responses.Add("I'm starting to see the patterns in the circuitry.");
                    theme.Responses.Add("The logic just... clicks sometimes.");
                    theme.Responses.Add("I'd rather use my head than my fists if I can help it.");
                    theme.Responses.Add(DialogueLine.WithTrait("Everything can be solved if you just have the right data.", "HardWorker"));
                    break;

                default:
                    theme.Openers.Add("Doing great work. A fast learner.");
                    theme.Openers.Add("Wonderful to see you improving yourself.");
                    theme.Responses.Add("I'm trying my best.");
                    break;
            }
            return theme;
        }

        public static List<DialogueLine> GetEncouragementOptions()
        {
            return new List<DialogueLine>
            {
                "Keep it up. We're going to need that soon.",
                "Good. The surface doesn't care how tired you are.",
                "I'm glad. One less thing for me to worry about.",
                "That's the spirit. This shelter won't run itself.",
                "Exactly. We survived this long because we learned.",
                DialogueLine.WithTrait("I believe in you. You're going to be better than I ever was.", "Optimist")
            };
        }

        public static List<DialogueLine> GetPhilosophyOptions(int category)
        {
            var options = new List<DialogueLine>();
            switch (category)
            {
                case 0: // Gratitude
                    options.Add("I have lived a long life. Grateful to still be drawing breath in this hole.");
                    options.Add("Every day is a gift, even when the air tastes like rust.");
                    options.Add("I never thought I would see so many seasons pass down here.");
                    options.Add("Looking back, I've had a better run than most.");
                    options.Add("I'm thankful for this family. Without you, the dark would be unbearable.");
                    options.Add("We still have water, heat, and each other. That's more than most have left.");
                    options.Add(DialogueLine.WithTrait("I never expected much from life, so every good day is a surprise.", "Pessimist"));
                    break;
                case 1: // Life Lessons
                    options.Add("Make the most of your youth. Time goes faster than a radiation cloud.");
                    options.Add("In this world, family is the only currency that doesn't lose value.");
                    options.Add("Don't take a single day for granted. Tomorrow is never promised.");
                    options.Add("Cherish the quiet moments. They're all we really have.");
                    options.Add("The surface is a harsh teacher, but she never lies.");
                    options.Add("Trust your gut, but always check your filters twice.");
                    options.Add(DialogueLine.WithTrait("Experience is just the name we give to our scars.", "Pessimist"));
                    break;
                case 2: // Legacy
                    options.Add("I hope I've passed on enough grit to keep you lot going when I'm gone.");
                    options.Add("When I'm gone, keep building. Don't let the wastes take what we've won.");
                    options.Add("The young will outstrip us eventually. That's the only way we win.");
                    options.Add("Leave the shelter better than you found it.");
                    options.Add("I want you to remember the stories I told you. Keep them alive.");
                    options.Add("My time is nearing its end, but the family's story is just beginning.");
                    options.Add(DialogueLine.WithTrait("I'm not leaving much behind, but I'm leaving you. That's enough.", "Optimist"));
                    break;
                default: // Observing Young
                    options.Add("They remind me of myself before the world turned grey.");
                    options.Add("I hope they have it easier than I did. A little hope goes a long way.");
                    options.Add("Watching them work... it makes the struggle worth it.");
                    options.Add("The energy of youth. I miss that. Keep it burning.");
                    break;
            }
            return options;
        }
    }
}
