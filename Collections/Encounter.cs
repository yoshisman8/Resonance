using System;
using System.Collections.Generic;
using System.Text;
using LiteDB;
using System.Linq;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System.Diagnostics.CodeAnalysis;

namespace Resonance.Collections
{
    public class Encounter
    {

        [BsonId]
        public ulong ChannelId { get; set; }
        public ulong GameMaster { get; set; }
        public bool Active { get; set; } = false;
        public bool Initiated { get; set; } = false;
        public int Round { get; set; } = 1;

        public List<Combatant> Combatants { get; set; } = new List<Combatant>();
        public Combatant Current { get; set; } = null; 

        public Combatant NextTurn()
        {
            int index = Combatants.IndexOf(Current);

            if (index + 1 >= Combatants.Count) { index = 0; Round++; }
            else index++;
            Combatant Comb = Combatants[index];
            Current = Comb;
            return Comb;
        }
        public Combatant PrevTurn()
        {
            int index = Combatants.IndexOf(Current);

            if (index - 1 == 0) { index = Combatants.Count - 1; Round--; }
            else index--;

            Combatant Comb = Combatants[index];
            Current = Comb;
            return Comb;
        }

        public void Start()
        {
            Initiated = true;
            Current = Combatants[0];
        }

        public void End()
        {
            Combatants = new List<Combatant>();
            Current = null;
            Active = false;
            Initiated = false;
        }

        public void Move(Combatant combatant, Position position)
        {
            int index = Combatants.IndexOf(combatant);

            if (Current == combatant)
            {
                Combatants[index].Position = position;
                Current = Combatants[index];
            }
            else
            {
                Combatants[index].Position = position;
            }
            
        }
        public void Add(Combatant _combatant)
        {
            Combatants.Add(_combatant);

            Combatants = Combatants.OrderByDescending(x => x.Initiative).ToList();
        }

        public void Remove(Combatant _combatant)
        {
            int index = Combatants.IndexOf(_combatant);

            if (Current == Combatants[index])
            {
                NextTurn();
            }

            Combatants.RemoveAt(index);
        }
    }

    public class Combatant : IEquatable<Combatant>
    {
        public double Initiative { get; set; }
        public Actor Actor { get; set; } = null;
        public string Name { get; set; }
        public Position Position { get; set; } = Position.Center;

        public bool Equals([AllowNull] Combatant other)
        {
            if (Actor == null) return Name == other.Name;
            else return (Name == other.Name) && (Actor.Id == other.Actor.Id);
        }
    }

    public enum Position { 
        [ChoiceName("Ally Far Range")]
        Ally = 0,
        [ChoiceName("Close Range")]
        Center = 1,
        [ChoiceName("Enemy Far Range")]
        Enemy = 2
    }
}
