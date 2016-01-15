using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace ClientPreyer.Net
{
    class RequestResults
    {
    
    }

    [DataContract]
    class LoginResult
    {
        [DataMember]
        public string ok;

        public LoginResult(string rspString)
        {
            LoginResult rsl = parse<LoginResult>(rspString);
            this.ok = rsl.ok;
        }

        public static T parse<T>(string jsonString)
        {
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonString)))
            {
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(ms);
            }
        }

        public bool isLoginSucc()
        {
            return (ok.Equals("true"));
        }
    }

    [DataContract]
    class AttendanceFileInfo
    {
        [DataMember]
        public string fileName;
        [DataMember]
        public string exportUser;
        [DataMember]
        public string exportTime;
        [DataMember]
        public string downloadUrl;
    }

    class LoadAttendanceResult
    {
        List<AttendanceFileInfo> atndList;

        public LoadAttendanceResult(string rspString)
        {

        }

        public int fileCount {
            get { return atndList.Count(); }
        }
    }
}
