using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;
using Microsoft.Xna.Framework;

namespace Snakexperiment
{
    public class AIPlayer : IPlayerController
    {
        private static long _globalId = 0;

        private AIPlayerBrain _brain;
        private bool _isInitialized;
        private PlayerMovement _lastMovement;
        private SnakeGame _snakeGame;
        private long _speciesId;

        public AIPlayer()
        {
            Id = Interlocked.Increment(ref _globalId);
            _isInitialized = false;
            Decision = Vector<float>.Build.Dense(4, 0.0f);
            _lastMovement = PlayerMovement.Right;
            SpeciesId = Id;
        }

        public long Id { get; }
        public bool IsHuman { get; } = false;
        public Vector<float> Decision { get; private set; }
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
                _snakeGame = _snakeGame
            };
        }

        public AIPlayerBrain CloneBrain()
            => _brain.Clone();

        public PlayerMovement GetMovement()
            => GetNextMove();

        public void Initialize(SnakeGame snakeGame)
        {
            if (_isInitialized)
                return;

            _snakeGame = snakeGame;
            _brain = new AIPlayerBrain();
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

    public enum AIBreedingMode
    {
        Blend,
        Mix
    }

    public class AIPlayerBrain
    {
        private readonly RandomSource _random = MersenneTwister.Default;

        private readonly int _inputSize = 24;
        private readonly int _layer1Size = 18;
        private readonly int _layer2Size = 18;
        private readonly int _outputSize = 3;

        private readonly Matrix<double> _layer1Bias;
        private readonly Matrix<double> _layer2Bias;
        private readonly Matrix<double> _layer3Bias;
        private readonly Matrix<double> _layer1Weights;
        private readonly Matrix<double> _layer2Weights;
        private readonly Matrix<double> _layer3Weights;

        private Matrix<double> _inputValues;
        private Matrix<double> _layer1Values;
        private Matrix<double> _layer2Values;
        private Matrix<double> _layer3Values;

        public AIPlayerBrain()
        {
            // mathnet indices: row,col
            // mathnet mem: col maj

            _layer1Bias = Matrix<double>.Build.Dense(
                1,
                _layer1Size,
                (row, col) => _random.NextDouble() * 2.0 - 1.0);
            _layer2Bias = Matrix<double>.Build.Dense(
                1,
                _layer2Size,
                (row, col) => _random.NextDouble() * 2.0 - 1.0);
            _layer3Bias = Matrix<double>.Build.Dense(
                1,
                _outputSize,
                (row, col) => _random.NextDouble() * 2.0 - 1.0);
            _layer1Weights = Matrix<double>.Build.Dense(
                _inputSize,
                _layer1Size,
                (row, col) => _random.NextDouble() * 2.0 - 1.0);
            _layer2Weights = Matrix<double>.Build.Dense(
                _layer1Size,
                _layer2Size,
                (row, col) => _random.NextDouble() * 2.0 - 1.0);
            _layer3Weights = Matrix<double>.Build.Dense(
                _layer2Size,
                _outputSize,
                (row, col) => _random.NextDouble() * 2.0 - 1.0);
        }

        private AIPlayerBrain(AIPlayerBrain other)
        {
            _layer1Bias = other._layer1Bias.Clone();
            _layer2Bias = other._layer2Bias.Clone();
            _layer3Bias = other._layer3Bias.Clone();
            _layer1Weights = other._layer1Weights.Clone();
            _layer2Weights = other._layer2Weights.Clone();
            _layer3Weights = other._layer3Weights.Clone();
            _inputValues = other._inputValues?.Clone();
            _layer1Values = other._layer1Values?.Clone();
            _layer2Values = other._layer2Values?.Clone();
            _layer3Values = other._layer3Values?.Clone();
        }

        private AIPlayerBrain(AIPlayerBrain left, AIPlayerBrain right, AIBreedingMode breedingMode)
        {
            switch (breedingMode)
            {
                case AIBreedingMode.Blend:
                    _layer1Bias = left._layer1Bias * 0.5 + right._layer1Bias * 0.5;
                    _layer2Bias = left._layer2Bias * 0.5 + right._layer2Bias * 0.5;
                    _layer3Bias = left._layer3Bias * 0.5 + right._layer3Bias * 0.5;
                    _layer1Weights = left._layer1Weights * 0.5 + right._layer1Weights * 0.5;
                    _layer2Weights = left._layer2Weights * 0.5 + right._layer2Weights * 0.5;
                    _layer3Weights = left._layer3Weights * 0.5 + right._layer3Weights * 0.5;
                    break;

                case AIBreedingMode.Mix:
                    _layer1Bias = MixMatrices(left._layer1Bias, right._layer1Bias);
                    _layer2Bias = MixMatrices(left._layer2Bias, right._layer2Bias);
                    _layer3Bias = MixMatrices(left._layer3Bias, right._layer3Bias);
                    _layer1Weights = MixMatrices(left._layer1Weights, right._layer1Weights);
                    _layer2Weights = MixMatrices(left._layer2Weights, right._layer2Weights);
                    _layer3Weights = MixMatrices(left._layer3Weights, right._layer3Weights);
                    break;

                default: throw new ArgumentOutOfRangeException(nameof(breedingMode));
            }
        }

        public AIPlayerBrain Clone()
            => new AIPlayerBrain(this);

        public float[] Compute(params double[] inputs)
        {
            _inputValues = Matrix<double>.Build.DenseOfRowArrays(inputs);

            _layer1Values = LeakyReLU(_inputValues * _layer1Weights + _layer1Bias);
            _layer2Values = LeakyReLU(_layer1Values * _layer2Weights + _layer2Bias);
            _layer3Values = LeakyReLU(_layer2Values * _layer3Weights + _layer3Bias);

            return _layer3Values.ToColumnMajorArray().Select(i => (float)i).ToArray();
        }

        public Matrix<double>[] GetValues()
            => new[] { _inputValues?.Clone(), _layer1Values?.Clone(), _layer2Values?.Clone(), _layer3Values?.Clone() };

        public Matrix<double>[] GetWeights()
            => new[] { _layer1Weights.Clone(), _layer2Weights.Clone(), _layer3Weights.Clone() };

        public AIPlayerBrain MergeWith(AIPlayerBrain other, AIBreedingMode breedingMode)
            => new AIPlayerBrain(this, other, breedingMode);

        public void Mutate(double mutationRate)
        {
            double randomRate = _random.NextDouble() * mutationRate;
            Matrix<double>[] mutateMatrices =
            {
                _layer1Bias, _layer2Bias, _layer3Bias,
                _layer1Weights, _layer2Weights, _layer3Weights
            };
            foreach (Matrix<double> mutateMatrix in mutateMatrices)
            {
                for (int row = 0; row < mutateMatrix.RowCount; ++row)
                {
                    for (int col = 0; col < mutateMatrix.ColumnCount; ++col)
                    {
                        if (_random.NextDouble() > randomRate)
                            continue;

                        int method = _random.Next(4);
                        switch (method)
                        {
                            case 0: // tweak by up to ±0.2
                                mutateMatrix[row, col] += _random.NextDouble() * 0.4 - 0.2;
                                break;
                            case 1: // replace with a new weight range -1.0 to 1.0
                                mutateMatrix[row, col] = _random.NextDouble() * 2.0 - 1.0;
                                break;
                            case 2: // weaken or strengthen by up to 20%
                                mutateMatrix[row, col] *= 1.0 + _random.NextDouble() * 0.4 - 0.2;
                                break;
                            case 3: // negate
                                mutateMatrix[row, col] *= -1;
                                break;
                        }

                        mutateMatrix[row, col] = Math.Clamp(mutateMatrix[row, col], -1.0, 1.0);
                    }
                }
            }
        }

        private Matrix<double> MixMatrices(Matrix<double> left, Matrix<double> right)
        {
            Debug.Assert(left.ColumnCount == right.ColumnCount);
            Debug.Assert(left.RowCount == right.RowCount);
            return Matrix<double>.Build.Dense(
                left.RowCount,
                left.ColumnCount,
                (row, col) => _random.NextDouble() < 0.5 ? left[row,col] : right[row,col]);
        }

        private Matrix<double> Tanh(Matrix<double> input)
        {
            return input.PointwiseTanh();
        }

        private Matrix<double> Sigmoid(Matrix<double> input)
        {
            return Matrix<double>.Build.Dense(
                input.RowCount,
                input.ColumnCount,
                (row, col) => 1.0 / (1.0 + Math.Pow(MathHelper.E, -input[row,col])));
        }

        private Matrix<double> ReLU(Matrix<double> input)
        {
            return input.PointwiseMaximum(0.0);
        }

        private Matrix<double> LeakyReLU(Matrix<double> input)
        {
            return Matrix<double>.Build.Dense(
                input.RowCount,
                input.ColumnCount,
                (row, col) => input[row,col] < 0.0 ? 0.01 * input[row,col] : input[row,col]);
        }
    }
}
