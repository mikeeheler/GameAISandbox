using System;
using System.Linq;
using System.Threading;

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;

namespace Snakexperiment
{
    public class AIPlayer : IPlayerController
    {
        private static long _globalId = 0;

        private AIBrain _brain;
        private bool _isInitialized;
        private PlayerMovement _lastMovement;
        private SnakeGame _snakeGame;

        public AIPlayer()
        {
            Id = Interlocked.Increment(ref _globalId);
            _isInitialized = false;
            Decision = Vector<float>.Build.Dense(4, 0.0f);
            _lastMovement = PlayerMovement.Right;
            SpeciesId = Id;
        }

        public AIBrainType BrainType => _brain.BrainType;
        public Vector<float> Decision { get; private set; }
        public long Id { get; }
        public bool IsHuman { get; } = false;
        public long SpeciesId { get; private set; }

        public AIPlayer Clone()
        {
            return new AIPlayer
            {
                _brain = _brain.Clone(),
                _isInitialized = true,
                _snakeGame = _snakeGame,
                SpeciesId = SpeciesId
            };
        }

        public AIPlayer BreedWith(AIPlayer consentingAdult, AIBreedingMode breedingMode)
        {
            return new AIPlayer
            {
                _brain = _brain.MergeWith(consentingAdult._brain, breedingMode),
                _isInitialized = true,
                _snakeGame = _snakeGame,
                SpeciesId = SpeciesId
            };
        }

        public AIBrain CloneBrain()
            => _brain.Clone();

        public PlayerMovement GetMovement()
            => GetNextMove();

        public void Initialize(SnakeGame snakeGame)
        {
            if (_isInitialized)
                return;

            _snakeGame = snakeGame;
            _brain = new AIBrain();
            _isInitialized = true;
        }

        public void Mutate(double mutationRate)
        {
            _brain.Mutate(mutationRate);
        }

        public void Shutdown()
        {
        }

        private PlayerMovement GetNextMove()
        {
            var result = _brain.Compute(_snakeGame.GetSnakeVision());

            // Normalize values to make the sum ~=1.0 (p-norm=1.0 does this -- this is not a unit vector)
            var values = Vector<float>.Build.Dense(result).Normalize(1.0);
            // Bind each result to the movement it would select them sort them ascending
            var moves = new[] {
                (values[0], _lastMovement),
                (values[1], TurnLeft(_lastMovement)),
                (values[2], TurnRight(_lastMovement))
            }.OrderBy(item => item.Item1).ToArray();
            double totalSum = moves.Sum(x => x.Item1); // should be ==1.0 but in practice it's ~=1.0
            PlayerMovement move;
            // Sometimes the brain can't decide what to do and all values are zero (or close to it)
            // in this case just pick one.
            if (totalSum < 0.01)
            {
                int index = MersenneTwister.Default.Next(moves.Length);
                move = moves[index].Item2;
            }
            else
            {
                // Select a move based on probability, i.e. if 0=0.1, 1=0.2, and 3=0.7 after normalization then
                // 0-0.1 select move 0, 0.1-0.3 select move 1, and 0.3+ select move 2
                double roll = MersenneTwister.Default.NextDouble() * totalSum;
                double sum = 0.0;
                int index = 0;
                while (sum <= roll)
                    sum += moves[index++].Item1;
                move = moves[index-1].Item2;
            }
            Decision = values;
            _lastMovement = move;
            return move;
        }

        private PlayerMovement TurnLeft(PlayerMovement move)
        {
            return move switch
            {
                PlayerMovement.Down => PlayerMovement.Right,
                PlayerMovement.Left => PlayerMovement.Down,
                PlayerMovement.Right => PlayerMovement.Up,
                PlayerMovement.Up => PlayerMovement.Left,
                _ => throw new ArgumentOutOfRangeException(nameof(move)),
            };
        }

        private PlayerMovement TurnRight(PlayerMovement move)
        {
            return move switch
            {
                PlayerMovement.Down => PlayerMovement.Left,
                PlayerMovement.Left => PlayerMovement.Up,
                PlayerMovement.Right => PlayerMovement.Down,
                PlayerMovement.Up => PlayerMovement.Right,
                _ => throw new ArgumentOutOfRangeException(nameof(move)),
            };
        }
    }
}
