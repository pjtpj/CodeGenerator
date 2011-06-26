using System;
using Utilities;

class WCArguments
{
    [CommandLineArgument(CommandLineArgumentType.AtMostOnce, HelpText="Count number of lines in the input text.")]
    public bool lines;
    [CommandLineArgument(CommandLineArgumentType.AtMostOnce, HelpText="Count number of words in the input text.")]
    public bool words;
    [CommandLineArgument(CommandLineArgumentType.AtMostOnce, HelpText="Count number of chars in the input text.")]
    public bool chars;
    [DefaultCommandLineArgument(CommandLineArgumentType.MultipleUnique, HelpText="Input files to count.")]
    public string[] files;
}

class WC
{
    static void Main(string[] args)
    {
        WCArguments parsedArgs = new WCArguments();
        if (Utility.ParseCommandLineArgumentsWithUsage(args, parsedArgs))
        {
            // insert application code here
            Console.WriteLine(parsedArgs.lines);
            Console.WriteLine(parsedArgs.words);
            Console.WriteLine(parsedArgs.chars);
            Console.WriteLine(parsedArgs.files.Length);
        }
    }
}


