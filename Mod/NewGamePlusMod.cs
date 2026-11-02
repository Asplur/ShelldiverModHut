using AurieSharpInterop;
using BubbleLib;
using BubbleLib.Core;
using BubbleLib.GUI;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using YYTKInterop;

namespace NewGamePlus
{
    public class NewGamePlusMod : BubbleMod
    {
        private const string HeaderPrefix = "2F0100000100000001000000";
        private const double CarryFraction = 0.1;
        private const int BonusPerCycle = 100;

        private static readonly HashSet<string> KeepKeys = new()
        {
            "is_tutorial_finished",
            "current_unix_time",
            "hammock_color",
            "hammock_colors",
            "buy_ask_counter",
            "wishlist_ask_counter",
            "oxygen_resource_permanent_increase",
            "oxygen_jellyfish_permanent_increase",
            "_all_inventories",
            "skills_saved"
        };

        public NewGamePlusMod(AurieManagedModule module) : base(module)
        {
        }

        public override string Name => "New Game+";

        public override void Initialize()
        {
            Bubble.MainMenu!.AddMenuItem("", new MenuItem(Name, StartNewGamePlus));
        }

        private static void StartNewGamePlus()
        {
            bool confirmed = Game.Engine.CallFunction("show_question", "Start New Game+?\nSkills, unlocks, items and most currencies will be reset.\nA backup is made first, then the game closes.");
            if (!confirmed)
            {
                return;
            }

            try
            {
                ApplyReset();
                Game.Engine.CallFunction("game_end");
            }
            catch (Exception ex)
            {
                Game.Engine.CallFunction("show_message", $"New Game+ failed, your save was not changed.\n{ex.Message}");
            }
        }

        private static void ApplyReset()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Shelldiver");
            string savePath = Path.Combine(root, "save", "Game.json");
            string cyclePath = Path.Combine(root, "ngplus_cycle.txt");
            string backupDir = Path.Combine(root, "ngplus_backups");

            string hex = File.ReadAllText(savePath).Trim();
            if (!hex.StartsWith(HeaderPrefix))
            {
                throw new InvalidDataException("The save format is not recognised.");
            }

            string json = Encoding.UTF8.GetString(Convert.FromHexString(hex.Substring(32)));
            JsonObject save = JsonNode.Parse(json)!.AsObject();

            int cycle = File.Exists(cyclePath) ? int.Parse(File.ReadAllText(cyclePath).Trim()) + 1 : 1;
            double oldCoins = save["shell_coins"]?.GetValue<double>() ?? 0;

            ResetValues(save);
            save["shell_coins"] = Math.Floor(oldCoins * CarryFraction) + BonusPerCycle * cycle;

            string output = save.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            byte[] outputBytes = Encoding.UTF8.GetBytes(output);
            string length = Convert.ToHexString(BitConverter.GetBytes((uint)outputBytes.Length));

            Directory.CreateDirectory(backupDir);
            File.Copy(savePath, Path.Combine(backupDir, $"Game_{DateTime.Now:yyyyMMdd_HHmmss}.json"));
            File.WriteAllText(savePath, HeaderPrefix + length + Convert.ToHexString(outputBytes), Encoding.ASCII);
            File.WriteAllText(cyclePath, cycle.ToString());
        }

        private static void ResetValues(JsonObject save)
        {
            foreach (string key in save.Select(pair => pair.Key).ToList())
            {
                if (KeepKeys.Contains(key))
                {
                    continue;
                }

                JsonValueKind? kind = save[key]?.GetValueKind();
                if (kind == JsonValueKind.True || kind == JsonValueKind.False)
                {
                    save[key] = false;
                }
                else if (kind == JsonValueKind.Number)
                {
                    save[key] = 0;
                }
            }

            save["last_biome_visited"] = -2;
            save["is_tutorial_finished"] = true;
            save["skills_saved"] = new JsonObject();

            foreach (var inventory in save["_all_inventories"]!.AsObject())
            {
                foreach (JsonNode? slot in inventory.Value!["slots"]!.AsArray())
                {
                    slot!["id"] = -1;
                    slot["quantity"] = 0;
                    slot["type"] = -1;
                }
            }
        }
    }
}
