# PlantCensus

Oxygen Not Included mod. Lists the plants in an area with their growth and what keeps them from growing.

Part of the Census series (see [Critter Census](https://github.com/dha-lo-jd/oni-mod-CritterCensus)).

## What it does

- Adds a **Plant Census** tool next to the Harvest tool. Drag over an area to list its plants.
- In the Rooms overlay, clicking a Greenhouse lists the plants of that room.
- Each row shows the plant's state (ready / growing / stalled), whether it is wild or farmed, growth and the cycles left,
  the crop and amount of one harvest, the auto-harvest setting, temperature, and the reasons it is not growing.
- A tree is one row; its branches are listed under it and can be shown or hidden.
- Filter by state and species, sort by any column, click a row to jump to the plant.

It only shows information: nothing in the colony changes. The list is a snapshot taken when the window opens.

English and Japanese are supported. The design is described in [SPEC.md](SPEC.md) (Japanese).

## Build

Copy `PlantCensus/Local.Build.props.example` to `Local.Build.props`, set the paths, then `dotnet build`.
Debug builds are copied to `ModInstallPath`; Release builds produce `release/PlantCensus`.
