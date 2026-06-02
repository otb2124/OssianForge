using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System;

namespace OssianForge.Engine.Utils.Console
{

    public static class ConsoleUtils
    {
        [DllImport("kernel32.dll")]
        private static extern nint GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;

        public static void SetPosition(int x, int y)
        {
            nint consoleHandle = GetConsoleWindow();
            if (consoleHandle != nint.Zero)
            {
                SetWindowPos(consoleHandle, nint.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
            }
        }
    }
}
