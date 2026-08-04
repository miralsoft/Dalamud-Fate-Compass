using FateCompass.Core.Engage;
using FateCompass.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FateCompass.Adapters;

/// <summary>
/// The single gate to the game server (FH-01).
/// </summary>
/// <remarks>
/// Nothing else in this plugin may send an action. Every method logs what it sent and what
/// triggered it, so the automation boundary can be reviewed by reading this one file (FH-06).
/// A step that cannot run is skipped with a log line rather than retried (FH-07).
/// </remarks>
internal sealed unsafe class GameActions : IGameActions
{
    /// <summary>
    /// Minimum gap between two sends of the same step.
    /// </summary>
    /// <remarks>
    /// The retry loop checks the player state several times a second, which is right, but
    /// sending on every one of those checks is not: a stance that is briefly unavailable after
    /// a level sync produced seventeen requests in three seconds. Re-checking often and
    /// re-sending rarely is the combination that behaves (FH-07).
    /// </remarks>
    private static readonly TimeSpan MinimumResendGap = TimeSpan.FromSeconds(1);

    private readonly Dictionary<EngageStep, DateTime> lastSentAt = [];

    public bool Execute(EngageStep step, ActionTrigger trigger)
    {
        var now = DateTime.UtcNow;
        if (lastSentAt.TryGetValue(step, out var previous) && now - previous < MinimumResendGap)
        {
            return false;
        }

        lastSentAt[step] = now;

        try
        {
            var sent = step switch
            {
                EngageStep.Dismount => Dismount(),
                EngageStep.Mount => Mount(),
                EngageStep.LevelSync => LevelSync(),
                EngageStep.TankStance => TankStance(),
                _ => false,
            };

            DalamudServices.Log.Information(
                "GameActions: {Step} trigger={Trigger} sent={Sent}", step, trigger, sent);

            return sent;
        }
        catch (Exception ex)
        {
            // Never let a failed action escape into the game process (S-09).
            DalamudServices.Log.Error(ex, "GameActions: {Step} failed, trigger={Trigger}", step, trigger);
            return false;
        }
    }

    public bool Teleport(uint aetheryteId)
    {
        try
        {
            var telepo = Telepo.Instance();
            if (telepo is null)
            {
                return false;
            }

            var sent = telepo->Teleport(aetheryteId, 0);
            DalamudServices.Log.Information(
                "GameActions: Teleport aetheryte={Aetheryte} trigger={Trigger} sent={Sent}",
                aetheryteId, ActionTrigger.Manual, sent);

            return sent;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "GameActions: teleport to {Aetheryte} failed", aetheryteId);
            return false;
        }
    }

    /// <summary>
    /// How long a return we sent stays answerable.
    /// </summary>
    /// <remarks>
    /// Long enough for the prompt to appear, short enough that it cannot still be open when the
    /// player next opens some unrelated window. The prompt follows the cast within a frame or
    /// two, so this is generous already.
    /// </remarks>
    private static readonly TimeSpan ReturnPromptWindow = TimeSpan.FromSeconds(5);

    /// <summary>When a return we sent is still waiting for its prompt to be answered.</summary>
    private DateTime? returnAwaitingPrompt;

    public bool Return()
    {
        try
        {
            var sent = UseGeneralAction(GeneralActionReturn);

            // Only a return that was actually accepted opens a prompt worth answering. Arming
            // this on a refused cast would leave the plugin waiting to press yes on whatever
            // window happened to come next.
            returnAwaitingPrompt = sent ? DateTime.UtcNow : null;

            DalamudServices.Log.Information(
                "GameActions: Return trigger={Trigger} sent={Sent}", ActionTrigger.Manual, sent);

            return sent;
        }
        catch (Exception ex)
        {
            returnAwaitingPrompt = null;
            DalamudServices.Log.Error(ex, "GameActions: Return failed");
            return false;
        }
    }

    public bool ConfirmPendingReturn()
    {
        // Cleared before anything else can go wrong, so this can only ever fire once per return.
        var requestedAt = returnAwaitingPrompt;
        returnAwaitingPrompt = null;

        if (requestedAt is null || DateTime.UtcNow - requestedAt.Value > ReturnPromptWindow)
        {
            return false;
        }

        try
        {
            var addon = (AtkUnitBase*)DalamudServices.GameGui.GetAddonByName(SelectYesNoAddon).Address;
            if (addon is null || !addon->IsVisible)
            {
                return false;
            }

            // 0 is yes on this prompt, 1 is no.
            addon->FireCallbackInt(ConfirmYes);

            DalamudServices.Log.Information(
                "GameActions: confirmed the return prompt, {Age}ms after the cast",
                (int)(DateTime.UtcNow - requestedAt.Value).TotalMilliseconds);

            return true;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "GameActions: confirming the return prompt failed");
            return false;
        }
    }

    /// <summary>True when the player is currently level synced to the FATE they are in.</summary>
    internal static bool IsSyncedToCurrentFate()
    {
        var manager = FateManager.Instance();
        if (manager is null)
        {
            return false;
        }

        var fateId = manager->GetCurrentFateId();
        if (fateId == 0)
        {
            return false;
        }

        var fate = manager->GetFateById(fateId);
        return fate is not null && manager->IsSyncedToFate(fate);
    }

    /// <summary>The FATE the player is currently taking part in, or zero.</summary>
    internal static ushort CurrentFateId()
    {
        var manager = FateManager.Instance();
        return manager is null ? (ushort)0 : manager->GetCurrentFateId();
    }

    private static bool Dismount() => UseGeneralAction(GeneralActionDismount);

    private static bool Mount() => UseGeneralAction(GeneralActionMountRoulette);

    /// <summary>
    /// Level sync inside a FATE has no direct function in the client structs: every member
    /// named after level sync belongs to duty content instead. The text command is therefore
    /// the mechanism, which is the same path a player uses by hand or in a macro. The command
    /// itself comes from the game data (see <see cref="TextCommandResolver"/>), and the result
    /// is verified through <see cref="FateManager.IsSyncedToFate"/> rather than assumed.
    /// </summary>
    private static bool LevelSync()
    {
        if (IsSyncedToCurrentFate())
        {
            DalamudServices.Log.Debug("GameActions: already synced, nothing sent");
            return false;
        }

        // The result is deliberately not checked here. The sync does not apply within the same
        // frame, so reading it back immediately always reported failure even when it worked,
        // which made the log actively misleading. The retry loop re-plans a second later and
        // the planner skips the step once the sync is actually in place, so a genuine failure
        // still corrects itself.
        return ExecuteTextCommand(TextCommandResolver.LevelSyncOn());
    }

    private static bool TankStance()
    {
        var stanceActionId = TankStanceResolver.CurrentStanceActionId();
        if (stanceActionId is null)
        {
            DalamudServices.Log.Debug("GameActions: no tank stance for the current job, nothing sent");
            return false;
        }

        return UseAction(ActionType.Action, stanceActionId.Value);
    }

    /// <summary>
    /// Runs a text command exactly as typing it would. This is the same path a macro takes.
    /// </summary>
    private static bool ExecuteTextCommand(string command)
    {
        var shell = RaptureShellModule.Instance();
        var ui = UIModule.Instance();
        if (shell is null || ui is null)
        {
            return false;
        }

        if (shell->IsTextCommandUnavailable)
        {
            DalamudServices.Log.Debug("GameActions: text commands unavailable right now");
            return false;
        }

        // A failed allocation here would otherwise be dereferenced twice: once by the game and
        // once by the cleanup below.
        var payload = Utf8String.FromString(command);
        if (payload is null)
        {
            DalamudServices.Log.Warning("GameActions: could not allocate the command string");
            return false;
        }

        try
        {
            shell->ExecuteCommandInner(payload, ui);
            return true;
        }
        finally
        {
            payload->Dtor(true);
        }
    }

    /// <summary>
    /// Sends an action.
    /// </summary>
    /// <remarks>
    /// This used to refuse to send whenever <c>GetActionStatus</c> returned non-zero, which was
    /// wrong: for general actions such as dismount, and for stances, that call reports non-zero
    /// in situations where the action is perfectly usable. The result was that level sync
    /// worked (it goes through a text command) while dismount and stance silently did nothing.
    /// <para>
    /// The status is now logged and the action is sent regardless. The game refuses what it
    /// does not want, which is harmless, and the log still shows what the client thought.
    /// </para>
    /// </remarks>
    private static bool UseAction(ActionType type, uint actionId)
    {
        var manager = ActionManager.Instance();
        if (manager is null)
        {
            return false;
        }

        var status = manager->GetActionStatus(type, actionId, 0xE000_0000, false, false, null);
        var sent = manager->UseAction(type, actionId, 0xE000_0000, 0, ActionManager.UseActionMode.None, 0, null);

        DalamudServices.Log.Debug(
            "GameActions: action type={Type} id={Action} statusBeforeSend={Status} accepted={Sent}",
            type, actionId, status, sent);

        return sent;
    }

    private static bool UseGeneralAction(uint generalActionId) =>
        UseAction(ActionType.GeneralAction, generalActionId);

    /// <summary>
    /// General action ids, read out of the game's own <c>GeneralAction</c> sheet rather than
    /// remembered: 9 is Mount Roulette, 23 is Dismount, 8 is Return, and 7 beside it is Teleport.
    /// They are still the first values to confirm if one of these steps ever stops working.
    /// </summary>
    private const uint GeneralActionReturn = 8;

    /// <summary>The game's generic yes/no prompt.</summary>
    private const string SelectYesNoAddon = "SelectYesno";

    /// <summary>Callback value for yes on that prompt.</summary>
    private const int ConfirmYes = 0;

    private const uint GeneralActionMountRoulette = 9;

    private const uint GeneralActionDismount = 23;
}
