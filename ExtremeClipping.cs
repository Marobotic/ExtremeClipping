using System;
using System.IO;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;

namespace ExtremeClipping
{
    public class ExtremeClipping : IDalamudPlugin
    {
        public static ExtremeClipping Plugin { get; private set; }
        public static Configuration Config { get; private set; }

        public ExtremeClipping(IDalamudPluginInterface pluginInterface)
        {
            Plugin = this;
            DalamudApi.Initialize(this, pluginInterface);

            Config = LoadConfig();
            Config.Initialize();

            try
            {
                Game.Initialize();

                DalamudApi.Framework.Update += Update;
                DalamudApi.PluginInterface.UiBuilder.Draw += PluginUI.Draw;
                DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += ConfigUI.ToggleVisible;

                Modules.Modules.Initialize();
            }
            catch (Exception e)
            {
                PrintError("Failed to load!");
                DalamudApi.LogError(e.ToString());
            }
        }

        [Command("/extremeclipping")]
        [Aliases("/xclip")]
        [HelpMessage("/extremeclipping or /xclip [on|off|toggle|dry|help] - Toggles the config window if no option is specified.")]
        private void OnCommand(string command, string argument)
        {
            switch (argument)
            {
                case "on":
                case "toggle" when !Config.EnableAnimLockComp:
                case "t" when !Config.EnableAnimLockComp:
                    Config.EnableAnimLockComp = true;
                    Config.Save();
                    PrintEcho("Enabled animation lock compensation!");
                    break;
                case "off":
                case "toggle" when Config.EnableAnimLockComp:
                case "t" when Config.EnableAnimLockComp:
                    Config.EnableAnimLockComp = false;
                    Config.Save();
                    PrintEcho("Disabled animation lock compensation!");
                    break;
                case "dry":
                case "d":
                    PrintEcho($"Dry run is now {((Config.EnableDryRun = !Config.EnableDryRun) ? "enabled" : "disabled")}.");
                    Config.Save();
                    break;
                case "":
                    ConfigUI.ToggleVisible();
                    break;
                default:
                    PrintEcho("Invalid usage: Command must be \"/xclip <option>\"." +
                        "\non / off / toggle - Enables or disables animation lock compensation." +
                        "\ndry - Toggles dry run (will not override the animation lock).");
                    break;
            }
        }

        // 1.0.0.0 saved its settings as NoClippy.Configuration, a type that no longer exists, so
        // Dalamud can't read them back. Read the file again with the type fixed to this Configuration.
        private static Configuration LoadConfig()
        {
            try
            {
                if (DalamudApi.PluginInterface.GetPluginConfig() is Configuration config) return config;
            }
            catch (Exception e)
            {
                DalamudApi.LogInfo($"Reading the settings saved under the old name instead: {e.Message}");
            }

            var file = DalamudApi.PluginInterface.ConfigFile;
            if (!file.Exists) return new();

            try
            {
                return JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(file.FullName)) ?? new();
            }
            catch (Exception e)
            {
                DalamudApi.LogError($"Could not read the saved settings, starting fresh: {e}");
                return new();
            }
        }

        public static void PrintEcho(string message) => DalamudApi.ChatGui.Print($"[Extreme Clipping] {message}");
        public static void PrintError(string message) => DalamudApi.ChatGui.PrintError($"[Extreme Clipping] {message}");

        public static void PrintLog(string message)
        {
            if (Config.LogToChat)
            {
                if (Config.LogChatType != XivChatType.None)
                {
                    DalamudApi.ChatGui.Print(new XivChatEntry
                    {
                        Message = $"[Extreme Clipping] {message}",
                        Type = Config.LogChatType
                    });
                }
                else
                {
                    PrintEcho(message);
                }
            }
            else
            {
                DalamudApi.LogInfo(message);
            }
        }

        public static int F2MS(float f) => (int)Math.Round(f * 1000);

        private static void Update(IFramework framework) => Game.Update();

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            Config.Save();

            DalamudApi.Framework.Update -= Update;
            DalamudApi.PluginInterface.UiBuilder.Draw -= PluginUI.Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= ConfigUI.ToggleVisible;

            Modules.Modules.Dispose();
            Game.Dispose();

            DalamudApi.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
