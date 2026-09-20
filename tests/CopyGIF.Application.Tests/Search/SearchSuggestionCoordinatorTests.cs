using CopyGIF.Application.Search;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Search;

[TestClass]
public sealed class SearchSuggestionCoordinatorTests
{
    private static readonly DateTimeOffset ReferenceTime =
        new(
            2026,
            9,
            3,
            12,
            0,
            0,
            TimeSpan.Zero);

    [TestMethod]
    public async Task GetSuggestionsAsync_WhenDisabled_DoesNotLoadHistory()
    {
        FakeSettingsStore settingsStore =
            new()
            {
                Value =
                    CreateSettings(
                        useHistorySuggestions: false)
            };

        FakeSearchHistoryStore historyStore =
            new();

        using SearchSuggestionCoordinator coordinator =
            new(
                settingsStore,
                historyStore,
                new FakeClock());

        IReadOnlyList<string> suggestions =
            await coordinator.GetSuggestionsAsync(
                "cat");

        Assert.IsEmpty(
            suggestions);

        Assert.AreEqual(
            0,
            historyStore.LoadCallCount);
    }

    [TestMethod]
    public async Task GetSuggestionsAsync_PrioritizesPrefixesThenRecency()
    {
        FakeSearchHistoryStore historyStore =
            new()
            {
                Value =
                    new SearchHistorySnapshot
                    {
                        Entries =
                        [
                            CreateEntry(
                                "bobcat",
                                ReferenceTime),

                            CreateEntry(
                                "Cat dance",
                                ReferenceTime.AddMinutes(-2)),

                            CreateEntry(
                                "caterpillar",
                                ReferenceTime.AddMinutes(-1)),

                            CreateEntry(
                                "dogs",
                                ReferenceTime.AddMinutes(1))
                        ]
                    }
            };

        using SearchSuggestionCoordinator coordinator =
            CreateCoordinator(
                historyStore: historyStore);

        IReadOnlyList<string> suggestions =
            await coordinator.GetSuggestionsAsync(
                "  CAT  ");

        CollectionAssert.AreEqual(
            new[]
            {
                "caterpillar",
                "Cat dance",
                "bobcat"
            },
            suggestions.ToArray());
    }

    [TestMethod]
    public async Task GetSuggestionsAsync_UsesMostRecentQueriesForEmptyInput()
    {
        FakeSearchHistoryStore historyStore =
            new()
            {
                Value =
                    new SearchHistorySnapshot
                    {
                        Entries =
                        [
                            CreateEntry(
                                "first",
                                ReferenceTime.AddMinutes(-2)),

                            CreateEntry(
                                "second",
                                ReferenceTime),

                            CreateEntry(
                                "third",
                                ReferenceTime.AddMinutes(-1))
                        ]
                    }
            };

        using SearchSuggestionCoordinator coordinator =
            CreateCoordinator(
                historyStore: historyStore);

        IReadOnlyList<string> suggestions =
            await coordinator.GetSuggestionsAsync(
                string.Empty,
                maximumResults: 2);

        CollectionAssert.AreEqual(
            new[]
            {
                "second",
                "third"
            },
            suggestions.ToArray());
    }

    [TestMethod]
    public async Task RecordSearchAsync_AddsTrimmedQueryWithClockTime()
    {
        FakeSearchHistoryStore historyStore =
            new();

        FakeClock clock =
            new(
                ReferenceTime);

        using SearchSuggestionCoordinator coordinator =
            CreateCoordinator(
                historyStore: historyStore,
                clock: clock);

        await coordinator.RecordSearchAsync(
            "  funny cats  ");

        Assert.HasCount(
            1,
            historyStore.SavedSnapshots);

        SearchHistoryEntry entry =
            historyStore.SavedSnapshots[0]
                .Entries[0];

        Assert.AreEqual(
            "funny cats",
            entry.Query);

        Assert.AreEqual(
            ReferenceTime,
            entry.LastUsedAtUtc);

        Assert.AreEqual(
            1,
            entry.UseCount);
    }

    [TestMethod]
    public async Task RecordSearchAsync_MergesCaseInsensitiveDuplicate()
    {
        FakeSearchHistoryStore historyStore =
            new()
            {
                Value =
                    new SearchHistorySnapshot
                    {
                        Entries =
                        [
                            CreateEntry(
                                "Cats",
                                ReferenceTime.AddDays(-1),
                                useCount: 3),

                            CreateEntry(
                                "dogs",
                                ReferenceTime.AddHours(-1))
                        ]
                    }
            };

        FakeClock clock =
            new(
                ReferenceTime);

        using SearchSuggestionCoordinator coordinator =
            CreateCoordinator(
                historyStore: historyStore,
                clock: clock);

        await coordinator.RecordSearchAsync(
            "CATS");

        SearchHistorySnapshot saved =
            historyStore.SavedSnapshots[0];

        Assert.HasCount(
            2,
            saved.Entries);

        SearchHistoryEntry updated =
            saved.Entries[0];

        Assert.AreEqual(
            "CATS",
            updated.Query);

        Assert.AreEqual(
            4,
            updated.UseCount);

        Assert.AreEqual(
            ReferenceTime,
            updated.LastUsedAtUtc);
    }

    [TestMethod]
    public async Task RecordSearchAsync_RespectsConfiguredHistoryLimit()
    {
        FakeSettingsStore settingsStore =
            new()
            {
                Value =
                    CreateSettings(
                        searchHistoryLimit: 2)
            };

        FakeSearchHistoryStore historyStore =
            new()
            {
                Value =
                    new SearchHistorySnapshot
                    {
                        Entries =
                        [
                            CreateEntry(
                                "oldest",
                                ReferenceTime.AddDays(-2)),

                            CreateEntry(
                                "middle",
                                ReferenceTime.AddDays(-1))
                        ]
                    }
            };

        using SearchSuggestionCoordinator coordinator =
            CreateCoordinator(
                settingsStore,
                historyStore,
                new FakeClock(
                    ReferenceTime));

        await coordinator.RecordSearchAsync(
            "newest");

        CollectionAssert.AreEqual(
            new[]
            {
                "newest",
                "middle"
            },
            historyStore.SavedSnapshots[0]
                .Entries
                .Select(
                    entry => entry.Query)
                .ToArray());
    }

    [TestMethod]
    public async Task RecordSearchAsync_WhenDisabled_DoesNotLoadOrSaveHistory()
    {
        FakeSettingsStore settingsStore =
            new()
            {
                Value =
                    CreateSettings(
                        saveSearchHistory: false)
            };

        FakeSearchHistoryStore historyStore =
            new();

        using SearchSuggestionCoordinator coordinator =
            CreateCoordinator(
                settingsStore,
                historyStore,
                new FakeClock());

        await coordinator.RecordSearchAsync(
            "cats");

        Assert.AreEqual(
            0,
            historyStore.LoadCallCount);

        Assert.IsEmpty(
            historyStore.SavedSnapshots);
    }

    [TestMethod]
    public async Task ClearHistoryAsync_UsesHistoryStore()
    {
        FakeSearchHistoryStore historyStore =
            new()
            {
                Value =
                    new SearchHistorySnapshot
                    {
                        Entries =
                        [
                            CreateEntry(
                                "cats",
                                ReferenceTime)
                        ]
                    }
            };

        using SearchSuggestionCoordinator coordinator =
            CreateCoordinator(
                historyStore: historyStore);

        await coordinator.ClearHistoryAsync();

        Assert.AreEqual(
            1,
            historyStore.ClearCallCount);

        Assert.IsEmpty(
            historyStore.Value.Entries);
    }

    [TestMethod]
    public async Task GetSuggestionsAsync_RejectsInvalidMaximum()
    {
        using SearchSuggestionCoordinator coordinator =
            CreateCoordinator();

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            async () =>
            {
                await coordinator.GetSuggestionsAsync(
                    "cats",
                    maximumResults: 0);
            });
    }

    private static SearchSuggestionCoordinator
        CreateCoordinator(
            FakeSettingsStore? settingsStore = null,
            FakeSearchHistoryStore? historyStore = null,
            FakeClock? clock = null)
    {
        return new SearchSuggestionCoordinator(
            settingsStore ??
                new FakeSettingsStore(),
            historyStore ??
                new FakeSearchHistoryStore(),
            clock ??
                new FakeClock(
                    ReferenceTime));
    }

    private static SearchHistoryEntry CreateEntry(
        string query,
        DateTimeOffset lastUsedAtUtc,
        int useCount = 1)
    {
        return new SearchHistoryEntry
        {
            Query = query,
            LastUsedAtUtc = lastUsedAtUtc,
            UseCount = useCount
        };
    }

    private static AppSettings CreateSettings(
        bool saveSearchHistory = true,
        bool useHistorySuggestions = true,
        int searchHistoryLimit = 50)
    {
        return new AppSettings
        {
            Search =
                new SearchSettings
                {
                    SaveSearchHistory =
                        saveSearchHistory,

                    UseHistorySuggestions =
                        useHistorySuggestions,

                    SearchHistoryLimit =
                        searchHistoryLimit
                }
        };
    }
}
