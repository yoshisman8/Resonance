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
    public class CharacterModule : ApplicationCommandModule
    {
        public Services.Utilities Utils;
        public LiteDatabase db;

        [SlashCommand("Character","View or change your Active Character.")]
        public async Task ViewChar(InteractionContext ctx, [Option("Name","Name of the character you want to swap to.")]string name = null)
        {
            User U = Utils.GetUser(ctx.User.Id);

            if(name.NullorEmpty() && U.Active == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }
            else if (name.NullorEmpty())
            {
                Character C = U.Active;

                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        C.Render(0));
            }
            else
            {
                var col = db.GetCollection<Character>("Characters");

                var query = col.Find(x => x.Name.StartsWith(name.ToLower()) && x.Owner == ctx.User.Id);

                if(query.Count() == 0)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .WithContent($"You do not have a character whose name starts with {name}."));
                        return;
                }
                else
                {
                    Character C = query.FirstOrDefault();

                    U.Active = C;

                    Utils.UpdateUser(U);

                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        U.Active.Render(0).WithContent($"Changed active character to {C.Name}."));
                    return;
                }
            }
        }

        [SlashCommand("Health","Adjust your Active Character's Health Up or Down.")]
        public async Task Health(InteractionContext context, [Option("Value","Positive number heal, negative numbers harm.")]double Value)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            C.Health = Math.Max(0, Math.Min(C.GetMaxHealth(),C.Health + (int)Value));

            Utils.UpdateActor(C);

            await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent((Value > 0 ? $"{C.Name} gained {Value} Health Points (Current: {C.Health})." : $"{C.Name} loss {Math.Abs(Value)} Health Points (Current: {C.Health}).")));
        }

        [SlashCommand("Luck", "Adjust your Active Character's Luck Up or Down.")]
        public async Task Luck(InteractionContext context, [Option("Value", "Positive number add, negative numbers substract.")] double Value)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            C.Luck = Math.Max(0, Math.Min(10, C.Luck + (int)Value));

            Utils.UpdateActor(C);

            await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent((Value > 0 ? $"{C.Name} gained {Value} Luck Points (Current: {C.Luck})." : $"{C.Name} spent {Math.Abs(Value)} Luck Points (Current: {C.Luck}).")));
        }

        [SlashCommand("Color", "Assign a color to your Active Character. Must be a Hex Color Code.")]
        public async Task Color(InteractionContext context, [Option("Code", "Hex color code.")] string code)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C =  U.Active;

            try
            {
                var color = new DiscordColor(code);

                C.Color = color.ToString();

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                        .WithColor(color)
                        .WithDescription($"Changed {C.Name}'s Sheet color to " + color.ToString() + "!")));
            }
            catch
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .WithContent("This value is not a valid Hex color code (#AABBCC)."));
                return;
            }

        }

        [SlashCommand("Image", "Sets your active character's image.")]
        public async Task Image(InteractionContext context, [Option("URL", "Image URL.")] string url)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = Utils.GetActor(U.Active.Id);

            if (!url.IsImageUrl())
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .WithContent("This value is not a valid `.png` or `.jpeg` image URL!"));
                return;
            }
            else
            {
                C.Image = url;

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                    .WithContent($"Updated {C.Name}'s image!")
                    .AddEmbed(new DiscordEmbedBuilder().WithImageUrl(url).Build()));
            }
        }

        [SlashCommandGroup("Characters", "Manage your characters.")]
        public class CharacterManagement : ApplicationCommandModule
        {
            public Services.Utilities Utils;
            public LiteDatabase db;

            [SlashCommand("Create", "Creates a new Character.")]
            public async Task Create(InteractionContext ctx, [Option("Name", "The name of the character")] string Name, [Option("Level", "(Optional) The level of the characters. Defaults to 1")] double level = 1)
            {
                User user = Utils.GetUser(ctx.User.Id);

                var col = db.GetCollection<Character>("Characters");

                if (col.Exists(c => c.Name == Name.ToLower() && c.Owner == ctx.User.Id))
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent("You already have a character with this exact name!"));
                    return;
                }

                Character C = new Character() {
                    Name = Name,
                    Level = (int)level,
                    Owner = ctx.User.Id
                };

                C.Health = C.GetMaxHealth();

                col.Insert(C);

                col.EnsureIndex("Name", "LOWER($.Name)");
                col.EnsureIndex(x => x.Owner);

                Character _C = col.FindOne(x => x.Name == Name && x.Owner == ctx.User.Id);

                user.Active = _C;
                Utils.UpdateUser(user);

                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Successfully created character {Name} and assigned them as your current active character!"));
            }
            [SlashCommand("Rename", "Renames your active character.")]
            public async Task Rename(InteractionContext ctx, [Option("Name", "New name for your Active character.")] string Name)
            {
                User U = Utils.GetUser(ctx.User.Id);

                if (U.Active == null)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                    return;
                }
                else
                {
                    var col = db.GetCollection<Character>("Characters");

                    Character C = U.Active;
                    string old = C.Name;

                    if (col.Exists(c => c.Name == Name.ToLower() && c.Owner == ctx.User.Id))
                    {
                        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent("You already have a character with this exact name!"));
                        return;
                    }

                    C.Name = Name;

                    Utils.UpdateActor(C);

                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent($"Successfully renamed {old} to {Name}."));
                }
            }

            [SlashCommand("Delete", "Deletes a character. WARNING! THIS CANNOT BE UNDONE!")]
            public async Task Delete(InteractionContext ctx, [Option("Name", "Name of the character being deleted.")] string Name)
            {
                var col = db.GetCollection<Character>("Characters");

                var query = col.Find(x => x.Owner == ctx.User.Id && x.Name.StartsWith(Name.ToLower())).ToList();

                if (query.Count == 0)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .WithContent("Could not find any character you own with that name."));
                    return;
                }
                else
                {
                    var C = query.FirstOrDefault();

                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .WithContent($"Are you sure you want to delete {C.Name}?\n**WARNING! THIS CANNOT BE UNDONE!**")
                        .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Primary,"cancel","Cancel"),
                            new DiscordButtonComponent(ButtonStyle.Danger,$"dl{C.Id}","Delete")
                        }
                        ));
                }
            }

            [SlashCommand("LevelUp", "Levels up your active character.")]
            public async Task LevelUp(InteractionContext context, [Option("Levels", "(Optional) Amount of levels to increase. Defaults to 1.")] double Levels = 1)
            {
                User U = Utils.GetUser(context.User.Id);

                if (U.Active == null)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                    return;
                }

                Character C = U.Active;

                C.Level += (int)Levels;

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"{C.Name} is now Level {C.Level}!"));
            }

            [SlashCommand("LevelDown", "Levels down your active character.")]
            public async Task LevelDown(InteractionContext context, [Option("Levels", "(Optional) Amount of levels to decrease. Defaults to 1.")] double Levels = 1)
            {
                User U = Utils.GetUser(context.User.Id);

                if (U.Active == null)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                    return;
                }

                Character C = U.Active;

                C.Level = Math.Max(1, C.Level-(int)Levels);
                
                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"{C.Name} is now Level {C.Level}!"));
            }
 
        }

        [SlashCommandGroup("Attributes","Mange your active character attributes.")]
        public class AttributeManagement : ApplicationCommandModule
        {
            public Services.Utilities Utils;
            public LiteDatabase db;

            [SlashCommand("BonusHealth", "Set your active character's Bonus Maximum Health.")]
            public async Task BonusHP(InteractionContext context, [Option("Value", "The amount of Bonus Maximum Health to assign.")] double Value)
            {
                User U = Utils.GetUser(context.User.Id);

                if (U.Active == null)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                    return;
                }

                Character C = U.Active;

                C.BonusHealth = (int)Value;

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Updated {C.Name}'s Bonus Health to {Value}! Their new maximum health is now {C.GetMaxHealth()}"));
            }

            [SlashCommand("Vigor", "Set your Active Character's vigor.")]
            public async Task Vigor(InteractionContext context, [Option("Vigor", "New Vigor value. Minimum 1, Maximum 7.")] double value)
            {
                User U = Utils.GetUser(context.User.Id);

                if (U.Active == null)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                    return;
                }

                Character C = U.Active;

                if (value < 1 || value > 7)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Invalid value. Attributes can only go as low as 1 or as high as 7!"));
                    return;
                }
                C.Vigor = (int)value;

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Updated {C.Name}'s Vigor changed to {value}."));
            }

            [SlashCommand("Agility", "Set your Active Character's agility.")]
            public async Task Agility(InteractionContext context, [Option("Agility", "New agility value. Minimum 1, Maximum 7.")] double value)
            {
                User U = Utils.GetUser(context.User.Id);

                if (U.Active == null)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                    return;
                }

                Character C = U.Active;

                if (value < 1 || value > 7)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Invalid value. Attributes can only go as low as 1 or as high as 7!"));
                    return;
                }
                C.Agility = (int)value;

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Updated {C.Name}'s Agility changed to {value}."));
            }

            [SlashCommand("Insight", "Set your Active Character's vigor.")]
            public async Task Insight(InteractionContext context, [Option("Insight", "New Insight value. Minimum 1, Maximum 7.")] double value)
            {
                User U = Utils.GetUser(context.User.Id);

                if (U.Active == null)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                    return;
                }

                Character C = U.Active;

                if (value < 1 || value > 7)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Invalid value. Attributes can only go as low as 1 or as high as 7!"));
                    return;
                }
                C.Insight = (int)value;

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Updated {C.Name}'s Insight changed to {value}."));
            }

            [SlashCommand("Presence", "Set your Active Character's presence.")]
            public async Task Presence(InteractionContext context, [Option("Presence", "New Presence value. Minimum 1, Maximum 7.")] double value)
            {
                User U = Utils.GetUser(context.User.Id);

                if (U.Active == null)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                    return;
                }

                Character C = U.Active;

                if (value < 1 || value > 7)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Invalid value. Attributes can only go as low as 1 or as high as 7!"));
                    return;
                }
                C.Presence = (int)value;

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Updated {C.Name}'s Presence changed to {value}."));
            }
        }
    }

    [SlashCommandGroup("Skills","Manage your active character's skills.")]
    public class SkillManagement : ApplicationCommandModule
    {
        public Services.Utilities Utils;
        public LiteDatabase db;

        [SlashCommand("Add","Add a new skill to your active character.")]
        public async Task Add(InteractionContext context, [Option("Name","Name of the Skill.")]string Name,
            [Option("Description","A brief description as to what this skill is for.")]string Desc,
            [Option("Type","What type of Skill is this?")]SkillType Type,
            [Option("Ranks","How many point should this skill start with? Defaults to 1.")]double Ranks = 1)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            if(C.Skills.Exists(x=>x.Name.ToLower() == Name))
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"{C.Name} already has a skill with that exact name!"));
                return;
            }
            Ranks = Math.Abs(Ranks);


            Skill sk = new Skill()
            {
                Name = Name,
                Description = Desc,
                Type = Type,
                Ranks = (int)Math.Min((int)Type, Ranks)
            };

            C.Skills.Add(sk);

            Utils.UpdateActor(C);

            await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"Added {Type} skill {Name} to {C.Name} with {Ranks} ranks!"));
            return;
        }

        [SlashCommand("Ranks","Set the ranks of a Skill on your active character.")]
        public async Task Update(InteractionContext context, [Option("Name","Name of the Skill being altered.")]string Name,
            [Option("Ranks","New Rank value for the Skill.")]double Ranks)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            var query = C.Skills.FindAll(x => x.Name.ToLower().StartsWith(Name.ToLower()));

            if(query.Count == 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"{C.Name} does not have any skill whose name starts with \"{Name}\"."));
                return;
            }
            else
            {
                Skill Sk = query.FirstOrDefault();

                int index = C.Skills.FindIndex(x => x.Name == Sk.Name);

                int value = (int)Math.Abs(Ranks);

                C.Skills[index].Ranks = Math.Min(value, (int)Sk.Type);

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"Updated {C.Name}'s {Sk.Name} skill to have {Math.Min(value, (int)Sk.Type)} ranks."));
                return;
            }
        }

        [SlashCommand("Delete", "Deletes a skill on your active character. WARNING! THIS CANNOT BE UNDONE!")]
        public async Task Delete(InteractionContext context, [Option("Name", "Name of the Skill being altered.")] string Name)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            var query = C.Skills.FindAll(x => x.Name.ToLower().StartsWith(Name.ToLower()));

            if (query.Count == 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"{C.Name} does not have any skill whose name starts with \"{Name}\"."));
                return;
            }
            else
            {
                Skill Sk = query.FirstOrDefault();

                int index = C.Skills.FindIndex(x => x.Name == Sk.Name);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .WithContent($"Are you sure you want to delete the skill {Sk.Name} from {C.Name}?\n**WARNING! THIS CANNOT BE UNDONE!**")
                        .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Primary,"cancel","Cancel"),
                            new DiscordButtonComponent(ButtonStyle.Danger,$"kd{C.Id},{index}","Delete")
                        }
                        ));
            }
        }

    }

    [SlashCommandGroup("Inventory","Mange your active character's inventory.")]
    public class InventoryManagement : ApplicationCommandModule
    {
        public Services.Utilities Utils;
        public LiteDatabase db;

        [SlashCommand("Currency","Increase or Decrease your active character's currency.")]
        public async Task Currency(InteractionContext context, [Option("Value", "Positive number add, negative numbers substract.")]double Value)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            C.Currency = (int)Math.Max(0, C.Currency + Value);

            Utils.UpdateActor(C);

            await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent((Value > 0 ? $"{C.Name} gained {Value} Currency (Total: {C.Currency})." : $"{C.Name} spent {Math.Abs(Value)} Currency (Remaining: {C.Currency}).")));
        }

        [SlashCommand("Add", "Add an item to your active character's inventory.")]
        public async Task Add(InteractionContext context, [Option("Name","Name of the Item.")]string Name,
            [Option("Description","What does this item do?")]string Description,
            [Option("Type","What kind of item is this?")]ItemTypes Type,
            [Option("Quantity","(Optional) How much of this item do you want to add? Defaults to 1")]double Quantity = 1)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;
            if(C.Inventory.Exists(x=>x.Name.ToLower() == Name.ToLower()))
            {
                var index = C.Inventory.FindIndex(x => x.Name.ToLower() == Name.ToLower());
                
                Item I = C.Inventory[index];

                C.Inventory[index].Quantity += (int)Math.Abs(Quantity);

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"Added {Math.Abs(Quantity)} {I.Name} to {C.Name}'s inventory!"));
                return;
            }
            else
            {
                Item I = new Item()
                {
                    Name = Name,
                    Description = Description,
                    Type = Type,
                    Quantity = (int)Quantity
                };

                C.Inventory.Add(I);

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"Added {Math.Abs(Quantity)} {I.Name} to {C.Name}'s inventory!"));
                return;
            }
        }

        [SlashCommand("Duplicate","Duplicates an existing item in your active character's inventory. Use this for adding consumables.")]
        public async Task Dupe(InteractionContext context, [Option("Name","Name of the item.")]string Name,
            [Option("Quantity","How many of this item to add to your sheet.")]double Quantity)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            var query = C.Inventory.FindAll(x => x.Name.ToLower().StartsWith(Name.ToLower()));

            if (query.Count == 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"{C.Name} does not have any skill whose name starts with \"{Name}\"."));
                return;
            }
            else
            {
                Item I = query.FirstOrDefault();

                int index = C.Inventory.FindIndex(x => x.Name == I.Name);

                C.Inventory[index].Quantity += (int)Math.Abs(Quantity);

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"Added {Math.Abs(Quantity)} copies of {I.Name} to {C.Name}'s inventory."));
                return;
            }
        }


        [SlashCommand("Use","Equip or Consume an item.")]
        public async Task Use(InteractionContext context, [Option("Name","Name of the item")]string Name,
            [Option("Quantity","(Optional) Amount of said item to be used up. Defaults to 1.")]double Quantity = 1)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            var query = C.Inventory.FindAll(x => x.Name.ToLower().StartsWith(Name.ToLower()));

            if (query.Count == 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"{C.Name} does not have any skill whose name starts with \"{Name}\"."));
                return;
            }
            else
            {
                Item I = query.FirstOrDefault();

                int index = C.Inventory.FindIndex(x => x.Name == I.Name);

                if(I.Type == ItemTypes.Consumable && (I.Quantity == 0 || I.Quantity - Quantity < 0))
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"{C.Name} does not have any more (Or enough of) the item \"{I.Name}\". If you need more, use the `/Inventory Duplicate` command to add more of this item!"));
                    return;
                }

                if(I.Type == ItemTypes.Consumable)
                {
                    C.Inventory[index].Quantity -= (int)Quantity;

                    Utils.UpdateActor(C);

                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"{C.Name} uses {Quantity} \"{I.Name}\"!")
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle(I.Name)
                        .WithDescription(I.Description)
                        .WithColor(new DiscordColor(C.Color))
                        .WithThumbnail(C.Image)
                        ));
                    return;
                }else if(I.Type == ItemTypes.Misc)
                {
                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"{C.Name} uses their \"{I.Name}\"!")
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle(I.Name)
                        .WithDescription(I.Description)
                        .WithColor(new DiscordColor(C.Color))
                        .WithThumbnail(C.Image)
                        ));
                    return;
                }
                else
                {

                    C.Inventory[index].Equipped ^= true;

                    Utils.UpdateActor(C);

                    await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .WithContent($"{C.Name} {(C.Inventory[index].Equipped ? "Equips" : "Unequips")} their {I.Name}."));
                }
            }
        }

        [SlashCommand("Discard", "Remove an entire item stack from your Inventory entirely.")]
        public async Task Discard(InteractionContext context, [Option("Name","Name of the item being discarded.")]string Name)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            var query = C.Inventory.FindAll(x => x.Name.ToLower().StartsWith(Name.ToLower()));

            if (query.Count == 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"{C.Name} does not have any skill whose name starts with \"{Name}\"."));
                return;
            }
            else
            {
                Item I = query.FirstOrDefault();

                int index = C.Inventory.FindIndex(x => x.Name == I.Name);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                        .WithContent($"Are you sure you want to delete the item {I.Name} (Including the entire stack, in the case of consumables) from {C.Name}'s inventory?\n**WARNING! THIS CANNOT BE UNDONE!**\n**USE `/Inventory Use` TO SPEND ITEMS ONE AT A TIME INSTEAD**")
                        .AddComponents(new DiscordComponent[]
                        {
                            new DiscordButtonComponent(ButtonStyle.Primary,"cancel","Cancel"),
                            new DiscordButtonComponent(ButtonStyle.Danger,$"id{C.Id},{index}","Delete")
                        }
                        ));
            }
        }
    }
    
    [SlashCommandGroup("Conditions","Manage your active character's conditions.")]
    public class ConditionManagement : ApplicationCommandModule
    {
        public Services.Utilities Utils;
        public LiteDatabase db;

        [SlashCommand("Add","Adds a condition to your active character.")]
        public async Task Add(InteractionContext context, [Option("Name","The name of the condition")]string Name,[Option("Description","What does this condition do?")]string Description)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            Tracker Cond = new Tracker()
            {
                Name = Name,
                Description = Description
            };

            C.Conditions.Add(Cond);

            Utils.UpdateActor(C);

            await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"{C.Name} Is now affected by the {Name} condition!")
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle(Name)
                        .WithDescription(Description)));
        }

        [SlashCommand("Remove","Remove a given condition on your active character.")]
        public async Task Remove(InteractionContext context, [Option("Name","Name of the condition to remove")]string Name)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            var Q = C.Conditions.Where(x => x.Name.ToLower().StartsWith(Name.ToLower()));

            if(Q.Count() == 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"{C.Name} is not affected by a condition whose name starts with \"{Name}\""));
                return;
            }
            else
            {
                var Cond = Q.FirstOrDefault();

                var index = C.Conditions.IndexOf(Cond);

                C.Conditions.RemoveAt(index);

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Removed condition {Cond.Name} from {C.Name}."));
            }
        }
    }

    [SlashCommandGroup("Techniques","Manage your active character's techniques.")]
    public class TechManagement : ApplicationCommandModule
    {
        public Services.Utilities Utils;
        public LiteDatabase db;

        [SlashCommand("Add", "Adds a technique to your active character.")]
        public async Task Add(InteractionContext context, 
            [Option("Name", "The name of the technique")] string Name,
            [Option("Actions","What action does this tecnique use?")]ActionType actionType, 
            [Option("Attribute","What attribute is used on this Technique?")] Attributes Attribute,
            [Option("Skill","Skill used for this Techinque.")] string Skill,
            [Option("Description", "What does this techinque do?")] string Description)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            if (C.Techniques.Exists(x=>x.Name.ToLower() == Name.ToLower()))
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                    .WithContent($"{C.Name} already has a technique named \"{Name}\"!"));
                return;
            }
            Tech Tech = new Tech()
            {
                Name = Name,
                Action = actionType,
                Description = Description,
                Attribute = Attribute
            };

            var Q = C.Skills.Where(x => x.Name.ToLower().StartsWith(Skill.ToLower()));

            if(Q.Count() == 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                    .WithContent($"{C.Name} has no skill whose name starts with \"{Name}\"!"));
                return;
            }

            Skill Sk = Q.FirstOrDefault();

            Tech.Skill = Sk.Name;

            C.Techniques.Add(Tech);

            Utils.UpdateActor(C);

            await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"Added Tech {Name} to {C.Name}!")
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle($"{Name} {(actionType == ActionType.Simple?Dictionaries.Icons["emptyDot"]:Dictionaries.Icons["dot"])}")
                        .WithColor(new DiscordColor(C.Color))
                        .WithThumbnail(C.Image)
                        .WithDescription($"**[{Tech.Attribute} + {Sk.Name}]**\n{Description}")));
        }

        [SlashCommand("Remove", "Remove a technique on your active character.")]
        public async Task Remove(InteractionContext context, [Option("Name", "Name of the condition to remove")] string Name)
        {
            User U = Utils.GetUser(context.User.Id);

            if (U.Active == null)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You do not have an active character. Create one using the `/Character Create` command or select an existing one using `/Character Select` first!"));
                return;
            }

            Character C = U.Active;

            var Q = C.Techniques.Where(x => x.Name.ToLower().StartsWith(Name.ToLower()));

            if (Q.Count() == 0)
            {
                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"{C.Name} does not have any techniques whose name starts with \"{Name}\""));
                return;
            }
            else
            {
                var tech = Q.FirstOrDefault();

                var index = C.Techniques.IndexOf(tech);

                C.Conditions.RemoveAt(index);

                Utils.UpdateActor(C);

                await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"Removed technique {tech.Name} from {C.Name}."));
            }
        }
    }
}
