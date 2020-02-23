using System;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SnakeGame
{
    public class SnakeStatusHUD
    {
        private const string GAME_OVER_MESSAGE = "GAME OVER";
        private const string QUIT_MESSAGE = "Q to quit";
        private const string TRY_AGAIN_MESSAGE  = "SPACE to try again";

        private readonly SnakeEngine _engine;

        private SpriteFont _mainFont;
        private SpriteFont _smallFont;
        private Rectangle _viewport;

        private Vector2 _gameOverMessagePos;
        private Vector2 _quitMessagePos;
        private Vector2 _tryAgainMessagePos;

        public SnakeStatusHUD(SnakeEngine engine)
        {
            _engine = engine;
        }

        public void Draw(SpriteBatch spriteBatch, SnakeGameSim instance, RenderStats renderStats)
        {
            spriteBatch.DrawString(_mainFont, QUIT_MESSAGE, _quitMessagePos, Color.LightGray);

            double fps = Math.Round(renderStats.FramesPerSecond, 2);

            string scoreMessage = $"size: {instance.SnakeSize:N0}; fps: {fps:N0}";
            Point scoreMessageSize = _mainFont.MeasureString(scoreMessage).ToPoint();
            Point scoreMessagePosition = _viewport.Size - scoreMessageSize;
            spriteBatch.DrawString(_mainFont, scoreMessage, scoreMessagePosition.ToVector2(), Color.LightGray);

            if (_engine.ActivePlayer.IsHuman && !instance.Alive)
            {
                spriteBatch.DrawString(_mainFont, GAME_OVER_MESSAGE, _gameOverMessagePos, Color.LightGoldenrodYellow);
                spriteBatch.DrawString(_mainFont, TRY_AGAIN_MESSAGE, _tryAgainMessagePos, Color.LightGoldenrodYellow);
            }

            var stringBuilder = new StringBuilder()
                .Append("gen: ").AppendFormat("{0:N0}", _engine.Generation).Append("; ")
                .Append("idx: ").AppendFormat("{0:N0}", _engine.AIPlayerIndex).Append("; ")
                .Append("id: ").AppendFormat("{0:N0}", _engine.ActivePlayer.Id).Append("; ")
                .Append("species: ").AppendFormat("{0:N0}", _engine.ActivePlayer.SpeciesName).Append("; ")
                .Append("games: ").AppendFormat("{0:N0}", _engine.GamesPlayed).AppendLine()
                .Append("score: ").AppendFormat("{0:N0}", _engine.ActivePlayerScore).Append("; ")
                .Append("this-gen: ").AppendFormat("{0:N0}", _engine.ThisGenBestScore)
                    .Append(" (").Append(_engine.ThisGenBestUnit)
                    .Append('/').Append(_engine.ThisGenBestSpecies)
                    .Append("); ")
                .Append("all-time: ").AppendFormat("{0:N0}", _engine.AllTimeBestScore)
                    .Append(" (").Append(_engine.AllTimeBestUnit)
                    .Append('/').Append(_engine.AllTimeBestSpecies)
                    .AppendLine(") ")
                .Append("brain: ").Append(GetBrainTypeName(_engine.ActivePlayer.BrainType));

            spriteBatch.DrawString(_mainFont, stringBuilder, Vector2.Zero, Color.LightGray);
        }

        public void Initialize(ISnakeGraphics graphics)
        {
            _mainFont = graphics.DefaultUIFont;
            _smallFont = graphics.SmallUIFont;
        }

        public void OnWindowResize(Rectangle clientBounds)
        {
            _viewport = clientBounds;

            Vector2 gameOverMessageSize = _mainFont.MeasureString(GAME_OVER_MESSAGE);
            _gameOverMessagePos = new Vector2(
                (_viewport.Width - gameOverMessageSize.X) / 2,
                (_viewport.Height / 2) - gameOverMessageSize.Y);

            Vector2 quitMessageSize = _mainFont.MeasureString(QUIT_MESSAGE);
            _quitMessagePos = new Vector2(0.0f, _viewport.Height - quitMessageSize.Y);

            Vector2 tryAgainMessageSize = _mainFont.MeasureString(TRY_AGAIN_MESSAGE);
            _tryAgainMessagePos = new Vector2(
                (_viewport.Width - tryAgainMessageSize.X) / 2,
                _viewport.Height / 2);
        }

        private static string GetBrainTypeName(AIBrainType brainType)
            => brainType switch
            {
                AIBrainType.DescendentCoalesced => "coalesced",
                AIBrainType.DescendentMixed => "mixed",
                AIBrainType.MutatedClone => "mutant",
                AIBrainType.OneOfGodsOwnPrototypes => "prototype",
                _ => throw new ArgumentOutOfRangeException(nameof(brainType))
            };
    }
}
