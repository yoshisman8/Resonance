using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Resonance.Collections
{
    public class RollData
    {
        public int[] dice { get; set; }
        public int Boosts { get; set; }
        public int Actor { get; set; }
        public string Serialize()
        {
            return Actor +","+ String.Join(".",dice) + "," + Boosts;
        }
        public RollData Deserialize(string input)
        {
            string[] vars = input.Split(",");
            int[] die = vars[1].Split(".").Select(x => int.Parse(x)).ToArray();
            return new RollData()
            {
                Actor = int.Parse(vars[0]),
                dice = die,
                Boosts = int.Parse(vars[2]),
            };
        }
    }
}
