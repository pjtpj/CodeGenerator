CodeGenerator is programmer's command line tool used to generate code. Any type of code or text file can be generated. I use it to generate .cs (C#), .aspx, .sql and other types of files. 

CodeGenerator is similar in concept to the CodeSmith product from http://www.codesmithtools.com/. Two big differences are 1) Source code is included and 2) It's free! 

Depending on the programmer and the requirements, code generators are fairly simple to program yet they can be very useful. This category of programmer's tools is so popular that CodeProject has an entire section devoted to it at http://www.codeproject.com/KB/codegen/. 

If you think about the way PHP (or Asp.Net) works, CodeGenerator is similar. In the simplest case, the web server copies the PHP source file from input (the file) to output (the TCP output socket). If there are any special PHP "tags" in the file, then the PHP code within the tags is extracted and sent to the the PHP interpreter. CodeGenerator works just like this except that instead of sending output to a TCP socket (or the console), the output is typically sent to a file.

CodeGenerator's tags are similar to the tags used by ASP or Asp.Net. The language within the tags is always C#. Like Asp.Net, CodeGenerator supports a "code behind" file. 

To generate code, first a "template" is created. Any file name or extension can be used for the template file, but typically, I give the template file the same extension as the output file will have except that I append a "t" to the file's extension. For example, if I am generating C# source code, I would name the template MyTemplate.cst.

A .xml "batch file" can be used to supply input variables, name the output file(s) and otherwise control the code generation process. 

No GUI application is supposed or desired. I am a big fan of repeatable processes, so I only invoke my code generators via batch files that can be run by anybody with access to the source code.

CodeGenerator includes a small class library (in MysqlDatabaseSchema.cs) that can be used by template code to iterate MySQL database schemas. This capability is used by many of my projects to generate database access code. Because of the dependency on MySQL, to successfully compile CodeGenerator, you will need to first download and install MySQL Connector.Net from http://dev.mysql.com/downloads/connector/net/. I used MySQL Connector.Net Version 5.1.3.0. As the API is mostly set by Microsoft via Ado.Net, probably any modern version of MySQL Connector.Net will work fine if you fix the references.

The best way to understand CodeGenerator is to build the project and run some of the samples under the debugger. I have supplied example .xml batch files to use with the example templates. 

In projects that use generated code, I typically setup two folders: 

- GeneratedCode is used as the output folder for code that is is to be directly included in the project. This code will be regenerated automatically. Code in this folder will be overwritten by newly generated code and should not be modified.

- GeneratedExamples (optional) is used as the output folder for code that is not directly included in the project. Typically, these files and/or the code within will be manually copied and pasted into the project. Obviously, code manually copied from this folder and pasted into the project won't get automatically updated, so use it with caution. 
