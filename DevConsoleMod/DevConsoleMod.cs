using AurieSharpInterop;
using BubbleLib.Core;
using YYTKInterop;

namespace DevConsoleMod
{
    public class DevConsoleMod : BubbleMod
    {
        private const int ToggleKey = 192;
        private const int EnterKey = 13;
        private const int BackspaceKey = 8;
        private const int SpaceKey = 32;
        private const int MaxLogLines = 8;
        private const int Padding = 4;

        private const string InputObject = "o_input";
        private const string NpcParent = "par_npc";
        private const string NpcStepEvent = "par_npc_Step_0";
        private const string StepEvent = "_Step_0";
        private const string FrameEndEvent = "o_input_Draw_64";

        private static readonly string[] GameInputs =
        {
            "attack", "back", "crouch", "down", "down_menu", "down_menu_pressed", "enter", "enter_pressed",
            "hide_ui", "inspect", "interact", "interact_hold", "jump", "left", "left_menu", "left_menu_pressed",
            "mouse", "mouse_pressed", "mouse_right", "move_ui", "open_inventory", "open_skilltree", "pause",
            "reload_tool", "restart", "right", "right_menu", "right_menu_pressed", "scroll_down", "scroll_down_pressed",
            "scroll_up", "scroll_up_pressed", "start", "stats", "ui_back", "unpause", "up", "up_menu", "up_menu_pressed",
            "use_bubble_gun", "use_plasma_gun", "zoom_in", "zoom_out"
        };

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Shelldiver", "dev_console_npcs_hidden.txt");

        private readonly Dictionary<string, (string Help, Action<string[]> Run)> commands = new();
        private readonly List<string> log = new();
        private string typed = "";
        private bool open;
        private bool npcsHidden;
        private bool showNpcsOnce;
        private bool inputsClearedThisFrame;
        private bool npcsUpdatedThisFrame;
        private bool failed;

        public DevConsoleMod(AurieManagedModule module) : base(module)
        {
        }

        public override string Name => "Dev Console";

        public override void Initialize()
        {
            commands["help"] = ("List commands", _ => ShowHelp());
            commands["clear"] = ("Clear the console", _ => log.Clear());
            commands["npc"] = ("[on|off] Show, hide or toggle NPCs", RunNpcCommand);

            npcsHidden = File.Exists(SettingsPath);
            Game.Events.OnGameEvent += OnGameEvent;
            log.Add("Type help for commands. Press ` to close.");
        }

        public List<string> GetCommandNames()
        {
            return commands.Keys.ToList();
        }

        public string GetCommandLabel(string name)
        {
            return name == "npc" ? (npcsHidden ? "NPCs: Hidden" : "NPCs: Shown") : $"{name}: {commands[name].Help}";
        }

        public string RunCommand(string name)
        {
            AddLog("> " + name);
            commands[name].Run(Array.Empty<string>());
            return log.Count > 0 ? log[log.Count - 1] : "Done";
        }

        public override void DrawGUI()
        {
            if (failed)
            {
                return;
            }

            try
            {
                if (KeyPressed(ToggleKey))
                {
                    open = !open;
                    typed = "";
                    return;
                }

                if (open)
                {
                    ReadKeys();
                    DrawConsole();
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void OnGameEvent(CodeExecutionContext context)
        {
            if (failed)
            {
                return;
            }

            try
            {
                if (context.Name.Contains(FrameEndEvent))
                {
                    inputsClearedThisFrame = false;
                    npcsUpdatedThisFrame = false;
                    return;
                }

                if (open && !inputsClearedThisFrame && context.Name.Contains(StepEvent))
                {
                    inputsClearedThisFrame = true;
                    ClearGameInputs();
                }

                if (!npcsUpdatedThisFrame && context.Name.Contains(NpcStepEvent))
                {
                    npcsUpdatedThisFrame = true;
                    UpdateNpcs();
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void Fail(Exception ex)
        {
            failed = true;
            Framework.PrintEx(AurieLogSeverity.Error, $"[DevConsole] Stopped: {ex}");
        }

        private static bool KeyPressed(int key)
        {
            return Game.Engine.CallFunction("keyboard_check_pressed", key);
        }

        private void ReadKeys()
        {
            if (KeyPressed(EnterKey))
            {
                Submit();
                return;
            }

            if (KeyPressed(BackspaceKey) && typed.Length > 0)
            {
                typed = typed.Substring(0, typed.Length - 1);
            }

            if (KeyPressed(SpaceKey))
            {
                typed += " ";
            }

            for (int key = 'A'; key <= 'Z'; key++)
            {
                if (KeyPressed(key))
                {
                    typed += char.ToLowerInvariant((char)key);
                }
            }

            for (int key = '0'; key <= '9'; key++)
            {
                if (KeyPressed(key))
                {
                    typed += (char)key;
                }
            }
        }

        private void Submit()
        {
            string line = typed.Trim();
            typed = "";
            if (line.Length == 0)
            {
                return;
            }

            AddLog("> " + line);
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (commands.TryGetValue(parts[0], out var command))
            {
                command.Run(parts.Skip(1).ToArray());
            }
            else
            {
                AddLog($"Unknown command: {parts[0]}");
            }
        }

        private void AddLog(string line)
        {
            log.Add(line);
            if (log.Count > MaxLogLines)
            {
                log.RemoveAt(0);
            }
        }

        private void ShowHelp()
        {
            foreach (var command in commands)
            {
                AddLog($"{command.Key}: {command.Value.Help}");
            }
        }

        private void RunNpcCommand(string[] arguments)
        {
            bool hide;
            if (arguments.Length == 0)
            {
                hide = !npcsHidden;
            }
            else if (arguments[0] == "on")
            {
                hide = false;
            }
            else if (arguments[0] == "off")
            {
                hide = true;
            }
            else
            {
                AddLog("Usage: npc [on|off]");
                return;
            }

            npcsHidden = hide;
            showNpcsOnce = !hide;
            if (hide)
            {
                File.WriteAllText(SettingsPath, "1");
            }
            else
            {
                File.Delete(SettingsPath);
            }

            AddLog(hide ? "NPCs hidden" : "NPCs shown");
        }

        private void UpdateNpcs()
        {
            if (!npcsHidden && !showNpcsOnce)
            {
                return;
            }

            GameVariable parent = GML.GetAsset(NpcParent)!;
            int count = Game.Engine.CallFunction("instance_number", parent);
            for (int i = 0; i < count; i++)
            {
                GameVariable npc = Game.Engine.CallFunction("instance_find", parent, i);
                GML.SetInstanceVariable(npc, "visible", !npcsHidden);
            }

            showNpcsOnce = false;
        }

        private static void ClearGameInputs()
        {
            GameVariable input = Game.Engine.CallFunction("instance_find", GML.GetAsset(InputObject)!, 0);
            foreach (string name in GameInputs)
            {
                GML.SetInstanceVariable(input, name, 0);
            }
        }

        private void DrawConsole()
        {
            GameVariable oldColor = Game.Engine.CallFunction("draw_get_color");
            GameVariable oldAlpha = Game.Engine.CallFunction("draw_get_alpha");
            GameVariable oldHalign = Game.Engine.CallFunction("draw_get_halign");
            GameVariable oldValign = Game.Engine.CallFunction("draw_get_valign");

            int width = Game.Engine.CallFunction("display_get_gui_width");
            int lineHeight = Game.Engine.CallFunction("string_height", "A");
            int height = lineHeight * (log.Count + 1) + Padding * 2;

            Game.Engine.CallFunction("draw_set_halign", 0);
            Game.Engine.CallFunction("draw_set_valign", 0);
            Game.Engine.CallFunction("draw_set_alpha", 0.85);
            Game.Engine.CallFunction("draw_set_color", 0);
            Game.Engine.CallFunction("draw_rectangle", 0, 0, width, height, false);
            Game.Engine.CallFunction("draw_set_alpha", 1);
            Game.Engine.CallFunction("draw_set_color", 16777215);

            int y = Padding;
            foreach (string line in log)
            {
                Game.Engine.CallFunction("draw_text", Padding, y, line);
                y += lineHeight;
            }

            Game.Engine.CallFunction("draw_text", Padding, y, "> " + typed + "_");

            Game.Engine.CallFunction("draw_set_color", oldColor);
            Game.Engine.CallFunction("draw_set_alpha", oldAlpha);
            Game.Engine.CallFunction("draw_set_halign", oldHalign);
            Game.Engine.CallFunction("draw_set_valign", oldValign);
        }
    }
}
