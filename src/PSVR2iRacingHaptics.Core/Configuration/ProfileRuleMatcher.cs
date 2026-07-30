using System.Text.RegularExpressions;
using PSVR2iRacingHaptics.Core.Models;

namespace PSVR2iRacingHaptics.Core.Configuration;

public sealed record ProfileRuleMatch(
    HapticProfile Profile,
    ProfileAssignmentRule Rule,
    int Specificity,
    string Description);

/// <summary>
/// Deterministically selects the highest-priority matching car/track rule.
/// All populated fields are AND conditions; '*' and '?' are supported.
/// </summary>
public static class ProfileRuleMatcher
{
    public static ProfileRuleMatch? Select(
        AppSettings settings,
        TelemetryContext context)
    {
        if (!settings.AutoProfileSelectionEnabled || !context.HasIdentity)
        {
            return null;
        }

        return settings.ProfileRules
            .Where(rule => rule.Enabled && HasAtLeastOneFilter(rule))
            .Select(rule => new
            {
                Rule = rule,
                Profile = ProfileCatalog.FindProfile(settings, rule.ProfileId),
                Matches = Matches(rule, context),
                Specificity = Specificity(rule)
            })
            .Where(candidate => candidate.Profile is not null && candidate.Matches)
            .OrderByDescending(candidate => candidate.Rule.Priority)
            .ThenByDescending(candidate => candidate.Specificity)
            .ThenBy(candidate => candidate.Rule.Name, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => new ProfileRuleMatch(
                candidate.Profile!,
                candidate.Rule,
                candidate.Specificity,
                Describe(candidate.Rule, candidate.Profile!, context)))
            .FirstOrDefault();
    }

    public static bool Matches(
        ProfileAssignmentRule rule,
        TelemetryContext context) =>
        MatchField(rule.CarPathPattern, context.CarPath)
        && MatchField(rule.CarNamePattern, context.CarName)
        && MatchField(rule.CarClassPattern, context.CarClass)
        && MatchField(rule.TrackNamePattern, context.TrackName)
        && MatchField(rule.TrackConfigPattern, context.TrackConfigName);

    public static bool HasAtLeastOneFilter(ProfileAssignmentRule rule) =>
        !string.IsNullOrWhiteSpace(rule.CarPathPattern)
        || !string.IsNullOrWhiteSpace(rule.CarNamePattern)
        || !string.IsNullOrWhiteSpace(rule.CarClassPattern)
        || !string.IsNullOrWhiteSpace(rule.TrackNamePattern)
        || !string.IsNullOrWhiteSpace(rule.TrackConfigPattern);

    private static bool MatchField(string pattern, string value)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var expression = "^"
            + Regex.Escape(Normalize(pattern))
                .Replace(@"\*", ".*", StringComparison.Ordinal)
                .Replace(@"\?", ".", StringComparison.Ordinal)
            + "$";
        return Regex.IsMatch(
            Normalize(value),
            expression,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
    }

    private static int Specificity(ProfileAssignmentRule rule)
    {
        var patterns = new[]
        {
            rule.CarPathPattern,
            rule.CarNamePattern,
            rule.CarClassPattern,
            rule.TrackNamePattern,
            rule.TrackConfigPattern
        };
        return patterns.Sum(pattern =>
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return 0;
            }
            var wildcardPenalty = pattern.Count(character => character is '*' or '?') * 3;
            return 100 + Math.Max(0, pattern.Length - wildcardPenalty);
        });
    }

    private static string Describe(
        ProfileAssignmentRule rule,
        HapticProfile profile,
        TelemetryContext context) =>
        $"Rule '{rule.Name}' selected profile '{profile.Name}' for "
        + $"{context.CarDisplayName} at {context.TrackDisplayLabel}.";

    private static string Normalize(string value) =>
        value.Trim().Replace('\\', '/');
}
