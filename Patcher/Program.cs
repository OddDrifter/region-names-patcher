using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Implicit;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using Patcher.Serialization;
using System.Collections.Frozen;
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

        var mod     = await Helper.DeserializeFromPath(dataPath);
        var regions = ImmutableArray.CreateRange(mod.Regions.Select(region =>
        {
            if (state.LinkCache.TryResolve<IRegionGetter>(region.FormKey, out var getter))
            {
                var ret = state.PatchMod.Regions.GetOrAddAsOverride(getter);
                ret.Map ??= region.Map;
                return ret;
            }
            return state.PatchMod.Regions.DuplicateInAsNewRecord(region);
        }));

        using var loadOrder = state.LoadOrder;

        var linkCache   = state.LinkCache;
        var worldspaces = FrozenDictionary.ToFrozenDictionary(
            regions.Select(static i => i.Worldspace).Where(static i => !i.IsNull).Distinct(),
            static i => i.AsGetter(), 
            static i => new Dictionary<P2Int, IModContext<ISkyrimMod, ISkyrimModGetter, ICell, ICellGetter>>()
        );

        var exteriorCells = loadOrder.PriorityOrder
            .OnlyEnabledAndExisting().Cell().WinningContextOverrides(linkCache)
            .Where(static i => !i.Record.Flags.HasFlag(Cell.Flag.IsInteriorCell))
            .Where(static i => !i.Record.MajorFlags.HasFlag(Cell.MajorFlag.Persistent));

        foreach (var cellContext in exteriorCells)
        {
            if (cellContext.TryGetParent<IWorldspaceGetter>()?.ToNullableLink() is not { } link || !worldspaces.ContainsKey(link))
                continue;
            worldspaces[link].Add(cellContext.Record.Grid!.Point, cellContext);
        }

        foreach (var region in regions)
        {
            if (!worldspaces.TryGetValue(region.Worldspace, out var cellContexts))
                continue;

            int count = 0;
            var formLink = region.ToLink();

            foreach (var area in region.RegionAreas)
            {
                if (area.RegionPointListData is not ExtendedList<P2Float> points)
                    continue;

                List<Segment> segments = [];
                for (var i = 0; i < points.Count - 1; i++)
                    segments.Add(new Segment(points[i], points[i + 1]));
                segments.Add(new Segment(points[^1], points[0]));

                float x1 = points.Min(static i => i.X).NearestFloorOf(4096);
                float y1 = points.Min(static i => i.Y).NearestFloorOf(4096);
                float x2 = points.Max(static i => i.X).NearestCeilingOf(4096);
                float y2 = points.Max(static i => i.Y).NearestCeilingOf(4096);

                for (var x = x1; x <= x2; x += 4096f)
                {
                    for (var y = y1; y <= y2; y += 4096f)
                    {
                        var gx = (int)(x / 4096f);
                        var gy = (int)(y / 4096f);

                        if (!cellContexts.TryGetValue(new(gx, gy), out var ctx))
                            continue;

                        bool isInRegion = false;
                        Segment segment = new (new(x1, y1), new(x, y));
                        if (segments.Count(i => Utilities.Intersects(segment, i)) % 2 > 0)
                            isInRegion = true;

                        if (!isInRegion)
                        {
                            foreach (var edge in Utilities.Directions.Select(i => new Segment(new(x, y), new(x + i.X, y + i.Y))))
                            {
                                if (segments.Any(i => Utilities.Intersects(edge, i)))
                                    isInRegion = true;
                            }
                        }

                        if (isInRegion && (!ctx.Record.Regions?.Contains(formLink) ?? true))
                        {
                            (ctx.GetOrAddAsOverride(state.PatchMod).Regions ??= []).Add(formLink);
                            count++;
                        }
                    }
                }
            }

            Console.WriteLine($"Added region [{region.FormKey}] {region.EditorID} to {count} cell(s)");
        }
    }
}
