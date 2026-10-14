using System;
using Dalamud.Game.Text;
using Dalamud.Bindings.ImGui;

namespace ExtremeClipping
{
    public partial class Configuration
    {
        public bool LogToChat = false;
        public XivChatType LogChatType = XivChatType.None;
    }
}

// This is a module because why not
namespace ExtremeClipping.Modules
{
    public class Logging : Module
    {
        public override int DrawOrder => 8;

        public override void DrawSettings()
        {
            Theme.Section("Logging");

            if (ImGui.Checkbox("Output to chat log", ref ExtremeClipping.Config.LogToChat))
                ExtremeClipping.Config.Save();
            PluginUI.SetItemTooltip("Sends logging to the chat log instead.");

            if (!ExtremeClipping.Config.LogToChat) return;

            if (ImGui.BeginCombo("Log chat type", ExtremeClipping.Config.LogChatType.ToString()))
            {
                foreach (var chatType in Enum.GetValues<XivChatType>())
                {
                    if (!ImGui.Selectable(chatType.ToString())) continue;

                    ExtremeClipping.Config.LogChatType = chatType;
                    ExtremeClipping.Config.Save();
                }

                ImGui.EndCombo();
            }

            PluginUI.SetItemTooltip("Overrides the default Dalamud chat channel.");
        }
    }
}
