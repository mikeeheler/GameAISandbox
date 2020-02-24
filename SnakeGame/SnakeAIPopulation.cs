using System;
using System.Collections.Generic;
using System.Linq;

using MathNet.Numerics.Random;

namespace SnakeGame
{
    public class SnakeAIPopulation
    {
        private readonly List<PlayerScore> _playerScores;
        private readonly RandomSource _rng;

        public SnakeAIPopulation(int gamesPerGeneration, int populationSize)
        {
            GamesPerGeneration = gamesPerGeneration;
            PopulationSize = populationSize;

            _playerScores = new List<PlayerScore>(populationSize);
            _rng = SnakeRandom.Default;
        }

        public int GamesPerGeneration { get; }
        public int PopulationSize { get; }

        public AIPlayer BestPlayer { get; private set; }
        public int BestScore { get; private set; }

        public (AIPlayer Player, int Score)[] GetPlayers()
            => _playerScores.Select(playerScore => (playerScore.Player.Clone(), playerScore.Score)).ToArray();

        public void Initialize()
        {
            _playerScores.Clear();
            _playerScores.AddRange(Enumerable.Range(0, PopulationSize)
                .Select(_ => CreatePlayerScore()));

            BestPlayer = _playerScores[0].Player;
            BestScore = 0;
        }

        public void RankAndMutate()
        {
            var newPopulation = new List<PlayerScore>(PopulationSize);
            PlayerScore[] sorted = _playerScores.OrderByDescending(x => x.Score).ToArray();
            double upperBound = (double)(PopulationSize - 1) / PopulationSize;
            int totalSum = 0;
            for (int i = 0; i < sorted.Length; i++)
            {
                double killProbability = (double)i / sorted.Length;
                if (_rng.NextDouble() * upperBound < killProbability)
                {
                    newPopulation.Add(sorted[i]);
                    totalSum += sorted[i].Score;
                }
            }

            List<PlayerScore> parents = newPopulation.ToList();

            while (newPopulation.Count < PopulationSize)
            {
                int index = 0;
                int sum = 0;
                int roll = _rng.Next() % totalSum;
                while (sum <= roll && index < parents.Count)
                    sum += parents[index++].Score;
                var child = new PlayerScore
                {
                    Player = parents[--index].Player.Clone(),
                    Score = 0
                };
                double mutationRate = (_rng.NextDouble() * 0.15) + 0.1;
                child.Player.Mutate(mutationRate);
                newPopulation.Add(child);
            }

            _playerScores.Clear();
            _playerScores.AddRange(newPopulation.OrderBy(_ => _rng.Next()));
        }

        public void Run(SnakeEngine engine)
        {
            const int GENERATIONS_TO_SIM = 100;
            var results = new List<PlayerGameSet>(_playerScores.Count);
            for (int iter = 0; iter < GENERATIONS_TO_SIM; ++iter)
            {
                foreach (var playerScore in _playerScores)
                {
                    var player = playerScore.Player;
                    playerScore.Score = 0;

                    var gameSet = new PlayerGameSet
                    {
                        Player = playerScore.Player,
                        Games = Enumerable.Range(0, GamesPerGeneration)
                            .Select(_ => engine.CreateGame())
                            .ToList()
                    };

                    foreach (SnakeGameSim game in gameSet.Games)
                    {
                        while (game.Alive && game.TurnsSinceEating < game.GameRules.MaxAITurns)
                        {
                            game.Move(player.GetMovement(game));
                        }
                        playerScore.Score += game.ApplesEaten;

                        if (playerScore.Score > BestScore)
                        {
                            BestPlayer = player;
                            BestScore = playerScore.Score;
                        }
                    }
                }

                if (iter < (GENERATIONS_TO_SIM - 1))
                    RankAndMutate();
            }
        }

        private PlayerScore CreatePlayerScore()
        {
            var result = new PlayerScore
            {
                Player = new AIPlayer(),
                Score = 0
            };
            result.Player.Initialize();
            return result;
        }

        private class PlayerScore
        {
            public AIPlayer Player { get; set; }
            public int Score { get; set; }
        }

        private class PlayerGameSet
        {
            public AIPlayer Player { get; set; }
            public ICollection<SnakeGameSim> Games { get; set; }
        }
    }
}
