using System;
using System.Linq;
using System.Collections.Generic;
using MelonLoader;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Extensions;
using Il2Cpp;
using Il2CppAssets.Scripts.Models.Towers.Behaviors;
using Il2CppAssets.Scripts.Simulation.Towers;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using UnityEngine.InputSystem;
using PlacementHelper;
using Il2CppSystem.IO;
using Il2CppAssets.Scripts.Simulation.Objects;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Towers;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppGeom;
using Vector3Boxed = Il2CppAssets.Scripts.Simulation.SMath.Vector3Boxed;
using UnityEngine.InputSystem.Utilities;

[assembly: MelonInfo(typeof(PlacementHelper.PlacementHelper), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace PlacementHelper;

public class PlacementHelper : BloonsTD6Mod
{
    private bool isInGame;
    
    private Vector2? savedPosition;
    private string savedTowerId = "";
    private readonly List<Tower> highlightedTowers = new();
    private readonly List<Tower> sacrificers = new();
    private readonly List<Tower> highlightedSacrificers = new();
    private readonly List<Tower> highlightedSacrifices = new();

    public override void OnApplicationStart()
    {
        ModHelper.Msg<PlacementHelper>("PlacementHelper loaded!");
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        if (!isInGame || InGame.instance?.InputManagers?.FirstOrDefault() == null) return;
        
        var inputManager = InGame.instance.InputManagers.First();
        
        if (!inputManager.inPlacementMode || inputManager.towerModel == null)
        {
            UnHilightTowers(highlightedTowers);
            UnHilightTowers(highlightedSacrificers);
            UnHilightTowers(highlightedSacrifices);
            ResetSavedValues();
            return;
        }
        
        if (Settings.HighlightSacrifices)
        {
            HandleSacrifices();
        }
        
        if (inputManager.inInstaMode || inputManager.inPowerMode || inputManager.placementModel.baseId != savedTowerId)
        {
            UnHilightTowers(highlightedTowers);
            ResetSavedValues();
        }
        
        if (PopupScreen.instance.IsPopupActive()) return;
        
        if (Settings.PlaceTowerHotkey.JustPressed())
        {
            if (savedPosition is not null)
            {
                InGame.instance.bridge.CreateTowerAt(inputManager.InputId, new Vector2(savedPosition.Value.x, savedPosition.Value.y), inputManager.placementModel, new Il2CppAssets.Scripts.ObjectId(), false, null);
                ResetSavedValues();
                RefreshShop();
            } else
            {
                inputManager.TryPlace();
            }
        }
        
        var direction = GetDirectionInput();
        if (direction != Vector2.zero)
        {
            if (Settings.NudgeModifierHotkey.IsPressed() ^ Settings.invertNudgeModifier)
            {
                HandleNudgeInput(direction);
            }
            else
            {
                HandleSnapInDirection(direction);
            }
            return;
        }
        
        if (Settings.SnapToClosestHotkey.JustPressed())
        {
            HandleSnapToClosest();
            return;
        }
        
        if (inputManager.inInstaMode || inputManager.inPowerMode) return;
        
        if (Settings.SqueezeHotkey.JustPressed())
        {
            HandleSqueezeInput();
            return;
        }

        if (Settings.AxisAlignHotkey.JustPressed())
        {
            HandleAxisAlignInput();
            return;
        }

        if (Settings.RotateClockwiseHotkey.JustPressed())
        {
            HandleRotateInput(clockwise: true);
            return;
        }
        
        if (Settings.RotateAnticlockwiseHotkey.JustPressed())
        {
            HandleRotateInput(clockwise: false);
        }
    }

    private void HandleSacrifices()
    {
        var inputManager = InGame.instance.InputManagers.First();

        var position = inputManager.EntityPositionWorld;
        foreach (var sacrificer in sacrificers)
        {
            if (Distance(position.x, position.y, sacrificer.Position.X, sacrificer.Position.Y) < sacrificer.towerModel.range)
            {
                sacrificer.Hilight();
                highlightedSacrificers.Add(sacrificer);
            }
            else
            {
                sacrificer.UnHighlight();
                highlightedSacrificers.Remove(sacrificer);
            }
        }

        if (inputManager.towerModel.baseId == "SuperMonkey")
        {
            foreach (var tower in InGame.instance.GetTowers())
            {
                if (Distance(position.x, position.y, tower.Position.X, tower.Position.Y) < inputManager.towerModel.range)
                {
                    highlightedSacrifices.Add(tower);
                    tower.Hilight();
                }
                else
                {
                    highlightedSacrifices.Remove(tower);
                    tower.UnHighlight();
                }
            }
        }
    }

    private static Vector2 GetDirectionInput()
    {
        if (Settings.UpHotkey.JustPressed()) return Vector2.up;
        if (Settings.DownHotkey.JustPressed()) return Vector2.down;
        if (Settings.LeftHotkey.JustPressed()) return Vector2.left;
        if (Settings.RightHotkey.JustPressed()) return Vector2.right;
        return Vector2.zero;
    }

    private static void HandleNudgeInput(Vector2 direction)
    {
        Mouse.current.WarpCursorPosition(InputSystemController.MousePosition + direction);
    }

    private static void HandleSnapInDirection(Vector2 direction)
    {
        var currentPos = InputSystemController.MousePosition;
        var canPlace = CanPlaceAtMouse(currentPos);

        float maxX = Screen.width;
        float maxY = Screen.height;

        while (CanPlaceAtMouse(currentPos) == canPlace)
        {
            var nextPos = currentPos + direction;

            if (nextPos.x < 0 || nextPos.x >= maxX || nextPos.y < 0 || nextPos.y >= maxY)
            {
                return;
            }

            currentPos = nextPos;
        }

        if (canPlace)
        {
            currentPos -= direction;
        }

        Mouse.current.WarpCursorPosition(currentPos);
    }

    private void HandleSnapToClosest()
    {
        var currentPos = InputSystemController.MousePosition;

        if (CanPlaceAtMouse(currentPos))
            return;

        var i = 0;
        var searchPos = currentPos;

        while (!CanPlaceAtMouse(searchPos))
        {
            searchPos = currentPos + Vector2.up.Rotate(i * 10) * i / 10;

            searchPos = new Vector2((int)searchPos.x, (int)searchPos.y);

            if (i++ > 20000) return;
        }

        Mouse.current.WarpCursorPosition(searchPos);
    }

    private void HandleSqueezeInput()
    {
        ResetSavedValues();
        UnHilightTowers(highlightedTowers);

        var inputManager = InGame.instance.InputManagers.First();
        var placementModel = inputManager.placementModel;
        var position = inputManager.EntityPositionWorld;

        var closestTowers = InGame.instance.GetTowerManager().GetClosestTowers(
            new Vector3Boxed(position.x, position.y, 0f), 2).ToArray();

        if (closestTowers.Length != 2)
        {
            ModHelper.Msg<PlacementHelper>("Could not find 2 towers to squeeze in between");
            return;
        }

        if (closestTowers.Any(t => !t.towerModel.footprint.Is<CircleFootprintModel>()))
        {
            ModHelper.Msg<PlacementHelper>($"Two closest towers found {closestTowers[0].towerModel.baseId}, {closestTowers[1].towerModel.baseId} were not both circular");
            return;
        }

        var squeezePos = FindClosestTangentCircle(position, placementModel.radius, closestTowers);
        if (CanPlaceAtWorld(squeezePos))
        {
            savedPosition = squeezePos;
            savedTowerId = placementModel.baseId;

            foreach (var tower in closestTowers)
            {
                tower.Hilight();
                highlightedTowers.Add(tower);
            }

            // ModHelper.Msg<PlacementHelper>("Placement found");
        }
        // else ModHelper.Msg<PlacementHelper>("Couldn't find a placement");
    }

    private void HandleAxisAlignInput()
    {
        ResetSavedValues();
        UnHilightTowers(highlightedTowers);

        var inputManager = InGame.instance.InputManagers.First();
        var placementModel = inputManager.placementModel;
        Vector2 position = inputManager.EntityPositionWorld;

        var closestTowers = InGame.instance.GetTowerManager().GetClosestTowers(
            new Vector3Boxed(position.x, position.y), 1).ToArray();

        if (closestTowers.Length != 1)
        {
            ModHelper.Msg<PlacementHelper>("Couldn't find closest tower to axis align to!");
            return;
        }

        var closestTower = closestTowers[0];

        if (closestTower == null)
        {
            ModHelper.Msg<PlacementHelper>("Closest tower to axis align to was null!");
            return;
        }

        var closestPos = closestTower.Position;

        Vector2 dominantDirection = GetDominantDirection(closestPos.X, closestPos.Y, position.x, position.y);
        bool isVertical = dominantDirection == Vector2.up || dominantDirection == Vector2.down;

        if (!TryGetFootprintExtent(closestTower.towerModel.footprint, isVertical, "Axis align closest tower", out float closestExtent))
            return;

        if (!TryGetFootprintExtent(placementModel.footprint, isVertical, "Axis align placement tower", out float placementExtent))
            return;

        float distance = closestExtent + placementExtent;
        Vector2 newPosition = new Vector2(closestPos.X, closestPos.Y) + dominantDirection * distance;

        if (CanPlaceAtWorld(newPosition))
        {
            savedPosition = newPosition;
            savedTowerId = placementModel.baseId;

            closestTower.Hilight();
            highlightedTowers.Add(closestTower);
        }
    }

    private void HandleRotateInput(bool clockwise)
    {
        ResetSavedValues();
        UnHilightTowers(highlightedTowers);

        var inputManager = InGame.instance.InputManagers.First();
        var placementModel = inputManager.placementModel;
        Vector2 position = inputManager.EntityPositionWorld;

        if (!placementModel.footprint.Is<CircleFootprintModel>())
        {
            ModHelper.Msg<PlacementHelper>("Placement tower is not circular");
            return;
        }

        var closestTowers = InGame.instance.GetTowerManager().GetClosestTowers(
            new Vector3Boxed(position.x, position.y, 0f), 1).ToArray();

        if (closestTowers == null || closestTowers.Length == 0)
        {
            ModHelper.Msg<PlacementHelper>("No towers found nearby");
            return;
        }

        var closestTower = closestTowers[0];

        if (!closestTower.towerModel.footprint.Is<CircleFootprintModel>())
        {
            ModHelper.Msg<PlacementHelper>($"Closest tower ({closestTower.towerModel.baseId}) is not circular");
            return;
        }

        float placementRadius = placementModel.radius;
        float centerX = closestTower.Position.X;
        float centerY = closestTower.Position.Y;
        float centerRadius = closestTower.towerModel.radius;

        float tangentDistance = centerRadius + placementRadius + 0.0001f;
        float currentAngle = (float)Math.Atan2(position.y - centerY, position.x - centerX);
        float rotationAmount = (float)(2 * Math.PI / Settings.RotatePrecision);

        if (!clockwise) rotationAmount *= -1;

        Vector2 initialPos = new Vector2(
            centerX + tangentDistance * (float)Math.Cos(currentAngle),
            centerY + tangentDistance * (float)Math.Sin(currentAngle)
        );
        bool initialCanPlace = CanPlaceAtWorld(initialPos);

        float testAngle = currentAngle;
        Vector2 testPosition = initialPos;
        int attempts = 0;

        while (attempts++ <= Settings.RotatePrecision)
        {
            testAngle += rotationAmount;

            testPosition = new Vector2(
                centerX + tangentDistance * (float)Math.Cos(testAngle),
                centerY + tangentDistance * (float)Math.Sin(testAngle)
            );

            if (CanPlaceAtWorld(testPosition) != initialCanPlace)
            {
                if (initialCanPlace)
                {
                    testAngle -= rotationAmount;
                    testPosition = new Vector2(
                        centerX + tangentDistance * (float)Math.Cos(testAngle),
                        centerY + tangentDistance * (float)Math.Sin(testAngle)
                    );
                }
                break;
            }
        }

        if (CanPlaceAtWorld(testPosition))
        {
            savedPosition = testPosition;
            savedTowerId = placementModel.baseId;

            closestTower.Hilight();
            highlightedTowers.Add(closestTower);
        }
    }

    private static Vector2 FindClosestTangentCircle(Vector2 position, float radius, Tower[] towers)
    {
        float x = position.x, y = position.y;
        float r = radius + 0.0001f;

        float x1 = towers[0].Position.X, y1 = towers[0].Position.Y, r1 = towers[0].towerModel.radius;
        float x2 = towers[1].Position.X, y2 = towers[1].Position.Y, r2 = towers[1].towerModel.radius;

        float A = r + r1, B = (float)Distance(x1, y1, x2, y2), C = r + r2;

        if (B > A + C)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            float ux = dx / distance, uy = dy / distance;

            Vector2 edge1 = new Vector2(x1 + r1 * ux, y1 + r1 * uy);
            Vector2 edge2 = new Vector2(x2 - r2 * ux, y2 - r2 * uy);

            return new Vector2((edge1.x + edge2.x) / 2, (edge1.y + edge2.y) / 2);
        }

        double alpha = Math.Atan2(y2 - y1, x2 - x1);
        double beta = Math.Acos((A * A + B * B - C * C) / (2 * A * B));

        Vector2 pos1 = new((float)(x1 + A * Math.Cos(alpha - beta)), (float)(y1 + A * Math.Sin(alpha - beta)));
        Vector2 pos2 = new((float)(x1 + A * Math.Cos(alpha + beta)), (float)(y1 + A * Math.Sin(alpha + beta)));

        return Distance(x, y, pos1.x, pos1.y) < Distance(x, y, pos2.x, pos2.y) ? pos1 : pos2;
    }

    private static double Distance(float x0, float y0, float x1, float y1)
    {
        return Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
    }

    private static bool CanPlaceAtMouse(Vector2 mousePosition)
    {
        return CanPlaceAtWorld(InGame.instance.GetWorldFromPointer(mousePosition));
    }

    private static bool CanPlaceAtWorld(Vector2 worldPosition)
    {
        if (InGame.instance == null)
            return false;

        var inputManager = InGame.instance.InputManagers.First();
        if (inputManager == null)
            return false;

        var bridge = InGame.instance.bridge;
        if (bridge == null)
            return false;

        if (inputManager.placementModel == null)
            return false;

        return bridge.CanPlaceTowerAt(worldPosition, inputManager.placementModel, inputManager.InputId, inputManager.placementEntityId);
    }

    private void ResetSavedValues()
    {
        savedPosition = null;
        savedTowerId = "";
    }

    private static void UnHilightTowers(List<Tower> towers)
    {
        foreach (var tower in towers)
        {
            if (tower is { isDestroyed: false })
                tower.UnHighlight();
        }
        towers.Clear();
    }

    private static void RefreshShop()
    {
        // ShopMenu.instance.RebuildTowerSet();
        // foreach (var button in ShopMenu.instance.ActiveTowerButtons)
        // {
        //     button.Cast<TowerPurchaseButton>().Update();
        // }
    }

    private static bool isSacrificer(Tower tower)
    {
        var model = tower.towerModel;
        var tiers = model.tiers;
        return model.baseId == "SuperMonkey" && ((tiers[1] < 3 && tiers[2] == 0) || (tiers[1] == 0 && tiers[2] < 3) && tiers[0] < 5);
    }

    public override void OnMatchStart()
    {
        base.OnMatchStart();

        sacrificers.Clear();
        foreach (var tower in InGame.instance.GetTowers())
        {
            var model = tower.towerModel;
            if (model.baseId == "SuperMonkey" && model.tiers[1] < 3 && model.tiers[2] < 3)
            {
                sacrificers.Add(tower);
            }
        }

        isInGame = true;
    }

    public override void OnMatchEnd()
    {
        isInGame = false;
        
        base.OnMatchEnd();
    }

    public override void OnTowerDestroyed(Tower tower)
    {
        base.OnTowerDestroyed(tower);

        sacrificers.Remove(tower);
        highlightedSacrifices.Remove(tower);
    }

    public override void OnTowerCreated(Tower tower, Entity target, Model modelToUse)
    {
        base.OnTowerCreated(tower, target, modelToUse);

        if (isSacrificer(tower))
        {
            sacrificers.Add(tower);
        }
    }

    public override void OnTowerUpgraded(Tower tower, string upgradeName, TowerModel newBaseTowerModel)
    {
        base.OnTowerUpgraded(tower, upgradeName, newBaseTowerModel);

        if (tower.towerModel.baseId == "SuperMonkey" && sacrificers.Contains(tower) && !isSacrificer(tower))
        {
            sacrificers.Remove(tower);
        }
    }

    public override void OnTowerSelected(Tower tower)
    {
        base.OnTowerSelected(tower);

        // ModHelper.Msg<PlacementHelper>($"({tower.Position.X}, {tower.Position.Y}, {tower.Position.Z})");
    }

    /// Returns the cardinal direction (up/down/left/right) from point 0 to point 1.
    private static Vector2 GetDominantDirection(float x0, float y0, float x1, float y1)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;

        // Pick whichever axis has the larger displacement
        if (Mathf.Abs(dx) >= Mathf.Abs(dy))
        {
            return dx >= 0 ? Vector2.right : Vector2.left;
        }
        else
        {
            return dy >= 0 ? Vector2.up : Vector2.down;
        }
    }

    /// <summary>
    /// Gets the relevant extent (half-width along the axis, or radius) of a tower's footprint,
    /// for use in axis-aligned distance calculations.
    /// </summary>
    private bool TryGetFootprintExtent(FootprintModel footprint, bool isVertical, string context, out float extent)
    {
        if (footprint.Is<RectangleFootprintModel>())
        {
            var rect = footprint.As<RectangleFootprintModel>();
            extent = (isVertical ? rect.yWidth : rect.xWidth) / 2f;
            // ModHelper.Msg<PlacementHelper>($"{extent} extent found");
            return true;
        }

        if (footprint.Is<CircleFootprintModel>())
        {
            var circle = footprint.As<CircleFootprintModel>();
            extent = circle.radius;
            // ModHelper.Msg<PlacementHelper>($"{extent} extent found");
            return true;
        }

        extent = 0f;
        ModHelper.Msg<PlacementHelper>($"{context} had unknown footprint model!");
        return false;
    }
}