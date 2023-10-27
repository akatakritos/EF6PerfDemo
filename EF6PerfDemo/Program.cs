using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Data.Entity;
using Dapper;

namespace EF6PerfDemo
{
    static class Options
    {
        public const string ConnectionString =
            "server=mac-host;user id=sa;password=SuperSecretPassw0rd!;encrypt=false;multipleactiveresultsets=true;initial catalog=EF6PerfDemo";
    }

    internal class Program
    {
        public static void Main(string[] args)
        {

            Seed();

            // LazyLoad();
            // EagerLoadMovesAndWins();
            // EagerLoadEveryThing();
            // ManualLoading();
            // Dapper();

            TimeIt(LazyLoad);
            TimeIt(EagerLoadMovesAndWins);
            TimeIt(EagerLoadEveryThing);
            TimeIt(EagerLoadEveryThingNoTracking);
            TimeIt(ManualLoading);
            TimeIt(Dapper);
        }


        public static void Seed()
        {
            using var context = new PokemonContext();
            context.Seed();
        }

        /// <summary>
        /// This method relies on the default lazy loading feature. Anytime you read a nested navigation property,
        /// EF will check its cache before making another database query. This can be a performance problem.
        /// </summary>
        public static void LazyLoad()
        {
            using var context = new PokemonContext();
            var pokemon = context.Pokemon.ToList();

            foreach (var poke in pokemon)
            {
                PrintPokemonStats(poke);
            }
        }

        /// <summary>
        /// Here we try to eager load moves and wins, but rely on lazy loading to pull back trainers and type information
        /// This reduces queries, but the more Include calls you have the gnarlier the SQL. At some point it becomes worse
        /// than making multiple separate queries
        /// </summary>
        public static void EagerLoadMovesAndWins()
        {
            using var context = new PokemonContext();

            var pokemon = context.Pokemon
                .Include(p => p.Moves)
                .Include(p => p.WinRecords)
                .ToList();

            foreach (var poke in pokemon)
            {
                PrintPokemonStats(poke);
            }
        }

        /// <summary>
        /// Here we just load everything in one big call. There are only 6 pokemon, but if you run the query in management studio
        /// you'll see it bring back ~130 rows. The more Includes you have the wider and taller the result set
        /// </summary>
        public static void EagerLoadEveryThing()
        {
            using var context = new PokemonContext();
            context.Configuration.LazyLoadingEnabled = false;

            var pokemon = context.Pokemon
                .Include(p => p.Trainer)
                .Include(p => p.PokeType)
                .Include(p => p.Moves.Select(m => m.DamageType))
                .Include(p => p.WinRecords)
                .ToList();

            foreach (var poke in pokemon)
            {
                PrintPokemonStats(poke);
            }
        }

        /// <summary>
        /// Same as above, but using AsNoTracking to skip the perf hit of setting up change tracking
        /// </summary>
        public static void EagerLoadEveryThingNoTracking()
        {
            using var context = new PokemonContext();
            context.Configuration.LazyLoadingEnabled = false;

            var pokemon = context.Pokemon
                .Include(p => p.Moves.Select(m => m.DamageType))
                .Include(p => p.PokeType)
                .Include(p => p.WinRecords)
                .Include(p => p.Trainer)
                .AsNoTracking()
                .ToList();

            foreach (var poke in pokemon)
            {
                PrintPokemonStats(poke);
            }
        }

        /// <summary>
        /// Try to optimize the queries by manually loading data in different batches
        /// </summary>
        public static void ManualLoading()
        {
            using var context = new PokemonContext();
            // important: if lazy loading is enabled, it will still hit the database for List<T> associations because
            // it doesn't know if you've loaded them all
            context.Configuration.LazyLoadingEnabled = false;

            // TODO: experiment with what fields do best eager loaded with Include vs loaded separately
            var pokemon = context.Pokemon
                .Include(t => t.Trainer)
                .Include(p => p.PokeType)
                .ToList();

            var pokeIds = pokemon.Select(p => p.Id).ToArray();

            // notice how we don't have to loop through all the moves and winrecords and associate them back to the pokemon,
            // EF does that for us automatically
            context.Moves.Where(m => pokeIds.Contains(m.Pokemon_Id)).Include(m => m.DamageType).Load();
            context.WinRecords.Where(m => pokeIds.Contains(m.Pokemon_Id)).Load();

            foreach (var poke in pokemon)
            {
                PrintPokemonStats(poke);
            }
        }

        /// <summary>
        /// If speed is your utmost concern you can alway use Dapper or raw ADO.NET. But there is a lot of plumbing
        /// to take care of. We can get down to 2 round-trips to the database.
        /// </summary>
        public static void Dapper()
        {
            using var context = new PokemonContext();

            var trainers = new Dictionary<int, Trainer>();

            // We're joining pokemon and trainers, but that means we'll get different Trainer instances for each Pokemon,
            // and we don't want that because we need to populate the trainers Pokemon list
            var pokemon = context.Database.Connection.Query<Pokemon, Trainer, Pokemon>(@"
                SELECT p.Id, p.Trainer_Id, p.Name, p.PokeType_Id, t.Id, t.Name 
                FROM [Pokemons] p JOIN [Trainers] t on p.Trainer_Id = t.Id"
                , (pokemon, trainer) =>
                {
                    // since we're going to add this pokemon to the trainers pokemon list
                    if (trainers.TryGetValue(trainer.Id, out var cachedTrainer))
                    {
                        pokemon.Trainer = cachedTrainer;
                    }
                    else
                    {
                        // first time we've seen this trainer
                        trainer.Pokemon = new List<Pokemon>();
                        pokemon.Trainer = trainer;
                        trainers[trainer.Id] = trainer;
                    }

                    // add this pokemon to the cached trainer's list
                    pokemon.Trainer.Pokemon.Add(pokemon);

                    return pokemon;
                }).AsList();

            var pokemonLookup = pokemon.ToDictionary(p => p.Id, p => p);
            var pokeIds = pokemon.Select(p => p.Id).ToArray();

            // we are going to send three queries in one db round trip:
            // 1. Get all poketypes, we're pretty sure we need all of them
            // 2. Get all the moves for all the pokemon we have
            // 3. GEt all the win records for all the pokemon we have
            using (var multi = context.Database.Connection.QueryMultiple(
                       @"SELECT * FROM [PokeTypes];
                    SELECT * FROM [Moves] WHERE Pokemon_Id IN @pokeIds;
                    SELECT * FROM [WinRecords] WHERE Pokemon_Id IN @pokeIds", new { pokeIds }))
            {
                var pokeTypes = multi.Read<PokeType>()
                    .ToDictionary(pt => pt.Id, pt => pt);

                var moves = multi.Read<Move>();

                var winRecords = multi.Read<WinRecord>();


                // sort the moves into the right pokemon
                foreach (var m in moves)
                {
                    var poke = pokemonLookup[m.Pokemon_Id];
                    if (poke.Moves == null) poke.Moves = new List<Move>();

                    m.DamageType = pokeTypes[m.DamageType_Id];
                    poke.Moves.Add(m);
                }

                // sort the wins into the right pokemon
                foreach (var w in winRecords)
                {
                    var poke = pokemonLookup[w.Pokemon_Id];
                    if (poke.WinRecords == null) poke.WinRecords = new List<WinRecord>();

                    poke.WinRecords.Add(w);
                }

                // now that we have all the poketypes we can populate the pokemon
                foreach (var p in pokemon)
                {
                    p.PokeType = pokeTypes[p.PokeType_Id];
                }
            }


            // whew!
            foreach (var poke in pokemon)
            {
                PrintPokemonStats(poke);
            }
        }

        /// <summary>
        /// A flag to avoid printing to the console while running timing tests
        /// </summary>
        private static bool _silent = false;

        /// <summary>
        /// Wrapper around Console.WriteLine that obeys the _silent flag
        /// </summary>
        /// <param name="message"></param>
        static void WriteLine(string message)
        {
            if (!_silent)
            {
                Console.WriteLine(message);
            }
        }


        /// <summary>
        /// Runs the method 100 times (configurable) and prints out the duration at the end
        /// </summary>
        /// <param name="action"></param>
        /// <param name="times"></param>
        public static void TimeIt(Action action, int times = 100)
        {
            var notify = times / 10;
            var restoreSilent = _silent;
            _silent = true;
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < times; i++)
            {
                action();

                if (i > 0 && i % notify == 0)
                {
                    Console.WriteLine($"{i}/{times}");
                }
            }

            sw.Stop();

            _silent = restoreSilent;
            var before = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(
                $"Executed {times} times in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds * 1.0 / times}ms each)");
            Console.ForegroundColor = before;
        }

        /// <summary>
        /// Simulates a workload that accesses all the properties of a pokemon. In a real app, this might be from JSON encoding
        /// an HTTP response, or by mapping the entities into response models
        /// </summary>
        public static void PrintPokemonStats(Pokemon p)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{0} owns {1} a {2} type pokemon has the following moves:", p.Trainer.Name, p.Name,
                p.PokeType.Name).AppendLine();

            foreach (var m in p.Moves)
            {
                sb.AppendFormat("  - {0} ({1})", m.Name, m.DamageType.Name).AppendLine();
            }

            var wins = p.WinRecords.Count(w => w.Win);

            sb.AppendFormat("and a win record of {0}/{1}", wins, p.WinRecords.Count).AppendLine().AppendLine();

            WriteLine(sb.ToString());
        }
    }
}