using System;
using Microsoft.Xna.Framework.Input;

namespace SnakeGame
{
    public class KeyUpEventArgs : EventArgs
    {
        public KeyUpEventArgs(Keys keys)
        {
            Key = keys;
        }

        public Keys Key { get; }
    }
}
