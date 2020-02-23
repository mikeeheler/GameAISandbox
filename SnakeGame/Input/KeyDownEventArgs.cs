using System;
using Microsoft.Xna.Framework.Input;

namespace SnakeGame
{
    public class KeyDownEventArgs : EventArgs
    {
        public KeyDownEventArgs(Keys key)
        {
            Key = key;
        }

        public Keys Key { get; }
    }
}
