using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Patice.Domain;
using Patice.Segments;
using Xunit;

public sealed class SegmentStoresIntegration : IDisposable
{
    private readonly Guid customId = Guid.NewGuid();
    private readonly string path = Path.Combine(AppContext.BaseDirectory, "segment-test-data", Guid.NewGuid().ToString("N"));

    [Fact]
    public void AllEmbeddedJsonPassesValidationBeforeTheCatalogIsUsed()
    {
        var assembly = typeof(BuiltinSegments).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Patice.Segments.", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(resources);
        var ids = new HashSet<(int, Difficulty, Guid)>();
        var management = new SegmentManagement();
        management.RegisterStore(new BuiltinSegments());
        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            var segments = SegmentStorage.ReadSegments(stream);
            SegmentStorage.ValidateSegments(segments);
            foreach (var segment in segments)
            {
                Assert.True(ids.Add((segment.musicId, segment.difficulty, segment.id)), resource);
                Assert.Contains(management.Get(segment.musicId, segment.difficulty),
                    entry => entry.id == segment.id && entry.name == segment.name &&
                             entry.startMs == segment.startMs && entry.endMs == segment.endMs);
            }
        }
    }

    [Fact]
    public void LocalStoreRejectsInvalidDataOnReadAndWriteWithoutReplacingExistingData()
    {
        var local = new LocalSegmentStore(path);
        local.Store(new Segment(11820, Difficulty.Basic, customId, "自定义", 1000, 2000));
        Assert.Throws<InvalidDataException>(() =>
            local.Store(new Segment(11820, Difficulty.Basic, customId, "自定义", 2000, 1000)));
        Assert.Equal(1000, Assert.Single(new LocalSegmentStore(path).Get(11820, Difficulty.Basic)).startMs);

        var file = Path.Combine(path, "11820", "basic.json");
        File.WriteAllText(file, Regex.Replace(File.ReadAllText(file), "\"endMs\"\\s*:\\s*2000", "\"endMs\": 500"));
        var management = new SegmentManagement();
        management.RegisterStore(new LocalSegmentStore(path));
        Assert.Throws<InvalidDataException>(() => management.Get(11820, Difficulty.Basic));
    }

    [Fact]
    public void BuiltinAndLocalSegmentsShareTheSameLookupAfterSavingAndReopening()
    {
        var management = new SegmentManagement();
        management.RegisterStore(new BuiltinSegments());
        var builtin = management.Get(417, Difficulty.Master);
        Assert.NotEmpty(builtin);
        Assert.False(Directory.Exists(path));

        var local = new LocalSegmentStore(path);
        local.Store(new Segment(417, Difficulty.Master, customId, "自定义片段", 30000, 35000));
        local.Store(new Segment(417, Difficulty.Master, customId, "修改后的片段", 31000, 36000));
        local.Store(new Segment(417, Difficulty.Expert, customId, "另一难度", 40000, 45000));
        local.Store(new Segment(11820, Difficulty.Basic, customId, "另一首歌", 50000, 55000));

        management.RegisterStore(new LocalSegmentStore(path));
        var segments = management.Get(417, Difficulty.Master);
        Assert.Equal(builtin.Length + 1, segments.Length);
        var custom = Assert.Single(segments, segment => segment.id == customId);
        Assert.Equal("修改后的片段", custom.name);
        Assert.Equal(31000, custom.startMs);
        Assert.Equal(36000, custom.endMs);
        Assert.Equal("另一难度", Assert.Single(management.Get(417, Difficulty.Expert), segment => segment.id == customId).name);
        Assert.Equal("另一首歌", Assert.Single(management.Get(11820, Difficulty.Basic), segment => segment.id == customId).name);
        Assert.Empty(management.Get(99999, Difficulty.Basic));
    }

    public void Dispose()
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
