using FateCompass.Core.Model;
using FateCompass.Core.Routing;

namespace FateCompass.Core.Tests;

/// <summary>
/// Builders with sensible defaults, so each test only states the one thing it is about.
/// </summary>
internal static class TestData
{
    public static FateSnapshot Fate(
        uint id = 1,
        ushort level = 50,
        FateKind kind = FateKind.Slay,
        FateProgressState state = FateProgressState.Running,
        int progressPercent = 0,
        int secondsRemaining = 600,
        float x = 0f,
        float z = 0f,
        uint? definitionId = null,
        ActivitySource source = ActivitySource.Fate,
        bool isRegistrationOpen = false,
        bool hasRegistrationGate = false,
        int? secondsUntilRegistrationCloses = null,
        int? participants = null,
        int? maxParticipants = null,
        bool hasStarted = true) => new()
        {
            Id = id,
            DefinitionId = definitionId ?? id,
            Name = $"FATE {id}",
            Level = level,
            Kind = kind,
            State = state,
            Position = new WorldPosition(x, 0f, z),
            ProgressPercent = progressPercent,
            SecondsRemaining = secondsRemaining,
            Source = source,
            IsRegistrationOpen = isRegistrationOpen,
            HasRegistrationGate = hasRegistrationGate,
            SecondsUntilRegistrationCloses = secondsUntilRegistrationCloses,
            Participants = participants,
            MaxParticipants = maxParticipants,

            // A FATE still in preparation has no clock running yet, which is the whole point of
            // the distinction. Defaulting it the other way would let tests describe states the
            // game never produces.
            HasStarted = hasStarted && state != FateProgressState.Preparation,
        };

    public static PlayerSnapshot Player(
        float x = 0f,
        float z = 0f,
        ushort level = 90,
        bool isMounted = false,
        bool isInCombat = false,
        TankJob? tankJob = null,
        bool isTankStanceActive = false,
        bool isLevelSyncAvailable = false,
        bool isLevelSynced = false,
        bool isOccupied = false,
        bool canUseMount = true,
        ContentKind content = ContentKind.Overworld) => new()
        {
            Position = new WorldPosition(x, 0f, z),
            Level = level,
            Content = content,
            IsMounted = isMounted,
            IsInCombat = isInCombat,
            TankJob = tankJob,
            IsTankStanceActive = isTankStanceActive,
            IsLevelSyncAvailable = isLevelSyncAvailable,
            IsLevelSynced = isLevelSynced,
            IsOccupied = isOccupied,
            CanUseMount = canUseMount,
        };

    public static Aetheryte Aetheryte(uint id = 1, string? name = null, float x = 0f, float z = 0f) => new()
    {
        Id = id,
        Name = name ?? $"Aetheryte {id}",
        Position = new WorldPosition(x, 0f, z),
    };
}
