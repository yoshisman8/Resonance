using System;
using System.Collections.Generic;
using System.Text;
using LiteDB;

namespace Resonance.Collections
{
    public class Party
    {
        [BsonId]
        public int ID { get; set; }
        public string Name { get; set; }
        public ulong GameMaster { get; set; }
        public ulong Guild { get; set; }
        [BsonRef("Characters")]
        public List<Character> Actors { get; set; } = new List<Character>();
    }
}
