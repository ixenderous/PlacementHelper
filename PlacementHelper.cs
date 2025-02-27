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
using Il2CppAssets.Scripts.Models.Map;
using Il2CppAssets.Scripts.Unity;

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

    public override void OnNewGameModel(GameModel result, MapModel map)
    {
        base.OnNewGameModel(result, map);
    }

    public override void OnInGameLoaded(InGame inGame)
    {
        base.OnInGameLoaded(inGame);

        // InGame.instance.sceneCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
        // InGame.instance.sceneCamera.orthographicSize = 125;
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

        if (Settings.SqueezeHotkey.JustPressed())
        {
            HandleSqueezeInput();
            return;
        }

        // Add these new checks
        if (Settings.RotateClockwiseHotkey.JustPressed())
        {
            MelonLogger.Msg("Settings.RotateClockwiseHotkey.JustPressed()");
            HandleRotateInput(clockwise: true);
            return;
        }

        if (Settings.RotateAnticlockwiseHotkey.JustPressed())
        {
            MelonLogger.Msg("Settings.RotateAnticlockwiseHotkey.JustPressed()");
            HandleRotateInput(clockwise: false);
            return;
        }

        // Rest of your existing code...
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
        bool canPlace = CanPlaceAtMouse(currentPos);
        int attempts = 0;

        while (CanPlaceAtMouse(currentPos) == canPlace && attempts++ < 4000)
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
        if (CanPlaceAtWorld(squeezePos))
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
        float x = position.x, y = position.y;
        float r = radius + 0.0001f;

        float x1 = towers[0].Position.X, y1 = towers[0].Position.Y, r1 = towers[0].towerModel.radius;
        float x2 = towers[1].Position.X, y2 = towers[1].Position.Y, r2 = towers[1].towerModel.radius;

        float A = r + r1, B = (float)Distance(x1, y1, x2, y2), C = r + r2;

        // if the two towers are too far apart to squeeze between, place the tower in the middle of the gap
        if (B > A + C)
        {
            // calculate the normalized vector from tower[0] to tower[1]
            float dx = x2 - x1, dy = y2 - y1;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            float ux = dx / distance, uy = dy / distance;

            // calculate the edges of the two towers along this vector
            Vector2 edge1 = new Vector2(x1 + r1 * ux, y1 + r1 * uy);
            Vector2 edge2 = new Vector2(x2 - r2 * ux, y2 - r2 * uy);

            // return the midpoint of the gap between the two towers
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

    private void HandleRotateInput(bool clockwise)
    {
        ResetSavedValues();
        UnHilightTowers();

        var inputManager = InGame.instance.InputManager;
        var placementModel = inputManager.placementModel;
        Vector2 position = inputManager.EntityPositionWorld;

        if (!placementModel.footprint.Is<CircleFootprintModel>())
        {
            MelonLogger.Msg("Placement tower is not circular");
            return;
        }

        var closestTowers = InGame.instance.GetTowerManager().GetClosestTowers(
            new Il2CppAssets.Scripts.Simulation.SMath.Vector3Boxed(position.x, position.y, 0f), 1).ToArray();

        if (closestTowers == null || closestTowers.Length == 0)
        {
            MelonLogger.Msg("No towers found nearby");
            return;
        }

        var closestTower = closestTowers[0];

        if (!closestTower.towerModel.footprint.Is<CircleFootprintModel>())
        {
            MelonLogger.Msg($"Closest tower ({closestTower.towerModel.baseId}) is not circular");
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

            // Mouse.current.WarpCursorPosition(WorldToScreen(testPosition));
        }
    }

    private static bool CanPlaceAtWorld(Vector2 worldPosition)
    {
        var inputManager = InGame.instance.InputManager;
        var bridge = InGame.instance.bridge;
        return bridge.CanPlaceTowerAt(worldPosition, inputManager.placementModel, bridge.MyPlayerNumber, inputManager.placementEntityId);
    }

    private static bool CanPlaceAtMouse(Vector2 mousePosition)
    {
        var inputManager = InGame.instance.InputManager;
        var bridge = InGame.instance.bridge;
        return bridge.CanPlaceTowerAt(InGame.instance.GetWorldFromPointer(mousePosition), inputManager.placementModel, bridge.MyPlayerNumber, inputManager.placementEntityId);
    }

    // WHY DOESNT THIS WORK
    //private static Vector2 WorldToScreen(Vector2 worldPosition)
    //{
    //    var pos = InGame.instance.sceneCamera.WorldToScreenPoint(worldPosition);

    //    MelonLogger.Msg($"position: {pos.x}, {pos.y}, {pos.z}");

    //    var backtovectwo = new Vector2(pos.x, Screen.height - pos.y);
    //    return backtovectwo;
    //}
}