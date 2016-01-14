using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Logging;
using MeDataAdapters;
using MyNetwork;

namespace ClientPreyer
{
    class ClientDetailThread
    {
        private static ManualResetEvent s_run_event = new ManualResetEvent(false);
        private static CookieContainer s_cookie;
        private static string s_referer;
        public static int s_interval = 10000;

        JobCollection jobs = new JobCollection();

        public bool isRuning
        {
            get
            {
                return s_run_event.WaitOne(0);
            }
        } 

        public ClientDetailThread(CookieContainer ck, string referer)
        {
            s_cookie = ck;
            s_referer = referer;
        }

        public int loadJobs()
        {
            int job_count = jobs.loadJobs();

            Job job = jobs.getNextJob();
            while(job != null)
            {
                ThreadPool.QueueUserWorkItem(job_proc, job);
                job = jobs.getNextJob();
            }
            return job_count;
        }

        // This thread procedure performs the task.
        private static void job_proc(Object param)
        {
            int total = 0;
            int count = 0;
            Properties.Settings appSetting = new Properties.Settings();

            /// Wait for run-event being signaled
            s_run_event.WaitOne();

            Job job = (Job)param;
            if (job != null)
            {
                WebClient wc = ThreadMgr.createWebClient(s_cookie, s_referer); // Create New WebClient object for current thread.
                string trgUrlTmpl = appSetting.clientDetailUrl;
                string trgUrl = string.Format(trgUrlTmpl, job.pid, job.uid);
                string rspData = string.Empty;

                try
                {
                    rspData = wc.DownloadString(trgUrl);
                    count = parseClientDetailInfo(job.pid, job.uid, rspData);
                    Debug.WriteLine("Parse photographer (id={0}) , client = {1},{2}, succ={3}", job.pid, job.userName, job.realName, count);
                }
                catch (WebException wex)
                {
                    Debug.WriteLine("Parse photographer (id={0}) , client = {1},{2}, ERROR={3}", job.pid, job.userName, job.realName, wex.Message);
                    LogHelper.error(string.Format("Parse photographer (id={0}) , client = {1},{2}, ERROR={3}", job.pid, job.userName, job.realName, wex.Message));
                }
                total += count;
            }
            // return total;
        }

        private static int getSleepTime()
        {
            Random rdm = new Random();
            int intvl = rdm.Next() % s_interval;

            return intvl;
        }

        private static int parseClientDetailInfo(int pid, int uid, string rspData)
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

        public void start()
        {
            s_run_event.Set();
        }

        public void stop()
        {
            s_run_event.Reset();
        }
    }

    class Job
    {
        public int uid;
        public int pid;
        public string userName;
        public string realName;
    }

    class JobCollection
    {
        DataTable dtJobs;

        public int loadJobs()
        {
            if (dtJobs!= null && dtJobs.Rows.Count > 0) dtJobs.Clear();

            ClientBaseInfoAdapter adapter = new ClientBaseInfoAdapter();
            dtJobs = adapter.getAllClientBaseInfo(true);

            return dtJobs.Rows.Count;
        }

        public Job getNextJob()
        {
            Job job = null;
            lock (this) // Synchorization 
            {
                if (dtJobs.Rows.Count > 0)
                {
                    job = new Job();
                    job.uid = (int)dtJobs.Rows[0]["userId"];
                    job.pid = (int)dtJobs.Rows[0]["PhotographerId"];
                    job.userName = (string)dtJobs.Rows[0]["userName"];
                    job.realName = (string)dtJobs.Rows[0]["realName"];

                    dtJobs.Rows.RemoveAt(0); // Remove current row
                }
            }
            return job;
        }
    }
}
