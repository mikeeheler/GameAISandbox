using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using MathNet.Numerics.LinearAlgebra;

namespace SnakeGame
{
    public class AIPlayer : IPlayerController
    {
        private static long _globalId = 0;

        private AIBrain _brain;
        private bool _isInitialized;
        private PlayerMovement _lastMovement;

        public AIPlayer()
        {
            Id = Interlocked.Increment(ref _globalId);
            _isInitialized = false;
            Decision = Vector<float>.Build.Dense(4, 0.0f);
            IsHuman = false;
            Name = Guid.NewGuid().ToString();
            _lastMovement = PlayerMovement.Right;
            SpeciesName = GenerateSpeciesName();
        }

        private AIPlayer(Stream inputStream)
        {
            using var reader = new BinaryReader(inputStream, Encoding.UTF8, true);

            string nameKey = reader.ReadString();
            if (nameKey != "name") throw new FormatException();
            Name = reader.ReadString();

            string speciesKey = reader.ReadString();
            if (speciesKey != "species") throw new FormatException();
            SpeciesName = reader.ReadString();

            _brain = AIBrain.Deserialize(inputStream);

            Id = Interlocked.Increment(ref _globalId);
            _isInitialized = true;
            Decision = Vector<float>.Build.Dense(4, 0.0f);
            IsHuman = false;
            _lastMovement = PlayerMovement.Right;
        }

        public AIBrainType BrainType => _brain.BrainType;
        public Vector<float> Decision { get; private set; }
        public long Id { get; }
        public bool IsHuman { get; }
        public string Name { get; set; }
        public string SpeciesName { get; private set; }

        public static AIPlayer Deserialize(Stream inputStream)
            => new AIPlayer(inputStream);

        private static string GenerateSpeciesName()
        {
            int[] charmap = Enumerable.Range(48, 10)
                .Concat(Enumerable.Range(65, 26))
                .Concat(Enumerable.Range(97, 26))
                .ToArray();
            byte[] name = Enumerable.Range(0, 8)
                .Select(_ => charmap[SnakeRandom.Default.Next(charmap.Length)])
                .Select(Convert.ToByte)
                .ToArray();
            return Encoding.ASCII.GetString(name);
        }

        public AIPlayer BreedWith(AIPlayer consentingAdult, AIBreedingMode breedingMode)
        {
            return new AIPlayer
            {
                _brain = _brain.MergeWith(consentingAdult._brain, breedingMode),
                _isInitialized = true,
                SpeciesName = SpeciesName
            };
        }

        public AIPlayer Clone()
        {
            return new AIPlayer
            {
                _brain = _brain.Clone(),
                _isInitialized = true,
                SpeciesName = SpeciesName
            };
        }

        public AIBrain CloneBrain()
            => _brain.Clone();

        public PlayerMovement GetMovement(SnakeGameSim gameState)
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

        public void SerializeTo(Stream outputStream)
        {
            using var writer = new BinaryWriter(outputStream, Encoding.UTF8, true);
            writer.Write("name");
            writer.Write(Name);
            writer.Write("species");
            writer.Write(SpeciesName);
            _brain.SerializeTo(outputStream);
        }

        private PlayerMovement GetNextMove(SnakeGameSim instance)
        {
            double[] vision = instance.GetVision();
            double[] computeValues = new double[1 + vision.Length];
            Array.Copy(vision, 0, computeValues, 1, vision.Length);
            computeValues[0] = (double)instance.TurnsSinceEating / instance.GameRules.MaxAITurns;

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
                int index = SnakeRandom.Default.Next(moves.Length);
                move = moves[index].Item2;
            }
            else
            {
                // Select a move based on probability, i.e. if 0=0.1, 1=0.2, and 3=0.7 after normalization then
                // 0-0.1 select move 0, 0.1-0.3 select move 1, and 0.3+ select move 2
                double roll = SnakeRandom.Default.NextDouble() * totalSum;
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
