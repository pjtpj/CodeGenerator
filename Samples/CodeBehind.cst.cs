using System;
using CodeGenerator;

namespace CodeGenerator
{
	public class CodeBehindClass
	{
		public string TestStringMethod()
		{
			return "Today's date: " + DateTime.Now.ToString("MM/dd/yyyy");
		}

		public int TestIntMethod(int a, int b)
		{
			return a + b;
		}
	}
}