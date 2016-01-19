using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Logging;
using MeDataAdapters;
using MyNetwork;
using ClientPreyer.Net;
using System.IO;

namespace ClientPreyer
{
    class ThreadMgr
    {
        Properties.Settings _appSetting = new Properties.Settings();
        private List<int> _clientIdList = new List<int>();
        private List<AttendanceFileInfo> _atndFiles = new List<AttendanceFileInfo>();

        private static string _referer;
        private MyWebClient _wc;
        public bool isLogin;

        // Get the only instance of WebClient 
        public MyWebClient getWebClient(string method = "POST", string refUrl = null)
        {
            if (_wc == null)
            {
                _wc = new MyWebClient();
            }

            if (refUrl != null)
                _referer = refUrl;

            // 设置 Headers，发出请求报文后会丢失，因此必须重新设置
            if (method.Equals("POST", StringComparison.CurrentCultureIgnoreCase))
            {
                setPostRequestHeaders(_wc, _referer);
            }
            else
            {
                setGetRequestHeaders(_wc, refUrl);
            }

            return _wc;
        }

        // 向服务器请求生成考勤数据文件
        internal bool generateAttFile()
        {
            bool bSucc = false;
            string refUrl = "http://web.jingoal.com/attendance/attendance/web/index.jsp?locale=zh_CN&_t=1453189807391";
            string trgUrl = "http://web.jingoal.com/attendance/attendance/v2/export/attend_export.do?deptId=-1&toTime=1453046400000&fromTime=1451577600000&exportTables=11111";

            try
            {
                MyWebClient wc = getWebClient("post", refUrl);

                CookieCollection ckiColn = new CookieCollection();

                ckiColn.Add(new Cookie("JINSESSIONID", "d98461f8-d0d8-4c1a-8fc7-1210a7d7c571"));

                wc.addCookies(new Uri(trgUrl), ckiColn);

#if DEBUG
                string rspData = "{\"meta\":{\"code\":0,\"message\":\"\"},\"data\":null}";
#else
                string rspData = wc.DownloadString(trgUrl);
#endif
                GenerateAttFileResult rsl = new GenerateAttFileResult(rspData);

                bSucc = rsl.IsSucc;
            }
            catch (WebException wex)
            {
                LogHelper.error(wex.Message);
            }

            return bSucc;
        }

        internal void importAttDataToDB()
        {
            
        }

        internal void downloadAttFiles(string folder)
        {
            string folderPath = getAttFolderPath(folder);
            string refUrl = _appSetting.refererUrl;
            if (folderPath != null)
            {
                MyWebClient wc = getWebClient("POST", refUrl);

                // 下载文件
                if (_atndFiles != null)
                {
                    foreach (AttendanceFileInfo afi in _atndFiles)
                    {
                        wc.DownloadFile(afi.downloadUrl, folderPath + afi.fileName);
                    }
                }
            }
        }

        // 获取数据文件保存目录
        private string getAttFolderPath(string folder)
        {
            string folderPath = null;

            if (!Directory.Exists(folder))
            {
                DirectoryInfo di = Directory.CreateDirectory(folder);
                folderPath = di.FullName;
            }
            else
            {
                DirectoryInfo di = new DirectoryInfo(folder);
                folderPath = di.FullName;
            }

            return folderPath;
        }

        private static void setPostRequestHeaders(MyWebClient wc, string refUrl)
        {
            wc.Headers.Clear();
            wc.Headers.Add(HttpRequestHeader.ContentType, "application/x-www-form-urlencoded");
            wc.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            wc.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
            wc.Headers.Add(HttpRequestHeader.AcceptLanguage, "zh-CN,zh;q=0.8,en;q=0.6,zh-TW;q=0.4");
            wc.Headers.Add(HttpRequestHeader.Referer, refUrl);
            wc.Encoding = new UTF8Encoding();
        }

        private static void setGetRequestHeaders(MyWebClient wc, string refUrl)
        {
            wc.Headers.Clear();
            wc.Headers.Add("Accept", "*/*");
            wc.Headers.Add("Accept-Encoding", "gzip, deflate, sdch");
            wc.Headers.Add("Accept-Language", "zh-CN,zh;q=0.8");
            wc.Headers.Add("Cache-Control", "max-age=0");
            wc.Headers.Add("Content-Type", "application/json");
            wc.Headers.Add("Referer", refUrl);
            wc.Encoding = new UTF8Encoding();
        }

        internal void loadUserCfg()
        {
            string trgUrl = _appSetting.userCfgUrl;
            string refUrl = _appSetting.userCfgRefUrl;

            MyWebClient wc = getWebClient("GET", refUrl);

            CookieCollection ckiColn = new CookieCollection();

            ckiColn.Add(new Cookie("TOURL", "http%3A%2F%2Fweb.jingoal.com%2Fmodule%2Fcalendar%2Fworkbench%2FgetMarkDays.do%3FcalendarId%3D0%26currMonth%3D2016%2F1%26b1453215637200%3D1", "/"));
            ckiColn.Add(new Cookie("_ga", "GA1.2.19950754.1452869751", "/"));
            ckiColn.Add(new Cookie("_gat", "1", "/"));
            ckiColn.Add(new Cookie("code", "swK9xa", "/"));
            ckiColn.Add(new Cookie("flag", "login", "/"));
            ckiColn.Add(new Cookie("ouri", "http%3A%2F%2Fweb.jingoal.com%2F%23workbench", "/"));

            wc.addCookies(new Uri(trgUrl), ckiColn);

            string rspData = wc.DownloadString(trgUrl);

            LoadUserCfgResult rsl = new LoadUserCfgResult(rspData);

            LogHelper.info(string.Format("Load user config {0}", rsl.IsSucc ? "true" : "false"));
        }

        internal void loadAttendanceList()
        {
            string refUrl = _appSetting.refererUrl;
            string trgUrl = _appSetting.attListUrl;
            string postData = string.Format("type={0}&curr={1}", 2, 1);

            try
            {
                MyWebClient wc = getWebClient("post", refUrl);

                CookieCollection ckiColn = new CookieCollection();

                ckiColn.Add(new Cookie("JINSESSIONID", "d98461f8-d0d8-4c1a-8fc7-1210a7d7c571"));

                wc.addCookies(new Uri(trgUrl), ckiColn);
                string rspData = wc.UploadString(trgUrl, "");

                LoadAttendanceResult rsl = new LoadAttendanceResult(rspData);
                _atndFiles = rsl.AtndList;

                // LogHelper.info(string.Format("Load attendance file list {0} .", rsl.fileCount));

            }
            catch (WebException wex)
            {
                LogHelper.error(wex.Message);
            }
        }

        private CookieContainer CurrentSessionCookies
        {
            get {
                return _wc.CookieContainer;
            }
        }

        private string Referer
        {
            get {
                return _referer;
            }
        }

        // Create a new WebClient object
        public static MyWebClient createWebClient(CookieContainer ccntr = null, string refUrl = null)
        {
            MyWebClient wbclnt = new MyWebClient();

            // Header 字段可能在发出请求报文后丢失，因此必须重新设置
            setPostRequestHeaders(wbclnt, refUrl);

            if (ccntr != null)
            {
                wbclnt.addCookies(new Uri(refUrl), ccntr.GetCookies(new Uri(refUrl)));
            }

            return wbclnt;
        }

        ClientDetailThread clidtlThread = null;
#region abandoned code ...
        internal void preyAllClientDetailInfoAsync()
        {
            if (clidtlThread == null)
            {
                clidtlThread = new ClientDetailThread(CurrentSessionCookies, Referer);
                int job_count = clidtlThread.loadJobs();
            }

            if (clidtlThread.isRuning)
            {
                clidtlThread.stop();
            }
            else
            {
                clidtlThread.start();
            }
        }

        private int parseClientDetailInfo(int pid, int uid, string rspData)
        {
            Regex rgxEmail = new Regex("<input\\sid=\"user_email\".*value=\"(?<email>[a-zA-Z0-9_\\-\\.]+@[a-zA-Z0-9_\\-]+(\\.[a-zA-Z0-9_-]+)+)\"\\s/>", RegexOptions.Compiled | RegexOptions.Singleline);
            Regex rgxMobile = new Regex(@"<input\sid=""user_mobile"".*value=""(?<mobile>\d{11})""/>", RegexOptions.Compiled | RegexOptions.Singleline);

            Match mchEmail = rgxEmail.Match(rspData);
            Match mcMobile = rgxMobile.Match(rspData);

            string email = mchEmail.Groups["email"].Value;
            string mobile = mcMobile.Groups["mobile"].Value;

            ClientDetailInfoAdapter adapter = new ClientDetailInfoAdapter();

            int count = adapter.insert(pid, uid, email, mobile);

            return count;
        }

        internal int preyAllClientBaseInfo()
        {
            PhotographerAdapter adapter = new PhotographerAdapter();
            DataTable dt = adapter.getAllPhotographers(true);
            int totalClient = 0;
            int total = 0;

            foreach(DataRow row in dt.Rows)
            {
                int pid = (int)row["PhtgphrId"];
                int pageNum = (int)row["PageNum"];

                totalClient = preyClientBaseInfo(pid, pageNum+1, 100);
                total += totalClient;

                LogHelper.info(string.Format("Parse photographer(id={0}) pageNum={1} with totally {2} clients.", pid, pageNum, totalClient));
                adapter.setClientCount(pid, totalClient, true); // Set photographer row to 'completed'
            }

            return totalClient;
        }

        private int parsePhotographerInfo(string rspData)
        {
            int count = 0;
            Regex rgx = new Regex("<span\\sclass=\"(?<gender>\\w+)\">(?<ptgphrName>\\w+)</span>\\r\\s+</li>\\r\\s.*<a\\shref=\".*&photographer_id=(?<ptgphrId>\\d+)\">", RegexOptions.Compiled | RegexOptions.Multiline);

            MatchCollection mchz = rgx.Matches(rspData);
            if (mchz != null)
            {
                foreach(Match mc in mchz)
                {
                    count += savePhotographerInfo(mc.Groups["ptgphrName"].Value,
                        mc.Groups["ptgphrId"].Value,
                        mc.Groups["gender"].Value);
                }
            }

            return count;
        }

        private int savePhotographerInfo(string ptgphrName, string ptgphrId, string gender)
        {
            PhotographerAdapter adapter = new PhotographerAdapter();
            int count = adapter.insert(ptgphrName, ptgphrId, gender);
            if (count==1)
            {
                Debug.WriteLine(string.Format("[{0}, {1}, {2}]", ptgphrName, ptgphrId, gender));
            }
            return count;
        }

        private void saveClientBaseInfo(int userId, string userName, string realName,
            string city, string location, DateTime takeTime, DateTime activateTime, 
            int photoPoint, string status, int ptgphrId)
        {
            ClientBaseInfoAdapter adapter = new ClientBaseInfoAdapter();
            if (adapter.insert(userId, userName, realName, city, location, takeTime, activateTime, photoPoint, status, ptgphrId) == 1)
            {
                Debug.WriteLine(string.Format("[{0}, {1}, {2}]", userId, userName, realName));
            }
        }
#endregion abandoned code ...

        public bool Login(string userName, string password)
        {
            string trgUrl = _appSetting.loginUrl;
            string refUrl = _appSetting.refererUrl;
            string postData = string.Format("login_type=default&username={0}&password={1}&identify=", 
                userName, password);

            MyWebClient wc = getWebClient("post", refUrl);

            string rspData = wc.UploadString(trgUrl, postData);

            LoginResult rsl = new LoginResult(rspData);

            this.isLogin = rsl.isLoginSucc();

            return this.isLogin;
        }

        public int preyClientBaseInfo(int pid, int startIdx, int maxPageNum)
        {
            int cliCount = 0;
            int total = 0;
            WebClient wc = getWebClient();
            string trgUrlTmpl = _appSetting.clientBaseUrl;
            string trgUrl = string.Empty;
            string rspData = string.Empty;
            PhotographerAdapter adapter = new PhotographerAdapter();

            for(int i = startIdx; i < maxPageNum; i++)
            {
                waitRandomTime();
                trgUrl = string.Format(trgUrlTmpl, pid, i);

                try
                {
                    rspData = wc.DownloadString(trgUrl);
                    cliCount = parseAttendanceList(rspData);
                    total += cliCount;

                    adapter.setPageNum(pid, i);

                    Debug.WriteLine("Parse potographer (id={0}) , page = {1},  records = {2} ", pid, i, cliCount);
                    LogHelper.info(string.Format("Parse potographer (id={0}) , pageIndex = {1} with {2} clients.", pid, i, cliCount));

                    if (isLastPage(rspData)) {
                        Debug.WriteLine("Parse photographer (id={0}) {1} pages completed.", pid, i);
                        break; // it has reached last page
                    }
                }
                catch (WebException wex)
                {
                    Debug.WriteLine(string.Format("ERROR >> Parse potographer (id={0}) , pageIndex = {1} with Error {2}", pid, i, cliCount) + wex.Message);
                    LogHelper.error(string.Format("Parse potographer (id={0}) , pageIndex = {1} with ERROR {2}", pid, i, wex.Message));
                }

            }

            return total;
        }

        private bool isLastPage(string rspData)
        {
            int s = rspData.IndexOf("下一页", rspData.Length/2);
            return (s < 0);
        }

        private void waitRandomTime()
        {
            int sec = int.Parse(_appSetting.intervalTime);
            Random rdm = new Random(DateTime.Now.Millisecond);
            int intvl = rdm.Next();
            intvl = intvl % (sec*1000);
            
            Debug.WriteLine("Take a break {0} milliseconds", intvl);
            Thread.Sleep(intvl);
        }

        private int parseAttendanceList(string rspData)
        {
            int count = 0;
            Regex rgx = new Regex("<td>(?<userName>\\w+)</td>\\r\\s<td>(?<realName>\\w+)</td>\\r\\s<td\\salign=\"center\">"
                + "(?<city>\\w+)</td>\\r\\s<td\\salign=\"center\">(?<location>\\w+)</td>\\r\\s<td\\salign=\"center\">"
                + "(?<takeTime>[0-9\\-\\s\\:]+)</td>\\r\\s<td\\salign=\"center\">(?<activateTime>[0-9\\-\\s\\:]+)</td>"
                + "\\r\\s<td\\salign=\"center\">(?<point>\\w*)</td>\\r\\s<td\\salign=\"center\">(<span\\sclass=\"red\">)*"
                +"(?<status>\\w+)(</span>)*</td>\\r\\s<td\\salign=\"center\"><a\\shref=\"upload.php\\?act=edit&"
                +"photographer_id=(?<ptgphrId>\\d+)&pu_id=(?<userId>\\d+)\">&nbsp;编辑</a></td>", 
                RegexOptions.Compiled | RegexOptions.Multiline);

            MatchCollection mchz = rgx.Matches(rspData);
            if (mchz != null)
            {
                count = mchz.Count;
                foreach (Match mc in mchz)
                {
                    int userId = -1;
                    int ptgphrId = -1;
                    DateTime takeTime;
                    DateTime activateTime;
                    int point = 0;

                    DateTime.TryParse(mc.Groups["takeTime"].Value, out takeTime);
                    DateTime.TryParse(mc.Groups["activateTime"].Value, out activateTime);
                    int.TryParse(mc.Groups["point"].Value, out point);
                    int.TryParse(mc.Groups["userId"].Value, out userId);
                    int.TryParse(mc.Groups["ptgphrId"].Value, out ptgphrId);

                    saveClientBaseInfo(userId,
                                        mc.Groups["userName"].Value,
                                        mc.Groups["realName"].Value,
                                        mc.Groups["city"].Value,
                                        mc.Groups["location"].Value,
                                        takeTime,
                                        activateTime,
                                        point,
                                        mc.Groups["status"].Value,
                                        ptgphrId);
                }
            }

            return count;
        }

        public int preyClientInfos()
        {
            int count = 0;


            return count;
        }

        private bool preyClientInfo(int id)
        {
            bool bSucc = false;


            return bSucc;

        }

        internal int loadTask()
        {
            _clientIdList.Clear();

            ClientBaseInfoAdapter adapter = new ClientBaseInfoAdapter();
            DataTable dt = adapter.getPendingClients();
            foreach(DataRow row in dt.Rows)
            {
                int cid = (int)row["clientId"];

                _clientIdList.Add(cid);

            }
            return _clientIdList.Count;
        }

        internal void processTask(Action taskCompletedCallback)
        {

        }
    }
}
