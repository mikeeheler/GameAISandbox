using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;

namespace SnakeGame
{
    public class SnakeAIPopulation
    {
        private readonly List<PlayerGame> _players;

        public SnakeAIPopulation(
            int gamesPerGeneration,
            double mutationRate,
            int populationSize)
        {
            GamesPerGeneration = gamesPerGeneration;
            MutationRate = mutationRate;
            PopulationSize = populationSize;

            _players = new List<PlayerGame>(populationSize);
        }

        public int GamesPerGeneration { get; }
        public double MutationRate { get; }
        public int PopulationSize { get; }

        public void Initialize(SnakeEngine engine)
        {
            _players.Clear();
            _players.AddRange(Enumerable.Range(0, PopulationSize)
                .Select(_ => new PlayerGame
                {
                    Player = new AIPlayer(),
                    Instance = engine.CreateGame()
                }));
        }

        private class PlayerGame
        {
            public AIPlayer Player { get; set; }
            public SnakeGameSim Instance { get; set; }
        }
    }
}
