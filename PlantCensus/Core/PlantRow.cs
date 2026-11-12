using UnityEngine;

namespace PlantCensus;

// Listed in the order the state filter shows them.
public enum PlantState
{
    Ready,
    Growing,
    Stalled,
    Idle
}

public enum TemperatureBand
{
    Unknown,
    Comfortable,
    Hot,
    HotDeadly,
    Cold,
    ColdDeadly
}

// Snapshot of one plant taken when the census opens.
public sealed class PlantRow
{
    public PlantState State;

    // Species the plant belongs to (a mutant counts as its original species) and the plant's own name.
    public string SpeciesKey;
    public string SpeciesName;
    public string Name;

    public bool IsWild;

    // Growth toward the next harvest 0..1 (negative when the plant does not grow), and the cycles a full growth
    // takes / has left at the current speed (negative while it isn't growing).
    public float Growth = -1f;
    public float GrowthTotalCycles = -1f;
    public float GrowthRemainingCycles = -1f;

    // Harvested item and the formatted amount of one harvest; null when the plant yields nothing.
    public string CropName;
    public string CropAmount;

    // Whether the plant has a harvest setting at all, and whether it is harvested automatically when ready.
    public bool HasHarvestSetting;
    public bool AutoHarvest;

    // Plant temperature in kelvin (negative when unknown) and where it sits against the plant's limits.
    public float Temperature = -1f;
    public TemperatureBand TemperatureBand;

    // What keeps the plant from growing, one entry per cause; empty when nothing does.
    public string[] Problems = new string[0];

    public Sprite Icon;
    public Color IconColor = Color.white;

    // What a click selects, and where the camera goes.
    public KSelectable Target;
    public Vector3 Position;
}
