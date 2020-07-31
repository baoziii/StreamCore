using StreamCore.Chat;
using StreamCore.Config;
using StreamCore.SimpleJSON;
using StreamCore.Twitch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.Bilibili
{
    public class BilibiliAPI
    {
        private static readonly string BilibiliChannelInfoApi = "https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom?room_id="; 
        private static readonly string BilibiliChannelConfigApi = "https://api.live.bilibili.com/room/v1/Danmu/getConf?room_id=";
        private static readonly string BilibiliLiveUserInfoApi = "https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByUser";
        
        private static HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(5) };

        /// <summary>
        /// Get the specified Bilibili channel config.
        /// </summary>
        /// <param name="channelId">The Bilibili channel name to join.</param>
        public async static void GetChannelConfig(int channelId)
        {
            try
            {
                var NewChannelInfo = JSONObject.Parse(await httpClient.GetStringAsync(BilibiliChannelInfoApi + channelId));
                if (NewChannelInfo["data"]["room_info"]["room_id"] != String.Empty)
                {
                    BilibiliWebSocketClient.BilibiliChannelMaster = NewChannelInfo["data"]["room_info"]["uid"].ToString();
                    channelId = int.Parse(NewChannelInfo["data"]["room_info"]["room_id"]);
                    BilibiliWebSocketClient.RealBilibiliChannelId = channelId;
                }
                var NewLiveConfig = JSONObject.Parse(await httpClient.GetStringAsync(BilibiliChannelConfigApi + channelId));
                if (NewLiveConfig["data"]["host"] != String.Empty)
                {
                    BilibiliWebSocketClient.DanmukuServer = NewLiveConfig["data"]["host"];
                    BilibiliWebSocketClient.DanmukuServerPort = NewLiveConfig["data"]["port"];
                    BilibiliWebSocketClient.DanmukuToken = NewLiveConfig["data"]["token"];
                }
            }
            catch { }

            /*SendRawInternal(Assembly.GetCallingAssembly(), $"JOIN #{channelId}");*/
        }

        public static void GetRoomsForChannelAsync(BilibiliChannel channel, Action<bool, BilibiliChannel> onCompleted)
        {
            Task.Run(() =>
            {
                bool success = GetRoomsForChannel(channel) != null;
                onCompleted?.Invoke(success, channel);
            });
        }

        public static List<BilibiliRoom> GetRoomsForChannel(BilibiliChannel channel)
        {
            if (!TwitchWebSocketClient.LoggedIn)
            {
                return null;
            }

            Plugin.Log($"Getting rooms for channel {channel.name}");

            try
            {
                /*HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"https://api.twitch.tv/kraken/chat/{channel.roomId}/rooms");
                request.Credentials = CredentialCache.DefaultCredentials;

                request.Method = "GET";
                request.Accept = "application/vnd.twitchtv.v5+json";
                request.Headers.Set("Authorization", $"OAuth {TwitchLoginConfig.Instance.TwitchOAuthToken.Replace("oauth:", "")}");
                request.Headers.Set("Client-ID", ClientId);

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream dataStream = response.GetResponseStream();
                    StreamReader reader = new StreamReader(dataStream);

                    channel.rooms = TwitchRoom.FromJson(reader.ReadToEnd());

                    foreach (TwitchRoom r in channel.rooms)
                        Plugin.Log($"Room: {r.name}, ChannelName: {r.channelName}");

                    reader.Close();
                }
                response.Close();*/

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(BilibiliChannelInfoApi + channel.roomId);

                request.Method = "GET";

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream dataStream = response.GetResponseStream();
                    StreamReader reader = new StreamReader(dataStream);

                    channel.rooms = BilibiliRoom.FromJson(reader.ReadToEnd());

                    reader.Close();
                }
                response.Close();
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
            return channel.rooms;
        }
    }
}
