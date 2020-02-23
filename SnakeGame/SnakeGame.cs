using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using MathNet.Numerics.Random;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SnakeGame
{
    public class SnakeGame : Game
    {
        private const int GAMES_PER_GENERATION = 100;
        private const double MUTATION_RATE = 0.40;
        private const int POPULATION_SIZE = 100;

        private const double RAD90 = Math.PI * 0.5;
        private const double RAD180 = Math.PI;

        private readonly GraphicsDeviceManager _graphicsDeviceManager;
        private readonly RandomSource _rng;

        private readonly Color _activeNeuronColor;
        private readonly Color _backgroundColor;
        private readonly Color _inactiveNeuronColor;
        private readonly Color _snakeAliveColor;
        private readonly Color _snakeDeadColor;
        private readonly List<Color> _snakeShades;

        private SpriteBatch _spriteBatch;

        private Texture2D _appleTexture;
        private Texture2D _arrowTexture;
        private Texture2D _smallSquareTexture;
        private Texture2D _snakeAliveTexture;
        private Texture2D _snakeDeadTexture;
        private Texture2D _fieldTexture;

        private TimeSpan _lastTick;
        private bool _tickEnabled;
        private double _tickRate = 0.125;

        private Vector2 _fieldTopLeft;
        private IPlayerController _player;

        private List<AIPlayerScore> _aiPlayers;

        private bool _tabDown;
        private bool _plusDown;
        private bool _minusDown;

        public SnakeGame()
        {
            ActiveGame = new SnakeGameState();
            _graphicsDeviceManager = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferHeight = 720,
                PreferredBackBufferWidth = 1280,
                PreferHalfPixelOffset = true,
                PreferMultiSampling = true,
                SynchronizeWithVerticalRetrace = true
            };
            _rng = SnakeRandom.Default;
            _aiPlayers = GenerateAIPlayers(POPULATION_SIZE).ToList();
            AIPlayerIndex = 0;
            GamesPlayed = 0;
            Generation = 0;
            AllTimeBestScore = 0;
            AllTimeBestSpecies = 0;
            AllTimeBestUnit = 0;
            _fieldTopLeft = Vector2.Zero;

            _activeNeuronColor = new Color(0xff7fff7f);
            _backgroundColor = new Color(0xff101010);
            _inactiveNeuronColor = new Color(0xff0000ff);
            _snakeAliveColor = new Color(0xff1c86ce);
            _snakeDeadColor = new Color(0xff1b1b99);
            _snakeShades = new List<Color>(10);

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

        public SnakeGameState ActiveGame { get; }
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

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(_backgroundColor);

            _spriteBatch.Begin(
                SpriteSortMode.Deferred,
                null,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone);

            DrawField();
            DrawEntities();
            DrawDebug();

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        protected override void Initialize()
        {
            Components.Add(new SnakeStatusHUD(this));

            _lastTick = TimeSpan.Zero;
            Reset();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _appleTexture = Content.Load<Texture2D>("Textures/apple");
            _arrowTexture = Content.Load<Texture2D>("Textures/arrow");
            _smallSquareTexture = CreateFlatTexture(8, 8, Color.White);
            _snakeAliveTexture = CreateBorderSquare(16, 16, _snakeAliveColor, 2, Color.Black);
            _snakeDeadTexture = CreateBorderSquare(16, 16, _snakeDeadColor, 2, new Color(0xff111111));
            _fieldTexture = CreateFieldTexture();

            OnResize(null, EventArgs.Empty);

            base.LoadContent();
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

        private Texture2D CreateBorderSquare(int width, int height, Color color, int thickness, Color border)
        {
            Color[] image = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < thickness || y < thickness || x >= (width - thickness) || y >= (height - thickness))
                        image[(y * width) + x] = border;
                    else
                        image[(y * width) + x] = color;
                }
            }
            var result = new Texture2D(GraphicsDevice, width, height);
            result.SetData(image);
            return result;
        }

        private Texture2D CreateFieldTexture()
        {
            var tileDarkTexture = CreateBorderSquare(16, 16, new Color(0xff353535), 1, new Color(0xf1c1c1c));
            var tileLightTexture = CreateBorderSquare(16, 16, new Color(0xff444444), 1, new Color(0xf1c1c1c));

            Debug.Assert(tileDarkTexture.Height == tileLightTexture.Height);
            Debug.Assert(tileDarkTexture.Width == tileLightTexture.Width);

            // Render the field to a single texture because drawing 400 sprites every frame is slow
            using var renderTarget = new RenderTarget2D(
                tileDarkTexture.GraphicsDevice,
                SnakeRules.FIELD_WIDTH * tileDarkTexture.Width,
                SnakeRules.FIELD_HEIGHT * tileDarkTexture.Height);
            var result = new Texture2D(
                tileDarkTexture.GraphicsDevice,
                SnakeRules.FIELD_WIDTH * tileDarkTexture.Width,
                SnakeRules.FIELD_HEIGHT * tileDarkTexture.Height);

            GraphicsDevice.SetRenderTarget(renderTarget);
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin();
            Point tilePosition = Point.Zero;

            for (tilePosition.Y = 0; tilePosition.Y < SnakeRules.FIELD_HEIGHT; ++tilePosition.Y)
            {
                for (tilePosition.X = 0; tilePosition.X < SnakeRules.FIELD_WIDTH; ++tilePosition.X)
                {
                    Texture2D cellTexture = (tilePosition.X + tilePosition.Y) % 2 == 0
                        ? tileLightTexture
                        : tileDarkTexture;
                    _spriteBatch.Draw(
                        cellTexture,
                        (tilePosition * cellTexture.Bounds.Size).ToVector2(),
                        Color.White);
                }
            }
            _spriteBatch.End();

            GraphicsDevice.SetRenderTarget(null);

            Color[] fieldData = new Color[renderTarget.Width * renderTarget.Height];
            renderTarget.GetData(fieldData);
            result.SetData(fieldData);

            return result;
        }

        private Texture2D CreateFlatTexture(int width, int height, Color color)
        {
            var result = new Texture2D(GraphicsDevice, width, height);
            result.SetData(Enumerable.Repeat(color, height*width).ToArray());
            return result;
        }

        private void DrawSmallSquare(Vector2 pos, float shade)
        {
            _spriteBatch.Draw(_smallSquareTexture, pos, GetDebugColor(shade));
        }

        private void DrawDebug()
        {
            DrawDebugOutputs();

            var brain = _aiPlayers[AIPlayerIndex].Player.CloneBrain();
            var values = brain.GetValues();

            Vector2 topLeft = new Vector2(336, 310);
            Vector2 offset = new Vector2(16, 0);
            for (int layerIndex = 0; layerIndex < values.Length; ++layerIndex)
            {
                if (values[layerIndex] == null)
                    continue;

                double[] layer = values[layerIndex].Enumerate().ToArray();
                Vector2 pos = new Vector2(layerIndex * offset.X, offset.Y);
                for (int y = 0; y < layer.Length; ++y)
                {
                    pos.Y = y * 8;
                    DrawSmallSquare(topLeft + pos, (float)layer[y]);
                }
            }
        }

        private void DrawDebugArrow(Vector2 position, Vector2 offset, Color color, float rotation)
        {
            _spriteBatch.Draw(
                _arrowTexture,
                position + offset,
                null,
                color,
                rotation,
                offset,
                1.0f,
                SpriteEffects.None,
                0.0f);
        }

        private void DrawDebugOutputs()
        {
            Vector2 topLeft = new Vector2(400, 350);
            Vector2 offset = new Vector2(16, 16);
            var aiDecision = _aiPlayers[AIPlayerIndex].Player.Decision;
            float up = aiDecision[0];
            float left = aiDecision[1];
            float right = aiDecision[2];

            DrawDebugArrow(topLeft, offset, GetDebugColor(up), (float)RAD90);
            DrawDebugArrow(topLeft + new Vector2(-16, 32), offset, GetDebugColor(left), 0.0f);
            DrawDebugArrow(topLeft + new Vector2(16, 32), offset, GetDebugColor(right), (float)RAD180);
        }

        private void DrawField()
        {
            _spriteBatch.Draw(_fieldTexture, _fieldTopLeft, null, Color.White, 0.0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0.0f);
        }

        private void DrawEntities()
        {
            _spriteBatch.Draw(
                _appleTexture,
                _fieldTopLeft + (ActiveGame.ApplePosition * _appleTexture.Bounds.Size).ToVector2(),
                Color.White);

            if (ActiveGame.Snake.Count != _snakeShades.Count)
            {
                _snakeShades.Clear();
                _snakeShades.Capacity = ActiveGame.Snake.Count;
                for (int i = 0; i < ActiveGame.Snake.Count; i++)
                {
                    float ratio = Math.Clamp(((float)i / ActiveGame.Snake.Count * 0.5f) + 0.5f, 0.0f, 1.0f);
                    _snakeShades.Add(new Color(ratio, ratio, ratio, 1.0f));
                }
            }

            Texture2D snakeTexture = ActiveGame.Alive ? _snakeAliveTexture : _snakeDeadTexture;

            int pieceCount = 0;
            foreach (Point snakePiece in ActiveGame.Snake)
            {
                _spriteBatch.Draw(
                    snakeTexture,
                    (snakePiece * _snakeAliveTexture.Bounds.Size).ToVector2() + _fieldTopLeft,
                    _snakeShades[pieceCount++]);
            }
        }

        private IEnumerable<AIPlayerScore> GenerateAIPlayers(int size)
        {
            return Enumerable.Range(0, size).Select(_ =>
            {
                var player = new AIPlayerController();
                return new AIPlayerScore { Player = player, Score = 0 };
            });
        }

        private Color GetDebugColor(float shade)
        {
            shade = Math.Clamp(shade, -1.0f, 1.0f);

            Color shadeColor = new Color(
                (shade >= 0 ? _activeNeuronColor : _inactiveNeuronColor) * Math.Abs(shade),
                1.0f);
            return shadeColor;
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
            _fieldTopLeft.X = (Window.ClientBounds.Width - _fieldTexture.Width) / 2;
            _fieldTopLeft.Y = (Window.ClientBounds.Height - _fieldTexture.Height) / 2;
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
