using DSharpPlus.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus;
using System;
using System.Collections.Generic;
using System.Text;
using LiteDB;
using System.Threading.Tasks;
using Resonance.Collections;
using System.Linq;
using Resonance.Services;

namespace Resonance.Modules
{
    [SlashCommandGroup("Party","Manage Parties.")]
    public class PartyManagement : ApplicationCommandModule
    {
        public Services.Utilities Utils;
        public LiteDatabase db;

        [SlashCommand("Create","Creates a new party for players to join.")]
        public async Task Create(InteractionContext context, [Option("Name","Name of the party.")]string Name)
        {
            var col = db.GetCollection<Party>("Parties");

            var query = col.Include(x=>x.Actors).Find(x => x.Name == Name && x.Guild == context.Guild.Id);

            if(query.Count() > 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"There's already a party in this server named \"{Name}\"."));
                return;
            }
            else
            {
                Party Party = new Party()
                {
                    Name = Name,
                    GameMaster = context.User.Id,
                    Guild = context.Guild.Id
                };

                col.Insert(Party);

                col.EnsureIndex("Name", "LOWER($.Name)");
                col.EnsureIndex(x => x.GameMaster);

                Party _P = col.FindOne(x => x.Name == Name && x.GameMaster == context.User.Id);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"New party {Name} has been created!")
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithTitle(Party.Name)
                            .WithDescription($"**Game Master**\n> {context.User.Mention}")
                            .AddField("Party Members", "None! Use the Join button to join this party as your active character!"))
                        .AddComponents(new DiscordComponent[] 
                        {
                            new DiscordButtonComponent(ButtonStyle.Primary,$"p{_P.ID}","Join Party"),
                            new DiscordButtonComponent(ButtonStyle.Secondary,$"l{_P.ID}","Leave Party")
                        }));
            }
        }
        [SlashCommand("View","View a party created on this server.")]
        public async Task View(InteractionContext context, [Option("Name","Name of the party.")]string Name)
        {
            var col = db.GetCollection<Party>("Parties");

            var Q = col.Include(x => x.Actors).Find(x => x.Name.StartsWith(Name.ToLower()) && x.Guild == context.Guild.Id);

            if(Q.Count() == 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"There are no parties in this server whose name begins with \"{Name}\"."));
                return;
            }

            else
            {
                Party P = Q.FirstOrDefault();

                var GM = await context.Guild.GetMemberAsync(P.GameMaster);

                var EmbedBuilder = new DiscordEmbedBuilder()
                    .WithTitle(P.Name)
                    .WithDescription($"**Game Master**\n> {GM.Mention}");
                var sb = new StringBuilder();

                foreach (var member in P.Actors)
                {
                    var player = await context.Guild.GetMemberAsync(member.Owner);

                    sb.AppendLine($"• {member.Name} - Played By: {player.Mention}");
                }

                if (sb.Length == 0)
                {
                    EmbedBuilder.AddField("Party Members", "None! Use the Join button to join this party as your active character!");
                }
                else
                {
                    EmbedBuilder.AddField("Party Members", sb.ToString());
                }

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(EmbedBuilder.Build())
                        .AddComponents(new DiscordComponent[]
                        {
                                new DiscordButtonComponent(ButtonStyle.Primary,$"p{P.ID}","Join Party"),
                                new DiscordButtonComponent(ButtonStyle.Secondary,$"l{P.ID}","Leave Party")
                        }));
            }
        }
        [SlashCommand("Disband","Permanently deletes a party.")]
        public async Task Disband(InteractionContext context, [Option("Name","Exact, full name of the party.")]string Name)
        {
            var col = db.GetCollection<Party>("Parties");

            var Q = col.Find(x => x.Name.StartsWith(Name.ToLower()) && x.Guild == context.Guild.Id && x.GameMaster == context.User.Id);

            if (Q.Count() == 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"There are no parties you own in this server whose name begins with \"{Name}\"."));
                return;
            }
            else
            {
                Party P = Q.FirstOrDefault();

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Are you sure you want to permanently disband the party \"{P.Name}\"?\n**WARNING! THIS CANNOT BE UNDONE!")
                        .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Danger,$"dis{P.ID}","Disband"),
                            new DiscordButtonComponent(ButtonStyle.Secondary,$"cancel","Cancel")
                        }));
            }
        }
    
        [SlashCommand("Item","Gives an Item to a player in one of your Parties.")]
        public async Task GiveItem(InteractionContext context,
            [Option("Character","Full or Partial name of the party member.")]string Name,
            [Option("Item","Name of the Item.")]string ItemName,
            [Option("Description","Description of the Item.")]string Description,
            [Option("Type","The type of item this is.")]ItemTypes Type,
            [Option("Quantity","(Optional) Quantity of the item to give. Defaults to 1")]double Quantity = 1)
        {
            var col = db.GetCollection<Party>("Parties");

            var all = col.Include(x => x.Actors).Find(x => x.GameMaster == context.User.Id && x.Guild == context.Guild.Id);

            var Q = all.Where(x => x.Actors.Exists(y => y.Name.ToLower().StartsWith(Name.ToLower())));
            Quantity = Math.Abs(Quantity);

            if(Q.Count() == 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Could not find a Party Member named \"{Name}\" in any party you own on this server."));
                return;
            }
            else
            {
                Character C = Q.FirstOrDefault().Actors.Find(x => x.Name.ToLower().StartsWith(Name.ToLower()));

                if (C.Inventory.Exists(x => x.Name.ToLower() == ItemName.ToLower()))
                {
                    int Index = C.Inventory.FindIndex(x => x.Name.ToLower() == ItemName.ToLower());

                    C.Inventory[Index].Quantity += (int)Quantity;

                    Utils.UpdateActor(C);

                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent($"Added {Quantity} {C.Inventory[Index].Name} to {C.Name}'s Inventory!"));
                    return;
                }
                else
                {
                    Item I = new Item()
                    {
                        Name = ItemName,
                        Description = Description,
                        Quantity = (int)Quantity,
                        Type = Type
                    };

                    C.Inventory.Add(I);

                    Utils.UpdateActor(C);

                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent($"Added {Quantity} {ItemName} to {C.Name}'s Inventory!"));
                    return;
                }
            }
        }

        [SlashCommand("Condition", "Gives a Condition to a player in one of your Parties.")]
        public async Task GiveCondition(InteractionContext context,
            [Option("Character", "Full or Partial name of the party member.")] string Name,
            [Option("Condition", "Name of the Condition")] string ConditionName,
            [Option("Description", "Description of the Condition")] string Description)
        {
            var col = db.GetCollection<Party>("Parties");

            var all = col.Include(x => x.Actors).Find(x => x.GameMaster == context.User.Id && x.Guild == context.Guild.Id);

            var Q = all.Where(x => x.Actors.Exists(y => y.Name.ToLower().StartsWith(Name.ToLower())));

            if (Q.Count() == 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Could not find a Party Member named \"{Name}\" in any party you own on this server."));
                return;
            }
            else
            {
                Character C = Q.FirstOrDefault().Actors.Find(x => x.Name.ToLower().StartsWith(Name.ToLower()));

                if(C.Conditions.Exists(x=>x.Name.ToLower() == ConditionName.ToLower()))
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent($"{C.Name} alraedy has a condition named {ConditionName}!"));
                    return;
                }
                else
                {
                    Tracker Cond = new Tracker()
                    {
                        Name = ConditionName,
                        Description = Description
                    };

                    C.Conditions.Add(Cond);
                    Utils.UpdateActor(C);

                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent($"Added condition {ConditionName} to {C.Name}!"));
                    return;
                }
            }
        }
    
        [SlashCommand("Health","Increase or Decrease the health of a party member in one of your parties.")]
        public async Task Health(InteractionContext context, [Option("Name","Name of the party member.")]string Name,[Option("Value", "Positive number heal, negative numbers harm.")]double Quantity)
        {
            var col = db.GetCollection<Party>("Parties");

            var all = col.Include(x => x.Actors).Find(x => x.GameMaster == context.User.Id && x.Guild == context.Guild.Id);

            var Q = all.Where(x => x.Actors.Exists(y => y.Name.ToLower().StartsWith(Name.ToLower())));

            if (Q.Count() == 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Could not find a Party Member named \"{Name}\" in any party you own on this server."));
                return;
            }
            else
            {
                Character C = Q.FirstOrDefault().Actors.Find(x => x.Name.ToLower().StartsWith(Name.ToLower()));

                C.Health += (int)Quantity;

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"{(Quantity>0?"Increased":"Decreased")} {C.Name}'s Health by {(int)Quantity}."));
                return;
            }
        }
    }
}
