using AurieSharpInterop;
using System.Diagnostics;
using System.Reflection;
using YYTKInterop;

namespace FrameBridge
{
    public static class Main
    {
        private const string InputEvent = "o_input_Draw_64";
        private const string EnterInput = "enter";
        private const int StartupDelayFrames = 30;
        private const int LeftMouseButton = 1;
        private const int EnterKey = 13;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly Stopwatch Timer = new();
        private static MethodInfo? bubbleOnFrame;
        private static MethodInfo? inputOnFrame;
        private static MethodInfo? inputInitialize;
        private static MethodInfo? inputIsPressed;
        private static MethodInfo? inputIsInitialized;
        private static MethodInfo? menuHandleInput;
        private static PropertyInfo? mainMenu;
        private static int frame;
        private static bool failed;
        private static bool inputFailed;
        private static bool nativeFramesWork;

        public static AurieStatus InitializeMod(AurieManagedModule module)
        {
            Game.Events.OnGameEvent += OnGameEvent;
            Game.Events.OnFrame += OnNativeFrame;
            return AurieStatus.Success;
        }

        public static void UnloadMod(AurieManagedModule module)
        {
            Game.Events.OnGameEvent -= OnGameEvent;
            Game.Events.OnFrame -= OnNativeFrame;
        }

        private static void OnNativeFrame(int frameNumber, double deltaTime)
        {
            if (!nativeFramesWork)
            {
                nativeFramesWork = true;
                Framework.PrintEx(AurieLogSeverity.Info, "[FrameBridge] Native frame events work, standing down");
            }
        }

        private static void OnGameEvent(CodeExecutionContext context)
        {
            if (failed || nativeFramesWork || !context.Name.Contains(InputEvent))
            {
                return;
            }

            frame++;
            if (frame <= StartupDelayFrames)
            {
                return;
            }

            try
            {
                if (bubbleOnFrame == null)
                {
                    FindBubbleLibMembers();
                }

                double deltaMilliseconds = Timer.IsRunning ? Timer.Elapsed.TotalMilliseconds : 0;
                Timer.Restart();
                object[] arguments = { frame, deltaMilliseconds };
                bubbleOnFrame!.Invoke(null, arguments);
                UpdateInput(context, arguments);
                HandleDirectPress();
            }
            catch (Exception ex)
            {
                failed = true;
                Framework.PrintEx(AurieLogSeverity.Error, $"[FrameBridge] Stopped: {ex}");
            }
        }

        private static void UpdateInput(CodeExecutionContext context, object[] arguments)
        {
            if (inputFailed)
            {
                return;
            }

            try
            {
                if (!(bool)inputIsInitialized!.Invoke(null, null)!)
                {
                    inputInitialize!.Invoke(null, new object[] { context.Self });
                    Framework.PrintEx(AurieLogSeverity.Info, "[FrameBridge] Initialized input");
                }

                inputOnFrame!.Invoke(null, arguments);
            }
            catch (Exception ex)
            {
                inputFailed = true;
                Framework.PrintEx(AurieLogSeverity.Error, $"[FrameBridge] Input polling stopped: {ex}");
            }
        }

        private static void HandleDirectPress()
        {
            bool clicked = Game.Engine.CallFunction("mouse_check_button_pressed", LeftMouseButton);
            bool enterPressed = Game.Engine.CallFunction("keyboard_check_pressed", EnterKey);
            if (!clicked && !enterPressed)
            {
                return;
            }

            if (!inputFailed && (bool)inputIsPressed!.Invoke(null, new object[] { EnterInput })!)
            {
                return;
            }

            object? menu = mainMenu!.GetValue(null);
            if (menu != null)
            {
                menuHandleInput!.Invoke(menu, new object[] { EnterInput });
            }
        }

        private static void FindBubbleLibMembers()
        {
            Assembly bubbleLib = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "BubbleLib");
            Type bubble = bubbleLib.GetType("BubbleLib.Bubble", true)!;
            Type input = bubbleLib.GetType("BubbleLib.Core.InputController", true)!;
            Type menu = bubbleLib.GetType("BubbleLib.GUI.Menu", true)!;

            inputOnFrame = input.GetMethod("OnFrame", AnyStatic)!;
            inputInitialize = input.GetMethod("Initialize", AnyStatic)!;
            inputIsPressed = input.GetMethod("IsPressed", AnyStatic)!;
            inputIsInitialized = input.GetProperty("IsInitialized", AnyStatic)!.GetGetMethod(true)!;
            menuHandleInput = menu.GetMethod("HandleInput", AnyInstance)!;
            mainMenu = bubble.GetProperty("MainMenu", AnyStatic)!;
            bubbleOnFrame = bubble.GetMethod("OnFrame", AnyStatic)!;
        }
    }
}
