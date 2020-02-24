using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

using MathNet.Numerics.Random;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SnakeGame
{
    public class SnakeEngine : Game
    {
        private const int GAMES_PER_GENERATION = 100;
        private const double MUTATION_RATE = 0.40;

        private readonly GraphicsDeviceManager _graphicsDeviceManager;
        private readonly Thread _keyPollThread;
        private readonly object _keySync;
        private readonly ConcurrentQueue<Action> _mainThreadActions;
        private readonly HashSet<Keys> _pressedKeys;
        private readonly RandomSource _random;

        private ISnakeRenderer _renderer;
        private ISnakeGameRules _rules;

        private TimeSpan _lastTick;
        private bool _pollKeysEnabled;
        private bool _tickEnabled;
        private double _tickRate;

        private IPlayerController _player;
        private List<AIPlayerScore> _aiPlayers;

        public SnakeEngine()
        {
            _graphicsDeviceManager = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferHeight = 720,
                PreferredBackBufferWidth = 1280,
                PreferHalfPixelOffset = true,
                PreferMultiSampling = true,
                SynchronizeWithVerticalRetrace = true
            };
            _mainThreadActions = new ConcurrentQueue<Action>();
            _random = SnakeRandom.Default;
            PopulationSize = 100;

            _keyPollThread = new Thread(new ThreadStart(PollKeys)) { IsBackground = true };
            _keySync = new object();
            _pollKeysEnabled = true;
            _pressedKeys = new HashSet<Keys>(1024);

            // _aiPlayers = GenerateAIPlayers(POPULATION_SIZE).ToList();
            _aiPlayers = LoadAIFromJson(Path.Combine("Data", "test.json"));
            AIPlayerIndex = 0;
            GamesPlayed = 0;
            Generation = 0;

            AllTimeBestScore = 0;
            AllTimeBestSpecies = "";
            AllTimeBestUnit = 0;

            _tickEnabled = true;
            _tickRate = 0.125;

            IsFixedTimeStep = false;
            IsMouseVisible = true;

            Content.RootDirectory = "Content";
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnResize;
            Window.Title = "Snake Game AI Sandbox";

            KeyDown += HandleKeyDown;
            KeyUp += HandleKeyUp;
        }

        public SnakeGameSim ActiveGame { get; private set; }
        public AIPlayer ActivePlayer => _aiPlayers[AIPlayerIndex].Player;
        public int ActivePlayerScore => _aiPlayers[AIPlayerIndex].Score;
        public int AIPlayerIndex { get; private set; }
        public int AllTimeBestScore { get; private set; }
        public string AllTimeBestSpecies { get; private set; }
        public int AllTimeBestUnit { get; private set; }
        public int GamesPlayed { get; private set; }
        public int Generation { get; private set; }
        public int PopulationSize { get; private set; }
        public int ThisGenBestScore { get; private set; }
        public string ThisGenBestSpecies { get; private set; }
        public int ThisGenBestUnit { get; private set; }

        public event EventHandler<KeyDownEventArgs> KeyDown;
        public event EventHandler<KeyUpEventArgs> KeyUp;

        public SnakeGameSim CreateGame()
            => new SnakeGameSim(_rules);

        protected override void Draw(GameTime gameTime)
        {
            _renderer.Render(gameTime);
            base.Draw(gameTime);
        }

        protected override void Initialize()
        {
            base.Initialize();

            _lastTick = TimeSpan.Zero;
            _rules = new SnakeGameRules
            {
                FieldHeight = 21,
                FieldWidth = 21,
                MaxAITurns = 21 * 21,
                SnakeGrowLength = 5,
                SnakeStartLength = 10
            };

            Services.AddService(_rules);
            Services.AddService(new SnakeGraphics(this) as ISnakeGraphics);
            Services.AddService(_renderer = new SnakeRenderer(this) as ISnakeRenderer);

            ActiveGame = new SnakeGameSim(_rules);

            _renderer.Initialize();
            Reset();
            OnResize(this, EventArgs.Empty);

            _keyPollThread.Start();
        }

        protected override void OnExiting(object sender, EventArgs e)
        {
            // Not strictly necessary since _keyPollThread is a background thread, but it is good practice to clean up
            _pollKeysEnabled = false;
            _keyPollThread.Join();
            base.OnExiting(sender, e);
        }

        protected override void Update(GameTime gameTime)
        {
            /*
            Stopwatch timer = Stopwatch.StartNew();
            var populations = Enumerable.Range(0, Environment.ProcessorCount)
                .Select(index =>
                {
                    var population = new SnakeAIPopulation(100, 750);
                    void RunSim()
                    {
                        int populationIndex = index;
                        Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId} Population {populationIndex} running...");
                        population.Initialize();
                        population.Run(this);
                        Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId} Population {populationIndex} done.");
                    }
                    var thread = new Thread(new ThreadStart(RunSim)) { IsBackground = true };
                    return (thread, population);
                })
                .ToArray();
            foreach (var (thread, population) in populations)
            {
                thread.Start();
            }
            foreach (var (thread, population) in populations)
            {
                thread.Join();
            }
            Console.WriteLine("Completed in " + timer.Elapsed);

            var dataInfo = new DirectoryInfo("Data");
            if (!dataInfo.Exists) { dataInfo.Create(); dataInfo.Refresh(); }
            for (int popIndex = 0; popIndex < populations.Length; popIndex++)
            {
                var fileInfo = new FileInfo(Path.Combine(dataInfo.FullName, $"pop{popIndex}.json"));
                using var writer = fileInfo.CreateText();
                writer.WriteLine("[");
                var players = populations[popIndex].population.GetPlayers()
                    .OrderByDescending(x => x.Score)
                    .ToArray();
                for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
                {
                    var player = players[playerIndex].Player;
                    var playerInfo = new FileInfo(Path.Combine(dataInfo.FullName, $"pop{popIndex}", $"player{playerIndex}.bin"));
                    if (!playerInfo.Directory.Exists)
                    {
                        playerInfo.Directory.Create();
                        playerInfo.Directory.Refresh();
                    }
                    writer.WriteLine("  {");
                    writer.WriteLine($"    \"DataFile\": \"{playerInfo.FullName.Replace("\\", "\\\\")}\",");
                    writer.WriteLine($"    \"PlayerId\": \"{player.Id}\",");
                    writer.WriteLine($"    \"PlayerName\": \"{player.Name}\",");
                    writer.WriteLine($"    \"Score\": \"{players[playerIndex].Score}\",");
                    writer.WriteLine($"    \"Species\": \"{player.SpeciesName}\"");
                    writer.WriteLine(playerIndex == players.Length - 1 ? "  }" : "  },");

                    player.SerializeTo(playerInfo.Create());
                }
                writer.WriteLine("]");
            }
            */

            while (_mainThreadActions.TryDequeue(out Action action))
                action.Invoke();

            TimeSpan diff = gameTime.TotalGameTime - _lastTick;
            if (!_tickEnabled || diff.TotalSeconds >= _tickRate)
            {
                UpdateEntities(diff);

                if (!_player.IsHuman
                    && (!ActiveGame.Alive || ActiveGame.TotalTurns == _rules.MaxAITurns))
                {
                    HandleAI();
                    Reset();
                }

                _lastTick = gameTime.TotalGameTime;
            }

            base.Update(gameTime);
        }

        private IEnumerable<AIPlayerScore> GenerateAIPlayers(int size)
        {
            return Enumerable.Range(0, size).Select(_ =>
            {
                var player = new AIPlayer();
                return new AIPlayerScore { Player = player, Score = 0 };
            });
        }

        private void HandleAI()
        {
            var activePlayer = _aiPlayers[AIPlayerIndex];

            activePlayer.Score += ActiveGame.ApplesEaten;
            GamesPlayed++;

            if (activePlayer.Score > ThisGenBestScore)
            {
                ThisGenBestScore = activePlayer.Score;
                ThisGenBestSpecies = activePlayer.Player.SpeciesName;
                ThisGenBestUnit = (int)activePlayer.Player.Id;
            }

            if (ThisGenBestScore > AllTimeBestScore)
            {
                AllTimeBestScore = ThisGenBestScore;
                AllTimeBestSpecies = ThisGenBestSpecies;
                AllTimeBestUnit = ThisGenBestUnit;
            }

            if (GamesPlayed < GAMES_PER_GENERATION)
                return;

            AIPlayerIndex++;
            GamesPlayed = 0;

            if (AIPlayerIndex == PopulationSize)
            {
                AIPlayerIndex = 0;
                Generation++;

                ThisGenBestScore = 0;
                ThisGenBestSpecies = "";
                ThisGenBestUnit = 0;

                _aiPlayers = _aiPlayers.OrderByDescending(p => p.Score).Select(p => { p.Score = 0; return p; }).ToList();

                /*
                var sorted = _aiPlayers.OrderByDescending(x => x.Score).ToList();

                var genPath = new DirectoryInfo(Path.Combine("Data", "gen" + Generation));
                if (!genPath.Exists)
                {
                    genPath.Create();
                    genPath.Refresh();
                }

                for (int i = 0; i < sorted.Count; ++i)
                {
                    var aiFile = new FileInfo(Path.Combine(genPath.FullName, "ai" + i + ".bin"));
                    using var outputStream = File.Create(aiFile.FullName);
                    sorted[i].Player.SerializeTo(outputStream);
                }

                _aiPlayers = new List<AIPlayerScore>() { Capacity = PopulationSize };
                var parents = new List<AIPlayerScore>(sorted.Count);
                long oddsSum = 0;
                for (int i = 0; i < sorted.Count; ++i)
                {
                    // This should prune about half, with increasing likelihood of dying towards the bottom
                    // of the pile
                    double deathChance = (double)i / sorted.Count;
                    if (_random.NextDouble() < deathChance)
                        continue;

                    _aiPlayers.Add(sorted[i]);
                    parents.Add(sorted[i]);
                    oddsSum += sorted[i].Score;
                }

                // Now select parents to breed from. The top entities have the best chance of breeding.
                while (_aiPlayers.Count < PopulationSize)
                {
                    int index = 0;
                    double sum = 0.0;
                    double roll = _random.NextDouble() * oddsSum;
                    while (sum < roll && index <= parents.Count)
                        sum += parents[index++].Score;
                    var child = new AIPlayerScore
                    {
                        Player = parents[--index].Player.Clone(),
                        Score = 0
                    };
                    child.Player.Mutate(MUTATION_RATE);
                    _aiPlayers.Add(child);
                }

                // Shuffle the deck
                _aiPlayers = _aiPlayers.OrderBy(_ => _random.Next()).ToList();
                foreach (var aiPlayer in _aiPlayers)
                    aiPlayer.Score = 0;
                */
            }
        }

        private void HandleKeyDown(object sender, KeyDownEventArgs e)
        {
            if (e.Key == Keys.Escape || e.Key == Keys.Q)
            {
                _mainThreadActions.Enqueue(Exit);
            }
            else if (!ActiveGame.Alive && _player.IsHuman && e.Key == Keys.Space)
            {
                _mainThreadActions.Enqueue(Reset);
            }
            else if (e.Key == Keys.Tab)
            {
                _mainThreadActions.Enqueue(() =>
                {
                    _graphicsDeviceManager.SynchronizeWithVerticalRetrace = !_graphicsDeviceManager.SynchronizeWithVerticalRetrace;
                    _graphicsDeviceManager.ApplyChanges();
                    _tickEnabled = _graphicsDeviceManager.SynchronizeWithVerticalRetrace;
                });
            }
            else if (e.Key == Keys.Subtract)
            {
                _mainThreadActions.Enqueue(() => _tickRate *= 2.0);
            }
            else if (e.Key == Keys.Add)
            {
                _mainThreadActions.Enqueue(() => _tickRate *= 0.5);
            }
        }

        private void HandleKeyUp(object sender, KeyUpEventArgs e)
        {
        }

        private void HandleKeyboardState(KeyboardState keyboardState)
        {
            lock (_keySync)
            {
                if (_pressedKeys.Count != 0)
                {
                    var currentKeys = new HashSet<Keys>(_pressedKeys);
                    currentKeys.ExceptWith(new HashSet<Keys>(keyboardState.GetPressedKeys()));
                    foreach (Keys unpressedKey in currentKeys)
                    {
                        _pressedKeys.Remove(unpressedKey);
                        KeyUp?.Invoke(this, new KeyUpEventArgs(unpressedKey));
                    }
                }

                if (keyboardState.GetPressedKeyCount() > 0)
                {
                    var pressedKeys = new HashSet<Keys>(keyboardState.GetPressedKeys());
                    pressedKeys.ExceptWith(_pressedKeys);
                    foreach (Keys pressedKey in pressedKeys)
                    {
                        _pressedKeys.Add(pressedKey);
                        KeyDown?.Invoke(this, new KeyDownEventArgs(pressedKey));
                    }
                }
            }
        }

        private List<AIPlayerScore> LoadAIFromJson(string filePath)
        {
            var result = new List<AIPlayerScore>(1000);
            var foundingParents = new List<AIPlayer>(10);

            var testDocument = JsonDocument.Parse(File.ReadAllBytes(filePath));
            foreach (var element in testDocument.RootElement.EnumerateArray())
            {
                string dataFilePath = element.GetProperty("DataFile").GetString();
                string name = element.GetProperty("PlayerName").GetString();
                string speciesName = element.GetProperty("Species").GetString();
                var parent = new AIPlayerScore
                {
                    Player = AIPlayer.Deserialize(File.OpenRead(dataFilePath)),
                    Score = 0
                };

                foundingParents.Add(parent.Player);

                result.Add(parent);
                result.AddRange(Enumerable.Range(0, 9).Select(_ =>
                {
                    AIPlayer child = parent.Player.Clone();
                    child.Mutate(MUTATION_RATE);
                    return new AIPlayerScore
                    {
                        Player = child,
                        Score = 0
                    };
                }));
            }

            foreach (var firstParent in foundingParents)
            {
                foreach (var secondParent in foundingParents.Where(p => p != firstParent))
                {
                    result.AddRange(Enumerable.Range(0, 5)
                        .Select(_ => new AIPlayerScore
                        {
                            Player = firstParent.BreedWith(secondParent, AIBreedingMode.Mix),
                            Score = 0
                        }));
                }
            }

            result.AddRange(GenerateAIPlayers(20));
            PopulationSize = result.Count;

            result.TrimExcess();
            return result;
        }

        private void OnResize(object sender, EventArgs e)
        {
            _renderer.OnWindowResize(Window.ClientBounds);
        }

        private void PollKeys()
        {
            while (_pollKeysEnabled)
            {
                HandleKeyboardState(Keyboard.GetState());
                Thread.Sleep(10);
            }
        }

        private void Reset()
        {
            ActiveGame.Reset();

            _player = _aiPlayers[AIPlayerIndex].Player;
            _player.Initialize();
        }

        private void UpdateEntities(TimeSpan _)
        {
            PlayerMovement move = _player.GetMovement(ActiveGame);
            if (ActiveGame.IsLegalMove(move))
                ActiveGame.Move(move);
        }

        private class AIPlayerScore
        {
            public AIPlayer Player { get; set; }
            public int Score { get; set; }
        }
    }
}
