namespace PlacementHelper;

public static class ModHelperData
{
    public const string WorksOnVersion = "47.0";
    public const string Version = "0.0.8";
    public const string Name = "PlacementHelper";

    public const string Description =
    @"PlacementHelper is a utility mod that enhances tower placement precision with advanced controls.

Features:
- Use the Place Tower Hotkey for normal placement as well as subpixel squeezing and rotating placements.
- Snap the mouse to the closest valid placement.
- Snap or move pixel by pixel in a given direction.

- Squeeze towers between two nearby circular towers:
    * If a valid spot is found, the two towers are highlighted.
    * Press the Place Tower Hotkey to confirm the placement.

- Rotate the held tower around a nearby circular tower:
    * Hold the tower close to another tower.
    * Press a rotate hotkey (clockwise or counterclockwise).
    * If the tower is initially placeable, it will rotate up until it no longer is.
    * If not initially placeable, it will rotate until it becomes placeable.
    * Press the Place Tower Hotkey to confirm the placement.

- Highlights Super Monkeys that can sacrifice the held tower.
- Highlights towers that can be sacrificed by the held Super Monkey.

Note: Subpixel features (squeeze and rotate) do not currently work with Instamonkeys or Powers.";

    public const string RepoOwner = "iXendeRouS";
    public const string RepoName = "PlacementHelper";
}
