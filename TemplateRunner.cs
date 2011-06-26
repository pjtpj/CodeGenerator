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
	/// Runs a compiled template to generate output.
	/// </summary>
	class TemplateRunner
	{
		public Assembly Assembly;
		public string CodeGeneratorNamespace;
		public string CodeGeneratorClassName;
		public string CodeGeneratorMethodName;
		public TempFileCollection TempFiles = new TempFileCollection();

		public TemplateRunner() {}

		~TemplateRunner()
		{
			TempFiles.Delete();
		}

		protected Type TemplateType;
		protected Object TemplateInstance;

		public void SetProperties(string propertiesXml)
		{
			if (Assembly == null)
				return;

			if (TemplateInstance == null)
				CreateTemplateInstance();

			StringReader stringReader = new StringReader(propertiesXml);
			XmlReader reader = XmlReader.Create(stringReader);
			if (!reader.Read() || !reader.IsStartElement() || !(reader.Name == "codeSmith" || reader.Name == "codeGenerator"))
				throw new ApplicationException("Properties XML must be enclosed by <codeSmith> or <codeGenerator>");

			if (!reader.Read() || !reader.IsStartElement() || reader.Name != "propertySet")
				throw new ApplicationException("Set of property elements must be enclosed by <propertySet> element");

			if (!reader.ReadToFollowing("property"))
				return;
			do
			{
				if (reader.Name != "property")
					throw new ApplicationException(string.Format("<property> element is expected: {0}", reader.Name.Substring(0, 60)));

				string propertyName = reader.GetAttribute("name");

				PropertyInfo propertyInfo = TemplateType.GetProperty(propertyName);
				if (propertyInfo != null)
				{
					// Was using if(propertyInfo.PropertyType.IsPrimitive) propertyInfo.SetValue(TemplateInstance, reader.ReadElementContentAsObject(), null);
					// bug XmlSerializer works fine for all types
					XmlSerializer serializer = new XmlSerializer(propertyInfo.PropertyType, new XmlRootAttribute("property"));
					propertyInfo.SetValue(TemplateInstance, serializer.Deserialize(reader), null);
				}
			}
			while (reader.ReadToNextSibling("property"));
		}

		public string RunTemplate()
		{
			if (Assembly == null)
				return "";

			if (TemplateInstance == null)
				CreateTemplateInstance();

			// Invoke the code generation method
			string templateOutput = (string)TemplateType.InvokeMember(CodeGeneratorMethodName,
				BindingFlags.DeclaredOnly |
				BindingFlags.Public | BindingFlags.NonPublic |
				BindingFlags.Instance | BindingFlags.InvokeMethod, null, TemplateInstance, null);

			return templateOutput;
		}

		protected void CreateTemplateInstance()
		{
			TemplateType = Assembly.GetType(CodeGeneratorNamespace + "." + CodeGeneratorClassName);

			// TemplateInstance = Activator.CreateInstance(Assembly.Location, CodeGeneratorNamespace + "." + CodeGeneratorClassName);

			// Create instance by invoking the type's constructor
			TemplateInstance = TemplateType.InvokeMember(null,
				BindingFlags.DeclaredOnly |
				BindingFlags.Public | BindingFlags.NonPublic |

				BindingFlags.Instance | BindingFlags.CreateInstance, null, null, null);
		}
	}
}
