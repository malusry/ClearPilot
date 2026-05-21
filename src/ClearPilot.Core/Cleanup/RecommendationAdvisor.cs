using ClearPilot.Core.Analysis;
using ClearPilot.Core.Rules;

namespace ClearPilot.Core.Cleanup;

public static class RecommendationAdvisor
{
    public static TargetAdvice ForRule(CleanupRule rule)
    {
        var advice = rule.RuleId switch
        {
            "cp.s0.user-temp" => new TargetAdvice(
                RecommendationLevel.Recommended,
                "advice.user-temp",
                "Rebuildable user temporary files in the current user temp root.",
                "Some apps may recreate temporary files the next time they run.",
                "Safe to include in Quick Safe Clean.",
                "Does not remove user documents, game installs, saves, or browser identity data."),

            "cp.s1.windows-temp" => new TargetAdvice(
                RecommendationLevel.Recommended,
                "advice.windows-temp",
                "Windows temporary files in accessible non-admin scope are generally disposable.",
                "Windows or installers may recreate temporary files later.",
                "Include when you want to reclaim space from temporary leftovers.",
                "Access-denied or locked paths are skipped; no elevation is used."),

            "cp.s1.user-crash-dumps" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.user-crash-dumps",
                "Crash dumps can consume space but may still be useful for troubleshooting.",
                "You may lose diagnostic data for recent application crashes.",
                "Clean only if you do not need crash diagnostics.",
                "ClearPilot skips active, locked, or inaccessible dump files."),

            "cp.s1.windows-inet-cache" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.windows-inet-cache",
                "Temporary internet cache can be rebuilt and may free space.",
                "Apps may need to re-fetch cached web assets.",
                "Use when you want to reclaim cache space without touching identity state.",
                "Identity/session/storage data such as cookies, history, and login data are excluded."),

            "cp.s1.msstore-localcache" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.msstore-localcache",
                "Store app LocalCache folders are often disposable cache data.",
                "Store apps may rebuild cache and may start slower on first launch.",
                "Use when you need space and can tolerate cache rebuild behavior.",
                "Durable app state paths like LocalState, RoamingState, and Settings are excluded."),

            "cp.s1.steam-httpcache" => new TargetAdvice(
                RecommendationLevel.Recommended,
                "advice.steam-httpcache",
                "Steam HTTP cache is launcher UI/web cache that can be rebuilt.",
                "Steam may reload UI assets the next time it starts.",
                "Safe to include in Recommended Cleanup when Steam is closed.",
                "Installed games, library metadata, manifests, and saves are excluded."),

            "cp.s1.steam-logs" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.steam-logs",
                "Steam logs are useful for diagnostics but usually safe to remove.",
                "You may lose troubleshooting history for recent Steam issues.",
                "Clean when you do not need old Steam diagnostics.",
                "No game content or save data is removed."),

            "cp.s1.steam-dumps" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.steam-dumps",
                "Steam dump files can reclaim space if crash diagnostics are no longer needed.",
                "You may lose crash evidence needed for troubleshooting.",
                "Clean only if you do not need Steam crash diagnostics.",
                "ClearPilot skips locked or inaccessible files and never deletes installed games."),

            "cp.s1.epic-webcache" => new TargetAdvice(
                RecommendationLevel.Recommended,
                "advice.epic-webcache",
                "Epic Launcher web cache is rebuildable UI cache.",
                "Launcher UI assets may reload on next start.",
                "Safe to include when the launcher is not running.",
                "Manifest, install metadata, and identity/session state remain excluded."),

            "cp.s1.epic-logs" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.epic-logs",
                "Epic logs can reclaim small space but may help troubleshooting.",
                "You may lose launcher diagnostic history.",
                "Clean only if you do not need recent Epic diagnostics.",
                "No installed games, manifests, or account state are removed."),

            "cp.s1.battlenet-cache" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.battlenet-cache",
                "Launcher cache can be rebuilt and may free space.",
                "Launcher content may reload and startup may be slower once.",
                "Use when reclaiming space and Battle.net is not running.",
                "Battle.net Data and game/library state are blocked."),

            "cp.s1.battlenet-logs" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.battlenet-logs",
                "Battle.net logs are often disposable after troubleshooting.",
                "You may lose launcher troubleshooting history.",
                "Clean only when diagnostics are not needed.",
                "Installed games and identity/session state are not removed."),

            "cp.s1.riot-client-cache" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.riot-cache",
                "Riot launcher cache is generally rebuildable.",
                "Launcher UI/cache data may rebuild on next start.",
                "Use when Riot Client is closed and you need space.",
                "Game installs, saves, config, and account/session state are excluded."),

            "cp.s1.riot-client-logs" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.riot-logs",
                "Riot launcher logs can be removed when diagnostics are not needed.",
                "You may lose troubleshooting history.",
                "Clean only if recent Riot diagnostics are not needed.",
                "No game install/config/save data is removed."),

            "cp.s1.ea-app-cache" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.ea-cache",
                "EA App launcher cache is typically rebuildable.",
                "Launcher content may reload and first launch can be slower.",
                "Use when EA App is closed and you want to reclaim space.",
                "Installed games, manifests, and account/session state remain excluded."),

            "cp.s1.ea-app-logs" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.ea-logs",
                "EA App logs are usually safe to remove after troubleshooting.",
                "You may lose launcher diagnostic history.",
                "Clean only if you no longer need diagnostics.",
                "No game install/config/save data is removed."),

            "cp.s1.ubisoft-connect-cache" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.ubisoft-cache",
                "Ubisoft Connect cache directories are generally rebuildable.",
                "Launcher assets may reload after cleanup.",
                "Use when Ubisoft Connect is closed and space recovery is needed.",
                "Installed games, savegames, and account/session data are excluded."),

            "cp.s1.ubisoft-connect-logs" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.ubisoft-logs",
                "Ubisoft Connect logs are typically disposable after troubleshooting.",
                "You may lose diagnostic history for recent launcher issues.",
                "Clean only when diagnostics are not needed.",
                "No game install or save data is removed."),

            "cp.s1.electron-app-logs" or
            "cp.s1.vscode-logs" or
            "cp.s1.jetbrains-logs" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.app-profile-logs",
                "Old app logs can be removed after the app is closed; account, session, settings, and workspace data are excluded.",
                "Historical troubleshooting logs may no longer be available.",
                "Clean only after review and explicit confirmation.",
                "Process guard, denylist checks, and deletion-time revalidation remain enforced."),

            "cp.s1.electron-app-crash-reports" or
            "cp.s1.electron-app-crash-completed" or
            "cp.s1.vscode-crash-reports" or
            "cp.s1.vscode-crash-completed" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.app-profile-crash-diagnostics",
                "Old completed crash diagnostics can be removed after the app is closed.",
                "Historical crash investigation data may no longer be available.",
                "Clean only after review and explicit confirmation.",
                "Only allowlisted files in completed diagnostic subpaths are eligible; process guard and safety gates still apply."),

            "cp.s1.nuget-http-cache" or
            "cp.s1.nuget-global-packages" or
            "cp.s1.npm-cache" or
            "cp.s1.pnpm-store" or
            "cp.s1.yarn-cache" or
            "cp.s1.pip-cache" or
            "cp.s1.cargo-registry-cache" or
            "cp.s1.cargo-git-cache" or
            "cp.s1.gradle-dependency-cache" or
            "cp.s1.maven-repository-cache" or
            "cp.s1.deno-cache" or
            "cp.s1.bun-install-cache" or
            "cp.s1.composer-cache" or
            "cp.s1.go-cache" => new TargetAdvice(
                RecommendationLevel.Optional,
                "advice.package-manager-cache",
                "User-level package manager cache data can be rebuilt and is safe to clean with confirmation.",
                "Future installs or builds may need package redownloads, cache rebuilds, and network access; offline workflows can be affected.",
                "Clean when you need space and can tolerate slower first restore/build operations.",
                "Project-local dependency folders and build outputs are not part of these rules."),

            _ => DefaultForRisk(rule.RiskLevel)
        };

        return NormalizeByRiskGate(rule.RiskLevel, advice);
    }

    public static TargetAdvice ForDeepSpaceItem(DeepSpaceItem item)
    {
        if (item.TargetId is "cp.s2.steam-shadercache" or "cp.s2.steam-depotcache")
        {
            return new TargetAdvice(
                RecommendationLevel.NotRecommended,
                item.TargetId,
                item.Explanation,
                "Cleaning may trigger shader recompilation or disrupt resumable downloads.",
                item.SuggestedAction,
                "Analysis only. ClearPilot will not delete this target.");
        }

        if (item.Type == DeepSpaceItemType.SystemManagedWindowsArea)
        {
            return new TargetAdvice(
                RecommendationLevel.ReviewOnly,
                item.TargetId,
                item.Explanation,
                "Manual deletion can disrupt system-managed maintenance state.",
                item.SuggestedAction,
                "Analysis only. Use Windows Settings, Storage Sense, or Disk Cleanup.");
        }

        return new TargetAdvice(
            RecommendationLevel.ReviewOnly,
            item.TargetId,
            item.Explanation,
            "Impact depends on the owning app or workflow.",
            item.SuggestedAction,
            "Analysis only. ClearPilot will not delete this item.");
    }

    private static TargetAdvice DefaultForRisk(RiskLevel riskLevel)
    {
        return riskLevel switch
        {
            RiskLevel.S0VeryLowRisk => new TargetAdvice(
                RecommendationLevel.Recommended,
                "risk.s0",
                "Very-low-risk cleanup target.",
                "Target data may be recreated by apps later.",
                "Safe to clean.",
                "Risk gates and path safety checks still apply."),

            RiskLevel.S1LowRisk => new TargetAdvice(
                RecommendationLevel.Optional,
                "risk.s1",
                "Low-risk cleanup target requiring explicit confirmation.",
                "Some apps may rebuild cache or lose non-essential diagnostics.",
                "Clean only after review and explicit confirmation.",
                "Protected roots, denylist checks, and revalidation still apply."),

            RiskLevel.S2ReviewRequired => new TargetAdvice(
                RecommendationLevel.ReviewOnly,
                "risk.s2",
                "Review-only target.",
                "Deletion may have unclear impact.",
                "Review manually; no cleanup action is performed.",
                "ClearPilot does not delete S2 targets."),

            RiskLevel.S3DoNotCleanAutomatically => new TargetAdvice(
                RecommendationLevel.NotRecommended,
                "risk.s3",
                "Target is too risky for automated cleanup.",
                "Deletion can cause instability or data loss.",
                "Do not clean automatically.",
                "ClearPilot does not delete S3 targets."),

            RiskLevel.Blocked => new TargetAdvice(
                RecommendationLevel.Blocked,
                "risk.blocked",
                "Target is explicitly blocked by safety policy.",
                "Deletion can affect protected or sensitive data.",
                "No cleanup action is allowed.",
                "Blocked targets are refused in all cleanup modes."),

            _ => new TargetAdvice(
                RecommendationLevel.ReviewOnly,
                "risk.unknown",
                "Unknown target classification.",
                "Impact is uncertain.",
                "Review manually.",
                "ClearPilot will not bypass safety gates.")
        };
    }

    private static TargetAdvice NormalizeByRiskGate(RiskLevel riskLevel, TargetAdvice advice)
    {
        return riskLevel switch
        {
            RiskLevel.S0VeryLowRisk => advice with
            {
                Recommendation = advice.Recommendation is RecommendationLevel.ReviewOnly or RecommendationLevel.Blocked
                    ? RecommendationLevel.Recommended
                    : advice.Recommendation
            },
            RiskLevel.S1LowRisk => advice with
            {
                Recommendation = advice.Recommendation is RecommendationLevel.Recommended or RecommendationLevel.Optional
                    ? advice.Recommendation
                    : RecommendationLevel.Optional
            },
            RiskLevel.S2ReviewRequired => advice with
            {
                Recommendation = advice.Recommendation is RecommendationLevel.ReviewOnly or RecommendationLevel.NotRecommended
                    ? advice.Recommendation
                    : RecommendationLevel.ReviewOnly
            },
            RiskLevel.S3DoNotCleanAutomatically => advice with { Recommendation = RecommendationLevel.NotRecommended },
            RiskLevel.Blocked => advice with { Recommendation = RecommendationLevel.Blocked },
            _ => advice
        };
    }
}
