using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.Xna.Framework;

namespace SnakeGame
{
    public interface ISnakeGameState
    {
        bool Alive { get; }
        int ApplesEaten { get; }
        Point ApplePosition { get; }
        IReadOnlyCollection<Point> Snake { get; }

        TileState GetTile(Point point);
        Matrix<double> GetVision(Point position, Point direction);
        bool IsInBounds(Point point);
        bool IsLegalMove(PlayerMovement move);
        void Move(PlayerMovement move);
        void Reset();
    }
}
