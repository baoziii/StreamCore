//using EnhancedBilibiliChat.Bot;
using StreamCore.Chat;
using StreamCore.Config;
using StreamCore.SimpleJSON;
using StreamCore.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;
using BitConverterNew;

namespace StreamCore.Bilibili
{
    /// <summary>
    /// The main Bilibili websocket client.
    /// </summary>
    public class BilibiliWebSocketClient
    {
        //private static readonly Regex _bilibiliMessageRegex = new Regex(@"^(?:@(?<Tags>[^\r\n ]*) +|())(?::(?<HostName>[^\r\n ]+) +|())(?<MessageType>[^\r\n ]+)(?: +(?<ChannelName>[^:\r\n ]+[^\r\n ]*(?: +[^:\r\n ]+[^\r\n ]*)*)|())?(?: +:(?<Message>[^\r\n]*)| +())?[\r\n]*$", RegexOptions.Compiled | RegexOptions.Multiline);
        //private static readonly Regex _tagRegex = new Regex(@"(?<Tag>[^@^;^=]+)=(?<Value>[^;\s]+)", RegexOptions.Compiled | RegexOptions.Multiline);

        private static Random _rand = new Random();
        private static WebSocket _ws;
        private static short protocalVersion = 2;

        ///<Summary>
        /// Set the DanmukuServer 
        ///</Summary>
        public static string DanmukuServer { get; set; } = "broadcastlv.chat.bilibili.com";

        ///<Summary>
        /// Set the DanmukuServerPort
        ///</Summary>
        public static int DanmukuServerPort { get; set; } = 2243;

        ///<Summary>
        /// Set the Real Channel id (short_id to long_id if avaliable)
        ///</Summary>
        public static int RealBilibiliChannelId { get; set; } = 0;

        ///<Summary>
        /// Set the Channel Master uid
        ///</Summary>
        public static string BilibiliChannelMaster { get; set; } = "";

        ///<Summary>
        /// Set the Danmuku User Token
        ///</Summary>
        public static string DanmukuToken { get; set; } = "";

        ///<Summary>
        /// Display the damuku if 1
        ///</Summary>
        public static int DanmukuDanmuku { get; private set; } = 1;

        ///<Summary>
        /// Display the gift notice if 1
        ///</Summary>
        public static int DanmukuGift { get; private set; } = 1;

        ///<Summary>
        /// Display the new audience notice if 1
        ///</Summary>
        public static int DanmukuWelcome { get; private set; } = 0;

        ///<Summary>
        /// Display the room rank notice if 1
        ///</Summary>
        public static int DanmukuRoomRank { get; private set; } = 0;

        ///<Summary>
        /// Display the fan number notice if 1
        ///</Summary>
        public static int DanmukuFanUpdate { get; private set; } = 0;

        ///<Summary>
        /// Display the global banner notice (unrecommended) if 1
        ///</Summary>
        public static int DanmukuNotice { get; private set; } = 0;

        /// <summary>
        /// True if the client has been initialized already.
        /// </summary>
        public static bool Initialized { get; private set; } = false;

        /// <summary>
        /// True if the client is connected to Bilibili.
        /// </summary>
        public static bool Connected { get; private set; } = false;

        /// <summary>
        /// True if the user has entered valid login details.
        /// </summary>
        public static bool LoggedIn { get; private set; } = false;

        /// <summary>
        /// The last time the client established a connection to the Bilibili servers.
        /// </summary>
        public static DateTime ConnectionTime { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// A dictionary of channel information for every channel we've joined during this session, the key is the channel name.
        /// </summary>
        public static Dictionary<int, BilibiliChannel> ChannelInfo { get; private set; } = new Dictionary<int, BilibiliChannel>();

        /// <summary>
        /// A reference to the currently logged in Bilibili user, will say **Invalid Bilibili User** if the user is not logged in.
        /// </summary>
        public static BilibiliUser OurBilibiliUser { get; set; } = new BilibiliUser("*Invalid Bilibili User*");

        /// <summary>
        /// Callback for when the user changes the BilibiliChannelName in BilibiliLoginInfo.ini. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action<string> OnBilibiliChannelUpdated;

        /// <summary>
        /// Callback for when BilibiliLoginInfo.ini is updated *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action OnConfigUpdated;

        /// <summary>
        /// Callback that occurs when a connection to the Bilibili servers is successfully established. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action OnConnected;

        /// <summary>
        /// Callback that occurs when we get disconnected from the Bilibili servers. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action OnDisconnected;
        
        /// <summary>
        /// True if the BilibiliChannelName in BilibiliLoginInfo.ini is valid, and we've joined the channel successfully.
        /// </summary>
        public static bool IsChannelValid { get => ChannelInfo.TryGetValue(BilibiliLoginConfig.Instance.BilibiliChannelId, out var channelInfo) && channelInfo.roomId > 0; }

        private static DateTime _sendLimitResetTime = DateTime.UtcNow;
        private static int _reconnectCooldown = 500;
        private static int _fullReconnects = -1;
        private static int _lastChannel = 0;

        private static int _messagesSent = 0;
        private static int _sendResetInterval = 5;
        private static int _messageLimit { get => 1; } // Defines how many messages can be sent within _sendResetInterval without causing a global ban on bilibili 
        private static ConcurrentQueue<KeyValuePair<int, string>> _sendQueue = new ConcurrentQueue<KeyValuePair<int, string>>();
        private static bool updateInfoShown = false;

        /// <summary>
        /// A Dictionary stores ban rules from "UserData/BanList.ini".
        /// </summary>
        public static Dictionary<string, List<string>> banListRule { get; set; } = new Dictionary<string, List<string>>();
        
        internal static void Initialize_Internal()
        {
            if (Initialized)
                return;

            BilibiliMessageHandlers.Initialize();

            _lastChannel = BilibiliLoginConfig.Instance.BilibiliChannelId;
            BilibiliAPI.GetChannelConfig(BilibiliLoginConfig.Instance.BilibiliChannelId);
            BilibiliLoginConfig.Instance.ConfigChangedEvent += Instance_ConfigChangedEvent;
            BilibiliLoginConfig.BanListConfigLoad();
            Initialized = true;
            Task.Run(() => {
                Thread.Sleep(1000);
                Connect();
            });
        }

        private static void Instance_ConfigChangedEvent(BilibiliLoginConfig obj)
        {
            /*LoggedIn = true;*/
            if (Connected)
            {
                if (BilibiliLoginConfig.Instance.BilibiliChannelId != _lastChannel)
                {
                    if (_lastChannel != 0)
                        PartChannel(_lastChannel);
                    if (BilibiliLoginConfig.Instance.BilibiliChannelId != 0)
                        BilibiliAPI.GetChannelConfig(BilibiliLoginConfig.Instance.BilibiliChannelId);

                    // Invoke OnBilibiliChannelUpdated event
                    if(OnBilibiliChannelUpdated != null)
                    {
                        foreach(var e in OnBilibiliChannelUpdated.GetInvocationList())
                        {
                            try
                            {
                                e?.DynamicInvoke(BilibiliLoginConfig.Instance.BilibiliChannelId);
                            }
                            catch (Exception ex)
                            {
                                /*Plugin.Log("[Bilibili Instance Config Changed Event]" + ex.ToString());*/
                            }
                        }
                    }
                }
                _lastChannel = BilibiliLoginConfig.Instance.BilibiliChannelId;
            }

            // Invoke OnConfigUpdated event
            if (OnConfigUpdated != null)
            {
                foreach (var e in OnConfigUpdated.GetInvocationList())
                {
                    try
                    {
                        e?.DynamicInvoke();
                    }
                    catch (Exception ex)
                    {
                        /*Plugin.Log("[Bilibili Instance Config Changed Event onConfigUpdated]" + ex.ToString());*/
                    }
                }
            }
        }

        /// <summary>
        /// Shuts down the websocket client, called internally. There is no need to call this function.
        /// </summary>
        public static void Shutdown()
        {
            /*Plugin.Log(Connected.ToString() + " " + _ws.IsConnected.ToString());*/
            if (Connected)
            {
                Connected = false;
                if (_ws.IsConnected) {
                    try
                    {
                        Plugin.Log("Terminating Bilibili Websocket...");
                        _ws.Close();
                    }
                    catch (WebSocketException wse)
                    {
                        /*Plugin.Log("[Bilibili Shutdown] code: " + wse.Code.ToString() + ", data:" + wse.Data.ToString());*/
                    }
                    catch (Exception ex) {
                        /*Plugin.Log("[Bilibili Shutdown] " + ex.InnerException.ToString());*/
                    }
                }
            }
        }

        private static void Connect(bool isManualReconnect = false)
        {
            if (Globals.IsApplicationExiting)
                return;

            Plugin.Log("Reconnecting!");

            try
            {
                if (_ws != null && _ws.IsConnected)
                {
                    Plugin.Log("Closing existing connnection to Bilibili!");
                    _ws.Close();
                }
            }
            catch (Exception ex)
            {
                /*Plugin.Log("[Bilibili Connect 1] " + ex.ToString());*/
            }
            _fullReconnects++;

            try
            {
                // Create our websocket object and setup the callbacks
                using (_ws = new WebSocket("wss://" + DanmukuServer + "/sub"))
                {
                    _ws.OnOpen += (sender, e) =>
                    {
                        if (SendJoinChannel(RealBilibiliChannelId))
                        {
                            Plugin.Log("Connected to Bilibili!");
                            if (_lastChannel != BilibiliLoginConfig.Instance.BilibiliChannelId)
                                _lastChannel = BilibiliLoginConfig.Instance.BilibiliChannelId;
                        }

                        // Display a message in the chat informing the user whether or not the connection to the channel was successful
                        ConnectionTime = DateTime.UtcNow;

                        // Invoke OnConnected event
                        if (OnConnected != null)
                        {
                            foreach (var ev in OnConnected.GetInvocationList())
                            {
                                try
                                {
                                    ev?.DynamicInvoke();
                                }
                                catch (Exception ex)
                                {
                                    /*Plugin.Log("[Bilibili Connect 2] " + ex.ToString());*/
                                }
                            }
                        }
                        Connected = true;

                        if (!updateInfoShown) {
                            Update.GetPluginUpdateInfo();
                        }
                    };

                    _ws.OnClose += (sender, e) =>
                    {
                        Plugin.Log("Bilibili connection terminated.");
                        LoggedIn = false;
                        Connected = false;
                    };

                    _ws.OnError += (sender, e) =>
                    {
                        Plugin.Log($"An error occurred in the bilibili connection! Error: {e.Message}, Exception: {e.Exception}");
                        LoggedIn = false;
                        Connected = false;
                    };

                    _ws.OnMessage += Ws_OnMessage;

                    // Then start the connection
                    _ws.Connect();

                    // Create a new task to reconnect automatically if the connection dies for some unknown reason
                    Task.Run(() =>
                    {
                        while (Connected && _ws.ReadyState == WebSocketState.Open)
                        {
                            //Plugin.Log("Connected and alive!");
                            Thread.Sleep(500);
                        }

                        // Invoke OnDisconnected event
                        if (OnDisconnected != null)
                        {
                            foreach (var ev in OnDisconnected.GetInvocationList())
                            {
                                try
                                {
                                    ev?.DynamicInvoke();
                                }
                                catch (Exception ex)
                                {
                                    /*Plugin.Log("[Bilibili Connect 3] " + ex.ToString());*/
                                }
                            }
                        }

                        if (!isManualReconnect)
                        {
                            Thread.Sleep(Math.Min(_reconnectCooldown *= 2, 120000));
                            Connect();
                        }
                    });
                    ProcessSendQueue(_fullReconnects);
                }
            }
            catch (ThreadAbortException)
            {
                // This usually gets hit if our application is exiting or something
                return;
            }
            catch (Exception ex)
            {
                /*Plugin.Log("[Bilibili Connect 4] " + ex.ToString());*/
                // Try to reconnect for any exception in the websocket client other than a ThreadAbortException
                Thread.Sleep(Math.Min(_reconnectCooldown *= 2, 120000));
                Connect();
            }
        }

        private static void ProcessSendQueue(int fullReconnects)
        {
            while(!Globals.IsApplicationExiting && _fullReconnects == fullReconnects)
            {
                if (LoggedIn && _ws.ReadyState == WebSocketState.Open)
                {
                    if (_sendLimitResetTime < DateTime.UtcNow)
                    {
                        _messagesSent = 0;
                        _sendLimitResetTime = DateTime.UtcNow.AddSeconds(_sendResetInterval);
                    }

                    if (_sendQueue.Count > 0)
                    {
                        if (_messagesSent < _messageLimit && _sendQueue.TryDequeue(out var fullMsg))
                        {
                            // Split off the assembly hash, we'll use this in the callback we invoke to filter out calls to the assembly that created the callback.
                            string assembly = fullMsg.Key.ToString();
                            string msg = fullMsg.Value;

                            // Send the message, then invoke the received callback for all the other assemblies
                            try { _ws.Send(msg); } catch (Exception ex){ }
                            OnMessageReceived(System.Text.Encoding.UTF8.GetBytes(msg), assembly);
                            _messagesSent++;

                        }
                    }
                }
                Thread.Sleep(250);
            }
            Plugin.Log("Exiting!");
        }

        /*
        // Prepend the assembly hash code before adding it to the send queue, to be used in identifying the assembly for our callback
        private static void SendRawInternal(Assembly assembly, string msg)
        {
            if (LoggedIn && _ws.ReadyState == WebSocketState.Open && msg.Length > 0)
                _sendQueue.Enqueue(new KeyValuePair<int, string>(assembly.GetHashCode(), msg));
        }*/

        /// <summary>
        /// Prepends a non-breaking zero-width space to the beginning of the message (\uFEFF).
        /// </summary>
        /// <param name="msg">The message to prepend the escape character to.</param>
        /// <returns>The escaped message.</returns>
        private static string Escape(string msg)
        {
            return $"\uFEFF{msg}";
        }
 
        /// <summary>
        /// Sends a raw message to the Bilibili server.
        /// </summary>
        /// <param name="msg">The raw message to be sent.</param>
        public static void SendRawMessage(string msg)
        {
        // TODO: sendAPI;
            /*SendRawInternal(Assembly.GetCallingAssembly(), msg);*/
        }

        /// <summary>
        /// Exits the specified Bilibili channel. *NOTE* You cannot part from the channel defined in BilibiliLoginConfig.ini!
        /// </summary>
        /// <param name="channelId">The Bilibili channel name to part from.</param>
        public static void PartChannel(int channelId)
        {
            if (channelId == BilibiliLoginConfig.Instance.BilibiliChannelId)
            {
                throw new Exception("Cannot part from the channel defined in BilibiliLoginConfig.ini.");
            }
            /*SendRawInternal(Assembly.GetCallingAssembly(), $"PART #{channelId}");*/
        }
        
        private static void OnMessageReceived(byte[] rawMessage, string assemblyHash = "")
        {
            int read = 0;
            int packetLength = EndianBitConverter.BigEndian.ToInt32(rawMessage, 0);
            int headerLength = EndianBitConverter.BigEndian.ToInt16(rawMessage, 4);
            int protocalVersion = EndianBitConverter.BigEndian.ToInt16(rawMessage, 6);
            int action = EndianBitConverter.BigEndian.ToInt32(rawMessage, 8);
            int parameter = EndianBitConverter.BigEndian.ToInt32(rawMessage, 12);
            
            if (protocalVersion == 1) {
                formatDanmuku(SubBuffer(rawMessage, headerLength, packetLength - headerLength), action); // For popularity
            }
            else if (protocalVersion == 2) { // compressed Buffer
                if (action == 5) {
                    MemoryStream deflatedStream = new MemoryStream();
                    new DeflateStream(new MemoryStream(SubBuffer(rawMessage, headerLength, packetLength - headerLength), 2, packetLength - headerLength - 2), CompressionMode.Decompress).CopyTo(deflatedStream);

                    byte[] newRawMessage = deflatedStream.ToArray();
                    while (read < newRawMessage.Length)
                    {
                        int packetLength2 = EndianBitConverter.BigEndian.ToInt32(newRawMessage, 0 + read);
                        int headerLength2 = EndianBitConverter.BigEndian.ToInt16(newRawMessage, 4 + read);
                        int protocalVersion2 = EndianBitConverter.BigEndian.ToInt16(newRawMessage, 6 + read);
                        int action2 = EndianBitConverter.BigEndian.ToInt32(newRawMessage, 8 + read);
                        int parameter2 = EndianBitConverter.BigEndian.ToInt32(newRawMessage, 12 + read);

                        formatDanmuku(SubBuffer(newRawMessage, read + headerLength2, packetLength2 - headerLength2), action2);

                        read += packetLength2;
                    }
                }
                else
                {
                    formatDanmuku(SubBuffer(rawMessage, headerLength, packetLength - headerLength), action);
                }
            }
            else if (protocalVersion == 0) { // Plain Json text
                if (action == 5)
                {
                    while (read < rawMessage.Length)
                    {
                        int packetLength2 = EndianBitConverter.BigEndian.ToInt32(rawMessage, 0 + read);
                        int headerLength2 = EndianBitConverter.BigEndian.ToInt16(rawMessage, 4 + read);
                        int protocalVersion2 = EndianBitConverter.BigEndian.ToInt16(rawMessage, 6 + read);
                        int action2 = EndianBitConverter.BigEndian.ToInt32(rawMessage, 8 + read);
                        int parameter2 = EndianBitConverter.BigEndian.ToInt32(rawMessage, 12 + read);

                        formatDanmuku(SubBuffer(rawMessage, read + headerLength2, packetLength2 - headerLength2), action2);

                        read += packetLength2;
                    }
                }
                else {
                    formatDanmuku(SubBuffer(rawMessage, headerLength, packetLength - headerLength), action);
                }
            }
        }

        private static void formatDanmuku(byte[] buffer, int action) {
            string json = "Empty";
            BilibiliMessage bilibiliMsg = new BilibiliMessage();
            bilibiliMsg.message = "";
            bilibiliMsg.messageType = "";
            Plugin.Log("Action: " + action.ToString());
            
            switch (action)
            {
                case 8:
                    int channelId = BilibiliLoginConfig.Instance.BilibiliChannelId;
                    if (RealBilibiliChannelId != 0)
                    {
                        channelId = RealBilibiliChannelId;
                    }
                    bilibiliMsg.MessageType = "join_channel";
                    bilibiliMsg.message = "【系统】已连接上房间 " + channelId + " (゜-゜)つロ 干杯~";
                    bilibiliMsg.messageType = "Bilibili#liveChatMessage";
                    LoggedIn = true;
                    Connected = true;
                    HeartbeatLoop();
                    break;
                case 3:
                    bilibiliMsg.MessageType = "popularity";
                    int popularity = EndianBitConverter.BigEndian.ToInt32(buffer, 0);
                    if (popularity > 10000)
                        json = (popularity / 10000.0) + "w";
                    else if (popularity > 1000)
                        json = (popularity / 1000.0) + "k";
                    else
                        json = popularity + "";
                    bilibiliMsg.message = "【人气】当前人气值: " + json;
                    bilibiliMsg.messageType = "Bilibili#liveChatMessage";
                    break;
                case 5:
                    json = Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                    var danmuku = JSON.Parse(json);
                    Plugin.Log(danmuku.ToString());
                    switch (danmuku["cmd"].Value)
                    {
                        case "DANMU_MSG":
                            if (danmuku["info"][2][2].Value == "1" || danmuku["info"][2][0].Value == BilibiliChannelMaster) {
                                if (danmuku["info"][1].Value == "!clr")
                                {
                                    Plugin.Log("Receive request to clear danmuku!");
                                    bilibiliMsg.MessageType = "StreamCoreCMD_ClearMsg";
                                    if (danmuku["info"][2][2].Value == "1")
                                    {
                                        bilibiliMsg.message = $"房管 {danmuku["info"][2][1].Value} 清除了弹幕";
                                    }
                                    else
                                    {
                                        bilibiliMsg.message = $"主播 {danmuku["info"][2][1].Value} 清除了弹幕";
                                    }
                                }
                                else if (danmuku["info"][1].Value.StartsWith("!ban_usr "))
                                {
                                    bilibiliMsg.MessageType = "StreamCoreCMD_DeleteMsgByUser";
                                    bilibiliMsg.message = danmuku["info"][1].Value.Substring(9);
                                    Plugin.Log($"Receive request to delete danmuku contains username({bilibiliMsg.message})!");
                                }
                                else if (danmuku["info"][1].Value.StartsWith("!ban_word "))
                                {
                                    bilibiliMsg.MessageType = "StreamCoreCMD_DeleteMsgByWord";
                                    bilibiliMsg.message = danmuku["info"][1].Value.Substring(10);
                                    Plugin.Log($"Receive request to delete danmuku contains keyword({bilibiliMsg.message})!");
                                }
                                else {
                                    bilibiliMsg.MessageType = "damuku";
                                    bilibiliMsg.message = danmuku["info"][2][1].Value + ": " + danmuku["info"][1].Value;
                                }
                            } else {
                                bilibiliMsg.MessageType = "damuku";
                                bilibiliMsg.message = danmuku["info"][2][1].Value + ": " + danmuku["info"][1].Value;
                                /*bilibiliMsg.message = "【弹幕】" + danmuku["info"][2][1].Value + ": " + danmuku["info"][1].Value;*/
                                if (BanListDetect(danmuku["info"][2][0].Value.ToString(), "uid") || BanListDetect(danmuku["info"][2][1].Value.ToString(), "username") || BanListDetect(danmuku["info"][1].Value.ToString(), "content"))
                                    bilibiliMsg.MessageType = "banned";
                            }
                            
                            break;
                        case "DANMU_MSG:4:0:2:2:2:0":
                            if ((danmuku["info"][1].Value == "!clr") && (danmuku["info"][2][2].Value == "1" || danmuku["info"][2][0].Value == BilibiliChannelMaster))
                            {
                                bilibiliMsg.MessageType = "StreamCoreCMD_ClearMsg";
                                bilibiliMsg.message = danmuku["info"][2][1].Value;
                            }
                            else
                            {
                                bilibiliMsg.MessageType = "damuku";
                                bilibiliMsg.message = danmuku["info"][2][1].Value + ": " + danmuku["info"][1].Value;
                                /*bilibiliMsg.message = "【弹幕】" + danmuku["info"][2][1].Value + ": " + danmuku["info"][1].Value;*/
                                if (BanListDetect(danmuku["info"][2][0].Value.ToString(), "uid") || BanListDetect(danmuku["info"][2][1].Value.ToString(), "username") || BanListDetect(danmuku["info"][1].Value.ToString(), "content"))
                                    bilibiliMsg.MessageType = "banned";
                            }

                            break;
                        case "SEND_GIFT":
                            bilibiliMsg.MessageType = "gift";
                            if (danmuku["data"]["combo_num"].Value == "")
                            {
                                bilibiliMsg.message = "【礼物】" + danmuku["data"]["uname"].Value + danmuku["data"]["action"].Value + danmuku["data"]["num"].Value + "个" + danmuku["data"]["giftName"].Value;
                            }
                            else
                            {
                                bilibiliMsg.message = "【礼物】" + danmuku["data"]["uname"].Value + danmuku["data"]["action"].Value + danmuku["data"]["num"].Value + "个" + danmuku["data"]["giftName"].Value + " x" + danmuku["data"]["combo_num"].Value;
                            }
                            if (BanListDetect(danmuku["data"]["uid"].Value.ToString(), "uid") || BanListDetect(danmuku["data"]["uname"].Value.ToString(), "username"))
                                bilibiliMsg.MessageType = "banned";
                            break;
                        case "COMBO_END":
                            bilibiliMsg.MessageType = "combo_end";
                            bilibiliMsg.message = "【连击】" + danmuku["data"]["uname"].Value + danmuku["data"]["action"].Value + danmuku["data"]["gift_num"].Value + "个" + danmuku["data"]["gift_name"].Value + " x" + danmuku["data"]["combo_num"].Value;
                            if (BanListDetect(danmuku["data"]["uid"].Value.ToString(), "uid") || BanListDetect(danmuku["data"]["uname"].Value.ToString(), "username"))
                                bilibiliMsg.MessageType = "banned";
                            break;
                        case "COMBO_SEND":
                            bilibiliMsg.MessageType = "Combo_send";
                            bilibiliMsg.message = "【连击】" + danmuku["data"]["uname"].Value + danmuku["data"]["action"].Value + danmuku["data"]["gift_num"].Value + "个" + danmuku["data"]["gift_name"].Value + " x" + danmuku["data"]["combo_num"].Value;
                            if (BanListDetect(danmuku["data"]["uid"].Value.ToString(), "uid") || BanListDetect(danmuku["data"]["uname"].Value.ToString(), "username"))
                                bilibiliMsg.MessageType = "banned";
                            break;
                        case "SUPER_CHAT_MESSAGE":
                            bilibiliMsg.MessageType = "super_chat";
                            bilibiliMsg.message = "【醒目留言】(￥" + danmuku["data"]["price"].Value + ") " + danmuku["data"]["user_info"]["uname"].Value + " 留言说: " + danmuku["data"]["message"].Value;
                            if (BanListDetect(danmuku["data"]["user_info"]["uid"].Value.ToString(), "uid") || BanListDetect(danmuku["data"]["user_info"]["uname"].Value.ToString(), "username") || BanListDetect(danmuku["data"]["message"].Value.ToString(), "content"))
                                bilibiliMsg.MessageType = "banned";
                            break;
                        case "SUPER_CHAT_MESSAGE_JPN":
                            bilibiliMsg.MessageType = "super_chat_japanese";
                            bilibiliMsg.message = "【スーパーチャット】(CNY￥" + danmuku["data"]["price"].Value + ") " + danmuku["data"]["user_info"]["uname"].Value + " は言う: " + danmuku["data"]["message_jpn"].Value;
                            if (BanListDetect(danmuku["data"]["user_info"]["uid"].Value.ToString(), "uid") || BanListDetect(danmuku["data"]["user_info"]["uname"].Value.ToString(), "username") || BanListDetect(danmuku["data"]["message"].Value.ToString(), "content"))
                                bilibiliMsg.MessageType = "banned";
                            break;
                        case "WELCOME":
                            bilibiliMsg.MessageType = "welcome";
                            bilibiliMsg.message = "【入场】" + "欢迎老爷" + danmuku["data"]["uname"].Value + "进入直播间";
                            if (BanListDetect(danmuku["data"]["uid"].Value.ToString(), "uid") || BanListDetect(danmuku["data"]["uname"].Value.ToString(), "username"))
                                bilibiliMsg.MessageType = "banned";
                            break;
                        case "INTERACT_WORD":
                            bilibiliMsg.MessageType = "welcome";
                            bilibiliMsg.message = "【入场】" + "欢迎" + danmuku["data"]["uname"].Value + "进入直播间";
                            if (BanListDetect(danmuku["data"]["uid"].Value.ToString(), "uid") || BanListDetect(danmuku["data"]["uname"].Value.ToString(), "username"))
                                bilibiliMsg.MessageType = "banned";
                            break;
                        case "WELCOME_GUARD":
                            bilibiliMsg.MessageType = "welcome_guard";
                            bilibiliMsg.message = "【舰队】" + "欢迎舰长" + danmuku["data"]["username"].Value + "进入直播间";
                            if (BanListDetect(danmuku["data"]["uid"].Value.ToString(), "uid") || BanListDetect(danmuku["data"]["uname"].Value.ToString(), "username"))
                                bilibiliMsg.MessageType = "banned";
                            break;
                        case "ENTRY_EFFECT":
                            bilibiliMsg.MessageType = "effect";
                            bilibiliMsg.message = "【特效】" + danmuku["data"]["copy_writing"].Value.Replace("<%", "").Replace("%>", "");
                            if (BanListDetect(danmuku["data"]["copy_writing"].Value.ToString(), "username"))
                                bilibiliMsg.MessageType = "banned";
                            break;
                        case "ROOM_RANK":
                            bilibiliMsg.MessageType = "global";
                            bilibiliMsg.message = "【打榜】" + danmuku["data"]["rank_desc"].Value;
                            break;
                        case "ACTIVITY_BANNER_UPDATE_V2":
                            bilibiliMsg.MessageType = "global";
                            bilibiliMsg.message = "【横幅】" + "当前分区排名" + danmuku["data"]["title"].Value;
                            break;
                        case "ROOM_REAL_TIME_MESSAGE_UPDATE":
                            bilibiliMsg.MessageType = "global";
                            bilibiliMsg.message = "【关注】" + "粉丝数:" + danmuku["data"]["fans"].Value;
                            break;
                        case "NOTICE_MSG":
                            bilibiliMsg.MessageType = "junk";
                            bilibiliMsg.message = "【喇叭】" + danmuku["data"]["msg_common"].Value;
                            break;
                        case "ANCHOR_LOT_START":
                            bilibiliMsg.MessageType = "anchor_lot_start";
                            bilibiliMsg.message = "【天选】" + "天选之子活动开始啦";
                            break;
                        case "ANCHOR_LOT_CHECKSTATUS":
                            bilibiliMsg.MessageType = "anchor_lot_checkstatus";
                            bilibiliMsg.message = "【天选】" + "天选之子活动开始啦";
                            break;
                        case "ANCHOR_LOT_END":
                            bilibiliMsg.MessageType = "anchor_lot_end";
                            bilibiliMsg.message = "【天选】" + "天选之子活动结束啦，奖品是" + danmuku["data"]["award_name"].Value;
                            break;
                        case "ANCHOR_LOT_AWARD":
                            bilibiliMsg.MessageType = "anchor_lot";
                            JSONArray list = danmuku["data"]["award_users"].AsArray;
                            string usernameList = "";
                            for (int i = 0; i < list.Count; i++)
                            {
                                usernameList += (BanListDetect(list[i]["uname"].Value.ToString(), "username") || BanListDetect(list[i]["uid"].Value.ToString(), "uid")) ? "【该用户已被过滤】" : list[i]["uname"].Value.ToString();
                            }
                            bilibiliMsg.message = "【天选】" + "恭喜" + usernameList + "获得" + danmuku["data"]["award_name"].Value;
                            break;
                        case "RAFFLE_START":
                            bilibiliMsg.MessageType = "raffle_start";
                            bilibiliMsg.message = "【抽奖】" + danmuku["data"]["title"].Value + "开始啦!";
                            break;
                        case "ROOM_BLOCK_MSG":
                            bilibiliMsg.MessageType = "blacklist";
                            bilibiliMsg.message = "【封禁】" + danmuku["data"]["uname"].Value + "(UID: " + danmuku["data"]["uid"].Value + ")";
                            break;
                        case "GUARD_BUY":
                            bilibiliMsg.MessageType = "new_guard";
                            bilibiliMsg.message = "【上舰】" + danmuku["data"]["username"].Value + "成为" + danmuku["data"]["gift_name"].Value + "进入舰队啦";
                            if (BanListDetect(danmuku["data"]["uid"].Value.ToString(), "uid") || BanListDetect(danmuku["data"]["username"].Value.ToString(), "username"))
                                bilibiliMsg.MessageType = "banned";
                            break;
                        case "USER_TOAST_MSG":
                            bilibiliMsg.MessageType = "new_guard_msg";
                            bilibiliMsg.message = "【上舰】" + danmuku["data"]["username"].Value + "开通了" + danmuku["data"]["num"].Value + "个" + danmuku["data"]["unit"].Value + "的" + danmuku["data"]["role_name"].Value + "进入舰队啦";
                            if (BanListDetect(danmuku["data"]["uid"].Value.ToString(), "uid") || BanListDetect(danmuku["data"]["username"].Value.ToString(), "username"))
                                bilibiliMsg.MessageType = "banned";
                            break;
                        case "GUARD_MSG":
                            if (danmuku["broadcast_type"].Value != "0")
                            {
                                bilibiliMsg.MessageType = "guard_msg";
                                bilibiliMsg.message = "【上舰】" + danmuku["data"]["msg"].Value.Replace(":?", "");
                                if (BanListDetect(danmuku["data"]["msg"].Value.ToString(), "username"))
                                    bilibiliMsg.MessageType = "banned";
                            }
                            else
                            {
                                bilibiliMsg.MessageType = "junk";
                                bilibiliMsg.message = "【上舰广播】" + danmuku["data"]["msg"].Value.Replace(":?", "");
                                if (BanListDetect(danmuku["data"]["msg"].Value.ToString(), "username") || BanListDetect(danmuku["data"]["msg"].Value.ToString(), "content"))
                                    bilibiliMsg.MessageType = "banned";
                            }
                            break;
                        case "GUARD_LOTTERY_START":
                            bilibiliMsg.MessageType = "guard_lottery_msg";
                            bilibiliMsg.message = "【抽奖】" + "上舰抽奖开始啦";
                            break;
                        case "ROOM_CHANGE":
                            bilibiliMsg.MessageType = "room_change";
                            bilibiliMsg.message = "【变更】" + "直播间名称为: " + danmuku["data"]["title"].Value;
                            break;
                        case "PREPARING":
                            bilibiliMsg.MessageType = "room_perparing";
                            bilibiliMsg.message = "【下播】" + "直播间准备中";
                            break;
                        case "LIVE":
                            bilibiliMsg.MessageType = "room_live";
                            bilibiliMsg.message = "【开播】" + "直播间开播啦";
                            break;
                        default:
                            bilibiliMsg.MessageType = "unkown";
                            bilibiliMsg.message = "【暂不支持该消息】";
                            Plugin.Log("Unsupport Message: " + danmuku.ToString());
                            break;
                    }
                    /*switch (danmuku["cmd"].Value)
                    {
                        case "DANMU_MSG":
                            bilibiliMsg.MessageType = "damuku";
                            bilibiliMsg.message = danmuku["info"][2][1].Value + ": " + danmuku["info"][1].Value;
                            break;
                        case "DANMU_MSG:4:0:2:2:2:0":
                            bilibiliMsg.MessageType = "damuku";
                            bilibiliMsg.message = danmuku["info"][2][1].Value + ": " + danmuku["info"][1].Value;
                            break;
                        case "SEND_GIFT":
                            bilibiliMsg.MessageType = "gift";
                            if (danmuku["data"]["combo_num"].Value == "")
                            {
                                bilibiliMsg.message = "🎁" + danmuku["data"]["uname"].Value + danmuku["data"]["action"].Value + danmuku["data"]["num"].Value + "个" + danmuku["data"]["giftName"].Value;
                            }
                            else
                            {
                                bilibiliMsg.message = "🎁" + danmuku["data"]["uname"].Value + danmuku["data"]["action"].Value + danmuku["data"]["num"].Value + "个" + danmuku["data"]["giftName"].Value + " x" + danmuku["data"]["combo_num"].Value;
                            }
                            break;
                        case "COMBO_END":
                            bilibiliMsg.MessageType = "combo_end";
                            bilibiliMsg.message = "🥊" + danmuku["data"]["uname"].Value + danmuku["data"]["action"].Value + danmuku["data"]["gift_num"].Value + "个" + danmuku["data"]["gift_name"].Value + " x" + danmuku["data"]["combo_num"].Value;
                            break;
                        case "COMBO_SEND":
                            bilibiliMsg.MessageType = "Combo_send";
                            bilibiliMsg.message = "🥊" + danmuku["data"]["uname"].Value + danmuku["data"]["action"].Value + danmuku["data"]["gift_num"].Value + "个" + danmuku["data"]["gift_name"].Value + " x" + danmuku["data"]["combo_num"].Value;
                            break;
                        case "SUPER_CHAT_MESSAGE":
                            bilibiliMsg.MessageType = "super_chat";
                            bilibiliMsg.message = "💴(￥" + danmuku["data"]["price"].Value + ") " + danmuku["data"]["user_info"]["uname"].Value + " 留言说: " + danmuku["data"]["message"].Value;
                            break;
                        case "SUPER_CHAT_MESSAGE_JPN":
                            bilibiliMsg.MessageType = "super_chat_japanese";
                            bilibiliMsg.message = "💴(CNY￥" + danmuku["data"]["price"].Value + ") " + danmuku["data"]["user_info"]["uname"].Value + " は言う: " + danmuku["data"]["message_jpn"].Value;
                            break;
                        case "WELCOME":
                            bilibiliMsg.MessageType = "welcome";
                            bilibiliMsg.message = "👏" + "欢迎" + danmuku["data"]["uname"].Value + "进入直播间";
                            break;
                        case "WELCOME_GUARD":
                            bilibiliMsg.MessageType = "welcome_guard";
                            bilibiliMsg.message = "⚓" + "欢迎舰长" + danmuku["data"]["username"].Value + "进入直播间";
                            break;
                        case "ENTRY_EFFECT":
                            bilibiliMsg.MessageType = "effect";
                            bilibiliMsg.message = "✨" + danmuku["data"]["copy_writing"].Value.Replace("<%", "").Replace("%>", "");
                            break;
                        case "ROOM_RANK":
                            bilibiliMsg.MessageType = "global";
                            bilibiliMsg.message = "🥇" + danmuku["data"]["rank_desc"].Value;
                            break;
                        case "ACTIVITY_BANNER_UPDATE_V2":
                            bilibiliMsg.MessageType = "global";
                            bilibiliMsg.message = "💴" + "当前分区排名" + danmuku["data"]["title"].Value;
                            break;
                        case "ROOM_REAL_TIME_MESSAGE_UPDATE":
                            bilibiliMsg.MessageType = "global";
                            bilibiliMsg.message = "❤" + "粉丝数:" + danmuku["data"]["fans"].Value;
                            break;
                        case "NOTICE_MSG":
                            bilibiliMsg.MessageType = "junk";
                            bilibiliMsg.message = "📢" + danmuku["data"]["msg_common"].Value;
                            break;
                        case "ANCHOR_LOT_START":
                            bilibiliMsg.MessageType = "anchor_lot_start";
                            bilibiliMsg.message = "👼" + "天选之子活动开始啦";
                            break;
                        case "ANCHOR_LOT_CHECKSTATUS":
                            bilibiliMsg.MessageType = "anchor_lot_checkstatus";
                            bilibiliMsg.message = "👼" + "天选之子活动开始啦";
                            break;
                        case "ANCHOR_LOT_END":
                            bilibiliMsg.MessageType = "anchor_lot_end";
                            bilibiliMsg.message = "👼" + "天选之子活动结束啦，奖品是" + danmuku["data"]["award_name"].Value;
                            break;
                        case "ANCHOR_LOT_AWARD":
                            bilibiliMsg.MessageType = "anchor_lot";
                            JSONArray list = danmuku["data"]["award_users"].AsArray;
                            string usernameList = "";
                            for (int i = 0; i < list.Count; i++)
                            {
                                usernameList = usernameList + list[i]["uname"];
                            }
                            bilibiliMsg.message = "👼" + "恭喜" + usernameList + "获得" + danmuku["data"]["award_name"].Value;
                            break;
                        case "RAFFLE_START":
                            bilibiliMsg.MessageType = "raffle_start";
                            bilibiliMsg.message = "🎰" + danmuku["data"]["title"].Value + "开始啦!";
                            break;
                        case "ROOM_BLOCK_MSG":
                            bilibiliMsg.MessageType = "blacklist";
                            bilibiliMsg.message = "⛔" + danmuku["data"]["uname"].Value + "(UID: " + danmuku["data"]["uid"].Value + ")";
                            break;
                        case "GUARD_BUY":
                            bilibiliMsg.MessageType = "new_guard";
                            bilibiliMsg.message = "⚓" + danmuku["data"]["username"].Value + "成为" + danmuku["data"]["gift_name"].Value + "进入舰队啦";
                            break;
                        case "USER_TOAST_MSG":
                            bilibiliMsg.MessageType = "new_guard_msg";
                            bilibiliMsg.message = "⚓" + danmuku["data"]["username"].Value + "开通了" + danmuku["data"]["num"].Value + "个" + danmuku["data"]["unit"].Value + "的" + danmuku["data"]["role_name"].Value + "进入舰队啦";
                            break;
                        case "GUARD_MSG":
                            if (danmuku["broadcast_type"].Value != "0")
                            {
                                bilibiliMsg.MessageType = "guard_msg";
                                bilibiliMsg.message = "⚓" + danmuku["data"]["msg"].Value.Replace(":?", "");
                            }
                            else
                            {
                                bilibiliMsg.MessageType = "junk";
                                bilibiliMsg.message = "📢" + danmuku["data"]["msg"].Value.Replace(":?", "");
                            }
                            break;
                        case "GUARD_LOTTERY_START":
                            bilibiliMsg.MessageType = "guard_lottery_msg";
                            bilibiliMsg.message = "🎁" + "上舰抽奖开始啦";
                            break;
                        case "ROOM_CHANGE":
                            bilibiliMsg.MessageType = "room_change";
                            bilibiliMsg.message = "🔃" + "直播间名称为: " + danmuku["data"]["title"].Value;
                            break;
                        case "PREPARING":
                            bilibiliMsg.MessageType = "room_perparing";
                            bilibiliMsg.message = "🔚" + "直播间准备中";
                            break;
                        case "LIVE":
                            bilibiliMsg.MessageType = "room_live";
                            bilibiliMsg.message = "🔛" + "直播间开播啦";
                            break;
                        default:
                            bilibiliMsg.MessageType = "unkown";
                            bilibiliMsg.message = "❔";
                            Plugin.Log("Unsupport Message: " + danmuku.ToString());
                            break;
                    }*/
                    bilibiliMsg.messageType = "Bilibili#liveChatMessage";
                    break;
                default:
                    bilibiliMsg.messageType = "Unkown";
                    bilibiliMsg.message = "Unsupported Message";
                    Plugin.Log("Unsupport Message: " + action);
                    break;
            }
            /*Plugin.Log(bilibiliMsg.message);*/
            if (showDanmuku(bilibiliMsg.MessageType))
            {
                Plugin.Log(bilibiliMsg.message);

                Plugin.Log("Update: " + Update.GetMessage());
                /*BilibiliMessage bilibiliMsgUpdateInfo = new BilibiliMessage();
                bilibiliMsgUpdateInfo.message = Update.GetMessage();
                bilibiliMsgUpdateInfo.MessageType = "Update";*/
                if (!updateInfoShown && bilibiliMsg.message != "")
                {
                    Plugin.Log("Show Update Info");
                    /*BilibiliMessageHandlers.InvokeHandler(bilibiliMsgUpdateInfo, "");*/
                    updateInfoShown = true;
/*                    if (Update.GetMessage() != "") {
                        bilibiliMsg.message += "\n" + Update.GetMessage();
                    }*/
                }

                BilibiliMessageHandlers.InvokeHandler(bilibiliMsg, "");
            }
        }
        
        private static void Ws_OnMessage(object sender, MessageEventArgs ev)
        {

            try
            {
                if (ev.IsPing)
                {
                    Plugin.Log("[Bilibili] Socket receive A Ping");
                } if (ev.IsText)
                {
                    Plugin.Log("[Bilibili] Socket receive A Text: " + ev);
                }
                else if (ev.IsBinary)
                {
/*                    Plugin.Log("[Bilibili] Socket receive A Binary: " + ev);
                    Plugin.Log("[Bilibili] Socket receive A Binary: " + ev.ToString());*/
                    OnMessageReceived(ev.RawData);
                }
                else {
                    Plugin.Log("[Bilibili] Socket receive Unkown data: ");
                }
            }
            catch (Exception ex)
            {
                /*Plugin.Log("[Bilibili Websocket] onmessage: " + ex.ToString());*/
            }
        }

        private static bool showDanmuku(string type) {
            if (type.Equals("join_channel") || type.Equals("StreamCoreCMD_ClearMsg") || type.Equals("StreamCoreCMD_DeleteMsgByUser") || type.Equals("StreamCoreCMD_DeleteMsgByWord")) {
                return true;
            }
            if ((type.Equals("damuku") || type.Equals("SUPER_CHAT_MESSAGE") || type.Equals("SUPER_CHAT_MESSAGE_JPY")) && BilibiliLoginConfig.Instance.danmuku == 1) {
                return true;
            }
            if (type.Equals("popularity") && BilibiliLoginConfig.Instance.popularity == 1)
            {
                return true;
            }
            if ((type.Equals("gift") || type.Equals("combo_end") || type.Equals("combo_send") || type.Equals("SUPER_CHAT_MESSAGE") || type.Equals("SUPER_CHAT_MESSAGE_JPY")) && BilibiliLoginConfig.Instance.gift == 1)
            {
                return true;
            }
            if ((type.Equals("new_guard") || type.Equals("new_guard_msg") || type.Equals("guard_msg")) && BilibiliLoginConfig.Instance.guard == 1)
            {
                return true;
            }
            if ((type.Equals("anchor_lot_start") || type.Equals("anchor_lot_checkstatus") || type.Equals("anchor_lot_end") || type.Equals("raffle_start")) && BilibiliLoginConfig.Instance.anchor == 1)
            {
                return true;
            }
            if ((type.Equals("welcome") || type.Equals("welcome_guard") || type.Equals("effect")) && BilibiliLoginConfig.Instance.welcome == 1)
            {
                return true;
            }
            if (type.Equals("global") && BilibiliLoginConfig.Instance.global == 1)
            {
                return true;
            }
            if (type.Equals("blacklist") && BilibiliLoginConfig.Instance.blacklist == 1)
            {
                return true;
            }
            if ((type.Equals("room_change") || type.Equals("room_live") || type.Equals("room_perparing")) && BilibiliLoginConfig.Instance.roomInfo == 1)
            {
                return true;
            }
            if ((type.Equals("junk") || type.Equals("unkown") || type.Equals("banned")) && BilibiliLoginConfig.Instance.junk == 1)
            {
                return true;
            }

            return false;
        }

        private static void HeartbeatLoop()
        {
            Task.Run(async () => {
                while (Connected)
                {
                    await SendHeartbeat();
                    Thread.Sleep(30000);
                }
            });
        }

         private static async Task SendHeartbeat()
        {
            /*Plugin.Log("[Bilibili] Send Beat heart");*/
            await SendSocketDataAsync(0, 16, protocalVersion, 2, 1, "");
        }

        /*Refernce https://github.com/copyliu/bililive_dm/blob/master/BiliDMLib/DanmakuLoader.cs*/

        private static async Task SendSocketDataAsync(int packetlength, short magic, short ver, int action, int param = 1, string body = "")
        {
            var playload = Encoding.UTF8.GetBytes(body);
            if (packetlength == 0)
            {
                packetlength = playload.Length + 16;
            }
            var buffer = new byte[packetlength];
            using (var ms = new MemoryStream(buffer))
            {
                var b = EndianBitConverter.BigEndian.GetBytes(buffer.Length);

                await ms.WriteAsync(b, 0, 4);
                b = EndianBitConverter.BigEndian.GetBytes(magic);
                await ms.WriteAsync(b, 0, 2);
                b = EndianBitConverter.BigEndian.GetBytes(ver);
                await ms.WriteAsync(b, 0, 2);
                b = EndianBitConverter.BigEndian.GetBytes(action);
                await ms.WriteAsync(b, 0, 4);
                b = EndianBitConverter.BigEndian.GetBytes(param);
                await ms.WriteAsync(b, 0, 4);
                if (playload.Length > 0)
                {
                    ms.Write(playload, 0, playload.Length);
                }
                try {
                    _ws.SendAsync(buffer, null);
                    /*_ws.Send(buffer);*/
                } catch (Exception ex) { }
            }
        }

        private static bool SendJoinChannel(int channelId)
        {
            BilibiliAPI.GetChannelConfig(channelId);
            JSONObject payload = new JSONObject();
            Random r = new Random();
            payload.Add("roomid", channelId);
            payload.Add("uid", (long)(1e14 + 2e14 * r.NextDouble()));
            payload.Add("token", DanmukuToken);
            Plugin.Log("[Bilibili] Send Socket Data of: " + payload.ToString());
            Task.Run(async () => {
                await SendSocketDataAsync(0, 16, protocalVersion, 7, 1, payload.ToString());
            });
            return true;
        }

        // Reference https://github.com/copyliu/bililive_dm/blob/master/BiliDMLib/utils.cs
        /*private static byte[] ToBE(byte[] b)
        {
            if (BitConverter.IsLittleEndian)
            {
                return b.Reverse().ToArray();
            }
            else
            {
                return b;
            }
        }

        private static int BufferToInt(byte[] buffer, int offset, int count)
        {
            if (offset + count > buffer.Length)
                Plugin.Log("[Bilibili] The buffer is too small.");

            byte[] hex = new byte[count];

            for (int i = offset; i < offset + count; i++) {
                hex[i - offset] = buffer[i];
            }
            if (BitConverter.IsLittleEndian)
                Array.Reverse(hex);

            int value = 0;

            if (count == 2) {
                value = BitConverter.ToInt16(hex, 0);
            } else if (count == 4) {
                value = BitConverter.ToInt32(hex, 0);
            }
            return value;
        }*/

        private static byte[] SubBuffer(byte[] buffer, int offset, int count)
        {
            if (offset + count <= buffer.Length) {
                byte[] newBuffer = new byte[count];
                for (int i = offset; i < offset + count; i++)
                {
                    newBuffer[i - offset] = buffer[i];
                }
                return newBuffer;
            }
            return new byte[0];
        }

        private static bool BanListDetect(string content, string type)
        {
            foreach (string rule in banListRule[type])
            {
                if (rule.Contains(rule))
                    return true;
            }
            return false;
        }
    }
}
