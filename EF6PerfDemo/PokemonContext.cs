using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;

namespace EF6PerfDemo
{
    public class PokemonContext: DbContext
    {
        public DbSet<Pokemon> Pokemon { get; set; }
        public DbSet<PokeType> PokeTypes { get; set; }
        public DbSet<Move> Moves { get; set; }
        public DbSet<WinRecord> WinRecords { get; set; }
        public DbSet<Trainer> Trainers { get; set; }

        public PokemonContext() : base(Options.ConnectionString)
        {
            Database.SetInitializer(new DropCreateDatabaseIfModelChanges<PokemonContext>());
            Configuration.LazyLoadingEnabled = true;
            Configuration.ProxyCreationEnabled = true;


            #if DEBUG
            Database.Log = s => Debug.Write(s);
            #endif
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Trainer>()
                .HasMany(t => t.Pokemon)
                .WithRequired(p => p.Trainer)
                .HasForeignKey(p => p.Trainer_Id);

            modelBuilder.Entity<Pokemon>()
                .HasMany(m => m.Moves)
                .WithRequired(m => m.Pokemon)
                .HasForeignKey(m => m.Pokemon_Id);

            modelBuilder.Entity<Pokemon>()
                .HasMany(p => p.WinRecords)
                .WithRequired()
                .HasForeignKey(w => w.Pokemon_Id);

            modelBuilder.Entity<Pokemon>()
                .HasRequired(m => m.PokeType)
                .WithMany()
                .HasForeignKey(m => m.PokeType_Id)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PokeType>();
            modelBuilder.Entity<Move>()
                .HasRequired(m => m.DamageType)
                .WithMany()
                .HasForeignKey(m => m.DamageType_Id)
                .WillCascadeOnDelete(false);
        }

        public void Seed()
        {
            Database.ExecuteSqlCommand("DELETE FROM [Pokemons]");
            Database.ExecuteSqlCommand("DELETE FROM [Moves]");
            Database.ExecuteSqlCommand("DELETE FROM [PokeTypes]");
            Database.ExecuteSqlCommand("DELETE FROM [Trainers]");


            var fire = new PokeType() {  Name = "Fire" };
            var water = new PokeType() {  Name = "Water" };
            var grass = new PokeType() {  Name = "Grass" };
            var electric = new PokeType() {  Name = "Electric" };
            var normal = new PokeType() { Name = "Normal" };

            var ash = new Trainer()
            {
                Name = "Ash",
                Pokemon = new List<Pokemon>()
                {
                    new Pokemon()
                    {
                         Name = "Charmander", PokeType = fire, Moves = new List<Move>()
                        {
                            new Move() {  Name = "Tackle", DamageType = normal },
                            new Move() { Name = "Ember", DamageType = fire },
                        },
                        WinRecords = CreateWinRecords(7),
                    },

                    new Pokemon()
                    {
                         Name = "Bulbasaur", PokeType = grass, Moves = new List<Move>()
                        {
                            new Move() { Name = "Tackle", DamageType = normal },
                            new Move() { Name = "Grass Seed", DamageType = grass },
                        },
                        WinRecords = CreateWinRecords(4),
                    },
                    new Pokemon()
                    {
                        Name = "Squirtle", PokeType = water, Moves = new List<Move>()
                        {
                            new Move() {  Name = "Tackle", DamageType = normal },
                            new Move() {  Name = "Bubble", DamageType = water },

                        },
                        WinRecords = CreateWinRecords(2),
                    },
                    new Pokemon()
                    {
                        Id = 4, Name = "Pikachu", PokeType = electric, Moves = new List<Move>()
                        {
                            new Move() { Name = "Tackle", DamageType = normal },
                            new Move() { Name = "Electric Shock", DamageType = electric },
                        },
                        WinRecords = CreateWinRecords(10),
                    }
                }
            };

            var misty = new Trainer()
            {
                Name = "Misty",
                Pokemon = new List<Pokemon>()
                {
                    new Pokemon()
                    {
                        Name = "Staryu",
                        PokeType = water,
                        Moves = new List<Move>()
                        {
                            new Move()
                            {
                                Name = "Splash",
                                DamageType = water
                            }
                        },
                        WinRecords = CreateWinRecords(50),
                    },
                    new Pokemon()
                    {

                        Name = "Starmie",
                        PokeType = water,
                        Moves = new List<Move>()
                        {
                            new Move()
                            {
                                Name = "Splash",
                                DamageType = water
                            },
                            new Move()
                            {
                                Name = "Takedown",
                                DamageType = normal
                            }
                        },
                        WinRecords = CreateWinRecords(45),
                    }
                }
            };


            Trainers.Add(ash);
            Trainers.Add(misty);

            SaveChanges();
        }

        private Random _random = new Random();

        List<WinRecord> CreateWinRecords(int count)
        {
            return Enumerable.Range(0, count).Select(i => new WinRecord()
            {
                DateTime = DateTime.Now.Subtract(TimeSpan.FromDays(_random.NextDouble() * 365)),
                Win = _random.NextDouble() > 0.5

            }).ToList();
        }
    }
}