// Copyright (C) 2006 Teztech, Inc. All rights reserved.

using System;
using System.Xml.Serialization;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

using MySql.Data.MySqlClient;

/*

	The elaborate, all-inclusive design document:

namespace DatabaseSchema
{
	class Database
	{
		Database(string connectionString);                     // connect to server + database
		public DatabaseList<Table> Tables;                     // List of tables in database
		public DatabaseList<StoredProcedure> StoredProcedures; // List of stored procedures
	}

	class Table
	{
		public string Name;
		public DatabaseList<Column> Columns;     // List of columns
		public DatabaseList<Index>  Indexes;     // List of indexes
		public List<Key>            Keys;        // List of all keys, foreign and primary
		public List<Key>            ForeignKeys; // List of foreign keys
		public PrimaryKey           PrimaryKey;  // The table's primary key
		public DatabaseList<Column> NonPrimaryKeyColumns; // Shorthand to build list of non-primary key columns
		public DatabaseList<Column> NonKeyColumns;        // Shorthand to build a list of non-key columns
	}

	class Column {}          // Various properties such as Name, DataType, NativeType for a column
	class Index {}           // Various properties such as Name, MemberColumns, IsPrimaryKey for an index
	class Key {}             // Various properties such as Name, MemberColumns, IsPrimaryKey for a primary or foreign key
	class PrimaryKey {}      // Various properties such as Name, MemberColumns, IsPrimaryKey for a primary key
	class StoredProcedure {} // Various properties such as Name and Parameters for a stored procedure
}

	To cruise around a database schema, you declare either a Database or a Table as a 
	template property.Then, in your template's properties xml, you assign values for 
	Database.ConnectionString (if you are using a Database object as a template 
	property) or Table.ConnectionString and Table.Name (if you are using a Table object 
	as a template property). See the samples that use this provider for a demonstration.

	The API design is mine, but some of the implementation is based on MYSQL SCHEMA PROVIDER v0.4
	MYSQL SCHEMA PROVIDER v0.4 was develped by Alexander Mazurov (mazurov1@mail.ru), then
	fixed and enhanced by others including "DRS". 
*/

namespace DatabaseSchema
{
	public class Database
	{
		public Database() { /* initialize by setting ConnectionString */ }
		public Database(string connectionString)
		{
			ConnectionString = connectionString;
		}

		private MySqlConnection _connection;
		[XmlIgnore]
		public MySqlConnection Connection
		{
			get { return _connection; }
		}
		private string _connectionString;
		public string ConnectionString
		{
			get { return _connectionString; }
			set 
			{
				MySqlConnection connection = new MySqlConnection(value);
				connection.Open();
				_connection = connection;
				_connectionString = value;
				_name = Connection.Database;
			}
		}
		private string _name;
		[XmlIgnore]
		public string Name
		{
			get { return _name; }
		}
		private DatabaseList<Table> _tables;
		[XmlIgnore]
		public DatabaseList<Table> Tables
		{
			get 
			{
				if (_tables == null)
				{
					_tables = new DatabaseList<Table>();
					const int POS_NAME = 0;

					string sql = @"SHOW TABLES";

					MySqlCommand cmd = new MySqlCommand(sql, Connection);
					using (MySqlDataReader reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							// Exclude the extended properties table if it is encountered
							if (reader.GetString(POS_NAME).ToUpper() != "CODESMITH_EXTENDED_PROPERTIES")
							{
								Table table = new Table(this, reader.GetString(POS_NAME));
								_tables.Add(table.Name, table);
							}
						}
					}
				}

				return _tables; 
			}
		}

		public static string Quote(string name)
		{
			return "`" + name + "`";
		}
	}

	public class Table
	{
		public Table() { /* initialize by setting ConnectionString and Name */ }

		public Table(string connectionString, string name) 
		{
			_database = new Database(connectionString);
			_name = name;
		}

		public Table(Database database, string name)
		{
			  _database = database;
			  _name = name;
		}

		public string ConnectionString
		{
			get { return Database.ConnectionString; }
			set
			{
				if (_database == null)
					_database = new Database(value);
			}
		}
		private static Database _database;
		[XmlIgnore]
		public Database Database
		{
			get { return _database; }
		}
		private string _name;
		public string Name
		{
			get { return _name; }
			set { _name = value; }
		}

		private DatabaseList<Column> _columns;
		[XmlIgnore]
		public DatabaseList<Column> Columns
		{
			get 
			{
				if (_columns == null)
					LoadColumns();
				return _columns; 
			}
		}
		private List<Key> _foreignKeys;
		[XmlIgnore]
		public List<Key> ForeignKeys
		{
			get 
			{
				if (_foreignKeys == null)
				{
					LoadColumns();
					_foreignKeys = new List<Key>();
					foreach (Key key in Keys)
						if (key.ForeignKeyTable == this)
							_foreignKeys.Add(key);
				}

				return _foreignKeys; 
			}
		}
		private DatabaseList<Index> _indexes;
		[XmlIgnore]
		public DatabaseList<Index> Indexes
		{
			get 
			{
				if (_indexes == null)
				{
					LoadColumns();

					_indexes = new DatabaseList<Index>();

					string sql = @"SHOW INDEX FROM "+ Database.Quote(Name);

					MySqlCommand cmd = new MySqlCommand(sql, Database.Connection);
					using (MySqlDataReader reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							string iname = (string) reader["Key_name"];

							// Add the column to the collection by index name
							string indexColumn = (string) reader["Column_name"];
				
							// Determine if index is unique
							//DRS:2005-04-05 - reader shows datatype as TINYINT, which can be converted directly
							//	to a bool; the (Int64) cast was throwing an exception
							//ORIG:	bool isUnique = (((Int64)reader["Non_unique"]) == 0);
							bool isUnique = !Convert.ToBoolean(reader["Non_unique"]);

							// Determine if index is the primary key index
							bool isPrimary = (iname == "PRIMARY");

							// Determine if the index is on a TABLE or CLUSTER
							// NOTE: A Microsoft® SQL Server™ clustered index is not like an Oracle cluster. 
							// An Oracle cluster is a physical grouping of two or more tables that share the 
							// same data blocks and use common columns as a cluster key. SQL Server does not 
							// have a structure that is similar to an Oracle cluster.
							bool isClustered = false;

							Index index;
							if (_indexes.ContainsKey(iname))
							{
								index = _indexes[iname];
							}
							else
							{
								index = new Index(this, iname, isPrimary, isUnique, isClustered);
								_indexes.Add(iname, index);
							}

							index.MemberColumns.Add(Columns[indexColumn]);
						}
					}
				}
				return _indexes; 
			}
			set { _indexes = value; }
		}
		private List<Key> _keys;
		[XmlIgnore]
		public List<Key> Keys
		{
			get 
			{
				if (_keys == null)
				{
					LoadColumns();

					_keys = new List<Key>();

					// DRS:2005-04-05 Adding ability to determine keys, particularly foreign keys
					// ORIG: return new allKeyschema[0];
					ArrayList allKeys = new ArrayList();
					ArrayList mulKeyCols = new ArrayList();
					ArrayList priKeyCols = new ArrayList();

					// DRS:This will give us all of the comments for all tables
					string[] comments = GetTableComments();

					// DRS:The Comment column is of this form:
					//	InnoDB free: XXXXX; (`<fk_column>`) REFER (`<pk_table>\<fk_table>`) [ON UPDATE XXXXX] [ON DELETE XXXXX]; ...
					Regex rx = new Regex(@"\(`(\w+)`\)\s+REFER\s+`(\w+)/(\w+)`\(`(\w+)`\)",
						RegexOptions.Singleline | RegexOptions.IgnoreCase);

					// DRS:We'll start getting the keys from the table
					string sql = @"SHOW COLUMNS FROM " + Database.Quote(Name);
					MySqlCommand cmd = new MySqlCommand(sql, Database.Connection);
					using (MySqlDataReader reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							// DRS:This will give us the name of the column itself
							string colName = Convert.ToString(reader["Field"]);

							// DRS:The Key column will be either PRI or MUL (are there other choices?)
							switch (Convert.ToString(reader["Key"]))
							{
								case "PRI":
									priKeyCols.Add(colName);
									break;
								case "MUL":
									mulKeyCols.Add(colName);
									break;
							}
						}
					}

					// DRS:Look through all of the comments, checking for ourselves and references to ourselves
					// DRS:This has to be done outside of the above because the call to new
					//	will eventually open the connection again and thus throw an exception
					foreach (string comment in comments)
					{
						// DRS:See if we're looking at the comment for this table
						if (comment.StartsWith(Name + "::"))
						{
							MatchCollection mc = rx.Matches(comment);
							foreach (Match m in mc)
							{
								// DRS:Here's where we parse out the foreign/primary tables and columns
								string fkColumn = m.Groups[1].Value;
								string pkDatabase = m.Groups[2].Value;
								string pkTable = m.Groups[3].Value;
								string pkColumn = m.Groups[4].Value;

								// DRS:We're only dealing with local foreign keys for now
								if (pkDatabase != Database.Name) continue;
								if (!mulKeyCols.Contains(fkColumn)) continue;

								// DRS:We have to make up our own name for this key, so we'll come
								//	up with something guaranteed to be unique and then we add it to our list
								string fkName = String.Format("FK_{0}{1}_{2}{3}",
									Name.Replace("_", ""), fkColumn.ToString().Replace("_", ""), pkTable.Replace("_", ""), pkColumn.Replace("_", ""));
								allKeys.Add(new Key(Database, fkName,
									new string[] { fkColumn.ToString() }, Name, new string[] { pkColumn }, pkTable));
							}
						}

						// DRS:We're looking to see if there's a reference to us in the comment
						else
						{
							MatchCollection mc = rx.Matches(comment);
							foreach (Match m in mc)
							{
								// DRS:We've kept the foreign key table name in the front, before ::
								string fkTable = comment.Substring(0, comment.IndexOf("::"));

								// DRS:Here's where we parse out the foreign/primary tables and columns
								string fkColumn = m.Groups[1].Value;
								string pkDatabase = m.Groups[2].Value;
								string pkTable = m.Groups[3].Value;
								string pkColumn = m.Groups[4].Value;

								// DRS:We're only dealing with local foreign keys for now
								if (pkDatabase != Database.Name) continue;
								if (pkTable != Name) continue;
								if (!priKeyCols.Contains(pkColumn)) continue;

								// DRS:We have to make up our own name for this key, so we'll come
								//	up with something guaranteed to be unique and then we add it to our list
								string pkName = String.Format("PK_{0}{1}_{2}{3}",
									fkTable.Replace("_", ""), fkColumn.ToString().Replace("_", ""), pkTable.Replace("_", ""), pkColumn.Replace("_", ""));
								allKeys.Add(new Key(Database, pkName,
									new string[] { fkColumn.ToString() }, fkTable, new string[] { pkColumn }, pkTable));
							}
						}
					}

					foreach (Key key in allKeys)
						_keys.Add(key);
				}
				return _keys; 
			}
			set { _keys = value; }
		}
		private DatabaseList<Column> _nonKeyColumns;
		[XmlIgnore]
		public DatabaseList<Column> NonKeyColumns
		{
			get 
			{
				if (_nonKeyColumns == null)
				{
					LoadColumns();
					_nonKeyColumns = new DatabaseList<Column>();
					foreach (Column column in Columns)
						if (!column.IsPrimaryKeyMember && !column.IsForeignKeyMember)
							_nonKeyColumns.Add(column.Name, column);
				}
				return _nonKeyColumns; 
			}
		}
		private PrimaryKey _primaryKey;
		[XmlIgnore]
		public PrimaryKey PrimaryKey
		{
			get 
			{
				if (_primaryKey == null)
				{
					LoadColumns();
					string sql = @"SHOW INDEX FROM " + Database.Quote(Name);
					// GKM - Should we change to OracleDataReader implementation?
					MySqlCommand cmd = new MySqlCommand(sql, Database.Connection);
					using (MySqlDataReader reader = cmd.ExecuteReader())
					{
						ArrayList memberCols = new ArrayList();
						while (reader.Read())
						{

							if ((string)reader["Key_name"] == "PRIMARY")
							{
								memberCols.Add(reader["Column_name"].ToString());
							}
						}
						if (memberCols.Count > 0)
						{

							_primaryKey = new PrimaryKey(this, "PRIMARY", (string[])memberCols.ToArray(typeof(string)));
						}
					}
				}

				return _primaryKey; 
			}
		}
		private DatabaseList<Column> _primaryKeyColumns;
		[XmlIgnore]
		public DatabaseList<Column> PrimaryKeyColumns
		{
			get
			{
				if (_primaryKeyColumns == null)
				{
					LoadColumns();
					_primaryKeyColumns = new DatabaseList<Column>();
					foreach (Column column in Columns)
						if (column.IsPrimaryKeyMember)
							_primaryKeyColumns.Add(column.Name, column);
				}
				return _primaryKeyColumns;
			}
		}
		private DatabaseList<Column> _nonPrimaryKeyColumns;
		[XmlIgnore]
		public DatabaseList<Column> NonPrimaryKeyColumns
		{
			get
			{
				if (_nonPrimaryKeyColumns == null)
				{
					LoadColumns();
					_nonPrimaryKeyColumns = new DatabaseList<Column>();
					foreach (Column column in Columns)
						if (!column.IsPrimaryKeyMember)
							_nonPrimaryKeyColumns.Add(column.Name, column);
				}
				return _nonPrimaryKeyColumns;
			}
		}

		protected void LoadColumns()
		{
			_columns = new DatabaseList<Column>();

			string sql = @"SHOW COLUMNS FROM " + Database.Quote(Name);

			MySqlCommand cmd = new MySqlCommand(sql, Database.Connection);
			using (MySqlDataReader reader = cmd.ExecuteReader())
			{
				while (reader.Read())
				{
					MySqlTypeInfo type = new MySqlTypeInfo((string)reader["Type"]);
					// DRS:Checking the extra column for "auto_increment"
					bool isIdentity = Convert.ToString(reader["Extra"]).IndexOf("auto_increment") >= 0;
					Column cs = new Column(
						this,
						(string)reader["Field"],
						type.DbType,
						type.NativeType,
						type.Length,
						type.Precison,
						0,
						((string)reader["Null"]).ToUpper() == "YES",
						// DRS:The MappingFile.cst template for Wilson's ORMapper at the least
						//	uses this to determine identity.  Don't know what others use.
						isIdentity
						//TODO: comments , reader.IsDBNull(POS_COMMENTS) ? string.Empty : reader.GetString( POS_COMMENTS)
						);

					_columns.Add(cs.Name, cs);
				}
			}
		}

		private string[] GetTableComments()
		{
			ArrayList comments = new ArrayList();

			// DRS:Note that SHOW TABLE STATUS is looking for a STRING, not an object
			//	name, hence we can't use the Quote function here.
			string sql = String.Format(@"SHOW TABLE STATUS");

			MySqlCommand cmd = new MySqlCommand(sql, Database.Connection);
			using (MySqlDataReader reader = cmd.ExecuteReader())
			{
				// DRS:The information we are looking for is in the Comment column
				while (reader.Read())
					comments.Add(Convert.ToString(reader["Name"]) + "::" + Convert.ToString(reader["Comment"]));
			}

			return (string[])comments.ToArray(typeof(string));
		}
	}

	public class Column
	{
		public Column(Table table, string name, DbType dataType, string nativeType, int size, byte precision, int scale, bool allowDBNull, bool isIdentity)
		{
			_database = table.Database;
			_table = table;
			_name = name;
			_dataType = dataType;
			_nativeType = nativeType;
			_size = size;
			_precision = precision;
			_scale = scale;
			_allowDBNull = allowDBNull;
			_isIdentity = isIdentity;
		}

		private Database _database;
		public Database Database
		{
			get { return _database; }
			set { _database = value; }
		}
		private Table _table;
		public Table Table
		{
			get { return _table; }
			set { _table = value; }
		}
		private string _name;
		public string Name
		{
			get { return _name; }
			set { _name = value; }
		}

		private bool _allowDBNull;
		public bool AllowDBNull
		{
			get { return _allowDBNull; }
			set { _allowDBNull = value; }
		}
		private DbType _dataType;
		public DbType DataType
		{
			get { return _dataType; }
			set { _dataType = value; }
		}
		private string _nativeType;
		public string NativeType
		{
			get { return _nativeType; }
			set { _nativeType = value; }
		}
		private byte _precision;
		public byte Precision
		{
			get { return _precision; }
			set { _precision = value; }
		}
		private int _scale;
		public int Scale
		{
			get { return _scale; }
			set { _scale = value; }
		}
		private int _size;
		public int Size
		{
			get { return _size; }
			set { _size = value; }
		}
		private bool _isIdentity;
		public bool IsIdentity
		{
			get { return _isIdentity; }
			set { _isIdentity = value; }
		}
		public bool IsForeignKeyMember
		{
			get
			{
				for (int i = 0; i < Table.ForeignKeys.Count; i++)
				{
					if (Table.ForeignKeys[i].ForeignKeyMemberColumns.ContainsKey(Name))
					{
						return true;
					}
				}
				return false;
			}
		}
		public bool IsUnique
		{
			get
			{
				for (int i = 0; i < Table.Indexes.Count; i++)
				{
					if ((Table.Indexes[i].IsUnique && (Table.Indexes[i].MemberColumns.Count == 1)) && Table.Indexes[i].MemberColumns.Contains(this))
					{
						return true;
					}
				}
				return false;
			}
		}
		public bool IsPrimaryKeyMember
		{
			get
			{
				if (Table.PrimaryKey != null)
				{
					return Table.PrimaryKey.MemberColumns.ContainsKey(Name);
				}
				return false;
			}
		}
	}

	public class Index
	{
		public Index(Table table, string name, bool isPrimaryKey, bool isUnique, bool isClustered)
		{
			_database = table.Database;
			_table = table;
			_name = name;
			_isPrimaryKey = isPrimaryKey;
			_isUnique = isUnique;
			_isClustered = isClustered;
		}

		private Database _database;
		public Database Database
		{
			get { return _database; }
			set { _database = value; }
		}
		private Table _table;
		public Table Table
		{
			get { return _table; }
			set { _table = value; }
		}
		private string _name;
		public string Name
		{
			get { return _name; }
			set { _name = value; }
		}

		private bool _isClustered;
		public bool IsClustered
		{
			get { return _isClustered; }
			set { _isClustered = value; }
		}
		private bool _isPrimaryKey;
		public bool IsPrimaryKey
		{
			get { return _isPrimaryKey; }
			set { _isPrimaryKey = value; }
		}
		private bool _isUnique;
		public bool IsUnique
		{
			get { return _isUnique; }
			set { _isUnique = value; }
		}

		private List<Column> _memberColumns;
		public List<Column> MemberColumns
		{
			get { return _memberColumns; }
			set { _memberColumns = value; }
		}
	}

	public class PrimaryKey
	{
		public PrimaryKey(Table table, string name, string[] memberColumns)
		{
			_database = table.Database;
			_table = table;
			_name = name;

			_memberColumns = new DatabaseList<Column>();
			foreach (string column in memberColumns)
				_memberColumns.Add(column, _table.Columns[column]);
		}

		private Database _database;
		public Database Database
		{
			get { return _database; }
			set { _database = value; }
		}
		private Table _table;
		public Table Table
		{
			get { return _table; }
			set { _table = value; }
		}
		private string _name;
		public string Name
		{
			get { return _name; }
			set { _name = value; }
		}

		private DatabaseList<Column> _memberColumns;
		public DatabaseList<Column> MemberColumns
		{
			get { return _memberColumns; }
			set { _memberColumns = value; }
		}
	}

	public class Key
	{
		public Key(Database database, string name, string[] foreignKeyMemberColumns, string foreignKeyTable, 
			string[] primaryKeyMemberColumns, string primaryKeyTable)
		{
			_database = database;
			_name = name;
			
			_foreignKeyTable = Database.Tables[foreignKeyTable];
			_foreignKeyMemberColumns = new DatabaseList<Column>();
			foreach(string column in foreignKeyMemberColumns)
				_foreignKeyMemberColumns.Add(column, _foreignKeyTable.Columns[column]);

			_primaryKeyTable = Database.Tables[primaryKeyTable];
			_primaryKeyMemberColumns = new DatabaseList<Column>();
			foreach(string column in primaryKeyMemberColumns)
				_primaryKeyMemberColumns.Add(column, _primaryKeyTable.Columns[column]);
		}

		private Database _database;
		public Database Database
		{
			get { return _database; }
			set { _database = value; }
		}
		private Table _table;
		public Table Table
		{
			get { return _table; }
			set { _table = value; }
		}
		private string _name;
		public string Name
		{
			get { return _name; }
			set { _name = value; }
		}

		private bool _isClustered;
		public bool IsClustered
		{
			get { return _isClustered; }
			set { _isClustered = value; }
		}
		private bool _isPrimaryKey;
		public bool IsPrimaryKey
		{
			get { return _isPrimaryKey; }
			set { _isPrimaryKey = value; }
		}
		private bool _isUnique;
		public bool IsUnique
		{
			get { return _isUnique; }
			set { _isUnique = value; }
		}
		private DatabaseList<Column> _foreignKeyMemberColumns;
		public DatabaseList<Column> ForeignKeyMemberColumns
		{
			get { return _foreignKeyMemberColumns; }
			set { _foreignKeyMemberColumns = value; }
		}
		private Table _foreignKeyTable;
		public Table ForeignKeyTable
		{
			get { return _foreignKeyTable; }
			set { _foreignKeyTable = value; }
		}
		private DatabaseList<Column> _primaryKeyMemberColumns;
		public DatabaseList<Column> PrimaryKeyMemberColumns
		{
			get { return _primaryKeyMemberColumns; }
			set { _primaryKeyMemberColumns = value; }
		}
		public Table _primaryKeyTable;
		public Table PrimaryKeyTable
		{
			get { return _primaryKeyTable; }
			set { _primaryKeyTable = value; }
		}
	}

	public class DatabaseList<TValue> : List<TValue>
	{
		public DatabaseList() { }

		protected Dictionary<string, TValue> _dict = new Dictionary<string, TValue>();

		public void Add(string key, TValue value)
		{
			base.Add(value);
			_dict.Add(key, value);
		}

		public bool ContainsKey(string key)
		{
			return _dict.ContainsKey(key);
		}

		public TValue this[string key]
		{
			get
			{
				return _dict[key];
			}
		}
	}

	public class MySqlTypeInfo
	{
		private string _type;
		private int _length = 0;
		private DbType _dbtype;
		private byte _precision = 0;

		public DbType DbType
		{
			get { return _dbtype; }
		}

		public string NativeType
		{
			get { return _type; }
		}

		public int Length
		{
			get { return _length; }
		}
		public byte Precison
		{
			get { return _precision; }

		}

		public MySqlTypeInfo(string nativeType)
		{
			string[] extinfo = nativeType.Split(new char[] { ' ' });
			string[] typeandsize = extinfo[0].Split(new char[] { '(', ')', ',' });

			if (extinfo[0].ToUpper().StartsWith("SET"))
			{
				_type = extinfo[0];
			}

			if (extinfo[0].ToUpper().StartsWith("ENUM"))
			{
				_type = extinfo[0];
			}

			if (_type == null)
			{
				_type = typeandsize[0];
				if (typeandsize.Length > 1)
				{
					_length = Int32.Parse(typeandsize[1]);
					if (typeandsize.Length == 4)
					{
						_precision = byte.Parse(typeandsize[2]);
					}

				}

			}

			_initDbType();
		}

		private void _initDbType()
		{
			switch (_type.ToUpper())
			{
				case "INT":
				case "TINYINT":
				case "SMALLINT":
				case "MEDIUMINT":
				case "TIMESTAMP":
				case "YEAR":
					if (_type.ToUpper() == "TINYINT" && _length == 1)
						_dbtype = DbType.Boolean;
					else
						_dbtype = DbType.Int32;
					break;
				case "BIGINT":
					_dbtype = DbType.Int64;
					break;
				case "FLOAT":
				case "DECIMAL":
					_dbtype = DbType.Decimal;
					break;
				case "DOUBLE":
					_dbtype = DbType.Double;
					break;
				case "VARCHAR":
				case "TEXT":
				case "CHAR":
				case "LONGTEXT":
				case "MEDIUMTEXT":
				case "SET":
				case "ENUM":
					_dbtype = DbType.String;
					break;
				case "DATETIME":
				case "DATE":
					_dbtype = DbType.DateTime;
					break;
				case "TIME":
					_dbtype = DbType.Time;
					break;
				default:
					_dbtype = DbType.Object;
					break;
			}

		}
	}
}
