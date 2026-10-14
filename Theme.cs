using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace NoClippy
{
    // The Marochy look (Dotrb palette, from Marochy-Ecosystem-Theme/tokens/tokens.json) applied to ImGui.
    // Surfaces are the bg mixed toward the fg; square corners; 1 px lines; one accent.
    public static class Theme
    {
        private const uint Bg = 0x121212;
        private const uint Surface = 0x1C1C1C;
        private const uint Surface2 = 0x252525;
        private const uint Surface3 = 0x2D2D2D;
        private const uint Border = 0x323232;
        private const uint Faint = 0x646464;
        private const uint Muted = 0x969696;
        private const uint Fg = 0xF5F5F5;
        private const uint Accent = 0xB14242;
        private const uint AccentHover = 0xC85050;
        private const uint AccentActive = 0x9A3A3A;

        private static int colorCount;
        private static int varCount;

        private static Vector4 Rgb(uint hex, float alpha = 1f) =>
            new(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, alpha);

        private static void Color(ImGuiCol col, uint hex, float alpha = 1f)
        {
            ImGui.PushStyleColor(col, Rgb(hex, alpha));
            colorCount++;
        }

        private static void Var(ImGuiStyleVar v, float value)
        {
            ImGui.PushStyleVar(v, value);
            varCount++;
        }

        private static void Var(ImGuiStyleVar v, Vector2 value)
        {
            ImGui.PushStyleVar(v, value);
            varCount++;
        }

        public static void Push()
        {
            Color(ImGuiCol.WindowBg, Bg);
            Color(ImGuiCol.ChildBg, Surface);
            Color(ImGuiCol.PopupBg, Surface);
            Color(ImGuiCol.Border, Border);
            Color(ImGuiCol.Text, Fg);
            Color(ImGuiCol.TextDisabled, Muted);
            Color(ImGuiCol.TextSelectedBg, Accent, 0.45f);
            Color(ImGuiCol.FrameBg, Bg);
            Color(ImGuiCol.FrameBgHovered, Surface2);
            Color(ImGuiCol.FrameBgActive, Surface2);
            Color(ImGuiCol.CheckMark, Accent);
            Color(ImGuiCol.SliderGrab, Accent);
            Color(ImGuiCol.SliderGrabActive, AccentHover);
            Color(ImGuiCol.Button, Surface);
            Color(ImGuiCol.ButtonHovered, Surface2);
            Color(ImGuiCol.ButtonActive, Surface3);
            Color(ImGuiCol.Header, Surface2);
            Color(ImGuiCol.HeaderHovered, Surface2);
            Color(ImGuiCol.HeaderActive, Surface3);
            Color(ImGuiCol.Separator, Border);
            Color(ImGuiCol.TitleBg, Bg);
            Color(ImGuiCol.TitleBgActive, Surface);
            Color(ImGuiCol.TitleBgCollapsed, Bg);

            Var(ImGuiStyleVar.WindowRounding, 0f);
            Var(ImGuiStyleVar.ChildRounding, 0f);
            Var(ImGuiStyleVar.FrameRounding, 0f);
            Var(ImGuiStyleVar.PopupRounding, 0f);
            Var(ImGuiStyleVar.GrabRounding, 0f);
            Var(ImGuiStyleVar.ScrollbarRounding, 0f);
            Var(ImGuiStyleVar.TabRounding, 0f);
            Var(ImGuiStyleVar.WindowBorderSize, 1f);
            Var(ImGuiStyleVar.ChildBorderSize, 1f);
            Var(ImGuiStyleVar.PopupBorderSize, 1f);
            Var(ImGuiStyleVar.FrameBorderSize, 1f);
            Var(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6));
        }

        public static void Pop()
        {
            ImGui.PopStyleColor(colorCount);
            ImGui.PopStyleVar(varCount);
            colorCount = 0;
            varCount = 0;
        }

        // Primary button: accent fill, white text (accentInk on Dotrb).
        public static bool AccentButton(string label)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Rgb(Accent));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Rgb(AccentHover));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Rgb(AccentActive));
            ImGui.PushStyleColor(ImGuiCol.Text, Rgb(0xFFFFFF));
            var pressed = ImGui.Button(label);
            ImGui.PopStyleColor(4);
            return pressed;
        }

        // Section header: UPPERCASE, muted, then a hairline.
        public static void Section(string title)
        {
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, Rgb(Muted));
            ImGui.TextUnformatted(title.ToUpperInvariant());
            ImGui.PopStyleColor();
            ImGui.Separator();
        }

        // Quiet text for secondary information.
        public static void Caption(string text)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Rgb(Faint));
            ImGui.TextWrapped(text);
            ImGui.PopStyleColor();
        }
    }
}
