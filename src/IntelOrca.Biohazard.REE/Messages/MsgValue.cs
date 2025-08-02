namespace IntelOrca.Biohazard.REE.Messages
{
    public readonly struct MsgValue(LanguageId languageId, string text)
    {
        public LanguageId Language => languageId;
        public string Text => text;

        public override string ToString() => text;
    }
}
