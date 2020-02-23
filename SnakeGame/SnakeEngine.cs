using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        private const int POPULATION_SIZE = 100;

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
        private double _tickRate = 0.125;

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

            _keyPollThread = new Thread(new ThreadStart(PollKeys)) { IsBackground = true };
            _keySync = new object();
            _pollKeysEnabled = true;
            _pressedKeys = new HashSet<Keys>(1024);

            _aiPlayers = GenerateAIPlayers(POPULATION_SIZE).ToList();
            AIPlayerIndex = 0;
            GamesPlayed = 0;
            Generation = 0;
            AllTimeBestScore = 0;
            AllTimeBestSpecies = 0;
            AllTimeBestUnit = 0;

            _tickEnabled = true;

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
        public AIPlayerController ActivePlayer => _aiPlayers[AIPlayerIndex].Player;
        public int ActivePlayerScore => _aiPlayers[AIPlayerIndex].Score;
        public int AIPlayerIndex { get; private set; }
        public int AllTimeBestScore { get; private set; }
        public int AllTimeBestSpecies { get; private set; }
        public int AllTimeBestUnit { get; private set; }
        public int GamesPlayed { get; private set; }
        public int Generation { get; private set; }
        public int ThisGenBestScore { get; private set; }
        public int ThisGenBestSpecies { get; private set; }
        public int ThisGenBestUnit { get; private set; }

        public event EventHandler<KeyDownEventArgs> KeyDown;
        public event EventHandler<KeyUpEventArgs> KeyUp;

        protected override void Draw(GameTime gameTime)
        {
            _renderer.Render(gameTime);
            base.Draw(gameTime);
        }

        protected override void Initialize()
        {
            base.Initialize();

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

            _lastTick = TimeSpan.Zero;
            Reset();

            OnResize(this, EventArgs.Empty);

            _keyPollThread.Start();
        }

        protected override void OnExiting(object sender, EventArgs e)
        {
            _pollKeysEnabled = false;
            _keyPollThread.Join();
            base.OnExiting(sender, e);
        }

        protected override void Update(GameTime gameTime)
        {
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
                var player = new AIPlayerController();
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
                ThisGenBestSpecies = (int)activePlayer.Player.SpeciesId;
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

            if (AIPlayerIndex == POPULATION_SIZE)
            {
                AIPlayerIndex = 0;
                Generation++;
                ThisGenBestScore = 0;
                ThisGenBestSpecies = 0;
                ThisGenBestUnit = 0;

                var sorted = _aiPlayers.OrderByDescending(x => x.Score).ToList();
                _aiPlayers = new List<AIPlayerScore>() { Capacity = POPULATION_SIZE };
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
                while (_aiPlayers.Count < POPULATION_SIZE)
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
            }
        }

        private void HandleKeyDown(object sender, KeyDownEventArgs e)
        {
            if (e.Key == Keys.Escape || e.Key == Keys.Q)
            {
                Exit();
            }
            else if (!ActiveGame.Alive && _player.IsHuman && e.Key == Keys.Space)
            {
                Reset();
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
                _tickRate *= 2.0;
            }
            else if (e.Key == Keys.Add)
            {
                _tickRate *= 0.5;
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
            public AIPlayerController Player { get; set; }
            public int Score { get; set; }
        }
    }
}
