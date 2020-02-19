using System;
using System.Diagnostics;
using System.Linq;

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;
using Microsoft.Xna.Framework;

namespace SnakeGame
{
    public class AIBrain
    {
        private readonly RandomSource _random = MersenneTwister.Default;

        private readonly int _inputSize;
        private readonly int _hiddenSize;
        private readonly int _outputSize;

        private readonly Matrix<double> _inputBias;
        private readonly Matrix<double> _hiddenBias;
        private readonly Matrix<double> _inputWeights;
        private readonly Matrix<double> _hiddenWeights;

        private Matrix<double> _inputValues;
        private Matrix<double> _hiddenValues;
        private Matrix<double> _outputValues;

        public AIBrain(int inputSize, int hiddenSize, int outputSize)
        {
            Debug.Assert(inputSize > 0);
            Debug.Assert(hiddenSize > 0);
            Debug.Assert(outputSize > 0);

            _inputSize = inputSize;
            _hiddenSize = hiddenSize;
            _outputSize = outputSize;
            // mathnet indices: row,col
            // mathnet mem: col maj

            BrainType = AIBrainType.OneOfGodsOwnPrototypes;

            // TODO find a uniform distribution for Matrix<double>.Random that gives results -1 to 1
            //      -- it'll probably be faster
            _inputBias = Matrix<double>.Build.Dense(
                1,
                _hiddenSize,
                (row, col) => _random.NextDouble() * 2.0 - 1.0);
            _hiddenBias = Matrix<double>.Build.Dense(
                1,
                _outputSize,
                (row, col) => _random.NextDouble() * 2.0 - 1.0);
            _inputWeights = Matrix<double>.Build.Dense(
                _inputSize,
                _hiddenSize,
                (row, col) => _random.NextDouble() * 2.0 - 1.0);
            _hiddenWeights = Matrix<double>.Build.Dense(
                _hiddenSize,
                _outputSize,
                (row, col) => _random.NextDouble() * 2.0 - 1.0);
        }

        private AIBrain(AIBrain other)
        {
            BrainType = AIBrainType.MutatedClone;

            _inputSize = other._inputSize;
            _hiddenSize = other._hiddenSize;
            _outputSize = other._outputSize;

            _inputBias = other._inputBias.Clone();
            _hiddenBias = other._hiddenBias.Clone();
            _inputWeights = other._inputWeights.Clone();
            _hiddenWeights = other._hiddenWeights.Clone();
            _inputValues = other._inputValues?.Clone();
            _hiddenValues = other._hiddenValues?.Clone();
            _outputValues = other._outputValues?.Clone();
        }

        private AIBrain(AIBrain left, AIBrain right, AIBreedingMode breedingMode)
        {
            Debug.Assert(left._inputSize == right._inputSize);
            Debug.Assert(left._hiddenSize == right._hiddenSize);
            Debug.Assert(left._outputSize == right._outputSize);

            _inputSize = left._inputSize;
            _hiddenSize = left._hiddenSize;
            _outputSize = left._outputSize;

            switch (breedingMode)
            {
                // Half of both parents genes are present in every one of the child's genes
                case AIBreedingMode.Coalesce:
                    BrainType = AIBrainType.DescendentCoalesced;
                    _inputBias = left._inputBias * 0.5 + right._inputBias * 0.5;
                    _hiddenBias = left._hiddenBias * 0.5 + right._hiddenBias * 0.5;
                    _inputWeights = left._inputWeights * 0.5 + right._inputWeights * 0.5;
                    _hiddenWeights = left._hiddenWeights * 0.5 + right._hiddenWeights * 0.5;
                    break;

                // Half of parent 1's genes and half of parent 2's genes
                case AIBreedingMode.Mix:
                    BrainType = AIBrainType.DescendentMixed;
                    _inputBias = MixMatrices(left._inputBias, right._inputBias);
                    _hiddenBias = MixMatrices(left._hiddenBias, right._hiddenBias);
                    _inputWeights = MixMatrices(left._inputWeights, right._inputWeights);
                    _hiddenWeights = MixMatrices(left._hiddenWeights, right._hiddenWeights);
                    break;

                default: throw new ArgumentOutOfRangeException(nameof(breedingMode));
            }
        }

        public AIBrainType BrainType { get; }

        public AIBrain Clone()
            => new AIBrain(this);

        public float[] Compute(params double[] inputs)
        {
            Debug.Assert(inputs.Length == _inputSize);

            _inputValues = Matrix<double>.Build.DenseOfRowArrays(inputs);

            _hiddenValues = LeakyReLU(_inputValues * _inputWeights + _inputBias);
            _outputValues = LeakyReLU(_hiddenValues * _hiddenWeights + _hiddenBias);

            return _outputValues.ToColumnMajorArray().Select(i => (float)i).ToArray();
        }

        public Matrix<double>[] GetValues()
            => new[] { _inputValues?.Clone(), _hiddenValues?.Clone(), _outputValues?.Clone() };

        public Matrix<double>[] GetWeights()
            => new[] { _inputWeights.Clone(), _hiddenWeights.Clone() };

        public AIBrain MergeWith(AIBrain other, AIBreedingMode breedingMode)
            => new AIBrain(this, other, breedingMode);

        public void Mutate(double mutationRate)
        {
            double randomRate = _random.NextDouble() * mutationRate;
            Matrix<double>[] mutateMatrices =
            {
                _inputBias, _hiddenBias,
                _inputWeights, _hiddenWeights
            };
            for (int matrixIndex = 0; matrixIndex < mutateMatrices.Length; ++matrixIndex)
            {
                Matrix<double> mutateMatrix = mutateMatrices[matrixIndex];

                for (int row = 0; row < mutateMatrix.RowCount; ++row)
                {
                    for (int col = 0; col < mutateMatrix.ColumnCount; ++col)
                    {
                        if (_random.NextDouble() < randomRate)
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

        // DNA shuffle. On average, 50% of the genes from the left and 50% of the genes from the right go into
        // the result.
        private Matrix<double> MixMatrices(Matrix<double> left, Matrix<double> right)
        {
            Debug.Assert(left.ColumnCount == right.ColumnCount);
            Debug.Assert(left.RowCount == right.RowCount);
            return Matrix<double>.Build.Dense(
                left.RowCount,
                left.ColumnCount,
                (row, col) => _random.NextDouble() < 0.5 ? left[row,col] : right[row,col]);
        }

        // Hyperbolic tangent function returns an S-shaped curve from -1 to 1 for inputs in the range -1 to 1
        // with 0 mapping to 0 on the curve.
        // Beyond these bounds it's clamped to either -1 or 1.
        private Matrix<double> Tanh(Matrix<double> input)
        {
            return input.PointwiseTanh();
        }

        // The logistic function returns an S-shaped curve from 0 to 1 for inputs approximately in the range -6 to 6
        // with 0 mapping to 0.5 on the curve.
        // Beyond these bounds it's effectively clamped at either 0 or 1
        private Matrix<double> LogisticSigmoid(Matrix<double> input)
        {
            return Matrix<double>.Build.Dense(
                input.RowCount,
                input.ColumnCount,
                (row, col) => 1.0 / (1.0 + Math.Pow(MathHelper.E, -input[row,col])));
        }

        // "Rectified Linear Unit"
        // Aka max(0, x)
        private Matrix<double> ReLU(Matrix<double> input)
        {
            return input.PointwiseMaximum(0.0);
        }

        // "Leaky" ReLU allows negative weights, but strongly muted so that only the
        // strongest values leak through.
        private Matrix<double> LeakyReLU(Matrix<double> input)
        {
            return Matrix<double>.Build.Dense(
                input.RowCount,
                input.ColumnCount,
                (row, col) => input[row,col] < 0.0 ? 0.01 * input[row,col] : input[row,col]);
        }
    }
}
