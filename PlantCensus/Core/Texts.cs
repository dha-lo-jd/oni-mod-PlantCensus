namespace PlantCensus;

internal static class Texts
{
    // Names from the game carry <link> markup for the in-game encyclopedia; the list shows plain text.
    public static string StripLinks(string text) => string.IsNullOrEmpty(text) ? "" : global::STRINGS.UI.StripLinkFormatting(text);
}
