using System;
using System.Collections.Generic;
using System.Text;
using LiteDB;

namespace Resonance.Collections
{
    public class User
    {
        [BsonId]
        public ulong ID { get; set; }
        [BsonRef("Characters")]
        public Character Active { get; set; }
    }
}
