using System;
using System.Collections.Generic;
using System.Text;
using DSharpPlus.Entities;
using LiteDB;
using Resonance.Services;
using System.Linq;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using System.Diagnostics.CodeAnalysis;

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
        public List<Tech> Techniques { get; set; } = new List<Tech>();
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
            if(page == 2)
            {
                var ConditionsPage = new DiscordEmbedBuilder()
                    .WithTitle($"{Name} - Lv.{Level}")
                    .WithColor(new DiscordColor(Color))
                    .WithThumbnail(Image)
                    .WithDescription($"Health [{Health}/{GetMaxHealth()}]\n{Health.Bar(GetMaxHealth(), false)}\nLuck [{Luck}/10]\n{Luck.Bar(10, true)}\n\n**Conditions**");

                foreach(var con in Conditions.OrderBy(x => x.Name))
                {
                    ConditionsPage.AddField("• "+con.Name, con.Description);
                }

                Embed = ConditionsPage.Build();
            }
            if (page > 2)
            {
                var TechPage = new DiscordEmbedBuilder()
                    .WithTitle($"{Name} - Lv.{Level}")
                    .WithColor(new DiscordColor(Color))
                    .WithThumbnail(Image)
                    .WithDescription($"Health [{Health}/{GetMaxHealth()}]\n{Health.Bar(GetMaxHealth(), false)}\nLuck [{Luck}/10]\n{Luck.Bar(10, true)}\n\n**Techniques**");

                var tech = new List<Tech>();
                var sorted = Techniques.OrderBy(x => x.Name);

                int startPoint = 0 + (4 * (page - 3));

                for(int i = 0; i < 4; i++)
                {
                    if (startPoint + i >= Techniques.Count) break;
                    tech.Add(Techniques[startPoint + i]);
                }
                foreach(var t in tech)
                {
                    TechPage.AddField($"• {t.Name} {(t.Action == ActionType.Simple ? $"{Dictionaries.Icons["emptyDot"]}" : $"{Dictionaries.Icons["dot"]}")}", $"> **[{t.Attribute} + {t.Skill}]**\n> {t.Description}");
                }

                Embed = TechPage.Build();
            }
            var buttons = new List<DiscordComponent>()
            {
                new DiscordButtonComponent(ButtonStyle.Primary,"s,0,"+Id,"Main Page"),
                new DiscordButtonComponent(ButtonStyle.Primary,"s,1,"+Id,"Skills")
            };

            if(Conditions.Count > 0)
            {
                buttons.Add(new DiscordButtonComponent(ButtonStyle.Primary, $"s,2,{Id}", "Conditions"));
            }

            var builder = new DiscordInteractionResponseBuilder()
                .AddEmbed(Embed)
                .AddComponents(buttons);

            if (Techniques.Count > 0)
            {
                int techPages = (int)Math.Ceiling(((double)Techniques.Count / (double)4));
                var techButtons = new List<DiscordComponent>();

                for (int t = 0; t < Math.Min(techPages,4); t++)
                {
                    techButtons.Add(new DiscordButtonComponent(ButtonStyle.Primary, $"s,{3 + t},{Id}", $"Techniques {(t + 1)}"));
                }

                builder.AddComponents(techButtons);
            }

            

            return builder;
        }
    }
    public class Tracker : IEquatable<Tracker>
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public bool Equals([AllowNull] Tracker other)
        {
            return (Name.ToLower() == other.Name.ToLower()) && (Description.ToLower() == other.Description.ToLower());
        }
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

    public class Tech : IEquatable<Tech>
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public ActionType Action { get; set; }
        public string Skill { get; set; }
        public Attributes Attribute { get; set; }
        public bool Equals([AllowNull] Tech other)
        {
            return (Name.ToLower() == other.Name.ToLower()) && (Description.ToLower() == other.Description.ToLower());
        }
    }

    public enum ActionType
    {
        [ChoiceName("Simple Action")]
        Simple,
        [ChoiceName("Complex Action")]
        Complex
    }
    public enum Attributes
    {
        [ChoiceName("Vigor")]
        Vigor = 1,
        [ChoiceName("Agility")]
        Agility = 2,
        [ChoiceName("Insight")]
        Insight = 3,
        [ChoiceName("Presence")]
        Presence = 4
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
