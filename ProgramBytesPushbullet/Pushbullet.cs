using System;
using System.Net;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;      // For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronWebSocketClient;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;
using Newtonsoft.Json;


namespace ProgramBytesPushbullet
{
    public class Pushbullet
    {
        private string token = "";
        public WebSocketClient wsc = new WebSocketClient();
        public WebSocketClient.WEBSOCKET_RESULT_CODES ret;
        public WebSocketClient.WEBSOCKET_PACKET_TYPES opcode;
        public byte[] SendData = new byte[6];
        
        public byte[] ReceiveData;

        public newResponse OnNewResponse { get; set; }
        public delegate void newResponse(SimplSharpString TEXT);

        public string Token
        {
            set { token = value; }
        }

        public void Load()
        {
            CrestronConsole.PrintLine("Access Token: " + token.ToString());
            ReceiveData = new byte[SendData.Length];
            wsc.SSL = true;
            wsc.Port = WebSocketClient.WEBSOCKET_DEF_SSL_SECURE_PORT;
            wsc.URL = "wss://stream.pushbullet.com/websocket/" + token;
            wsc.Origin = "www.programbits.com";
            wsc.AddOnHeader = "Access-Token" + token;
            wsc.Protocol = "application/json; charset= utf-8";
            wsc.ConnectionCallBack = SendCallback;
            wsc.ReceiveCallBack = ReceiveCallback;
            wsc.ConnectAsync();
            
            if (ret == (int)WebSocketClient.WEBSOCKET_RESULT_CODES.WEBSOCKET_CLIENT_SUCCESS)
            {
                CrestronConsole.PrintLine("Websocket connected. \r\n");
                checkPushes();
            }
            else
            {
                CrestronConsole.PrintLine("Websocket could not connect to server.  Connect  returncode: " + ret.ToString());
            }
        }

        double lastModified = 0;

        public void checkPushes()
        {
            CrestronConsole.PrintLine("Getting Previous Pushes");

            HttpsClient hsc = new HttpsClient();
            hsc.PeerVerification = false;
            hsc.HostVerification = false;
            HttpsClientRequest hscr = new HttpsClientRequest();
            //HttpsClientResponse hscrs;

            hsc.KeepAlive = true;
            hscr.Url.Parse("https://api.pushbullet.com/v2/pushes?active=true&modified_after=" + lastModified);
            //hscr.Header.AddHeader(new HttpsHeader("Access-Token", token));
            hscr.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
            hscr.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));
            hscr.Header.AddHeader(new HttpsHeader("Authorization", "Bearer " + token));
            //hscr.ContentString = JsonConvert.SerializeObject(hscr.ToString());
            hsc.DispatchAsync(hscr, (hscrs, e) =>
                {
                    try
                    {
                        if (hscrs.Code >= 200 && hscrs.Code < 300)
                        {
                            // success
                            //CrestronConsole.PrintLine("Old Pushes Requst Send.");
                            //string s = hscrs.ContentString.ToString();
                            //CrestronConsole.Print("Old Push Data: " + s + "\n");
                            pushReturn pr = JsonConvert.DeserializeObject<pushReturn>(hscrs.ContentString);
                            
                            foreach (pushData pd in pr.pushes)
                            {
                                //CrestronConsole.Print("Json Push Data: " + pr.ToString() + "\n");
                                if (pd.modified > lastModified)
                                {
                                    lastModified = pd.modified;
                                    CrestronConsole.Print("Json Push Data: " + pd.body + "\n");
                                    if (pd.body.ToLower().StartsWith("unlock"))
                                        OnNewResponse(new SimplSharpString(pd.body.ToLower()));
                                    else if (pd.body.ToLower().StartsWith("ignore"))
                                        OnNewResponse(new SimplSharpString(pd.body.ToLower()));
                                }
                            }
                        }
                    }
                    catch (Exception f)
                    {
                        CrestronConsole.PrintLine("Old Push Connection error:" + f.ToString());
                    }
                });
        }
        
        public class PushInfo
        {
            public string type { get; set; }
            public string title { get; set; }
            public string body { get; set; }
        }

        public void BellPush()
        {
            CrestronConsole.PrintLine("button push received");

            PushInfo pushinfo = new PushInfo
            {
                type = "note",
                title = "Doorbell",
                body = "reply unlock or ignore"
            };

            HttpsClient hsc = new HttpsClient();
            hsc.PeerVerification = false;
            hsc.HostVerification = false;
            HttpsClientRequest hscr = new HttpsClientRequest();
            HttpsClientResponse hscrs;

            hsc.KeepAlive = true;
            hscr.Url.Parse("https://api.pushbullet.com/v2/pushes");
            //hscr.Header.AddHeader(new HttpsHeader("Access-Token", token));
            hscr.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
            hscr.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));
            hscr.Header.AddHeader(new HttpsHeader("Authorization", "Bearer " + token));
            hscr.ContentString = JsonConvert.SerializeObject(pushinfo);
            try
            {
                hscrs = hsc.Dispatch(hscr);
                if (hscrs.Code >= 200 && hscrs.Code < 300)
                {
                    // success
                    CrestronConsole.PrintLine("Dispatch worked.");
                    //checkPushes();
                }
                else
                {
                    CrestronConsole.PrintLine("Dispatch server error:" + hscrs.Code);
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Connection error:" + e.ToString());
            }


        }

        //public void Unlock()
        //{
            //CrestronConsole.PrintLine("Need Unlock!\n");
            //responseUnlock();
        //}

        //public void Ignore()
        //{
            //CrestronConsole.PrintLine("Ignore!\n");
            //responseIgnore();
        //}

        public int SendCallback(WebSocketClient.WEBSOCKET_RESULT_CODES error)
        {
            try
            {
                wsc.ReceiveAsync();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("SendCallback Error" + e.ToString());
                return -1;
            }

            return 0;
        }

        public int ReceiveCallback(byte[] data, uint datalen, WebSocketClient.WEBSOCKET_PACKET_TYPES opcode, WebSocketClient.WEBSOCKET_RESULT_CODES error)
        {
            string sData = Encoding.ASCII.GetString(data, 0, data.Length);
            try
            {
                socketData msg = JsonConvert.DeserializeObject<socketData>(sData);
                if (msg.type != "nop")
                    CrestronConsole.PrintLine("Received data:" + sData.ToString());
                if (msg.type == "tickle" && msg.subtype == "push")
                    checkPushes();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Unknown Data Received" + e.ToString());
                DisconnectWebSocket();
            }
            wsc.ReceiveAsync();
            return 0;

        }

        public void DisconnectWebSocket()
        {
            wsc.Disconnect();
            CrestronConsole.PrintLine("Websocket disconnected.\r\n");
            Load();
        }
    }
}



