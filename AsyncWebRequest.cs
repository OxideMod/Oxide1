using System;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;

namespace Oxide
{
    /// <summary>
    /// An asynchronous web request
    /// </summary>
    public class AsyncWebRequest
    {
        private WebRequest request;
        private string url;

        private Thread thread;

        public bool Complete { get; private set; }
        public int ResponseCode { get; private set; }
        public string Response { get; private set; }

        private bool handled;

        public event Action<AsyncWebRequest> OnResponse;

        public AsyncWebRequest(string url)
        {
            this.url = url;
            thread = new Thread(Worker);
            thread.Start();
            //Main.Log("Worker thread started...");
        }

        private void Worker()
        {
            try
            {
                //Main.Log("Creating request...");
                request = WebRequest.Create(url);
                request.Credentials = CredentialCache.DefaultCredentials;
                //Main.Log("Waiting for response...");
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                if (response == null)
                {
                    // Uhh panic what do we do
                    Logger.Error("AsyncWebRequest panic: Response is null!");
                    return;
                }
                //Main.Log("Reading response stream...");
                Stream strm = response.GetResponseStream();
                StreamReader rdr = new StreamReader(strm);
                Response = rdr.ReadToEnd();
                ResponseCode = (int)response.StatusCode;
                rdr.Close();
                strm.Close();
                response.Close();
                Complete = true;
                //Main.Log("Web request complete.");
            }
            catch (WebException webex)
            {
                var response = webex.Response as HttpWebResponse;
                Response = webex.Message;
                ResponseCode = (int)response.StatusCode;
                Complete = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        /// <summary>
        /// Updates this request
        /// </summary>
        public void Update()
        {
            if (Complete && !handled)
            {
                handled = true;
                if (OnResponse != null)
                {
                    //Main.Log("Firing web request callback...");
                    OnResponse(this);
                }
            }
        }

    }
}
