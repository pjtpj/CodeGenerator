namespace CommandLine
{
    using System;
    using System.Text;
    using System.Runtime.InteropServices;
    
    /// <summary>
    /// A delegate used in error reporting.
    /// </summary>
    public delegate void ErrorReporter(string message);

    /// <summary>
    /// Useful Stuff.
    /// </summary>
    public sealed class Utility
    {
        /// <summary>
        /// The System Defined new line string.
        /// </summary>
        public const string NewLine = "\r\n";
        
        /// <summary>
        /// Don't ever call this.
        /// </summary>
        private Utility() {}
        
        /// <summary>
        /// Parses Command Line Arguments. Displays usage message to Console.Out
        /// if /?, /help or invalid arguments are encounterd.
        /// Errors are output on Console.Error.
        /// Use CommandLineArgumentAttributes to control parsing behaviour.
        /// </summary>
        /// <param name="arguments"> The actual arguments. </param>
        /// <param name="destination"> The resulting parsed arguments. </param>
        /// <returns> true if no errors were detected. </returns>
        public static bool ParseCommandLineArgumentsWithUsage(string [] arguments, object destination)
        {
            if (Utility.ParseHelp(arguments) || !Utility.ParseCommandLineArguments(arguments, destination))
            {
                // error encountered in arguments. Display usage message
                System.Console.Write(Utility.CommandLineArgumentsUsage(destination.GetType()));
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// Parses Command Line Arguments. 
        /// Errors are output on Console.Error.
        /// Use CommandLineArgumentAttributes to control parsing behaviour.
        /// </summary>
        /// <param name="arguments"> The actual arguments. </param>
        /// <param name="destination"> The resulting parsed arguments. </param>
        /// <returns> true if no errors were detected. </returns>
        public static bool ParseCommandLineArguments(string [] arguments, object destination)
        {
            return ParseCommandLineArguments(arguments, destination, new ErrorReporter(Console.Error.WriteLine));
        }
        
        /// <summary>
        /// Parses Command Line Arguments. 
        /// Use CommandLineArgumentAttributes to control parsing behaviour.
        /// </summary>
        /// <param name="arguments"> The actual arguments. </param>
        /// <param name="destination"> The resulting parsed arguments. </param>
        /// <param name="reporter"> The destination for parse errors. </param>
        /// <returns> true if no errors were detected. </returns>
        public static bool ParseCommandLineArguments(string[] arguments, object destination, ErrorReporter reporter)
        {
            CommandLineArgumentParser parser = new CommandLineArgumentParser(destination.GetType(), reporter);
            return parser.Parse(arguments, destination);
        }

        private static void NullErrorReporter(string message) 
        { 
        } 

        private class HelpArgument 
        { 
            [CommandLineArgumentAttribute(CommandLineArgumentType.AtMostOnce, ShortName="?")] 
            public bool help = false; 
        } 

        /// <summary>
        /// Checks if a set of arguments asks for help.
        /// </summary>
        /// <param name="args"> Args to check for help. </param>
        /// <returns> Returns true if args contains /? or /help. </returns>
        public static bool ParseHelp(string[] args)
        {
            CommandLineArgumentParser helpParser = new CommandLineArgumentParser(typeof(HelpArgument), new ErrorReporter(NullErrorReporter));
            HelpArgument helpArgument = new HelpArgument();
            helpParser.Parse(args, helpArgument);
            return helpArgument.help;
        }


        /// <summary>
        /// Returns a Usage string for command line argument parsing.
        /// Use CommandLineArgumentAttributes to control parsing behaviour.
        /// Formats the output to the width of the current console window.
        /// </summary>
        /// <param name="argumentType"> The type of the arguments to display usage for. </param>
        /// <returns> Printable string containing a user friendly description of command line arguments. </returns>
        public static string CommandLineArgumentsUsage(Type argumentType)
        {
            int screenWidth = Utility.GetConsoleWindowWidth();
            if (screenWidth == 0)
                screenWidth = 80;
            return CommandLineArgumentsUsage(argumentType, screenWidth);
        }

        /// <summary>
        /// Returns a Usage string for command line argument parsing.
        /// Use CommandLineArgumentAttributes to control parsing behaviour.
        /// </summary>
        /// <param name="argumentType"> The type of the arguments to display usage for. </param>
        /// <param name="columns"> The number of columns to format the output to. </param>
        /// <returns> Printable string containing a user friendly description of command line arguments. </returns>
        public static string CommandLineArgumentsUsage(Type argumentType, int columns)
        {
            return (new CommandLineArgumentParser(argumentType, null)).GetUsageString(columns);
        }

        private const int STD_OUTPUT_HANDLE  = -11;

        private struct COORD
        {
            internal Int16 x;
            internal Int16 y;
        }

        private struct SMALL_RECT
        {
            internal Int16 Left;
            internal Int16 Top;
            internal Int16 Right;
            internal Int16 Bottom;
        }

        private struct CONSOLE_SCREEN_BUFFER_INFO
        {
            internal COORD dwSize;
            internal COORD dwCursorPosition;
            internal Int16 wAttributes;
            internal SMALL_RECT srWindow;
            internal COORD dwMaximumWindowSize;
        }

        [DllImport("kernel32.dll", EntryPoint="GetStdHandle", SetLastError=true, CharSet=CharSet.Auto, CallingConvention=CallingConvention.StdCall)]
        private static extern int GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", EntryPoint="GetConsoleScreenBufferInfo", SetLastError=true, CharSet=CharSet.Auto, CallingConvention=CallingConvention.StdCall)]
        private static extern int GetConsoleScreenBufferInfo(int hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        /// <summary>
        /// Returns the number of columns in the current console window
        /// </summary>
        /// <returns>Returns the number of columns in the current console window</returns>
        public static int GetConsoleWindowWidth()
        {
            int screenWidth;
            CONSOLE_SCREEN_BUFFER_INFO csbi = new CONSOLE_SCREEN_BUFFER_INFO();
			csbi.dwSize.x = 0;
			csbi.dwSize.y = 0;
			csbi.dwCursorPosition.x = 0;
			csbi.dwCursorPosition.y = 0;
			csbi.wAttributes = 0;
			csbi.srWindow.Left = 0;
			csbi.srWindow.Top = 0;
			csbi.srWindow.Right = 0;
			csbi.srWindow.Bottom = 0;
			csbi.dwMaximumWindowSize.x = 0;
			csbi.dwMaximumWindowSize.y = 0;

            int rc;
            rc = GetConsoleScreenBufferInfo(GetStdHandle(STD_OUTPUT_HANDLE), ref csbi);
            screenWidth = csbi.dwSize.x;
            return screenWidth;
        }
        
        /// <summary>
        /// Searches a StringBuilder for a character
        /// </summary>
        /// <param name="text"> The text to search. </param>
        /// <param name="value"> The character value to search for. </param>
        /// <param name="startIndex"> The index to stat searching at. </param>
        /// <returns> The index of the first occurence of value or -1 if it is not found. </returns>
        public static int IndexOf(StringBuilder text, char value, int startIndex)
        {
            for (int index = startIndex; index < text.Length; index++)
            {
                if (text[index] == value)
                    return index;
            }

            return -1;
        }

        /// <summary>
        /// Searches a StringBuilder for a character in reverse
        /// </summary>
        /// <param name="text"> The text to search. </param>
        /// <param name="value"> The character to search for. </param>
        /// <param name="startIndex"> The index to start the search at. </param>
        /// <returns>The index of the last occurence of value in text or -1 if it is not found. </returns>
        public static int LastIndexOf(StringBuilder text, char value, int startIndex)
        {
            for (int index = Math.Min(startIndex, text.Length - 1); index >= 0; index --)
            {
                if (text[index] == value)
                    return index;
            }
            
            return -1;
        }
    }
}