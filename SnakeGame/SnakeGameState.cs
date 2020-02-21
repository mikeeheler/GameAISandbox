using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;
using Microsoft.Xna.Framework;

namespace SnakeGame
{
    public class SnakeGameState
    {
        private readonly RandomSource _rng;
        private readonly Queue<Point> _snake;

        private Point _applePosition;
        private Point _currentPosition;
        private Point _lastDirection;

        public SnakeGameState()
        {
            _rng = MersenneTwister.Default;
            _snake = new Queue<Point>();
            Reset();
        }

        public bool Alive { get; private set; }
        public int ApplesEaten { get; private set; }
        public Point ApplePosition => _applePosition;
        public IReadOnlyCollection<Point> Snake => _snake.Select(p => new Point(p.X, p.Y)).ToArray();
        public int SnakeSize { get; private set; }
        public int TotalTurns { get; private set; }
        public int TurnsSinceEating { get; private set; }

        public TileState GetTile(Point point)
        {
            if (point == _applePosition)
                return TileState.Apple;

            if (!IsInBounds(point))
                return TileState.Void;

            if (_snake.Contains(point))
                return TileState.Snake;

            return TileState.Empty;
        }

        public double[] GetVision()
        {
            if (_lastDirection == Point.Zero)
                return Enumerable.Repeat(0.0, 21).ToArray();

            Point forward = _lastDirection;
            Point left = new Point(forward.Y, -forward.X);
            Point right = new Point(-forward.Y, forward.X);
            Point behind = new Point(-forward.X, -forward.Y);

            // 3x8 matrix; each column encodes the data for a particular direction.
            // i.e. forward: m[0,0] = apple, m[1,0] = snake, m[2,0] = wall
            return Look(_currentPosition, forward)
                .Concat(Look(_currentPosition, forward + left))
                .Concat(Look(_currentPosition, left))
                .Concat(Look(_currentPosition, behind + left))
                .Concat(Look(_currentPosition, forward + right))
                .Concat(Look(_currentPosition, right))
                .Concat(Look(_currentPosition, behind + right))
                .ToArray();
        }

        public bool IsInBounds(Point point)
            => point.X >= 0 && point.X < SnakeRules.FIELD_WIDTH && point.Y >= 0 && point.Y < SnakeRules.FIELD_HEIGHT;

        public bool IsLegalMove(PlayerMovement move)
            => GetDirection(move) + _lastDirection != Point.Zero; // The only illegal move is a 180

        public void Move(PlayerMovement move)
        {
            Debug.Assert(IsLegalMove(move));

            if (!(Alive && IsLegalMove(move)))
                return;

            Point direction = GetDirection(move);
            Point newPosition = _currentPosition + direction;
            TotalTurns++;
            TurnsSinceEating++;

            switch (GetTile(newPosition))
            {
                case TileState.Apple:
                    ApplesEaten++;
                    SnakeSize += SnakeRules.GROW_LENGTH;
                    do { _applePosition = GetRandomPoint(); }
                    while (_applePosition == newPosition && GetTile(_applePosition) != TileState.Empty);
                    TurnsSinceEating = 0;
                    break;

                case TileState.Empty: break;
                case TileState.Snake:
                case TileState.Void:
                    Alive = false;
                    break;
            }

            _snake.Enqueue(newPosition);
            while (_snake.Count > SnakeSize)
                _snake.Dequeue();

            _currentPosition = newPosition;
            _lastDirection = direction;
        }

        public void Reset()
        {
            Alive = true;
            _applePosition = GetRandomPoint();
            ApplesEaten = 0;
            _currentPosition = new Point(SnakeRules.FIELD_WIDTH / 2, SnakeRules.FIELD_HEIGHT / 2);
            _lastDirection = Point.Zero;
            _snake.Clear();
            _snake.Enqueue(_currentPosition);
            SnakeSize = SnakeRules.START_LENGTH;
            TotalTurns = 0;
            TurnsSinceEating = 0;
        }

        private Point GetDirection(PlayerMovement move)
            => move switch
            {
                PlayerMovement.Down => new Point(0, 1),
                PlayerMovement.Left => new Point(-1, 0),
                PlayerMovement.Right => new Point(1, 0),
                PlayerMovement.Up => new Point(0, -1),
                _ => throw new ArgumentOutOfRangeException(nameof(move))
            };

        private Point GetRandomPoint()
            => new Point(_rng.Next(SnakeRules.FIELD_WIDTH), _rng.Next(SnakeRules.FIELD_HEIGHT));

        private double[] Look(Point point, Point direction)
        {
            // apple, snake, wall
            // inverted distance, so 1 = on top of it, 0 = far away
            var result = new double[] { 0.0, 0.0, 0.0 };

            Point lookPos = point;
            int distance = 0;
            bool snakeFound = false;

        lookAgain:

            lookPos += direction;
            ++distance;

            if (!IsInBounds(lookPos))
            {
                result[2] = Math.Sqrt(1.0 / distance);
                return result;
            }

            if (lookPos == _applePosition)
            {
                result[0] = Math.Sqrt(1.0 / distance);
                goto lookAgain;
            }

            // Only want distance to closest piece of snake, don't keep checking if it's found
            if (!snakeFound && _snake.Contains(lookPos))
            {
                result[1] = Math.Sqrt(1.0 / distance);
                snakeFound = true;
            }

            goto lookAgain;
        }
    }
}