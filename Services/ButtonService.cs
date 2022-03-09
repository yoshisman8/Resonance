using Dice;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using LiteDB;
using Resonance.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resonance.Services
{
    public class ButtonService
    {
        private LiteDatabase database;
        private Utilities utils;
        public ButtonService(DiscordClient client, LiteDatabase _db, Utilities _utils)
        {
            database = _db;
            utils = _utils;
        }

        public async Task HandleButtonAsync(DiscordClient c, ComponentInteractionCreateEventArgs e)
        {
            var u = utils.GetUser(e.User.Id);
            if (e.Id == "cancel")
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                        new DiscordInteractionResponseBuilder().WithContent("Operation Cancelled!"));
            }
            else if (e.Id.StartsWith("s"))
            {
                string[] args = e.Id.Split(",");
                int page = int.Parse(args[1]);

                int id = int.Parse(args[2]);

                var col = database.GetCollection<Character>("Characters");

                var C = col.FindById(id);

                if (C == null)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                        .WithContent("This character does not exist anymore!"));
                    return;
                }
                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, C.Render(page));
            }

            else if (e.Id.StartsWith("dl"))
            {
                int id = int.Parse(e.Id.Substring(2));

                var col = database.GetCollection<Character>("Characters");

                var C = col.FindById(id);

                var User = utils.GetUser(e.User.Id);

                col.Delete(id);

                if (User.Active.Id == id)
                {
                    User.Active = null;
                    utils.UpdateUser(User);
                }
                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                    .WithContent($"Character **{C.Name}** has been deleted. If this was your active character, you no longer have an active character."));
                return;
            }
            else if (e.Id.StartsWith("kd"))
            {
                string[] data = e.Id.Substring(2).Split(',');
                int ActorId = int.Parse(data[0]);
                int SkillIndex = int.Parse(data[1]);

                var col = database.GetCollection<Character>("Characters");

                var C = col.FindById(ActorId);

                var sk = C.Skills[SkillIndex];

                C.Skills.RemoveAt(SkillIndex);

                utils.UpdateActor(C);

                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Removed skill {sk.Name} from {C.Name}."));
            }
            else if (e.Id.StartsWith("id"))
            {
                string[] data = e.Id.Substring(2).Split(',');
                int ActorId = int.Parse(data[0]);
                int Index = int.Parse(data[1]);

                var col = database.GetCollection<Character>("Characters");

                var C = col.FindById(ActorId);

                var sk = C.Inventory[Index];

                C.Skills.RemoveAt(Index);

                utils.UpdateActor(C);

                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Removed item {sk.Name} from {C.Name}."));
            }
            else if (e.Id.StartsWith("b"))
            {
                Collections.RollData data = new Collections.RollData().Deserialize(e.Id.Substring(1));

                var actors = database.GetCollection<Character>("Characters");

                var a = actors.FindById(data.Actor);

                a.Luck -= 1;

                utils.UpdateActor(a);

                data.Boosts++;

                var dice = Roller.Roll("1d6!e");

                var temp = data.dice.ToList();

                temp.AddRange(dice.Values.Where(x => x.NumSides == 6 && x.DieType == DieType.Normal).Select(x => (int)x.Value));

                data.dice = temp.ToArray();

                var Embed = utils.EmbedRoll(data);

                string serial = data.Serialize();

                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                    .WithContent($"{a.Name}'s fate has changed...\nSpent {data.Boosts} Luck Points boosting! ({a.Luck} Remaining Luck Points)")
                    .AddEmbed(Embed)
                    .AddComponents(new DiscordComponent[]
                    {
                        new DiscordButtonComponent(new DiscordButtonComponent(ButtonStyle.Primary,"b"+serial,"Boost",false, new DiscordComponentEmoji("✨")))
                    }));
            }
            else if (e.Id.StartsWith("j"))
            {
                int pos = int.Parse(e.Id.Substring(1));

                var U = utils.GetUser(e.User.Id);

                var C = U.Active;

                if (c == null)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Pong);
                    return;
                }

                var Enc = utils.GetEncounter(e.Channel.Id);

                if (!Enc.Active)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Pong);
                    return;
                }

                if (Enc.Combatants.Exists(x => x.Actor.Id == C.Id))
                {
                    int I = Enc.Combatants.FindIndex(x => x.Actor != null && x.Actor.Id == C.Id);

                    Enc.Combatants[I].Position = (Position)pos;
                }
                else
                {

                    var roll = Roller.Roll($"1d20 + {Math.Max(C.Insight, C.Agility)}");

                    double init = (double)roll.Value + (Math.Max(C.Insight, C.Agility) * 0.1);

                    Enc.Add(new Combatant()
                    {
                        Actor = C,
                        Name = C.Name,
                        Position = (Position)pos,
                        Initiative = init
                    });
                }

                utils.UpdateEncounter(Enc);

                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                    .WithContent($"A new encounter has been initiated!\n• **Players**: Use the buttons below to join the encounter with your Active character.\n• **Game Master** Use the `/Encounter AddNPC` command to add an NPC to the encounter!")
                    .AddEmbed(utils.EmbedEncounter(Enc))
                    .AddComponents(new DiscordComponent[]
                    {
                        new DiscordButtonComponent(ButtonStyle.Primary,$"j0","Join",false,new DiscordComponentEmoji("◀️")),
                        new DiscordButtonComponent(ButtonStyle.Primary,$"j1","Join",false,new DiscordComponentEmoji("⏏️")),
                        new DiscordButtonComponent(ButtonStyle.Primary,$"j2","Join",false,new DiscordComponentEmoji("▶️"))
                    })
                    .AddComponents(new DiscordComponent[]
                    {
                        new DiscordButtonComponent(ButtonStyle.Secondary,"r","Refresh"),
                        new DiscordButtonComponent(ButtonStyle.Secondary,"go","Start Combat")
                    }));
            }
            else if (e.Id.StartsWith("e"))
            {
                var Enc = utils.GetEncounter(e.Channel.Id);

                if (Enc.Active)
                {
                    Enc.End();

                    utils.UpdateEncounter(Enc);

                    await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("Encounter over!"));
                }
                else
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("There was no encounter to end!"));
                }
            }
            else if (e.Id == "t")
            {
                var Enc = utils.GetEncounter(e.Channel.Id);

                if (!Enc.Active)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Pong);
                    return;
                }
                else
                {
                    if(Enc.GameMaster == e.User.Id || Enc.Current.Actor?.Owner == e.User.Id)
                    {
                        Enc.NextTurn();

                        utils.UpdateEncounter(Enc);
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                            .WithContent("Turn over!"));

                        await e.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder()
                                .WithContent($"{(Enc.Current.Actor != null ? $"<@{Enc.Current.Actor.Owner}>" : $"<@{Enc.GameMaster}>")}'s Turn!")
                                .AddEmbed(utils.EmbedEncounter(Enc))
                                .AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"m0","Move",false,new DiscordComponentEmoji("◀️")),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"m1","Move",false,new DiscordComponentEmoji("⏏️")),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"m2","Move",false,new DiscordComponentEmoji("▶️"))

                                })
                                .AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Secondary,$"ut","Previous turn"),
                                    new DiscordButtonComponent(ButtonStyle.Secondary,$"t","Next turn"),
                                    new DiscordButtonComponent(ButtonStyle.Secondary,"r","Refresh")
                                }));
                    }
                    else
                    {
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.Pong);
                        return;
                    }
                }
            }
            else if (e.Id == "ut")
            {
                var Enc = utils.GetEncounter(e.Channel.Id);

                if (!Enc.Active)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Pong);
                    return;
                }
                else
                {
                    if (Enc.GameMaster == e.User.Id || Enc.Current.Actor?.Owner == e.User.Id)
                    {
                        Enc.PrevTurn();

                        utils.UpdateEncounter(Enc);
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                            .WithContent("Undoing Turn!"));

                        await e.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder()
                                .WithContent($"{(Enc.Current.Actor != null ? $"<@{Enc.Current.Actor.Owner}>" : $"<@{Enc.GameMaster}>")}'s Turn!")
                                .AddEmbed(utils.EmbedEncounter(Enc))
                                .AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"m0","Move",false,new DiscordComponentEmoji("◀️")),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"m1","Move",false,new DiscordComponentEmoji("⏏️")),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"m2","Move",false,new DiscordComponentEmoji("▶️"))

                                })
                                .AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Secondary,$"ut","Previous turn"),
                                    new DiscordButtonComponent(ButtonStyle.Secondary,$"t","Next turn"),
                                    new DiscordButtonComponent(ButtonStyle.Secondary,"r","Refresh")
                                }));
                    }
                    else
                    {
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.Pong);
                        return;
                    }
                }
            }
            else if(e.Id == "r")
            {
                var Enc = utils.GetEncounter(e.Channel.Id);

                if (!Enc.Active)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Pong);
                    return;
                }
                else
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                        new DiscordInteractionResponseBuilder()
                            .WithContent(e.Message.Content)
                            .AddComponents(e.Message.Components)
                            .AddEmbed(utils.EmbedEncounter(Enc)));
                }
            }
            else if(e.Id == "go")
            {
                var Enc = utils.GetEncounter(e.Channel.Id);

                if (!Enc.Active)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Pong);
                    return;
                }
                else if(Enc.Active && Enc.Initiated)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Pong);
                    return;
                }
                else
                {
                    Enc.Start();

                    utils.UpdateEncounter(Enc);

                    await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                        new DiscordInteractionResponseBuilder().WithContent("Initiating Encounter..."));

                    await e.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder()
                        .WithContent($"{(Enc.Current.Actor != null ? $"<@{Enc.Current.Actor.Owner}>" : $"<@{Enc.GameMaster}>")}'s Turn!")
                        .AddEmbed(utils.EmbedEncounter(Enc))
                        .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Primary,"m0","Move",false,new DiscordComponentEmoji("◀️")),
                            new DiscordButtonComponent(ButtonStyle.Primary,"m1","Move",false,new DiscordComponentEmoji("⏏️")),
                            new DiscordButtonComponent(ButtonStyle.Primary,"m2","Move",false,new DiscordComponentEmoji("▶️"))

                        })
                        .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Secondary,$"ut","Previous turn"),
                            new DiscordButtonComponent(ButtonStyle.Secondary,$"t","Next turn"),
                            new DiscordButtonComponent(ButtonStyle.Secondary,"r","Refresh")
                        }));
                }
            }
            else if (e.Id.StartsWith("m"))
            {
                int pos = int.Parse(e.Id.Substring(1));

                var Enc = utils.GetEncounter(e.Channel.Id);

                if (!Enc.Active)
                {
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Pong);
                    return;
                }
                else
                {
                    if(Enc.GameMaster == e.User.Id || Enc.Current.Actor?.Owner == e.User.Id)
                    {
                        Enc.Move(Enc.Current, (Position)pos);

                        utils.UpdateEncounter(Enc);

                        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                            new DiscordInteractionResponseBuilder()
                            .WithContent(e.Message.Content)
                            .AddEmbed(utils.EmbedEncounter(Enc))
                            .AddComponents(new DiscordComponent[]
                            {
                                new DiscordButtonComponent(ButtonStyle.Primary,"m0","Move",false,new DiscordComponentEmoji("◀️")),
                                new DiscordButtonComponent(ButtonStyle.Primary,"m1","Move",false,new DiscordComponentEmoji("⏏️")),
                                new DiscordButtonComponent(ButtonStyle.Primary,"m2","Move",false,new DiscordComponentEmoji("▶️"))

                            })
                            .AddComponents(new DiscordComponent[]
                            {
                                new DiscordButtonComponent(ButtonStyle.Secondary,$"ut","Previous turn"),
                                new DiscordButtonComponent(ButtonStyle.Secondary,$"t","Next turn"),
                                new DiscordButtonComponent(ButtonStyle.Secondary,"r","Refresh")
                            }));
                    }
                    else
                    {
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.Pong);
                        return;
                    }
                }
            }
            
            else if (e.Id.StartsWith("h"))
            {
                string[] args = e.Id.Substring(1).Split(',');

                var builder = new DiscordInteractionResponseBuilder();

                switch (args[0])
                {
                    case "0":
                        switch (args[1])
                        {
                            case "0":
                                builder.AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("Help Files - Commands")
                                .WithDescription("" +
                                "The following is the list of all commands available to use with this bot. All parameters wrapped around brackets `[Example]` are mandatory. While parameters wrapped around curly brackets `{example}` are optional and can be excluded from commands.\n\n" +
                                "Commands are divided into 6 mayor groups: Character, Attributes, Skills, Inventory, General and Encounter commands.\n" +
                                "Most, if not all, of these commands require you to have an **Active Character**. When you create a character using `/Characters Create`, this character is automatically assigned as your active character. You can then use the `/Character` command to switch your active character.")
                                );
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,1","Characters"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,2","Attributes"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,3","Skills")
                                });
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,4","Inventory"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,5","General"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,6","Encounter")
                                });
                                break;
                            case "1":
                                builder.AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("Help Files - Commands")
                                .WithDescription("" +
                                "The following is the list of all commands available to use with this bot. All parameters wrapped around brackets `[Example]` are mandatory. While parameters wrapped around curly brackets `{example}` are optional and can be excluded from commands.\n\n" +
                                "Commands are divided into 6 mayor groups: Character, Attributes, Skills, Inventory, General and Encounter commands.\n" +
                                "Most, if not all, of these commands require you to have an **Active Character**. When you create a character using `/Characters Create`, this character is automatically assigned as your active character. You can then use the `/Character` command to switch your active character.")
                                .AddField("Character Commands", "These are all the commands which are used to manage your characters and exist within the `/Character` umbrella:\n" +
                                    "• `/Characters Create [Name] {Level}` Create a new character. Level is set to 1 by default.\n" +
                                    "• `/Characters Delete [Name]` Delete a character.\n" +
                                    "• `/Characters Rename [New Name]` Renames your Active Character.\n" +
                                    "• `/Characters LevelUp {Levels}` Levels up your Active Character. Levels defaults to 1.\n" +
                                    "• `/Characters LevelDown {Levels}` Levels down your Active Characters. Levels defaults to 1."));
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,1","Characters"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,2","Attributes"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,3","Skills")
                                });
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,4","Inventory"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,5","General"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,6","Encounter")
                                });
                                break;
                            case "2":
                                builder.AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("Help Files - Commands")
                                .WithDescription("" +
                                "The following is the list of all commands available to use with this bot. All parameters wrapped around brackets `[Example]` are mandatory. While parameters wrapped around curly brackets `{example}` are optional and can be excluded from commands.\n\n" +
                                "Commands are divided into 6 mayor groups: Character, Attributes, Skills, Inventory, General and Encounter commands.\n" +
                                "Most, if not all, of these commands require you to have an **Active Character**. When you create a character using `/Characters Create`, this character is automatically assigned as your active character. You can then use the `/Character` command to switch your active character.")
                                .AddField("Attribute Commands", "These are commands tied to altering attributes within your Active Character and exist under the `/Attributes` umbrella:\n" +
                                "• `/Attributes BonusHealth [Value]` Sets your Active Character's bonus health. Only required if bonus HP is granted from Items or Boons.\n" +
                                "• `/Attributes Vigor [Value]` Sets your Active Character's vigor attribute. Cannot go below 1 or above 7.\n" +
                                "• `/Attributes Agility [Value]` Sets your Active Character's agility attribute. Cannot go below 1 or above 7.\n" +
                                "• `/Attributes Insight [Value]` Sets your Active Character's insight attribute. Cannot go below 1 or above 7.\n" +
                                "• `/Attributes Presence [Value]` Sets your Active Character's presence attribute. Cannot go below 1 or above 7."));
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,1","Characters"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,2","Attributes"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,3","Skills")
                                });
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,4","Inventory"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,5","General"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,6","Encounter")
                                });
                                break;
                            case "3":
                                builder.AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("Help Files - Commands")
                                .WithDescription("" +
                                "The following is the list of all commands available to use with this bot. All parameters wrapped around brackets `[Example]` are mandatory. While parameters wrapped around curly brackets `{example}` are optional and can be excluded from commands.\n\n" +
                                "Commands are divided into 6 mayor groups: Character, Attributes, Skills, Inventory, General and Encounter commands.\n" +
                                "Most, if not all, of these commands require you to have an **Active Character**. When you create a character using `/Characters Create`, this character is automatically assigned as your active character. You can then use the `/Character` command to switch your active character.")
                                .AddField("Skill Commands", "These are commands tied to managing Skills within your Active Character and exist within the `/skills` umbrella:\n" +
                                "• `/Skills Add [Name] [Description] [Type] {Ranks}` Adds a new Skill to your active character. Ranks defaults to 1.\n" +
                                "• `/Skills Ranks [Name] [Ranks]` Sets the Ranks an existing skill on your Active character.\n" +
                                "• `/Skills Delete [Name]` Remove a skill from your Active Character."));
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,1","Characters"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,2","Attributes"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,3","Skills")
                                });
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,4","Inventory"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,5","General"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,6","Encounter")
                                });
                                break;
                            case "4":
                                builder.AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("Help Files - Commands")
                                .WithDescription("" +
                                "The following is the list of all commands available to use with this bot. All parameters wrapped around brackets `[Example]` are mandatory. While parameters wrapped around curly brackets `{example}` are optional and can be excluded from commands.\n\n" +
                                "Commands are divided into 6 mayor groups: Character, Attributes, Skills, Inventory, General and Encounter commands.\n" +
                                "Most, if not all, of these commands require you to have an **Active Character**. When you create a character using `/Characters Create`, this character is automatically assigned as your active character. You can then use the `/Character` command to switch your active character.")
                                .AddField("Inventory Commands", "These commands allow you to manage your active character's Inventory.\n" +
                                "• `/Inventory Currency [Value]` Increase or decrease the currency of your active character.\n" +
                                "• `/Inventory Add [Name] [Description] [Item Type] {Quantity}` Adds a new item to your character's inventory. Quantity defaults to 1.\n" +
                                "• `/Inventory Duplicate [Name] [Amount]` Takes an existing item in your active character's inventory and add copies of it. Useful for consumables and other items which are held in stacks.\n" +
                                "• `/Inventory Use [Name] {Amount}` Equips/Unequips a piece of equipment in your active character's inventory. If the item is a consumable, it uses one or more of the item. This command never deletes consumables whose amount reaches 0, allowing you to add more copies of the item using `/Inventory Duplicate`.\n" +
                                "• `/Inventory Discard [Name]` Permanently deletes an item from your Active character inventory. Only use this if you want to entirely remove an item from inventory insteadof just using the item."));
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,1","Characters"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,2","Attributes"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,3","Skills")
                                });
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,4","Inventory"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,5","General"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,6","Encounter")
                                });
                                break;
                            case "5":
                                builder.AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("Help Files - Commands")
                                .WithDescription("" +
                                "The following is the list of all commands available to use with this bot. All parameters wrapped around brackets `[Example]` are mandatory. While parameters wrapped around curly brackets `{example}` are optional and can be excluded from commands.\n\n" +
                                "Commands are divided into 6 mayor groups: Character, Attributes, Skills, Inventory, General and Encounter commands.\n" +
                                "Most, if not all, of these commands require you to have an **Active Character**. When you create a character using `/Characters Create`, this character is automatically assigned as your active character. You can then use the `/Character` command to switch your active character.")
                                .AddField("General Command", "These are all the command which are not tied to an existing group.\n" +
                                "• `/Character {Name}` View your active character. If a name is provided, you change your active character instead.\n" +
                                "• `/Color [Hex Color code]` Change your Active Character's color. This color is added to all embeds tied to your character.\n" +
                                "• `/Image [Image URL]` Changes your Active Character's image. The url must be a valid URL for a .png or .jpeg image.\n" +
                                "• `/Health [Amount]` Add or subtract health from your active character. Health cannot go under 0 or above your Maximum.\n" +
                                "• `/Luck [Amount]` Add or subtract from your active character's Luck pool. Luck cannot go under 0 or above 10."));
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,1","Characters"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,2","Attributes"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,3","Skills")
                                });
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,4","Inventory"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,5","General"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,6","Encounter")
                                });
                                break;
                            case "6":
                                builder.AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("Help Files - Commands")
                                .WithDescription("" +
                                "The following is the list of all commands available to use with this bot. All parameters wrapped around brackets `[Example]` are mandatory. While parameters wrapped around curly brackets `{example}` are optional and can be excluded from commands.\n\n" +
                                "Commands are divided into 6 mayor groups: Character, Attributes, Skills, Inventory, General and Encounter commands.\n" +
                                "Most, if not all, of these commands require you to have an **Active Character**. When you create a character using `/Characters Create`, this character is automatically assigned as your active character. You can then use the `/Character` command to switch your active character.")
                                .AddField("Encounter Commands", "These commands are used to run encounters. Encounters are tied to the text channel, meaning only 1 encounter can happen per channel at any given time.\n" +
                                "• `/Encounter View` View the current active character.\n" +
                                "• `/Encounter Start` Starts an encounter in the current channel.\n" +
                                "• `/Encounter End` Ends an encounter in the current channel.\n" +
                                "• `/Encounter AddNPC [Name] [Position] {Initiative}` Adds an NPC to the encounter. If no Initiative is provided, a d20 is rolled.\n" +
                                "• `/Encounter Remove [Name]` Removes a combatant from the current encounter.\n" +
                                "• `/Encounter Move [Name] [Position]` Moves a combatant from one position to another. Only the Game Master can move NPCs or combatants other than your own active character.")
                                .AddField("Combat Buttons","Unlike most of the other commands. A god portion of the controls for encounters are done using button as opposed to commands! For example, For a player to join an encounter, they must press the Join buttons to join the encounter in one of the 3 combat zones. And then in order to start your encounter, you press the `Start` button. During combat, you can move forwards or backwards in the turn order by using the `Next Turn` and `Previous Turn` buttons. And the current combatant can use the `Move` buttons to move from one field to another. ");
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,1","Characters"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,2","Attributes"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,3","Skills")
                                });
                                builder.AddComponents(new DiscordComponent[]
                                {
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,4","Inventory"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,5","General"),
                                    new DiscordButtonComponent(ButtonStyle.Primary,"h0,6","Encounter")
                                });
                                break;
                        }
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                            builder);
                        break;
                }
            }
        
        }
    }
}
