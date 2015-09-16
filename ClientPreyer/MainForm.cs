using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MeDataAdapters;

namespace ClientPreyer
{
    public partial class mainForm : Form
    {
        public mainForm()
        {
            InitializeComponent();
        }

        private void mainForm_Load(object sender, EventArgs e)
        {
#if DEBUG 
            txbUserName.Text = "emi00";
            txbPassword.Text = "101010";
#endif
            numIntervalTime.Value = int.Parse(_appSetting.intervalTime);
        }

        ThreadMgr mgr = new ThreadMgr();
        private void btnStart_Click(object sender, EventArgs e)
        {
            if (mgr.isLogin)
            {
                // Parse all photographer's info
                // int nPhtgpher = mgr.preyPhotograpthers(20);

                // Parse all client's basic info
                int nBase = mgr.preyAllClientBaseInfo();

                // Parse all client's detail info
                // int nDetail = mgr.preyAllClientDetailInfo();
                mgr.preyAllClientDetailInfoAsync();

            }
        }

        private void taskCompletedCallback()
        {

        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            string userName = txbUserName.Text;
            string password = txbPassword.Text;

            if (!string.IsNullOrEmpty(userName))
            {
                userName = userName.Trim();
                userName = userName.ToLower();
            }
            else
            {
                MessageBox.Show("用户名不能为空.");
            }

            if (!string.IsNullOrEmpty(password))
            {
                password = password.Trim();
            }
            else
            {
                MessageBox.Show("密码不能为空.");
            }

            SysUserAdapter adpter = new SysUserAdapter();
            DataTable dt  = adpter.getSysUser(userName, password);

            if (dt != null && dt.Rows.Count == 1)
            {
                if (mgr.Login(userName, password))
                {
                    lblLoginStatus.Text = "已登录.";
                    rtxLog.Text += "登录目标网站成功.\n";
                }
                else
                {
                    rtxLog.Text += "登录目标网站失败.\n";
                }
            }
            else
            {
                MessageBox.Show("登录失败，请检查用户名或密码是否正确.");
                lblLoginStatus.Text = "未登录.";
            }

        }

        Properties.Settings _appSetting = new Properties.Settings();

        private void numIntervalTime_ValueChanged(object sender, EventArgs e)
        {
            if (numIntervalTime.Value < 1)
            {
                MessageBox.Show("数据攫取的时间间隔不能小于 1.");
            }
            else
            {
                _appSetting.intervalTime = numIntervalTime.Value.ToString();
                _appSetting.Save();
            }
        }
    }
}
