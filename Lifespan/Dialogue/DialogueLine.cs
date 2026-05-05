namespace Lifespan
{
    /// <summary>
    /// Represents a single line of dialogue with optional trait-based weighting.
    /// </summary>
    public class DialogueLine
    {
        public string Text;
        public string TraitId;

        public DialogueLine(string text, string traitId = null)
        {
            Text = text;
            TraitId = traitId;
        }

        public static implicit operator DialogueLine(string text)
        {
            return new DialogueLine(text);
        }

        public static DialogueLine WithTrait(string text, string traitId)
        {
            return new DialogueLine(text, traitId);
        }
    }
}
