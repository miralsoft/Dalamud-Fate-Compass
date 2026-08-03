using FateHelper.Core.Localization;

namespace FateHelper.Core.Announce;

/// <summary>
/// The channels the announcement can be addressed to.
/// </summary>
/// <remarks>
/// A preset list rather than free text, because a mistyped command is only noticed once the
/// message has already been sent somewhere unintended.
/// <para>
/// The commands are the English short forms. The client accepts those whatever language it runs
/// in, and unlike the translated ones they do not change under us when the interface language
/// does. If one ever turns out not to work, the cost is small and visible: the player presses
/// enter and the game says it does not know the command. Nothing is sent anywhere wrong.
/// </para>
/// </remarks>
public static class ChatChannels
{
    /// <summary>
    /// One selectable channel: what gets typed, and how it is named to the player.
    /// </summary>
    /// <param name="Command">The text command written into the chat box.</param>
    /// <param name="LabelKey">Translation key for the name shown in the picker.</param>
    /// <param name="Number">
    /// Linkshell number, filled into the label. Zero for the channels that have no number.
    /// </param>
    /// <remarks>
    /// The name is a key rather than a word, because these are game channels the player knows by
    /// their translated names: a German client calls the party "Gruppe", and a picker offering
    /// "Party" is asking them to translate back.
    /// </remarks>
    public sealed record Channel(string Command, string LabelKey, int Number = 0);

    /// <summary>
    /// Echo first, deliberately. It is the only channel nobody else sees, which makes it the
    /// safe place to try the feature out before pointing it at a party.
    /// </summary>
    public static IReadOnlyList<Channel> All { get; } =
    [
        new("/echo", StringKeys.ChannelEcho),
        new("/say", StringKeys.ChannelSay),
        new("/p", StringKeys.ChannelParty),
        new("/a", StringKeys.ChannelAlliance),
        new("/fc", StringKeys.ChannelFreeCompany),
        new("/shout", StringKeys.ChannelShout),
        new("/yell", StringKeys.ChannelYell),
        new("/cwl1", StringKeys.ChannelCrossWorld, 1),
        new("/l1", StringKeys.ChannelLinkshell, 1),
        new("/l2", StringKeys.ChannelLinkshell, 2),
        new("/l3", StringKeys.ChannelLinkshell, 3),
        new("/l4", StringKeys.ChannelLinkshell, 4),
        new("/l5", StringKeys.ChannelLinkshell, 5),
        new("/l6", StringKeys.ChannelLinkshell, 6),
        new("/l7", StringKeys.ChannelLinkshell, 7),
        new("/l8", StringKeys.ChannelLinkshell, 8),
    ];

    /// <summary>The names of every channel, in list order, in the player's language.</summary>
    public static string[] LabelsFor(Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        return [.. All.Select(channel => channel.Number > 0
            ? localizer.Format(channel.LabelKey, channel.Number)
            : localizer.Get(channel.LabelKey))];
    }

    /// <summary>Where a fresh installation points: the party, which is who this is for.</summary>
    public const string DefaultCommand = "/p";

    /// <summary>Position of a command in the list, or zero when it is not one of them.</summary>
    public static int IndexOf(string command)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (string.Equals(All[i].Command, command, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }
}
