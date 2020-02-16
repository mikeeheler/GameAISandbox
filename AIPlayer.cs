using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;

namespace Snakexperiment
{
    public class AIPlayer : IPlayerController
    {
        public AIPlayer()
        {
        }

        public PlayerMovement GetMovement()
        {
            throw new System.NotImplementedException();
        }

        public void Initialize(SnakeGame snakeGame)
        {
            var baseLayer = new NeuralLayer(snakeGame.FieldHeight * snakeGame.FieldWidth + 1, 1.0f);
            var outputs = new NeuralLayer(3, 0.0f);

            baseLayer.LinkTo(outputs);

            throw new System.NotImplementedException();
        }

        public void Shutdown()
        {
            throw new System.NotImplementedException();
        }

        public void Update(GameTime gameTime)
        {
            throw new System.NotImplementedException();
        }
    }

    public class Synapse
    {
        private readonly List<(Synapse, float)> _inputs;
        private readonly NeuralLayer _owner;

        public Synapse(NeuralLayer owner, float initialValue)
        {
            _inputs = new List<(Synapse, float)>();
            _owner = owner;
            Value = initialValue;
        }

        public IReadOnlyCollection<(Synapse, float)> Inputs => _inputs;
        public float Value { get; private set; }

        public void ClearInputs()
        {
            _inputs.Clear();
        }

        public void Compute(float value)
        {
            Value = (float)(1.0 / (1.0 + Math.Pow(MathHelper.E, value)));
        }

        public void LinkTo(Synapse other, float weight)
        {
            if (other._owner == _owner)
                throw new ArgumentException(nameof(other));

            other._inputs.Add((this, weight));
        }

        public void SetValue(float value)
        {
            Value = value;
        }
    }

    public class NeuralLayer
    {
        private readonly List<Synapse> _synapses;
        private NeuralLayer _nextLayer;
        private NeuralLayer _previousLayer;

        public NeuralLayer(int synapseCount, float bias)
        {
            _synapses = new List<Synapse>(synapseCount + 1);
            for (int i = 0; i < synapseCount; ++i)
                _synapses.Add(new Synapse(this, 0.0f));
            _synapses.Add(new Synapse(this, bias));
        }

        public void Compute()
        {
            if (_previousLayer != null)
            {
                foreach (var synapse in _synapses)
                {
                    float sum = 0.0f;
                    foreach (var inputSynapse in synapse.Inputs)
                    {
                        sum += inputSynapse.Item1.Value * inputSynapse.Item2;
                    }
                    synapse.Compute(sum);
                }
            }
            _nextLayer.Compute();
        }

        public void LinkTo(NeuralLayer nextLayer)
        {
            if (_nextLayer != null)
            {
                foreach (var nextSynapse in _nextLayer._synapses)
                    nextSynapse.ClearInputs();
                nextLayer.LinkTo(_nextLayer);
            }

            _nextLayer = nextLayer;
            nextLayer._previousLayer = this;

            foreach (var nextSynapse in _nextLayer._synapses)
                foreach (var synapse in _synapses)
                    synapse.LinkTo(nextSynapse, 1.0f);
        }
    }
}
