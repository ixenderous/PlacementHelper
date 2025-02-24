using System;
using System.Linq;
using MelonLoader;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Extensions;
using Il2Cpp;
using Il2CppSystem.IO;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Simulation.Objects;
using Il2CppAssets.Scripts.Simulation.Towers;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using UnityEngine.InputSystem;
using PlacementHelper;
using Il2CppAssets.Scripts.Models.Towers.Behaviors;
using System.Collections.Generic;

[assembly: MelonInfo(typeof(PlacementHelper.PlacementHelper), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace PlacementHelper;

public class PlacementHelper : BloonsTD6Mod
{
    private Vector2? savedPosition;
    private string savedTowerId = "";
    private int framesSincePlacementMode = 0;
    private List<Tower> highlightedTowers = new();

    public override void OnApplicationStart()
    {
        ModHelper.Msg<PlacementHelper>("PlacementHelper loaded!");
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        if (InGame.instance?.bridge == null) return;

        if (!InGame.instance.InputManager.IsInPlacementMode)
        {
            UnHilightTowers();

            if (savedPosition != null && ++framesSincePlacementMode > 6)
            {
                ResetSavedValues();
            }
            return;
        }
      
        framesSincePlacementMode = 0;

        HandleSqueezeInput();
        if (Settings.NudgeModifierHotkey.IsPressed())
        {
            if (Settings.invertNudgeModifier) HandleSnapInput();
            else HandleNudgeInput();
        }
        else HandleSnapInput();
    }

    private static void HandleNudgeInput()
    {
        Vector2 direction = GetDirectionInput();
        if (direction != Vector2.zero)
        {
            Mouse.current.WarpCursorPosition(InputSystemController.MousePosition + direction);
        }
    }

    private static void HandleSnapInput()
    {
        Vector2 direction = GetDirectionInput();
        if (direction != Vector2.zero) SnapInDirection(direction);
    }

    private static Vector2 GetDirectionInput()
    {
        if (Settings.UpHotkey.JustPressed()) return Vector2.up;
        if (Settings.DownHotkey.JustPressed()) return Vector2.down;
        if (Settings.LeftHotkey.JustPressed()) return Vector2.left;
        if (Settings.RightHotkey.JustPressed()) return Vector2.right;
        return Vector2.zero;
    }

    private static void SnapInDirection(Vector2 direction)
    {
        var currentPos = InputSystemController.MousePosition;
        bool canPlace = CanPlace(currentPos);
        int attempts = 0;

        while (CanPlace(currentPos) == canPlace && attempts++ < 4000)
        {
            currentPos += direction;
        }

        if (canPlace) currentPos -= direction;
        if (attempts < 4000) Mouse.current.WarpCursorPosition(currentPos);
    }

    private void HandleSqueezeInput()
    {
        if (!Settings.SqueezeHotkey.JustPressed()) return;

        ResetSavedValues();
        UnHilightTowers();

        var inputManager = InGame.instance.InputManager;
        var placementModel = inputManager.placementModel;
        Vector2 position = inputManager.EntityPositionWorld;

        var closestTowers = InGame.instance.GetTowerManager().GetClosestTowers(new Il2CppAssets.Scripts.Simulation.SMath.Vector3Boxed(position.x, position.y, 0f), 2).ToArray(); ;

        if (closestTowers.Length != 2)
        {
            MelonLogger.Msg("Could not find 2 towers to squeeze inbetween");
            return;
        }

        if (closestTowers.Any(t => !t.towerModel.footprint.Is<CircleFootprintModel>()))
        {
            MelonLogger.Msg($"Two closest towers found {closestTowers[0].towerModel.baseId}, {closestTowers[1].towerModel.baseId} were not both circular");
            return;
        }

        var squeezePos = FindClosestTangentCircle(position, placementModel.radius, closestTowers);
        if (InGame.instance.bridge.CanPlaceTowerAt(squeezePos, placementModel, InGame.instance.bridge.MyPlayerNumber, inputManager.placementEntityId))
        {
            savedPosition = squeezePos;
            savedTowerId = placementModel.baseId;

            foreach (var tower in closestTowers)
            {
                tower.Hilight();
                highlightedTowers.Add(tower);
            }

            MelonLogger.Msg("Placement found");
        }
        else MelonLogger.Msg("Couldn't find a placement");
    }

    private static Vector2 FindClosestTangentCircle(Vector2 position, float radius, Tower[] towers)
    {
        return FindClosestTangentCircle(position.x, position.y, radius + 0.0001f,
            towers[0].Position.X, towers[0].Position.Y, towers[0].towerModel.radius,
            towers[1].Position.X, towers[1].Position.Y, towers[1].towerModel.radius);
    }

    private static Vector2 FindClosestTangentCircle(float x, float y, float r, float x1, float y1, float r1, float x2, float y2, float r2)
    {
        float A = r + r1, B = (float)Distance(x1, y1, x2, y2), C = r + r2;
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

    private static bool CanPlace(Vector2 position)
    {
        var inputManager = InGame.instance.InputManager;
        var bridge = InGame.instance.bridge;
        return bridge.CanPlaceTowerAt(InGame.instance.GetWorldFromPointer(position), inputManager.placementModel, bridge.MyPlayerNumber, inputManager.placementEntityId);
    }

    private void ResetSavedValues()
    {
        savedPosition = null;
        savedTowerId = "";
    }

    public override void OnTowerCreated(Tower tower, Entity target, Model modelToUse)
    {
        base.OnTowerCreated(tower, target, modelToUse);

        if (savedTowerId == tower.towerModel.baseId && savedPosition.HasValue)
        {
            tower.PositionTower(new(savedPosition.Value.x, savedPosition.Value.y));
        }
        ResetSavedValues();
    }

    private void UnHilightTowers()
    {
        if (highlightedTowers.Count == 0) return;
        
        foreach (var tower in highlightedTowers)
        {
            if (tower != null && !tower.isDestroyed)
            tower.UnHighlight();
        }
        highlightedTowers.Clear();
    }
}
