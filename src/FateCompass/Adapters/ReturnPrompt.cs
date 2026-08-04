using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FateCompass.Configuration;
using FateCompass.Services;

namespace FateCompass.Adapters;

/// <summary>
/// Answers the confirmation the game raises after a Return that this plugin cast.
/// </summary>
/// <remarks>
/// Pressing the travel button in an exploratory zone casts Return, and the game then asks
/// whether you meant it. The answer is already known: the player pressed the button that asks
/// for exactly this. So the prompt is answered for them.
/// <para>
/// This is the only place the plugin operates a game window rather than sending an action, and
/// it is fenced accordingly. It listens for one named window, it only acts while a return this
/// plugin sent is still unanswered, it acts once, and it can be switched off. Every other prompt
/// the player ever sees passes through here untouched, because the latch is closed.
/// </para>
/// <para>
/// It is worth being plain about the trade. Dalamud's published restrictions name dialog boxes,
/// and no plugin in the official repository does this. It is here because the owner decided the
/// distinction that matters is whether the plugin begins something, and this begins nothing: it
/// finishes a request the player made a second earlier. That is a defensible reading and it is
/// not the only one, which is why the switch exists.
/// </para>
/// </remarks>
internal static class ReturnPrompt
{
    private const string SelectYesNoAddon = "SelectYesno";

    private static PluginConfiguration? configuration;
    private static bool listening;

    /// <summary>
    /// Starts listening. Safe to call twice; the second call does nothing.
    /// </summary>
    internal static void Initialise(PluginConfiguration pluginConfiguration)
    {
        if (listening)
        {
            return;
        }

        configuration = pluginConfiguration;

        try
        {
            DalamudServices.AddonLifecycle.RegisterListener(
                AddonEvent.PostSetup, SelectYesNoAddon, OnPromptOpened);

            listening = true;
        }
        catch (Exception ex)
        {
            // A feature that cannot arm is a feature that is off, not a plugin that fails to
            // load (FH-07).
            DalamudServices.Log.Warning(ex, "ReturnPrompt: could not listen for the prompt");
        }
    }

    /// <summary>Stops listening. Called before anything else is released, like every other native hook.</summary>
    internal static void Shutdown()
    {
        if (!listening)
        {
            return;
        }

        listening = false;

        try
        {
            DalamudServices.AddonLifecycle.UnregisterListener(
                AddonEvent.PostSetup, SelectYesNoAddon, OnPromptOpened);
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "ReturnPrompt: could not stop listening");
        }
        finally
        {
            configuration = null;
        }
    }

    /// <summary>
    /// Runs whenever any yes/no prompt opens, and almost always does nothing.
    /// </summary>
    /// <remarks>
    /// The decision about whether this particular prompt is ours belongs to
    /// <see cref="IGameActions.ConfirmPendingReturn"/>, so that everything which reaches the game
    /// stays behind the one gate (FH-01). This method only asks.
    /// <para>
    /// Addon lifecycle callbacks arrive on the framework thread, which is what makes it legal to
    /// touch addon memory from here at all (FH-08).
    /// </para>
    /// </remarks>
    private static void OnPromptOpened(AddonEvent type, AddonArgs args)
    {
        try
        {
            if (configuration?.Settings.ConfirmReturnPrompt != true)
            {
                return;
            }

            Plugin.Actions.ConfirmPendingReturn();
        }
        catch (Exception ex)
        {
            // Never let this escape into the game's own UI setup path (S-09).
            DalamudServices.Log.Error(ex, "ReturnPrompt: handling the prompt failed");
        }
    }
}
