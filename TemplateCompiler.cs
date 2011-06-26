// Copyright (C) 2006 Teztech, Inc. All rights reserved.

using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;
using Microsoft.CSharp;
using Microsoft.VisualBasic;

namespace CodeGenerator
{
	/// <summary>
	/// Compiles a template into a binary Assembly. <see cref="TemplateRunner"/> to run
	/// the compiled template.
	/// </summary>
	class TemplateCompiler
	{
		// Settings ------------

		public static string CodeGeneratorNamespace = "CodeGenerator";
		public static string CodeGeneratorMethodName = "Generate"; // See constructor
		public static string ResultStringBuilderName = "generatedCode";

		public bool Debug = false;

		// Public Interface ----

		public TemplateCompiler() { }

		public TemplateRunner CompileTemplate(string template, bool debug)
		{
			// So Each CodeGenerator class must have a unique class name so that
			// we can load them all into memory at once
			CompilerRuns++;
			CodeGeneratorClassName = string.Format("Generator{0}", CompilerRuns);

			Debug = debug;
			ParseTemplate(template);
			Assembly codeBehindAssembly = null;
			TemplateRunner runner = new TemplateRunner();
			if (Src != "")
				codeBehindAssembly = ProcessCodeBehind(Src, runner);
			
			string templateAssemblyCode = GenerateAssemblyCode();
			runner.Assembly = CompileAssemblyCode(templateAssemblyCode, codeBehindAssembly, runner);
			runner.CodeGeneratorNamespace = CodeGeneratorNamespace;
			runner.CodeGeneratorClassName = CodeGeneratorClassName;
			runner.CodeGeneratorMethodName = CodeGeneratorMethodName;

			return runner;
		}

		// Data Model ----------

		// CodeTemplate directive attributes
		protected string Language       = "C#";
		protected string TargetLanguage = "C#";
		protected string Inherits       = "CodeGenerator.CodeTemplate";
		protected string Src            = ""; // No code behind

		protected static int CompilerRuns = 0;
		protected string CodeGeneratorClassName = "Generator"; // See constructor

		// Assemblie and Import directives
		protected List<string> Assemblies = new List<string>();
		protected List<string> Imports = new List<string>();

		// Property directive attributes
		protected class Property
		{
			public string Name = "";
			public string Type = "";

			public Property(string name, string type)
			{
				Name = name;
				Type = type;
			}
		}
		protected List<Property> Properties = new List<Property>();

		// Code expressions, statement blocks, and literals from the template
		protected abstract class StatementGenerator
		{
			public abstract CodeStatement GenerateStatement();
		}
		protected class DirectiveGenerator : StatementGenerator
		{
			public string Directive;
			public Dictionary<string, string> Attributes;

			public DirectiveGenerator(string directive, Dictionary<string, string> attributes)
			{
				Directive = directive;
				Attributes = attributes;
			}

			public override CodeStatement GenerateStatement()
			{
				// Just a placeholder
				return null;
			}
		}
		protected class CommentGenerator : StatementGenerator
		{
			public string Comment;

			public CommentGenerator(string comment)
			{
				Comment = comment;
			}

			public override CodeStatement GenerateStatement()
			{
				// Just a placeholder
				return null;
			}
		}
		protected class ExpressionGenerator : StatementGenerator
		{
			public string Text;

			public ExpressionGenerator(string text)
			{
				Text = text;
			}

			public override CodeStatement GenerateStatement()
			{
				CodeExpression textExpression   = new CodeSnippetExpression(Text);
				CodeExpression builderObject    = new CodeVariableReferenceExpression(ResultStringBuilderName);
				CodeExpression appendExpression = new CodeMethodInvokeExpression(builderObject, "Append", textExpression);
				return new CodeExpressionStatement(appendExpression);
			}
		}
		protected class BlockGenerator : StatementGenerator
		{
			public string Text;

			public BlockGenerator(string text)
			{
				Text = text;
			}

			public override CodeStatement GenerateStatement()
			{
				return new CodeSnippetStatement(Text);
			}
		}
		protected class LiteralGenerator : StatementGenerator
		{
			public string Text;

			public LiteralGenerator(string text)
			{
				Text = text;
			}

			public override CodeStatement GenerateStatement()
			{
				CodeExpression textExpression   = new CodePrimitiveExpression(Text);
				CodeExpression builderObject    = new CodeVariableReferenceExpression(ResultStringBuilderName);
				CodeExpression appendExpression = new CodeMethodInvokeExpression(builderObject, "Append", textExpression);
				return new CodeExpressionStatement(appendExpression);
			}
		}
		protected List<StatementGenerator> StatementGenerators = new List<StatementGenerator>();

		// Blocks of code to be inserted at the class member level due to <script> tags
		protected List<string> ClassMemberCodeBlocks = new List<string>();

		// CodeBehind files can be included in multiple templates, but they can only be complied once
		protected Dictionary<string, Assembly> CodeBehindAssemblies = new Dictionary<string, Assembly>();

		// Implementation -----------------------------------------------------

		/*
			This is a Regex based parser that works by scanning the input text for 
			the following patterns: 

				<%@ ... %>                                - Template Directive
				<%= ... %>                                - Expression in Language
				<% ... %>                                 - Block of statements in Language
				<%--... --%>                              - Template Comment
				<!-- #include file="CommonScript.cs" -->  - Include file processing
				<script ...> </script>                    - Code block
				% is escaped by % (so you can output "<% %>" by writing "<%% %%>" in your template)
		
			All other input text is literal text to be copied to the output

			Regex patterns are "compiled", so it helps performance a bit to make them static:
		*/
		protected static RegexOptions _regexOptions = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline;
		protected static Regex _tagRegex        = new Regex( @"(?<tag>(?:<%[^%]%>)|(?:<%[^%].*?[^%]%>))",            _regexOptions);
		protected static Regex _directiveRegex  = new Regex( @"<%@(?<directive>.*?[^%])%>",                          _regexOptions);
		protected static Regex _expressionRegex = new Regex( @"<%=(?<expression>.*?[^%])%>",                         _regexOptions);
		protected static Regex _blockRegex      = new Regex( @"<%(?!--)(?<block>(?:[^@]|[^=]|[^%])|(?:(?:[^@]|[^=]|[^%]).*?[^%]))%>",   _regexOptions);
		protected static Regex _commentRegex    = new Regex( @"<%--(?<comment>.*?)--%>",                             _regexOptions);
		protected static Regex _includeRegex    = new Regex( @"<!--\s+\#include\s+file=\""(?<file>[^\""]+)""\s+-->", _regexOptions);
		protected static Regex _scriptRegex     = new Regex( @"<script(?<attributes>.*?)>(?<script>.*?)</script>",   _regexOptions);

		protected static Regex _attributesParser = new Regex( @"\s*((?<name>.*?)\s*=\s*\""(?<value>.*?)\""\s*){0,}", _regexOptions);
		protected static Regex _directiveParser  = new Regex( @"\s*(?<directive>\S+)\s+((?<name>.*?)\s*=\s*\""(?<value>.*?)\""\s*){0,}", _regexOptions );

		protected void ParseTemplate(string template)
		{
			// Includes are expanded, just like a preprocessor
			template = ProcessIncludes(template);

			// Get <script> blocks with runat="template" out of the way
			template = ProcessScripts(template);

			// The text between tags is literal text
			int iStartLiteral = 0;
			foreach (Match tagMatch in _tagRegex.Matches(template))
			{
				if (tagMatch.Index > iStartLiteral)
				{
					string literalText = template.Substring(iStartLiteral, tagMatch.Index - iStartLiteral);
					ProcessLiteral(literalText);
				}

				string tagText = tagMatch.Groups["tag"].Value;

				if(_directiveRegex.IsMatch(tagText))
					ProcessDirective(tagText);
				else if(_commentRegex.IsMatch(tagText))
					ProcessComment(tagText);
				else if(_expressionRegex.IsMatch(tagText))
					ProcessExpression(tagText);
				else if(_blockRegex.IsMatch(tagText))
					ProcessBlock(tagText);
				else
					throw new ApplicationException(string.Format("Tag does not match any tag patterns: {0}", tagText.Substring(0, 60)));

				iStartLiteral = tagMatch.Index + tagMatch.Length;
			}

			if(iStartLiteral < template.Length)
			{
				string literalText = template.Substring(iStartLiteral, template.Length - iStartLiteral);
				ProcessLiteral(literalText);
			}

			OptimizeNewlines();
		}

		protected string GenerateAssemblyCode()
		{
			// Use CodeCompileUnit to create a CodeDom tree for the template. Once the tree is created,
			// we let the CodeDomProvider generate source code for the template's assembly

			// Module -----------------

			CodeCompileUnit compileUnit = new CodeCompileUnit();

			foreach (string assembly in Assemblies)
				compileUnit.ReferencedAssemblies.Add(assembly);

			CodeNamespace ns = new CodeNamespace(CodeGeneratorNamespace);
			compileUnit.Namespaces.Add(ns);
			ns.Imports.Add(new CodeNamespaceImport("System"));
			ns.Imports.Add(new CodeNamespaceImport("System.IO"));
			ns.Imports.Add(new CodeNamespaceImport("System.Text"));
			ns.Imports.Add(new CodeNamespaceImport("System.Collections.Generic"));
			foreach(string import in Imports)
				ns.Imports.Add(new CodeNamespaceImport(import));

			// Class ------------------

			CodeTypeDeclaration cls = new CodeTypeDeclaration(CodeGeneratorClassName);
			cls.BaseTypes.Add(new CodeTypeReference(Inherits));
			ns.Types.Add(cls);

			// Class constructor
			CodeConstructor defaultConstructor = new CodeConstructor();
			defaultConstructor.Attributes = MemberAttributes.Public;
			cls.Members.Add(defaultConstructor);

			// Class properties
			foreach (Property property in Properties)
			{
				string fieldName = "_" + property.Name.Substring(0, 1).ToLower() + property.Name.Substring(1);
				
				CodeMemberField memberField = new CodeMemberField(property.Type, fieldName);
				cls.Members.Add(memberField);

				CodeMemberProperty memberProperty = new CodeMemberProperty();
				memberProperty.Attributes = MemberAttributes.Public | MemberAttributes.Final;
				memberProperty.Type = new CodeTypeReference(property.Type);
				memberProperty.Name = property.Name;
				memberProperty.HasGet = true;
				memberProperty.HasSet = true;

				// get { return this.< insert fieldname here>; }
				CodeExpression fieldExpression = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), fieldName);
				memberProperty.GetStatements.Add(new CodeMethodReturnStatement(fieldExpression));

				// set { <field> = value; }
				memberProperty.SetStatements.Add(new CodeAssignStatement(fieldExpression, new CodePropertySetValueReferenceExpression()));

				cls.Members.Add(memberProperty);
			}

			// GenerateCode Method ----

			CodeMemberMethod generateCode = new CodeMemberMethod();
			generateCode.Name = CodeGeneratorMethodName;
			generateCode.Attributes = MemberAttributes.Public;
			generateCode.ReturnType = new CodeTypeReference("System.String");

			// StringBuilder generatedCode = new StringBuilder();
			CodeObjectCreateExpression resultNewExpression = new CodeObjectCreateExpression(
				"System.Text.StringBuilder", new CodeExpression[] { });
			CodeVariableDeclarationStatement resultVaribleStatement = new CodeVariableDeclarationStatement(
				typeof(StringBuilder), ResultStringBuilderName, resultNewExpression);
			generateCode.Statements.Add(resultVaribleStatement);

			foreach (StatementGenerator generator in StatementGenerators)
			{
				CodeStatement statement = generator.GenerateStatement();
				if(statement != null)
					generateCode.Statements.Add(statement);
			}

			CodeExpression builderObject      = new CodeVariableReferenceExpression(ResultStringBuilderName);
			CodeExpression toStringExpression = new CodeMethodInvokeExpression(builderObject, "ToString", new CodeExpression[]{});
			generateCode.Statements.Add(new CodeMethodReturnStatement(toStringExpression));

			cls.Members.Add(generateCode);

			// Add blocks of code due to <script> tags and code behind files
			foreach(string codeBlockText in ClassMemberCodeBlocks)
			{
				CodeSnippetTypeMember codeBlock = new CodeSnippetTypeMember(codeBlockText);
				cls.Members.Add(codeBlock);
			}

			// Generate code
			// See ms-help://MS.VSCC.v80/MS.MSDN.v80/MS.NETDEVFX.v20.en/cpref2/html/T_System_CodeDom_Compiler_CodeDomProvider.htm

			CodeDomProvider provider = CodeDomProvider.CreateProvider(Language);
			CodeGeneratorOptions generatorOptions = new CodeGeneratorOptions();
			StringWriter generatorWriter = new StringWriter();
			IndentedTextWriter generatorWriterI = new IndentedTextWriter(generatorWriter);
			provider.GenerateCodeFromCompileUnit(compileUnit, generatorWriterI, generatorOptions);

			return generatorWriter.ToString();
		}

		protected Assembly CompileAssemblyCode(string templateAssemblyCode, Assembly codeBehindAssembly, TemplateRunner runner)
		{
			CompilerParameters compilerParams = new CompilerParameters();
			compilerParams.GenerateExecutable = false;
			compilerParams.GenerateInMemory   = false;
			compilerParams.IncludeDebugInformation = true;
			compilerParams.OutputAssembly     = runner.TempFiles.AddExtension(string.Format("{0}.dll", codeBehindAssembly == null ? "" : "cba"), false);

			compilerParams.ReferencedAssemblies.Add("System.dll");
			compilerParams.ReferencedAssemblies.Add("mscorlib.dll");
			foreach (string assemblyName in Assemblies)
			{
				try
				{
					Assembly assembly = Assembly.LoadWithPartialName(assemblyName);
					compilerParams.ReferencedAssemblies.Add(assembly.Location);
				}
				catch
				{
					Assembly assembly = Assembly.LoadFrom(assemblyName);
					compilerParams.ReferencedAssemblies.Add(assembly.Location);
				}
			}
			compilerParams.ReferencedAssemblies.Add(GetType().Assembly.Location);
			if(codeBehindAssembly != null)
				compilerParams.ReferencedAssemblies.Add(codeBehindAssembly.Location);

			CodeDomProvider provider = CodeDomProvider.CreateProvider(Language);
			CompilerResults results = provider.CompileAssemblyFromSource(compilerParams, templateAssemblyCode);
			bool success = results.Errors.Count == 0;

			// BUGBUG: Move error messages to API

			// Emit source code with line numbers if build fails
			if(Debug || !success)
			{
				using (StringReader reader = new StringReader(templateAssemblyCode))
				{
					int lineNumber = 1;
					string lineText;
	                while ((lineText = reader.ReadLine()) != null) 
					{
						Console.Error.WriteLine("{0,6}: {1}", lineNumber.ToString("######"), lineText);
						lineNumber++;
					}
				}
			}

			foreach(CompilerError error in results.Errors)
				Console.Error.WriteLine("{0}({1}, {2}): error {3}: {4}", error.FileName, error.Line, error.Column, error.ErrorNumber, error.ErrorText);

			Console.Error.WriteLine("Build {0}: {1} Errors, 0 Warnings", !success ? "Failed" : "Succeeded", results.Errors.Count);

			/* if (success)
				Assembly.LoadFrom(results.CompiledAssembly.Location); */

			return success ? results.CompiledAssembly : null;
		}

		protected string ProcessIncludes(string template)
		{
			Match includeMatch = _includeRegex.Match(template);
			while (includeMatch.Success)
			{
				string includeFile = includeMatch.Groups["file"].Value;
				StreamReader includeReader = new StreamReader(includeFile);
				string includeText = includeReader.ReadToEnd();
				includeText = ProcessIncludes(includeText);
				template = template.Remove(includeMatch.Index, includeMatch.Length);
				template = template.Insert(includeMatch.Index, includeText);

				includeMatch = _includeRegex.Match(template);
			}

			return template;
		}

		protected string ProcessScripts(string template)
		{
			Match scriptMatch = _scriptRegex.Match(template);
			while (scriptMatch.Success)
			{
				string attributesText = scriptMatch.Groups["attributes"].Value;
				Match attributesMatch = _attributesParser.Match(attributesText);
				if (!attributesMatch.Success)
				{
					scriptMatch = _scriptRegex.Match(template, scriptMatch.Index + scriptMatch.Length);
					continue;
				}

				Dictionary<string, string> attributes = ConvertAttributes(attributesMatch);
				if (!attributes.ContainsKey("runat") || attributes["runat"] != "template")
				{
					scriptMatch = _scriptRegex.Match(template, scriptMatch.Index + scriptMatch.Length);
					continue;
				}

				string scriptText = scriptMatch.Groups["script"].Value;
				ClassMemberCodeBlocks.Add(scriptText);

				template = template.Remove(scriptMatch.Index, scriptMatch.Length);

				scriptMatch = _scriptRegex.Match(template, scriptMatch.Index);
			}

			return template;
		}

		// Move parsed attributes from a Match into a Dictionary
		protected Dictionary<string, string> ConvertAttributes(Match match)
		{
			Dictionary<string, string> attributes = new Dictionary<string,string>();

			Group attributeNames  = match.Groups["name"];
			Group attributeValues = match.Groups["value"];
			for(int iAttribute = 0; iAttribute < attributeNames.Captures.Count; iAttribute++)
				attributes.Add(attributeNames.Captures[iAttribute].Value, attributeValues.Captures[iAttribute].Value);

			return attributes;
		}

		protected void ProcessDirective(string tagText)
		{
			string directiveText = _directiveRegex.Match(tagText).Groups["directive"].Value;
			Match directiveMatch = _directiveParser.Match(directiveText);
			if(!directiveMatch.Success)
				throw new ApplicationException(string.Format("Cannot parse directive tag: {0}", tagText.Substring(0, 60)));
			string directive = directiveMatch.Groups["directive"].Value;
			Dictionary<string, string> attributes = ConvertAttributes(directiveMatch);
			StatementGenerators.Add(new DirectiveGenerator(directive, attributes));

			switch(directive)
			{
				case "CodeTemplate":
				{
					if(attributes.ContainsKey("Language"))       Language       = attributes["Language"];
					if(attributes.ContainsKey("TargetLanguage")) TargetLanguage = attributes["TargetLanguage"];
					if(attributes.ContainsKey("Inherits"))       Inherits       = attributes["Inherits"];
					if(attributes.ContainsKey("Src"))            Src            = attributes["Src"];
					break;
				}
				case "Assembly":
				{
					if(!attributes.ContainsKey("Name"))
						throw new ApplicationException(string.Format("Assembly directive requries a Name attribute: {0}", tagText.Substring(0, 60)));
					Assemblies.Add(attributes["Name"]);
					break;
				}
				case "Import":
				{
					if(!attributes.ContainsKey("Namespace"))
						throw new ApplicationException(string.Format("Import directive requries a Name attribute: {0}", tagText.Substring(0, 60)));
					Imports.Add(attributes["Namespace"]);
					break;
				}
				case "Property":
				{
					if(!attributes.ContainsKey("Name") || !attributes.ContainsKey("Type"))
						throw new ApplicationException(string.Format("Property directive requries Name and Type attributes: {0}", tagText.Substring(0, 60)));

					Properties.Add(new Property(attributes["Name"], attributes["Type"]));
					break;
				}
			}
		}

		protected void ProcessComment(string tagText)
		{
			string commentText = _commentRegex.Match(tagText).Groups["comment"].Value;
			StatementGenerators.Add(new CommentGenerator(commentText));
		}

		protected void ProcessExpression(string tagText)
		{
			string expressionText = _expressionRegex.Match(tagText).Groups["expression"].Value;
			StatementGenerators.Add(new ExpressionGenerator(expressionText));
		}

		protected void ProcessBlock(string tagText)
		{
			string blockText = _blockRegex.Match(tagText).Groups["block"].Value;
			StatementGenerators.Add(new BlockGenerator(blockText));
		}

		/// <summary>
		/// Each line goes into its own generator object. This makes the generated code a bit nicer
		/// and it makes newline clean up (done later in OptimizeNewlines) easier. We also handle escape 
		/// sequences here.
		/// </summary>
		protected void ProcessLiteral(string literalText)
		{
			literalText = literalText.Replace("<%%", "<%");
			literalText = literalText.Replace("%%>", "%>");
			int iStart = 0;
			int iNewline = literalText.IndexOf("\r\n");
			while (iNewline >= 0)
			{
				int iCount = iNewline - iStart + 2;
				StatementGenerators.Add(new LiteralGenerator(literalText.Substring(iStart, iCount)));
				iStart += iCount;
				if (iStart >= literalText.Length)
					break;
				iNewline = literalText.IndexOf("\r\n", iStart);
			}
			if (literalText.Length > iStart)
				StatementGenerators.Add(new LiteralGenerator(literalText.Substring(iStart, literalText.Length - iStart)));
		}

		/// <summary>
		/// If a line only has a directive or block statement, then including it in the output
		/// will result in an empty line (possibly with some white space). To make the output
		/// cleaner, we need to suppress these empty lines. To suppress the empty lines, search
		/// through StatementGenerators looking for lines that consist of only white space,
		/// directives and blocks. When we find such a line, we delete all the literals, thus
		/// deleting the white space and the newline.
		/// </summary>
		protected void OptimizeNewlines()
		{
			int iLineStartGenerator = 0;
			for (int iGenerator = 0; iGenerator < StatementGenerators.Count; iGenerator++)
			{
				if (StatementGenerators[iGenerator].GetType() == typeof(LiteralGenerator))
				{
					LiteralGenerator generatorLiteral = (LiteralGenerator)StatementGenerators[iGenerator];
					if (generatorLiteral.Text.Contains("\r\n"))
					{
						int iLineEndGenerator = iGenerator; // Current "line" runs from iLineStartGenerator to iLineEndGenerator
						if (iLineStartGenerator == iLineEndGenerator)
						{
							iLineStartGenerator++;
							continue;
						}
						bool suppressLine = true;
						for(int iCheckGenerator = iLineStartGenerator; iCheckGenerator <= iLineEndGenerator; iCheckGenerator++)
						{
							if (StatementGenerators[iCheckGenerator].GetType() == typeof(ExpressionGenerator))
							{
								// Expressions always cause output
								suppressLine = false; 
								break;
							}
							else if (StatementGenerators[iCheckGenerator].GetType() == typeof(LiteralGenerator))
							{
								LiteralGenerator checkLiteral = (LiteralGenerator)StatementGenerators[iCheckGenerator];
								if (checkLiteral.Text.Trim() != "")
								{
									suppressLine = false; 
									break;
								}
							}
						}
						if(suppressLine)
						{
							// All the literals between iLineStartGenerator and iLineEndGenerator are all white space, so delete them
							for(int iDeleteGenerator = iLineEndGenerator; iDeleteGenerator >= iLineStartGenerator; iDeleteGenerator--)
							{
								if (StatementGenerators[iDeleteGenerator].GetType() == typeof(LiteralGenerator))
								{
									StatementGenerators.RemoveAt(iDeleteGenerator);
									iLineEndGenerator--;
								}
							}
						}
						iLineStartGenerator = iLineEndGenerator + 1;
						iGenerator = iLineEndGenerator;
					}
				}
			}
		}

		protected Assembly ProcessCodeBehind(string src, TemplateRunner runner)
		{
			if (CodeBehindAssemblies.ContainsKey(src))
				return CodeBehindAssemblies[src];

			StreamReader codeBehindReader = new StreamReader(Src);
			Assembly codeBehindAssembly = CompileAssemblyCode(codeBehindReader.ReadToEnd(), null, runner);
			if (codeBehindAssembly != null)
				CodeBehindAssemblies[src] = codeBehindAssembly;

			return codeBehindAssembly;
		}
	}

	/// <summary>
	/// Default base class for the class generated by <see cref="TemplateCompiler"/>
	/// </summary>
	public class CodeTemplate
	{
		public CodeTemplate() { }
	}
}
/*
 
 Test data for the regular expression parser (paste into The Regulator, etc.):
  
This is a test
<%This is a 
block%>
<%@This is a
test %>
<%This is another block%>
<%-- this is a 
comment--%>
<%=This is an
expression%>
<%-- this is another
<script stuff>This is 
a sript
</script>
comment--%>
<%=This is another expressio n %>
<script>ScriptTest</script>
<%}%>
<%%%%>
<%%abc%%>
<%abc%%>
<%%abc%>
<%@a%>
<%}%>

<%@ CodeTemplate Language="VB" TargetLanguage="VB" Description="Generates a strongly-typed collection of key-and-value pairs that are sorted by the keys and are accessible by key and by index." %>
<%@ CodeTemplate Language="CS" Inherits="CodeSmith.BaseTemplates.SqlCodeTemplate" %>
<%@ Assembly Name="CodeSmith.CustomProperties" %>
<%@ Assembly Name="CodeSmith.BaseTemplates" %>
<%@ Assembly Name="SchemaExplorer" %>
<%@ Import Namespace="SchemaExplorer" %>
<%@ Property Name="Key" Type="System.String" %>
<%@ Property Name="ClassName" Type="System.String" Category="Context" Description="The name of the class to be generated." %>
<%@ Property Name="Accessibility" Type="AccessibilityEnum" Category="Options" Description="The accessibility of the class to be generated." %>
<%@ Property Name="ClassNamespace" Type="System.String" Optional="True" Category="Context" Description="The namespace that the generated class will be a member of." %>
 
*/
