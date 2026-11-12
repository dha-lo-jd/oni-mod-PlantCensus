using System.Collections.Generic;
using Klei.AI;
using UnityEngine;

namespace PlantCensus;

// Finds every plant rooted in the selected cells and snapshots its growth. A tree is one entry: its trunk, with the
// branches as children. A tree counts as selected when its trunk or any of its branches is in the area.
public static class PlantCollector
{
    private const float SecondsPerCycle = 600f;

    public static List<PlantRow> Collect(CensusSelection selection)
    {
        var plants = new List<GameObject>();
        var seen = new HashSet<GameObject>();
        foreach (var uprootable in Components.Uprootables.Items)
        {
            if (uprootable == null || uprootable.gameObject == null)
            {
                continue;
            }

            var go = uprootable.gameObject;
            var prefabId = go.GetComponent<KPrefabID>();
            if (prefabId == null || !prefabId.HasTag(GameTags.Plant))
            {
                continue;
            }

            if (!selection.Cells.Contains(Grid.PosToCell(go.transform.GetPosition())))
            {
                continue;
            }

            // A branch stands for its tree.
            var branch = go.GetSMI<PlantBranch.Instance>();
            var plant = branch != null && branch.HasTrunk ? branch.trunk.gameObject : go;
            if (plant != null && seen.Add(plant))
            {
                plants.Add(plant);
            }
        }

        var rows = new List<PlantRow>();
        foreach (var plant in plants)
        {
            var prefabId = plant.GetComponent<KPrefabID>();
            if (prefabId == null)
            {
                continue;
            }

            var row = BuildRow(plant, prefabId);
            AddBranches(row, plant);
            rows.Add(row);
        }

        return rows;
    }

    private static void AddBranches(PlantRow row, GameObject trunk)
    {
        var grower = trunk.GetSMI<PlantBranchGrower.Instance>();
        if (grower == null)
        {
            return;
        }

        foreach (var branch in grower.GetExistingBranches())
        {
            var prefabId = branch != null ? branch.GetComponent<KPrefabID>() : null;
            if (prefabId == null)
            {
                continue;
            }

            // A branch belongs to its tree's species.
            var child = BuildRow(branch, prefabId);
            child.IsBranch = true;
            child.SpeciesKey = row.SpeciesKey;
            child.SpeciesName = row.SpeciesName;
            row.Children.Add(child);
        }

        if (row.Children.Count > 0)
        {
            Summarize(row);
        }
    }

    // Makes the trunk's row speak for the whole tree: what the trunk has no value of comes from the branches.
    public static void Summarize(PlantRow row)
    {
        var children = row.Children;
        row.ReadyBranches = 0;
        var anyStalled = row.State == PlantState.Stalled;
        var anyGrowing = row.State == PlantState.Growing;
        foreach (var child in children)
        {
            if (child.State == PlantState.Ready)
            {
                row.ReadyBranches++;
            }

            anyStalled |= child.State == PlantState.Stalled;
            anyGrowing |= child.State == PlantState.Growing;
        }

        row.State = row.ReadyBranches > 0 || row.State == PlantState.Ready ? PlantState.Ready
            : anyStalled ? PlantState.Stalled
            : anyGrowing ? PlantState.Growing
            : PlantState.Idle;

        // A full-grown trunk shows how far its branches are.
        if (row.Growth < 0f || row.Growth >= 1f)
        {
            var sum = 0f;
            var count = 0;
            foreach (var child in children)
            {
                if (child.Growth >= 0f)
                {
                    sum += child.Growth;
                    count++;
                }
            }

            if (count > 0)
            {
                row.Growth = sum / count;
                row.GrowthIsAverage = true;
                row.GrowthTotalCycles = -1f;
                row.GrowthRemainingCycles = -1f;
            }
        }

        if (row.CropName == null)
        {
            foreach (var child in children)
            {
                if (child.CropName == null)
                {
                    continue;
                }

                if (row.CropName == null)
                {
                    row.CropName = child.CropName;
                    row.CropAmount = child.CropAmount;
                }

                row.CropCount++;
            }
        }

        if (!row.HasHarvestSetting)
        {
            foreach (var child in children)
            {
                if (child.HasHarvestSetting)
                {
                    row.HasHarvestSetting = true;
                    row.AutoHarvest = child.AutoHarvest;
                    break;
                }
            }
        }

        var problems = new List<string>(row.Problems);
        foreach (var child in children)
        {
            foreach (var problem in child.Problems)
            {
                if (!problems.Contains(problem))
                {
                    problems.Add(problem);
                }
            }
        }

        row.Problems = problems.ToArray();
    }

    public static PlantRow BuildRow(GameObject go, KPrefabID prefabId)
    {
        var receptacle = go.GetComponent<ReceptacleMonitor>();
        var row = new PlantRow
        {
            Name = Texts.StripLinks(go.GetProperName()),
            IsWild = receptacle == null || !receptacle.Replanted,
            Target = go.GetComponent<KSelectable>(),
            Position = go.transform.GetPosition(),
        };

        SetSpecies(row, go, prefabId);
        SetGrowth(row, go);
        SetCrop(row, go);
        SetTemperature(row, go);

        var designatable = go.GetComponent<HarvestDesignatable>();
        if (designatable != null)
        {
            row.HasHarvestSetting = true;
            row.AutoHarvest = designatable.HarvestWhenReady;
        }

        var wilt = go.GetComponent<WiltCondition>();
        row.Problems = Problems(row.Target, wilt);

        var harvestable = go.GetComponent<Harvestable>();
        if (harvestable != null && harvestable.CanBeHarvested)
        {
            row.State = PlantState.Ready;
        }
        else if (wilt != null && wilt.IsWilting())
        {
            row.State = PlantState.Stalled;
        }
        else
        {
            // A plant that is full-grown but has nothing to harvest (a tree trunk) is done growing.
            row.State = row.Growth >= 0f && row.Growth < 1f ? PlantState.Growing : PlantState.Idle;
        }

        var icon = Def.GetUISprite(go);
        if (icon != null)
        {
            row.Icon = icon.first;
            row.IconColor = icon.second;
        }

        return row;
    }

    // A mutant is grouped under the species it mutated from.
    private static void SetSpecies(PlantRow row, GameObject go, KPrefabID prefabId)
    {
        var species = prefabId.PrefabTag;
        var mutant = go.GetComponent<MutantPlant>();
        if (mutant != null && mutant.SpeciesID.IsValid)
        {
            species = mutant.SpeciesID;
        }

        var prefab = Assets.GetPrefab(species);
        row.SpeciesKey = species.ToString();
        row.SpeciesName = Texts.StripLinks(prefab != null ? prefab.GetProperName() : species.ProperName());
    }

    // Growth comes from whatever manages the plant's growing states: the Growing component of an ordinary crop, or
    // the state machine of a tree branch.
    private static void SetGrowth(PlantRow row, GameObject go)
    {
        var manager = go.GetComponent<IManageGrowingStates>() ?? go.GetSMI<IManageGrowingStates>();
        if (manager == null)
        {
            return;
        }

        row.Growth = Mathf.Clamp01(manager.PercentGrown());

        var maturity = Db.Get().Amounts.Maturity.Lookup(go);
        var rate = maturity != null ? maturity.GetDelta() : 0f;
        if (rate > 0f)
        {
            row.GrowthTotalCycles = maturity.GetMax() / rate / SecondsPerCycle;
            row.GrowthRemainingCycles = Mathf.Max(0f, maturity.GetMax() - maturity.value) / rate / SecondsPerCycle;
        }
    }

    // The harvested item and one harvest's amount, formatted the way the game's planter side screen does.
    private static void SetCrop(PlantRow row, GameObject go)
    {
        var crop = go.GetComponent<Crop>();
        if (crop == null || string.IsNullOrEmpty(crop.cropId))
        {
            return;
        }

        var tag = new Tag(crop.cropId);
        var prefab = Assets.GetPrefab(tag);
        if (prefab == null)
        {
            return;
        }

        var yield = Db.Get().PlantAttributes.YieldAmount.Lookup(go);
        var amount = yield != null ? yield.GetTotalValue() : crop.cropVal.numProduced;
        var edible = prefab.GetComponent<Edible>();
        row.CropName = Texts.StripLinks(prefab.GetProperName());
        if (edible != null)
        {
            row.CropAmount = GameUtil.GetFormattedCalories(edible.FoodInfo.CaloriesPerUnit * amount);
        }
        else if (GameTags.DisplayAsUnits.Contains(tag))
        {
            row.CropAmount = GameUtil.GetFormattedUnits(amount, GameUtil.TimeSlice.None, displaySuffix: false);
        }
        else
        {
            row.CropAmount = GameUtil.GetFormattedMass(amount);
        }
    }

    private static void SetTemperature(PlantRow row, GameObject go)
    {
        var vulnerable = go.GetComponent<TemperatureVulnerable>();
        if (vulnerable == null)
        {
            var element = go.GetComponent<PrimaryElement>();
            if (element != null)
            {
                row.Temperature = element.Temperature;
            }

            return;
        }

        row.Temperature = vulnerable.InternalTemperature;
        switch (vulnerable.GetInternalTemperatureState)
        {
            case TemperatureVulnerable.TemperatureState.LethalHot:
                row.TemperatureBand = TemperatureBand.HotDeadly;
                break;
            case TemperatureVulnerable.TemperatureState.WarningHot:
                row.TemperatureBand = TemperatureBand.Hot;
                break;
            case TemperatureVulnerable.TemperatureState.LethalCold:
                row.TemperatureBand = TemperatureBand.ColdDeadly;
                break;
            case TemperatureVulnerable.TemperatureState.WarningCold:
                row.TemperatureBand = TemperatureBand.Cold;
                break;
            default:
                row.TemperatureBand = TemperatureBand.Comfortable;
                break;
        }
    }

    // The wilt causes as the game words them in the plant's own status, then any other status the game flags as bad.
    private static string[] Problems(KSelectable selectable, WiltCondition wilt)
    {
        var problems = new List<string>();
        if (wilt != null)
        {
            foreach (var line in wilt.WiltCausesString().Split('\n'))
            {
                Add(problems, line);
            }
        }

        string wilting = null;
        var group = selectable != null ? selectable.GetStatusItemGroup() : null;
        if (group != null)
        {
            var statuses = Db.Get().CreatureStatusItems;
            foreach (var entry in group)
            {
                if (entry.item == null)
                {
                    continue;
                }

                // The wilting statuses only say "not growing" and list the causes already taken above.
                if (entry.item == statuses.Wilting || entry.item == statuses.WiltingDomestic
                    || entry.item == statuses.WiltingNonGrowing || entry.item == statuses.WiltingNonGrowingDomestic)
                {
                    wilting = entry.GetName();
                    continue;
                }

                if (entry.item.notificationType == NotificationType.Bad)
                {
                    Add(problems, entry.GetName());
                }
            }
        }

        // A wilting plant whose causes have no text of their own still shows that it is wilting.
        if (problems.Count == 0 && wilting != null)
        {
            Add(problems, wilting);
        }

        return problems.ToArray();
    }

    // Wilt causes come as bulleted, indented lines, some with link markup.
    private static void Add(List<string> problems, string text)
    {
        var clean = Texts.StripLinks(text ?? "").Trim().TrimStart('•', '·', '-', '*', '?').Trim();
        if (clean.Length > 0 && !problems.Contains(clean))
        {
            problems.Add(clean);
        }
    }
}
