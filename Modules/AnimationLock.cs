using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using static ExtremeClipping.ExtremeClipping;

namespace ExtremeClipping
{
    public partial class Configuration
    {
        public bool EnableAnimLockComp = true;
        public int SimulatedPingMs = 8;
        public bool EnableLogging = false;
        public bool EnableDryRun = false;
        public Dictionary<uint, float> AnimationLocks = new();
        public ulong TotalActionsReduced = 0ul;
        public double TotalAnimationLockReduction = 0d;
    }
}

namespace ExtremeClipping.Modules
{
    public class AnimationLock : Module
    {
        // ALL INFO BELOW IS BASED ON MY FINDINGS AND I RESERVE THE RIGHT TO HAVE MISINTERPRETED SOMETHING, THANKS
        // The typical time range that passes for the client is never equal to ping, it always seems to be at least ping + server delay
        // The server delay is usually around 40-60 ms in the overworld, but falls to 30-40 ms inside of instances
        // Additionally, your FPS will add more time because one frame MUST pass for you to receive the new animation lock
        // Therefore, most players will never receive a response within 40 ms at any ping
        // Another interesting fact is that the delay from the server will spike if you send multiple packets at the same time
        // This seems to imply that the server will not process more than one packet from you per tick
        // You can see this if you sheathe your weapon before using an action, you will notice delays that are around 50 ms higher than usual
        // This explains the phenomenon where moving seems to make it harder to weave

        // For these reasons, I do not believe it is possible to triple weave on any ping without clipping even the slightest amount as that would require 25 ms response times for a 2.5 GCD triple

        // This module simulates around 10 ms ping inside instances

        public override bool IsEnabled
        {
            get => Config.EnableAnimLockComp;
            set => Config.EnableAnimLockComp = value;
        }

        public override int DrawOrder => 1;
        public override bool HasOverview => true;

        public const int MinSimulatedPingMs = 0;
        public const int MaxSimulatedPingMs = 100;
        public const int DefaultSimulatedPingMs = 8;

        // The ping the module pretends you have, in seconds. Set from the slider in the overview.
        private static float simulatedRTT => Math.Clamp(Config.SimulatedPingMs, MinSimulatedPingMs, MaxSimulatedPingMs) / 1000f;

        private float delay = -1;
        private int packetsSent = 0;
        private bool isCasting = false;
        private float intervalPacketsTimer = 0;
        private int intervalPacketsIndex = 0;
        private readonly int[] intervalPackets = new int[5]; // Record the last 50 ms of packets
        private bool enableAnticheat = false;
        private bool saveConfig = false;
        private readonly Dictionary<ushort, float> appliedAnimationLocks = new();

        public bool IsDryRunEnabled => enableAnticheat || Config.EnableDryRun;

        private float AverageDelay(float currentDelay, float weight) =>
            delay > 0
                ? delay = delay * (1 - weight) + currentDelay * weight
                : delay = currentDelay; // Initial starting delay

        private static float GetAnimationLock(uint actionID) => (!Config.AnimationLocks.TryGetValue(actionID, out var animationLock) || animationLock < 0.5f
                ? Game.DefaultClientAnimationLock
                : animationLock)
            + simulatedRTT;

        private void UpdateDatabase(uint actionID, float animationLock)
        {
            if (Config.AnimationLocks.TryGetValue(actionID, out var oldLock) && oldLock == animationLock) return;
            Config.AnimationLocks[actionID] = animationLock;
            saveConfig = true;
            DalamudApi.LogDebug($"Recorded new animation lock value of {F2MS(animationLock)} ms for {actionID}");
        }

        private unsafe void UseActionLocation(nint actionManager, uint actionType, uint actionID, ulong targetedActorID, nint vectorLocation, uint param, byte ret)
        {
            packetsSent = intervalPackets.Sum();

            if (Game.actionManager->animationLock != Game.DefaultClientAnimationLock) return;

            var id = ActionManager.GetSpellIdForAction((ActionType)actionType, actionID);
            var animationLock = GetAnimationLock(id);
            if (!IsDryRunEnabled)
            {
                Game.actionManager->animationLock = animationLock;
                appliedAnimationLocks[Game.actionManager->currentSequence] = animationLock;
            }

            DalamudApi.LogDebug($"Applying {F2MS(animationLock)} ms animation lock for {actionType} {actionID} ({id})");
        }

        private void CastBegin(ulong objectID, nint packetData) => isCasting = true;
        private void CastInterrupt(nint actionManager) => isCasting = false;

        private unsafe void ReceiveActionEffect(uint casterEntityId, Character* casterPtr, Vector3* targetPos, ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetEntityIds, float oldLock, float newLock)
        {
            try
            {
                if (oldLock == newLock || (nint)casterPtr != DalamudApi.ObjectTable.LocalPlayer?.Address) return;

                // Ignore cast locks (caster tax, teleport, lb)
                if (isCasting)
                {
                    isCasting = false;

                    // The old lock should always be 0, but high ping can cause the packet to arrive too late and allow near instant double weaves
                    newLock += oldLock;
                    if (!IsDryRunEnabled)
                        Game.actionManager->animationLock = newLock;

                    if (Config.EnableLogging)
                        PrintLog($"Cast Lock: {F2MS(newLock)} ms (+{F2MS(oldLock)})");
                    return;
                }

                if (newLock != header->AnimationLock)
                {
                    PrintError("Mismatched animation lock offset! This can be caused by another plugin affecting the animation lock.");
                    return;
                }

                // Special case to (mostly) prevent accidentally using XivAlexander at the same time
                var isUsingAlexander = newLock % 0.01 is >= 0.0005f and <= 0.0095f;
                if (!enableAnticheat && isUsingAlexander)
                {
                    enableAnticheat = true;
                    PrintError($"Unexpected lock of {F2MS(newLock)} ms, temporary dry run has been enabled. Please disable any other programs or plugins that may be affecting the animation lock.");
                }

                var sequence = header->SourceSequence; // This is 0 for some special actions
                var actionID = header->SpellId;
                var appliedLock = appliedAnimationLocks.GetValueOrDefault(sequence, 0.5f);

                if (sequence == Game.actionManager->currentSequence)
                    appliedAnimationLocks.Clear(); // Probably unnecessary

                var lastRecordedLock = IsDryRunEnabled ? newLock : appliedLock - simulatedRTT;

                if (!enableAnticheat)
                    UpdateDatabase(actionID, newLock);

                // Get the difference between the recorded animation lock and the real one
                var correction = newLock - lastRecordedLock;
                var rtt = appliedLock - oldLock;

                if (rtt <= simulatedRTT)
                {
                    if (Config.EnableLogging)
                        PrintLog($"RTT ({F2MS(rtt)} ms) was lower than {F2MS(simulatedRTT)} ms, no adjustments were made");
                    return;
                }

                var prevAverage = delay;
                var newAverage = AverageDelay(rtt, packetsSent > 1 ? 0.1f : 1f);
                var average = Math.Max(prevAverage > 0 ? prevAverage : newAverage, 0.001f);

                var variationMultiplier = Math.Max(rtt / average, 1) - 1;
                var networkVariation = simulatedRTT * variationMultiplier;

                var adjustedAnimationLock = Math.Max(oldLock + correction + networkVariation, 0);

                if (!IsDryRunEnabled && float.IsFinite(adjustedAnimationLock) && adjustedAnimationLock < 10)
                {
                    Game.actionManager->animationLock = adjustedAnimationLock;

                    Config.TotalAnimationLockReduction += newLock - adjustedAnimationLock;
                    Config.TotalActionsReduced++;

                    if (!saveConfig && DalamudApi.Condition[ConditionFlag.InCombat])
                        saveConfig = true;
                }

                if (!Config.EnableLogging) return;

                var sb = new StringBuilder(IsDryRunEnabled ? "[DRY] " : string.Empty)
                        .Append($"Action: {actionID} ")
                        .Append(lastRecordedLock != newLock ? $"({F2MS(lastRecordedLock)} > {F2MS(newLock)} ms)" : $"({F2MS(newLock)} ms)")
                        .Append($" || RTT: {F2MS(rtt)} (+{variationMultiplier:P0}) ms");

                if (enableAnticheat)
                    sb.Append($" [Alexander: {F2MS(rtt - (appliedLock - simulatedRTT - newLock))} ms]");

                if (!IsDryRunEnabled)
                    sb.Append($" || Lock: {F2MS(oldLock)} > {F2MS(adjustedAnimationLock)} ({F2MS(correction + networkVariation):+0;-#}) ms");

                sb.Append($" || Packets: {packetsSent}");

                PrintLog(sb.ToString());
            }
            catch { PrintError("Error in AnimationLock Module"); }
        }

        private void NetworkMessage()
        {
            intervalPackets[intervalPacketsIndex]++;
        }

        private void Update()
        {
            if (saveConfig && DalamudApi.Condition[ConditionFlag.BetweenAreas])
            {
                Config.Save();
                saveConfig = false;
            }

            intervalPacketsTimer += (float)DalamudApi.Framework.UpdateDelta.TotalSeconds;
            while (intervalPacketsTimer >= 0.01f)
            {
                intervalPacketsTimer -= 0.01f;
                intervalPacketsIndex = (intervalPacketsIndex + 1) % intervalPackets.Length;
                intervalPackets[intervalPacketsIndex] = 0;
            }
        }

        public override void DrawConfig()
        {
            Theme.Section("Animation lock");

            if (ImGui.Checkbox("Enable Animation Lock Reduction", ref Config.EnableAnimLockComp))
                Config.Save();
            PluginUI.SetItemTooltip("Modifies the way the game handles animation lock,\n" +
                $"causing it to simulate {Config.SimulatedPingMs} ms ping.");

            ImGui.Spacing();

            Theme.Caption("Simulated ping");

            // Slider takes the full row; Reset sits on the same line at its right edge.
            var ping = Config.SimulatedPingMs;
            var resetWidth = ImGui.CalcTextSize("Reset").X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetStyle().ItemSpacing.X;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - resetWidth);
            if (ImGui.SliderInt("##SimulatedPing", ref ping, MinSimulatedPingMs, MaxSimulatedPingMs, "%d ms"))
                Config.SimulatedPingMs = ping;
            if (ImGui.IsItemDeactivatedAfterEdit())
                Config.Save();
            PluginUI.SetItemTooltip($"How much latency to simulate for animation lock. Default is {DefaultSimulatedPingMs} ms.");

            ImGui.SameLine();
            if (ImGui.Button("Reset"))
            {
                Config.SimulatedPingMs = DefaultSimulatedPingMs;
                Config.Save();
            }
            PluginUI.SetItemTooltip($"Set back to the default of {DefaultSimulatedPingMs} ms.");

            ImGui.Spacing();
            Theme.Caption($"Reduced a total of {TimeSpan.FromSeconds(Config.TotalAnimationLockReduction):d\\:hh\\:mm\\:ss} from {Config.TotalActionsReduced} actions");
        }

        public override void DrawSettings()
        {
            Theme.Section("Animation lock");

            if (ImGui.Checkbox("Enable logging", ref Config.EnableLogging))
                Config.Save();
            PluginUI.SetItemTooltip("Logs every animation lock adjustment.");

            var _ = IsDryRunEnabled;
            if (ImGui.Checkbox("Dry run", ref _))
            {
                Config.EnableDryRun = _;
                enableAnticheat = false;
                Config.Save();
            }
            PluginUI.SetItemTooltip("The plugin will still log and perform calculations, but no in-game values will be overwritten.");
        }

        public override unsafe void Enable()
        {
            Game.OnUseActionLocation += UseActionLocation;
            Game.OnCastBegin += CastBegin;
            Game.OnCastInterrupt += CastInterrupt;
            Game.OnReceiveActionEffect += ReceiveActionEffect;
            Game.OnUpdate += Update;
            Game.OnNetworkMessageDelegate += NetworkMessage;
        }

        public override unsafe void Disable()
        {
            Game.OnUseActionLocation -= UseActionLocation;
            Game.OnCastBegin -= CastBegin;
            Game.OnCastInterrupt -= CastInterrupt;
            Game.OnReceiveActionEffect -= ReceiveActionEffect;
            Game.OnUpdate -= Update;
            Game.OnNetworkMessageDelegate -= NetworkMessage;
        }
    }
}
