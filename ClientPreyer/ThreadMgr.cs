﻿using System;
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

namespace ClientPreyer
{
    class ThreadMgr
    {
        Properties.Settings _appSetting = new Properties.Settings();
        private List<int> _clientIdList = new List<int>();
        private static string _current_cookies;
        private static string _referer;
        private MyWebClient _wc;
        public bool isLogin;

        // Get the only instance of WebClient 
        public MyWebClient getWebClient(CookieContainer ccntr = null, string refUrl = null)
        {
            if (_wc == null) _wc = new MyWebClient();
            if (refUrl != null) _referer = refUrl;

            // 设置 Headers，发出请求报文后会丢失，因此必须重新设置
            setRequestHeaders(_wc, _referer);

            // 设置 Cookies
            if (_wc.ResponseHeaders != null && _wc.ResponseHeaders.AllKeys.Contains("Set-Cookie"))
            {
                _current_cookies = _wc.ResponseHeaders["Set-Cookie"];
                if (_current_cookies != null)
                {
                    _wc.Headers.Add(HttpRequestHeader.Cookie, _current_cookies);
                    LogHelper.info("getWebClient : _cookie - " + _current_cookies);
                }
            }

            if (ccntr != null)
            {
                _wc.CookieContainer.Add(ccntr.GetCookies(new Uri("http://web.jingoal.com")));
            }

            return _wc;
        }

        private static void setRequestHeaders(MyWebClient wc, string refUrl)
        {
            wc.Headers.Clear();
            wc.Headers.Add(HttpRequestHeader.ContentType, "application/x-www-form-urlencoded");
            wc.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            wc.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
            wc.Headers.Add(HttpRequestHeader.AcceptLanguage, "zh-CN,zh;q=0.8,en;q=0.6,zh-TW;q=0.4");
            wc.Headers.Add(HttpRequestHeader.Referer, refUrl);
            wc.Encoding = new UTF8Encoding();
        }

        internal void loadAttendanceList()
        {
            string refUrl = _appSetting.refererUrl;
            string trgUrl = _appSetting.attListUrl;
            string postData = string.Format("type={0}&curr={1}", 2, 1);

            try
            {
                CookieContainer cc = new CookieContainer();
                string strCookies = "route=60cd899e37616d32b077390b1a32217d; apps=jmbmgtweb; JSESSIONID=FC0EDA9813B60450D02B040011F40638; bigDataUuid=e38b5fa4-e935-f7b3-f15f-7e5ef0fa4048; Hm_lvt_586f9b4035e5997f77635b13cc04984c=1452703716,1452869751; Hm_lpvt_586f9b4035e5997f77635b13cc04984c=1452869751; _ga=GA1.2.19950754.1452869751; _gat=1";
                string[] cookies = strCookies.Split(';');
                foreach(string c  in cookies)
                {
                    string[] ck = c.Split('=');
                    if (ck.Length == 2)
                    {
                        Cookie cki = new Cookie(ck[0].Trim(), ck[1].Trim(), "/", ".jingoal.com");
                        cc.Add(cki);
                    }
                }
                MyWebClient wc = getWebClient(cc, refUrl);

                // string rspData = wc.UploadString(trgUrl, postData);
                string rspData = wc.DownloadString(trgUrl);

                LoadAttendanceResult rsl = new LoadAttendanceResult(rspData);

                LogHelper.info(string.Format("Load attendance file list {0} .", rsl.fileCount));

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
            get{
                return _referer;
            }
        }

        // Create a new WebClient object
        public static MyWebClient createWebClient(CookieContainer ccntr=null, string refUrl=null)
        {
            MyWebClient wbclnt = new MyWebClient();

            // Header 字段可能在发出请求报文后丢失，因此必须重新设置
            setRequestHeaders(wbclnt, refUrl);

            if (ccntr != null)
            {
                wbclnt.CookieContainer = ccntr;
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

        internal int preyAllClientDetailInfo()
        {
            int total = 0;
            ClientBaseInfoAdapter adapter = new ClientBaseInfoAdapter();

            DataTable dt = adapter.getAllClientBaseInfo(true);
            foreach(DataRow row in dt.Rows)
            {
                int uid = (int)row["userId"];
                int pid = (int)row["PhotographerId"];
                string userName = (string)row["userName"];
                string realName = (string)row["realName"];

                int count = 0;
                WebClient wc = getWebClient();
                string trgUrlTmpl = _appSetting.clientDetailUrl;
                string trgUrl = string.Format(trgUrlTmpl, pid, uid);
                string rspData = string.Empty;

                waitRandomTime();
                try
                {
                    rspData = wc.DownloadString(trgUrl);
                    count = parseClientDetailInfo(pid, uid, rspData);
                    Debug.WriteLine("Parse photographer (id={0}) , client = {1},{2}, succ={3}", pid, userName, realName, count);
                }
                catch(WebException wex)
                {
                    Debug.WriteLine("Parse photographer (id={0}) , client = {1},{2}, ERROR={3}", pid, userName, realName, wex.Message);
                    LogHelper.error(string.Format("Parse photographer (id={0}) , client = {1},{2}, ERROR={3}", pid, userName, realName, wex.Message));
                }
                total += count;


            }
            return total;
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

        internal int preyPhotograpthers(int pageNum)
        {
            int count = 0;
            MyWebClient wc = getWebClient();

            string trgTmplUrl = _appSetting.allPhtgpherUrl;

            for(int i=1; i <= pageNum; i++)
            {
                string rspData = wc.DownloadString(string.Format(trgTmplUrl, i.ToString()));
                count += parsePhotographerInfo(rspData);
            }

            return count;
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

            MyWebClient wc = getWebClient(null, refUrl);

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
