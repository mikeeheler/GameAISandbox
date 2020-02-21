using System;
using System.Linq;
using System.Threading;

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;

namespace SnakeGame
{
    public class AIPlayerController : IPlayerController
    {
        private static long _globalId = 0;

        private AIBrain _brain;
        private bool _isInitialized;
        private PlayerMovement _lastMovement;

        public AIPlayerController()
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

        public AIPlayerController Clone()
        {
            return new AIPlayerController
            {
                _brain = _brain.Clone(),
                _isInitialized = true,
                SpeciesId = SpeciesId
            };
        }

        public AIPlayerController BreedWith(AIPlayerController consentingAdult, AIBreedingMode breedingMode)
        {
            return new AIPlayerController
            {
                _brain = _brain.MergeWith(consentingAdult._brain, breedingMode),
                _isInitialized = true,
                SpeciesId = SpeciesId
            };
        }

        public AIBrain CloneBrain()
            => _brain.Clone();

        public PlayerMovement GetMovement(SnakeGameState gameState)
            => GetNextMove(gameState);

        public void Initialize()
        {
            if (_isInitialized)
                return;

            _brain = new AIBrain(22, 18, 3);
            _isInitialized = true;
        }

        public void Mutate(double mutationRate)
        {
            _brain.Mutate(mutationRate);
        }

        public void Shutdown()
        {
        }

        private PlayerMovement GetNextMove(SnakeGameState gameState)
        {
            double[] vision = gameState.GetVision();
            double[] computeValues = new double[1 + vision.Length];
            Array.Copy(vision, 0, computeValues, 1, vision.Length);
            computeValues[0] = (double)gameState.TurnsSinceEating / SnakeRules.MAX_AI_TURNS;

            var result = _brain.Compute(computeValues);
            Decision = Vector<float>.Build.Dense(result);

            var probabilities = Decision.Clone();
            // All negative probabilities; raise them until none are negative (the least desired will have 0 probability)
            if (probabilities.Maximum() < 0.0f)
                probabilities -= probabilities.Minimum();
            else // otherwise throw away all negative options
                probabilities = probabilities.PointwiseMaximum(0.0f);

            // Bind each result to the movement it would select. This distinction is arbitrary
            // TODO dig deep to see if there's some implicit bias in this mechanism
            var moves = new[] {
                (probabilities[0], _lastMovement),
                (probabilities[1], TurnLeft(_lastMovement)),
                (probabilities[2], TurnRight(_lastMovement))
            };
            double totalSum = probabilities.Sum();

            PlayerMovement move;
            // Sometimes brain can't decide what to do and all values are zero (or close to it)
            // in this case just pick one.
            if (totalSum < 0.001)
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
                while (sum < roll && index < moves.Length)
                    sum += moves[index++].Item1;
                move = moves[--index].Item2;
            }

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
