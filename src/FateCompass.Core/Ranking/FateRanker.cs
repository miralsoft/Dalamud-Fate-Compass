using FateCompass.Core.Configuration;
using FateCompass.Core.Model;

namespace FateCompass.Core.Ranking;

/// <summary>
/// Orders the FATEs of a zone into a recommendation list.
/// </summary>
/// <remarks>
/// Distance alone is a poor recommendation. A FATE that will expire before the player arrives
/// is worthless no matter how close it is, and one sitting at ninety percent pays almost
/// nothing. This ranker therefore scores three factors and excludes the cases where travelling
/// is pointless, with the reason preserved so the UI can explain itself.
/// </remarks>
public static class FateRanker
{
    /// <summary>
    /// Evaluates every FATE and returns them ordered: recommended ones first by descending
    /// score, then the excluded ones grouped by reason. The returned list always contains every
    /// input FATE, so the caller can render a complete picture.
    /// </summary>
    public static IReadOnlyList<RankedFate> Rank(
        IEnumerable<FateSnapshot> fates,
        PlayerSnapshot player,
        FateCompassSettings settings)
    {
        ArgumentNullException.ThrowIfNull(fates);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(settings);

        var evaluated = fates.Select(fate => Evaluate(fate, player, settings)).ToList();

        // Standing objectives are worth showing but do not belong in the numbered order. A raid
        // that runs for an hour is not the second stop on a farming route, and giving it a
        // position in that route would say it is.
        var recommended = evaluated
            .Where(candidate =>
                candidate.ExclusionReason == FateExclusionReason.None
                && !candidate.IsSpecialObjective)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.DistanceYalms)
            .ThenBy(candidate => candidate.Fate.Id)
            .Select((candidate, index) => candidate with { Rank = index + 1 });

        var special = evaluated
            .Where(candidate =>
                candidate.ExclusionReason == FateExclusionReason.None
                && candidate.IsSpecialObjective)
            .OrderBy(candidate => candidate.DistanceYalms)
            .ThenBy(candidate => candidate.Fate.Id);

        var excluded = evaluated
            .Where(candidate => candidate.ExclusionReason != FateExclusionReason.None)
            .OrderBy(candidate => candidate.ExclusionReason)
            .ThenBy(candidate => candidate.DistanceYalms)
            .ThenBy(candidate => candidate.Fate.Id);

        return [.. recommended, .. special, .. excluded];
    }

    private static RankedFate Evaluate(
        FateSnapshot fate,
        PlayerSnapshot player,
        FateCompassSettings settings)
    {
        var distance = player.Position.TravelDistanceTo(
            fate.Position, settings.VerticalTravelWeight);
        var speed = settings.SpeedFor(player);
        var travelSeconds = EstimateTravelSeconds(distance, speed);

        var reason = DetermineExclusion(fate, settings, travelSeconds);
        if (reason != FateExclusionReason.None)
        {
            return new RankedFate
            {
                Fate = fate,
                Rank = 0,
                Score = 0f,
                DistanceYalms = distance,
                EstimatedTravelSeconds = travelSeconds,
                ExclusionReason = reason,
            };
        }

        return new RankedFate
        {
            Fate = fate,
            Rank = 0,
            Score = Score(fate, distance, speed, settings.Weights),
            DistanceYalms = distance,
            EstimatedTravelSeconds = travelSeconds,
            ExclusionReason = FateExclusionReason.None,
        };
    }

    private static FateExclusionReason DetermineExclusion(
        FateSnapshot fate,
        FateCompassSettings settings,
        float travelSeconds)
    {
        // A closed registration window is checked before anything else, because it is the one
        // exclusion the player cannot do anything about. The encounter carries on and keeps
        // reporting time left, which is exactly what makes it look joinable when it is not.
        if (fate.HasRegistrationGate && !fate.IsRegistrationOpen)
        {
            return FateExclusionReason.RegistrationClosed;
        }

        if (!fate.IsJoinable)
        {
            return FateExclusionReason.NotJoinable;
        }

        if (!FateFilter.IsIncluded(fate, settings))
        {
            return FateExclusionReason.Filtered;
        }

        if (fate.IsNearlyDone(settings.NearlyDoneThresholdPercent))
        {
            return FateExclusionReason.NearlyComplete;
        }

        // While the sign-up window is open, that cut-off is the only deadline worth measuring
        // against: arriving after it means arriving too late, however much of the fight is left.
        if (fate.IsRegistrationOpen && fate.SecondsUntilRegistrationCloses is { } closing)
        {
            return !float.IsPositiveInfinity(travelSeconds) && travelSeconds > closing
                ? FateExclusionReason.RegistrationTooLate
                : FateExclusionReason.None;
        }

        // A FATE that has not started yet has its whole duration ahead of it, so neither the
        // expiry nor the reachability check applies. Judging it on a clock that has not begun
        // is what made freshly spawned FATEs look hopeless.
        if (!fate.HasStarted || fate.State == FateProgressState.Preparation)
        {
            return FateExclusionReason.None;
        }

        if (fate.SecondsRemaining < settings.MinimumSecondsRemaining)
        {
            return FateExclusionReason.ExpiringSoon;
        }

        // Only meaningful when a travel estimate was possible at all.
        if (!float.IsPositiveInfinity(travelSeconds) && travelSeconds > fate.SecondsRemaining)
        {
            return FateExclusionReason.Unreachable;
        }

        return FateExclusionReason.None;
    }

    /// <summary>
    /// How comfortable the remaining time is, from 0 when it is hopeless to 1 when there is
    /// plenty.
    /// </summary>
    private static float TimeScore(FateSnapshot fate, float travelSeconds, RankingWeights weights)
    {
        var travelOrZero = float.IsPositiveInfinity(travelSeconds) ? 0f : MathF.Max(travelSeconds, 0f);

        // An open sign-up window is judged on whether the door can be reached in time, not on
        // how much of the fight would be left afterwards. Those are different questions, and
        // measuring a three-minute window against a three-minute comfort buffer would mark
        // every reachable encounter as barely worth going to.
        if (fate.IsRegistrationOpen && fate.SecondsUntilRegistrationCloses is { } closing)
        {
            var margin = MathF.Max(weights.ComfortableRegistrationMarginSeconds, 1f);
            return Math.Clamp((closing - travelOrZero) / margin, 0f, 1f);
        }

        // A FATE that has not started has its whole duration ahead of it.
        if (!fate.HasStarted)
        {
            return 1f;
        }

        var travel = travelOrZero;
        var buffer = MathF.Max(weights.ComfortableParticipationSeconds, 1f);

        // Time left after arriving, measured against how long is worth having on arrival.
        var spare = fate.SecondsRemaining - travel;
        return Math.Clamp(spare / buffer, 0f, 1f);
    }

    private static float Score(
        FateSnapshot fate, float distance, float speedYalmsPerSecond, RankingWeights weights)
    {
        // Half-life curve: 1 at the player's feet, 0.5 at the configured distance, never negative.
        var halfLife = MathF.Max(weights.DistanceHalfLifeYalms, 1f);
        var distanceScore = halfLife / (halfLife + MathF.Max(distance, 0f));


        var progressScore = 1f - (Math.Clamp(fate.ProgressPercent, 0, 100) / 100f);

        var travelSeconds = EstimateTravelSeconds(distance, speedYalmsPerSecond);
        var timeScore = TimeScore(fate, travelSeconds, weights);

        var score = (distanceScore * weights.Distance)
            + (timeScore * weights.TimeRemaining)
            + (progressScore * weights.Progress);

        // Only applies where the game reports participation. An activity that does not report
        // it must not be penalised against ones that do, so it simply contributes nothing.
        if (fate.Occupancy is { } occupancy)
        {
            score += (1f - occupancy) * weights.Occupancy;
        }

        // An open registration window is the one thing here that can be permanently missed.
        if (fate.IsRegistrationOpen)
        {
            score += weights.RegistrationOpenBonus;
        }

        // A critical encounter that is still reachable outranks the ordinary business of the
        // zone. Getting here at all means the sign-up deadline was already checked, so this is
        // only ever added to something the player can actually make.
        if (fate.Kind is FateKind.CriticalEncounter or FateKind.CriticalEngagement)
        {
            score += weights.CriticalPriorityBonus;
        }

        return score;
    }

    /// <summary>
    /// Travel time at the assumed speed. A non-positive speed means no estimate is possible,
    /// which is reported as infinity so the reachability check can skip rather than exclude
    /// every FATE on a misconfiguration.
    /// </summary>
    private static float EstimateTravelSeconds(float distance, float speedYalmsPerSecond)
        => speedYalmsPerSecond <= 0f ? float.PositiveInfinity : distance / speedYalmsPerSecond;
}
