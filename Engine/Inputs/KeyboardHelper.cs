using Silk.NET.Input;

namespace OssianForge.Engine.Inputs
{
    public static class KeyboardHelper
    {
        public static char GetCharFromKey(Key key, bool shiftPressed)
        {
            switch (key)
            {
                // Alphabet keys
                case Key.A: return shiftPressed ? 'A' : 'a';
                case Key.B: return shiftPressed ? 'B' : 'b';
                case Key.C: return shiftPressed ? 'C' : 'c';
                case Key.D: return shiftPressed ? 'D' : 'd';
                case Key.E: return shiftPressed ? 'E' : 'e';
                case Key.F: return shiftPressed ? 'F' : 'f';
                case Key.G: return shiftPressed ? 'G' : 'g';
                case Key.H: return shiftPressed ? 'H' : 'h';
                case Key.I: return shiftPressed ? 'I' : 'i';
                case Key.J: return shiftPressed ? 'J' : 'j';
                case Key.K: return shiftPressed ? 'K' : 'k';
                case Key.L: return shiftPressed ? 'L' : 'l';
                case Key.M: return shiftPressed ? 'M' : 'm';
                case Key.N: return shiftPressed ? 'N' : 'n';
                case Key.O: return shiftPressed ? 'O' : 'o';
                case Key.P: return shiftPressed ? 'P' : 'p';
                case Key.Q: return shiftPressed ? 'Q' : 'q';
                case Key.R: return shiftPressed ? 'R' : 'r';
                case Key.S: return shiftPressed ? 'S' : 's';
                case Key.T: return shiftPressed ? 'T' : 't';
                case Key.U: return shiftPressed ? 'U' : 'u';
                case Key.V: return shiftPressed ? 'V' : 'v';
                case Key.W: return shiftPressed ? 'W' : 'w';
                case Key.X: return shiftPressed ? 'X' : 'x';
                case Key.Y: return shiftPressed ? 'Y' : 'y';
                case Key.Z: return shiftPressed ? 'Z' : 'z';

                // Number keys
                /*
                case Key.D0: return shiftPressed ? ')' : '0';
                case Key.D1: return shiftPressed ? '!' : '1';
                case Key.D2: return shiftPressed ? '@' : '2';
                case Key.D3: return shiftPressed ? '#' : '3';
                case Key.D4: return shiftPressed ? '$' : '4';
                case Key.D5: return shiftPressed ? '%' : '5';
                case Key.D6: return shiftPressed ? '^' : '6';
                case Key.D7: return shiftPressed ? '&' : '7';
                case Key.D8: return shiftPressed ? '*' : '8';
                case Key.D9: return shiftPressed ? '(' : '9';

                // Symbol keys
                case Key.OemTilde: return shiftPressed ? '~' : '`';
                case Key.OemSemicolon: return shiftPressed ? ':' : ';';
                case Key.OemQuotes: return shiftPressed ? '"' : '\'';
                case Key.OemOpenBrackets: return shiftPressed ? '{' : '[';
                case Key.OemCloseBrackets: return shiftPressed ? '}' : ']';
                case Key.OemMinus: return shiftPressed ? '_' : '-';
                case Key.OemComma: return shiftPressed ? '<' : ',';
                case Key.Space: return ' ';
                case Key.Enter: return '\n';
                case Key.Tab: return '\t';
                case Key.Back: return (char)8; // Backspace
                case Key.Delete: return (char)127; // Delete
                */
                // Ignore other keys
                default: return '\0';
            }
        }
    }
}
