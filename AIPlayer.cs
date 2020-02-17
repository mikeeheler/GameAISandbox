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
        private AIPlayerBrain _brain;
        private bool _isInitialized;
        private SnakeGame _snakeGame;

        public AIPlayer()
        {
            _isInitialized = false;
        }

        public bool IsHuman { get; } = false;

        public AIPlayer Clone()
        {
            return new AIPlayer
            {
                _brain = _brain.Clone(),
                _isInitialized = true,
                _snakeGame = _snakeGame,
            };
        }

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
            var appleVector = applePosition - direction;
            var result = _brain.Compute(
                direction[0],
                direction[1],
                appleVector[0],
                appleVector[1],
                _snakeGame.SnakePosition.X,
                _snakeGame.SnakePosition.Y,
                _snakeGame.IsLegalMove(PlayerMovement.Down) ? 1 : -1,
                _snakeGame.IsLegalMove(PlayerMovement.Left) ? 1 : -1,
                _snakeGame.IsLegalMove(PlayerMovement.Right) ? 1 : -1,
                _snakeGame.IsLegalMove(PlayerMovement.Up) ? 1 : -1);
            var values = Vector<float>.Build.Dense(result);
            int index =  values.MaximumIndex();

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
        private readonly int _inputSize = 10;
        private readonly int _layer1Size = 20;
        private readonly int _layer2Size = 20;
        private readonly int _outputSize = 4;

        private readonly Matrix<double> _layer1Bias;
        private readonly Matrix<double> _layer2Bias;
        private readonly Matrix<double> _layer3Bias;
        private readonly Matrix<double> _layer1Weights;
        private readonly Matrix<double> _layer2Weights;
        private readonly Matrix<double> _layer3Weights;

        private Matrix<double> _layer1Values;
        private Matrix<double> _layer2Values;
        private Matrix<double> _layer3Values;

        public AIPlayerBrain()
        {
            // mathnet indices: row,col
            // mathnet mem: col maj

            double bias = -1.0;

            _layer1Bias = Matrix<double>.Build.Dense(1, _layer1Size, bias);
            _layer2Bias = Matrix<double>.Build.Dense(1, _layer2Size, bias);
            _layer3Bias = Matrix<double>.Build.Dense(1, _outputSize, bias);
            _layer1Weights = Matrix<double>.Build.Random(_inputSize, _layer1Size, new Normal(0.0, 1.0));
            _layer2Weights = Matrix<double>.Build.Random(_layer1Size, _layer2Size, new Normal(0.0, 1.0));
            _layer3Weights = Matrix<double>.Build.Random(_layer2Size, _outputSize, new Normal(0.0, 1.0));
        }

        private AIPlayerBrain(AIPlayerBrain other)
        {
            _layer1Bias = other._layer1Bias.Clone();
            _layer2Bias = other._layer2Bias.Clone();
            _layer3Bias = other._layer3Bias.Clone();
            _layer1Weights = other._layer1Weights.Clone();
            _layer2Weights = other._layer2Weights.Clone();
            _layer3Weights = other._layer3Weights.Clone();
            _layer1Values = other._layer1Values.Clone();
            _layer2Values = other._layer2Values.Clone();
            _layer3Values = other._layer3Values.Clone();
        }

        public AIPlayerBrain Clone()
        {
            return new AIPlayerBrain(this);
        }

        public float[] Compute(params double[] inputs)
        {
            var inputValues = Matrix<double>.Build.DenseOfRowArrays(inputs);

            _layer1Values = Tanh(inputValues * _layer1Weights + _layer1Bias);
            _layer2Values = Tanh(_layer1Values * _layer2Weights + _layer2Bias);
            _layer3Values = Tanh(_layer2Values * _layer3Weights + _layer3Bias);

            return _layer3Values.ToColumnMajorArray().Select(i => (float)i).ToArray();
        }

        public void Mutate(double mutationRate)
        {
            Matrix<double>[] weights = { _layer1Weights, _layer2Weights, _layer3Weights };
            foreach (Matrix<double> weightMatrix in weights)
            {
                for (int row = 0; row < weightMatrix.RowCount; ++row)
                {
                    for (int col = 0; col < weightMatrix.ColumnCount; ++col)
                    {
                        if (MersenneTwister.Default.NextDouble() > mutationRate)
                            continue;

                        weightMatrix[row,col] += MersenneTwister.Default.NextDouble() * 2.0 - 1.0;
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
            return input;
            /*return Matrix<double>.Build.Dense(
                input.RowCount,
                input.ColumnCount,
                (row, col) => Math.Clamp(input[row,col], 0.0, 1.0));*/
        }
    }
}
