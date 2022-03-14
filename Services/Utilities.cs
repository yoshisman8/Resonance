using Dice;
using DSharpPlus.Entities;
using LiteDB;
using Resonance.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;

namespace Resonance.Services
{
    public class Utilities
    {
        private LiteDatabase database;
        public Utilities(LiteDatabase _db)
        {
            database = _db;
        }
        public User GetUser(ulong Id)
        {
            var col = database.GetCollection<User>("Users");

            if (col.Exists(x => x.ID == Id))
            {
                return col.Include(x => x.Active).FindOne(x => x.ID == Id);
            }
            else
            {
                var User = new User()
                {
                    ID = Id
                };
                col.Insert(User);
                col.EnsureIndex(x => x.ID);

                return col.Include(x => x.Active).FindOne(x => x.ID == Id);
            }
        }
        public Encounter GetEncounter(ulong channel)
        {
            var col = database.GetCollection<Encounter>("Encounters");

            if (col.Exists(x => x.ChannelId == channel))
            {
                return col.Include(x => x.Active).FindOne(x => x.ChannelId == channel);
            }
            else
            {
                var Enc = new Encounter()
                {
                    ChannelId = channel
                };
                col.Insert(Enc);
                col.EnsureIndex(x => x.ChannelId);

                return col.Include(x => x.Active).FindOne(x => x.ChannelId == channel);
            }
        }

        public Character GetActor(int id)
        {
            var actors = database.GetCollection<Character>("Characters");

            return actors.FindById(id);
        }


        public void UpdateUser(User U)
        {
            var col = database.GetCollection<User>("Users");
            col.Update(U);
        }
        public void UpdateActor(Character A)
        {
            var col = database.GetCollection<Character>("Characters");

            col.Update(A);
        }
        public void UpdateEncounter(Encounter e)
        {
            var col = database.GetCollection<Encounter>("Encounters");

            col.Update(e);
        }

        public DiscordEmbed EmbedRoll(Collections.RollData roll)
        {
            var builder = new DiscordEmbedBuilder();
            var sb = new StringBuilder();

            Character A = null;

            A = GetActor(roll.Actor);

            if (roll.dice.Length == 1) {
                builder.WithTitle($"{A.Name} makes a dire roll!");
            }
            else
            {
                builder.WithTitle($"{A.Name} makes a roll!");
            }

            builder.WithThumbnail(A.Image);

            builder.WithColor(new DiscordColor(A.Color));

            

            if (!roll.Technique.NullorEmpty())
            {
                string _Name = string.Join("", roll.Technique.Take(3));

                string _Desc = string.Join("", roll.Technique.Substring(3).Take(3));

                ActionType _Type = (ActionType)int.Parse(roll.Technique.Substring(6));

                Tech Technique = A.Techniques.Find(x => x.Name.StartsWith(_Name) && x.Description.StartsWith(_Desc) && x.Action == _Type);

                if (Technique != null)
                {
                    sb.AppendLine($"**[{Technique.Attribute} + {Technique.Skill}]**");

                    sb.AppendLine(string.Join(" ", roll.dice.Select(x => Dictionaries.GameDice[x]))+"\n");

                    int successes = roll.dice.Where(x => x >= 4).Count();

                    if (roll.dice.Length == 1)
                    {
                        sb.AppendLine($"**{(successes > 0 ? "Critical Success!" : "Critical Failure")}!**\n");
                    }
                    else
                    {
                        sb.AppendLine($"**{successes} Successe{(successes == 1 ? "" : "s")}!**\n");
                    }

                    sb.AppendLine($"**{Technique.Name}** {(Technique.Action == ActionType.Simple ? Dictionaries.Icons["emptyDot"] : Dictionaries.Icons["dot"])}");

                    sb.AppendLine($"> {Technique.Description}");
                }
                else
                {
                    sb.AppendLine(string.Join(" ", roll.dice.Select(x => Dictionaries.GameDice[x])));
                    int successes = roll.dice.Where(x => x >= 4).Count();

                    if (roll.dice.Length == 1)
                    {
                        sb.AppendLine($"**{(successes > 0 ? "Critical Success!" : "Critical Failure")}!**");
                    }
                    else
                    {
                        sb.AppendLine($"**{successes} Successe{(successes == 1 ? "" : "s")}!**");
                    }
                }
                
            }
            else
            {
                sb.AppendLine(string.Join(" ", roll.dice.Select(x => Dictionaries.GameDice[x])));
                int successes = roll.dice.Where(x => x >= 4).Count();

                if (roll.dice.Length == 1)
                {
                    sb.AppendLine($"**{(successes > 0 ? "Critical Success!" : "Critical Failure")}!**");
                }
                else
                {
                    sb.AppendLine($"**{successes} Successe{(successes == 1 ? "" : "s")}!**");
                }
            }

            builder.WithDescription(sb.ToString());
            return builder.Build();
        }
    
        public DiscordEmbed EmbedEncounter(Encounter e)
        {
            var builder = new DiscordEmbedBuilder();

            builder.WithTitle($"Encounter {(e.Initiated ? "Is underway!" : "is being prepared!")}");

            var sb = new StringBuilder();
            sb.AppendLine($"**Round {e.Round}**");
            sb.AppendLine("**Combatants**");

            foreach(var c in e.Combatants)
            {
                if(c.Equals(e.Current))
                {
                    if(c.Actor != null)
                    {
                        var A = GetActor(c.Actor.Id);

                        sb.AppendLine($"{Dictionaries.Icons["dot"]} - `{c.Initiative}` - {A.Name}");
                        sb.AppendLine($"> Health [{A.Health}/{A.GetMaxHealth()}]");
                        sb.AppendLine($"> {A.Health.Bar(A.GetMaxHealth(), false)}");
                    }
                    else
                    {
                        sb.AppendLine($"{Dictionaries.Icons["dot"]} - `{c.Initiative}` - {c.Name}");
                    }
                }
                else
                {
                    if (c.Actor != null)
                    {
                        var A = GetActor(c.Actor.Id);

                        sb.AppendLine($"{Dictionaries.Icons["emptyDot"]} - `{c.Initiative}` - {A.Name}");
                    }
                    else
                    {
                        sb.AppendLine($"{Dictionaries.Icons["emptyDot"]} - `{c.Initiative}` - {c.Name}");
                    }
                }
            }

            builder.WithDescription(sb.ToString());

            sb.Clear();


            var left = e.Combatants.Where(x => x.Position == Position.Ally);

            foreach(var c in left)
            {
                if (c.Actor != null)
                {
                    var A = GetActor(c.Actor.Id);

                    sb.AppendLine($"🔸 {A.Name}");
                }
                else
                {
                    sb.AppendLine($"🔸 {c.Name}");
                }
            }

            if(sb.Length > 0)
            {
                builder.AddField("◀️ Ally Far Range", sb.ToString(), true);
            }
            else
            {
                builder.AddField("◀️ Ally Far Range", "Empty",true);
            }
            sb.Clear();

            var center = e.Combatants.Where(x => x.Position == Position.Center);

            foreach (var c in center)
            {
                if (c.Actor != null)
                {
                    var A = GetActor(c.Actor.Id);

                    sb.AppendLine($"🔸 {A.Name}");
                }
                else
                {
                    sb.AppendLine($"🔸 {c.Name}");
                }
            }

            if (sb.Length > 0)
            {
                builder.AddField("Close ⏏️ Range", sb.ToString(), true);
            }
            else
            {
                builder.AddField("Close ⏏️ Range", "Empty", true);
            }
            sb.Clear();

            var right = e.Combatants.Where(x => x.Position == Position.Enemy);

            foreach (var c in right)
            {
                if (c.Actor != null)
                {
                    var A = GetActor(c.Actor.Id);

                    sb.AppendLine($"🔸 {A.Name}");
                }
                else
                {
                    sb.AppendLine($"🔸 {c.Name}");
                }
            }

            if (sb.Length > 0)
            {
                builder.AddField("Far Enemy Range ▶️", sb.ToString(), true);
            }
            else
            {
                builder.AddField("Far Enemy Range ▶️", "Empty", true);
            }
            sb.Clear();

            return builder.Build();
        }
    }

    public static class Dictionaries
    {
        public static Dictionary<int, string> Dots { get; set; } = new Dictionary<int, string>()
        {
            {0, ":zero:" },
            {1, ":one:" },
            {2, ":two:" },
            {3, ":three:" },
            {4, ":four:" },
            {5, ":five:" },
            {6, ":six:" },
            {7, ":seven:" },
            {8, ":eight:" },
            {9, ":nine:" }
        };
        public static Dictionary<string, string> Icons { get; set; } = new Dictionary<string, string>()
        {
            {"hp" ,"<:Health:827589986283028490>" },
            {"luck","<:Luck:948212068413210658>" },
            {"empty","<:Empty:685854267512455178>" },
            {"dot","<:dot:948213047615430706>" },
            {"emptyDot","<:EmptyRank:951131338470215712>" }
        };
        public static Dictionary<int, string> GameDice { get; set; } = new Dictionary<int, string>()
        {
            { 1, "<:GD6_1:948217647554789456>" },
            { 2, "<:GD6_2:948217647659614209>" },
            { 3, "<:GD6_3:948217647437320242>" },
            { 4, "<:GD6_4:948217647374409798>" },
            { 5, "<:GD6_5:948217647680589884>" },
            { 6, "<:GD6_6:948217647319879720>" }
        };
        public static Dictionary<int, string> d20 { get; set; } = new Dictionary<int, string>()
        {
            {20, "<:d20_20:663149799792705557>" },
            {19, "<:d20_19:663149782847586304>" },
            {18, "<:d20_18:663149770621190145>" },
            {17, "<:d20_17:663149758885396502>" },
            {16, "<:d20_16:663149470216749107>" },
            {15, "<:d20_15:663149458963300352>" },
            {14, "<:d20_14:663149447278100500>" },
            {13, "<:d20_13:663149437459234846>" },
            {12, "<:d20_12:663149424909746207>" },
            {11, "<:d20_11:663149398712123415>" },
            {10, "<:d20_10:663149389396574212>" },
            {9, "<:d20_9:663149377954775076>" },
            {8, "<:d20_8:663149293695139840>" },
            {7, "<:d20_7:663149292743032852>" },
            {6, "<:d20_6:663149290532634635>" },
            {5, "<:d20_5:663147362608480276>" },
            {4, "<:d20_4:663147362512011305>" },
            {3, "<:d20_3:663147362067415041>" },
            {2, "<:d20_2:663147361954037825>" },
            {1, "<:d20_1:663146691016523779>" }
        };
        public static Dictionary<int, string> d12 { get; set; } = new Dictionary<int, string>()
        {
            {12, "<:d12_12:663152540426174484>" },
            {11, "<:d12_11:663152540472442900>" },
            {10, "<:d12_10:663152540439019527>" },
            {9, "<:d12_9:663152540199682061>" },
            {8, "<:d12_8:663152540459728947>" },
            {7, "<:d12_7:663152540116058133>" },
            {6, "<:d12_6:663152540484894740>" },
            {5, "<:d12_5:663152540250144804>" },
            {4, "<:d12_4:663152540426305546>" },
            {3, "<:d12_3:663152540161933326>" },
            {2, "<:d12_2:663152538291404821>" },
            {1, "<:d12_1:663152538396393482>" }
        };
        public static Dictionary<int, string> d10 { get; set; } = new Dictionary<int, string>()
        {
            {10, "<:d10_10:663158741352579122>" },
            {9, "<:d10_9:663158741331476480>" },
            {8, "<:d10_8:663158741079687189>" },
            {7, "<:d10_7:663158742636036138>" },
            {6, "<:d10_6:663158741121761280>" },
            {5, "<:d10_5:663158740576632843>" },
            {4, "<:d10_4:663158740685553713>" },
            {3, "<:d10_3:663158740442415175>" },
            {2, "<:d10_2:663158740496810011>" },
            {1, "<:d10_1:663158740463255592>" }
        };
        public static Dictionary<int, string> d8 { get; set; } = new Dictionary<int, string>()
        {
            {8, "<:d8_8:663158785795162112>" },
            {7, "<:d8_7:663158785841561629>" },
            {6, "<:d8_6:663158785774190595>" },
            {5, "<:d8_5:663158785271005185>" },
            {4, "<:d8_4:663158785107296286>" },
            {3, "<:d8_3:663158785543503920>" },
            {2, "<:d8_2:663158785224867880>" },
            {1, "<:d8_1:663158784859963473>" }
        };
        public static Dictionary<int, string> d6 { get; set; } = new Dictionary<int, string>()
        {
            {6, "<:d6_6:663158852551835678>" },
            {5, "<:d6_5:663158852136599564>" },
            {4, "<:d6_4:663158856247148566>" },
            {3, "<:d6_3:663158852358766632>" },
            {2, "<:d6_2:663158852354834452>" },
            {1, "<:d6_1:663158852354572309>" }
        };
        public static Dictionary<int, string> d4 { get; set; } = new Dictionary<int, string>()
        {
            {4, "<:d4_4:663158852472274944>" },
            {3, "<:d4_3:663158852178411560>" },
            {2, "<:d4_2:663158851734077462>" },
            {1, "<:d4_1:663158851909976085>" }
        };
    }
    public static class Extensions
    {



        public static string Dots(this int value, int max)
        {
            int empty = Math.Max(0, max - value);

            string dots = string.Concat(Enumerable.Repeat(Dictionaries.Icons["dot"], value));
            dots += string.Concat(Enumerable.Repeat(Dictionaries.Icons["emptyDot"], empty));

            return dots;
        }
        public static string Bar(this int value, int max, bool luck)
        {
            var sb = new StringBuilder();

            if (max > 10)
            {
                decimal percent = ((decimal)Math.Max(Math.Min(value, max), 0) / (decimal)max) * 10;

                var diff = 10 - Math.Ceiling(percent);
                
                for (int i = 0; i < Math.Ceiling(percent); i++)
                {
                    sb.Append(luck ? Dictionaries.Icons["luck"] : Dictionaries.Icons["hp"]);
                }
                for (int i = 0; i < diff; i++)
                {
                    sb.Append(Dictionaries.Icons["empty"]);
                }
            }
            else
            {
                for (int i = 0; i < value; i++)
                {
                    sb.Append(luck ? Dictionaries.Icons["luck"] : Dictionaries.Icons["hp"]);
                }
                for (int i = 0; i < Math.Max(0,max-value); i++)
                {
                    sb.Append(Dictionaries.Icons["empty"]);
                }
            }

            return sb.ToString();

        }
        public static bool IsImageUrl(this string URL)
        {
            try
            {
                var req = (HttpWebRequest)HttpWebRequest.Create(URL);
                req.Method = "HEAD";
                using (var resp = req.GetResponse())
                {
                    return resp.ContentType.ToLower(CultureInfo.InvariantCulture)
                            .StartsWith("image/", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }
        public static bool NullorEmpty(this string _string)
        {
            if (_string == null) return true;
            if (_string == "") return true;
            else return false;
        }
        public static IEnumerable<string> SplitByLength(this string str, int maxLength)
        {
            for (int index = 0; index < str.Length; index += maxLength)
            {
                yield return str.Substring(index, Math.Min(maxLength, str.Length - index));
            }
        }
        public static string FirstCharToUpper(this string input) =>
                input switch
                {
                    null => throw new ArgumentNullException(nameof(input)),
                    "" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
                    _ => input.First().ToString().ToUpper() + input.Substring(1)
                };

        public static string ParseResult(this RollResult result)
        {
            var sb = new StringBuilder();

            foreach (var dice in result.Values)
            {
                switch (dice.DieType)
                {
                    case DieType.Normal:
                        switch (dice.NumSides)
                        {
                            case 4:
                                sb.Append(Dictionaries.d4[(int)dice.Value] + " ");
                                break;
                            case 6:
                                sb.Append(Dictionaries.d6[(int)dice.Value] + " ");
                                break;
                            case 8:
                                sb.Append(Dictionaries.d8[(int)dice.Value] + " ");
                                break;
                            case 10:
                                sb.Append(Dictionaries.d10[(int)dice.Value] + " ");
                                break;
                            case 12:
                                sb.Append(Dictionaries.d12[(int)dice.Value] + " ");
                                break;
                            case 20:
                                sb.Append(Dictionaries.d20[(int)dice.Value] + " ");
                                break;
                            default:
                                sb.Append(dice.Value);
                                break;
                        }
                        break;
                    case DieType.Special:
                        switch ((SpecialDie)dice.Value)
                        {
                            case SpecialDie.Add:
                                sb.Append("+ ");
                                break;
                            case SpecialDie.CloseParen:
                                sb.Append(") ");
                                break;
                            case SpecialDie.Comma:
                                sb.Append(", ");
                                break;
                            case SpecialDie.Divide:
                                sb.Append("/ ");
                                break;
                            case SpecialDie.Multiply:
                                sb.Append("* ");
                                break;
                            case SpecialDie.Negate:
                                sb.Append("- ");
                                break;
                            case SpecialDie.OpenParen:
                                sb.Append("( ");
                                break;
                            case SpecialDie.Subtract:
                                sb.Append("- ");
                                break;
                            case SpecialDie.Text:
                                sb.Append(dice.Data);
                                break;
                        }
                        break;
                    default:
                        sb.Append(dice.Value + " ");
                        break;
                }
            }

            return sb.ToString().Trim();
        }
    }
}
