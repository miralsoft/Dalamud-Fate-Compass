using FateHelper.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace FateHelper.Adapters;

/// <summary>
/// Types text into the player's chat box without sending it.
/// </summary>
/// <remarks>
/// The line is put where the player would have typed it and left there. Pressing enter, and
/// choosing which channel it goes to, stays entirely theirs. Nothing is transmitted, so this
/// keeps the plugin on the right side of the line it draws around the server (FH-01): it types,
/// the player sends.
/// <para>
/// The text is inserted at the cursor rather than replacing what is already in the box, so a
/// half-written message survives.
/// </para>
/// </remarks>
internal static unsafe class ChatInput
{
    private const string ChatLogAddon = "ChatLog";

    /// <summary>
    /// The game's own placeholder for the current map flag. Typing this rather than coordinates
    /// means the game expands it when the message is sent, exactly as if the player had typed it.
    /// </summary>
    internal const string FlagPlaceholder = "<flag>";

    /// <summary>
    /// True when the chat box holds nothing at all.
    /// </summary>
    /// <remarks>
    /// The gate on anything written without the player asking for it right then. Half a typed
    /// message is worth more to them than a keypress saved, and there is no way to put it back.
    /// Returns false when the box cannot be read, so an unknown state is treated as occupied.
    /// </remarks>
    internal static bool IsEmpty()
    {
        try
        {
            if (DalamudServices.Framework.IsFrameworkUnloading)
            {
                return false;
            }

            var handle = DalamudServices.GameGui.GetAddonByName(ChatLogAddon);
            var addon = (AddonChatLog*)handle.Address;

            if (addon is null || addon->TextInput is null)
            {
                return false;
            }

            return addon->TextInput->AtkComponentInputBase.RawString.AsSpan().Length == 0;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "ChatInput: could not read the chat box");
            return false;
        }
    }

    /// <summary>
    /// Appends text to whatever is in the chat box. Returns false when it cannot be reached.
    /// </summary>
    /// <remarks>
    /// Writes the whole line back rather than inserting into it. <c>InsertText</c> puts the
    /// characters in the right place, but while the chat box does not have focus its visible
    /// text node is not rebuilt from the result: the old line stayed on screen and the new one
    /// was drawn after it, so a message read as duplicated until the box was clicked into.
    /// Replacing the full text takes the path that does refresh the node, and what appears is
    /// then what is actually there.
    /// <para>
    /// The existing text is carried across as raw bytes, never as a managed string. Anything
    /// already typed can contain the game's own encoded payloads, such as an auto-translate
    /// phrase, and those do not survive a round trip through text.
    /// </para>
    /// </remarks>
    internal static bool Append(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        try
        {
            if (DalamudServices.Framework.IsFrameworkUnloading)
            {
                return false;
            }

            var handle = DalamudServices.GameGui.GetAddonByName(ChatLogAddon);
            var addon = (AddonChatLog*)handle.Address;

            // Every step is checked before it is followed. The chat log is absent on a loading
            // screen and half-built during a zone change (FH-09).
            if (addon is null || addon->TextInput is null)
            {
                return false;
            }

            var input = addon->TextInput;
            // The raw string, not the evaluated one. What the player typed can hold the game's
            // own encoded payloads, and only the raw form still has them.
            var existing = input->AtkComponentInputBase.RawString.AsSpan();

            var addition = System.Text.Encoding.UTF8.GetBytes(
                NeedsSpace(existing) ? " " + text : text);

            var combined = new byte[existing.Length + addition.Length];
            existing.CopyTo(combined);
            addition.CopyTo(combined.AsSpan(existing.Length));

            // The span overload: the client structs layer pins for the duration of the call and
            // the game copies the bytes into its own string, so nothing of ours is left behind
            // for it to dereference on a later frame (FH-10).
            input->SetText(combined.AsSpan());

            DalamudServices.Log.Debug(
                "ChatInput: chat line is now {Length} bytes", combined.Length);

            return true;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "ChatInput: could not write to the chat box");
            return false;
        }
    }

    /// <summary>
    /// True when a separating space is wanted: there is text already and it does not end in one.
    /// </summary>
    private static bool NeedsSpace(ReadOnlySpan<byte> existing) =>
        existing.Length > 0 && existing[^1] != (byte)' ';
}
