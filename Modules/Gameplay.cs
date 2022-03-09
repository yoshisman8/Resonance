using DSharpPlus.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus;
using System;
using System.Collections.Generic;
using System.Text;
using LiteDB;
using System.Threading.Tasks;
using Dice;
using Resonance.Collections;
using System.Linq;
using Resonance.Services;

namespace Resonance.Modules
{
    public class GameplayModule : ApplicationCommandModule
    {
        public Services.Utilities Utils;
        public LiteDatabase db;

        [SlashCommand("Roll","Make a simple, non-gameplay roll.")]
        public async Task Roll(InteractionContext context, [Option("Expression","Dice Expression. See /Help Rolling for more info.")]string Expression)
        {
            try
            {
                var Roll = Roller.Roll(Expression);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                    .WithContent($"{Roll.ParseResult()} = `{Roll.Value}`"));
            }
            catch
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("There was an error with this roll! It likely was not a valid dice expression. If you are unsure what a valid dice expression is, check out this link:\n<https://skizzerz.net/DiceRoller/Dice_Reference>"));
                return;
            }
        }

        [SlashCommand("Check", "Make a check using your active character.")]
        public async Task Check(InteractionContext context,
            [Choice("Vigor",1)]
            [Choice("Agility",2)]
            [Choice("Insight",3)]
            [Choice("Presence",4)]
            [Option("Attribute","The attribute being used on this check.")]double Att,
            [Option("Skill", "Name of the Skill being used. Type 'None' if no skill is being used.")] string Skill,
            [Option("Modifier", "(Optional) Extra Bonuses/Penalties to this check.")] double Mods = 0)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            int AttributeBonus = 0;

            switch (Att)
            {
                case 1:
                    AttributeBonus = C.Vigor;
                    break;
                case 2:
                    AttributeBonus = C.Agility;
                    break;
                case 3:
                    AttributeBonus = C.Insight;
                    break;
                case 4:
                    AttributeBonus = C.Presence;
                    break;
            }

            int SkillBonus = 0;

            var Q = C.Skills.Where(x => x.Name.ToLower().StartsWith(Skill.ToLower()));

            if (Q.Count() > 0)
            {
                Skill K = Q.FirstOrDefault();

                SkillBonus = K.Ranks;
            }

            Mods = Math.Floor(Mods);

            var Roll = Roller.Roll($"{AttributeBonus + SkillBonus + Mods}d6!e");

            Collections.RollData Data = new Collections.RollData();

            Data.Actor = C.Id;
            Data.Boosts = 0;
            Data.dice = Roll.Values.Where(x=>x.DieType == DieType.Normal && x.NumSides == 6).Select(x => (int)x.Value).ToArray();

            await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(Utils.EmbedRoll(Data))
                    .AddComponents(new DiscordComponent[]
                    {
                        new DiscordButtonComponent(new DiscordButtonComponent(ButtonStyle.Primary,"b"+Data.Serialize(),"Boost",false, new DiscordComponentEmoji("✨"))) 
                    })
                );
        }


        [SlashCommandGroup("Encounter", "Manage Encounters in the current channel.")]
        public class CombatManager : ApplicationCommandModule
        {
            public Services.Utilities Utils;
            public LiteDatabase db;

            [SlashCommand("View", "View the status of the current encounter in this channel.")]
            public async Task View(InteractionContext context)
            {
                Encounter U = Utils.GetEncounter(context.Channel.Id);

                if (U.Active == false)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("There is no active encounter on this channe. Feel free to start one using the `/Encounter Start` command!"));
                    return;
                }
                else if(U.Active && !U.Initiated)
                {
                    var embed = Utils.EmbedEncounter(U);

                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .AddEmbed(embed)
                        .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Primary,"j0","Join",false,new DiscordComponentEmoji("◀️")),
                            new DiscordButtonComponent(ButtonStyle.Primary,"j1","Join",false,new DiscordComponentEmoji("⏏️")),
                            new DiscordButtonComponent(ButtonStyle.Primary,"j2","Join",false,new DiscordComponentEmoji("▶️"))
                        })
                        .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Secondary,"r","Refresh"),
                            new DiscordButtonComponent(ButtonStyle.Secondary,"go","Start Combat")
                        }));
                }
                else if (U.Active && U.Initiated)
                {
                    var embed = Utils.EmbedEncounter(U);

                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .AddEmbed(embed)
                        .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Primary,"m0","Move",false,new DiscordComponentEmoji("◀️")),
                            new DiscordButtonComponent(ButtonStyle.Primary,"m1","Move",false,new DiscordComponentEmoji("⏏️")),
                            new DiscordButtonComponent(ButtonStyle.Primary,"m2","Move",false,new DiscordComponentEmoji("▶️"))
                            
                        })
                        .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Secondary,$"ut","Previous turn"),
                            new DiscordButtonComponent(ButtonStyle.Primary,$"t","Next turn"),
                            new DiscordButtonComponent(ButtonStyle.Secondary,"r","Refresh")
                        }));
                }

            }
            [SlashCommand("Start", "Start an encounter on the current channel.")]
            public async Task Start(InteractionContext context)
            {
                Encounter U = Utils.GetEncounter(context.Channel.Id);

                if (U.Active)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("There's already an Active encounter! Use the Start Combat button to Initiate the encounter. If you wish to end the encounter, use the `/Encounter End` command!"));
                    return;
                }
                else
                {
                    U.Active = true;
                    U.GameMaster = context.User.Id;

                    Utils.UpdateEncounter(U);

                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .WithContent($"A new encounter has been initiated!\n• **Players**: Use the buttons below to join the encounter with your Active character.\n• **Game Master** Use the `/Encounter AddNPC` command to add an NPC to the encounter!")
                        .AddEmbed(Utils.EmbedEncounter(U))
                        .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Primary,$"j0","Join (Ally Range)",false,new DiscordComponentEmoji("◀️")),
                            new DiscordButtonComponent(ButtonStyle.Primary,$"j1","Join (Close Range)",false,new DiscordComponentEmoji("⏏️")),
                            new DiscordButtonComponent(ButtonStyle.Primary,$"j2","Join (Enemy Range)",false,new DiscordComponentEmoji("▶️"))
                        })
                        .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Secondary,"r","Refresh"),
                            new DiscordButtonComponent(ButtonStyle.Secondary,"go","Start Combat")
                        }));
                    return;
                }
            }

            [SlashCommand("End", "Ends an existing encounter.")]
            public async Task End(InteractionContext context)
            {
                Encounter E = Utils.GetEncounter(context.Channel.Id);

                if (!E.Active)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("There is no active encounter on this channel!"));
                    return;
                }
                else
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Are you sure you want to end the encounter? This will clear the board and cannot be undone!")
                            .AddComponents(new DiscordComponent[]
                            {
                                new DiscordButtonComponent(ButtonStyle.Danger,"e","End Encounter"),
                                new DiscordButtonComponent(ButtonStyle.Secondary,"cancel","Cancel")
                            }));
                    return;
                }
            }
            [SlashCommand("Move", "Move yourself or an NPC (Game Master Only) to a different range.")]
            public async Task Move(InteractionContext context, [Option("Range", "The range to move to")] Position pos, [Option("Name", "Name of the NPC to move (Game Master Only)")] string Name = null)
            {
                Encounter E = Utils.GetEncounter(context.Channel.Id);

                if (!E.Active)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("There is no active encounter on this channel!"));
                    return;
                }

                if (Name != null && E.GameMaster != context.User.Id)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Only the Game Master can move other combatants around!"));
                    return;
                }
                else if (context.User.Id == E.GameMaster && Name != null)
                {
                    var Q = E.Combatants.Where(x => x.Name.ToLower().StartsWith(Name.ToLower()));

                    if (Q.Count() == 0)
                    {
                        await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().WithContent($"There aren't any combatants in this encounter whose name begins with \"{Name}\"."));
                        return;
                    }
                    else
                    {
                        var C = Q.FirstOrDefault();

                        int index = E.Combatants.IndexOf(C);

                        E.Combatants[index].Position = pos;

                        Utils.UpdateEncounter(E);

                        await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().WithContent($"Combatant {C.Name} has now moved to the {pos} range!"));
                        return;
                    }
                }
                else
                {
                    User U = Utils.GetUser(context.User.Id);

                    if (U.Active == null)
                    {
                        await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                        return;
                    }

                    Character A = U.Active;

                    var C = E.Combatants.Find(x => x.Actor.Id == A.Id);

                    if (C == null)
                    {
                        await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().WithContent("Your active character is not participating in this encounter!"));
                        return;
                    }

                    var index = E.Combatants.IndexOf(C);

                    E.Combatants[index].Position = pos;

                    Utils.UpdateEncounter(E);

                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent($"{A.Name} has now moved to the {pos} range!"));
                    return;
                }

            }

            [SlashCommand("AddNPC", "Adds an NPC to the current encounter (Game Master Only).")]
            public async Task AddNPC(InteractionContext context, [Option("Name", "UNIQUE name for this combatant.")] string Name, [Option("Position","Postion for this NPC to start on.")]Position pos,[Option("Initiative","Initiative Number. If empty, a D20 will be rolled.")]double Initiative = -1)
            {
                Encounter E = Utils.GetEncounter(context.Channel.Id);

                if (!E.Active)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("There is no active encounter on this channel!"));
                    return;
                }

                if(E.Active && E.GameMaster != context.User.Id)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Only the Game Master can add NPCs to an encounter!"));
                    return;
                }
                else
                {
                    if(E.Combatants.Exists(x=>x.Name.ToLower() == Name.ToLower()))
                    {
                        await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().WithContent("This encounter already has an NPC with that exact name!"));
                        return;
                    }
                    else
                    {
                        if(Initiative == -1)
                        {
                            Initiative = (double)Roller.Roll("1d20").Value;
                        }

                        E.Add(new Combatant()
                        {
                            Initiative = Initiative,
                            Name = Name,
                            Position = pos
                        });

                        Utils.UpdateEncounter(E);

                        await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().WithContent($"Added NPC {Name} to the Encounter!"));
                        return;
                    }
                }

            }
        
            [SlashCommand("Remove","(Game Master Only) Removes a combatant from the current encounter.")]
            public async Task Remove(InteractionContext context,[Option("Name","COMPLETE name of the combtant being removed.")]string Name)
            {
                Encounter E = Utils.GetEncounter(context.Channel.Id);

                if (!E.Active)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("There is no active encounter on this channel!"));
                    return;
                }
                if(E.GameMaster != context.User.Id)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Only the Game Master can remove combatants!"));
                    return;
                }
                else
                {
                    Combatant C = E.Combatants.Find(x => x.Name.ToLower() == Name.ToLower());

                    if(C== null)
                    {
                        await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().WithContent($"There are no combatants in this encounter with the exact name of \"{Name}\"."));
                        return;
                    }

                    else
                    {
                        E.Remove(C);

                        Utils.UpdateEncounter(E);

                        await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().WithContent($"Removed actor {C.Name} from the current enconter!")
                            .AddEmbed(Utils.EmbedEncounter(E)));
                        return;
                    }
                }
            }
        }
    }
}
