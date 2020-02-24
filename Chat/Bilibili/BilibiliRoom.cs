using StreamCore.SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.Bilibili
{
    public class BilibiliRoom
    {
        public string id;
        public string ownerId;
        public string name;
        public string topic;
        public string channelName
        {
            get
            {
                return $"chatrooms:{ownerId}:{id}";
            }
        }

        public static List<BilibiliRoom> FromJson(string json)
        {
            if (json == string.Empty)
                return new List<BilibiliRoom>();

            JSONNode liveConfig = JSON.Parse(json);
            if (liveConfig == null || liveConfig.IsNull)
                return new List<BilibiliRoom>();

            List<BilibiliRoom> rooms = new List<BilibiliRoom>();
            /*            if (NewChannelInfo["data"]["room_info"]["room_id"] != String.Empty)
                        {
                            channelId = int.Parse(NewChannelInfo["data"]["room_info"]["room_id"]);
                            BilibiliWebSocketClient.RealBilibiliChannelId = channelId;
                        }
                        var NewLiveConfig = JSONObject.Parse(await httpClient.GetStringAsync(BilibiliChannelConfigApi + channelId));
                        if (NewLiveConfig["data"]["host"] != String.Empty)
                        {
                            BilibiliWebSocketClient.DanmukuServer = NewLiveConfig["data"]["host"];
                            BilibiliWebSocketClient.DanmukuServerPort = NewLiveConfig["data"]["port"];
                        }*/
            rooms.Add(new BilibiliRoom()
            {
                id = liveConfig["data"]["room_info"]["room_id"],
                ownerId = liveConfig["data"]["room_info"]["uid"],
                name = liveConfig["data"]["room_info"]["title"],
                topic = liveConfig["data"]["room_info"]["area_name"]
            });
            return rooms;
        }
    }
}
