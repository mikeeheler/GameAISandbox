using System;
using System.Linq;

using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.Xna.Framework;

namespace Snakexperiment
{
    public class AIPlayer : IPlayerController
    {
        private SnakeGame _snakeGame;
        private AIPlayerBrain _brain;
        private PlayerMovement _nextMove;

        public AIPlayer()
        {
        }

        public bool IsHuman { get; } = false;

        public PlayerMovement GetMovement()
            => GetNextMove();

        public void Initialize(SnakeGame snakeGame)
        {
            _snakeGame = snakeGame;
            _brain = new AIPlayerBrain();
            _nextMove = PlayerMovement.Right;
            _nextMove = GetNextMove();
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
        private readonly int _inputSize = 8;
        private readonly int _layer1Size = 12;
        private readonly int _layer2Size = 12;
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

        public float[] Compute(params double[] inputs)
        {
            var inputValues = Matrix<double>.Build.DenseOfRowArrays(inputs);

            _layer1Values = Tanh(inputValues * _layer1Weights + _layer1Bias);
            _layer2Values = Tanh(_layer1Values * _layer2Weights + _layer2Bias);
            _layer3Values = Tanh(_layer2Values * _layer3Weights + _layer3Bias);

            return _layer3Values.ToColumnMajorArray().Select(i => (float)i).ToArray();
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
