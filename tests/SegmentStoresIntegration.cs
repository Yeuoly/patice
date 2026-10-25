using System;
using System.IO;
using System.Linq;
using Patice.Domain;
using Patice.Segments;
using Xunit;

public sealed class SegmentStoresIntegration : IDisposable
{
    private readonly string path = Path.Combine(AppContext.BaseDirectory, "segment-test-data", Guid.NewGuid().ToString("N"));

    [Fact]
    public void BuiltinAndLocalSegmentsShareTheSameLookupAfterSavingAndReopening()
    {
        var management = new SegmentManagement();
        management.RegisterStore(new BuiltinSegments());
        Assert.Equal(2, management.Get(11820, "basic").Length);
        Assert.Empty(management.Get(11820, "expert"));
        Assert.False(Directory.Exists(path));

        var local = new LocalSegmentStore(path);
        local.Store(11820, "basic", new Segment("custom", "自定义片段", 30000, 35000));
        local.Store(11820, "basic", new Segment("custom", "修改后的片段", 31000, 36000));
        local.Store(11820, "expert", new Segment("custom", "另一难度", 40000, 45000));
        local.Store(12025, "basic", new Segment("custom", "另一首歌", 50000, 55000));

        management.RegisterStore(new LocalSegmentStore(path));
        var segments = management.Get(11820, "basic");
        Assert.Equal(new[] { "opening", "passage", "custom" }, segments.Select(segment => segment.id));
        Assert.Equal("修改后的片段", segments[2].name);
        Assert.Equal(31000, segments[2].startMs);
        Assert.Equal(36000, segments[2].endMs);
        Assert.Equal("另一难度", Assert.Single(management.Get(11820, "expert")).name);
        Assert.Equal("另一首歌", Assert.Single(management.Get(12025, "basic")).name);
        Assert.Empty(management.Get(99999, "basic"));
    }

    [Fact]
    public void LocalLookupRejectsAFileBelongingToAnotherChart()
    {
        var local = new LocalSegmentStore(path);
        local.Store(11820, "basic", new Segment("custom", "自定义", 1000, 2000));
        Directory.CreateDirectory(Path.Combine(path, "12025"));
        File.Copy(Path.Combine(path, "11820", "basic.json"), Path.Combine(path, "12025", "basic.json"));
        var management = new SegmentManagement();
        management.RegisterStore(new BuiltinSegments());
        management.RegisterStore(new LocalSegmentStore(path));
        Assert.Throws<InvalidDataException>(() => management.Get(12025, "basic"));
    }

    public void Dispose()
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
