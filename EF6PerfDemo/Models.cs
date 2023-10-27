using System;
using System.Collections.Generic;

namespace EF6PerfDemo
{
    public class Trainer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public virtual List<Pokemon> Pokemon { get; set; }
    }

    public class Pokemon
    {
        public int Id { get; set; }

        public int Trainer_Id { get; set; }
        public virtual Trainer Trainer { get; set; }

        public string Name { get; set; }

        public int PokeType_Id { get; set; }
        public virtual PokeType PokeType { get; set; }

        public virtual List<Move> Moves { get; set; }

        public virtual List<WinRecord> WinRecords { get; set; }
    }

    public class PokeType
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Move
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public int Pokemon_Id { get;set; }
        public virtual Pokemon Pokemon { get; set; }

        public int DamageType_Id { get; set; }
        public virtual PokeType DamageType { get; set; }
    }

    public class WinRecord
    {
        public int Id { get; set; }
        public int Pokemon_Id { get; set; }

        public DateTime DateTime { get; set; }
        public bool Win { get; set; }
    }
}