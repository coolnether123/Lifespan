using System.Collections.Generic;

namespace Lifespan.Dialogue.Content
{
    public static class IllnessDialogue
    {
        public static List<DialogueLine> GetFlavorOptions(string illnessId)
        {
            var options = new List<DialogueLine>();
            switch (illnessId)
            {
                case ElderIllnessManager.ILLNESS_MILD_DEMENTIA:
                    options.Add("I... I can't remember where I put my rations.");
                    options.Add("Did we just eat? Or was that yesterday?");
                    options.Add("Everyone's faces look so... blurry today.");
                    options.Add("I swear I heard the radio, but it's off.");
                    options.Add("Why did I come into this room again?");
                    options.Add("Something feels missing, but I don't know what.");
                    options.Add("The lights flicker, but maybe it's just my eyes.");
                    options.Add("I could have sworn I saw someone standing by the door.");
                    options.Add("My thoughts feel like they're slipping through my fingers.");
                    options.Add("Is it Tuesday? It feels like it should be Tuesday.");
                    options.Add("The map... the routes... they're all tangled in my head.");
                    options.Add("I used to know this bunker like the back of my hand. Now it's a labyrinth.");
                    options.Add("Did I tell you about the world before? I feel like I'm losing the words.");
                    options.Add("I'm looking for someone. I... I can't quite remember who.");
                    options.Add(DialogueLine.WithTrait("The past is a fog, and the future is a void.", "Pessimist"));
                    options.Add(DialogueLine.WithTrait("The memories are like ghosts in the dark.", "Pessimist"));
                    options.Add(DialogueLine.WithTrait("I still have my notes. I won't let the dark take everything.", "Optimist"));
                    break;
                case ElderIllnessManager.ILLNESS_DEMENTIA:
                    options.Add("Who are you people? Why are we underground?");
                    options.Add("I need to go home. My mother is waiting.");
                    options.Add("The walls... they're breathing again.");
                    options.Add("I don't know what this tool is for anymore.");
                    options.Add("The voices... they aren't making sense!");
                    options.Add("I want to leave! Open the door!");
                    options.Add("I'm late for work... I need to find my keys.");
                    options.Add("Don't look at me like that, I know who you're hiding!");
                    options.Add("The shadows... they're whispering about the surface.");
                    options.Add("Where is my bed? This isn't my room.");
                    options.Add("Everything is white... like a static on the old TVs.");
                    options.Add("I can't feel my own name anymore.");
                    options.Add("The bunker is a cage. Why did they trap me here?");
                    options.Add("Stop moving the rooms! I can't find the exit!");
                    options.Add(DialogueLine.WithTrait("None of this is real. It's just a long, terrible dream.", "Pessimist"));
                    options.Add(DialogueLine.WithTrait("I'm not afraid of the dark. I've lived in it long enough.", "Courageous"));
                    break;
                case ElderIllnessManager.ILLNESS_MILD_ARTHRITIS:
                    options.Add("Ouch! My knees aren't what they used to be.");
                    options.Add("Cold damp air... goes right to the bone.");
                    options.Add("Just need a minute... stiff joints.");
                    options.Add("Can someone help me with this lid? My hands hurt.");
                    options.Add("Feels like rain coming... my ankles are throbbing.");
                    options.Add("I'm just a bit creaky this morning.");
                    options.Add("I'm moving like a rusty hinge today.");
                    options.Add("Every step feels like walking on gravel.");
                    options.Add("My back... it's just one long ache.");
                    options.Add("I need to sit for a bit, my hips are on fire.");
                    options.Add("The humidity in here is a curse for my bones.");
                    options.Add("I've been on these feet for sixty years. They're finally complaining.");
                    options.Add("My fingers feel like they're made of stone today.");
                    options.Add("Just a bit of friction in the gears, that's all.");
                    options.Add(DialogueLine.WithTrait("Still standing, at least. Can't let a bit of stiffness stop me.", "Optimist"));
                    options.Add(DialogueLine.WithTrait("I've survived sixty rotations. My joints are just keeping score.", "Courageous"));
                    options.Add(DialogueLine.WithTrait("Every morning is a battle with my own body.", "Pessimist"));
                    break;
                case ElderIllnessManager.ILLNESS_ARTHRITIS:
                    options.Add("I can't... I can't move my fingers.");
                    options.Add("The pain... it never stops.");
                    options.Add("I'm useless like this. I can't even stand.");
                    options.Add("My bones feel like they're grinding to dust.");
                    options.Add("Everything is locked up tight.");
                    options.Add("Please... make the pain stop.");
                    options.Add("My joints are swollen to twice their size.");
                    options.Add("I can't even hold a cup of water anymore.");
                    options.Add("Every movement is a battle I'm losing.");
                    options.Add("I feel like my bones are fusing together.");
                    break;
                case ElderIllnessManager.ILLNESS_MILD_HEART:
                    options.Add("My chest feels... tight.");
                    options.Add("Just a bit out of breath, give me a second.");
                    options.Add("Is it hot in here? I feel flushed.");
                    options.Add("Strange fluttering feeling...");
                    options.Add("Need to... catch my breath.");
                    options.Add("Everything is spinning a little.");
                    options.Add("My chest feels like it's in a vice.");
                    options.Add("I can feel my heart pounding against my ribs.");
                    options.Add("I'm seeing spots... just need a moment.");
                    options.Add("Why is it so hard to get a full breath?");
                    break;
                case ElderIllnessManager.ILLNESS_HEART:
                    options.Add("It feels like an elephant is sitting on my chest.");
                    options.Add("My left arm... it's going numb.");
                    options.Add("I can hear my own heartbeat in my ears.");
                    options.Add("I'm dizzy... I need to sit down immediately.");
                    options.Add("Call for help... can't breathe properly.");
                    options.Add("My heart feels like it's exploding.");
                    options.Add("It's a heavy weight... crushing me.");
                    options.Add("My jaw... why does my jaw hurt so much?");
                    options.Add("The room is spinning... I can't see straight.");
                    options.Add("Everything is going dark... help...");
                    options.Add(DialogueLine.WithTrait("I'm not afraid. If this is the end, I've lived enough.", "Courageous"));
                    break;
                case ElderIllnessManager.ILLNESS_MILD_FRAILTY:
                    options.Add("This pack feels heavier than it did yesterday.");
                    options.Add("I bruise so easily these days.");
                    options.Add("Just feeling a bit weak, that's all.");
                    options.Add("I stepped wrong and twisted something.");
                    options.Add("My grip isn't what it used to be.");
                    options.Add("Why is everything so heavy?");
                    options.Add("I feel like I'm losing weight without trying.");
                    options.Add("My skin looks so thin, like paper.");
                    options.Add("I'm exhausted after just a few steps.");
                    options.Add("I feel... diminished somehow.");
                    break;
                case ElderIllnessManager.ILLNESS_FRAILTY:
                    options.Add("I feel like I'm made of glass.");
                    options.Add("I don't have the strength to lift that.");
                    options.Add("My legs are shaking just standing here.");
                    options.Add("I'm wasting away... look at my arms.");
                    options.Add("I feel like a stiff breeze would break me.");
                    options.Add("Help me up... I can't do it.");
                    options.Add("I can barely support my own weight.");
                    options.Add("My muscles... they're just gone.");
                    options.Add("I'm cold... so very cold.");
                    options.Add("I'm just a ghost of what I was.");
                    break;
                case ElderIllnessManager.ILLNESS_MILD_RESPIRATORY:
                    options.Add("*Cough* Just a tickle in my throat.");
                    options.Add("It's... *wheeze*... dusty in here.");
                    options.Add("I get winded just walking up the stairs.");
                    options.Add("Can we turn up the ventilation?");
                    options.Add("Trying to clear my throat... won't go away.");
                    options.Add("Just a little shortness of breath.");
                    options.Add("My chest is whistling with every breath.");
                    options.Add("I can't seem to clear the congestion.");
                    options.Add("I'm always searching for more air.");
                    options.Add("This air feels so thin and heavy.");
                    options.Add("The scrap heap dust is finally catching up with me.");
                    break;
                case ElderIllnessManager.ILLNESS_RESPIRATORY:
                    options.Add("I can't... *gasp*... breathe.");
                    options.Add("It feels like drowning... on dry land.");
                    options.Add("*Violent coughing fit*");
                    options.Add("My lungs... burning...");
                    options.Add("Air... need... more... air.");
                    options.Add("Every breath hurts.");
                    options.Add("I'm drowning... help me...");
                    options.Add("My throat is closing...");
                    options.Add("I can't speak... no air...");
                    options.Add("It's like breathing through a straw.");
                    break;
                default:
                    options.Add("I don't feel quite right.");
                    options.Add("Age catches up to us all.");
                    options.Add("Just getting old, I suppose.");
                    options.Add("My body is failing me.");
                    options.Add("Days are getting harder.");
                    options.Add("Just need some rest.");
                    break;
            }
            return options;
        }

        public static List<DialogueLine> GetObservationOptions(string victimName, string illnessId)
        {
            var options = new List<DialogueLine>();
            switch (illnessId)
            {
                case ElderIllnessManager.ILLNESS_MILD_DEMENTIA:
                case ElderIllnessManager.ILLNESS_DEMENTIA:
                    options.Add($"{victimName} has been staring at walls lately.");
                    options.Add("I think they're forgetting things significantly.");
                    options.Add($"{victimName} called me by the wrong name.");
                    options.Add("The lights are on but nobody's home with them.");
                    options.Add($"Did {victimName} ask you that same question twice?");
                    options.Add("They look so lost lately.");
                    options.Add($"{victimName} is talking to people who aren't there.");
                    options.Add("I found them wandering in the storage room again.");
                    options.Add($"{victimName} doesn't seem to know where they are.");
                    options.Add("It's getting harder to reach them.");
                    break;
                case ElderIllnessManager.ILLNESS_MILD_ARTHRITIS:
                case ElderIllnessManager.ILLNESS_ARTHRITIS:
                    options.Add($"{victimName} is really struggling to move.");
                    options.Add("We should let them rest, their joints are swollen.");
                    options.Add("It hurts just watching them walk.");
                    options.Add($"{victimName} can barely hold a wrench.");
                    options.Add($"I can hear {victimName}'s knees popping from here.");
                    options.Add("The cold floor isn't good for them.");
                    options.Add($"{victimName} is rubbing their hands constantly.");
                    options.Add("They're wincing with every step today.");
                    options.Add($"{victimName} is much slower than last week.");
                    options.Add("I think the arthritis is winning.");
                    break;
                case ElderIllnessManager.ILLNESS_MILD_HEART:
                case ElderIllnessManager.ILLNESS_HEART:
                    options.Add($"{victimName} is clutching their chest.");
                    options.Add("They look pale and sweaty.");
                    options.Add($"We need to keep {victimName}'s stress down.");
                    options.Add("They're breathing irregularly.");
                    options.Add($"{victimName} needs to sit down, now.");
                    options.Add("Their pulse is all over the place.");
                    options.Add($"{victimName} looks like they've seen a ghost.");
                    options.Add("They're gasping for air after the slightest effort.");
                    options.Add($"{victimName}'s lips look a bit blue.");
                    options.Add("Something is very wrong with their heart.");
                    break;
                case ElderIllnessManager.ILLNESS_MILD_FRAILTY:
                case ElderIllnessManager.ILLNESS_FRAILTY:
                    options.Add($"{victimName} looks thinner every day.");
                    options.Add("They almost fell over just now.");
                    options.Add("Don't let them carry heavy loads.");
                    options.Add("They look so fragile.");
                    options.Add($"{victimName} is fading away.");
                    options.Add("I'm worried they'll break something.");
                    options.Add($"{victimName} is trembling just standing there.");
                    options.Add("They can't even lift a water bucket anymore.");
                    options.Add($"{victimName} is skin and bones.");
                    options.Add("They have no strength left.");
                    break;
                case ElderIllnessManager.ILLNESS_MILD_RESPIRATORY:
                case ElderIllnessManager.ILLNESS_RESPIRATORY:
                    options.Add($"{victimName}'s coughing is keeping me awake.");
                    options.Add("Their breathing sounds like a rattle.");
                    options.Add("Is there blood in that handkerchief?");
                    options.Add("They're gasping for air constantly.");
                    options.Add("That cough sounds deep in the chest.");
                    options.Add("Oxygen... they need more air.");
                    options.Add($"{victimName} is wheezing with every single breath.");
                    options.Add("Their chest is heaving just to get a little air.");
                    options.Add("That sound... it sounds like fluid.");
                    options.Add("I don't think their lungs have long left.");
                    options.Add("I can hear {victimName} gasping from the next room.");
                    break;
            }
            return options;
        }

        public static string GetHeartAttackMessage(string firstName) => $"{firstName} suffered a heart attack!";
        public static string GetMinorHeartPalpitationMessage(string firstName) => $"{firstName} felt a confusing flutter in their chest.";

        public class IllnessConversationData
        {
            public List<DialogueLine> Openers = new List<DialogueLine>();
            public List<DialogueLine> Responses = new List<DialogueLine>();
            public List<DialogueLine> Closers = new List<DialogueLine>();
        }

        public static IllnessConversationData GetConversation(string illnessId)
        {
            var data = new IllnessConversationData();
            switch (illnessId)
            {
                case ElderIllnessManager.ILLNESS_MILD_DEMENTIA:
                case ElderIllnessManager.ILLNESS_DEMENTIA:
                    data.Openers.Add("The radio's been quiet today, hasn't it?");
                    data.Openers.Add("I was looking for that old manual we were reading yesterday.");
                    data.Openers.Add("Did you see where I put the spare filters? I swear they were right here.");
                    data.Openers.Add("It's a bit cold in here, don't you think? Maybe the heater is off.");
                    
                    data.Responses.Add("I... I thought I heard it. Was there a broadcast? I can't quite find the right frequency.");
                    data.Responses.Add("Manual? I... I recall the pages, but the words... they seem to have shifted places.");
                    data.Responses.Add("Filters? I... maybe I used them? No, that doesn't feel right either.");
                    data.Responses.Add("Heater? I... I hadn't noticed. My mind must have been elsewhere.");
                    data.Responses.Add(DialogueLine.WithTrait("Everything is fading... I can't hold onto the details anymore.", "Pessimist"));
                    
                    data.Closers.Add("It's alright. We'll check the logs together later.");
                    data.Closers.Add("Don't worry about it. I probably left it in the storage room myself.");
                    data.Closers.Add("I'll help you look. Two pairs of eyes are better than one.");
                    data.Closers.Add("The air is fine. Let's just sit for a minute.");
                    data.Closers.Add(DialogueLine.WithTrait("The mind is a treacherous thing. We'll get through it together.", "Optimist"));
                    break;

                case ElderIllnessManager.ILLNESS_MILD_ARTHRITIS:
                case ElderIllnessManager.ILLNESS_ARTHRITIS:
                    data.Openers.Add("That's a nasty grip you've got on that wrench. Want me to take over?");
                    data.Openers.Add("You're moving a bit like a rusty hinge today. Need some oil?");
                    data.Openers.Add("Let me handle the heavy crates. You've done enough for today.");
                    data.Openers.Add("Your hands are looking a bit swollen. Maybe take a break from the repairs?");
                    
                    data.Responses.Add("It’s just... the air's a bit thick. My hands feel like they’re made of lead.");
                    data.Responses.Add("Just a bit stiff. This concrete floor never was very forgiving, was it?");
                    data.Responses.Add("I can still do my part. It just... takes a little longer than it used to.");
                    data.Responses.Add("They're just... uncooperative today. Like they've forgotten how to hold things.");
                    data.Responses.Add(DialogueLine.WithTrait("I've survived the wastes, I can survive a bit of joint pain.", "Courageous"));
                    
                    data.Closers.Add("Rest them for a bit. The tools aren't going anywhere.");
                    data.Closers.Add("Sit by the stove for a while. The warmth might help.");
                    data.Closers.Add("We're a team. Use those hands for something lighter for a while.");
                    data.Closers.Add("There's no shame in slowing down. We've all seen some miles.");
                    break;

                case ElderIllnessManager.ILLNESS_MILD_HEART:
                case ElderIllnessManager.ILLNESS_HEART:
                    data.Openers.Add("You're looking a bit flushed. Maybe slow down on the water pump?");
                    data.Openers.Add("Everything alright? You've been standing still for a while.");
                    data.Openers.Add("Sit down for a second. Your face is as pale as a ghost's.");
                    data.Openers.Add("I heard your heart racing from across the room. Take it easy.");
                    
                    data.Responses.Add("Just a bit of... pressure. Like the bunker air is too heavy. I'll be fine in a moment.");
                    data.Responses.Add("My heart is just... racing for no reason. Like it's trying to keep up with something I can't see.");
                    data.Responses.Add("I... I felt a bit dizzy, that's all. It'll pass if I just breathe.");
                    data.Responses.Add("It's just a bit of excitement... or maybe the lack of it. I'm okay.");
                    data.Responses.Add(DialogueLine.WithTrait("It's just a bit of stress. I've been through worse.", "Courageous"));
                    
                    data.Closers.Add("Sit down. I'll finish this shift.");
                    data.Closers.Add("Deep breaths. It's just the stress of the shelter.");
                    data.Closers.Add("I'm getting you some water. Don't you dare move.");
                    data.Closers.Add("We'll keep an eye on you. No more heavy work today.");
                    break;

                case ElderIllnessManager.ILLNESS_MILD_FRAILTY:
                case ElderIllnessManager.ILLNESS_FRAILTY:
                    data.Openers.Add("Careful on that ladder. It's wobblier than usual.");
                    data.Openers.Add("That pack looks heavier than it did this morning.");
                    data.Openers.Add("Let me take that. You've been carrying it for miles.");
                    data.Openers.Add("You're looking a bit thin lately. Are you eating enough?");
                    
                    data.Responses.Add("Is it? I thought my legs were just... tired. Everything feels so much further away lately.");
                    data.Responses.Add("I could have sworn I carried more than this back in the day. Must be the rations.");
                    data.Responses.Add("I... I just need a moment to catch my balance. The floor feels a bit uneven.");
                    data.Responses.Add("I'm fine. Just... the weight of the world, I suppose.");
                    
                    data.Closers.Add("Just take it slow. We're in no rush.");
                    data.Closers.Add("Leave the heavy lifting to the younger ones for now.");
                    data.Closers.Add("I'll help you with the rest. We're almost there.");
                    data.Closers.Add("Rest is also a part of survival. Don't forget that.");
                    data.Closers.Add(DialogueLine.WithTrait("We've still got plenty of life left in us, despite the weakness.", "Optimist"));
                    break;

                case ElderIllnessManager.ILLNESS_MILD_RESPIRATORY:
                case ElderIllnessManager.ILLNESS_RESPIRATORY:
                    data.Openers.Add("The filters must be acting up again. Smells like dust in here, doesn't it?");
                    data.Openers.Add("That's a persistent tickle you've got. Want some water?");
                    data.Openers.Add("You're sounding a bit wheezy. Maybe take a break from the ventilation repair?");
                    data.Openers.Add("That cough sounds like it's coming from deep down. Are you alright?");
                    
                    data.Responses.Add("I can't... find the clean air. Feels like I'm breathing through a wool blanket.");
                    data.Responses.Add("It's just the stale air. It gets in deep and doesn't want to leave.");
                    data.Responses.Add("I... I just need to catch my breath. The air feels so thin today.");
                    data.Responses.Add("It's nothing... just a bit of dust. I've survived worse.");
                    data.Responses.Add(DialogueLine.WithTrait("My lungs are as old as the bunker. They're just complaining.", "Optimist"));
                    
                    data.Closers.Add("I'll check the vents. Stay near the recycler.");
                    data.Closers.Add("Try to keep your activity down until the air clears.");
                    data.Closers.Add("I'm getting you some water. Just sit and breathe.");
                    data.Closers.Add("We'll get through this. One breath at a time.");
                    break;

                default:
                    data.Openers.Add("You're looking a bit tired today.");
                    data.Responses.Add("Just the weight of the years, I suppose.");
                    data.Closers.Add("Go on then, get some rest.");
                    break;
            }
            return data;
        }
    }
}
