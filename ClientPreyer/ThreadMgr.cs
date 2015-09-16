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

namespace ClientPreyer
{
    class ThreadMgr
    {
        Properties.Settings _appSetting = new Properties.Settings();
        private List<int> _clientIdList = new List<int>();
        private static string _cookie;
        private static string _referer;
        private MyWebClient _wc;
        public bool isLogin;

        public MyWebClient getWebClient(CookieContainer ccntr = null, string referer = null)
        {
            if (_wc == null)
            {
                _wc = new MyWebClient();
            }
            else // Update cookie
            {
                if (_wc.ResponseHeaders != null && _wc.ResponseHeaders.AllKeys.Contains("Set-Cookie"))
                {
                    _cookie = _wc.ResponseHeaders["Set-Cookie"];
                    if (_cookie != null)
                    {
                        _wc.Headers.Add(HttpRequestHeader.Cookie, _cookie);
                        LogHelper.info("getWebClient : _cookie - " + _cookie);
                    }
                }
            }

            if (referer != null) _referer = referer;

            // Header 字段可以在发出请求报文后丢失，因此必须重新设置
            _wc.Headers.Clear();
            _wc.Headers.Add(HttpRequestHeader.ContentType, "application/x-www-form-urlencoded");
            _wc.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _wc.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
            _wc.Headers.Add(HttpRequestHeader.AcceptLanguage, "zh-CN,zh;q=0.8,en;q=0.6,zh-TW;q=0.4");
            _wc.Headers.Add(HttpRequestHeader.Referer, referer);
            _wc.Encoding = new UTF8Encoding();

            return _wc;
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

        public static MyWebClient createWebClient(CookieContainer ccntr=null, string referer=null)
        {
            MyWebClient wbclnt = new MyWebClient();

            // Header 字段可以在发出请求报文后丢失，因此必须重新设置
            wbclnt.Headers.Add(HttpRequestHeader.ContentType, "application/x-www-form-urlencoded");
            wbclnt.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            wbclnt.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
            wbclnt.Headers.Add(HttpRequestHeader.AcceptLanguage, "zh-CN,zh;q=0.8,en;q=0.6,zh-TW;q=0.4");
            wbclnt.Headers.Add(HttpRequestHeader.Referer, referer);
            wbclnt.Encoding = new UTF8Encoding();

            if (ccntr != null)
            {
                /// wbclnt.Headers.Add(HttpRequestHeader.Cookie, cookie);
                wbclnt.CookieContainer = ccntr;
                _cookie = ccntr.GetCookieHeader(new Uri(referer));
                LogHelper.info("create webclient with cookie : " + _cookie);
            }

            if (referer != null) _referer = referer;

            return wbclnt;
        }

        ClientDetailThread clidtlThread = null;

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

        public bool Login(string userName, string password)
        {
            string referer = "http://www.p1.cn/";
            MyWebClient wc = getWebClient(null, referer);

            string trgUrl = _appSetting.loginUrl;
            string postData = string.Format("action=login&return_to={2}&frontpage=1&remember_me=1&login_name={0}&password={1}", 
                userName, password, "/siteadmin/photographer/photographers.php");

            string rspData = wc.UploadString(trgUrl, postData);
            
            this.isLogin = isLoginSucc(wc.ResponseCookies);

            return this.isLogin;
        }

        private bool isLoginSucc(CookieCollection cookies)
        {
            CookieContainer cctner = new CookieContainer();
            
            bool bLogin = false;
            if (cookies != null)
            {
                Cookie ck = cookies["user_login"];
                if (ck != null)
                {
                    bLogin = true;
                    LogHelper.info(string.Format("Login website as {0}", ck.Value));
                }
            }

            return bLogin;
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
                    cliCount = parseClientBaseInfo(rspData);
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

        private int parseClientBaseInfo(string rspData)
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
