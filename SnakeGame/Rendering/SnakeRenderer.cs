using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SnakeGame
{
    public class SnakeRenderer : ISnakeRenderer
    {
        private const double RAD90 = Math.PI * 0.5;
        private const double RAD180 = Math.PI;

        private readonly Color _activeNeuronColor;
        private readonly Color _backgroundColor;
        private readonly Color _inactiveNeuronColor;
        private readonly Color _snakeAliveColor;
        private readonly Color _snakeDeadColor;
        private readonly List<Color> _snakeShades;

        private readonly SnakeEngine _snakeApp;
        private readonly SnakeStatusHUD _statusHUD;

        private long _frameCount;
        private long _lastFrameCount;
        private TimeSpan _lastStatsUpdate;
        private RenderStats _renderStats;

        private Rectangle _fieldViewport;
        private SpriteBatch _spriteBatch;

        private Texture2D _fieldTexture;
        private Texture2D _appleTexture;
        private Texture2D _arrowTexture;
        private Texture2D _smallSquareTexture;
        private Texture2D _snakeAliveTexture;
        private Texture2D _snakeDeadTexture;

        public SnakeRenderer(SnakeEngine snakeApp)
        {
            _activeNeuronColor = new Color(0xff7fff7f);
            _backgroundColor = new Color(0xff101010);
            _inactiveNeuronColor = new Color(0xff0000ff);
            _snakeAliveColor = new Color(0xff1c86ce);
            _snakeDeadColor = new Color(0xff1b1b99);
            _snakeShades = new List<Color>(10);

            _snakeApp = snakeApp;

            _statusHUD = new SnakeStatusHUD(snakeApp);
        }

        public void Initialize()
        {
            _fieldViewport = new Rectangle(0, 0, 0, 0);
            _spriteBatch = new SpriteBatch(_snakeApp.GraphicsDevice);

            _frameCount = 0;
            _lastFrameCount = 0;
            _lastStatsUpdate = TimeSpan.Zero;
            _renderStats = new RenderStats { FramesPerSecond = 0.0 };

            var graphics = _snakeApp.Services.GetService<ISnakeGraphics>();
            var rules = _snakeApp.Services.GetService<ISnakeGameRules>();

            _fieldTexture = CreateFieldTexture(graphics, rules.FieldWidth, rules.FieldHeight);
            _fieldViewport.Size = _fieldTexture.Bounds.Size;

            _appleTexture = graphics.AppleTexture;
            _arrowTexture = graphics.ArrowTexture;
            _smallSquareTexture = graphics.CreateFlatTexture(8, 8, Color.White);
            _snakeAliveTexture = graphics.CreateBorderSquare(16, 16, _snakeAliveColor, 2, Color.Black);
            _snakeDeadTexture = graphics.CreateBorderSquare(16, 16, _snakeDeadColor, 2, new Color(0xff111111));

            _statusHUD.Initialize(graphics);
        }

        public void Render(GameTime gameTime)
        {
            _snakeApp.GraphicsDevice.Clear(_backgroundColor);

            _spriteBatch.Begin(
                SpriteSortMode.Deferred,
                null,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                null);

            DrawGame();
            DrawHUD();
            DrawDebug();

            _spriteBatch.End();

            TimeSpan timeSinceUpdate = gameTime.TotalGameTime - _lastStatsUpdate;
            if (timeSinceUpdate.TotalSeconds >= 5.0)
                UpdateRenderStats(gameTime.TotalGameTime);
        }

        public void OnWindowResize(Rectangle clientBounds)
        {
            _fieldViewport.Location = new Point(
                (clientBounds.Size.X - _fieldViewport.Size.X) / 2,
                (clientBounds.Size.Y - _fieldViewport.Size.Y) / 2);
        }

        private Texture2D CreateFieldTexture(ISnakeGraphics graphics, int fieldWidth, int fieldHeight)
        {
            var tileDarkTexture = graphics.CreateBorderSquare(16, 16, new Color(0xff353535), 1, new Color(0xf1c1c1c));
            var tileLightTexture = graphics.CreateBorderSquare(16, 16, new Color(0xff444444), 1, new Color(0xf1c1c1c));

            Debug.Assert(tileDarkTexture.Height == tileLightTexture.Height);
            Debug.Assert(tileDarkTexture.Width == tileLightTexture.Width);

            // Render the field to a single texture because drawing 400 sprites every frame is slow
            using var renderTarget = new RenderTarget2D(
                tileDarkTexture.GraphicsDevice,
                fieldWidth * tileDarkTexture.Width,
                fieldHeight * tileDarkTexture.Height);
            var result = new Texture2D(
                tileDarkTexture.GraphicsDevice,
                fieldWidth * tileDarkTexture.Width,
                fieldHeight * tileDarkTexture.Height);

            _snakeApp.GraphicsDevice.SetRenderTarget(renderTarget);
            _snakeApp.GraphicsDevice.Clear(Color.Black);

            var offscreenSpriteBatch = new SpriteBatch(_snakeApp.GraphicsDevice);
            offscreenSpriteBatch.Begin();
            Point tilePosition = Point.Zero;

            for (tilePosition.Y = 0; tilePosition.Y < fieldHeight; ++tilePosition.Y)
            {
                for (tilePosition.X = 0; tilePosition.X < fieldWidth; ++tilePosition.X)
                {
                    Texture2D cellTexture = (tilePosition.X + tilePosition.Y) % 2 == 0
                        ? tileLightTexture
                        : tileDarkTexture;
                    offscreenSpriteBatch.Draw(
                        cellTexture,
                        (tilePosition * cellTexture.Bounds.Size).ToVector2(),
                        Color.White);
                }
            }
            offscreenSpriteBatch.End();

            _snakeApp.GraphicsDevice.SetRenderTarget(null);

            Color[] fieldData = new Color[renderTarget.Width * renderTarget.Height];
            renderTarget.GetData(fieldData);
            result.SetData(fieldData);

            return result;
        }

        private void DrawDebug()
        {
            DrawDebugOutputs();

            var brain = _snakeApp.ActivePlayer.CloneBrain();
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
            var aiDecision = _snakeApp.ActivePlayer.Decision;
            float up = aiDecision[0];
            float left = aiDecision[1];
            float right = aiDecision[2];

            DrawDebugArrow(topLeft, offset, GetDebugColor(up), (float)RAD90);
            DrawDebugArrow(topLeft + new Vector2(-16, 32), offset, GetDebugColor(left), 0.0f);
            DrawDebugArrow(topLeft + new Vector2(16, 32), offset, GetDebugColor(right), (float)RAD180);
        }

        private void DrawGame()
        {
            _spriteBatch.Draw(
                _fieldTexture,
                _fieldViewport.Location.ToVector2(), null,
                Color.White,
                0.0f, Vector2.Zero, 1.0f,
                SpriteEffects.None,
                0.0f);

            _spriteBatch.Draw(
                _appleTexture,
                ((_snakeApp.ActiveGame.ApplePosition * _appleTexture.Bounds.Size) + _fieldViewport.Location).ToVector2(),
                Color.White);

            if (_snakeApp.ActiveGame.Snake.Count != _snakeShades.Count)
            {
                _snakeShades.Clear();
                _snakeShades.Capacity = _snakeApp.ActiveGame.Snake.Count;
                for (int i = 0; i < _snakeApp.ActiveGame.Snake.Count; i++)
                {
                    float ratio = Math.Clamp(((float)i / _snakeApp.ActiveGame.Snake.Count * 0.5f) + 0.5f, 0.0f, 1.0f);
                    _snakeShades.Add(new Color(ratio, ratio, ratio, 1.0f));
                }
            }

            Texture2D snakeTexture = _snakeApp.ActiveGame.Alive ? _snakeAliveTexture : _snakeDeadTexture;

            int pieceCount = 0;
            foreach (Point snakePiece in _snakeApp.ActiveGame.Snake)
            {
                _spriteBatch.Draw(
                    snakeTexture,
                    (_fieldViewport.Location + (snakePiece * _snakeAliveTexture.Bounds.Size)).ToVector2(),
                    _snakeShades[pieceCount++]);
            }
        }

        private void DrawHUD()
        {
            _statusHUD.Draw(_spriteBatch, _snakeApp.ActiveGame, _renderStats);
        }

        private void DrawSmallSquare(Vector2 pos, float shade)
        {
            _spriteBatch.Draw(_smallSquareTexture, pos, GetDebugColor(shade));
        }

        private Color GetDebugColor(float shade)
        {
            shade = Math.Clamp(shade, -1.0f, 1.0f);

            return new Color(
                (shade >= 0 ? _activeNeuronColor : _inactiveNeuronColor) * Math.Abs(shade),
                1.0f);
        }

        private void UpdateRenderStats(TimeSpan gameTime)
        {
            long frameDiff = _frameCount - _lastFrameCount;
            TimeSpan timeDiff = gameTime - _lastStatsUpdate;

            _lastFrameCount = _frameCount;
            _lastStatsUpdate = gameTime;
            _renderStats = new RenderStats
            {
                FramesPerSecond = frameDiff / timeDiff.TotalSeconds
            };
        }
    }

    public struct RenderStats
    {
        public double FramesPerSecond;
    }
}
