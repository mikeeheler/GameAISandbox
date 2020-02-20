using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;
using Microsoft.Xna.Framework;

namespace SnakeGame
{
    public class SnakeGameState : ISnakeGameState
    {
        private readonly RandomSource _rng;
        private readonly Queue<Point> _snake;

        private Point _applePosition;
        private Point _currentPosition;
        private Point _lastDirection;
        private int _targetSize;

        public SnakeGameState()
        {
            _rng = MersenneTwister.Default;
            _snake = new Queue<Point>();
        }

        public bool Alive { get; private set; }
        public int ApplesEaten { get; private set; }
        public Point ApplePosition => _applePosition;
        public IReadOnlyCollection<Point> Snake => _snake.Select(p => new Point(p.X, p.Y)).ToArray();

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

        public Matrix<double> GetVision(Point position, Point forward)
        {
            Point left = new Point(forward.Y, -forward.X);
            Point right = new Point(-forward.Y, forward.X);
            Point behind = new Point(-forward.X, -forward.Y);

            // 3x8 matrix; each column encodes the data for a particular direction.
            // i.e. forward: m[0,0] = apple, m[1,0] = snake, m[2,0] = wall
            return Matrix<double>.Build.Dense(3, 8,
                Look(position, forward)
                .Concat(Look(position, forward + left))
                .Concat(Look(position, left))
                .Concat(Look(position, behind + left))
                .Concat(Look(position, forward + right))
                .Concat(Look(position, right))
                .Concat(Look(position, behind + right))
                .ToArray());
        }

        public bool IsInBounds(Point point)
            => point.X >= 0 && point.X < SnakeRules.FIELD_WIDTH && point.Y >= 0 && point.Y < SnakeRules.FIELD_HEIGHT;

        public bool IsLegalMove(PlayerMovement move)
            => GetDirection(move) + _lastDirection != Point.Zero; // The only illegal move is a 180

        public void Move(PlayerMovement move)
        {
            Debug.Assert(IsLegalMove(move));

            if (!Alive)
                return;

            Point direction = GetDirection(move);
            Point newPosition = _currentPosition + direction;

            switch (GetTile(newPosition))
            {
                case TileState.Apple:
                    ApplesEaten++;
                    _targetSize += SnakeRules.GROW_LENGTH;
                    do { _applePosition = GetRandomPoint(); }
                    while (_applePosition == newPosition && GetTile(_applePosition) != TileState.Empty);
                    break;
                case TileState.Empty: break;
                case TileState.Snake:
                case TileState.Void:
                    Alive = false;
                    break;
            }

            _snake.Enqueue(newPosition);
            while (_snake.Count > _targetSize)
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
            _targetSize = SnakeRules.START_LENGTH;
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
