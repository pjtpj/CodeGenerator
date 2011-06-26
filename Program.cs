// Copyright (C) 2006 Teztech, Inc. All rights reserved.

using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Collections.Generic;

using CommandLine;

namespace CodeGenerator
{
	class Program
	{
		/// <summary>
		/// Structure to hold argument values
		/// </summary>
		class Arguments
		{
			[CommandLineArgument(CommandLineArgumentType.AtMostOnce, LongName = "template", HelpText = "The template file to use for code generation.")]
			public string Template = "";
			[CommandLineArgument(CommandLineArgumentType.AtMostOnce, LongName = "properties", HelpText = "A propertyset XML file to set the template's properties.")]
			public string Properties = "";
			[CommandLineArgument(CommandLineArgumentType.AtMostOnce, LongName = "out", HelpText = "The output file for generated code.")]
			public string Out = "";
			[CommandLineArgument(CommandLineArgumentType.AtMostOnce, LongName = "merge", HelpText = "Replace this region in the outupt file with the generated code.")]
			public string Merge = "";
			[CommandLineArgument(CommandLineArgumentType.Multiple, LongName = "property", HelpText = "Use Name=Value to specify a property value.")]
			public string[] Property = new string[]{};
			[CommandLineArgument(CommandLineArgumentType.AtMostOnce, LongName = "debug", HelpText = "Enable debugging.")]
			public bool Debug = false;
			[CommandLineArgument(CommandLineArgumentType.AtMostOnce, LongName = "tempfiles", HelpText = "Don't delete temporary files.")]
			public bool TempFiles = false;
			[CommandLineArgument(CommandLineArgumentType.AtMostOnce, LongName = "batch", HelpText = "The XML file to use for batch code generation.")]
			public string Batch = "";
		}


		/// <summary>
		/// Console application entry point
		/// </summary>
		/// <param name="commandLineArgs"></param>
		static void Main(string[] commandLineArgs)
		{
			try
			{
				Arguments args = new Arguments();

				if (!Utility.ParseCommandLineArgumentsWithUsage(commandLineArgs, args))
					return;

				if ((args.Template == "" && args.Batch == "") || (args.Template != "" && args.Batch != ""))
				{
					Console.WriteLine("ERROR: You must supply a template or batch file (but not both).");
					Console.Write(Utility.CommandLineArgumentsUsage(args.GetType()));
					return;
				}

				XmlDocument propertiesXml = new XmlDocument();
				if (args.Properties != "")
				{
					StreamReader propertiesReader = new StreamReader(args.Properties);
					string propertiesText = propertiesReader.ReadToEnd();
					propertiesText.Replace("<codeSmith>", "<codeGenerator>");
					propertiesText.Replace("</codeSmith>", "</codeGenerator>");
					propertiesXml.LoadXml(propertiesText);
				}
				else
				{
					propertiesXml.LoadXml(@"<?xml version=""1.0"" encoding=""us-ascii""?><codeGenerator><propertySet></propertySet></codeGenerator>");
				}

				XmlNode propertySetNode = propertiesXml.SelectSingleNode("/codeGenerator/propertySet");
				if (propertySetNode == null)
					propertySetNode = propertiesXml.SelectSingleNode("/codeSmith/propertySet");
				if (propertySetNode == null)
					throw new ApplicationException("Set of property elements must be enclosed by <codeGenerator> (or <codeSmith>) and <propertySet> elements");

				// Parse comand line properties
				foreach (string property in args.Property)
				{
					string[] nameValue = property.Split(new Char[] { '=' }, 2);
					if (nameValue.Length != 2)
					{
						Console.WriteLine(string.Format("ERROR: A property must have the form Name=Value: {0}", property));
						Console.Write(Utility.CommandLineArgumentsUsage(args.GetType()));
						return;
					}

					XmlAttribute propertyNameAttribute = propertiesXml.CreateAttribute("name");
					propertyNameAttribute.Value = nameValue[0];
					XmlNode propertyNode = propertiesXml.CreateElement("property");
					propertyNode.Attributes.Append(propertyNameAttribute);
					propertyNode.InnerText = nameValue[1];

					propertySetNode.AppendChild(propertyNode);
				}

				if (args.Template != "")
				{
					TemplateCompiler compiler = new TemplateCompiler();
					StreamReader templateReader = new StreamReader(args.Template);
					TemplateRunner runner = compiler.CompileTemplate(templateReader.ReadToEnd(), args.Debug);
					templateReader.Close();

					if (args.Out != "")
						Console.Write("Generating {0}", args.Out);

					StringWriter propertiesXmlWriter = new StringWriter();
					propertiesXml.Save(propertiesXmlWriter);
					string propertiesXmlText = propertiesXmlWriter.ToString();
					runner.SetProperties(propertiesXmlText);
					string templateOutput = runner.RunTemplate();

					if (args.Out != "")
					{
						using (StreamWriter outputWriter = new StreamWriter(args.Out))
							outputWriter.Write(templateOutput);
						Console.WriteLine(".");
					}
					else
					{
						Console.Write(templateOutput);
					}

					return;
				}

				if (args.Batch != "")
				{
					StreamReader batchReader = new StreamReader(args.Batch);
					string batchText = batchReader.ReadToEnd();
					batchText.Replace("<codeSmith>", "<codeGenerator>");
					batchText.Replace("</codeSmith>", "</codeGenerator>");
					XmlDocument batchXml = new XmlDocument();
					batchXml.LoadXml(batchText);

					// <defaultTemplate path="mytemplate.cst" />
					string defaultTemplate = GetAttributeValue(batchXml.SelectSingleNode("/codeGenerator/defaultTemplate"), "path");
					if (defaultTemplate == "")
						defaultTemplate = args.Template;

					// <defaultOutput path="mytemplate.cs" />
					string defaultOutput = GetAttributeValue(batchXml.SelectSingleNode("/codeGenerator/defaultOutput"), "path");
					if (defaultOutput == "")
						defaultOutput = args.Out;

					string lastTemplate = null;
					TemplateRunner lastRunner = null;

					XmlNodeList batchPropertySetNodes = batchXml.SelectNodes("/codeGenerator/propertySets/propertySet");
					foreach (XmlNode batchPropertySetNode in batchPropertySetNodes)
					{
						// <propertySet template="mytemplate.cst">
						string template = GetAttributeValue(batchPropertySetNode, "template");
						if (template == "")
							template = defaultTemplate;

						// <propertySet output="mytemplate.cs">
						string output = GetAttributeValue(batchPropertySetNode, "output");
						if (output == "")
							output = defaultOutput;

						// Construct a new properties document for the batch process. We start with 
						// properties read from args.Properties and args.Property, then add in 
						// property elements found under /codeGenerator/defaultProperties, then, finally,
						// add property elements found under /codeGenerator/propertySets/propertySet

						// Construct a new Xml document for the batch properties by converting propertiesXml
						// into a string, then calling XmlDocument.LoadXml
						StringWriter propertiesXmlWriter = new StringWriter();
						propertiesXml.Save(propertiesXmlWriter);
						string propertiesXmlText = propertiesXmlWriter.ToString();
						XmlDocument batchTargetPropertiesXml = new XmlDocument();
						batchTargetPropertiesXml.LoadXml(propertiesXmlText);
						// Locate /codeGenerator/propertySet so we can append properties here
						XmlNode batchTargetPropertySetNode = batchTargetPropertiesXml.SelectSingleNode("/codeGenerator/propertySet");

						// Add property elements found under /codeGenerator/defaultProperties
						XmlNodeList batchDefaultPropertiesNodes = batchXml.SelectNodes("/codeGenerator/defaultProperties/property");
						if (batchDefaultPropertiesNodes != null)
						{
							foreach (XmlNode batchDefaultProperty in batchDefaultPropertiesNodes)
							{
								XmlNode importNode = batchTargetPropertiesXml.ImportNode(batchDefaultProperty, true);  // Copy from one XmlDocument to another
								batchTargetPropertySetNode.AppendChild(importNode);  // Place in tree
							}
						}

						// Add property elements found under /codeGenerator/propertySets/propertySet
						XmlNodeList propertyNodes = batchPropertySetNode.ChildNodes;
						if (propertyNodes != null)
						{
							foreach (XmlNode propertyNode in propertyNodes)
							{
								XmlNode importNode = batchTargetPropertiesXml.ImportNode(propertyNode, true); // Copy from one XmlDocument to another
								batchTargetPropertySetNode.AppendChild(importNode);  // Place in tree
							}
						}

						// This minor caching of templates should catch the most common cases
						if (template != lastTemplate)
						{
							TemplateCompiler compiler = new TemplateCompiler();
							StreamReader templateReader = new StreamReader(template);
							lastRunner = compiler.CompileTemplate(templateReader.ReadToEnd(), args.Debug);
							lastTemplate = template;
							templateReader.Close();
						}

						if (output != "")
							Console.Write("Generating {0}...", output);

						StringWriter batchTargetPropertiesXmlWriter = new StringWriter();
						batchTargetPropertiesXml.Save(batchTargetPropertiesXmlWriter);
						string batchTargetPropertiesXmlText = batchTargetPropertiesXmlWriter.ToString();
						lastRunner.SetProperties(batchTargetPropertiesXmlText);
						string batchTargetTemplateOutput = lastRunner.RunTemplate();

						if (output != "")
						{
							using (StreamWriter outputWriter = new StreamWriter(output))
								outputWriter.Write(batchTargetTemplateOutput);
							Console.WriteLine("done.");
						}
						else
						{
							Console.Write(batchTargetTemplateOutput);
						}
					}
				}
			}
			catch (Exception e)
			{
				Console.Write("ERROR A fatal exception occured: {0}: {1}", e.GetType().Name, e.Message);
				if(e.InnerException != null)
					Console.Write(" ({0}: {1})", e.InnerException.GetType().Name, e.InnerException.Message);
				Console.WriteLine();
			}
		}

		public static string GetAttributeValue(XmlNode element, string attributeName)
		{
			if (element == null)
				return "";
			XmlAttribute attribute = element.Attributes[attributeName];
			if (attribute == null)
				return "";
			return attribute.Value;
		}
	}
}
