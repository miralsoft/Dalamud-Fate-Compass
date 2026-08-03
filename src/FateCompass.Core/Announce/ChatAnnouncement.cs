namespace FateCompass.Core.Announce;

/// <summary>
/// Builds the line that gets typed into the chat box for the leading FATE.
/// </summary>
/// <remarks>
/// A pure string builder with no framework in it, so the shape of the message can be pinned
/// down by tests rather than by looking at it in game.
/// <para>
/// It only ever produces text. Putting that text in front of the player is the adapter's job,
/// and sending it is nobody's job but the player's (FH-01).
/// </para>
/// </remarks>
public static class ChatAnnouncement
{
    /// <summary>
    /// The game's own placeholder for the current map flag, which expands when the message is
    /// sent. Written rather than coordinates because only this becomes a link the reader can
    /// click.
    /// </summary>
    public const string FlagPlaceholder = "<flag>";

    /// <summary>
    /// Builds the line, or null when there is nothing sensible to say.
    /// </summary>
    /// <param name="channelCommand">Channel prefix such as <c>/p</c>. Empty writes no prefix.</param>
    /// <param name="fateName">Name of the FATE being pointed at.</param>
    /// <param name="includeName">Whether to name the FATE as well as flagging it.</param>
    public static string? Build(string channelCommand, string fateName, bool includeName)
    {
        var parts = new List<string>(3);

        var channel = (channelCommand ?? string.Empty).Trim();
        if (channel.Length > 0)
        {
            parts.Add(channel);
        }

        parts.Add(FlagPlaceholder);

        // The flag first, then the name. The reader's eye goes to the link either way, and one
        // fixed order means one less thing to decide and one less setting to explain.
        if (includeName && !string.IsNullOrWhiteSpace(fateName))
        {
            parts.Add(fateName.Trim());
        }

        return string.Join(' ', parts);
    }
}
