Command Line Argument Parser
----------------------------

Author: peterhal@microsoft.com

Parsing command line arguments to a console application is a common problem. 
This library handles the common task of reading arguments from a command line 
and filling in the values in a type.

To use this library, define a class whose fields represent the data that your 
application wants to receive from arguments on the command line. Then call 
Utilities.Utility.ParseCommandLineArguments() to fill the object with the data 
from the command line. Each field in the class defines a command line argument. 
The type of the field is used to validate the data read from the command line. 
The name of the field defines the name of the command line option.

The parser can handle fields of the following types:

- string
- int
- uint
- bool
- enum
- array of the above type

For example, suppose you want to read in the argument list for wc (word count). 
wc takes three optional boolean arguments: -l, -w, and -c and a list of files.

You could parse these arguments using the following code:

class WCArguments
{
    public bool lines;
    public bool words;
    public bool chars;
    public string[] files;
}

class WC
{
    static void Main(string[] args)
    {
        if (Utility.ParseCommandLineArgumentsWithUsage(args, parsedArgs))
        {
            // insert application code here
        }
    }
}

So you could call this aplication with the following command line to count 
lines in the foo and bar files:

    wc.exe /lines /files:foo /files:bar

The program will display the following usage message when bad command line 
arguments are used:

    wc.exe -x

Unrecognized command line argument '-x'
    /lines[+|-]                         short form /l
    /words[+|-]                         short form /w
    /chars[+|-]                         short form /c
    /files:<string>                     short form /f
    @<file>                             Read response file for more options

That was pretty easy. However, you realy want to omit the "/files:" for the 
list of files. The details of field parsing can be controled using custom 
attributes. The attributes which control parsing behaviour are:

CommandLineArgumentAttribute 
    - controls short name, long name, required, allow duplicates and help text
DefaultCommandLineArgumentAttribute 
    - allows omition of the "/name".
    - This attribute is allowed on only one field in the argument class.

So for the wc.exe program we want this:

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
        }
    }
}



So now we have the command line we want:

    wc.exe /lines foo bar

This will set lines to true and will set files to an array containing the 
strings "foo" and "bar".

The new usage message becomes:

    wc.exe -x

Unrecognized command line argument '-x'
/lines[+|-]  Count number of lines in the input text. (short form /l)
/words[+|-]  Count number of words in the input text. (short form /w)
/chars[+|-]  Count number of chars in the input text. (short form /c)
@<file>      Read response file for more options
<files>      Input files to count. (short form /f)

If you want more control over how error messages are reported, how /help is 
dealt with, etc you can go directly to the CommandLineArgumentParser
class.

    

Cheers,
Peter Hallam
C# Compiler Developer
Microsoft Corp.




Release Notes
-------------

10/02/2002 Initial Release
10/14/2002 Bug Fix
01/08/2003 Bug Fix in @ include files
10/23/2004 Added user specified help text, formatting of help text to 
           screen width. Added ParseHelp for /?.