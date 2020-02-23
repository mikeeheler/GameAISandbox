using System;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SnakeGame
{
    public class SnakeStatusHUD : DrawableGameComponent
    {
        private const string GAME_OVER_MESSAGE = "GAME OVER";
        private const string QUIT_MESSAGE = "Q to quit";
        private const string TRY_AGAIN_MESSAGE  = "SPACE to try again";

        private readonly SnakeGame _snakeGame;

        private SpriteBatch _spriteBatch;
        private SpriteFont _mainFont;
        private SpriteFont _smallFont;

        private Vector2 _gameOverMessagePos;
        private Vector2 _quitMessagePos;
        private Vector2 _tryAgainMessagePos;

        public SnakeStatusHUD(SnakeGame game) : base(game)
        {
            _snakeGame = game;
            game.Window.ClientSizeChanged += OnResize;
        }

        public override void Initialize()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            DrawOrder = 1000;

            base.Initialize();
        }

        public override void Draw(GameTime gameTime)
        {
            _spriteBatch.Begin(
                SpriteSortMode.Deferred,
                null,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone);

            _spriteBatch.DrawString(_mainFont, QUIT_MESSAGE, _quitMessagePos, Color.LightGray);

            string scoreMessage = $"size: {_snakeGame.ActiveGame.SnakeSize:N0}";
            Point scoreMessageSize = _mainFont.MeasureString(scoreMessage).ToPoint();
            Point scoreMessagePosition = Game.Window.ClientBounds.Size - scoreMessageSize;
            _spriteBatch.DrawString(_mainFont, scoreMessage, scoreMessagePosition.ToVector2(), Color.LightGray);

            if (_snakeGame.ActivePlayer.IsHuman && !_snakeGame.ActiveGame.Alive)
            {
                _spriteBatch.DrawString(_mainFont, GAME_OVER_MESSAGE, _gameOverMessagePos, Color.LightGoldenrodYellow);
                _spriteBatch.DrawString(_mainFont, TRY_AGAIN_MESSAGE, _tryAgainMessagePos, Color.LightGoldenrodYellow);
            }

            static string GetBrainTypeName(AIBrainType brainType)
                => brainType switch
                {
                    AIBrainType.DescendentCoalesced => "coalesced",
                    AIBrainType.DescendentMixed => "mixed",
                    AIBrainType.MutatedClone => "mutant",
                    AIBrainType.OneOfGodsOwnPrototypes => "prototype",
                    _ => throw new ArgumentOutOfRangeException(nameof(brainType))
                };

            var stringBuilder = new StringBuilder()
                .Append("gen: ").AppendFormat("{0:N0}", _snakeGame.Generation).Append("; ")
                .Append("idx: ").AppendFormat("{0:N0}", _snakeGame.AIPlayerIndex).Append("; ")
                .Append("id: ").AppendFormat("{0:N0}", _snakeGame.ActivePlayer.Id).Append("; ")
                .Append("species: ").AppendFormat("{0:N0}", _snakeGame.ActivePlayer.SpeciesId).Append("; ")
                .Append("games: ").AppendFormat("{0:N0}", _snakeGame.GamesPlayed).AppendLine()
                .Append("score: ").AppendFormat("{0:N0}", _snakeGame.ActivePlayerScore).Append("; ")
                .Append("this-gen: ").AppendFormat("{0:N0}", _snakeGame.ThisGenBestScore)
                    .Append(" (").Append(_snakeGame.ThisGenBestUnit)
                    .Append('/').Append(_snakeGame.ThisGenBestSpecies)
                    .Append("); ")
                .Append("all-time: ").AppendFormat("{0:N0}", _snakeGame.AllTimeBestScore)
                    .Append(" (").Append(_snakeGame.AllTimeBestUnit)
                    .Append('/').Append(_snakeGame.AllTimeBestSpecies)
                    .AppendLine(") ")
                .Append("brain: ").Append(GetBrainTypeName(_snakeGame.ActivePlayer.BrainType));

            _spriteBatch.DrawString(_mainFont, stringBuilder, Vector2.Zero, Color.LightGray);

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        protected override void LoadContent()
        {
            _mainFont = Game.Content.Load<SpriteFont>("UIFont");
            _smallFont = Game.Content.Load<SpriteFont>("UIFont-Small");

            OnResize(this, EventArgs.Empty);

            base.LoadContent();
        }

        private void OnResize(object sender, EventArgs e)
        {
            int windowHeight = Game.Window.ClientBounds.Height;
            int windowWidth = Game.Window.ClientBounds.Width;

            Vector2 gameOverMessageSize = _mainFont.MeasureString(GAME_OVER_MESSAGE);
            _gameOverMessagePos = new Vector2(
                (windowWidth - gameOverMessageSize.X) / 2,
                (windowHeight / 2) - gameOverMessageSize.Y);

            Vector2 quitMessageSize = _mainFont.MeasureString(QUIT_MESSAGE);
            _quitMessagePos = new Vector2(0.0f, windowHeight - quitMessageSize.Y);

            Vector2 tryAgainMessageSize = _mainFont.MeasureString(TRY_AGAIN_MESSAGE);
            _tryAgainMessagePos = new Vector2(
                (windowWidth - tryAgainMessageSize.X) / 2,
                windowHeight / 2);
        }
    }
}
