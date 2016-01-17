using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MyNetwork
{
    class MyWebClient : WebClient
    {
        private CookieContainer _cookieContainer = new CookieContainer();
        private CookieCollection _responseCookies = new CookieCollection();

        public CookieContainer CookieContainer
        {
            get { return _cookieContainer; }
            set { _cookieContainer = value; }
        }
        public CookieCollection ResponseCookies
        {
            get
            {
                return _responseCookies;
            }
        }
        /**
        WebClient uses HttpWebRequest under the covers.And HttpWebRequest supports gzip/deflate decompression. See HttpWebRequest AutomaticDecompression property
        However, WebClient class does not expose this property directly.So you will have to derive from it to set the property on the underlying HttpWebRequest.
        */
        protected override WebRequest GetWebRequest(Uri address)
        {
            HttpWebRequest request = base.GetWebRequest(address) as HttpWebRequest;
            request.CookieContainer = _cookieContainer;
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            return request;
        }

        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
        {
            WebResponse response = base.GetWebResponse(request, result);
            saveCookies(response);
            return response;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            WebResponse response = base.GetWebResponse(request);
            saveCookies(response);
            return response;
        }

        private void saveCookies(WebResponse r)
        {
            var response = r as HttpWebResponse;
            if (response != null)
            {
                foreach (Cookie cki in response.Cookies)
                {
                    // Check if this cookie is existed in _responseCookies collection
                    bool found = false;
                    foreach (Cookie rspCki in _responseCookies)
                    {
                        if (rspCki.Name.Equals(cki.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (found) // Update existed cookie's value
                    {
                        _responseCookies[cki.Name].Value = cki.Value;
                    }
                    else
                    {
                        _responseCookies.Add(cki); // Add new cookie
                    }
                }

                _cookieContainer.Add(_responseCookies);
            }
        }

        public string UploadFileEx(string url, string uploadFile, NameValueCollection nameValues)
        {
            string fileName = Path.GetFileName(uploadFile);

            // Boundary for multipart data
            string uniqueTag = "----------" + DateTime.Now.Ticks.ToString("x"); // in fact, this is the real boundary 
            string boundary = "--" + uniqueTag + "\r\n";
            string trailer = "\r\n--" + uniqueTag + "--\r\n";
            // Build the trailing boundary string as a byte array, ensuring the boundary appears on a line by itself
            byte[] boundaryBytes = Encoding.ASCII.GetBytes(boundary);
            byte[] trailerBytes = System.Text.Encoding.ASCII.GetBytes(trailer);

            // 1. Build requests headers
            //
            // Build up the post message header
            StringBuilder sb = new StringBuilder();

            for(int i=0; i<nameValues.Count; i++)
            {
                sb.Append(boundary);
                sb.AppendFormat("Content-Disposition: form-data; name=\"{0}\"\r\n\r\n", nameValues.GetKey(i));
                sb.Append(nameValues[i]);
                sb.Append("\r\n");
            }

            sb.Append(boundary);
            sb.AppendFormat("Content-Disposition: form-data; name=\"Filedata\"; filename=\"{0}\"\r\n", fileName);
            sb.Append("Content-Type: application/octet-stream\r\n");
            sb.Append("\r\n");

            string postHeader = sb.ToString();
            byte[] postHeaderBytes = Encoding.UTF8.GetBytes(postHeader);

            // 2. Load file data
            FileStream fileStream = new FileStream(uploadFile, FileMode.Open, FileAccess.Read);

            HttpWebRequest webrequest = (HttpWebRequest)base.GetWebRequest(new Uri(url));
            webrequest.ContentType = "multipart/form-data; boundary=" + uniqueTag; // Here, use the 'real' boundary
            webrequest.Method = "POST";
            webrequest.ContentLength = postHeaderBytes.Length + fileStream.Length + trailerBytes.Length; ;
            Stream requestStream = webrequest.GetRequestStream();

            // 3. Write out request headers
            requestStream.Write(postHeaderBytes, 0, postHeaderBytes.Length);

            // 4. Write out file data
            byte[] buffer = new Byte[checked((uint)Math.Min(4096,(int)fileStream.Length))];
            int bytesRead = 0;
            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                requestStream.Write(buffer, 0, bytesRead);
            }

            // 5. Write out trailing boundary
            requestStream.Write(trailerBytes, 0, trailerBytes.Length);

            // 6. Get Respose from server
            WebResponse responce = webrequest.GetResponse();
            Stream s = responce.GetResponseStream();
            StreamReader sr = new StreamReader(s);

            return sr.ReadToEnd();
        }
    }
}
