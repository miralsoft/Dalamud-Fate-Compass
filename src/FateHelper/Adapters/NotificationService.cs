using Dalamud.Interface.ImGuiNotification;
using FateHelper.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace FateHelper.Adapters;

/// <summary>
/// Announces a newly spotted FATE. Display and sound only, nothing is sent anywhere.
/// </summary>
internal static unsafe class NotificationService
{
    /// <summary>
    /// One of the game's own alert sounds, so nothing has to be shipped and no audio licence
    /// question arises.
    /// </summary>
    private const uint DefaultSoundEffect = 6;

    /// <param name="title">Heading of the pop-up.</param>
    /// <param name="message">Its body.</param>
    /// <param name="withSound">Whether to play the game's alert sound as well.</param>
    /// <param name="withPopup">
    /// Whether to show the pop-up at all. The sound is independent of it: a chime says "look at
    /// the list" without covering a corner of the screen, and that turns out to be what most of
    /// the value was.
    /// </param>
    internal static void Announce(string title, string message, bool withSound, bool withPopup)
    {
        try
        {
            if (withPopup)
            {
                DalamudServices.Notifications.AddNotification(new Notification
                {
                    Title = title,
                    Content = message,
                    Type = NotificationType.Info,
                    InitialDuration = TimeSpan.FromSeconds(5),
                });
            }

            if (withSound)
            {
                PlaySound();
            }
        }
        catch (Exception ex)
        {
            // A failed notification is never worth interrupting the player for.
            DalamudServices.Log.Warning(ex, "NotificationService: could not announce");
        }
    }

    private static void PlaySound()
    {
        try
        {
            UIGlobals.PlayChatSoundEffect(DefaultSoundEffect);
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "NotificationService: could not play the sound");
        }
    }
}
