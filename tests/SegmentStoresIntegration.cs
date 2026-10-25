using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Patice.Domain;
using Patice.Segments;
using Xunit;

public sealed class SegmentStoresIntegration : IDisposable
{
    private readonly string path = Path.Combine(AppContext.BaseDirectory, "segment-test-data", Guid.NewGuid().ToString("N"));

    [Fact]
    public void AllEmbeddedJsonPassesValidationBeforeTheCatalogIsUsed()
    {
        var assembly = typeof(BuiltinSegments).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Patice.Segments.", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(resources);
        var ids = new HashSet<(int, Difficulty, string)>();
        var management = new SegmentManagement();
        management.RegisterStore(new BuiltinSegments());
        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            var document = SegmentStorage.ReadDocument(stream);
            SegmentStorage.ValidateDocument(document);
            foreach (var segment in document.Segments)
            {
                Assert.True(ids.Add((document.Chart.MusicId, document.Chart.Difficulty, segment.id)), resource);
                Assert.Contains(management.Get(document.Chart.MusicId, document.Chart.Difficulty),
                    entry => entry.id == segment.id && entry.name == segment.name &&
                             entry.startMs == segment.startMs && entry.endMs == segment.endMs);
            }
        }
    }

    [Fact]
    public void LocalStoreRejectsInvalidDataOnReadAndWriteWithoutReplacingExistingData()
    {
        var local = new LocalSegmentStore(path);
        local.Store(11820, Difficulty.Basic, new Segment("custom", "自定义", 1000, 2000));
        Assert.Throws<InvalidDataException>(() =>
            local.Store(11820, Difficulty.Basic, new Segment("custom", "自定义", 2000, 1000)));
        Assert.Equal(1000, Assert.Single(new LocalSegmentStore(path).Get(11820, Difficulty.Basic)).startMs);

        var file = Path.Combine(path, "11820", "basic.json");
        File.WriteAllText(file, File.ReadAllText(file).Replace("\"endMs\": 2000", "\"endMs\": 500"));
        var management = new SegmentManagement();
        management.RegisterStore(new LocalSegmentStore(path));
        Assert.Throws<InvalidDataException>(() => management.Get(11820, Difficulty.Basic));
    }

    [Fact]
    public void BuiltinAndLocalSegmentsShareTheSameLookupAfterSavingAndReopening()
    {
        var management = new SegmentManagement();
        management.RegisterStore(new BuiltinSegments());
        Assert.Equal(2, management.Get(11820, Difficulty.Basic).Length);
        Assert.Empty(management.Get(11820, Difficulty.Expert));
        Assert.False(Directory.Exists(path));

        var local = new LocalSegmentStore(path);
        local.Store(11820, Difficulty.Basic, new Segment("custom", "自定义片段", 30000, 35000));
        local.Store(11820, Difficulty.Basic, new Segment("custom", "修改后的片段", 31000, 36000));
        local.Store(11820, Difficulty.Expert, new Segment("custom", "另一难度", 40000, 45000));
        local.Store(12025, Difficulty.Basic, new Segment("custom", "另一首歌", 50000, 55000));

        management.RegisterStore(new LocalSegmentStore(path));
        var segments = management.Get(11820, Difficulty.Basic);
        Assert.Equal(new[] { "opening", "passage", "custom" }, segments.Select(segment => segment.id));
        Assert.Equal("修改后的片段", segments[2].name);
        Assert.Equal(31000, segments[2].startMs);
        Assert.Equal(36000, segments[2].endMs);
        Assert.Equal("另一难度", Assert.Single(management.Get(11820, Difficulty.Expert)).name);
        Assert.Equal("另一首歌", Assert.Single(management.Get(12025, Difficulty.Basic)).name);
        Assert.Empty(management.Get(99999, Difficulty.Basic));
    }

    [Fact]
    public void LocalLookupRejectsAFileBelongingToAnotherChart()
    {
        var local = new LocalSegmentStore(path);
        local.Store(11820, Difficulty.Basic, new Segment("custom", "自定义", 1000, 2000));
        Directory.CreateDirectory(Path.Combine(path, "12025"));
        File.Copy(Path.Combine(path, "11820", "basic.json"), Path.Combine(path, "12025", "basic.json"));
        var management = new SegmentManagement();
        management.RegisterStore(new BuiltinSegments());
        management.RegisterStore(new LocalSegmentStore(path));
        Assert.Throws<InvalidDataException>(() => management.Get(12025, Difficulty.Basic));
    }

    public void Dispose()
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
