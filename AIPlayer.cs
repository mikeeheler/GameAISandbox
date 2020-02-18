using System;
using System.Linq;

using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;
using Microsoft.Xna.Framework;

namespace Snakexperiment
{
    public class AIPlayer : IPlayerController
    {
        private readonly Point _downDirection = new Point(0, 1);
        private readonly Point _leftDirection = new Point(-1, 0);
        private readonly Point _rightDirection = new Point(1, 0);
        private readonly Point _upDirection = new Point(0, -1);
        private AIPlayerBrain _brain;
        private bool _isInitialized;
        private SnakeGame _snakeGame;
        private Vector<float> _lastDecision;

        public AIPlayer()
        {
            _isInitialized = false;
            _lastDecision = Vector<float>.Build.Dense(4, 0.0f);
        }

        public bool IsHuman { get; } = false;
        public Vector<float> Decision => _lastDecision;

        public AIPlayer Clone()
        {
            return new AIPlayer
            {
                _brain = _brain.Clone(),
                _isInitialized = true,
                _snakeGame = _snakeGame
            };
        }

        public AIPlayer BreedWith(AIPlayer consentingAdult)
        {
            return new AIPlayer
            {
                _brain = _brain.MergeWith(consentingAdult._brain),
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
            var direction = PointToVector(_snakeGame.Direction);
            var applePosition = PointToVector(_snakeGame.ApplePosition);
            var appleVector = (applePosition - direction).Normalize(2.0);
            var snakePos = _snakeGame.SnakePosition;
            /*
            var result = _brain.Compute(
                direction[0],
                direction[1],
                appleVector[0],
                appleVector[1],
                _snakeGame.IsLegalMove(PlayerMovement.Down)
                    && !_snakeGame.IsCollision(snakePos + _downDirection) ? 1 : -1,
                _snakeGame.IsLegalMove(PlayerMovement.Left)
                    && !_snakeGame.IsCollision(snakePos + _leftDirection) ? 1 : -1,
                _snakeGame.IsLegalMove(PlayerMovement.Right)
                    && !_snakeGame.IsCollision(snakePos + _rightDirection) ? 1 : -1,
                _snakeGame.IsLegalMove(PlayerMovement.Up)
                    && !_snakeGame.IsCollision(snakePos + _upDirection) ? 1 : -1);
            */
            var result = _brain.Compute(_snakeGame.GetBoardValues());
            var values = Vector<float>.Build.Dense(result);
            int index =  values.MaximumIndex();
            _lastDecision = values;

            return index switch
            {
                0 => PlayerMovement.Down,
                1 => PlayerMovement.Left,
                2 => PlayerMovement.Right,
                3 => PlayerMovement.Up,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private Vector<double> PointToVector(Point point)
            => Vector<double>.Build.Dense(new double[] { point.X, point.Y });
    }

    public class AIPlayerBrain
    {
        private readonly int _inputSize = 400;
        private readonly int _layer1Size = 200;
        private readonly int _layer2Size = 100;
        private readonly int _outputSize = 4;

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
                (row, col) => MersenneTwister.Default.NextDouble() * 2.0 - 1.0);
            _layer2Bias = Matrix<double>.Build.Dense(
                1,
                _layer2Size,
                (row, col) => MersenneTwister.Default.NextDouble() * 2.0 - 1.0);
            _layer3Bias = Matrix<double>.Build.Dense(
                1,
                _outputSize,
                (row, col) => MersenneTwister.Default.NextDouble() * 2.0 - 1.0);
            _layer1Weights = Matrix<double>.Build.Dense(
                _inputSize,
                _layer1Size,
                (row, col) => MersenneTwister.Default.NextDouble() * 2.0 - 1.0);
            _layer2Weights = Matrix<double>.Build.Dense(
                _layer1Size,
                _layer2Size,
                (row, col) => MersenneTwister.Default.NextDouble() * 2.0 - 1.0);
            _layer3Weights = Matrix<double>.Build.Dense(
                _layer2Size,
                _outputSize,
                (row, col) => MersenneTwister.Default.NextDouble() * 2.0 - 1.0);
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

        private AIPlayerBrain(AIPlayerBrain left, AIPlayerBrain right)
        {
            _layer1Bias = left._layer1Bias * 0.5 + right._layer1Bias * 0.5;
            _layer2Bias = left._layer2Bias * 0.5 + right._layer2Bias * 0.5;
            _layer3Bias = left._layer3Bias * 0.5 + right._layer3Bias * 0.5;
            _layer1Weights = left._layer1Weights * 0.5 + right._layer1Weights * 0.5;
            _layer2Weights = left._layer2Weights * 0.5 + right._layer2Weights * 0.5;
            _layer3Weights = left._layer3Weights * 0.5 + right._layer3Weights * 0.5;
            _inputValues = left._inputValues?.Clone();
            _layer1Values = left._layer1Values?.Clone();
            _layer2Values = left._layer2Values?.Clone();
            _layer3Values = left._layer3Values?.Clone();
        }

        public AIPlayerBrain Clone()
            => new AIPlayerBrain(this);

        public float[] Compute(params double[] inputs)
        {
            _inputValues = Matrix<double>.Build.DenseOfRowArrays(inputs);

            _layer1Values = ReLU(_inputValues * _layer1Weights + _layer1Bias);
            _layer2Values = ReLU(_layer1Values * _layer2Weights + _layer2Bias);
            _layer3Values = ReLU(_layer2Values * _layer3Weights + _layer3Bias);

            return _layer3Values.ToColumnMajorArray().Select(i => (float)i).ToArray();
        }

        public Matrix<double>[] GetValues()
            => new[] { /*_layer1Values?.Clone(),*/ _layer3Values?.Clone() };

        public Matrix<double>[] GetWeights()
            => new[] { /*_layer1Weights.Clone(),*/ _layer3Weights.Clone() };

        public AIPlayerBrain MergeWith(AIPlayerBrain other)
            => new AIPlayerBrain(this, other);

        public void Mutate(double mutationRate)
        {
            double randomRate = MersenneTwister.Default.NextDouble() * mutationRate;
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
                        if (MersenneTwister.Default.NextDouble() > randomRate)
                            continue;

                        int method = MersenneTwister.Default.Next(4);
                        switch (method)
                        {
                            case 0: // tweak by up to ±0.1
                                mutateMatrix[row, col] += MersenneTwister.Default.NextDouble() * 0.2 - 0.1;
                                break;
                            case 1: // replace with a new weight range -1.0 to 1.0
                                mutateMatrix[row, col] = MersenneTwister.Default.NextDouble() * 2.0 - 1.0;
                                break;
                            case 2: // multiply by up to 20%
                                mutateMatrix[row, col] *= MersenneTwister.Default.NextDouble() * 0.2;
                                break;
                            case 3: // negate
                                mutateMatrix[row, col] *= -1;
                                break;
                        }
                    }
                }
            }
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
            return Matrix<double>.Build.Dense(
                input.RowCount,
                input.ColumnCount,
                (row, col) => Math.Max(0.0, input[row,col]));
        }
    }
}
