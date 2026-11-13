#if DEBUG
// In-game tests run by Gravitas.TestHarness:
//   python Gravitas.TestProbe/probe.py run --project <this repo> --tests '{"tags":["plantcensus"]}'
// Needs HarnessDllPath in Local.Build.props (Check / Wait / Arrange come from the harness assembly).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gravitas.TestHarness;
using HarmonyLib;
using UnityEngine;

namespace PlantCensus;

internal static class HarnessTests
{
    private const string Mealwood = "BasicSingleHarvestPlant";
    private const string MealwoodSeed = "BasicSingleHarvestPlantSeed";
    private const string ArborTree = "ForestTree";

    [HarnessTest(Name = "plantcensus.mod_loaded", Tags = "plantcensus", RequiresGame = false)]
    public static void ModLoaded()
    {
        Check.True(PlantCensusMod.modPath != null, "PlantCensusMod.OnLoad has not run");
    }

    // UserMod2.OnLoad applies the patches; a second PatchAll would run every patch twice.
    [HarnessTest(Name = "plantcensus.patches_applied_once", Tags = "plantcensus", RequiresGame = false)]
    public static void PatchesAppliedOnce()
    {
        CheckPatchedOnce(AccessTools.Method(typeof(PlayerController), "OnPrefabInit"), typeof(PlayerControllerOnPrefabInitPatch), prefix: false);
        CheckPatchedOnce(AccessTools.Method(typeof(ToolMenu), "CreateBasicTools"), typeof(ToolMenuCreateBasicToolsPatch), prefix: false);
        CheckPatchedOnce(AccessTools.Method(typeof(GameInputMapping), "CompareActionKeyCodes"), typeof(CompareActionKeyCodesPatch), prefix: true);
        CheckPatchedOnce(AccessTools.Method(typeof(GameUtil), nameof(GameUtil.GetHotkeyString)), typeof(GetHotkeyStringPatch), prefix: true);
        CheckPatchedOnce(AccessTools.Method(typeof(SelectTool), nameof(SelectTool.OnLeftClickDown)), typeof(SelectToolRoomClickPatch), prefix: true);
    }

    private static void CheckPatchedOnce(System.Reflection.MethodBase target, System.Type patch, bool prefix)
    {
        Check.NotNull(target, "patch target for " + patch.Name + " not found");
        var info = Harmony.GetPatchInfo(target);
        Check.NotNull(info, target.Name + " is not patched");
        var patches = prefix ? info.Prefixes : info.Postfixes;
        Check.Equal(1, patches.Count(p => p.PatchMethod.DeclaringType == patch), patch.Name + " on " + target.Name);
    }

    // The tool exists on the player controller and its button sits right after the harvest tool, once.
    [HarnessTest(Name = "plantcensus.tool_registered", Tags = "plantcensus")]
    public static void ToolRegistered()
    {
        Check.Equal(1, PlayerController.Instance.tools.OfType<CensusTool>().Count(), "census tools on the player controller");

        var names = ToolMenu.Instance.basicTools.Where(c => c.tools.Count > 0).Select(c => c.tools[0].toolName).ToList();
        Check.Equal(1, names.Count(n => n == CensusTool.ToolName), "census buttons in the tool menu");
        Check.Equal(names.IndexOf("HarvestTool") + 1, names.IndexOf(CensusTool.ToolName), "position of the census button");
    }

    // The button shows the mod's own icon when ModAssets/tool_icon.png is there, else the borrowed vanilla one.
    [HarnessTest(Name = "plantcensus.tool_icon", Tags = "plantcensus")]
    public static void ToolIconUsed()
    {
        var file = System.IO.Path.Combine(PlantCensusMod.modPath, "ModAssets", ToolIcon.FileName);
        var expected = System.IO.File.Exists(file) ? ToolIcon.SpriteName : ToolIcon.Fallback;
        Check.Equal(expected, ToolIcon.Resolve(), "icon name for the tool button");
        Check.NotNull(Assets.GetSprite(expected), "the game has no sprite named " + expected);

        var collection = ToolMenu.Instance.basicTools.First(c => c.tools.Count > 0 && c.tools[0].toolName == CensusTool.ToolName);
        Check.Equal(expected, collection.icon, "icon of the census button");
        var image = collection.tools[0].toggle != null ? collection.tools[0].toggle.transform.Find("FG") : null;
        Check.NotNull(image, "the census button has no icon image");
        Check.True(image.GetComponent<UnityEngine.UI.Image>().sprite == Assets.GetSprite(expected), "the census button shows another sprite");
        Check.NoLogErrors();
    }

    // A wild plant inside the area is listed with its species, growth and crop; one outside is not.
    [HarnessTest(Name = "plantcensus.wild_plant_listed", Tags = "plantcensus")]
    public static IEnumerator WildPlantListed()
    {
        var scene = new Scene();
        try
        {
            var plant = scene.WildPlant();
            yield return null;
            yield return null;
            var cell = Grid.PosToCell(plant);

            var rows = PlantCollector.Collect(CensusSelection.FromRect(cell, cell));
            Check.Equal(1, rows.Count, "rows for the plant's cell");
            var row = rows[0];
            Check.Equal(Mealwood, row.SpeciesKey, "species key");
            Check.True(!string.IsNullOrEmpty(row.SpeciesName) && !row.SpeciesName.Contains("<"), "species name is '" + row.SpeciesName + "'");
            Check.True(!string.IsNullOrEmpty(row.Name) && !row.Name.Contains("<"), "name is '" + row.Name + "'");
            Check.True(row.IsWild, "a plant outside a planter is wild");
            Check.True(row.Growth >= 0f && row.Growth <= 1f, "growth is " + row.Growth);
            Check.True(!string.IsNullOrEmpty(row.CropName), "crop name is missing");
            Check.True(!string.IsNullOrEmpty(row.CropAmount), "crop amount is missing");
            Check.True(row.HasHarvestSetting, "Mealwood has a harvest setting");
            Check.True(row.Temperature > 0f, "temperature is " + row.Temperature);
            Check.NotNull(row.Icon, "icon");
            Check.True(row.Target == plant.GetComponent<KSelectable>(), "the row selects the plant");

            var beside = Grid.CellRight(Grid.CellRight(cell));
            Check.Equal(0, PlantCollector.Collect(CensusSelection.FromRect(beside, beside)).Count, "rows for a cell beside the plant");
            Check.NoLogErrors();
        }
        finally
        {
            scene.Dispose();
        }
    }

    // A plant in a planter box counts as farmed.
    [HarnessTest(Name = "plantcensus.farmed_plant_listed", Tags = "plantcensus")]
    public static IEnumerator FarmedPlantListed()
    {
        var scene = new Scene();
        try
        {
            var planter = scene.Planter();
            yield return null;
            yield return null;
            var plot = planter.GetComponent<PlantablePlot>();
            Check.NotNull(plot, "the planter box has no plot");
            plot.ForceDeposit(Arrange.Spawn(MealwoodSeed, Grid.PosToCell(planter)));
            yield return null;
            yield return null;
            Check.NotNull(plot.Occupant, "the seed did not become a plant");

            var cell = Grid.PosToCell(plot.Occupant);
            var rows = PlantCollector.Collect(CensusSelection.FromRect(cell, cell));
            Check.Equal(1, rows.Count, "rows for the planter's cell");
            Check.Equal(Mealwood, rows[0].SpeciesKey, "species key");
            Check.False(rows[0].IsWild, "a plant in a planter box is farmed");
        }
        finally
        {
            scene.Dispose();
        }
    }

    // A plant that is too hot stops growing, and the list says why.
    [HarnessTest(Name = "plantcensus.stalled_plant_shows_cause", Tags = "plantcensus", TimeoutSeconds = 180)]
    public static IEnumerator StalledPlantShowsCause()
    {
        var scene = new Scene();
        try
        {
            var plant = scene.WildPlant();
            yield return null;
            yield return null;

            // Above Mealwood's comfortable range (30 C), below its lethal limit (125 C).
            plant.GetComponent<PrimaryElement>().Temperature = 273.15f + 60f;
            yield return Wait.Seconds(6f);
            Check.True(plant != null, "the plant died");

            var wilt = plant.GetComponent<WiltCondition>();
            Check.True(wilt.IsWilting(), "the hot plant is not wilting");
            var row = PlantCollector.BuildRow(plant, plant.GetComponent<KPrefabID>());
            Check.Equal(PlantState.Stalled, row.State, "state");
            Check.True(row.Problems.Length > 0, "a stalled plant lists no problem");
            Check.True(row.Problems.All(p => p.Length > 0 && !p.Contains("<") && !p.Contains("\n") && p == p.Trim()),
                "problems are not plain text: " + string.Join(" | ", row.Problems));
            Check.Equal(TemperatureBand.Hot, row.TemperatureBand, "temperature band");
            Check.True(row.GrowthTotalCycles < 0f, "a stalled plant reports a growth time of " + row.GrowthTotalCycles);
        }
        finally
        {
            scene.Dispose();
        }
    }

    // A tree is one row with its branches under it, whichever part of it is in the area; the window shows the
    // branches only while the tree is expanded, and the state filter applies to them too.
    [HarnessTest(Name = "plantcensus.tree_branches_grouped", Tags = "plantcensus", TimeoutSeconds = 180)]
    public static IEnumerator TreeBranchesGrouped()
    {
        var scene = new Scene();
        try
        {
            var tree = scene.WildPlant(ArborTree);
            yield return null;
            yield return null;
            var grower = tree.GetSMI<PlantBranchGrower.Instance>();
            Check.NotNull(grower, "the Arbor Tree has no branch grower");
            for (var i = 0; i < 3; i++)
            {
                grower.SpawnRandomBranch(0.5f);
            }

            yield return null;
            yield return null;
            var branches = grower.GetExistingBranches();
            foreach (var branch in branches)
            {
                scene.Track(branch);
            }

            Check.True(branches.Length >= 2, "the tree grew " + branches.Length + " branches");

            var trunkCell = Grid.PosToCell(tree);
            var rows = PlantCollector.Collect(CensusSelection.FromRect(trunkCell, trunkCell));
            Check.Equal(1, rows.Count, "rows for the trunk's cell");
            Check.Equal(branches.Length, rows[0].Children.Count, "branches under the trunk");
            Check.False(rows[0].IsBranch, "the trunk is marked as a branch");
            Check.True(rows[0].Children.All(c => c.IsBranch && c.SpeciesKey == ArborTree), "branches are not filed under the tree's species");
            Check.True(rows[0].Children.All(c => c.Children.Count == 0), "a branch has branches");

            var branchCell = Grid.PosToCell(branches[0]);
            Check.True(branchCell != trunkCell, "the branch stands in the trunk's cell");
            var byBranch = PlantCollector.Collect(CensusSelection.FromRect(branchCell, branchCell));
            Check.Equal(1, byBranch.Count, "rows for a branch's cell");
            Check.True(byBranch[0].Target == tree.GetComponent<KSelectable>(), "a branch's cell does not list its tree");
            Check.Equal(branches.Length, byBranch[0].Children.Count, "branches under the trunk, selected through a branch");

            var both = CensusSelection.FromRect(trunkCell, trunkCell);
            both.Cells.Add(branchCell);
            Check.Equal(1, PlantCollector.Collect(both).Count, "rows for the trunk's and a branch's cell together");

            CensusScreen.Open(CensusSelection.FromRect(trunkCell, trunkCell));
            yield return null;
            var screen = CensusScreen.Current;
            Check.NotNull(screen, "the census window did not open");
            Check.Equal(1, screen.TotalRows, "total rows");
            Check.Equal(1, screen.ShownRows, "shown rows");
            Check.Equal(0, screen.ShownBranches, "branches shown before expanding");

            var row = screen.Row(0);
            screen.ToggleExpanded(row);
            Check.Equal(branches.Length, screen.ShownBranches, "branches shown after expanding");

            var state = row.Children[0].State;
            var others = row.Children.Count(c => c.State != state);
            screen.ToggleState(state);
            Check.Equal(screen.ShownRows == 0 ? 0 : others, screen.ShownBranches, "branches shown with one state switched off");
            screen.ToggleState(state);
            Check.Equal(branches.Length, screen.ShownBranches, "branches shown with the state back on");

            screen.ToggleExpanded(row);
            Check.Equal(0, screen.ShownBranches, "branches shown after collapsing");
            yield return null;
            Check.NoLogErrors();
        }
        finally
        {
            if (CensusScreen.Current != null)
            {
                CensusScreen.Current.Close();
            }

            scene.Dispose();
        }
    }

    // The trunk's row sums up the tree: ready if any branch is, the branches' average growth, their crop and problems.
    [HarnessTest(Name = "plantcensus.tree_summary", Tags = "plantcensus", RequiresGame = false)]
    public static void TreeSummary()
    {
        var trunk = new PlantRow { Name = "T", State = PlantState.Idle, Growth = 1f, Problems = new[] { "own" } };
        trunk.Children.Add(new PlantRow { State = PlantState.Ready, Growth = 1f, CropName = "Wood", CropAmount = "300 kg", HasHarvestSetting = true, AutoHarvest = true });
        trunk.Children.Add(new PlantRow { State = PlantState.Growing, Growth = 0.5f, CropName = "Wood", CropAmount = "300 kg" });
        trunk.Children.Add(new PlantRow { State = PlantState.Stalled, Growth = 0.3f, Problems = new[] { "cold", "own" } });
        PlantCollector.Summarize(trunk);

        Check.Equal(PlantState.Ready, trunk.State, "state");
        Check.Equal(1, trunk.ReadyBranches, "ready branches");
        Check.True(trunk.GrowthIsAverage, "growth is not marked as an average");
        Check.Near(0.6, trunk.Growth, 0.001, "average growth");
        Check.Equal("Wood", trunk.CropName, "crop name");
        Check.Equal("300 kg", trunk.CropAmount, "crop amount");
        Check.Equal(2, trunk.CropCount, "branches with a crop");
        Check.True(trunk.HasHarvestSetting && trunk.AutoHarvest, "harvest setting was not taken from the branches");
        Check.Equal("own|cold", string.Join("|", trunk.Problems), "problems");

        // The tree passes a state filter through a branch, too.
        var rows = new List<PlantRow> { trunk };
        Check.Equal(1, CensusScreen.Filter(rows, new HashSet<PlantState> { PlantState.Stalled }, new HashSet<string>()).Count, "filter by a branch's state");
        Check.Equal(0, CensusScreen.Filter(rows, new HashSet<PlantState> { PlantState.Idle }, new HashSet<string>()).Count, "filter by a state nothing has");

        // A trunk that is still growing keeps its own growth.
        var young = new PlantRow { State = PlantState.Growing, Growth = 0.4f };
        young.Children.Add(new PlantRow { State = PlantState.Growing, Growth = 0.9f });
        PlantCollector.Summarize(young);
        Check.False(young.GrowthIsAverage, "a growing trunk shows the branches' average");
        Check.Near(0.4, young.Growth, 0.001, "growth of a growing trunk");
        Check.Equal(PlantState.Growing, young.State, "state of a growing trunk");
    }

    // The window opens on the area, the filters narrow the list, and it closes cleanly.
    [HarnessTest(Name = "plantcensus.screen_filters", Tags = "plantcensus")]
    public static IEnumerator ScreenFilters()
    {
        var scene = new Scene();
        try
        {
            var plant = scene.WildPlant();
            yield return null;
            yield return null;
            var cell = Grid.PosToCell(plant);
            var state = PlantCollector.BuildRow(plant, plant.GetComponent<KPrefabID>()).State;

            CensusScreen.Open(CensusSelection.FromRect(cell, cell));
            yield return null;
            var screen = CensusScreen.Current;
            Check.NotNull(screen, "the census window did not open");
            Check.Equal(1, screen.TotalRows, "total rows");
            Check.Equal(1, screen.ShownRows, "shown rows");

            screen.ToggleState(state);
            Check.Equal(0, screen.ShownRows, "shown rows with the plant's state switched off");
            screen.ToggleState(state);
            Check.Equal(1, screen.ShownRows, "shown rows with the state back on");

            screen.ToggleSpecies("NoSuchPlant");
            Check.Equal(0, screen.ShownRows, "shown rows with another species selected");
            screen.ToggleSpecies(Mealwood);
            Check.Equal(1, screen.ShownRows, "shown rows with the plant's species also selected");
            screen.ClearSpecies();
            Check.Equal(1, screen.ShownRows, "shown rows with the species filter cleared");

            yield return null;
            screen.Close();
            yield return null;
            Check.True(CensusScreen.Current == null, "the census window is still open");
            Check.NoLogErrors();
        }
        finally
        {
            if (CensusScreen.Current != null)
            {
                CensusScreen.Current.Close();
            }

            scene.Dispose();
        }
    }

    // An area without plants opens an empty list instead of failing.
    [HarnessTest(Name = "plantcensus.empty_area", Tags = "plantcensus")]
    public static IEnumerator EmptyArea()
    {
        try
        {
            CensusScreen.Open(new CensusSelection());
            yield return null;
            Check.NotNull(CensusScreen.Current, "the census window did not open");
            Check.Equal(0, CensusScreen.Current.TotalRows, "total rows");
            Check.Equal(0, CensusScreen.Current.ShownRows, "shown rows");
            Check.NoLogErrors();
        }
        finally
        {
            if (CensusScreen.Current != null)
            {
                CensusScreen.Current.Close();
            }
        }
    }

    // Sorting keeps rows without a value at the end in both directions.
    [HarnessTest(Name = "plantcensus.sorting", Tags = "plantcensus", RequiresGame = false)]
    public static void Sorting()
    {
        var rows = new List<PlantRow>
        {
            new PlantRow { Name = "B", Growth = 0.5f, CropName = "Y" },
            new PlantRow { Name = "D", Growth = -1f },
            new PlantRow { Name = "A", Growth = 0.9f, CropName = "X" },
            new PlantRow { Name = "C", Growth = 0.1f, CropName = "Z" },
        };

        Check.Equal("CBAD", Names(CensusScreen.Sorted(rows, 3, false)), "growth ascending");
        Check.Equal("ABCD", Names(CensusScreen.Sorted(rows, 3, true)), "growth descending");
        Check.Equal("ABCD", Names(CensusScreen.Sorted(rows, 1, false)), "name ascending");
        Check.Equal("DCBA", Names(CensusScreen.Sorted(rows, 1, true)), "name descending");
        Check.Equal("ABCD", Names(CensusScreen.Sorted(rows, 4, false)), "yield ascending");
        Check.Equal("CBAD", Names(CensusScreen.Sorted(rows, 4, true)), "yield descending");
        Check.Equal("BDAC", Names(CensusScreen.Sorted(rows, -1, false)), "unsorted");

        var states = new HashSet<PlantState> { PlantState.Ready };
        rows[2].State = PlantState.Stalled;
        Check.Equal("BDC", Names(CensusScreen.Filter(rows, states, new HashSet<string>())), "state filter");
    }

    // The player's way in, played with the harness's input: pick the tool on the toolbar, drag over the plant, click its row.
    [HarnessTest(Name = "plantcensus.toolbar_drag_and_row_click", Tags = "plantcensus", TimeoutSeconds = 180)]
    public static IEnumerator ToolbarDragAndRowClick()
    {
        var scene = new Scene();
        try
        {
            var plant = scene.WildPlant();
            yield return null;
            yield return null;
            var cell = Grid.PosToCell(plant);

            Play.ClickToolButton(CensusTool.ToolName);
            Check.True(Play.ActiveTool is CensusTool, "clicking the census button should activate the census tool, got " + Play.ActiveTool);
            Check.True(CensusScreen.Current == null, "the window is open before the drag");

            Play.Drag(cell, cell);
            yield return null;
            var screen = CensusScreen.Current;
            Check.NotNull(screen, "dragging the census tool over the plant did not open the window");
            Check.Equal(1, screen.TotalRows, "rows for the dragged cell");
            Check.Equal(1, Play.FindUi("Row", screen).Count, "row buttons on screen");

            Play.ClickUi("Row", screen);
            yield return null;
            Check.True(CensusScreen.Current == null, "picking a row should close the window");
            Check.True(Play.ActiveTool is SelectTool, "picking a row should hand over to the select tool, got " + Play.ActiveTool);
            Check.True(SelectTool.Instance.selected == plant.GetComponent<KSelectable>(), "the clicked plant should be selected");
            Check.NoLogErrors();
        }
        finally
        {
            if (CensusScreen.Current != null)
            {
                CensusScreen.Current.Close();
            }

            SelectTool.Instance.Select(null);
            Play.ClearTool();
            scene.Dispose();
        }
    }

    // The button in front of a tree's name shows and hides its branches without selecting the tree.
    [HarnessTest(Name = "plantcensus.expander_click", Tags = "plantcensus", TimeoutSeconds = 180)]
    public static IEnumerator ExpanderClick()
    {
        var scene = new Scene();
        try
        {
            var tree = scene.WildPlant(ArborTree);
            yield return null;
            yield return null;
            var grower = tree.GetSMI<PlantBranchGrower.Instance>();
            Check.NotNull(grower, "the Arbor Tree has no branch grower");
            for (var i = 0; i < 3; i++)
            {
                grower.SpawnRandomBranch(0.5f);
            }

            yield return null;
            yield return null;
            var branches = grower.GetExistingBranches();
            foreach (var branch in branches)
            {
                scene.Track(branch);
            }

            Check.True(branches.Length >= 2, "the tree grew " + branches.Length + " branches");

            var cell = Grid.PosToCell(tree);
            Play.ClickToolButton(CensusTool.ToolName);
            Play.Drag(cell, cell);
            yield return null;
            var screen = CensusScreen.Current;
            Check.NotNull(screen, "the census window did not open");
            Check.Equal(0, screen.ShownBranches, "branches shown before the click");
            Check.Equal(1, Play.FindUi("Row", screen).Count, "row buttons before expanding");

            Play.ClickUi("Expander", screen);
            yield return null;
            Check.True(CensusScreen.Current == screen, "the window must stay open when a tree is expanded");
            Check.Equal(branches.Length, screen.ShownBranches, "branches shown after clicking the expander");
            Check.Equal(1 + branches.Length, Play.FindUi("Row", screen).Count, "row buttons after expanding");

            Play.ClickUi("Expander", screen);
            yield return null;
            Check.Equal(0, screen.ShownBranches, "branches shown after clicking the expander again");
            Check.Equal(1, Play.FindUi("Row", screen).Count, "row buttons after collapsing");
            Check.True(SelectTool.Instance.selected == null || SelectTool.Instance.selected.gameObject != tree, "the tree must not be selected by the expander");
            Check.NoLogErrors();
        }
        finally
        {
            if (CensusScreen.Current != null)
            {
                CensusScreen.Current.Close();
            }

            Play.ClearTool();
            scene.Dispose();
        }
    }

    private static string Names(IEnumerable<PlantRow> rows) => string.Concat(rows.Select(r => r.Name));

    // Writes the picture the game shows for a prefab in its own lists (the "ui" pose) to a PNG file. Used once to get
    // real silhouettes for icons. The source texture is not readable, so it goes through a render texture.
    public static string ExportUiSprite(string prefabId, string path)
    {
        var prefab = Assets.GetPrefab(prefabId);
        if (prefab == null)
        {
            return "no prefab " + prefabId;
        }

        var sprite = Def.GetUISprite(prefab).first;
        var texture = sprite.texture;
        var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        Graphics.Blit(texture, target);
        RenderTexture.active = target;
        var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        System.IO.File.WriteAllBytes(path, copy.EncodeToPNG());
        Object.Destroy(copy);
        var rect = sprite.textureRect;
        return texture.width + "x" + texture.height + " rect x=" + rect.x + " y=" + rect.y + " w=" + rect.width + " h=" + rect.height;
    }

    // Scene for looking at the window by hand or in a screenshot. Call through the harness, with game time in between:
    //   /invoke PlantCensus.HarnessTests.DemoBuild, /time/advance, /invoke PlantCensus.HarnessTests.DemoOpen, /screenshot
    private static readonly List<GameObject> demoPlants = new List<GameObject>();
    private static Scene demoScene;

    public static int DemoBuild()
    {
        demoScene = new Scene();
        demoPlants.Clear();
        foreach (var id in new[] { Mealwood, Mealwood, "PrickleFlower", "ColdWheat", "PrickleGrass", "SeaLettuce", ArborTree })
        {
            if (Assets.GetPrefab(id) != null)
            {
                demoPlants.Add(demoScene.WildPlant(id));
            }
        }

        var grown = demoPlants[1].GetComponent<Growing>();
        if (grown != null)
        {
            grown.OverrideMaturityLevel(1f);
        }

        return demoPlants.Count;
    }

    public static int DemoOpen()
    {
        var cells = new List<int>();
        foreach (var go in demoPlants.Where(g => g != null))
        {
            cells.Add(Grid.PosToCell(go));
        }

        // Give the trees some branches, at different stages, and show them.
        foreach (var go in demoPlants.Where(g => g != null))
        {
            var grower = go.GetSMI<PlantBranchGrower.Instance>();
            if (grower == null || grower.GetExistingBranches().Length > 0)
            {
                continue;
            }

            foreach (var growth in new[] { 1f, 0.6f, 0.2f })
            {
                grower.SpawnRandomBranch(growth);
            }

            foreach (var branch in grower.GetExistingBranches())
            {
                demoScene.Track(branch);
            }
        }

        CensusScreen.Open(CensusSelection.FromCells(cells));
        var screen = CensusScreen.Current;
        if (screen == null)
        {
            return -1;
        }

        for (var i = 0; i < screen.TotalRows; i++)
        {
            if (screen.Row(i).Children.Count > 0)
            {
                screen.ToggleExpanded(screen.Row(i));
            }
        }

        return screen.TotalRows;
    }

    // Builds what a test needs in a free spot near the start and removes it afterwards. The test save is an empty
    // sandbox without floor, so buildings are placed without the build-location rules.
    private sealed class Scene
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        // A plant (Mealwood unless told otherwise) standing on a new floor tile.
        public GameObject WildPlant(string prefabId = Mealwood)
        {
            var floor = FreeSpot();
            Place("Tile", floor);
            var plant = Arrange.Spawn(prefabId, Grid.CellAbove(floor));
            Check.NotNull(plant, "the game did not spawn " + prefabId);
            objects.Add(plant);

            // The new tile becomes solid on the next simulation step; until then the plant must not count as uprooted.
            var uprooted = plant.GetComponent<UprootedMonitor>();
            if (uprooted != null)
            {
                uprooted.canBeUprooted = false;
            }

            return plant;
        }

        public GameObject Planter() => Place("PlanterBox", Grid.CellAbove(FreeSpot()));

        // Something the test made by other means; removed with the rest (before what was made earlier).
        public void Track(GameObject go)
        {
            if (go != null && !objects.Contains(go))
            {
                objects.Add(go);
            }
        }

        public void Dispose()
        {
            for (var i = objects.Count - 1; i >= 0; i--)
            {
                if (objects[i] != null)
                {
                    Arrange.Destroy(objects[i]);
                }
            }

            objects.Clear();
        }

        private GameObject Place(string prefabId, int cell)
        {
            var def = Assets.GetBuildingDef(prefabId);
            Check.NotNull(def, "no building " + prefabId);
            var go = def.Build(cell, Orientation.Neutral, null, def.DefaultElements(), 293.15f, false, GameClock.Instance.GetTime());
            Check.NotNull(go, "the game did not build " + prefabId);
            objects.Add(go);
            return go;
        }

        // The bottom cell of a free 3x3 block near the start; things are built on its middle column.
        private static int FreeSpot()
        {
            Grid.CellToXY(Arrange.HomeCell(), out var cx, out var cy);
            for (var r = 2; r <= 40; r++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    for (var dy = -r; dy <= r; dy++)
                    {
                        if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r)
                        {
                            continue;
                        }

                        if (BlockIsFree(cx + dx, cy + dy))
                        {
                            return Grid.XYToCell(cx + dx, cy + dy);
                        }
                    }
                }
            }

            Check.Fail("no free 3x3 block near the anchor cell");
            return -1;
        }

        private static bool BlockIsFree(int x, int y)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = 0; dy <= 2; dy++)
                {
                    var cell = Grid.XYToCell(x + dx, y + dy);
                    if (!Grid.IsValidCell(cell) || !Grid.IsVisible(cell) || Grid.Solid[cell]
                        || Grid.Objects[cell, (int)ObjectLayer.Building] != null || Grid.Objects[cell, (int)ObjectLayer.Plants] != null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    // The census button has no hotkey (Action.NumActions). Vanilla's ToolMenu.OnKeyDown used to throw an IndexOutOfRangeException
    // for it on every key press (fixed by KButtonEventIsActionPatch, 2026-11-13). The press goes through the game's own input path.
    [HarnessTest(Name = "plantcensus.key_press_does_not_throw", Tags = "plantcensus")]
    public static IEnumerator KeyPressDoesNotThrow()
    {
        yield return Wait.Ticks(2);
        Play.ClearTool();
        Play.Press(KKeyCode.Escape);
        Play.Press(Action.Dig); // a tool hotkey: ToolMenu compares every tool's hotkey, ours included
        Play.ClearTool();
        Play.Press(KKeyCode.Escape);
    }
}
#endif
