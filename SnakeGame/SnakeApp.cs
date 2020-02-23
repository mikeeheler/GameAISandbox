using System;
using System.Collections.Generic;
using System.Linq;

using MathNet.Numerics.Random;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SnakeGame
{
    public class SnakeApp : Game
    {
        private const int GAMES_PER_GENERATION = 100;
        private const double MUTATION_RATE = 0.40;
        private const int POPULATION_SIZE = 100;

        private readonly GraphicsDeviceManager _graphicsDeviceManager;
        private readonly RandomSource _rng;
        private readonly SnakeRenderer _renderer;

        private TimeSpan _lastTick;
        private bool _tickEnabled;
        private double _tickRate = 0.125;

        private IPlayerController _player;

        private List<AIPlayerScore> _aiPlayers;

        private bool _tabDown;
        private bool _plusDown;
        private bool _minusDown;

        public SnakeApp()
        {
            ActiveGame = new SnakeGameSim();
            _graphicsDeviceManager = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferHeight = 720,
                PreferredBackBufferWidth = 1280,
                PreferHalfPixelOffset = true,
                PreferMultiSampling = true,
                SynchronizeWithVerticalRetrace = true
            };
            _rng = SnakeRandom.Default;
            _renderer = new SnakeRenderer(this);
            _aiPlayers = GenerateAIPlayers(POPULATION_SIZE).ToList();
            AIPlayerIndex = 0;
            GamesPlayed = 0;
            Generation = 0;
            AllTimeBestScore = 0;
            AllTimeBestSpecies = 0;
            AllTimeBestUnit = 0;

            _tickEnabled = true;
            _tabDown = false;
            _minusDown = false;
            _plusDown = false;

            IsFixedTimeStep = false;
            IsMouseVisible = true;

            Content.RootDirectory = "Content";
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnResize;
            Window.Title = "Snake Game AI Sandbox";
        }

        public SnakeGameSim ActiveGame { get; }
        public AIPlayerController ActivePlayer => _aiPlayers[AIPlayerIndex].Player;
        public int ActivePlayerScore => _aiPlayers[AIPlayerIndex].Score;
        public int AIPlayerIndex { get; private set; }
        public int AllTimeBestScore { get; private set; }
        public int AllTimeBestSpecies { get; private set; }
        public int AllTimeBestUnit { get; private set; }
        public int GamesPlayed { get; private set; }
        public int Generation { get; private set; }
        public SpriteBatch SpriteBatch { get; private set; }
        public int ThisGenBestScore { get; private set; }
        public int ThisGenBestSpecies { get; private set; }
        public int ThisGenBestUnit { get; private set; }

        protected override void Draw(GameTime gameTime)
        {
            _renderer.RenderGame(gameTime);
            base.Draw(gameTime);
        }

        protected override void Initialize()
        {
            Services.AddService(new SnakeGraphics(this) as ISnakeGraphics);

            _renderer.Initialize();

            _lastTick = TimeSpan.Zero;
            Reset();

            base.Initialize();
        }

        protected override void Update(GameTime gameTime)
        {
            HandleKeyPress(Keyboard.GetState());

            TimeSpan diff = gameTime.TotalGameTime - _lastTick;
            if (!_tickEnabled || diff.TotalSeconds >= _tickRate)
            {
                UpdateEntities(diff);

                if (!_player.IsHuman
                    && (!ActiveGame.Alive || ActiveGame.TotalTurns == SnakeRules.MAX_AI_TURNS))
                {
                    HandleAI();
                    Reset();
                }

                _lastTick = gameTime.TotalGameTime;
            }

            base.Update(gameTime);
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            _player.Shutdown();

            base.OnExiting(sender, args);
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
                    if (_rng.NextDouble() < deathChance)
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
                    double roll = _rng.NextDouble() * oddsSum;
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
                _aiPlayers = _aiPlayers.OrderBy(_ => _rng.Next()).ToList();
                foreach (var aiPlayer in _aiPlayers)
                    aiPlayer.Score = 0;
            }
        }

        private void HandleKeyPress(KeyboardState keyboardState)
        {
            if (keyboardState.IsKeyDown(Keys.Escape) || keyboardState.IsKeyDown(Keys.Q))
            {
                Exit();
                return;
            }

            if (!ActiveGame.Alive && _player.IsHuman && keyboardState.IsKeyDown(Keys.Space))
            {
                Reset();
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Tab))
                _tabDown = true;
            if (keyboardState.IsKeyDown(Keys.Subtract))
                _minusDown = true;
            if (keyboardState.IsKeyDown(Keys.Add))
                _plusDown = true;

            if (_tabDown && keyboardState.IsKeyUp(Keys.Tab))
            {
                _graphicsDeviceManager.SynchronizeWithVerticalRetrace = !_graphicsDeviceManager.SynchronizeWithVerticalRetrace;
                _graphicsDeviceManager.ApplyChanges();
                _tickEnabled = _graphicsDeviceManager.SynchronizeWithVerticalRetrace;
                _tabDown = false;
            }

            if (_minusDown && keyboardState.IsKeyUp(Keys.Subtract))
            {
                _tickRate *= 2.0;
                _minusDown = false;
            }

            if (_plusDown && keyboardState.IsKeyUp(Keys.Add))
            {
                _tickRate *= 0.5;
                _plusDown = false;
            }
        }

        private void OnResize(object sender, EventArgs e)
        {
            _renderer.OnWindowResize(Window.ClientBounds);
        }

        private void Reset()
        {
            ActiveGame.Reset();
            _player?.Shutdown();
            _player = _aiPlayers[AIPlayerIndex].Player;
            _player.Initialize();
        }

        private void UpdateEntities(TimeSpan _)
        {
            ActiveGame.Move(_player.GetMovement(ActiveGame));
        }

        private class AIPlayerScore
        {
            public AIPlayerController Player { get; set; }
            public int Score { get; set; }
        }
    }
}
