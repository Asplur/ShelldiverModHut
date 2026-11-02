using AurieSharpInterop;
using BubbleLib.Core;
using System.Text;
using YYTKInterop;

namespace TrainerMod
{
    public class TrainerMod : BubbleMod
    {
        private const string FrameEvent = "o_input_Step_1";
        private const string CheatStruct = "debug_cheats";
        private const int DumpValueLimit = 2000;
        private const double MultiplierStep = 0.25;
        private const double MaxMultiplier = 20;
        private const int CoinStep = 10000;
        private const int CoinBigStep = 1000000;
        private const int GameSpeedStep = 15;
        private const int NormalGameSpeed = 60;

        private static readonly HashSet<string> UsefulDevOptions = new()
        {
            "unlimited_oxygen", "all_skills_free", "faster_cutscenes", "skip_intro", "skip_transition", "ignore_tutorial"
        };

        private static readonly string[] TargetObjects = { "par_player", "par_tool", "o_bubble_gun", "o_camera" };
        private static readonly string DumpPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Shelldiver", "trainer_dump.txt");

        private sealed class Row
        {
            public required Func<string> Label;
            public Action<int>? Adjust;
            public Func<string>? Activate;
            public bool IsToggle;
        }

        private sealed class Multiplier
        {
            public required string[] Variables;
            public double Value = 1;
            public bool NeedsRestore;
        }

        private sealed class Forced
        {
            public required string[] Variables;
            public double Value;
            public bool Enabled;
        }

        private readonly record struct Target(string Key, GameVariable? Instance);

        private readonly List<Row> rows = new();
        private readonly List<Multiplier> multipliers = new();
        private readonly List<Forced> forced = new();
        private readonly Dictionary<string, (double Base, double Written)> originals = new();
        private readonly HashSet<string> reportedErrors = new();
        private bool failed;

        public TrainerMod(AurieManagedModule module) : base(module)
        {
        }

        public override string Name => "Trainer";

        public override void Initialize()
        {
            AddMultiplier("Run speed", "walk_speed");
            AddMultiplier("Swim speed", "swim_speed");
            AddMultiplier("Jump height", "yjump_speed");
            AddMultiplier("Gravity", "ygravity");
            AddMultiplier("Acceleration", "acceleration");
            AddMultiplier("Pickup range", "total_bonus_player_pickup_range");
            AddMultiplier("Bubble damage", "total_bonus_bubble_damage");
            AddMultiplier("Bubble range", "total_bonus_bubble_life_time");
            AddMultiplier("Bubble speed", "total_bonus_bubble_projectile_speed");
            AddMultiplier("Bubble size", "total_bonus_bubble_projectile_size");
            AddMultiplier("Bubble fire rate", "total_bonus_bubble_fire_rate");
            AddMultiplier("Bubble reload", "total_bonus_bubble_reload_speed");
            AddMultiplier("Plasma damage", "total_bonus_plasma_damage");
            AddMultiplier("Plasma range", "total_bonus_plasma_range");
            AddMultiplier("Plasma fire rate", "total_bonus_plasma_fire_rate");
            AddMultiplier("Plasma reload", "total_bonus_plasma_reload_speed");
            AddMultiplier("Max oxygen", "oxygen_max");
            AddMultiplier("Oxygen regen", "total_bonus_player_oxygen_regen_amount");
            AddMultiplier("Shop sell speed", "total_bonus_store_sell_speed");
            AddMultiplier("Chest speed", "total_bonus_chest_speed");
            AddMultiplier("Transmute speed", "total_bonus_transmutate_speed");
            AddMultiplier("Alchemy speed", "total_bonus_alchemy_speed");

            AddToggle("No oxygen drain", 1, "oxygen_invulnerable");
            AddToggle("Invulnerable", 1, "invulnerable");
            AddToggle("Infinite ammo", 1, "do_ignore_ammo_consumption");
            AddCheatRows();

            AddCurrency("Shell coins", "shell_coins");
            AddCurrency("Scrap", "scrap_coins");
            AddCurrency("Crystal", "crystal_coins");
            AddCurrency("Mushroom", "mushroom_coins");
            AddCurrency("Obsidian", "obsidian_coins");
            AddCurrency("Flask", "flask_coins");
            AddCurrency("Shop shells", "store_shell_coins");

            AddSetting("Depths unlocked", "depths_unlocked_effected", 1);
            AddSetting("Prestige level", "prestige_level", 1);

            rows.Add(new Row
            {
                Label = () => $"Game speed: {GameSpeed()} FPS",
                Adjust = direction => Game.Engine.CallFunction("game_set_speed", Math.Max(GameSpeedStep, GameSpeed() + direction * GameSpeedStep), 0),
                Activate = () =>
                {
                    Game.Engine.CallFunction("game_set_speed", NormalGameSpeed, 0);
                    return "Game speed reset";
                }
            });

            rows.Add(new Row
            {
                Label = () => "Refill oxygen",
                Activate = RefillOxygen
            });

            rows.Add(new Row
            {
                Label = () => "Dump variables to file",
                Activate = DumpVariables
            });

            Game.Events.OnGameEvent += OnGameEvent;
        }

        public int GetTrainerRowCount()
        {
            return rows.Count;
        }

        public string GetTrainerLabel(int index)
        {
            try
            {
                return rows[index].Label();
            }
            catch (Exception ex)
            {
                Report("label", ex);
                return "Error";
            }
        }

        public void AdjustTrainerRow(int index, int direction)
        {
            Row row = rows[index];
            if (row.Adjust != null)
            {
                row.Adjust(direction);
            }
            else if (row.IsToggle)
            {
                row.Activate?.Invoke();
            }
        }

        public string ActivateTrainerRow(int index)
        {
            return rows[index].Activate?.Invoke() ?? "";
        }

        private void AddMultiplier(string label, params string[] variables)
        {
            Multiplier multiplier = new() { Variables = variables };
            multipliers.Add(multiplier);
            rows.Add(new Row
            {
                Label = () => FindTargets(variables).Any() ? $"{label}: x{multiplier.Value:0.##}" : $"{label}: N/A",
                Adjust = direction =>
                {
                    multiplier.Value = Math.Clamp(multiplier.Value + direction * MultiplierStep, MultiplierStep, MaxMultiplier);
                    multiplier.NeedsRestore = true;
                },
                Activate = () =>
                {
                    multiplier.Value = 1;
                    multiplier.NeedsRestore = true;
                    return $"{label} reset";
                }
            });
        }

        private void AddToggle(string label, double value, params string[] variables)
        {
            Forced entry = new() { Variables = variables, Value = value };
            forced.Add(entry);
            rows.Add(new Row
            {
                IsToggle = true,
                Label = () => FindTargets(variables).Any() ? $"{label}: {(entry.Enabled ? "On" : "Off")}" : $"{label}: N/A",
                Activate = () =>
                {
                    entry.Enabled = !entry.Enabled;
                    if (!entry.Enabled)
                    {
                        WriteAll(variables, 0);
                    }

                    return $"{label} {(entry.Enabled ? "on" : "off")}";
                }
            });
        }

        private void AddCheatRows()
        {
            try
            {
                GameVariable cheats = Game.Engine.GetGlobalObject()[CheatStruct];
                GameVariable names = Game.Engine.CallFunction("struct_get_names", cheats);
                foreach (GameVariable entry in names.ToArrayView())
                {
                    string key = entry.ToString();
                    if (!UsefulDevOptions.Contains(key))
                    {
                        continue;
                    }

                    string label = "Dev: " + key.Replace('_', ' ');
                    rows.Add(new Row
                    {
                        IsToggle = true,
                        Label = () => $"{label}: {(ReadCheat(key) ? "On" : "Off")}",
                        Activate = () =>
                        {
                            bool enabled = !ReadCheat(key);
                            Game.Engine.CallFunction("struct_set", Game.Engine.GetGlobalObject()[CheatStruct], key, enabled ? 1 : 0);
                            return $"{label} {(enabled ? "on" : "off")}";
                        }
                    });
                }

                Framework.PrintEx(AurieLogSeverity.Info, "[Trainer] Cheats: " + string.Join(", ", names.ToArrayView().Select(entry => entry.ToString())));
            }
            catch (Exception ex)
            {
                Framework.PrintEx(AurieLogSeverity.Warning, $"[Trainer] Could not read {CheatStruct}: {ex.Message}");
            }
        }

        private static bool ReadCheat(string key)
        {
            GameVariable value = Game.Engine.CallFunction("struct_get", Game.Engine.GetGlobalObject()[CheatStruct], key);
            return value.ToFloat() != 0;
        }

        private void AddCurrency(string label, string variable)
        {
            rows.Add(new Row
            {
                Label = () => ReadFirst(variable) is double value ? $"{label}: {value:N0}" : $"{label}: N/A",
                Adjust = direction => AddToVariable(variable, direction * CoinStep),
                Activate = () =>
                {
                    AddToVariable(variable, CoinBigStep);
                    return $"Added {CoinBigStep:N0} {label}";
                }
            });
        }

        private void AddSetting(string label, string variable, int step)
        {
            rows.Add(new Row
            {
                Label = () => ReadFirst(variable) is double value ? $"{label}: {value:0}" : $"{label}: N/A",
                Adjust = direction => AddToVariable(variable, direction * step)
            });
        }

        private static int GameSpeed()
        {
            return Game.Engine.CallFunction("game_get_speed", 0);
        }

        private string RefillOxygen()
        {
            if (ReadFirst("oxygen_max") is not double max)
            {
                return "Oxygen not found here";
            }

            WriteAll(new[] { "oxygen", "oxygen_real" }, max);
            return "Oxygen refilled";
        }

        private void AddToVariable(string variable, double amount)
        {
            if (ReadFirst(variable) is double value)
            {
                WriteAll(new[] { variable }, Math.Max(0, value + amount));
            }
        }

        private void OnGameEvent(CodeExecutionContext context)
        {
            if (failed || !context.Name.Contains(FrameEvent))
            {
                return;
            }

            try
            {
                foreach (Multiplier multiplier in multipliers)
                {
                    if (multiplier.Value != 1 || multiplier.NeedsRestore)
                    {
                        ApplyMultiplier(multiplier);
                    }
                }

                foreach (Forced entry in forced)
                {
                    if (entry.Enabled)
                    {
                        WriteAll(entry.Variables, entry.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                failed = true;
                Framework.PrintEx(AurieLogSeverity.Error, $"[Trainer] Stopped: {ex}");
            }
        }

        private void ApplyMultiplier(Multiplier multiplier)
        {
            foreach (Target target in FindTargets(multiplier.Variables))
            {
                string name = target.Key.Split('|')[0];
                double? read = ReadTarget(target, name);
                if (read is not double current)
                {
                    continue;
                }

                if (!originals.TryGetValue(target.Key, out var state) || Math.Abs(current - state.Written) > 0.0001)
                {
                    state = (current, current);
                }

                double value = state.Base * multiplier.Value;
                WriteTarget(target, name, value);
                originals[target.Key] = (state.Base, value);
            }

            multiplier.NeedsRestore = false;
        }

        private IEnumerable<Target> FindTargets(string[] variables)
        {
            foreach (string name in variables)
            {
                if (Game.Engine.CallFunction("variable_global_exists", name))
                {
                    yield return new Target($"{name}|global", null);
                }

                foreach (string objectName in TargetObjects)
                {
                    GameVariable? asset = GML.GetAsset(objectName);
                    if (asset == null)
                    {
                        continue;
                    }

                    int count = Game.Engine.CallFunction("instance_number", asset);
                    for (int i = 0; i < count; i++)
                    {
                        GameVariable instance = Game.Engine.CallFunction("instance_find", asset, i);
                        if (GML.HasInstanceVariable(instance, name))
                        {
                            yield return new Target($"{name}|{instance}", instance);
                        }
                    }
                }
            }
        }

        private double? ReadFirst(string variable)
        {
            foreach (Target target in FindTargets(new[] { variable }))
            {
                double? value = ReadTarget(target, variable);
                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        private double? ReadTarget(Target target, string name)
        {
            try
            {
                GameVariable? value = target.Instance == null
                    ? Game.Engine.CallFunction("variable_global_get", name)
                    : GML.GetInstanceVariable(target.Instance, name);
                return value?.ToFloat();
            }
            catch (Exception ex)
            {
                Report(name, ex);
                return null;
            }
        }

        private static void WriteTarget(Target target, string name, double value)
        {
            if (target.Instance == null)
            {
                Game.Engine.CallFunction("variable_global_set", name, value);
            }
            else
            {
                GML.SetInstanceVariable(target.Instance, name, value);
            }
        }

        private void WriteAll(string[] variables, double value)
        {
            foreach (Target target in FindTargets(variables))
            {
                WriteTarget(target, target.Key.Split('|')[0], value);
            }
        }

        private void Report(string name, Exception ex)
        {
            if (reportedErrors.Add(name))
            {
                Framework.PrintEx(AurieLogSeverity.Warning, $"[Trainer] Could not read {name}: {ex.Message}");
            }
        }

        private string DumpVariables()
        {
            StringBuilder text = new();
            text.AppendLine("GLOBAL");
            foreach (var member in Game.Engine.GetGlobalObject().Members)
            {
                AppendVariable(text, member.Key, member.Value);
            }

            foreach (string objectName in TargetObjects)
            {
                GameVariable? asset = GML.GetAsset(objectName);
                if (asset == null)
                {
                    continue;
                }

                int count = Game.Engine.CallFunction("instance_number", asset);
                for (int i = 0; i < count; i++)
                {
                    GameVariable instance = Game.Engine.CallFunction("instance_find", asset, i);
                    text.AppendLine($"INSTANCE {objectName} {instance}");
                    GameVariable names = Game.Engine.CallFunction("variable_instance_get_names", instance);
                    foreach (GameVariable name in names.ToArrayView())
                    {
                        string key = name.ToString();
                        AppendVariable(text, key, GML.GetInstanceVariable(instance, key));
                    }
                }
            }

            File.WriteAllText(DumpPath, text.ToString());
            return "Saved trainer_dump.txt";
        }

        private static void AppendVariable(StringBuilder text, string name, GameVariable? value)
        {
            string shown;
            try
            {
                shown = value?.ToString() ?? "null";
            }
            catch (Exception)
            {
                shown = "unreadable";
            }

            text.AppendLine($"  {name} = {(shown.Length > DumpValueLimit ? shown.Substring(0, DumpValueLimit) : shown)}");
        }
    }
}
