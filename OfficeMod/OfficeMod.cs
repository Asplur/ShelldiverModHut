using AurieSharpInterop;
using BubbleLib;
using BubbleLib.Core;
using System.Reflection;
using YYTKInterop;

namespace OfficeMod
{
    public class OfficeMod : BubbleMod
    {
        private const string IslandRoom = "rm_main_island";
        private const string PropDrawEvent = "par_prop_Draw_0";
        private const string FrameEndEvent = "o_input_Draw_64";
        private const string StepEvent = "_Step_0";
        private const float OfficeX = 2120;
        private const float GroundY = 401;
        private const float InteractRange = 70;
        private const float PromptGap = 12;

        private const string ModFolder = "mods\\bubble";
        private const string DisabledSuffix = ".disabled";
        private static readonly string[] ProtectedModFiles = { "OfficeMod.dll", "LandExtensionMod.dll" };

        private const int EnterKey = 13;
        private const int SpaceKey = 32;
        private const int EscapeKey = 27;
        private const int LeftKey = 37;
        private const int UpKey = 38;
        private const int RightKey = 39;
        private const int DownKey = 40;
        private const int KeyA = 65;
        private const int KeyD = 68;
        private const int KeyS = 83;
        private const int KeyW = 87;
        private const int LeftMouse = 1;
        private const int RightMouse = 2;

        private const double TextScale = 0.7;
        private const double TitleScale = 0.8;
        private const double IconScale = 0.7;
        private const int PanelWidth = 300;
        private const int PanelHeight = 190;
        private const int PanelFallbackColor = 4203056;
        private const int ContentLeft = 12;
        private const int TitleY = 8;
        private const int TabsY = 28;
        private const int TabWidth = 66;
        private const int TabHeight = 12;
        private const int RowsTop = 46;
        private const int RowHeight = 13;
        private const int VisibleRows = 8;
        private const int FooterY = 170;
        private const int ValueRight = 270;
        private const int ValueColumn = 165;
        private const int ScrollbarX = 288;
        private const int ScrollbarWidth = 3;

        private const int White = 16777215;
        private const int Grey = 12632256;
        private const int Gold = 4247792;
        private const int Dark = 0;

        private const double AnimationSpeed = 0.14;
        private const double SlideSpeed = 0.25;
        private const int SlideDistance = 12;
        private const double DimAlpha = 0.45;
        private const double HighlightAlpha = 0.18;
        private const int HoldDelay = 18;
        private const int HoldRate = 4;
        private const int StatusFrames = 180;
        private const int StatusFadeFrames = 30;

        private static readonly string[] TabNames = { "Mods", "Commands" };

        private static readonly string[] GameInputs =
        {
            "attack", "back", "crouch", "down", "down_menu", "down_menu_pressed", "enter", "enter_pressed",
            "hide_ui", "inspect", "interact", "interact_hold", "jump", "left", "left_menu", "left_menu_pressed",
            "mouse", "mouse_pressed", "mouse_right", "move_ui", "open_inventory", "open_skilltree", "pause",
            "reload_tool", "restart", "right", "right_menu", "right_menu_pressed", "scroll_down", "scroll_down_pressed",
            "scroll_up", "scroll_up_pressed", "start", "stats", "ui_back", "unpause", "up", "up_menu", "up_menu_pressed",
            "use_bubble_gun", "use_plasma_gun", "zoom_in", "zoom_out"
        };

        private sealed record PanelRow(Func<string> Label, Action? Activate, Action<int>? Adjust = null);

        private readonly int[] holdTimers = new int[4];
        private List<PanelRow>? submenu;
        private string submenuTitle = "";
        private List<PanelRow> modRows = new();
        private List<PanelRow> commandRows = new();
        private string status = "";
        private string shownStatus = "";
        private int statusTimer;
        private int selected;
        private int lastMouseX;
        private int lastMouseY;
        private Sprite? sprite;
        private bool drawnThisFrame;
        private bool steppedThisFrame;
        private bool panelOpen;
        private bool closing;
        private bool interactHeld;
        private int tab;
        private int scroll;
        private double openAmount;
        private double smoothScroll;
        private double smoothSelected;
        private double smoothTab;
        private bool failed;

        public OfficeMod(AurieManagedModule module) : base(module)
        {
        }

        public override string Name => "Office";

        public override void Initialize()
        {
            sprite = new Sprite("Office/Office.png");
            Game.Events.OnGameEvent += OnGameEvent;
        }

        public override void DrawGUI()
        {
            if (failed || !panelOpen)
            {
                return;
            }

            try
            {
                if (!closing)
                {
                    ReadPanelInput();
                }

                UpdateAnimation();
                if (panelOpen)
                {
                    DrawPanel();
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void OnGameEvent(CodeExecutionContext context)
        {
            try
            {
                if (context.Name.Contains(FrameEndEvent))
                {
                    drawnThisFrame = false;
                    steppedThisFrame = false;
                }
                else if (!failed && !steppedThisFrame && context.Name.Contains(StepEvent))
                {
                    steppedThisFrame = true;
                    HandleInteract();
                }
                else if (!drawnThisFrame && context.Name.Contains(PropDrawEvent))
                {
                    drawnThisFrame = true;
                    DrawOffice();
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
            panelOpen = false;
            Framework.PrintEx(AurieLogSeverity.Error, $"[Office] Stopped: {ex}");
        }

        private static bool OnIsland()
        {
            string room = Game.Engine.CallFunction("room_get_name", Game.Engine.GetBuiltinVariable("room", Game.Engine.GetGlobalObject(), 0));
            return room == IslandRoom;
        }

        private static bool PlayerIsNear()
        {
            GameVariable player = GML.GetAsset("par_player")!;
            int count = Game.Engine.CallFunction("instance_number", player);
            if (count == 0)
            {
                return false;
            }

            GameVariable instance = Game.Engine.CallFunction("instance_find", player, 0);
            return Math.Abs(GML.GetInstanceVariable(instance, "x")!.ToFloat() - OfficeX) <= InteractRange;
        }

        private void HandleInteract()
        {
            if (!OnIsland())
            {
                panelOpen = false;
                closing = false;
                openAmount = 0;
                interactHeld = false;
                return;
            }

            GameVariable inputAsset = GML.GetAsset("o_input")!;
            int count = Game.Engine.CallFunction("instance_number", inputAsset);
            if (count == 0)
            {
                return;
            }

            GameVariable input = Game.Engine.CallFunction("instance_find", inputAsset, 0);
            bool interact = GML.GetInstanceVariable(input, "interact")!.ToFloat() != 0;
            bool pressed = interact && !interactHeld;
            interactHeld = interact;
            bool wasOpen = panelOpen;

            if (pressed)
            {
                if (panelOpen)
                {
                    closing = !closing;
                }
                else if (PlayerIsNear())
                {
                    OpenPanel();
                }
            }

            if (wasOpen || panelOpen)
            {
                foreach (string name in GameInputs)
                {
                    GML.SetInstanceVariable(input, name, 0);
                }
            }
        }

        private void OpenPanel()
        {
            panelOpen = true;
            closing = false;
            openAmount = 0;
            tab = 0;
            smoothTab = 0;
            ResetList();
            submenu = null;
            modRows = LoadModRows();
            commandRows = LoadCommandRows();
            lastMouseX = MouseGuiX();
            lastMouseY = MouseGuiY();
            PlaySelect();
        }

        private void ResetList()
        {
            scroll = 0;
            selected = 0;
            smoothScroll = 0;
            smoothSelected = 0;
            status = "";
        }

        private static void PlaySelect()
        {
            Game.Engine.CallFunction("audio_play_sound", GML.GetAsset("snd_select")!, 0, false);
        }

        private static IEnumerable<BubbleMod> LoadedMods()
        {
            FieldInfo field = typeof(Bubble).GetField("Mods", BindingFlags.Static | BindingFlags.NonPublic)!;
            return (IEnumerable<BubbleMod>)field.GetValue(null)!;
        }

        private List<PanelRow> LoadModRows()
        {
            List<PanelRow> rows = new() { new PanelRow(() => $"BubbleLib: v{Bubble.Version}", null) };
            foreach (BubbleMod mod in LoadedMods())
            {
                string file = mod.GetType().Assembly.GetName().Name + ".dll";
                rows.Add(mod.GetType().GetMethod("GetTrainerRowCount") != null
                    ? CreateTrainerRow(mod)
                    : CreateModRow(mod.Name, file));
            }

            if (Directory.Exists(ModFolder))
            {
                foreach (string path in Directory.GetFiles(ModFolder, "*" + DisabledSuffix))
                {
                    string file = Path.GetFileName(path).Replace(DisabledSuffix, "");
                    if (!LoadedMods().Any(mod => mod.GetType().Assembly.GetName().Name + ".dll" == file))
                    {
                        rows.Add(CreateModRow(Path.GetFileNameWithoutExtension(file), file));
                    }
                }
            }

            return rows;
        }

        private PanelRow CreateTrainerRow(BubbleMod mod)
        {
            Type type = mod.GetType();
            MethodInfo label = type.GetMethod("GetTrainerLabel")!;
            MethodInfo adjust = type.GetMethod("AdjustTrainerRow")!;
            MethodInfo activate = type.GetMethod("ActivateTrainerRow")!;

            return new PanelRow(() => $"{mod.Name} >", () =>
            {
                int count = (int)type.GetMethod("GetTrainerRowCount")!.Invoke(mod, null)!;
                submenu = Enumerable.Range(0, count).Select(index => new PanelRow(
                    () => (string)label.Invoke(mod, new object[] { index })!,
                    () => status = (string)activate.Invoke(mod, new object[] { index })!,
                    direction => adjust.Invoke(mod, new object[] { index, direction }))).ToList();
                submenuTitle = mod.Name;
                ResetList();
            });
        }

        private PanelRow CreateModRow(string name, string file)
        {
            string enabledPath = Path.Combine(ModFolder, file);
            string disabledPath = enabledPath + DisabledSuffix;
            if (ProtectedModFiles.Contains(file))
            {
                return new PanelRow(() => $"{name}: Always on", null);
            }

            return new PanelRow(
                () => $"{name}: {(File.Exists(enabledPath) ? "On" : "Off")}",
                () =>
                {
                    if (File.Exists(enabledPath))
                    {
                        File.Move(enabledPath, disabledPath, true);
                    }
                    else
                    {
                        File.Move(disabledPath, enabledPath, true);
                    }

                    status = "Applies next time you start the game";
                });
        }

        private List<PanelRow> LoadCommandRows()
        {
            BubbleMod? console = LoadedMods().FirstOrDefault(mod => mod.GetType().GetMethod("RunCommand") != null);
            if (console == null)
            {
                return new List<PanelRow> { new PanelRow(() => "Dev Console is off", null) };
            }

            Type type = console.GetType();
            MethodInfo labels = type.GetMethod("GetCommandLabel")!;
            MethodInfo run = type.GetMethod("RunCommand")!;
            List<string> names = (List<string>)type.GetMethod("GetCommandNames")!.Invoke(console, null)!;
            return names.Select(name => new PanelRow(
                () => (string)labels.Invoke(console, new object[] { name })!,
                () => status = (string)run.Invoke(console, new object[] { name })!)).ToList();
        }

        private static bool KeyPressed(int key)
        {
            return Game.Engine.CallFunction("keyboard_check_pressed", key);
        }

        private static bool KeyHeld(int key)
        {
            return Game.Engine.CallFunction("keyboard_check", key);
        }

        private bool KeyRepeat(int slot, int key, int alternateKey)
        {
            if (!KeyHeld(key) && !KeyHeld(alternateKey))
            {
                holdTimers[slot] = 0;
                return false;
            }

            holdTimers[slot]++;
            return holdTimers[slot] == 1 || (holdTimers[slot] > HoldDelay && (holdTimers[slot] - HoldDelay) % HoldRate == 0);
        }

        private static int MouseGuiX()
        {
            return Game.Engine.CallFunction("device_mouse_x_to_gui", 0);
        }

        private static int MouseGuiY()
        {
            return Game.Engine.CallFunction("device_mouse_y_to_gui", 0);
        }

        private void ReadPanelInput()
        {
            if (KeyPressed(EscapeKey))
            {
                if (submenu != null)
                {
                    submenu = null;
                    ResetList();
                }
                else
                {
                    closing = true;
                }

                return;
            }

            if (KeyRepeat(0, LeftKey, KeyA))
            {
                HorizontalInput(-1);
            }

            if (KeyRepeat(1, RightKey, KeyD))
            {
                HorizontalInput(1);
            }

            if (KeyRepeat(2, UpKey, KeyW))
            {
                Select(selected - 1);
            }

            if (KeyRepeat(3, DownKey, KeyS))
            {
                Select(selected + 1);
            }

            PanelOrigin(out int panelX, out int panelY);
            int mouseX = MouseGuiX();
            int mouseY = MouseGuiY();
            int hovered = RowAt(mouseX, mouseY, panelX, panelY);
            if ((mouseX != lastMouseX || mouseY != lastMouseY) && hovered >= 0)
            {
                Select(hovered);
            }

            lastMouseX = mouseX;
            lastMouseY = mouseY;

            ReadWheel(mouseX, panelX, hovered);

            if (KeyPressed(EnterKey) || KeyPressed(SpaceKey))
            {
                ActivateSelected();
            }

            bool clicked = Game.Engine.CallFunction("mouse_check_button_pressed", LeftMouse);
            if (clicked)
            {
                HandleClick(mouseX, mouseY, panelX, panelY, hovered);
            }

            bool rightClicked = Game.Engine.CallFunction("mouse_check_button_pressed", RightMouse);
            if (rightClicked && hovered >= 0 && mouseX >= panelX + ValueColumn)
            {
                Select(hovered);
                AdjustRow(CurrentRows()[hovered], 1);
            }
        }

        private void ReadWheel(int mouseX, int panelX, int hovered)
        {
            bool wheelUp = Game.Engine.CallFunction("mouse_wheel_up");
            bool wheelDown = Game.Engine.CallFunction("mouse_wheel_down");
            if (!wheelUp && !wheelDown)
            {
                return;
            }

            int direction = wheelUp ? 1 : -1;
            List<PanelRow> rows = CurrentRows();
            if (hovered >= 0 && rows[hovered].Adjust != null && mouseX >= panelX + ValueColumn)
            {
                Select(hovered);
                AdjustRow(rows[hovered], direction);
                return;
            }

            scroll = Math.Clamp(scroll - direction, 0, Math.Max(0, rows.Count - VisibleRows));
            selected = Math.Clamp(selected, scroll, Math.Min(rows.Count - 1, scroll + VisibleRows - 1));
        }

        private void HandleClick(int mouseX, int mouseY, int panelX, int panelY, int hovered)
        {
            if (submenu == null && mouseY >= panelY + TabsY && mouseY < panelY + TabsY + TabHeight && mouseX >= panelX + ContentLeft)
            {
                int clickedTab = (mouseX - (panelX + ContentLeft)) / TabWidth;
                if (clickedTab < TabNames.Length)
                {
                    SetTab(clickedTab);
                }

                return;
            }

            if (hovered < 0)
            {
                return;
            }

            Select(hovered);
            PanelRow row = CurrentRows()[hovered];
            if (row.Adjust != null && mouseX >= panelX + ValueColumn)
            {
                AdjustRow(row, -1);
            }
            else
            {
                ActivateSelected();
            }
        }

        private void HorizontalInput(int direction)
        {
            if (submenu == null)
            {
                SetTab(direction < 0 ? 0 : 1);
            }
            else if (selected < submenu.Count)
            {
                AdjustRow(submenu[selected], direction);
            }
        }

        private void AdjustRow(PanelRow row, int direction)
        {
            if (row.Adjust == null)
            {
                return;
            }

            try
            {
                row.Adjust(direction);
                status = "";
            }
            catch (Exception ex)
            {
                status = "Could not change that";
                Framework.PrintEx(AurieLogSeverity.Error, $"[Office] Row adjust failed: {ex}");
            }

            PlaySelect();
        }

        private int RowAt(int mouseX, int mouseY, int panelX, int panelY)
        {
            int top = panelY + RowsTop;
            if (mouseX < panelX + ContentLeft || mouseX >= panelX + ScrollbarX || mouseY < top || mouseY >= top + VisibleRows * RowHeight)
            {
                return -1;
            }

            int row = scroll + (mouseY - top) / RowHeight;
            return row < CurrentRows().Count ? row : -1;
        }

        private void Select(int row)
        {
            int clamped = Math.Clamp(row, 0, Math.Max(0, CurrentRows().Count - 1));
            if (clamped == selected)
            {
                return;
            }

            selected = clamped;
            if (selected < scroll)
            {
                scroll = selected;
            }
            else if (selected >= scroll + VisibleRows)
            {
                scroll = selected - VisibleRows + 1;
            }

            PlaySelect();
        }

        private void ActivateSelected()
        {
            List<PanelRow> rows = CurrentRows();
            if (selected >= rows.Count || rows[selected].Activate == null)
            {
                return;
            }

            try
            {
                rows[selected].Activate!();
            }
            catch (Exception ex)
            {
                status = "Could not do that";
                Framework.PrintEx(AurieLogSeverity.Error, $"[Office] Row action failed: {ex}");
            }

            PlaySelect();
        }

        private void SetTab(int newTab)
        {
            if (newTab == tab)
            {
                return;
            }

            tab = newTab;
            ResetList();
            PlaySelect();
        }

        private List<PanelRow> CurrentRows()
        {
            return submenu ?? (tab == 0 ? modRows : commandRows);
        }

        private static void PanelOrigin(out int x, out int y)
        {
            int guiWidth = Game.Engine.CallFunction("display_get_gui_width");
            int guiHeight = Game.Engine.CallFunction("display_get_gui_height");
            x = (guiWidth - PanelWidth) / 2;
            y = (guiHeight - PanelHeight) / 2;
        }

        private void UpdateAnimation()
        {
            openAmount = Math.Clamp(openAmount + (closing ? -AnimationSpeed : AnimationSpeed), 0, 1);
            if (closing && openAmount <= 0)
            {
                panelOpen = false;
                closing = false;
                return;
            }

            smoothScroll += (scroll - smoothScroll) * SlideSpeed;
            smoothSelected += (selected - smoothSelected) * SlideSpeed;
            smoothTab += (tab - smoothTab) * SlideSpeed;

            if (status != shownStatus)
            {
                shownStatus = status;
                statusTimer = StatusFrames;
            }
            else if (statusTimer > 0)
            {
                statusTimer--;
            }
        }

        private void DrawOffice()
        {
            if (!OnIsland())
            {
                return;
            }

            sprite!.Draw(OfficeX, GroundY - sprite.Height / 2f);
            if (!panelOpen && PlayerIsNear())
            {
                GameVariable key = GML.GetAsset("s_input_keyboard_e")!;
                Game.Engine.CallFunction("draw_sprite", key, 0, OfficeX, GroundY - sprite.Height - PromptGap);
            }
        }

        private void DrawPanel()
        {
            GameVariable oldColor = Game.Engine.CallFunction("draw_get_color");
            GameVariable oldAlpha = Game.Engine.CallFunction("draw_get_alpha");
            GameVariable oldFont = Game.Engine.CallFunction("draw_get_font");
            GameVariable oldHalign = Game.Engine.CallFunction("draw_get_halign");
            GameVariable oldValign = Game.Engine.CallFunction("draw_get_valign");

            double eased = 1 - Math.Pow(1 - openAmount, 3);
            int guiWidth = Game.Engine.CallFunction("display_get_gui_width");
            int guiHeight = Game.Engine.CallFunction("display_get_gui_height");
            PanelOrigin(out int x, out int y);
            y += (int)((1 - eased) * SlideDistance);

            Game.Engine.CallFunction("draw_set_halign", 0);
            Game.Engine.CallFunction("draw_set_valign", 0);
            DrawBox(0, 0, guiWidth, guiHeight, Dark, DimAlpha * eased);
            DrawPanelBackground(x, y, eased);

            Game.Engine.CallFunction("draw_set_alpha", eased);
            Game.Engine.CallFunction("draw_set_font", GML.GetAsset("fnt_title")!);
            DrawText(x + ContentLeft, y + TitleY, submenu == null ? "Office" : submenuTitle, White, TitleScale);
            Game.Engine.CallFunction("draw_set_font", GML.GetAsset("fnt_standard")!);

            DrawHeader(x, y, eased);
            DrawRows(x, y, eased);
            DrawScrollbar(x, y, eased);
            DrawFooter(x, y, eased);

            Game.Engine.CallFunction("draw_set_color", oldColor);
            Game.Engine.CallFunction("draw_set_alpha", oldAlpha);
            Game.Engine.CallFunction("draw_set_font", oldFont);
            Game.Engine.CallFunction("draw_set_halign", oldHalign);
            Game.Engine.CallFunction("draw_set_valign", oldValign);
        }

        private void DrawHeader(int x, int y, double eased)
        {
            if (submenu != null)
            {
                DrawText(x + ContentLeft, y + TabsY, "Scroll on a value or use Left/Right to change it", Grey, TextScale);
                return;
            }

            for (int i = 0; i < TabNames.Length; i++)
            {
                DrawText(x + ContentLeft + i * TabWidth, y + TabsY, TabNames[i], i == tab ? Gold : Grey, TextScale);
            }

            int underlineX = x + ContentLeft + (int)(smoothTab * TabWidth);
            DrawBox(underlineX, y + TabsY + TabHeight, underlineX + TabWidth - 14, y + TabsY + TabHeight + 1, Gold, eased);
            Game.Engine.CallFunction("draw_set_alpha", eased);
        }

        private void DrawRows(int x, int y, double eased)
        {
            List<PanelRow> rows = CurrentRows();
            int top = y + RowsTop;

            double highlightRow = smoothSelected - smoothScroll;
            if (rows.Count > 0 && highlightRow > -1 && highlightRow < VisibleRows)
            {
                double visibility = RowVisibility(highlightRow);
                int barY = top + (int)(highlightRow * RowHeight);
                DrawBox(x + ContentLeft - 4, barY - 1, x + ScrollbarX - 4, barY + RowHeight - 2, White, HighlightAlpha * visibility * eased);
            }

            int first = Math.Max(0, (int)Math.Floor(smoothScroll));
            int last = Math.Min(rows.Count - 1, first + VisibleRows);
            for (int i = first; i <= last; i++)
            {
                double position = i - smoothScroll;
                double visibility = RowVisibility(position);
                if (visibility <= 0)
                {
                    continue;
                }

                Game.Engine.CallFunction("draw_set_alpha", visibility * eased);
                DrawRow(x, top + (int)(position * RowHeight), rows[i], i == selected);
            }

            Game.Engine.CallFunction("draw_set_alpha", eased);
        }

        private static double RowVisibility(double position)
        {
            if (position < 0)
            {
                return Math.Clamp(1 + position, 0, 1);
            }

            return position > VisibleRows - 1 ? Math.Clamp(VisibleRows - position, 0, 1) : 1;
        }

        private static void DrawRow(int x, int y, PanelRow row, bool isSelected)
        {
            string text = row.Label();
            int split = text.LastIndexOf(": ", StringComparison.Ordinal);
            string label = split >= 0 ? text.Substring(0, split) : text;
            string value = split >= 0 ? text.Substring(split + 2) : "";
            int labelColor = isSelected ? Gold : row.Activate == null && row.Adjust == null ? Grey : White;

            DrawText(x + ContentLeft, y, label, labelColor, TextScale);
            if (value.Length == 0)
            {
                return;
            }

            float width = Game.Engine.CallFunction("string_width", value).ToFloat() * (float)TextScale;
            int valueX = x + ValueRight - (int)width;
            DrawText(valueX, y, value, isSelected ? Gold : White, TextScale);
            if (isSelected && row.Adjust != null)
            {
                DrawText(valueX - 10, y, "<", Grey, TextScale);
                DrawText(x + ValueRight + 4, y, ">", Grey, TextScale);
            }
        }

        private void DrawScrollbar(int x, int y, double eased)
        {
            int count = CurrentRows().Count;
            if (count <= VisibleRows)
            {
                return;
            }

            int trackTop = y + RowsTop;
            int trackHeight = VisibleRows * RowHeight - 2;
            int thumbHeight = Math.Max(10, trackHeight * VisibleRows / count);
            int thumbTop = trackTop + (int)((trackHeight - thumbHeight) * smoothScroll / (count - VisibleRows));

            DrawBox(x + ScrollbarX, trackTop, x + ScrollbarX + ScrollbarWidth, trackTop + trackHeight, Dark, 0.3 * eased);
            DrawBox(x + ScrollbarX, thumbTop, x + ScrollbarX + ScrollbarWidth, thumbTop + thumbHeight, Gold, eased);
            Game.Engine.CallFunction("draw_set_alpha", eased);
        }

        private void DrawFooter(int x, int y, double eased)
        {
            GameVariable escape = GML.GetAsset("s_input_keyboard_escape")!;
            Game.Engine.CallFunction("draw_sprite_ext", escape, 0, x + ContentLeft + 6, y + FooterY + 5, IconScale, IconScale, 0, White, eased);
            DrawText(x + ContentLeft + 16, y + FooterY, submenu == null ? "Close" : "Back", Grey, TextScale);

            if (shownStatus.Length > 0 && statusTimer > 0)
            {
                double fade = Math.Min(1, statusTimer / (double)StatusFadeFrames);
                Game.Engine.CallFunction("draw_set_alpha", fade * eased);
                DrawText(x + 70, y + FooterY, shownStatus, Gold, TextScale);
                Game.Engine.CallFunction("draw_set_alpha", eased);
            }
        }

        private static void DrawBox(int x1, int y1, int x2, int y2, int color, double alpha)
        {
            Game.Engine.CallFunction("draw_set_color", color);
            Game.Engine.CallFunction("draw_set_alpha", alpha);
            Game.Engine.CallFunction("draw_rectangle", x1, y1, x2, y2, false);
        }

        private static void DrawText(int x, int y, string text, int color, double scale)
        {
            GameVariable script = Game.Engine.GetGlobalObject()["draw_text_outline"];
            Game.Engine.CallFunction("script_execute", script, x, y, text, scale, scale, color);
        }

        private static void DrawPanelBackground(int x, int y, double alpha)
        {
            try
            {
                GameVariable bg = GML.GetAsset("s_inventory_bg")!;
                Game.Engine.CallFunction("draw_sprite_stretched_ext", bg, 0, x, y, PanelWidth, PanelHeight, White, alpha);
            }
            catch (Exception ex)
            {
                Framework.PrintEx(AurieLogSeverity.Warning, $"[Office] Using plain panel: {ex.Message}");
                DrawBox(x, y, x + PanelWidth, y + PanelHeight, PanelFallbackColor, alpha);
            }
        }
    }
}
