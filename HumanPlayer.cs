using System;
using System.Threading;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Snakexperiment
{
    public class HumanPlayer : IPlayerController
    {
        private readonly object _inputSync;
        private PlayerMovement _desiredMove;
        private Thread _inputPollThread;
        private bool _pollInput;

        public HumanPlayer()
        {
            _inputSync = new object();
        }

        public bool IsHuman { get; } = true;

        public void Initialize(SnakeGame snakeGame)
        {
            _desiredMove = PlayerMovement.Right;
            _pollInput = true;
            _inputPollThread = new Thread(new ParameterizedThreadStart(PollInput));
            _inputPollThread.Start(snakeGame);
        }

        public PlayerMovement GetMovement()
        {
            lock (_inputSync)
            {
                return _desiredMove;
            }
        }

        public void Shutdown()
        {
            _pollInput = false;
            _inputPollThread.Join();
        }

        private void PollInput(object parameter)
        {
            SnakeGame snakeGame = parameter as SnakeGame;

            // Poll input at a rate of ~200fps, detached from the game update or frame display loop

            while (_pollInput)
            {
                lock (_inputSync)
                {
                    var keyState = Keyboard.GetState();
                    if (snakeGame.IsLegalMove(PlayerMovement.Up)
                        && (keyState.IsKeyDown(Keys.W) || keyState.IsKeyDown(Keys.Up)))
                    {
                        _desiredMove = PlayerMovement.Up;
                    }
                    else if (snakeGame.IsLegalMove(PlayerMovement.Down)
                        && (keyState.IsKeyDown(Keys.S) || keyState.IsKeyDown(Keys.Down)))
                    {
                        _desiredMove = PlayerMovement.Down;
                    }
                    else if (snakeGame.IsLegalMove(PlayerMovement.Left)
                        && (keyState.IsKeyDown(Keys.A) || keyState.IsKeyDown(Keys.Left)))
                    {
                        _desiredMove = PlayerMovement.Left;
                    }
                    else if (snakeGame.IsLegalMove(PlayerMovement.Right)
                        && (keyState.IsKeyDown(Keys.D) || keyState.IsKeyDown(Keys.Right)))
                    {
                        _desiredMove = PlayerMovement.Right;
                    }
                }

                Thread.Sleep(5);
            }
        }
    }
}
