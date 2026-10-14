using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Bindings.ImGui;

namespace ExtremeClipping
{
    public partial class Configuration
    {
        public bool EnableEncounterStats = false;
        public bool EnableEncounterStatsLogging = false;
    }
}

namespace ExtremeClipping.Modules
{
    public class Stats : Module
    {
        public override bool IsEnabled
        {
            get => ExtremeClipping.Config.EnableEncounterStats;
            set => ExtremeClipping.Config.EnableEncounterStats = value;
        }

        public override int DrawOrder => 5;

        private DateTime begunEncounter = DateTime.MinValue;
        private ushort lastDetectedClip = 0;
        private float currentWastedGCD = 0;
        private float encounterTotalClip = 0;
        private float encounterTotalWaste = 0;

        private void BeginEncounter()
        {
            begunEncounter = DateTime.Now;
            encounterTotalClip = 0;
            encounterTotalWaste = 0;
            currentWastedGCD = 0;
        }

        private void EndEncounter()
        {
            var span = DateTime.Now - begunEncounter;
            var formattedTime = $"{Math.Floor(span.TotalMinutes):00}:{span.Seconds:00}";
            ExtremeClipping.PrintLog($"[{formattedTime}] Encounter stats: {encounterTotalClip:0.00} seconds of clipping, {encounterTotalWaste:0.00} seconds of wasted GCD.");
            begunEncounter = DateTime.MinValue;
        }

        private unsafe void DetectClipping()
        {
            var animationLock = Game.actionManager->animationLock;
            if (lastDetectedClip == Game.actionManager->currentSequence || Game.actionManager->isGCDRecastActive || animationLock <= 0) return;

            if (animationLock != 0.1f) // TODO need better way of detecting cast tax, IsCasting is not reliable here, additionally, this will detect LB
            {
                encounterTotalClip += animationLock;
                if (ExtremeClipping.Config.EnableEncounterStatsLogging)
                    ExtremeClipping.PrintLog($"GCD Clip: {ExtremeClipping.F2MS(animationLock)} ms");
            }

            lastDetectedClip = Game.actionManager->currentSequence;
        }

        private unsafe void DetectWastedGCD()
        {
            if (!Game.actionManager->isGCDRecastActive && !Game.actionManager->isQueued)
            {
                if (Game.actionManager->animationLock > 0) return;
                currentWastedGCD += ImGui.GetIO().DeltaTime;
            }
            else if (currentWastedGCD > 0)
            {
                encounterTotalWaste += currentWastedGCD;
                if (ExtremeClipping.Config.EnableEncounterStatsLogging)
                    ExtremeClipping.PrintLog($"Wasted GCD: {ExtremeClipping.F2MS(currentWastedGCD)} ms");
                currentWastedGCD = 0;
            }
        }

        private void Update()
        {
            if (DalamudApi.Condition[ConditionFlag.InCombat])
            {
                if (begunEncounter == DateTime.MinValue)
                    BeginEncounter();

                DetectClipping();
                DetectWastedGCD();
            }
            else if (begunEncounter != DateTime.MinValue)
            {
                EndEncounter();
            }
        }

        public override void DrawSettings()
        {
            Theme.Section("Encounter stats");

            if (ImGui.Checkbox("Enable encounter stats", ref ExtremeClipping.Config.EnableEncounterStats))
                ExtremeClipping.Config.Save();
            PluginUI.SetItemTooltip("Tracks clips and wasted GCD time while in combat, and logs the total afterwards.");

            if (!ExtremeClipping.Config.EnableEncounterStats) return;

            if (ImGui.Checkbox("Enable stats logging", ref ExtremeClipping.Config.EnableEncounterStatsLogging))
                ExtremeClipping.Config.Save();
            PluginUI.SetItemTooltip("Logs individual encounter clips and wasted GCD time.");
        }

        public override void Enable() => Game.OnUpdate += Update;
        public override void Disable() => Game.OnUpdate -= Update;
    }
}
