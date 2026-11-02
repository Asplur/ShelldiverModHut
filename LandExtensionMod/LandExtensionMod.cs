using AurieSharpInterop;
using BubbleLib.Constants;
using BubbleLib.Core;
using YYTKInterop;

namespace LandExtensionMod
{
    public class LandExtensionMod : BubbleMod
    {
        private const string FrameEvent = "o_input_Step_1";
        private const string MainLayer = "tl_main";
        private const string FrontLayer = "tl_main_front";
        private const string WallObject = "o_invisible_block";
        private const string CameraObject = "o_camera_offset";

        private const int TileSize = 16;
        private const int TileTop = 32;
        private const int TileFill = 62;
        private const int TileTopEdge = 33;
        private const int TileSideEdge = 63;

        private const int FirstColumn = 250;
        private const int LastColumn = 269;
        private const int TopRow = 25;
        private const int LastFillRow = 31;
        private const int LastFrontRow = 28;

        private const int GroundY = 400;
        private const int OldWallX = 4080;
        private const int NewWallX = 4288;
        private const int OldCameraX = 3952;
        private const int NewCameraX = 4160;

        private const int TileTopLeftEdge = 31;
        private const int TileLeftEdge = 61;
        private const int WestFirstColumn = 124;
        private const int WestLastTopColumn = 143;
        private const int WestLastColumn = 145;
        private const int WestLastFillRow = 32;
        private const int WestLastFrontRow = 31;
        private const int WestOldWallX = 2225;
        private const int WestNewWallX = 1984;
        private const int WestFloorEndX = 2288;
        private const int WestOldCameraX = 2175;
        private const int WestNewCameraX = 1934;

        private string lastRoom = "";
        private bool failed;

        public LandExtensionMod(AurieManagedModule module) : base(module)
        {
        }

        public override string Name => "Land Extension";

        public override void Initialize()
        {
            for (int x = OldWallX; x < NewWallX; x += TileSize)
            {
                GML.AddObjectToRoom(Rooms.Main_island, WallObject, x, GroundY);
            }

            for (int x = WestNewWallX + TileSize; x < WestFloorEndX; x += TileSize)
            {
                GML.AddObjectToRoom(Rooms.Main_island, WallObject, x, GroundY);
            }

            Game.Events.OnGameEvent += OnGameEvent;
        }

        private void OnGameEvent(CodeExecutionContext context)
        {
            if (failed || !context.Name.Contains(FrameEvent))
            {
                return;
            }

            try
            {
                string room = Game.Engine.CallFunction("room_get_name", Game.Engine.GetBuiltinVariable("room", Game.Engine.GetGlobalObject(), 0));
                if (room == lastRoom)
                {
                    return;
                }

                lastRoom = room;
                if (room == Rooms.Main_island)
                {
                    PaintLand();
                    MoveInstance(WallObject, OldWallX, 0, NewWallX);
                    MoveInstance(CameraObject, OldCameraX, 0, NewCameraX);
                    PaintWestLand();
                    MoveInstance(WallObject, WestOldWallX, 0, WestNewWallX);
                    MoveInstance(CameraObject, WestOldCameraX, 0, WestNewCameraX);
                    Framework.PrintEx(AurieLogSeverity.Info, "[LandExtension] Applied");
                }
            }
            catch (Exception ex)
            {
                failed = true;
                Framework.PrintEx(AurieLogSeverity.Error, $"[LandExtension] Stopped: {ex}");
            }
        }

        private static void PaintLand()
        {
            GameVariable main = GetTilemap(MainLayer);
            GameVariable front = GetTilemap(FrontLayer);

            for (int column = FirstColumn; column <= LastColumn; column++)
            {
                bool isEdge = column == LastColumn;
                Game.Engine.CallFunction("tilemap_set", main, isEdge ? TileTopEdge : TileTop, column, TopRow);
                for (int row = TopRow + 1; row <= LastFillRow; row++)
                {
                    Game.Engine.CallFunction("tilemap_set", main, isEdge ? TileSideEdge : TileFill, column, row);
                }

                for (int row = TopRow; row <= LastFrontRow; row++)
                {
                    Game.Engine.CallFunction("tilemap_set", front, 0, column, row);
                }
            }
        }

        private static void PaintWestLand()
        {
            GameVariable main = GetTilemap(MainLayer);
            GameVariable front = GetTilemap(FrontLayer);

            for (int column = WestFirstColumn; column <= WestLastColumn; column++)
            {
                bool isEdge = column == WestFirstColumn;
                if (column <= WestLastTopColumn)
                {
                    Game.Engine.CallFunction("tilemap_set", main, isEdge ? TileTopLeftEdge : TileTop, column, TopRow);
                }

                for (int row = TopRow + 1; row <= WestLastFillRow; row++)
                {
                    Game.Engine.CallFunction("tilemap_set", main, isEdge ? TileLeftEdge : TileFill, column, row);
                }

                for (int row = TopRow; row <= WestLastFrontRow; row++)
                {
                    Game.Engine.CallFunction("tilemap_set", front, 0, column, row);
                }
            }
        }

        private static GameVariable GetTilemap(string layerName)
        {
            GameVariable layer = Game.Engine.CallFunction("layer_get_id", layerName);
            return Game.Engine.CallFunction("layer_tilemap_get_id", layer);
        }

        private static void MoveInstance(string objectName, int oldX, int y, int newX)
        {
            GameVariable asset = GML.GetAsset(objectName)!;
            int count = Game.Engine.CallFunction("instance_number", asset);
            for (int i = 0; i < count; i++)
            {
                GameVariable instance = Game.Engine.CallFunction("instance_find", asset, i);
                float x = GML.GetInstanceVariable(instance, "x")!.ToFloat();
                float instanceY = GML.GetInstanceVariable(instance, "y")!.ToFloat();
                if (Math.Abs(x - oldX) < 1 && Math.Abs(instanceY - y) < 1)
                {
                    GML.SetInstanceVariable(instance, "x", newX);
                    return;
                }
            }

            Framework.PrintEx(AurieLogSeverity.Warning, $"[LandExtension] Did not find {objectName} at {oldX}");
        }
    }
}
