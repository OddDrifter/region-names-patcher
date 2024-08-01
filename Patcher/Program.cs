using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Implicit;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using Patcher.Serialization;
using System.Collections.Immutable;

namespace Patcher;

class Program
{
    public static async Task<int> Main(string[] args) =>
        await SynthesisPipeline.Instance
            .AddRunnabilityCheck(CheckRunnability)
            .AddPatch<ISkyrimMod, ISkyrimModGetter>(Func)
            .SetTypicalOpen(GameRelease.SkyrimSE, new("Unique Region Names - Generated", ModType.Plugin))
            .Run(args);

    public static void CheckRunnability(IRunnabilityState state)
    {
        var required = ImplicitListings.Instance.Skyrim(SkyrimRelease.SkyrimSE);
        state.LoadOrder.AssertListsMods(required);
    }

    public static async void Func(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
    {
        if (state.InternalDataPath is not DirectoryPath dataPath)
            throw new DirectoryNotFoundException();

        var mod = await Helper.DeserializeFromPath(dataPath);
        var regions = mod.Regions.Select(region =>
        {
            return region.FormKey.ModKey == mod.ModKey
                ? state.PatchMod.Regions.DuplicateInAsNewRecord(region)
                : state.PatchMod.Regions.GetOrAddAsOverride(region);
        }).ToImmutableArray();
        var worldspaces = regions.Select(static i => i.Worldspace)
            .ToHashSet();

        var linkCache = state.LinkCache;
        var exteriorCells = state.LoadOrder.PriorityOrder.Cell()
            .WinningContextOverrides(linkCache)
            .Where(i => i.TryGetParent<IWorldspaceGetter>(out var parent) && worldspaces.Contains(parent))
            .Where(static i => !i.Record.Flags.HasFlag(Cell.Flag.IsInteriorCell))
            .Where(static i => !i.Record.MajorFlags.HasFlag(Cell.MajorFlag.Persistent))
            .ToImmutableArray();

        foreach (var grp in regions.GroupBy(static i => i.Worldspace)) 
        {
            var world = grp.Key;
            if (!world.TryResolveIdentifier(linkCache, out var _))
                continue;

            var cellContexts = exteriorCells.Where(i => i.TryGetParent<IWorldspaceGetter>()?.FormKey == world.FormKey)
                .ToDictionary(i => i.Record.Grid!.Point);

            foreach (var region in grp)
            {
                int addedCount = 0;
                var link = region.ToLink();

                foreach (var area in region.RegionAreas)
                {
                    if (area.RegionPointListData is not ExtendedList<P2Float> points)
                        continue;

                    float x1 = points.Min(static i => i.X);
                    float y1 = points.Min(static i => i.Y);
                    float x2 = points.Max(static i => i.X);
                    float y2 = points.Max(static i => i.Y);

                    List<SFloat> segments = [];
                    for (var i = 0; i < points.Count - 1; i++)
                        segments.Add(new SFloat(points[i], points[i + 1]));
                    segments.Add(new SFloat(points[^1], points[0]));

                    for (var x = x1; x2 - x > float.Epsilon; x += 4096f)
                    {
                        for (var y = y1; y2 - y > float.Epsilon; y += 4096f)
                        {
                            var segment = new SFloat(new(x, y), new(x1 - 4096f, y));
                            var isInsideRegion = segments.Count(i => Utilities.Intersects(segment, i)) % 2 > 0;
                            var isOnSegment = segments.Any(i => Utilities.IsPointOnSegment(i.P1, segment.P1, i.P2));

                            if ((isInsideRegion || isOnSegment) &&
                                cellContexts.TryGetValue(new P2Int((int)Math.Floor(x / 4096.0), (int)Math.Floor(y / 4096.0)), out var ctx))
                            {
                                if (ctx.Record.Regions is null ||
                                    ctx.Record.Regions.Contains(link) is false)
                                {
                                    (ctx.GetOrAddAsOverride(state.PatchMod).Regions ??= []).Add(link);
                                    addedCount++;
                                }
                            }
                        }
                    }
                }

                Console.WriteLine($"Added region [{region.FormKey}] {region.EditorID} to {addedCount} cell(s)");
            }
        }
    }
}
