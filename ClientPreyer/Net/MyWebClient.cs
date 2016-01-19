using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MyNetwork
{
    class MyWebClient : WebClient
    {
        // Cookies used with send-request
        private CookieContainer _cookieContainer = new CookieContainer();

        // Cookies saved from response
        private CookieCollection _responseCookies = new CookieCollection();

        public CookieContainer CookieContainer
        {
            get
            {
                return _cookieContainer;
            }
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
            request.CookieContainer = getCookieContainer(address);
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            return request;
        }

        public void addCookies(Uri uri, CookieCollection ccln)
        {
            _cookieContainer.Add(uri, ccln);
        }

        private CookieContainer getCookieContainer(Uri address)
        {
            CookieCollection ccln = _cookieContainer.GetCookies(address);

            if (_responseCookies.Count > 0)
            {
                foreach (Cookie cki in _responseCookies)
                {
                    // determines the response-cookie is existed in cookie-collection
                    bool found = false;
                    foreach (Cookie c in ccln)
                    {
                        if (cki.Name == c.Name && compareDomain(cki.Domain, address.Host)==0)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found) // add cookie to collection, skipping existed cookies
                    {
                        ccln.Add(new Cookie(cki.Name, cki.Value, "/"));
                    }
                }
            }

            // add all cookies of same domain to cookie-container
            _cookieContainer = new CookieContainer();
            _cookieContainer.Add(address, ccln);
            return _cookieContainer;
        }

        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
        {
            WebResponse response = base.GetWebResponse(request, result);

            // Save cookies from response
            saveCookies(response);
            return response;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            WebResponse response = base.GetWebResponse(request);

            // Save cookies from response
            saveCookies(response);
            return response;
        }

        private void saveCookies(WebResponse r)
        {
            var response = r as HttpWebResponse;
            if (response != null)
            {
                
                _responseCookies.Add(response.Cookies);

                Debug.WriteLine("---------------------------------------------------");
                foreach(Cookie cki in response.Cookies)
                {
                    bool found = false;
                    foreach(Cookie c in _responseCookies)
                    {
                        if (c.Name == cki.Name && compareDomain(c.Domain, cki.Domain) == 0)
                        {
                            c.Value = cki.Value; // Update cookie's value
                            c.Domain = cki.Domain;
                            c.Path = cki.Path;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        _responseCookies.Add(cki); // Add new cookie
                    }
                    Debug.WriteLine("MyWebClient save cookies : {0}={1},{2},{3}", cki.Name,  cki.Value, cki.Path, cki.Domain);
                }
                Debug.WriteLine("---------------------------------------------------");
            }
        }

        private int compareDomain(string d1, string d2)
        {
            int i = d1.IndexOf('.');
            int j = d2.IndexOf('.');
            int k = 1;
            if (i == j)
            {
                d1 = d1.Substring(i);
                d2 = d2.Substring(j);
                k = (d1 == d2) ? 0 : 1;
            }
            return k;
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
