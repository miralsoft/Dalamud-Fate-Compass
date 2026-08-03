using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace FateHelper.Services;

/// <summary>
/// The Dalamud services this plugin uses, injected once by the framework.
/// </summary>
/// <remarks>
/// Collected in one place rather than scattered across constructors, so the plugin's total
/// dependency on the framework is visible at a glance.
/// <para>
/// Note for anyone porting older code: <c>IClientState.LocalPlayer</c> was removed in API 15.
/// The local player comes from <see cref="ObjectTable"/> now.
/// </para>
/// </remarks>
internal sealed class DalamudServices
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService] internal static IClientState ClientState { get; private set; } = null!;

    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;

    [PluginService] internal static IFateTable FateTable { get; private set; } = null!;

    [PluginService] internal static ICondition Condition { get; private set; } = null!;

    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;

    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    [PluginService] internal static INotificationManager Notifications { get; private set; } = null!;

    /// <summary>The server info bar next to the minimap.</summary>
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;

    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;

    /// <summary>
    /// Runs an action on the game's framework thread.
    /// </summary>
    /// <remarks>
    /// Every call into the game has to happen here. Button handlers run inside the ImGui draw
    /// callback, which is a different thread, and calling game functions from there is a
    /// well-known way to take the client down. Anything a button triggers goes through this.
    /// <para>
    /// Failures are logged rather than thrown, because an exception escaping the framework
    /// thread is itself a crash.
    /// </para>
    /// </remarks>
    internal static void OnGameThread(Action action, string what)
    {
        ArgumentNullException.ThrowIfNull(action);

        Framework.RunOnFrameworkThread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed on the framework thread: {What}", what);
            }
        });
    }

    internal static void Initialise(IDalamudPluginInterface pluginInterface)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        pluginInterface.Create<DalamudServices>();
    }
}
