using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web;
using System.Linq;
using System.Text.RegularExpressions;
using Logging;

namespace MySqlAdapters
{
    public class Term
    {
        public string name;          // 字段名称，与数据库表字段一致
        public string paramName;     // 参数名称，通常格式为： @user_name
        public string[] paramNames; // 参数名称数组，通常格式为： @user_name，仅当条件表达式包含多个参数时有效
        public string cmprOp;        // 字段名与字段值之间的比较运算符
        public string connOp;        // 与其他条件连接的运算符

        // 准确的参数名
        public string strictParamName
        {
            get
            {
                return getStrictParamName(this.paramName);
            }
        }

        public Term(string n, string cp, string cn)
        {
            name = n;
            cmprOp = cp;
            connOp = cn;
        }
        /// <summary>
        /// 数据库查询、更新条件
        /// </summary>
        /// <param name="fdName">数据库字段名称</param>
        /// <param name="cmprOp">字段值比较操作符: '=', 'like', '>', '>=' </param>
        /// <param name="pmName">SQL语句中对应该字段的参数</param>
        /// <param name="connOp">与其他条件之间的连接操作符: and, or, ',' </param>
        public Term(string fdName, string cmprOp, string pmName, string connOp)
        {
            this.name = " " + fdName + " ";
            this.paramName = " " + pmName + " ";
            this.cmprOp = " " + cmprOp + " ";
            this.connOp = " " + connOp + " ";
        }

        /// <summary>
        /// 数据库查询、更新条件
        /// </summary>
        /// <param name="fdName">数据库字段名称</param>
        /// <param name="cmprOp">字段值比较操作符: '=', 'like', '>', '>=' </param>
        /// <param name="pmNames">SQL语句中对应该字段的参数</param>
        /// <param name="connOp">与其他条件之间的连接操作符: and, or, ',' </param>
        /// <param name="paramCount">pmNames 中包含的参数个数，例如 between 连接操作符就需要 2 个参数</param>
        public Term(string fdName, string cmprOp, string pmNames, string connOp, int paramCount)
        {
            this.name = " " + fdName + " ";
            this.paramName = pmNames;
            this.paramNames = parseParamName(pmNames,paramCount);
            this.cmprOp = " " + cmprOp + " ";
            this.connOp = " " + connOp + " ";
        }
        // 使用正则表达式解析SQL 参数名称
        private string[] parseParamName(string paramNames, int paramCount)
        {
            string[] pmz = new string[paramCount];
            Regex rgx = new Regex(@"\@[\d\w_]+", RegexOptions.Compiled);
            MatchCollection matches = rgx.Matches(paramNames);
            for(int i=0; i< matches.Count && i < paramCount; i++)
            {
                Match mch = matches[i];
                pmz[i] = mch.Groups[0].Value;
            }
            return pmz;
        }

        // 获取准确的参数名（按 @user_name 格式）
        private static string getStrictParamName(string pmName)
        {
            string strictParamName;
            Regex rgx = new Regex(@"\@[\d\w_]+", RegexOptions.Compiled);
            Match mch = rgx.Match(pmName);
            if (mch != null)
            {
                strictParamName = mch.Groups[0].Value;
            }
            else
            {
                strictParamName = pmName;
            }
            return strictParamName.Trim();
        }

        static public bool isValidField(string str)
        {
            if (str == null) return false;
            str = str.Trim(new char[] {' ', '%'});

            return (!string.IsNullOrWhiteSpace(str) && !string.IsNullOrEmpty(str) && !str.Equals("*"));
        }

        static public bool isValidField(DateTime dtm)
        {
            DateTime tmpDt = new DateTime();
            return (dtm > tmpDt);
        }

        static public bool isValidField(Nullable<DateTime> dtm)
        {
            if (dtm == null) return false;

            DateTime tmpDt = new DateTime();
            return (dtm > tmpDt);
        }

        static public bool isValidField(int i)
        {
            return (i >= 0);
        }

        static public bool isValidField(decimal? d)
        {
            return (d != null && d >= 0);
        }

        static public bool isValidField(Nullable<int> i)
        {
            if (i == null) return false;
            return (i >= 0);
        }

        static public bool isValidField(object val)
        {
            if (val == null)
            {
                return false;
            }

            if (val.GetType() == Type.GetType("System.String"))
            {
                string s = (string)val;
                return isValidField(s);
            }

            if (val.GetType() == Type.GetType("System.DateTime"))
            {
                DateTime dt = (DateTime)val;
                return isValidField(dt);                    
            }

            return true;
        }

        public static DateTime getDateTime(string sDt, int dtType=0)
        {
            DateTime dt = new DateTime();
            DateTime tmpDt = new DateTime();
            if (DateTime.TryParse(sDt, out dt))
            {
                switch (dtType)
                {
                    case 0:
                        tmpDt = new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0);
                        break;
                    case 1:
                        tmpDt = new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59);
                        break;
                    case 2:
                        tmpDt = dt;
                        break;
                    default :
                        tmpDt = dt;
                        break;
                }
            }

            return tmpDt;
        }
    }

    public class TermList
    {
        private List<Term> _terms = new List<Term>();
        private List<MySqlParameter> _params = new List<MySqlParameter>();

        public bool isEmpty()
        {
            return (_terms.Count > 0);
        }

        public List<MySqlParameter> Params
        {
            get { return _params; }
        }
        public List<Term> Terms
        {
            get { return _terms; }
        }

        public bool Add(Term t, string val)
        {
            bool bSucc = false;
            if (Term.isValidField(val))
            {
                _terms.Add(t);
                _params.Add(new MySqlParameter(t.strictParamName, val));
                bSucc = true;
            }

            return bSucc;
        }

        public bool Add(Term t, DateTime? val)
        {
            bool bSucc = false;
            if (Term.isValidField(val))
            {
                _terms.Add(t);
                _params.Add(new MySqlParameter(t.paramName.Trim(), val));
                bSucc = true;
            }

            return bSucc;
        }
        // 添加多个SQL参数的值
        public bool Add(Term t, object[] valz)
        {
            bool bSucc = false;
            int cnt = valz.Length;
            for (int i = 0; i < valz.Length; i++)
            {
                // 从 paramNames 中取SQL参数名称
                if (Term.isValidField(valz[i]))
                {
                    _params.Add(new MySqlParameter(t.paramNames[i], valz[i]));
                    cnt--;
                }
            }
            bSucc = (cnt == 0);

            if (bSucc){
                 _terms.Add(t);
            }

            return bSucc;
        }

        public bool Add(Term t, int? val)
        {
            bool bSucc = false;
            if (Term.isValidField(val))
            {
                _terms.Add(t);
                _params.Add(new MySqlParameter(t.paramName.Trim(), val)); // N.B. Must trim the whitespace character.
                bSucc = true;
            }

            return bSucc;
        }

        // 把参数集合复制到给定的参数集合中
        public int CopyToParam(MySqlParameterCollection pm)
        {
            pm.Clear();
            foreach (MySqlParameter p in _params)
            {
                pm.Add(p);
            }
            return pm.Count;
        }
        // 把条件集合转换成字符串，可用于SQL 语句
        public string ConverToString()
        {
            string str = " "; // N.B. There is a space character.

            foreach (Term t in _terms)
            {
                if (!string.IsNullOrWhiteSpace(str))
                {
                    str += t.connOp;
                }
                str += t.name + t.cmprOp + t.paramName;
            }
            return str;
        }
    }

    #region Classes for build SQL statement
    public abstract class SqlBuilder    // 用于构造 SQL 语句的类
    {
        protected string _tblName;
        protected StringBuilder _strBuilder = new StringBuilder();
        protected bool hasField = false;

        protected SqlBuilder(string tblName)
        {
            _tblName = tblName;
        }

        public virtual void AppendField<T>(string name, T val)
        {

        }

        public virtual void AppendField(string name, object val, Type tp)
        {

        }

        public virtual void AppendField(string connOp, string fdName, string cmpOp, string fdVal)
        {
            fdVal = fdVal.Trim();
            if (!string.IsNullOrEmpty(fdVal))
            {
                throw(new NotImplementedException());
            }
        }

        public virtual void AppendClause(string clause)
        {
            _strBuilder.Append(" ");
            _strBuilder.Append(clause);
        }

        public void AppendField(string fName, string op, DateTime fValue)
        {
            _strBuilder.Append(" ");
            _strBuilder.Append(fName);
            _strBuilder.Append(" ");
            _strBuilder.Append(op);
            _strBuilder.Append(" '");
            _strBuilder.Append(fValue.ToString("yyyy-mm-dd hh:MM:ss"));
            _strBuilder.Append("' ");
        }

        public void AppendOp(string op)
        {
            _strBuilder.Append(" ");
            _strBuilder.Append(op);
            _strBuilder.Append(" ");
        }

        public override string ToString()
        {
            return _strBuilder.ToString();
        }
    }

    public class UpdateSqlBuilder : SqlBuilder
    {
        public UpdateSqlBuilder(string tblName)
            : base(tblName)
        {
            _strBuilder.Append("UPDATE ");
            _strBuilder.Append(_tblName);
            _strBuilder.Append(" SET ");
        }

        public override void AppendField<T>(string name, T val)
        {
            if (hasField)
            {
                _strBuilder.Append(", ");
            }

            if (val != null)
            {
                _strBuilder.Append(name);
                _strBuilder.Append("=");
                _strBuilder.Append(val);
                hasField = true;
            }
        }

        public override void AppendField(string name, object val, Type tp)
        {
            if (hasField)
            {
                _strBuilder.Append(", ");
            }

            if (val != null)
            {
                _strBuilder.Append(name);
                _strBuilder.Append("=");

                // 如果是 String 或者 DateTime 类型，需要为值增加引号
                if (tp == Type.GetType("System.String") ||
                    tp == Type.GetType("System.DateTime"))
                {
                    _strBuilder.Append("\"");
                    _strBuilder.Append(val);
                    _strBuilder.Append("\"");
                }
                else
                {
                    _strBuilder.Append(val);
                }

                hasField = true;
            }
        }

        public void AppendField(string name, String val)
        {
            if (hasField)
            {
                _strBuilder.Append(", ");
            }

            if (val != null)
            {
                _strBuilder.Append(name);
                _strBuilder.Append("=");
                _strBuilder.Append("\"");
                _strBuilder.Append(val);
                _strBuilder.Append("\"");
                hasField = true;
            }
        }
    }

    public class InsertSqlBuilder : SqlBuilder
    {
        StringBuilder _colBuilder = new StringBuilder();
        StringBuilder _valBuilder = new StringBuilder();
        StringBuilder _cluBuilder = new StringBuilder();
        List<string> _Values = new List<string>();

        bool _hasCol = false;
        bool _hasVal = false;

        public InsertSqlBuilder(string tblName)
            : base(tblName)
        {
            _strBuilder.Append("INSERT IGNORE INTO ");
            _strBuilder.Append(_tblName);
        }

        public InsertSqlBuilder(string cmd, string tblName)
            : base(tblName)
        {
            _strBuilder.Append(cmd);
            _strBuilder.Append(_tblName);
        }

        public override void AppendClause(string clause)
        {
            _cluBuilder.Append(" ");
            _cluBuilder.Append(clause);
        }
        public override void AppendField(string name, object val, Type tp)
        {
            if (_hasCol)
            {
                _colBuilder.Append(", ");
            }
            if (_hasVal)
            {
                _valBuilder.Append(", ");
            }

            _colBuilder.Append(name);
            _hasCol = true;

            // 如果是 String 或者 DateTime 类型，需要为值增加引号
            if (tp == Type.GetType("System.String"))
            {
                _valBuilder.Append("\"");
                _valBuilder.Append(val);
                _valBuilder.Append("\"");
            }
            else if (tp == Type.GetType("System.DateTime"))
            {
                _valBuilder.Append("\"");
                _valBuilder.Append(((DateTime)val).ToString("yyy-MM-dd hh:mm:ss"));
                _valBuilder.Append("\"");
            }
            else
            {
                _valBuilder.Append(val);
            }
            _hasVal = true;
        }

        internal void AppendColumns(DataColumnCollection Colms)
        {
            foreach (DataColumn col in Colms)
            {
                if (_hasCol)
                {
                    _colBuilder.Append(", ");
                }
                _colBuilder.Append(col.ColumnName);
                _hasCol = true;
            }
        }
        internal void AppendValue(object val)
        {
            if (_hasVal)
            {
                _valBuilder.Append(", ");
            }

            // 如果是 String 或者 DateTime 类型，需要为值增加引号
            if (val.GetType() == Type.GetType("System.String"))
            {
                _valBuilder.Append("\"");
                _valBuilder.Append(val);
                _valBuilder.Append("\"");
            }
            else if (val.GetType() == Type.GetType("System.DateTime"))
            {
                _valBuilder.Append("\"");
                _valBuilder.Append(((DateTime)val).ToString("yyyy-MM-dd hh:mm:ss"));
                _valBuilder.Append("\"");
            }
            else if (Convert.IsDBNull(val)) // insert a null value to MySql
            {
                _valBuilder.Append("null");
            }
            else
            {
                _valBuilder.Append(val);
            }
            _hasVal = true;
        }

        internal void AppendValues(DataRow row)
        {
            bool hasValues = false;

            if (hasValues)
            {
                AppendTag(", ");
            }

            AppendTag("(");
            foreach (DataColumn dtCol in row.Table.Columns)
            {
                AppendValue(row[dtCol.Ordinal]);
            }
            AppendTag(")");
            hasValues = true;
        }

        internal void AppendTag(string val)
        {
            _valBuilder.Append(val);
        }

        public string Debug_ToString()
        {
            _strBuilder.Append("(");
            _strBuilder.Append(_colBuilder);
            _strBuilder.Append(")");

            _strBuilder.Append(" VALUES");
            _strBuilder.Append(_valBuilder);

            _strBuilder.Append(_cluBuilder);

            return _strBuilder.ToString();
        }

        // Generate insert-sql2 command string
        public override string ToString()
        {
            _strBuilder.Append("(");
            _strBuilder.Append(_colBuilder);
            _strBuilder.Append(")");

            _strBuilder.Append(" VALUES");
            _strBuilder.Append(_valBuilder);

            _strBuilder.Append(_cluBuilder);

            // Calls the base class' function
            return base.ToString();
        }

        internal void BeginValues()
        {
            _hasVal = false;
        }

        internal void EndValues()
        {
            _hasVal = true;
        }

        internal void ClearValues()
        {
            _valBuilder.Clear();
        }
    }

    public class DeleteSqlBuilder : SqlBuilder
    {
        public DeleteSqlBuilder(string tblName)
            : base(tblName)
        {
            _strBuilder.Append("DELETE FROM ");
            _strBuilder.Append(_tblName);
        }
    }

    public class SelectBuilder : SqlBuilder
    {
        public SelectBuilder(string tblName)
            : base(tblName)
        {
            _strBuilder.Append("SELECT ");
            _strBuilder.Append(" FROM ");
            _strBuilder.Append(tblName);
        }

        public void AppendField(string fldName)
        {
            _strBuilder.Insert(8, " ");
            _strBuilder.Insert(8, fldName);
        }
    }
    #endregion

    public abstract class MySqlAdapter : IDisposable
    {
        #region 数据库参数
        protected static string db_host;
        protected static int db_port;
        protected static string db_name;
        protected static string db_user;
        protected static string db_pass;
        protected static string db_charset;
        #endregion 数据库参数

        protected DataTable _dtTable;
        protected string _tableName;

        public DataTable dataTable
        {
            get
            {
                if (_dtTable == null)
                {
                    _dtTable = new DataTable();
                }
                return _dtTable;
            }
        }

        protected abstract void initMySqlSettings();
        protected abstract void initTableName();/*{
            _tableName = tblName; // Set the table-name for current table-adapter
        }*/

        #region MySQL 访问所需基础成员及函数，复用时构造函数名称需要修改
        static int m_openedCount = 0;
        private string m_connString = null;
        private MySqlConnection m_sqlConn = null;

        public MySqlAdapter()
        {
            initTableName();
            initMySqlSettings();
            initDb();
        }

        ~MySqlAdapter()
        {
            // N.B. Destructors cannot be called. They are invoked automatically
            closeDB(); // Release the MySQL connection resource
        }

        // Property : DB Connection 
        public MySqlConnection sqlConn
        {
            get
            {
                initMySqlSettings();
                initDb();
                return m_sqlConn;
            }
        }

        private string getConnectionStr()
        {
            return connStringBuilder(db_host, db_port, db_name, db_user, db_pass, db_charset);
        }

        private string connStringBuilder(string host, int port, string dbname, string username, string password, string charset)
        {
            string cs = string.Format("Server={0};Port={1};Database={2};Uid={3};Pwd={4};Charset={5};Pooling=True;Allow Zero Datetime=True",
                host, port, dbname, username, password, charset);
            return cs;
        }

        private bool initDb()
        {
            bool result = true;
            try
            {
                if (m_sqlConn == null) // Open DB connection when it is null
                {
                    m_connString = connStringBuilder(db_host, db_port, db_name, db_user, db_pass, db_charset);
                    m_sqlConn = new MySqlConnection(m_connString);
                }

                if (m_sqlConn.State != ConnectionState.Open)// Open the DB connection when it is not OPEN
                {
                    m_sqlConn.Open();
                    m_openedCount++;

                    LogHelper.info(string.Format("Open MySQL DB ({0}) connection successfully.[ConnectionState.Open]", m_openedCount));
                }
            }
            catch (MySqlException e)
            {
                result = false;

                m_sqlConn.Close();
                m_sqlConn = null;

                LogHelper.error("initDb : " + e.Message);
            }

            return result;
        }

        public void closeDB()
        {
            if (m_sqlConn != null)
            {
                m_sqlConn.Close();
                m_sqlConn = null;

                m_openedCount--;
                LogHelper.info(string.Format("Closed MySQL DB ({0}) connection successfully.", m_openedCount));
            }
        }

        #endregion

        public delegate DataTable FillTableDelegate(MySqlDataReader sqlReader);

        public string TableName
        {
            get { return _tableName; }
            set { _tableName = value; }
        }

        // 验证字段的有效性
        protected bool isValidField(object val)
        {
            if (val == null)
            {
                return false;
            }

            if (val.GetType() == Type.GetType("System.String"))
            {
                string s = (string)val;
                s = s.Trim();
                if (s.Length == 0)
                {
                    return false;
                }
            }

            if (val.GetType() == Type.GetType("System.DateTime"))
            {
                DateTime dt = (DateTime)val;

                if (dt.Year < 1900)
                {
                    return false;
                }
            }

            return true;
        }

        private FillTableDelegate _fillTableDelegate = null;

        public FillTableDelegate FillTableHandler
        {
            get { return _fillTableDelegate; }
            set { _fillTableDelegate = value; }
        }

        // 派生类中需实现此函数，将 SqlReader 中的数据填写到特定类型的 DataTable 实例
        // public abstract int fillTable(DataTable dtTable, MySqlDataReader sqlReader);
        protected virtual int fillTable(DataTable dtTable, MySqlDataReader sqlReader)
        {
            DataRow dtRow = null;
            int iCol = -1;

            while (sqlReader.Read()) // 读取所有记录
            {
                dtRow = dtTable.NewRow();
                foreach (DataColumn dtCol in dtTable.Columns)
                {
                    iCol = sqlReader.GetOrdinal(dtCol.ColumnName);

                    if (iCol >= 0)
                    {
                        dtRow[dtCol.ColumnName] = sqlReader[iCol];
                    }
                }

                dtTable.Rows.Add(dtRow);
            }

            return dtTable.Rows.Count;
        }

        protected virtual DataTable getDataTable(MySqlDataReader sqlReader)
        {
            DataTable dataTable = new DataTable();
            DataRow dtRow = null;

            // Add columns accrodding to schema info
            DataTable schm = sqlReader.GetSchemaTable();
            foreach (DataRow sr in schm.Rows)
            {
                dataTable.Columns.Add(sr["ColumnName"].ToString(), Type.GetType(sr["DataType"].ToString()));
            }

            while (sqlReader.Read()) // 读取所有记录
            {

                dtRow = dataTable.NewRow();
                for (int i = 0; i < sqlReader.FieldCount; i++)
                {
                    dtRow[i] = sqlReader[i];
                }

                dataTable.Rows.Add(dtRow);
            }

            return dataTable;
        }

        protected DataTable GetDataEx(MySqlCommand sqlCmd)
        {
            MySqlDataAdapter adpt = new MySqlDataAdapter();
            adpt.SelectCommand = sqlCmd;
            DataTable dt = new DataTable();

            try
            {
                adpt.Fill(dt);
            }
            catch (MySqlException msqlEx)
            {
                Debug.WriteLine("MySqlAdapter.GetDataEx : " + msqlEx.Message);
            }

            return dt;
        }

        public DataTable Fill(DataTable dt)
        {
            if (_dtTable == null)
            {
                _dtTable = new DataTable();
            }
            throw new NotImplementedException();
        }

        // 根据给定查询条件（SQL）返回数据，保存在 DataTable 对象里
        public DataTable GetDataEx(string sql = null)
        {
            if (string.IsNullOrEmpty(sql))
            {
                sql = "select * from " + _tableName;
            }
            MySqlCommand sqlCmd = new MySqlCommand();
            sqlCmd.Connection = this.sqlConn;
            sqlCmd.CommandType = CommandType.Text;
            sqlCmd.CommandText = sql;

            return GetDataEx(sqlCmd);
        }

        public DataTable Find(string fldName, string fldValue)
        {
            try
            {
                // Build select text
                SelectBuilder sqlBuilder = new SelectBuilder(_tableName);

                sqlBuilder.AppendField("*");
                sqlBuilder.AppendClause(string.Format(" WHERE {0}='{1}'", fldName, fldValue));

                string cmdText = sqlBuilder.ToString();
                Debug.WriteLine("MySqlAdapter.find SQL : " + cmdText);
                // Create Command object
                MySqlCommand cmdSql = new MySqlCommand(cmdText, sqlConn);
                cmdSql.CommandType = CommandType.Text;

                // Execute SQL Command 
                MySqlDataReader sqlReader = cmdSql.ExecuteReader();

                if (_fillTableDelegate == null)
                {
                    fillTable(_dtTable, sqlReader);
                }
                else
                {
                    this._dtTable = _fillTableDelegate(sqlReader);
                }

                sqlReader.Close(); // 必须关闭
                cmdSql = null;
                sqlReader = null;
            }
            catch (MySqlException ex)
            {
                LogHelper.error("MySqlAdapter.find : " + ex.Message);
            }

            return this._dtTable;
        }

        public DataTable Find(string where_clause)
        {
            try
            {
                // Build select text
                SelectBuilder sqlBuilder = new SelectBuilder(_tableName);

                sqlBuilder.AppendField("*");
                sqlBuilder.AppendClause(where_clause);

                string cmdText = sqlBuilder.ToString();
                Debug.WriteLine("MySqlAdapter.find SQL : " + cmdText);
                // Create Command object
                MySqlCommand cmdSql = new MySqlCommand(cmdText, sqlConn);
                cmdSql.CommandType = CommandType.Text;

                // Execute SQL Command 
                MySqlDataReader sqlReader = cmdSql.ExecuteReader();

                if (_fillTableDelegate == null)
                {
                    fillTable(_dtTable, sqlReader);
                }
                else
                {
                    this._dtTable = _fillTableDelegate(sqlReader);
                }

                sqlReader.Close(); // 必须关闭
                cmdSql = null;
                sqlReader = null;
            }
            catch (MySqlException ex)
            {
                LogHelper.error("MySqlAdapter.find : " + ex.Message);
            }

            return this._dtTable;
        }

        public int Delete(string where_clause)
        {
            int effCount = 0;
            try
            {
                // Build DELETE text
                DeleteSqlBuilder sqlBuilder = new DeleteSqlBuilder(_tableName);
                sqlBuilder.AppendClause(where_clause);

                string cmdText = sqlBuilder.ToString();
                Debug.WriteLine("Delete SQL :" + cmdText);

                // Create Command object
                MySqlCommand cmdSql = new MySqlCommand(cmdText, sqlConn);
                cmdSql.CommandType = CommandType.Text;

                // Execute SQL Command 
                effCount = cmdSql.ExecuteNonQuery();

                cmdSql = null;
            }
            catch (MySqlException ex)
            {
                LogHelper.error("MySqlAdapter.Delete : " + ex.Message);
            }

            return effCount;
        }

        public int Insert(DataRow row)
        {
            int effCount = 0;
            try
            {
                // Build insert text
                InsertSqlBuilder sqlBuilder = new InsertSqlBuilder(_tableName);

                sqlBuilder.AppendTag("(");
                foreach (DataColumn dtCol in row.Table.Columns)
                {
                    if (!Convert.IsDBNull(row[dtCol.Ordinal]) && !dtCol.AutoIncrement)
                    {
                        Type tp = row[dtCol.Ordinal].GetType();
                        sqlBuilder.AppendField(dtCol.ColumnName, row[dtCol.Ordinal], tp);
                    }
                }
                sqlBuilder.AppendTag(")");

                string cmdText = sqlBuilder.ToString();
                Debug.WriteLine("Insert SQL : " + cmdText);

                // Create Command object
                MySqlCommand cmdSql = new MySqlCommand(cmdText, sqlConn);
                cmdSql.CommandType = CommandType.Text;

                // Execute SQL Command 
                effCount = cmdSql.ExecuteNonQuery();

                cmdSql = null;
            }
            catch (MySqlException ex)
            {
                LogHelper.error("MySqlAdapter.Insert : " + ex.Message);
            }

            return effCount;
        }

        public int Insert(DataTable dataTable, int batchSize, string clause = null)
        {
            int effCount = 0;
            int rowCnt = dataTable.Rows.Count;
            int rowIdx = 0;
            for (int leftCnt = rowCnt; leftCnt > 0; leftCnt -= batchSize)
            {
                // Build insert text
                InsertSqlBuilder sqlBuilder = new InsertSqlBuilder(_tableName);
                // Append columns
                sqlBuilder.AppendColumns(dataTable.Columns);

                // Append rows
                for (int valsCnt = 0; (rowIdx < rowCnt) && (valsCnt < batchSize); valsCnt++, rowIdx++)
                {
                    sqlBuilder.BeginValues();
                    if (valsCnt > 0)
                    {
                        sqlBuilder.AppendTag(", ");
                    }

                    sqlBuilder.AppendTag("(");
                    foreach (DataColumn dtCol in dataTable.Columns)
                    {
                        sqlBuilder.AppendValue(dataTable.Rows[rowIdx][dtCol.Ordinal]);
                    }
                    sqlBuilder.AppendTag(")");
                    sqlBuilder.EndValues();
                }

                if (clause != null)
                {
                    sqlBuilder.AppendClause(clause);
                }
                string cmdText = sqlBuilder.ToString();
                Debug.WriteLine("Insert SQL : " + cmdText);
                try
                {
                    // Create Command object
                    MySqlCommand cmdSql = new MySqlCommand(cmdText, sqlConn);
                    cmdSql.CommandType = CommandType.Text;

                    // Execute SQL Command 
                    effCount += cmdSql.ExecuteNonQuery();

                    cmdSql = null;
                }
                catch (MySqlException ex)
                {
                    LogHelper.error(string.Format("MySqlAdapter.Insert : {0} [rowIdx={1}]", ex.Message, rowIdx));
                }
            } // END : for (int leftCnt ...

            return effCount;
        }

        // 从给定 DataTable 中导入数据到数据库
        public int Import(DataTable dtTable, int batchSize, string clause = null)
        {
            int count = 0;

            count = Insert(dtTable, batchSize, clause);

            return count;
        }

        public int Update(DataRow row, string where_clause)
        {
            int effCount = 0;
            try
            {
                // Build select text
                UpdateSqlBuilder sqlBuilder = new UpdateSqlBuilder(_tableName);

                foreach (DataColumn dtCol in row.Table.Columns)
                {
                    if (!Convert.IsDBNull(row[dtCol.Ordinal]) && !dtCol.AutoIncrement)
                    {
                        Type tp = row[dtCol.Ordinal].GetType();
                        sqlBuilder.AppendField(dtCol.ColumnName, row[dtCol.Ordinal], tp);
                    }
                }

                sqlBuilder.AppendClause(where_clause);

                string cmdText = sqlBuilder.ToString();
                Debug.WriteLine("Update SQL : " + cmdText);

                // Create Command object
                MySqlCommand cmdSql = new MySqlCommand(cmdText, sqlConn);
                cmdSql.CommandType = CommandType.Text;

                // Execute SQL Command 
                effCount = cmdSql.ExecuteNonQuery();

                cmdSql = null;
            }
            catch (MySqlException ex)
            {
                LogHelper.error("MySqlAdapter.Update : " + ex.Message);
            }

            return effCount;
        }

        // Insert rows to database, when there is conflicted row update its all fields
        public int InsertReplace(DataTable dataTable, string keyFieldName, int batchSize, string clause = null)
        {
            int effCount = 0;
            int rowCnt = dataTable.Rows.Count;
            int rowIdx = 0;
            for (int leftCnt = rowCnt; leftCnt > 0; leftCnt -= batchSize)
            {
                // Build insert text
                InsertSqlBuilder sqlBuilder = new InsertSqlBuilder("REPLACE INTO ", _tableName);
                // Append columns
                sqlBuilder.AppendColumns(dataTable.Columns);

                // Append rows
                for (int valsCnt = 0; (rowIdx < rowCnt) && (valsCnt < batchSize); valsCnt++, rowIdx++)
                {
                    sqlBuilder.BeginValues();
                    if (valsCnt > 0)
                    {
                        sqlBuilder.AppendTag(", ");
                    }

                    sqlBuilder.AppendTag("(");
                    foreach (DataColumn dtCol in dataTable.Columns)
                    {
                        sqlBuilder.AppendValue(dataTable.Rows[rowIdx][dtCol.Ordinal]);
                    }
                    sqlBuilder.AppendTag(")");
                    sqlBuilder.EndValues();
                }

                if (clause != null)
                {
                    sqlBuilder.AppendClause(clause);
                }
                string cmdText = sqlBuilder.ToString();
                Debug.WriteLine("Insert SQL : " + cmdText);
                try
                {
                    // Create Command object
                    MySqlCommand cmdSql = new MySqlCommand(cmdText, sqlConn);
                    cmdSql.CommandType = CommandType.Text;

                    // Execute SQL Command 
                    effCount += cmdSql.ExecuteNonQuery();

                    cmdSql = null;
                }
                catch (MySqlException ex)
                {
                    LogHelper.error(string.Format("MySqlAdapter.Insert : {0} [rowIdx={1}]", ex.Message, rowIdx));
                }
            } // END : for (int leftCnt ...

            return effCount;
        }

        public virtual void Dispose()
        {
            if (this._dtTable != null) this._dtTable.Dispose();
            if (this.m_sqlConn != null)  this.m_sqlConn.Dispose();
        }
    }

    public class FieldNameMapping :IEnumerable<KeyValuePair<string, string>>
    {
        // Dictionary : field-name --> field-val
        protected Dictionary<string, string> _fnMap = null;
        protected KeyValuePair<string, string> _defaultMap;
        // Constructor
        public FieldNameMapping()
        {
            _fnMap = new Dictionary<string, string>();
        }
        public FieldNameMapping(Dictionary<string, string> fnMap)
        {
            _fnMap = fnMap;
        }

        // field-name element in dictionary
        public int Length
        {
            get { return _fnMap.Count; }
        }
        public void addItem(string fieldName, string alias)
        {
            _fnMap.Add(fieldName, alias);
        }
        public void addDefaultItem(string key, string val)
        {
            _defaultMap = new KeyValuePair<string, string>(key, val);
        }
        public string getKey(string val)
        {
            string fieldName = null;
            if (_fnMap.ContainsValue(val))
            {
                fieldName = _fnMap.FirstOrDefault(x => x.Value.Equals(val)).Key;
            }
            else
            {
                if (!string.IsNullOrEmpty(_defaultMap.Key))
                {
                    fieldName = _defaultMap.Key; // set to default value
                }
                else
                {
                    fieldName = val;
                }
            }
            return fieldName;
        }

        // Get field-name of give val
        public string getFieldName(string alias)
        {
            return getKey(alias);
        }

        public string getValue(string key)
        {
            string val = null;
            if (_fnMap.ContainsKey(key))
            {
                val = _fnMap[key];
            }
            else
            {
                if (!string.IsNullOrEmpty(_defaultMap.Value))
                {
                    val = _defaultMap.Value;
                }
                else
                {
                    val = key;
                }
            }

            return val;            
        }
        // Get val of given field
        public string getAliasName(string field)
        {
            return getValue(field);
        }
        // Try to get Alias, if no val is found, return field-name
        public string tryGetAlais(string field)
        {
            string alias = null;
            if (_fnMap.ContainsKey(field))
            {
                alias = _fnMap[field];
            }
            else
            {
                alias = field;
            }
            return alias;
        }

        public string this[string key]
        {
            get
            {
                return getValue(key);
            }
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (KeyValuePair<string, string> map in _fnMap)
            {
                yield return map;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

}
