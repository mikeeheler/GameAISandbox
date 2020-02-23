using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SnakeGame
{
    public class SnakeRenderer
    {
        private const double RAD90 = Math.PI * 0.5;
        private const double RAD180 = Math.PI;

        private readonly Color _activeNeuronColor;
        private readonly Color _backgroundColor;
        private readonly Color _inactiveNeuronColor;
        private readonly Color _snakeAliveColor;
        private readonly Color _snakeDeadColor;
        private readonly List<Color> _snakeShades;

        private Rectangle _gameViewport;
        private SpriteBatch _spriteBatch;

        private Texture2D _fieldTexture;
        private Texture2D _appleTexture;
        private Texture2D _arrowTexture;
        private Texture2D _smallSquareTexture;
        private Texture2D _snakeAliveTexture;
        private Texture2D _snakeDeadTexture;

        public SnakeRenderer(SnakeGame game)
        {
            _activeNeuronColor = new Color(0xff7fff7f);
            _backgroundColor = new Color(0xff101010);
            _inactiveNeuronColor = new Color(0xff0000ff);
            _snakeAliveColor = new Color(0xff1c86ce);
            _snakeDeadColor = new Color(0xff1b1b99);
            _snakeShades = new List<Color>(10);

            Game = game;
        }

        public SnakeGame Game { get; }
        public GraphicsDevice GraphicsDevice => Game.GraphicsDevice;

        public void Initialize()
        {
            _gameViewport = new Rectangle(0, 0, 0, 0);
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            var graphics = Game.Services.GetService<ISnakeGraphics>();
            _fieldTexture = CreateFieldTexture(graphics);
            _gameViewport.Size = _fieldTexture.Bounds.Size;

            _appleTexture = Game.Content.Load<Texture2D>("Textures/apple");
            _arrowTexture = Game.Content.Load<Texture2D>("Textures/arrow");
            _smallSquareTexture = graphics.CreateFlatTexture(8, 8, Color.White);
            _snakeAliveTexture = graphics.CreateBorderSquare(16, 16, _snakeAliveColor, 2, Color.Black);
            _snakeDeadTexture = graphics.CreateBorderSquare(16, 16, _snakeDeadColor, 2, new Color(0xff111111));

            OnWindowResize(Game.Window.ClientBounds);
        }

        public void RenderGame()
        {
            GraphicsDevice.Clear(_backgroundColor);

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
        }

        public void OnWindowResize(Rectangle clientBounds)
        {
            _gameViewport.Location = new Point(
                (clientBounds.Size.X - _gameViewport.Size.X) / 2,
                (clientBounds.Size.Y - _gameViewport.Size.Y) / 2);
        }

        private Texture2D CreateFieldTexture(ISnakeGraphics graphics)
        {
            var tileDarkTexture = graphics.CreateBorderSquare(16, 16, new Color(0xff353535), 1, new Color(0xf1c1c1c));
            var tileLightTexture = graphics.CreateBorderSquare(16, 16, new Color(0xff444444), 1, new Color(0xf1c1c1c));

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

            var offscreenSpriteBatch = new SpriteBatch(GraphicsDevice);
            offscreenSpriteBatch.Begin();
            Point tilePosition = Point.Zero;

            for (tilePosition.Y = 0; tilePosition.Y < SnakeRules.FIELD_HEIGHT; ++tilePosition.Y)
            {
                for (tilePosition.X = 0; tilePosition.X < SnakeRules.FIELD_WIDTH; ++tilePosition.X)
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

            GraphicsDevice.SetRenderTarget(null);

            Color[] fieldData = new Color[renderTarget.Width * renderTarget.Height];
            renderTarget.GetData(fieldData);
            result.SetData(fieldData);

            return result;
        }

        private void DrawDebug()
        {
            DrawDebugOutputs();

            var brain = Game.ActivePlayer.CloneBrain();
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
            var aiDecision = Game.ActivePlayer.Decision;
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
                _gameViewport.Location.ToVector2(), null,
                Color.White,
                0.0f, Vector2.Zero, 1.0f,
                SpriteEffects.None,
                0.0f);

            _spriteBatch.Draw(
                _appleTexture,
                ((Game.ActiveGame.ApplePosition * _appleTexture.Bounds.Size) + _gameViewport.Location).ToVector2(),
                Color.White);

            if (Game.ActiveGame.Snake.Count != _snakeShades.Count)
            {
                _snakeShades.Clear();
                _snakeShades.Capacity = Game.ActiveGame.Snake.Count;
                for (int i = 0; i < Game.ActiveGame.Snake.Count; i++)
                {
                    float ratio = Math.Clamp(((float)i / Game.ActiveGame.Snake.Count * 0.5f) + 0.5f, 0.0f, 1.0f);
                    _snakeShades.Add(new Color(ratio, ratio, ratio, 1.0f));
                }
            }

            Texture2D snakeTexture = Game.ActiveGame.Alive ? _snakeAliveTexture : _snakeDeadTexture;

            int pieceCount = 0;
            foreach (Point snakePiece in Game.ActiveGame.Snake)
            {
                _spriteBatch.Draw(
                    snakeTexture,
                    (_gameViewport.Location + (snakePiece * _snakeAliveTexture.Bounds.Size)).ToVector2(),
                    _snakeShades[pieceCount++]);
            }
        }

        private void DrawHUD()
        {
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
    }
}
