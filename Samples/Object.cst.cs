using System;
using System.Data;
using System.Collections;
using System.ComponentModel;
using DatabaseSchema;

namespace CodeGenerator
{
	public class ObjectTemplate
	{
		public bool IsColumnBinary(Column column)
		{
			switch (column.NativeType)
			{
				case "longblob":
					return true;
			}

			switch (column.DataType)
			{
				case DbType.Binary:
					return true;
			}

			return false;
		}

		public bool HasIdentity(Table table)
		{
			foreach (Column column in table.Columns)
			{
				if (column.IsIdentity)
					return true;
			}
			return false;
		}

		public string GetCamelCaseName(string value)
		{
			return value.Substring(0, 1).ToLower() + value.Substring(1);
		}

		public string GetLocalVariableName(Column column)
		{
			string propertyName = GetPropertyName(column);
			string memberVariableName = GetCamelCaseName(propertyName);

			return memberVariableName;
		}

		public string GetMemberVariableName(Column column)
		{
			return GetMemberVariableName(column, false);
		}

		public string GetMemberVariableName(Column column, bool isOriginalValue)
		{
			string propertyName = GetPropertyName(column);
			string memberVariableName = "_" + GetCamelCaseName(propertyName);
			if (isOriginalValue)
				memberVariableName += "OriginalValue";

			return memberVariableName;
		}

		public string GetMemberParameterName(Column column)
		{
			string memberParameterName = GetMemberVariableName(column);
			if (column.DataType == DbType.Date || column.DataType == DbType.DateTime)
				memberParameterName += ".ToUniversalTime()";
			return memberParameterName;
		}

		public string GetPropertyName(Column column)
		{
			string propertyName = column.Name;

			// if (propertyName == column.Table.Name + "Name") return "Name";
			// if (propertyName == column.Table.Name + "Description") return "Description";

			// if (propertyName.EndsWith("TypeCode")) propertyName = propertyName.Substring(0, propertyName.Length - 4);

			return propertyName;
		}

		public string GetMemberVariableDefaultValue(Column column)
		{
			switch (column.NativeType)
			{
				case "longblob":
					{
						return " = new byte[0]";
					}
			}

			switch (column.DataType)
			{
				case DbType.Guid:
					{
						return " = Guid.Empty";
					}
				case DbType.AnsiString:
				case DbType.AnsiStringFixedLength:
				case DbType.String:
				case DbType.StringFixedLength:
					{
						return " = string.Empty";
					}
				case DbType.DateTime:
					{
						return " = DbRequest.ZeroDateTime";
					}
				case DbType.Binary:
					{
						return " = new byte[0]";
					}
				default:
					{
						return "";
					}
			}
		}

		public string GetCSharpType(Column column)
		{
			if (column.Name.EndsWith("TypeCode")) return column.Name;

			switch (column.NativeType)
			{
				case "longblob": return "byte[]";
			}

			switch (column.DataType)
			{
				case DbType.AnsiString: return "string";
				case DbType.AnsiStringFixedLength: return "string";
				case DbType.Binary: return "byte[]";
				case DbType.Boolean: return column.AllowDBNull ? "bool?" : "bool";
				case DbType.Byte: return column.AllowDBNull ? "byte?" : "byte";
				case DbType.Currency: return column.AllowDBNull ? "decimal?" : "decimal";
				case DbType.Date: return column.AllowDBNull ? "DateTime?" : "DateTime";
				case DbType.DateTime: return column.AllowDBNull ? "DateTime?" : "DateTime";
				case DbType.Decimal: return column.AllowDBNull ? "decimal?" : "decimal";
				case DbType.Double: return column.AllowDBNull ? "double?" : "double";
				case DbType.Guid: return column.AllowDBNull ? "Guid?" : "Guid";
				case DbType.Int16: return column.AllowDBNull ? "short?" : "short";
				case DbType.Int32: return column.AllowDBNull ? "int?" : "int";
				case DbType.Int64: return column.AllowDBNull ? "long?" : "long";
				case DbType.Object: return "object";
				case DbType.SByte: return column.AllowDBNull ? "sbyte?" : "sbyte";
				case DbType.Single: return column.AllowDBNull ? "float?" : "float";
				case DbType.String: return "string";
				case DbType.StringFixedLength: return "string";
				case DbType.Time: return column.AllowDBNull ? "TimeSpan?" : "TimeSpan";
				case DbType.UInt16: return column.AllowDBNull ? "ushort?" : "ushort";
				case DbType.UInt32: return column.AllowDBNull ? "uint?" : "uint";
				case DbType.UInt64: return column.AllowDBNull ? "ulong?" : "ulong";
				case DbType.VarNumeric: return "decimal";
				default:
					{
						return "__UNKNOWN__" + column.NativeType;
					}
			}
		}

		public string GetReaderMethod(Column column)
		{
			switch (column.DataType)
			{
				case DbType.Byte:
					{
						return "GetByte";
					}
				case DbType.Int16:
					{
						return "GetInt16";
					}
				case DbType.Int32:
					{
						return "GetInt32";
					}
				case DbType.Int64:
					{
						return "GetInt64";
					}
				case DbType.AnsiStringFixedLength:
				case DbType.AnsiString:
				case DbType.String:
				case DbType.StringFixedLength:
					{
						return "GetString";
					}
				case DbType.Boolean:
					{
						return "GetBoolean";
					}
				case DbType.Guid:
					{
						return "GetGuid";
					}
				case DbType.Currency:
				case DbType.Decimal:
					{
						return "GetDecimal";
					}
				case DbType.DateTime:
				case DbType.Date:
					{
						return "GetDateTime";
					}
				case DbType.Double:
					{
						return "GetDouble";
					}
				case DbType.Binary:
					{
						return "GetBytes";
					}
				default:
					{
						return "__SQL__" + column.DataType;
					}
			}
		}

		public string GetClassName(Table table)
		{
			if (table.Name.EndsWith("Companies") || table.Name.EndsWith("Inquiries") || table.Name.EndsWith("Categories") || table.Name.EndsWith("Countries"))
			{
				return table.Name.Substring(0, table.Name.Length - 3) + "y";
			}
			else if (table.Name.EndsWith("Addresses") || table.Name.EndsWith("Mailboxes"))
			{
				return table.Name.Substring(0, table.Name.Length - 2) + "";
			}
			else if (table.Name.EndsWith("s"))
			{
				return table.Name.Substring(0, table.Name.Length - 1) + "";
			}
			else
			{
				return table.Name + "";
			}
		}
		public string GetDbType(Column column)
		{
			switch (column.NativeType)
			{
				case "bigint": return "DbType.Int64";
				case "longblob": return "DbType.Binary";
				case "binary": return "DbType.Binary";
				case "bit": return "DbType.Boolean";
				case "char": return "DbType.AnsiStringFixedLength";
				case "datetime": return "DbType.DateTime";
				case "decimal": return "DbType.Decimal";
				case "float": return "DbType.Single";
				case "double": return "DbType.Double";
				case "image": return "DbType.Binary";
				case "int": return "DbType.Int32";
				case "money": return "DbType.Currency";
				case "nchar": return "DbType.StringFixedLength";
				case "ntext": return "DbType.String";
				case "numeric": return "DbType.Decimal";
				case "nvarchar": return "DbType.String";
				case "real": return "DbType.Single";
				case "smalldatetime": return "DbType.DateTime";
				case "smallint": return "DbType.Int16";
				case "smallmoney": return "DbType.Decimal";
				case "sql_variant": return "DbType.Object";
				case "sysname": return "DbType.String";
				case "text": return "DbType.String";
				case "timestamp": return "DbType.Binary";
				case "tinyint": return "DbType.Byte";
				case "uniqueidentifier": return "DbType.Guid";
				case "varbinary": return "DbType.Binary";
				case "varchar": return "DbType.AnsiString";
				default: return "__UNKNOWN__" + column.NativeType;
			}
		}

		public string GetSqlDbType(Column column)
		{
			switch (column.NativeType)
			{
				case "bigint": return "SqlDbType.BigInt";
				case "longblob": return "SqlDbType.Binary";
				case "binary": return "SqlDbType.Binary";
				case "bit": return "SqlDbType.Bit";
				case "char": return "SqlDbType.Char";
				case "datetime": return "SqlDbType.DateTime";
				case "decimal": return "SqlDbType.Decimal";
				case "float": return "SqlDbType.Float";
				case "image": return "SqlDbType.Image";
				case "int": return "SqlDbType.Int";
				case "money": return "SqlDbType.Money";
				case "nchar": return "SqlDbType.NChar";
				case "ntext": return "SqlDbType.NText";
				case "numeric": return "SqlDbType.Decimal";
				case "nvarchar": return "SqlDbType.NVarChar";
				case "real": return "SqlDbType.Real";
				case "smalldatetime": return "SqlDbType.SmallDateTime";
				case "smallint": return "SqlDbType.SmallInt";
				case "smallmoney": return "SqlDbType.SmallMoney";
				case "sql_variant": return "SqlDbType.Variant";
				case "sysname": return "SqlDbType.NChar";
				case "text": return "SqlDbType.Text";
				case "timestamp": return "SqlDbType.Timestamp";
				case "tinyint": return "SqlDbType.TinyInt";
				case "uniqueidentifier": return "SqlDbType.UniqueIdentifier";
				case "varbinary": return "SqlDbType.VarBinary";
				case "varchar": return "SqlDbType.VarChar";
				default: return "__UNKNOWN__" + column.NativeType;
			}
		}

		//public override string GetFileName()
		//{
		//    return this.GetClassName(this.SourceTable) + ".cs";
		//}
	}
}