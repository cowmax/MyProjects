﻿using MySql.Data.MySqlClient;
using MySqlAdapters;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;

namespace MeDataAdapters
{
    // 类：用于配置与 Trafficdb 数据库的公共参数
    public abstract class MeSqlDataAdapter : MySqlAdapter
    {
        // initialize MySql database (connection string) parameters
        protected override void initMySqlSettings()
        {
            // 从配置文件中读取 MySQL 数据库参数
            DBParam dbParam = LoadBimDBSetting(@"DBSetting.xml");
            db_host = dbParam.dbHost;
            db_port = dbParam.dbPort;
            db_name = dbParam.dbName;
            db_user = dbParam.userName;
            db_pass = dbParam.password;
            db_charset = dbParam.charset;
        }
        // 从XML文件中读取 MySQL 数据库配置
        public DBParam LoadBimDBSetting(string filePath)
        {
            DBParam dbParam = new DBParam();

            try
            {
                XElement root = XElement.Load(filePath);
                XElement xn = root.Element("active_setting");
                int idx = int.Parse(xn.Value);
                IEnumerable<XElement> xns = root.Elements("db");

                xn = xns.ElementAt(idx - 1);
                dbParam.userName = xn.Element("db_user").Value;
                dbParam.dbHost = xn.Element("db_host").Value;
                dbParam.dbPort = int.Parse(xn.Element("db_port").Value);
                dbParam.dbName = xn.Element("db_name").Value;
                dbParam.password = xn.Element("db_pass").Value;
                dbParam.charset = xn.Element("db_charset").Value;
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine(ioEx.Message);
            }

            return dbParam;
        }
        // 创建临时表
        public string CreateTempTable(string tableName, bool bFillData)
        {
            string tmpTableName = "tmp_" + tableName;
            string sql = string.Format("DROP TEMPORARY TABLE IF EXISTS {0}; CREATE TEMPORARY TABLE {0} ( SELECT * FROM {1} {2}) ",
                tmpTableName, tableName, bFillData ? "" : "limit 0");
            MySqlCommand sqlCmd = new MySqlCommand();
            sqlCmd.Connection = this.sqlConn;
            sqlCmd.CommandType = CommandType.Text;
            sqlCmd.CommandText = sql;

            int cnt = 0;
            try
            {
                cnt = sqlCmd.ExecuteNonQuery();
            }
            catch (SqlException ex)
            {
                Debug.WriteLine("CreateTempTable : " + ex.Message);
                tmpTableName = null;
            }

            return tmpTableName;
        }

        // 创建据有 Table-Schema 的 DataTable 实例
        public DataTable NewTable()
        {
            MySqlDataAdapter adpt = new MySqlDataAdapter("SELECT * FROM " + this.TableName, this.sqlConn);
            DataTable dt = new DataTable();
            adpt.FillSchema(dt, SchemaType.Mapped);

            return dt;
        }

        protected string get_guid()
        {
            return Guid.NewGuid().ToString("N");
        }

        protected DateTime get_date(string sDate)
        {
            DateTime d;
            DateTime.TryParse(sDate, out d);

            return d;
        }

        protected decimal get_decimal(string str, decimal defv)
        {
            decimal v = defv;
            decimal.TryParse(str, out v);
            return v;
        }

        protected object get_phone(string phone, string defs)
        {
            string s = defs;
            Regex rgx = new Regex(@"\d{2,4}\-{0,1}\d{4,8}", RegexOptions.Compiled);
            Match mch = rgx.Match(phone);
            if (mch != null && mch.Groups.Count > 0)
            {
                s = mch.Groups[0].Value;
            }

            return s;
        }

        protected decimal get_isaudit(string isaudit, int defv)
        {
            decimal v = defv;
            decimal.TryParse(isaudit, out v);
            return v;
        }

        protected string get_mobile(string mobile, string defs)
        {
            string s = defs;
            Regex rgx = new Regex(@"1\d{10}", RegexOptions.Compiled);
            Match mch = rgx.Match(mobile);
            if (mch != null && mch.Groups.Count > 0)
            {
                s = mch.Groups[0].Value;
            }

            return s;
        }
    }

    public class PhotographerAdapter : MeSqlDataAdapter
    {
        public int insert(string ptgphrName, string ptgphrId, string gender)
        {
            MySqlCommand insertCmd = new MySqlCommand();
            insertCmd.Connection = this.sqlConn;
            insertCmd.CommandText = "INSERT IGNORE INTO photographer(PhtgphrName, PhtgphrId, Gender) VALUE(@PhtgphrName, @PhtgphrId, @Gender)";
            insertCmd.Parameters.AddWithValue("@PhtgphrName", ptgphrName);
            insertCmd.Parameters.AddWithValue("@PhtgphrId", ptgphrId);
            insertCmd.Parameters.AddWithValue("@gender", gender);

            return insertCmd.ExecuteNonQuery();
        }

        protected override void initTableName()
        {
            _tableName = "photographer";
        }

        internal DataTable getAllPhotographers(bool notPrey)
        {
            int preyState = notPrey ? 0 : 1;
            string sql = string.Format("SELECT * FROM {0} WHERE preyState={1}", _tableName, preyState);
            MySqlCommand sqlCmd = new MySqlCommand(sql, this.sqlConn);

            DataTable dt = new DataTable();
            MySqlDataAdapter adapter = new MySqlDataAdapter(sqlCmd);
            adapter.Fill(dt);

            return dt;
        }

        internal int setPageNum(int pid, int  pageNum)
        {
            MySqlCommand sqlCmd = new MySqlCommand();
            sqlCmd.Connection = this.sqlConn;
            sqlCmd.CommandText = string.Format("UPDATE {0} SET PageNum=@pageNum WHERE PhtgphrId=@pid", _tableName);
            sqlCmd.Parameters.AddWithValue("@pid", pid);
            sqlCmd.Parameters.AddWithValue("@pageNum", pageNum);

            return sqlCmd.ExecuteNonQuery();
        }

        internal int setClientCount(int pid, int clientCount, bool bCompleted)
        {
            MySqlCommand sqlCmd = new MySqlCommand();
            sqlCmd.Connection = this.sqlConn;
            sqlCmd.CommandText = string.Format("UPDATE {0} SET ClientCount=@clientCount, PreyState=@preyState WHERE PhtgphrId=@pid", _tableName);
            int preyState = bCompleted ? 1 : 0;
            sqlCmd.Parameters.AddWithValue("@pid", pid);
            sqlCmd.Parameters.AddWithValue("@clientCount", clientCount);
            sqlCmd.Parameters.AddWithValue("@preyState", preyState);

            return sqlCmd.ExecuteNonQuery();
        }
    }

    public class ClientBaseInfoAdapter : MeSqlDataAdapter
    {
        protected override void initTableName()
        {
            _tableName = "ClientBaseInfo";
        }

        public DataTable getClientInfo(string sql)
        {
            this.dataTable.Clear();

            MySqlCommand sqlCmd = new MySqlCommand(sql, this.sqlConn);
            MySqlDataAdapter adpter = new MySqlDataAdapter(sqlCmd);

            adpter.Fill(this.dataTable);
            return this.dataTable;
        }

        internal DataTable getPendingClients()
        {
            this.dataTable.Clear();
            string sql = string.Format("SELECT * FROM ClientBaseInfo WHERE PreyState=0");
            MySqlCommand sqlCmd = new MySqlCommand(sql, this.sqlConn);
            MySqlDataAdapter adpter = new MySqlDataAdapter(sqlCmd);

            adpter.Fill(this.dataTable);
            return this.dataTable;
        }

        internal int insert(int userId, string userName, string realName, string city, string location,
            DateTime takeTime, DateTime activateTime, int photoPoint, string status, int ptgphrId)
        {
            MySqlCommand insertCmd = new MySqlCommand();
            insertCmd.Connection = this.sqlConn;
            insertCmd.CommandText = "INSERT IGNORE INTO ClientBaseInfo(UserId, UserName, RealName, City, "
                +"Location, TakeTime, ActivateTime, PhotoPoint, Status, PhotographerId) "
                + "VALUE(@UserId,@UserName,@RealName,@City,@Location,@TakeTime,@ActivateTime,@PhotoPoint,@Status,@PhotographerId)";
            insertCmd.Parameters.AddWithValue("@UserId", userId);
            insertCmd.Parameters.AddWithValue("@UserName", userName);
            insertCmd.Parameters.AddWithValue("@RealName", realName);
            insertCmd.Parameters.AddWithValue("@City", city);
            insertCmd.Parameters.AddWithValue("@Location", location);
            insertCmd.Parameters.AddWithValue("@TakeTime", takeTime);
            insertCmd.Parameters.AddWithValue("@ActivateTime", activateTime);
            insertCmd.Parameters.AddWithValue("@PhotoPoint", photoPoint);
            insertCmd.Parameters.AddWithValue("@Status", status);
            insertCmd.Parameters.AddWithValue("@PhotographerId", ptgphrId);

            return insertCmd.ExecuteNonQuery();
        }

        internal DataTable getAllClientBaseInfo(bool notPrey)
        {
            int preyState = notPrey ? 0 : 1;
            string sql = string.Format("SELECT * FROM {0} WHERE preyState={1}", _tableName, preyState);
            MySqlCommand sqlCmd = new MySqlCommand(sql, this.sqlConn);

            DataTable dt = new DataTable();
            MySqlDataAdapter adapter = new MySqlDataAdapter(sqlCmd);
            adapter.Fill(dt);

            return dt;
        }
    }

    public class ClientDetailInfoAdapter : MeSqlDataAdapter
    {
        protected override void initTableName()
        {
            _tableName = "clientdetailinfo";
        }

        internal int insert(int pid, int uid, string email, string mobile)
        {
            MySqlCommand insertCmd = new MySqlCommand();
            insertCmd.Connection = this.sqlConn;
            insertCmd.CommandText = string.Format("INSERT INTO {0}(PhotographerId, UserId, Email, Mobile)\n"
                                  + "VALUE(@PhotographerId, @UserId, @Email, @Mobile)\n"
                                  + "ON DUPLICATE KEY UPDATE {0}.Email=@Email, {0}.Mobile=@Mobile", _tableName);
            insertCmd.Parameters.AddWithValue("@UserId", uid);
            insertCmd.Parameters.AddWithValue("@PhotographerId", pid);
            insertCmd.Parameters.AddWithValue("@Email", email);
            insertCmd.Parameters.AddWithValue("@Mobile", mobile);

            return insertCmd.ExecuteNonQuery();
        }
    }

    public class SysUserAdapter : MeSqlDataAdapter
    {
        protected override void initTableName()
        {
            _tableName = "SysUser";
        }

        public DataTable getSysUser(string userName, string password)
        {
            this.dataTable.Clear();
            string sql = string.Format("SELECT * FROM SysUser WHERE UserName='{0}' AND PASSWORD='{1}' LIMIT 1", userName, password);
            MySqlDataAdapter adpater = new MySqlDataAdapter(sql, this.sqlConn);
            adpater.Fill(this.dataTable);

            return this.dataTable;
        }

        public DataTable getSysUser(int id)
        {
            _dtTable.Clear();
            string sql = string.Format("SELECT * FROM SysUser WHERE UserId={0} LIMIT 1", id);
            MySqlDataAdapter adpater = new MySqlDataAdapter(sql, this.sqlConn);
            adpater.Fill(this.dataTable);

            return this.dataTable;

        }
    }

    // 类：用于保存 MySQL 数据库连接参数的
    public class DBParam
    {
        public string userName;
        public string password;
        public string dbHost;
        public string dbName;
        public int dbPort;
        public string charset;
    }
}
