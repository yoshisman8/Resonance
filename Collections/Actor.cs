using System;
using System.Collections.Generic;
using System.Text;
using DSharpPlus.Entities;
using LiteDB;
using Resonance.Services;
using System.Linq;
using DSharpPlus;
using DSharpPlus.SlashCommands;

namespace Resonance.Collections
{
    public class Actor
    {
        [BsonId]
        public int Id { get; set; }
        public string Name { get; set; }
        public ulong Owner { get; set; }

        // Character Variables
        public int Level { get; set; } = 1;

        // Health is equal to 5 + Vigor + 1/3rd of your Level
        public int Health { get; set; } = 5;
        public int BonusHealth { get; set; } = 0;

        // Ability scores. Cap at 7
        public int Vigor { get; set; } = 1;
        public int Agility { get; set; } = 1;
        public int Insight { get; set; } = 1;
        public int Presence { get; set; } = 1;

        // List of skills including basic general skills
        public List<Skill> Skills { get; set; } = new List<Skill>() {
            new Skill()
            {
                Name = "Athletics",
                Description = "General capacity for athleticism in all forms.",
                Ranks = 0,
                Type = SkillType.General
            },
            new Skill()
            {
                Name = "Acrobatics",
                Description = "Flexibility and reflexive movement in its purest form.",
                Ranks = 0,
                Type = SkillType.General
            },
            new Skill()
            {
                Name = "Academics",
                Description = "General knowledge about many things of all subjects.",
                Ranks = 0,
                Type = SkillType.General
            },
            new Skill()
            {
                Name = "Charisma",
                Description = "Social tact and force of influence in its most general form.",
                Ranks = 0,
                Type = SkillType.General
            },
            new Skill()
            {
                Name = "Martial Combat",
                Description = "Prowess with all manner of weapons.",
                Ranks = 0,
                Type = SkillType.General
            },
            new Skill()
            {
                Name = "Magic",
                Description = "Capacity for all magical prowess.",
                Ranks = 0,
                Type = SkillType.General
            }
        };

        public int GetMaxHealth()
        {
            return (int)(5 + Math.Floor((decimal)Level / 2) + Vigor);
        }
    }
    
    public class Character : Actor
    {
        public string Bio { get; set; }
        public string Image { get; set; }
        public string Color { get; set; } = "#696866";

        // Luck caps at 10
        public int Luck { get; set; } = 10;

        public int Currency { get; set; } = 0;
        public List<Item> Inventory { get; set; } = new List<Item>();

        public List<Tracker> Conditions { get; set; } = new List<Tracker>();
        public List<Tracker> Trackers { get; set; } = new List<Tracker>();

        public DiscordInteractionResponseBuilder Render(int page)
        {
            DiscordEmbed Embed = null;

            if(page == 0)
            {
                var MainPage = new DiscordEmbedBuilder()
                    .WithTitle($"{Name} - Lv.{Level}")
                    .WithColor(new DiscordColor(Color))
                    .WithThumbnail(Image)
                    .WithDescription($"Health [{Health}/{GetMaxHealth()}]\n{Health.Bar(GetMaxHealth(),false)}\nLuck [{Luck}/10]\n{Luck.Bar(10,true)}");

                string attributes = $"**Vigor**\n> {Vigor.Dots(7)}\n**Agility**\n> {Agility.Dots(7)}\n**Insight**\n> {Insight.Dots(7)}\n**Presence**\n> {Presence.Dots(7)}";

                StringBuilder sb = new StringBuilder();

                foreach (var Skill in Skills.OrderBy(x => x.Name))
                {
                    sb.AppendLine($"{Dictionaries.Dots[Skill.Ranks]} **{Skill.Name}**");
                    //sb.AppendLine($"> ");
                }

                decimal maxAtt = 8 + Math.Floor((decimal)Level / 3);
                int currAtt = Vigor + Agility + Insight + Presence - 4;

                decimal maxSkill = 8 + Level - 1;
                int currSkill = Skills.Select(x => x.Ranks).Sum();
                
                MainPage.AddField($"Attributes {(currAtt < maxAtt?$"[{currAtt}/{maxAtt}]":"")}", attributes,true);
                MainPage.AddField($"Skills {(currSkill < maxSkill ? $"[{currSkill}/{maxSkill}]" : "")}", sb.ToString(), true);

                sb.Clear();

                foreach(var item in Inventory.OrderBy(x=> x.Name))
                {
                    if(item.Type == ItemTypes.Consumable || item.Type == ItemTypes.Misc)
                    {
                        sb.AppendLine($"🔸 {item.Name} x{item.Quantity}");
                    }
                    else 
                    {
                        sb.AppendLine($"{(item.Equipped ? Dictionaries.Icons["dot"] : Dictionaries.Icons["emptyDot"])} {item.Name} x{item.Quantity}");
                    } 
                }
                MainPage.AddField("Inventory", $"Currency: {Currency}\n{sb.ToString()}");

                Embed = MainPage.Build();
            }
            if(page == 1)
            {

                decimal maxSkill = 8 + Math.Floor((decimal)Level / 2);
                int currSkill = Skills.Select(x => x.Ranks).Sum();


                var SkillsPage = new DiscordEmbedBuilder()
                    .WithTitle($"{Name} - Lv.{Level}")
                    .WithColor(new DiscordColor(Color))
                    .WithThumbnail(Image)
                    .WithDescription($"Health [{Health}/{GetMaxHealth()}]\n{Health.Bar(GetMaxHealth(), false)}\nLuck [{Luck}/10]\n{Luck.Bar(10, true)}\n\n**Skills**");

                foreach(var sk in Skills.OrderBy(x=>x.Name))
                {
                    SkillsPage.AddField($"• **{sk.Name}** {sk.Ranks.Dots((int)sk.Type)}", sk.Description);
                }

                Embed = SkillsPage.Build();
            }

            var buttons = new List<DiscordComponent>()
            {
                new DiscordButtonComponent(ButtonStyle.Primary,"s,0,"+Id,"Main Page"),
                new DiscordButtonComponent(ButtonStyle.Primary,"s,1,"+Id,"Skills")
            };

            var builder = new DiscordInteractionResponseBuilder()
                .AddEmbed(Embed)
                .AddComponents(buttons);

            return builder;
        }
    }
    public class Tracker
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int Value { get; set; } = 0;
        public int Max { get; set; } = 0;
    }
    public class Skill
    {
        public string Name { get; set; }
        public SkillType Type { get; set; } = SkillType.Advanced;
        public int Ranks { get; set; }
        public string Description { get; set; }
    }

    public class Item
    {
        public string Name { get; set; }
        public bool Equipped { get; set; } = false;
        public ItemTypes Type { get; set; } = ItemTypes.Misc;
        public int Quantity { get; set; } = 1;
        public string Description { get; set; }
        public int Value { get; set; } = 0;
    }
    public enum SkillType { [ChoiceName("General Skill. Max 3 Ranks.")]General = 3, [ChoiceName("Advanced Skill. Max 5 Ranks.")] Advanced = 5, [ChoiceName("Specialized Skill. Max 7 Ranks.")] Specialized = 7 }
    public enum ItemTypes { 
        [ChoiceName("Light Weapon")]
        LightWeapon, 
        [ChoiceName("Simple Weapon")]
        SimpleWeapon, 
        [ChoiceName("Heavy Weapon")]
        HeavyWeapon, 
        [ChoiceName("Light Armor/Shield")]
        LightArmor, 
        [ChoiceName("Medium Armor/Shield")]
        MediumArmor, 
        [ChoiceName("Heavy Armor/Shield")]
        HeavyArmor, 
        [ChoiceName("Consumable/Expendable Item")]
        Consumable, 
        [ChoiceName("Accessory")]
        Accessory, 
        [ChoiceName("Miscellaneous/Non-Usable/Non-Wearable")]
        Misc 
    }
}
