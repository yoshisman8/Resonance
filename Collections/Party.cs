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

        public ulong GameMaster { get; set; }
        public List<Character> Actors { get; set; }
    }
}
